using System;
using HarmonyLib;
using Verse;

namespace RimCord.HarmonyPatches
{
    [HarmonyPatch(typeof(TickManager))]
    [HarmonyPatch("TogglePaused")]
    public static class PauseTogglePatch
    {
        [HarmonyPostfix]
        public static void Postfix(TickManager __instance)
        {
            try
            {
                var presenceManager = RimCordMod.PresenceManager;
                if (presenceManager != null && !presenceManager.IsDisposed)
                {
                    presenceManager.Update();
                }
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in PauseTogglePatch: {0}", ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(TickManager))]
    [HarmonyPatch("CurTimeSpeed", MethodType.Setter)]
    public static class TimeSpeedChangePatch
    {
        [HarmonyPostfix]
        public static void Postfix(TickManager __instance)
        {
            try
            {
                var presenceManager = RimCordMod.PresenceManager;
                if (presenceManager != null && !presenceManager.IsDisposed)
                {
                    presenceManager.Update();
                }
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in TimeSpeedChangePatch: {0}", ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(TickManager))]
    [HarmonyPatch("Pause")]
    public static class PauseMethodPatch
    {
        [HarmonyPostfix]
        public static void Postfix(TickManager __instance)
        {
            try
            {
                var presenceManager = RimCordMod.PresenceManager;
                if (presenceManager != null && !presenceManager.IsDisposed)
                {
                    presenceManager.Update();
                }
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in PauseMethodPatch: {0}", ex.Message);
            }
        }
    }
}
