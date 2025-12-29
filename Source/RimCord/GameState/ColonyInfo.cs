using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimCord.GameState
{
    public static class ColonyInfo
    {
        private static string cachedColonyName;
        private static int cachedColonistCount;
        private static int cachedTick = -1;
        private const int CacheValidityTicks = 1;

        public static string GetColonyName()
        {
            RefreshCacheIfNeeded();
            return cachedColonyName;
        }

        public static int GetColonistCount()
        {
            RefreshCacheIfNeeded();
            return cachedColonistCount;
        }

        private static void RefreshCacheIfNeeded()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int tickDiff = currentTick - cachedTick;
            if (cachedTick >= 0 && tickDiff >= 0 && tickDiff < CacheValidityTicks)
                return;

            cachedTick = currentTick;
            cachedColonyName = ComputeColonyName();
            cachedColonistCount = ComputeColonistCount();
        }

        private static string ComputeColonyName()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                    return null;

                var faction = map.ParentFaction;
                if (faction == null || !faction.IsPlayer)
                    return null;

                if (map.Parent is Settlement settlement && !string.IsNullOrEmpty(settlement.Name))
                    return settlement.Name;

                if (map.Parent?.Label != null)
                    return map.Parent.Label;

                return "Unknown Colony";
            }
            catch
            {
                return null;
            }
        }

        private static int ComputeColonistCount()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map?.mapPawns == null)
                    return 0;

                var allColonists = map.mapPawns.FreeColonistsSpawned;
                if (allColonists == null)
                    return 0;

                int count = 0;
                foreach (var pawn in allColonists)
                {
                    if (pawn == null || pawn.Dead || !pawn.Spawned)
                        continue;
                    
                    if (pawn.RaceProps?.Humanlike != true)
                        continue;

                    // Exclude slaves and prisoners
                    if (pawn.IsSlave || pawn.IsPrisoner)
                        continue;

                    count++;
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        internal static void InvalidateCache()
        {
            cachedTick = -1;
        }
    }
}
