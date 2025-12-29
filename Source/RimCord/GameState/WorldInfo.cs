using RimWorld;
using Verse;

namespace RimCord.GameState
{
    public static class WorldInfo
    {
        private static string cachedBiome;
        private static string cachedQuadrum;
        private static int cachedYear;
        private static int cachedTick = -1;
        private const int CacheValidityTicks = 120;

        public static string GetBiomeName()
        {
            RefreshCacheIfNeeded();
            return cachedBiome;
        }

        public static string GetQuadrum()
        {
            RefreshCacheIfNeeded();
            return cachedQuadrum;
        }

        public static int GetYear()
        {
            RefreshCacheIfNeeded();
            return cachedYear;
        }

        private static void RefreshCacheIfNeeded()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int tickDiff = currentTick - cachedTick;
            if (cachedTick >= 0 && tickDiff >= 0 && tickDiff < CacheValidityTicks)
                return;

            cachedTick = currentTick;
            cachedBiome = ComputeBiomeName();
            cachedQuadrum = ComputeQuadrum();
            cachedYear = ComputeYear();
        }

        private static string ComputeBiomeName()
        {
            try
            {
                var map = Find.CurrentMap;
                return map?.Biome?.LabelCap;
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeQuadrum()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null || Find.TickManager == null || Find.WorldGrid == null)
                    return null;

                var quadrum = GenDate.Quadrum(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x);
                return quadrum.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static int ComputeYear()
        {
            try
            {
                if (Find.TickManager == null)
                    return 0;

                int tile = Find.CurrentMap?.Tile ?? 0;
                float longLatX = 0f;

                if (Find.WorldGrid != null && tile > 0)
                {
                    longLatX = Find.WorldGrid.LongLatOf(tile).x;
                }

                return GenDate.Year(Find.TickManager.TicksAbs, longLatX);
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
