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

        internal static string BuildState(ActivityInfo activity, bool inGame, Func<(string State, string Details)?> recentEventProvider)
        {
            bool isQueuedEvent = activity != null && string.Equals(activity.Activity, "QueuedEvent", StringComparison.Ordinal);
            bool isGameCondition = activity != null && string.Equals(activity.Activity, "GameCondition", StringComparison.Ordinal);

            string eventState = GetEventStateLine(activity, isQueuedEvent, isGameCondition);
            if (!string.IsNullOrEmpty(eventState))
            {
                return LimitPresenceText(eventState);
            }

            if (activity != null && !isQueuedEvent && !isGameCondition && !string.IsNullOrEmpty(activity.StateOverride))
            {
                return LimitPresenceText(SanitizePresenceText(activity.StateOverride));
            }

            // if theres no current activity, show the last event instead
            if (activity == null)
            {
                string recentEventState = GetRecentEventStateLine(recentEventProvider);
                if (!string.IsNullOrEmpty(recentEventState))
                {
                    return recentEventState;
                }
            }

            if (!inGame)
            {
                return RimCordText.SafeTranslate(RimCordText.MainMenu);
            }

            if (activity != null)
            {
                if (activity.Activity == "Raid" && activity.IsUrgent)
                {
                    // show who's attacking if we know, otherwise just generic warning
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

            if (Find.TickManager != null && Find.TickManager.Paused)
            {
                return RimCordText.StatusPaused.Translate();
            }

            return RimCordText.StatusPlaying.Translate();
        }

        internal static string BuildDetails(ActivityInfo activity, bool inGame, RimCordSettings settings, Func<(string State, string Details)?> recentEventProvider)
        {
            bool isQueuedEvent = activity != null && string.Equals(activity.Activity, "QueuedEvent", StringComparison.Ordinal);
            bool isGameCondition = activity != null && string.Equals(activity.Activity, "GameCondition", StringComparison.Ordinal);

            if (activity != null && !isQueuedEvent && !isGameCondition && !string.IsNullOrEmpty(activity.DetailsOverride))
            {
                return LimitDetailsText(activity.DetailsOverride);
            }

            if (!inGame)
            {
                try
                {
                    int modCount = ModsConfig.ActiveModsInLoadOrder
                        .Count(m => m != null && !m.Official && !m.PackageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase));
                    if (modCount > 0)
                    {
                        string modText = modCount == 1 
                            ? "RimCord_Mod".Translate() 
                            : "RimCord_Mods".Translate();
                        return string.Format("{0} ({1} {2})", 
                            RimCordText.SafeTranslate(RimCordText.BrowsingMods),
                            modCount,
                            modText);
                    }
                }
                catch { }
                return RimCordText.SafeTranslate(RimCordText.BrowsingMods);
            }

            detailsParts.Clear();

            if (settings.ShowColonyName)
            {
                string colonyName = ColonyInfo.GetColonyName();
                if (!string.IsNullOrEmpty(colonyName))
                    detailsParts.Add(colonyName);
            }

            // Colonist count is now displayed via Discord's party system (X of X format)

            int year = WorldInfo.GetYear();
            if (year > 0)
            {
                string quadrum = WorldInfo.GetQuadrum();
                detailsParts.Add(string.IsNullOrEmpty(quadrum)
                    ? string.Format("{0} {1}", RimCordText.Year.Translate(), year)
                    : string.Format("{0} {1}, {2}", RimCordText.Year.Translate(), year, quadrum));
            }

            if (settings.ShowBiome)
            {
                string biome = WorldInfo.GetBiomeName();
                if (!string.IsNullOrEmpty(biome))
                    detailsParts.Add(biome);
            }

            if (settings.ShowGameSpeed)
            {
                string speed = SpeedInfo.GetSpeedMultiplier();
                if (!string.IsNullOrEmpty(speed) && !string.Equals(speed, "Paused", StringComparison.OrdinalIgnoreCase))
                    detailsParts.Add(string.Format("{0} {1}", speed, RimCordText.Speed.Translate()));
            }

            string joinedParts = detailsParts.Count > 0 ? string.Join(" | ", detailsParts) : null;

            if (string.IsNullOrEmpty(joinedParts))
            {
                string letterDescription = GetLetterDescription(activity, isQueuedEvent, recentEventProvider);
                if (!string.IsNullOrEmpty(letterDescription))
                {
                    joinedParts = letterDescription;
                }
                else
                {
                    string conditionDescription = GetConditionDescription(activity, isGameCondition);
                    if (!string.IsNullOrEmpty(conditionDescription))
                    {
                        joinedParts = conditionDescription;
                    }
                    else
                    {
                        var recentEvent = recentEventProvider?.Invoke();
                        if (recentEvent.HasValue && !string.IsNullOrEmpty(recentEvent.Value.Details))
                        {
                            joinedParts = recentEvent.Value.Details;
                        }
                    }
                }
            }

            if (activity != null && activity.IsUrgent && activity.Activity == "Raid")
            {
                joinedParts = BuildRaidDetails();
            }

            if (string.IsNullOrEmpty(joinedParts))
            {
                int defaultYear = WorldInfo.GetYear();
                return defaultYear > 0
                    ? LimitDetailsText(string.Format("{0} {1}", RimCordText.Year.Translate(), defaultYear))
                    : LimitDetailsText(RimCordText.StatusPlayingRimWorld.Translate());
            }

            return LimitDetailsText(joinedParts);
        }

        private static string BuildRaidDetails()
        {
            string colonyName = ColonyInfo.GetColonyName();
            int year = WorldInfo.GetYear();

            var activityParts = new List<string>();
            if (!string.IsNullOrEmpty(colonyName))
            {
                activityParts.Add(colonyName);
            }

            activityParts.Add(string.Format("{0} {1}", RimCordText.Year.Translate(), year));
            return string.Join(" | ", activityParts);
        }

        private static string GetLetterDescription(ActivityInfo activity, bool isQueuedEvent, Func<(string State, string Details)?> recentEventProvider)
        {
            if (activity != null && isQueuedEvent)
            {
                if (!string.IsNullOrEmpty(activity.DetailsOverride))
                {
                    return LimitDetailsText(activity.DetailsOverride);
                }

                if (!string.IsNullOrEmpty(activity.StateOverride))
                {
                    return LimitDetailsText(activity.StateOverride);
                }
            }

            var recentEvent = recentEventProvider?.Invoke();
            if (recentEvent.HasValue)
            {
                var (state, details) = recentEvent.Value;
                string value = !string.IsNullOrEmpty(details) ? details : state;
                return LimitDetailsText(value);
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
                return ComposeEventLine(activity.StateOverride, activity.DetailsOverride);
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

        private static string GetRecentEventStateLine(Func<(string State, string Details)?> recentEventProvider)
        {
            var recentEvent = recentEventProvider?.Invoke();
            if (!recentEvent.HasValue)
            {
                return null;
            }

            var (state, details) = recentEvent.Value;
            string persistedLine = ComposeEventLine(state, details);
            if (!string.IsNullOrEmpty(persistedLine))
            {
                return persistedLine;
            }

            string sanitizedState = LimitPresenceText(SanitizePresenceText(state));
            return string.IsNullOrEmpty(sanitizedState) ? null : sanitizedState;
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
        internal const string Speed = "RimCord_Speed";
        internal const string MainMenu = "RimCord_MainMenu";
        internal const string BrowsingMods = "RimCord_BrowsingMods";

        // fallbacks incase the translation system hasnt loaded yet
        private static readonly System.Collections.Generic.Dictionary<string, string> Fallbacks = new System.Collections.Generic.Dictionary<string, string>
        {
            { StatusPaused, "Game Paused" },
            { StatusPlaying, "Playing RimWorld" },
            { StatusPlayingRimWorld, "Playing RimWorld" },
            { Year, "Year" },
            { Speed, "speed" },
            { MainMenu, "Main Menu" },
            { BrowsingMods, "Browsing mods and settings" }
        };

        // tries to translate, falls back to english if something goes wrong
        internal static string SafeTranslate(string key)
        {
            try
            {
                // language db might not be ready during early startup
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
