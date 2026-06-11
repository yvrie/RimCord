using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimCord.GameState;
using UnityEngine;
using Verse;

namespace RimCord.HarmonyPatches
{
    [HarmonyPatch]
    public static class RealtimePresencePatch
    {
        private const float UpdateIntervalSeconds = 3f;
        private static float lastUpdateTime = -UpdateIntervalSeconds;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Root), nameof(Root.Update));
            yield return AccessTools.Method(typeof(Root_Play), nameof(Root_Play.Update));
            yield return AccessTools.Method(typeof(Root_Entry), nameof(Root_Entry.Update));
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastUpdateTime < UpdateIntervalSeconds)
                return;

            lastUpdateTime = now;

            try
            {
                var settings = RimCordMod.Settings;
                if (settings == null ||
                    !settings.EnableRichPresence ||
                    LanguageDatabase.activeLanguage == null)
                    return;

                if (Current.ProgramState == ProgramState.Playing)
                {
                    RaidTracker.InvalidateCache();
                    GameConditionInfo.InvalidateCache();
                    MentalBreakTracker.Reset();
                }

                var manager = RimCordMod.PresenceManager;
                if (manager == null || manager.IsDisposed)
                    return;

                manager.Update();
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Real-time presence update failed: {0}", ex.Message);
            }
        }
    }
}
