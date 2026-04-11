using RimWorld;
using Verse;

namespace RimCord.GameState
{
    public struct StorytellerAssets
    {
        public string SmallImageKey;
        public string SmallImageText;

        public static StorytellerAssets Empty => new StorytellerAssets
        {
            SmallImageKey = null,
            SmallImageText = null
        };
    }

    public static class StorytellerInfo
    {
        private static StorytellerAssets cachedAssets;
        private static bool hasCachedAssets;
        private static int cachedTick = -1;
        private const int CacheValidityTicks = 60;

        public static StorytellerAssets GetStorytellerAssets()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (hasCachedAssets && cachedTick >= 0 && currentTick - cachedTick < CacheValidityTicks)
            {
                return cachedAssets;
            }

            cachedTick = currentTick;
            cachedAssets = ComputeStorytellerAssets();
            hasCachedAssets = true;
            return cachedAssets;
        }

        private static StorytellerAssets ComputeStorytellerAssets()
        {
            var storyteller = Find.Storyteller;
            if (storyteller?.def == null)
            {
                return StorytellerAssets.Empty;
            }

            var def = storyteller.def;
            string imageKey = ResolveSmallImageKey(def);
            string label = def.LabelCap.NullOrEmpty() ? def.label ?? def.defName : def.LabelCap.ToString();
            string difficulty = GetDifficultyLabel(storyteller);
            if (!string.IsNullOrEmpty(difficulty))
            {
                label = string.Format("{0} – {1}", label, difficulty);
            }

            return new StorytellerAssets
            {
                SmallImageKey = imageKey,
                SmallImageText = label
            };
        }

        internal static void InvalidateCache()
        {
            cachedTick = -1;
            hasCachedAssets = false;
        }

        private static string GetDifficultyLabel(Storyteller storyteller)
        {
            var difficultyDef = storyteller?.difficultyDef ?? Find.Storyteller?.difficultyDef;
            if (difficultyDef == null)
            {
                return null;
            }

            if (!difficultyDef.LabelCap.NullOrEmpty())
            {
                return difficultyDef.LabelCap.ToString();
            }

            if (!difficultyDef.label.NullOrEmpty())
            {
                return difficultyDef.label;
            }

            return difficultyDef.defName;
        }

        private static string ResolveSmallImageKey(StorytellerDef def)
        {
            if (def == null)
            {
                return null;
            }

            string identity = (def.defName ?? def.label ?? string.Empty).ToLowerInvariant();

            if (identity.Contains("cassandra"))
            {
                return "cassandra";
            }

            if (identity.Contains("phoebe"))
            {
                return "phoebe";
            }

            if (identity.Contains("randy"))
            {
                return "randy";
            }

            return "random";
        }
    }
}
