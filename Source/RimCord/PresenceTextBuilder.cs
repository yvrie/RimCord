using System;
using System.Collections.Generic;
using System.Linq;
using RimCord.GameState;
using RimWorld;
using Verse;

namespace RimCord
{
    internal static class PresenceTextBuilder
    {
        internal const int MaxEventStateLength = 96;
        internal const int MaxPresenceDetailsLength = 128;
        private static readonly List<string> detailsParts = new List<string>(8);

        internal static string BuildState(ActivityInfo activity, bool inGame, bool isPaused, int colonistCount)
        {
            bool isQueuedEvent = activity != null && string.Equals(activity.Activity, "QueuedEvent", StringComparison.Ordinal);
            bool isGameCondition = activity != null && string.Equals(activity.Activity, "GameCondition", StringComparison.Ordinal);

            string eventState = GetEventStateLine(activity, false, isGameCondition);
            if (!string.IsNullOrEmpty(eventState))
            {
                return LimitPresenceText(eventState);
            }

            if (inGame && isPaused)
            {
                return GetGenericInGameState(isPaused, colonistCount);
            }

            if (inGame && RimCordMod.Settings?.ShowColonistCount == true)
                return GetGenericInGameState(isPaused, colonistCount);

            if (activity != null && !isQueuedEvent && !isGameCondition && !string.IsNullOrEmpty(activity.StateOverride))
            {
                return LimitPresenceText(SanitizePresenceText(activity.StateOverride));
            }

            if (!inGame)
            {
                return RimCordText.SafeTranslate(RimCordText.MainMenu);
            }

            if (activity != null)
            {
                if (activity.Activity == "Raid" && activity.IsUrgent)
                {
                    if (!string.IsNullOrEmpty(activity.RaidFactionName))
                    {
                        return string.Format("RimCord_RaidBy".Translate(), activity.RaidFactionName);
                    }

                    return "RimCord_UnderAttack".Translate();
                }

                if (activity.Activity == "MentalBreak")
                {
                    var mentalInfo = activity.MentalBreakInfo;
                    if (mentalInfo != null)
                    {
                        string pawnName = mentalInfo.PawnLabel ?? "RimCord_Colonist".Translate();
                        string breakType = mentalInfo.MentalStateLabel ?? "RimCord_MentalBreak".Translate();
                        return string.Format("{0}: {1}", pawnName, breakType);
                    }
                    return "RimCord_MentalBreak".Translate();
                }
            }

            return GetGenericInGameState(isPaused, colonistCount);
        }

        private static string GetGenericInGameState(bool isPaused, int colonistCount)
        {
            if (RimCordMod.Settings?.ShowColonistCount == true)
            {
                return colonistCount > 0
                    ? RimCordText.SafeTranslate(RimCordText.Colonists)
                    : RimCordText.SafeTranslate(RimCordText.ColonyLost);
            }

            return isPaused
                ? RimCordText.SafeTranslate(RimCordText.StatusPaused)
                : RimCordText.SafeTranslate(RimCordText.StatusPlaying);
        }

        internal static string BuildDetails(ActivityInfo activity, bool inGame, bool isPaused, int colonistCount, RimCordSettings settings)
        {
            bool isQueuedEvent = activity != null && string.Equals(activity.Activity, "QueuedEvent", StringComparison.Ordinal);
            bool isGameCondition = activity != null && string.Equals(activity.Activity, "GameCondition", StringComparison.Ordinal);

            if (inGame && isQueuedEvent)
            {
                return BuildLetterDetails(activity.StateOverride, settings);
            }

            if (inGame && isPaused)
            {
                string pausedDetails = PausedContextBuilder.GetPausedDetails(settings);
                if (!string.IsNullOrEmpty(pausedDetails))
                    return pausedDetails;

                return LimitDetailsText(RimCordText.SafeTranslate(RimCordText.StatusPaused));
            }

            if (inGame && !isPaused && settings.ShowColonistCount)
            {
                string eventContext = GetEventContextForDetails(activity, isQueuedEvent, isGameCondition);
                string colonyInfo = BuildColonyContext(settings);

                if (!string.IsNullOrEmpty(eventContext) && !string.IsNullOrEmpty(colonyInfo))
                    return LimitDetailsText(string.Format("{0} | {1}", eventContext, colonyInfo));
                if (!string.IsNullOrEmpty(eventContext))
                    return LimitDetailsText(eventContext);
                if (!string.IsNullOrEmpty(colonyInfo))
                    return LimitDetailsText(colonyInfo);
                return BuildDefaultInGameDetails(settings);
            }

            if (activity != null && !isQueuedEvent && !isGameCondition && !string.IsNullOrEmpty(activity.DetailsOverride))
            {
                return LimitDetailsText(activity.DetailsOverride);
            }

            if (!inGame)
            {
                bool showMainMenuModCount = settings == null || settings.ShowMainMenuModCount;
                if (!showMainMenuModCount)
                {
                    return RimCordText.SafeTranslate(RimCordText.BrowsingMods);
                }

                try
                {
                    if (LanguageDatabase.activeLanguage == null)
                    {
                        return RimCordText.SafeTranslate(RimCordText.BrowsingMods);
                    }

                    int modCount = ModsConfig.ActiveModsInLoadOrder
                        .Count(m => m != null && !m.Official && !m.PackageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase));
                    if (modCount > 0)
                    {
                        string modText = modCount == 1 ? "mod" : "mods";
                        try
                        {
                            modText = modCount == 1
                                ? "RimCord_Mod".Translate().ToString()
                                : "RimCord_Mods".Translate().ToString();
                        }
                        catch { }

                        return string.Format("{0} ({1} {2})",
                            RimCordText.SafeTranslate(RimCordText.BrowsingMods),
                            modCount,
                            modText);
                    }
                }
                catch { }
                return RimCordText.SafeTranslate(RimCordText.BrowsingMods);
            }

