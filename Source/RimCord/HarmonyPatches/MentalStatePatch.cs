using System;
using HarmonyLib;
using RimCord.GameState;
using Verse;
using Verse.AI;

namespace RimCord.HarmonyPatches
{
    internal static class MentalStatePresenceRefresh
    {
        internal static void Refresh(string source)
        {
            try
            {
                MentalBreakTracker.Reset();
                PresenceEventQueue.ClearMentalBreakEvent();

                var manager = RimCordMod.PresenceManager;
                if (manager != null && !manager.IsDisposed)
                {
                    manager.Update(force: true);
                }
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Mental-state presence refresh failed ({0}): {1}", source, ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(MentalState), nameof(MentalState.RecoverFromState))]
    public static class MentalStateRecoverPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            MentalStatePresenceRefresh.Refresh("RecoverFromState");
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), "ClearMentalStateDirect")]
    public static class MentalStateClearPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            MentalStatePresenceRefresh.Refresh("ClearMentalStateDirect");
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.Reset))]
    public static class MentalStateResetPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            MentalStatePresenceRefresh.Refresh("MentalStateHandler.Reset");
        }
    }
}
