using System.Linq;
using RimWorld;
using Verse;

namespace RimCord.GameState
{
    public static class GameConditionInfo
    {
        private static string cachedConditionLabel;
        private static int cachedTick = -1;
        private const int CacheValidityTicks = 120;

        public static string GetTopConditionLabel()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (cachedTick >= 0 && currentTick - cachedTick < CacheValidityTicks)
            {
                return cachedConditionLabel;
            }

            cachedTick = currentTick;
            cachedConditionLabel = ComputeTopConditionLabel();
            return cachedConditionLabel;
        }

        private static string ComputeTopConditionLabel()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                return null;
            }

            var manager = map.gameConditionManager;
            if (manager == null || manager.ActiveConditions == null)
            {
                return null;
            }

            var condition = manager.ActiveConditions
                .Where(c => c != null && !c.Expired && !c.Permanent)
                .OrderByDescending(GetConditionPriority)
                .FirstOrDefault();

            if (condition == null)
            {
                return null;
            }

            if (!condition.LabelCap.NullOrEmpty())
            {
                return condition.LabelCap;
            }

            if (condition.def != null)
            {
                if (!condition.def.LabelCap.NullOrEmpty())
                {
                    return condition.def.LabelCap;
                }

                if (!string.IsNullOrEmpty(condition.def.label))
                {
                    return condition.def.label.CapitalizeFirst();
                }

                if (!string.IsNullOrEmpty(condition.def.defName))
                {
                    return condition.def.defName;
                }
            }

            return null;
        }

        internal static void InvalidateCache()
        {
            cachedTick = -1;
        }

        private static int GetConditionPriority(GameCondition condition)
        {
            if (condition == null)
            {
                return 0;
            }

            int priority = 50;

            if (!condition.Permanent && condition.TicksLeft > 0)
            {
                priority += condition.TicksLeft / 60;
            }

            if (condition.Permanent)
            {
                priority += 1000;
            }

            return priority;
        }
    }
}
