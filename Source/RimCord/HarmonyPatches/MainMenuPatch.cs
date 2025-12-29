using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace RimCord.HarmonyPatches
{
    internal static class MenuPresenceUpdater
    {
        private static float lastUpdateTime;
        private const float UpdateInterval = 5f;
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            TryUpdateMenuPresence();
        }

        public static void TryUpdateMenuPresence()
        {
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - lastUpdateTime < UpdateInterval)
                return;

            lastUpdateTime = currentTime;

            try
            {
                if (RimCordMod.Settings == null || !RimCordMod.Settings.EnableRichPresence)
                    return;

                ProgramState state;
                try { state = Current.ProgramState; }
                catch { return; }

                if (state == ProgramState.Playing)
                    return;

                if (RimCordMod.PresenceManager == null)
                {
                    RimCordMod.PresenceManager = new PresenceManager();
                    RimCordMod.PresenceManager.Initialize();
                }

                var presenceManager = RimCordMod.PresenceManager;
                if (presenceManager == null || presenceManager.IsDisposed)
                    return;

                presenceManager.Update();
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Menu presence error: {0}", ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
    public static class MainMenuPatch
    {
        [HarmonyPostfix]
        public static void Postfix() => MenuPresenceUpdater.TryUpdateMenuPresence();
    }

    [HarmonyPatch(typeof(UIRoot_Entry), nameof(UIRoot_Entry.UIRootOnGUI))]
    public static class UIRootEntryOnGUIPatch
    {
        [HarmonyPostfix]
        public static void Postfix() => MenuPresenceUpdater.TryUpdateMenuPresence();
    }
}