            string joinedParts = BuildColonyContext(settings);

            if (string.IsNullOrEmpty(joinedParts))
            {
                string conditionDescription = GetConditionDescription(activity, isGameCondition);
                if (!string.IsNullOrEmpty(conditionDescription))
                {
                    joinedParts = conditionDescription;
                }
            }

            if (string.IsNullOrEmpty(joinedParts))
            {
                return BuildDefaultInGameDetails(settings);
            }

            return LimitDetailsText(joinedParts);
        }

        private static string GetEventContextForDetails(
            ActivityInfo activity,
            bool isQueuedEvent,
            bool isGameCondition)
        {
            if (activity != null)
            {
                if (activity.Activity == "Raid")
                {
                    if (!string.IsNullOrEmpty(activity.RaidFactionName))
                        return LimitPresenceText(string.Format("RimCord_RaidBy".Translate(), activity.RaidFactionName));
                    return LimitPresenceText("RimCord_UnderAttack".Translate());
                }

                if (activity.Activity == "MentalBreak")
                {
                    var mentalInfo = activity.MentalBreakInfo;
                    if (mentalInfo != null)
                    {
                        string pawnName = mentalInfo.PawnLabel ?? "RimCord_Colonist".Translate();
                        string breakType = mentalInfo.MentalStateLabel ?? "RimCord_MentalBreak".Translate();
                        return LimitPresenceText(string.Format("{0}: {1}", pawnName, breakType));
                    }
                    return LimitPresenceText("RimCord_MentalBreak".Translate());
                }

                if (isQueuedEvent)
                {
                    return null;
                }

                if (isGameCondition)
                {
                    string label = activity.StateOverride ?? ActivityDetector.GetActivityDisplayText(activity);
                    if (!string.IsNullOrEmpty(label)) return LimitPresenceText(SanitizePresenceText(label));
                }

                if (!string.IsNullOrEmpty(activity.StateOverride))
                    return LimitPresenceText(SanitizePresenceText(activity.StateOverride));
            }

            return null;
        }

        private static string GetConditionDescription(ActivityInfo activity, bool isGameCondition)
        {
            if (!isGameCondition || activity == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(activity.StateOverride))
            {
                return LimitDetailsText(activity.StateOverride);
            }

            if (!string.IsNullOrEmpty(activity.DetailsOverride))
            {
                return LimitDetailsText(activity.DetailsOverride);
            }

            string activityText = ActivityDetector.GetActivityDisplayText(activity);
            return string.IsNullOrEmpty(activityText) ? null : LimitDetailsText(activityText);
        }

        private static string GetEventStateLine(ActivityInfo activity, bool isQueuedEvent, bool isGameCondition)
        {
            if (activity == null)
            {
                return null;
            }

            if (isQueuedEvent)
            {
                return LimitPresenceText(SanitizePresenceText(activity.StateOverride));
            }

            if (isGameCondition)
            {
                string label = activity.StateOverride ?? ActivityDetector.GetActivityDisplayText(activity);
                return ComposeEventLine(label, activity.DetailsOverride);
            }

            return null;
        }

        private static string ComposeEventLine(string label, string description)
        {
            string cleanLabel = SanitizePresenceText(label);
            string cleanDescription = SanitizePresenceText(description);

            if (string.IsNullOrEmpty(cleanLabel))
            {
                return LimitPresenceText(cleanDescription);
            }

            if (string.IsNullOrEmpty(cleanDescription) || cleanDescription.StartsWith(cleanLabel, StringComparison.OrdinalIgnoreCase))
            {
                return LimitPresenceText(cleanLabel);
            }

            return LimitPresenceText(string.Format("{0}: {1}", cleanLabel, cleanDescription));
        }

