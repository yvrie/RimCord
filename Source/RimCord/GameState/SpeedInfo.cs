using System;
using RimWorld;
using Verse;

namespace RimCord.GameState
{
    public static class SpeedInfo
    {
        private const string SpeedPaused = "Paused";
        private const string Speed1x = "1x";
        private const string Speed2x = "2x";
        private const string Speed3x = "3x";
        private const string Speed4x = "4x";

        public static string GetSpeedMultiplier()
        {
            try
            {
                var tickManager = Find.TickManager;
                if (tickManager == null)
                    return null;

                if (tickManager.Paused)
                    return SpeedPaused;

                switch (tickManager.CurTimeSpeed)
                {
                    case TimeSpeed.Paused:
                        return SpeedPaused;
                    case TimeSpeed.Normal:
                        return Speed1x;
                    case TimeSpeed.Fast:
                        return Speed2x;
                    case TimeSpeed.Superfast:
                        return Speed3x;
                    case TimeSpeed.Ultrafast:
                        return Speed4x;
                    default:
                        return Speed1x;
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool IsPaused()
        {
            try
            {
                return Find.TickManager?.Paused == true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsInGame()
        {
            try
            {
                return Find.CurrentMap != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
