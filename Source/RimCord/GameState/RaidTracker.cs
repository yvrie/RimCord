using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimCord.GameState
{
    public static class RaidTracker
    {
        private class RaidEventInfo
        {
            public int MapId;
            public Faction Faction;
            public string FactionName;
            public DateTime StartedAtUtc;
            public int StartedAtTick;
            public IncidentParms IncidentParms;
        }

        private static readonly Dictionary<int, RaidEventInfo> activeRaids = new Dictionary<int, RaidEventInfo>();
        private static readonly List<Lord> raidLordsBuffer = new List<Lord>(8);
        private static string lastRaidFactionName;
        private static DateTime? lastRaidEventTime = null;
        private static bool cachedIsRaidActive;
        private static int cachedRaidCheckTick = -1;
        private const int RaidCacheValidityTicks = 60;

        public static bool IsRaidActive()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                Reset();
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int tickDiff = currentTick - cachedRaidCheckTick;
            if (cachedRaidCheckTick >= 0 && tickDiff >= 0 && tickDiff < RaidCacheValidityTicks)
            {
                return cachedIsRaidActive;
            }

            cachedRaidCheckTick = currentTick;
            cachedIsRaidActive = ComputeIsRaidActive(map);
            return cachedIsRaidActive;
        }

        private static bool ComputeIsRaidActive(Map map)
        {
            bool hasActiveRaid = HasActiveRaidIncident(map);
            if (!hasActiveRaid)
            {
                var trackedInfo = GetRaidInfoForMap(map);
                if (trackedInfo != null && HasAnyRaidPawn(map, trackedInfo.Faction))
                    hasActiveRaid = true;
            }

            if (hasActiveRaid)
            {
                if (!lastRaidEventTime.HasValue)
                {
                    var info = GetRaidInfoForMap(map);
                    lastRaidEventTime = info != null ? info.StartedAtUtc : DateTime.UtcNow;
                }

                if (string.IsNullOrEmpty(lastRaidFactionName))
                {
                    lastRaidFactionName = GetRaidFactionNameFromMap(map);
                }

                return true;
            }

            bool removed = ForgetRaid(map);
            if (removed || lastRaidEventTime.HasValue)
            {
                string mapLabel = map?.Parent?.Label ?? map?.ToStringSafe() ?? "Unknown map";
                RimCordLogger.SilentInfo("Raid cleared on {0}", mapLabel);
            }

            lastRaidFactionName = null;
            lastRaidEventTime = null;
            return false;
        }

        public static string GetRaidFactionName()
        {
            if (!string.IsNullOrEmpty(lastRaidFactionName))
            {
                return lastRaidFactionName;
            }

            var map = Find.CurrentMap;
            if (map != null)
            {
                string currentFactionName = GetRaidFactionNameFromMap(map);
                if (!string.IsNullOrEmpty(currentFactionName))
                {
                    lastRaidFactionName = currentFactionName;
                    return currentFactionName;
                }
            }

            return null;
        }

        public static DateTime? GetLastRaidStartTime()
        {
            return lastRaidEventTime;
        }

        public static void NotifyRaidTriggered(IncidentParms parms)
        {
            var map = parms?.target as Map;
            if (map == null)
            {
                return;
            }

            RememberRaid(map, parms.faction, parms, overwrite: true);

            string mapLabel = map?.Parent?.Label ?? map?.ToStringSafe() ?? "Unknown map";
            string factionName = parms.faction?.Name ?? parms.faction?.def?.label ?? "Unknown faction";
            RimCordLogger.SilentInfo("Raid incident triggered on {0}: {1}", mapLabel, factionName);
        }

        private static bool HasActiveRaidIncident(Map map)
        {
            if (map == null)
                return false;

            if (HasRaidLord(map))
                return true;

            if (CheckForRaidPawns(map))
                return true;

            var info = GetRaidInfoForMap(map);
            if (info != null && HasVisibleFactionRaiders(map, info.Faction))
                return true;

            if (HasAnyHostileAttackers(map))
                return true;

            return false;
        }

        private static bool HasAnyHostileAttackers(Map map)
        {
            if (map == null)
                return false;

            var pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
                return false;

            var playerFaction = Faction.OfPlayer;

            foreach (var pawn in pawns)
            {
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
                    continue;

                var faction = pawn.Faction;
                if (faction == null || faction == playerFaction)
                    continue;

                if (ShouldIgnoreFaction(faction))
                    continue;

                if (!faction.HostileTo(playerFaction))
                    continue;

                if (pawn.InMentalState)
                    continue;

                var lord = pawn.GetLord();
                if (lord != null)
                {
                    var lordJob = lord.LordJob;
                    if (lordJob != null)
                    {
                        string jobTypeName = lordJob.GetType().Name;
                        if (ContainsIgnoreCase(jobTypeName, "assault") || ContainsIgnoreCase(jobTypeName, "attack") ||
                            ContainsIgnoreCase(jobTypeName, "siege") || ContainsIgnoreCase(jobTypeName, "breach") ||
                            ContainsIgnoreCase(jobTypeName, "raid") || ContainsIgnoreCase(jobTypeName, "kidnap") ||
                            ContainsIgnoreCase(jobTypeName, "steal"))
                        {
                            RememberRaid(map, faction);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasActiveVisiblePawns(Lord lord, Map map)
        {
            if (lord?.ownedPawns == null)
            {
                return false;
            }

            foreach (var pawn in lord.ownedPawns)
            {
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
                {
                    continue;
                }

                if (ShouldDelayPawnRaidDetection(pawn, map))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool ShouldDelayPawnRaidDetection(Pawn pawn, Map map)
        {
            if (pawn == null || map == null)
            {
                return false;
            }

            var fogGrid = map.fogGrid;
            if (fogGrid != null && fogGrid.IsFogged(pawn.Position))
            {
                return true;
            }

            var dormantComp = pawn.GetComp<CompCanBeDormant>();
            if (dormantComp != null && !dormantComp.Awake)
            {
                return true;
            }

            return false;
        }

        private static bool HasRaidLord(Map map)
        {
            var raidLords = GetActiveRaidLords(map);
            if (raidLords.Count == 0)
            {
                return false;
            }

            foreach (var lord in raidLords)
            {
                if (!HasActiveVisiblePawns(lord, map))
                {
                    continue;
                }

                var faction = lord?.faction;
                if (ShouldIgnoreFaction(faction))
                {
                    continue;
                }

                if (faction != null)
                {
                    RememberRaid(map, faction);
                    break;
                }
            }

            return true;
        }

        private static bool CheckForRaidPawns(Map map)
        {
            if (map == null)
                return false;

            try
            {
                var allPawns = map.mapPawns?.AllPawnsSpawned;
                if (allPawns == null)
                    return false;

                foreach (var pawn in allPawns)
                {
                    if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
                        continue;

                    if (ShouldDelayPawnRaidDetection(pawn, map))
                    {
                        continue;
                    }

                    var lord = pawn.GetLord();
                    if (lord == null || !IsRaidLord(lord))
                        continue;

                    var faction = lord.faction ?? pawn.Faction;
                    if (ShouldIgnoreFaction(faction))
                    {
                        continue;
                    }
                    RememberRaid(map, faction);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in CheckForRaidPawns: {0}", ex.Message);
                return false;
            }
        }

        private static bool HasVisibleFactionRaiders(Map map, Faction faction)
        {
            if (map == null || faction == null)
            {
                return false;
            }

            var pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return false;
            }

            foreach (var pawn in pawns)
            {
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
                {
                    continue;
                }

                if (pawn.Faction != faction)
                {
                    continue;
                }

                if (ShouldIgnoreFaction(pawn.Faction))
                {
                    continue;
                }

                if (ShouldDelayPawnRaidDetection(pawn, map))
                    continue;

                return true;
            }

            return false;
        }

        private static bool HasAnyRaidPawn(Map map, Faction faction)
        {
            if (map == null || faction == null)
            {
                return false;
            }

            var pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return false;
            }

            foreach (var pawn in pawns)
            {
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
                {
                    continue;
                }

                if (pawn.Faction != faction)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static string GetRaidFactionNameFromMap(Map map)
        {
            if (map == null)
                return null;

            var info = GetRaidInfoForMap(map);
            if (info != null)
            {
                if (!string.IsNullOrEmpty(info.FactionName))
                {
                    return info.FactionName;
                }

                if (info.Faction != null)
                {
                    string displayName = GetFactionDisplayName(info.Faction);
                    info.FactionName = displayName;
                    return displayName;
                }
            }

            var raidLords = GetActiveRaidLords(map);
            foreach (var lord in raidLords)
            {
                var faction = lord?.faction;
                if (ShouldIgnoreFaction(faction))
                {
                    continue;
                }

                if (faction != null)
                {
                    return GetFactionDisplayName(faction);
                }
            }

            return null;
        }

        private static void RememberRaid(Map map, Faction faction, IncidentParms parms = null, bool overwrite = false)
        {
            if (map == null)
                return;

            RaidEventInfo info;
            bool hasExisting = activeRaids.TryGetValue(map.uniqueID, out info);

            if (!hasExisting || overwrite)
            {
                info = new RaidEventInfo
                {
                    MapId = map.uniqueID,
                    StartedAtUtc = DateTime.UtcNow,
                    StartedAtTick = (Find.TickManager != null) ? Find.TickManager.TicksGame : 0
                };
                activeRaids[map.uniqueID] = info;
                lastRaidEventTime = info.StartedAtUtc;
            }

            if (faction != null)
            {
                info.Faction = faction;
                info.FactionName = GetFactionDisplayName(faction);
                lastRaidFactionName = info.FactionName;
            }

            if (parms != null)
            {
                info.IncidentParms = parms;
            }

            if (!lastRaidEventTime.HasValue)
            {
                lastRaidEventTime = info.StartedAtUtc;
            }
        }

        private static bool ForgetRaid(Map map)
        {
            if (map == null)
                return false;

            return activeRaids.Remove(map.uniqueID);
        }

        private static RaidEventInfo GetRaidInfoForMap(Map map)
        {
            if (map == null)
                return null;

            activeRaids.TryGetValue(map.uniqueID, out var info);
            return info;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source != null && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<Lord> GetActiveRaidLords(Map map)
        {
            raidLordsBuffer.Clear();
            if (map == null || map.lordManager == null)
                return raidLordsBuffer;

            var lords = map.lordManager.lords;
            if (lords == null)
                return raidLordsBuffer;

            foreach (var lord in lords)
            {
                if (IsRaidLord(lord))
                    raidLordsBuffer.Add(lord);
            }

            return raidLordsBuffer;
        }

        private static bool IsRaidLord(Lord lord)
        {
            if (lord == null)
                return false;

            return IsRaidLordJob(lord.LordJob);
        }

        private static bool IsRaidLordJob(LordJob job)
        {
            if (job == null)
                return false;

            var jobType = job.GetType();
            if (IsRaidJobType(jobType))
            {
                return true;
            }

            string typeName = jobType.FullName ?? jobType.Name;
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            typeName = typeName.ToLowerInvariant();

            if (typeName.Contains("siege") || typeName.Contains("breach"))
            {
                return true;
            }

            return false;
        }

        private static bool IsRaidJobType(Type jobType)
        {
            while (jobType != null)
            {
                string fullName = jobType.FullName ?? jobType.Name;
                if (!string.IsNullOrEmpty(fullName) && fullName.Equals("Verse.AI.Group.LordJob_Raid", StringComparison.Ordinal))
                {
                    return true;
                }

                jobType = jobType.BaseType;
            }

            return false;
        }

        public static void Reset()
        {
            lastRaidFactionName = null;
            lastRaidEventTime = null;
            activeRaids.Clear();
            cachedRaidCheckTick = -1;
            cachedIsRaidActive = false;
            PresenceEventQueue.Reset();
        }

        private static bool ShouldIgnoreFaction(Faction faction)
        {
            if (faction == null || faction.def == null)
            {
                return false;
            }

            if (faction == Faction.OfInsects)
            {
                return true;
            }

            string defName = faction.def.defName != null ? faction.def.defName.ToLowerInvariant() : string.Empty;

            if (defName.Contains("insect"))
            {
                return true;
            }

            return false;
        }

        private static string GetFactionDisplayName(Faction faction)
        {
            if (faction == null)
            {
                return null;
            }

            string name = faction.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            string defLabel = faction.def?.LabelCap ?? faction.def?.label;
            if (!string.IsNullOrWhiteSpace(defLabel))
            {
                return defLabel;
            }

            return faction.ToStringSafe();
        }

    }
}

