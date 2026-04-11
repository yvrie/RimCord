using System;
using HarmonyLib;
using Verse;

namespace RimCord.HarmonyPatches
{
    internal static class PausePresenceUpdateHelper
    {
        public static void ForcePresenceUpdate(string source)
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing)
                    return;

                var presenceManager = RimCordMod.PresenceManager;
                if (presenceManager != null && !presenceManager.IsDisposed)
                {
                    presenceManager.Update(force: true);
                }
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error updating presence after {0}: {1}", source, ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(TickManager))]
    [HarmonyPatch("TogglePaused")]
    public static class PauseTogglePatch
    {
        [HarmonyPostfix]
        public static void Postfix(TickManager __instance)
        {
            PausePresenceUpdateHelper.ForcePresenceUpdate("TogglePaused");
        }
    }

    [HarmonyPatch(typeof(TickManager))]
    [HarmonyPatch("CurTimeSpeed", MethodType.Setter)]
    public static class TimeSpeedChangePatch
    {
        [HarmonyPostfix]
        public static void Postfix(TickManager __instance)
        {
            PausePresenceUpdateHelper.ForcePresenceUpdate("CurTimeSpeed");
        }
    }
}
