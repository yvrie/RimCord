using HarmonyLib;
using RimCord.GameState;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimCord.HarmonyPatches
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class GameTickPatch
    {
        private static int errorCount;
        private const int MaxErrorsBeforeDisable = 10;
        private static int lastUpdateTick;
        private const int UpdateIntervalTicks = 900; // 15 seconds at normal speed

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (errorCount >= MaxErrorsBeforeDisable)
                return;

            try
            {
                var tickManager = Find.TickManager;
                if (tickManager == null)
                    return;

                int currentTick = tickManager.TicksGame;
                if (currentTick - lastUpdateTick < UpdateIntervalTicks)
                    return;
                lastUpdateTick = currentTick;

                var presenceManager = RimCordMod.PresenceManager;
                if (presenceManager == null || presenceManager.IsDisposed)
                    return;

                if (Current.ProgramState != ProgramState.Playing)
                    return;

                presenceManager.Update();
            }
            catch (Exception ex)
            {
                errorCount++;
                if (errorCount >= MaxErrorsBeforeDisable)
                {
                    RimCordLogger.Error("Too many errors in GameTickPatch, disabling: {0}", ex.Message);
                }
                else
                {
                    RimCordLogger.Warning("Error in GameTickPatch.Postfix ({0}/{1}): {2}", 
                        errorCount, MaxErrorsBeforeDisable, ex.Message);
                }
            }
        }

        internal static void ResetErrorCount()
        {
            errorCount = 0;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class GameInitPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                GameTickPatch.ResetErrorCount();

                var presenceManager = RimCordMod.PresenceManager;
                if (presenceManager == null || presenceManager.IsDisposed)
                {
                    RimCordLogger.Info("Creating new PresenceManager on game load...");
                    RimCordMod.PresenceManager = new PresenceManager();
                    RimCordMod.PresenceManager.Initialize();
                }
                else
                {
                    presenceManager.Initialize();
                }

                ColonyInfo.InvalidateCache();
            }
            catch (Exception ex)
            {
                RimCordLogger.Error("Error in GameInitPatch.Postfix: {0}", ex.Message);
            }
        }
    }

    [HarmonyPatch]
    public static class LetterStackPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in typeof(LetterStack).GetMethods())
            {
                if (method.Name != "ReceiveLetter")
                    continue;
                
                var parameters = method.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(Letter))
                    yield return method;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            if (let == null)
                return;

            try
            {
                LetterEventTracker.NotifyLetter(let);
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in LetterStackPatch.Postfix: {0}", ex.Message);
            }
        }
    }
}
