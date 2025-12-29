using HarmonyLib;
using System;
using Verse;

namespace RimCord.HarmonyPatches
{
    // clears Discord status when game closes so it doesnt show "playing" forever
    [HarmonyPatch(typeof(Root), nameof(Root.Shutdown))]
    public static class RootShutdownPatch
    {
        private static bool hasShutdown;

        static RootShutdownPatch()
        {
            // backup handlers incase the normal shutdown doesnt trigger
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            CleanupPresence("Root.Shutdown");
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            CleanupPresence("ProcessExit");
        }

        private static void OnDomainUnload(object sender, EventArgs e)
        {
            CleanupPresence("DomainUnload");
        }

        private static void CleanupPresence(string source)
        {
            if (hasShutdown)
                return;

            hasShutdown = true;

            try
            {
                var manager = RimCordMod.PresenceManager;
                if (manager != null && !manager.IsDisposed)
                {
                    RimCordLogger.Info("Clearing Discord presence ({0})...", source);
                    manager.Dispose();
                    RimCordMod.PresenceManager = null;
                }
            }
            catch (Exception ex)
            {
                // cant really do much if cleanup fails, just try to log it
                try
                {
                    RimCordLogger.Warning("Cleanup failed: {0}", ex.Message);
                }
                catch { }
            }
        }
    }
}