        private static string SanitizePresenceText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Replace('\n', ' ').Replace("\r", string.Empty).Trim();
        }

        internal static string LimitPresenceText(string value)
        {
            return LimitPresenceText(value, MaxEventStateLength);
        }

        internal static string LimitPresenceText(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (maxLength <= 0)
            {
                return string.Empty;
            }

            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength).TrimEnd() + "...";
        }

        private static string BuildLetterDetails(string letterTitle, RimCordSettings settings)
        {
            string cleanTitle = LimitPresenceText(SanitizePresenceText(letterTitle));
            string colonyContext = BuildColonyContext(settings);

            if (!string.IsNullOrEmpty(cleanTitle) && !string.IsNullOrEmpty(colonyContext))
            {
                return LimitDetailsText(string.Format("{0} | {1}", cleanTitle, colonyContext));
            }

            if (!string.IsNullOrEmpty(cleanTitle))
            {
                return LimitDetailsText(cleanTitle);
            }

            if (!string.IsNullOrEmpty(colonyContext))
            {
                return LimitDetailsText(colonyContext);
            }

            return BuildDefaultInGameDetails(settings);
        }

        private static string BuildColonyContext(RimCordSettings settings)
        {
            detailsParts.Clear();

            if (settings != null && settings.ShowColonyName)
            {
                string colonyName = ColonyInfo.GetColonyName();
                if (!string.IsNullOrEmpty(colonyName))
                {
                    detailsParts.Add(colonyName);
                }
            }

            bool showYear = settings == null || settings.ShowInGameYear;
            bool showQuadrum = settings == null || settings.ShowInGameQuadrum;
            if (showYear || showQuadrum)
            {
                int year = showYear ? WorldInfo.GetYear() : 0;
                string quadrum = showQuadrum ? WorldInfo.GetQuadrum() : null;

                if (year > 0 && !string.IsNullOrEmpty(quadrum))
                {
                    detailsParts.Add(string.Format("{0} {1}, {2}", RimCordText.Year.Translate(), year, quadrum));
                }
                else if (year > 0)
                {
                    detailsParts.Add(string.Format("{0} {1}", RimCordText.Year.Translate(), year));
                }
                else if (!string.IsNullOrEmpty(quadrum))
                {
                    detailsParts.Add(quadrum);
                }
            }

            if (settings != null && settings.ShowBiome)
            {
                string biome = WorldInfo.GetBiomeName();
                if (!string.IsNullOrEmpty(biome))
                {
                    detailsParts.Add(biome);
                }
            }

            return detailsParts.Count > 0 ? string.Join(" | ", detailsParts) : null;
        }

        private static string BuildDefaultInGameDetails(RimCordSettings settings)
        {
            bool showYear = settings == null || settings.ShowInGameYear;
            bool showQuadrum = settings == null || settings.ShowInGameQuadrum;
            if (showYear || showQuadrum)
            {
                int year = showYear ? WorldInfo.GetYear() : 0;
                string quadrum = showQuadrum ? WorldInfo.GetQuadrum() : null;

                if (year > 0 && !string.IsNullOrEmpty(quadrum))
                {
                    return LimitDetailsText(string.Format("{0} {1}, {2}", RimCordText.Year.Translate(), year, quadrum));
                }

                if (year > 0)
                {
                    return LimitDetailsText(string.Format("{0} {1}", RimCordText.Year.Translate(), year));
                }

                if (!string.IsNullOrEmpty(quadrum))
                {
                    return LimitDetailsText(quadrum);
                }
            }

            return LimitDetailsText(RimCordText.SafeTranslate(RimCordText.StatusPlayingRimWorld));
        }

        private static string LimitDetailsText(string value)
        {
            var sanitized = SanitizePresenceText(value);
            if (string.IsNullOrEmpty(sanitized))
            {
                return sanitized;
            }

            if (sanitized.Length <= MaxPresenceDetailsLength)
            {
                return sanitized;
            }

            return sanitized.Substring(0, MaxPresenceDetailsLength).TrimEnd() + "...";
        }

    }

    internal static class RimCordText
    {
        internal const string StatusPaused = "RimCord_Status_Paused";
        internal const string StatusPlaying = "RimCord_Status_Playing";
        internal const string StatusPlayingRimWorld = "RimCord_Status_PlayingRimWorld";
        internal const string Year = "RimCord_Year";
        internal const string Colonists = "RimCord_Colonists";
        internal const string ColonyLost = "RimCord_ColonyLost";
        internal const string DefaultButtonLabel = "RimCord_DefaultButtonLabel";

        internal const string MainMenu = "RimCord_MainMenu";
        internal const string BrowsingMods = "RimCord_BrowsingMods";

        private static readonly System.Collections.Generic.Dictionary<string, string> Fallbacks = new System.Collections.Generic.Dictionary<string, string>
        {
            { StatusPaused, "Game Paused" },
            { StatusPlaying, "Playing RimWorld" },
            { StatusPlayingRimWorld, "Playing RimWorld" },
            { Year, "Year" },

            { MainMenu, "Main Menu" },
            { BrowsingMods, "Browsing mods and settings" },
            { Colonists, "Colonists" },
            { ColonyLost, "Colony Lost" },
            { DefaultButtonLabel, "Watch live" }
        };

        internal static string SafeTranslate(string key)
        {
            try
            {
                if (LanguageDatabase.activeLanguage == null)
                {
                    return GetFallback(key);
                }

                return key.Translate();
            }
            catch
            {
                return GetFallback(key);
            }
        }

        private static string GetFallback(string key)
        {
            if (Fallbacks.TryGetValue(key, out string fallback))
            {
                return fallback;
            }
            return key;
        }
    }
}
