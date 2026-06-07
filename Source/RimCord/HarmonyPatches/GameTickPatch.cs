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
        private const int UpdateIntervalTicks = 900;

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

        internal static void ResetUpdateThrottle()
        {
            lastUpdateTick = -UpdateIntervalTicks;
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
                GameTickPatch.ResetUpdateThrottle();

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

                WorldInfo.InvalidateCache();
                StorytellerInfo.InvalidateCache();
                ForcePresenceUpdate("game load");
                SchedulePostLoadPresenceUpdate();
            }
            catch (Exception ex)
            {
                RimCordLogger.Error("Error in GameInitPatch.Postfix: {0}", ex.Message);
            }
        }

        private static void SchedulePostLoadPresenceUpdate()
        {
            try
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        WorldInfo.InvalidateCache();
                        StorytellerInfo.InvalidateCache();
                        ForcePresenceUpdate("post-load");
                    }
                    catch (Exception ex)
                    {
                        RimCordLogger.Warning("Error in post-load presence update: {0}", ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Unable to schedule post-load presence update: {0}", ex.Message);
            }
        }

        private static void ForcePresenceUpdate(string source)
        {
            var manager = RimCordMod.PresenceManager;
            if (manager == null || manager.IsDisposed)
                return;

            try
            {
                manager.Update(force: true);
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Forced presence update failed ({0}): {1}", source, ex.Message);
            }
        }
    }

    internal static class ColonistRosterPresenceRefresh
    {
        private static DateTime lastRefreshUtc = DateTime.MinValue;
        private static int lastObservedColonistCount = -1;
        private const double DuplicateRefreshWindowSeconds = 0.75;

        internal static bool CouldAffectDisplayedColonistCount(Pawn pawn)
        {
            try
            {
                if (RimCordMod.Settings == null ||
                    !RimCordMod.Settings.EnableRichPresence ||
                    !RimCordMod.Settings.ShowColonistCount)
                    return false;

                if (pawn == null)
                    return false;

                if (pawn.RaceProps?.Humanlike != true)
                    return false;

                var faction = pawn.Faction;
                if (faction == null || !faction.IsPlayer)
                    return false;

                return !pawn.IsSlave && !pawn.IsPrisoner;
            }
            catch
            {
                return false;
            }
        }

        internal static void ForceRefresh(string source)
        {
            try
            {
                if (RimCordMod.Settings == null ||
                    !RimCordMod.Settings.EnableRichPresence ||
                    !RimCordMod.Settings.ShowColonistCount)
                    return;

                if (Current.ProgramState != ProgramState.Playing)
                    return;

                int currentColonistCount = ColonyInfo.GetColonistCount();
                var now = DateTime.UtcNow;
                bool duplicateRefresh = currentColonistCount == lastObservedColonistCount
                    && (now - lastRefreshUtc).TotalSeconds < DuplicateRefreshWindowSeconds;
                if (duplicateRefresh)
                    return;

                lastObservedColonistCount = currentColonistCount;
                lastRefreshUtc = now;

                var manager = RimCordMod.PresenceManager;
                if (manager == null || manager.IsDisposed)
                    return;

                manager.Update(force: true);
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Colonist roster presence refresh failed ({0}): {1}", source, ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.RegisterPawn))]
    public static class MapPawnsRegisterPawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p)
        {
            if (ColonistRosterPresenceRefresh.CouldAffectDisplayedColonistCount(p))
            {
                ColonistRosterPresenceRefresh.ForceRefresh("pawn registered");
            }
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.DeRegisterPawn))]
    public static class MapPawnsDeRegisterPawnPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn p, ref bool __state)
        {
            __state = ColonistRosterPresenceRefresh.CouldAffectDisplayedColonistCount(p);
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state)
            {
                ColonistRosterPresenceRefresh.ForceRefresh("pawn deregistered");
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class PawnKillPresencePatch
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn __instance, ref bool __state)
        {
            __state = ColonistRosterPresenceRefresh.CouldAffectDisplayedColonistCount(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state)
            {
                ColonistRosterPresenceRefresh.ForceRefresh("pawn killed");
            }
        }
    }

    internal static class LetterPresenceRefresh
    {
        internal static void RefreshIfRecorded(bool recorded, string source)
        {
            if (!recorded)
                return;

            var manager = RimCordMod.PresenceManager;
            if (manager == null || manager.IsDisposed)
                return;

            manager.Update(force: true);
        }
    }

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        new[] { typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(LookTargets), typeof(Faction), typeof(Quest), typeof(List<ThingDef>), typeof(string), typeof(int), typeof(bool) })]
    public static class LetterStackReceiveTargetsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(LetterStack __instance, ref int __state)
        {
            __state = LetterEventTracker.GetLetterCount(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix(LetterStack __instance, TaggedString label, LetterDef textLetterDef, int __state)
        {
            try
            {
                bool recorded = LetterEventTracker.NotifyAddedLetter(__instance, __state, label, textLetterDef);
                LetterPresenceRefresh.RefreshIfRecorded(recorded, "ReceiveLetter targets");
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error capturing target letter: {0}", ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        new[] { typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(string), typeof(int), typeof(bool) })]
    public static class LetterStackReceiveLabelPatch
    {
        [HarmonyPrefix]
        public static void Prefix(LetterStack __instance, ref int __state)
        {
            __state = LetterEventTracker.GetLetterCount(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix(LetterStack __instance, TaggedString label, LetterDef textLetterDef, int __state)
        {
            try
            {
                bool recorded = LetterEventTracker.NotifyAddedLetter(__instance, __state, label, textLetterDef);
                LetterPresenceRefresh.RefreshIfRecorded(recorded, "ReceiveLetter label");
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error capturing label letter: {0}", ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class LetterStackReceiveInstancePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            try
            {
                bool recorded = LetterEventTracker.NotifyAddedLetter(let);
                LetterPresenceRefresh.RefreshIfRecorded(recorded, "ReceiveLetter instance");
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error capturing letter instance: {0}", ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LetterStackUpdate))]
    public static class LetterStackModFallbackPatch
    {
        [HarmonyPostfix]
        public static void Postfix(LetterStack __instance)
        {
            try
            {
                bool recorded = LetterEventTracker.DetectLettersAddedByMods(__instance);
                LetterPresenceRefresh.RefreshIfRecorded(recorded, "LetterStack mod fallback");
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error checking modded letters: {0}", ex.Message);
            }
        }
    }
}
