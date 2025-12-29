using System;
using RimWorld;
using Verse;

namespace RimCord.GameState
{
    public class MentalBreakInfo
    {
        public string PawnLabel { get; set; }
        public string MentalStateLabel { get; set; }
        public MentalStateDef MentalStateDef { get; set; }
    }

    public static class MentalBreakTracker
    {
        private static MentalBreakInfo cachedMentalBreak;
        private static int cachedTick = -1;
        private const int CacheValidityTicks = 60;

        public static bool IsMentalBreakActive()
        {
            return GetActiveMentalBreakInfo() != null;
        }

        public static MentalBreakInfo GetActiveMentalBreakInfo()
        {
            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            int tickDiff = ticksGame - cachedTick;
            if (cachedTick >= 0 && tickDiff >= 0 && tickDiff < CacheValidityTicks)
            {
                return cachedMentalBreak;
            }

            var map = Find.CurrentMap;
            if (map == null)
            {
                ClearCache();
                return null;
            }

            try
            {
                var playerPawns = map.mapPawns?.SpawnedPawnsInFaction(Faction.OfPlayer);
                if (playerPawns == null)
                {
                    ClearCache();
                    return null;
                }

                foreach (var pawn in playerPawns)
                {
                    if (pawn == null || pawn.Dead || !pawn.Spawned)
                        continue;

                    try
                    {
                        if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
                            continue;

                        var activeDef = GetCurrentMentalStateDef(pawn);
                        if (activeDef != null)
                        {
                            cachedMentalBreak = new MentalBreakInfo
                            {
                                PawnLabel = pawn.LabelShortCap,
                                MentalStateLabel = activeDef.LabelCap,
                                MentalStateDef = activeDef
                            };
                            cachedTick = ticksGame;
                            return cachedMentalBreak;
                        }
                    }
                    catch (Exception ex)
                    {
                        RimCordLogger.Warning("Error checking mental state for pawn: {0}", ex.Message);
                        continue;
                    }
                }

                ClearCache();
                return null;
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in HasActiveMentalBreak: {0}", ex.Message);
                ClearCache();
                return null;
            }
        }

        private static MentalStateDef GetCurrentMentalStateDef(Pawn pawn)
        {
            if (pawn == null)
                return null;

            if (pawn.InMentalState && pawn.MentalStateDef != null)
            {
                return pawn.MentalStateDef;
            }

            var curState = pawn.mindState?.mentalStateHandler?.CurState;
            if (curState != null)
            {
                return curState.def;
            }

            return null;
        }

        public static void Reset()
        {
            ClearCache();
        }

        private static void ClearCache()
        {
            cachedMentalBreak = null;
            cachedTick = -1;
        }
    }
}

