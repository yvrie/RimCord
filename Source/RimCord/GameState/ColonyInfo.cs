using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimCord.GameState
{
    public static class ColonyInfo
    {
        public static string GetColonyName()
        {
            return ComputeColonyName();
        }

        public static int GetColonistCount()
        {
            return ComputeColonistCount();
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

    }
}
