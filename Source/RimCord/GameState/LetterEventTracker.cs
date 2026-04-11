using System;
using System.Reflection;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace RimCord.GameState
{
    public static class LetterEventTracker
    {
        private const int DefaultDurationSeconds = 12;
        private const int MaxDetailsLength = 160;
        private const int MaxBriefDetailsLength = 72;
        private const double DuplicateWindowSeconds = 0.75;
        private static DateTime lastRecordedAtUtc = DateTime.MinValue;
        private static string lastRecordedState;
        private static string lastRecordedDetails;
        private static readonly MethodInfo GetMouseoverMethod = typeof(Letter).GetMethod("GetMouseoverText", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Regex GrammarStarPattern = new Regex(@"\(\*[^)]*\)", RegexOptions.Compiled);
        private static readonly Regex GrammarSlashPattern = new Regex(@"\(/[^)]*\)", RegexOptions.Compiled);
        private static readonly Regex BracketPattern = new Regex(@"\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex TagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);

        public static void NotifyLetter(Letter letter)
        {
            if (letter == null)
            {
                return;
            }

            var label = letter.Label;
            var text = TryGetLetterText(letter);

            string state = StripGrammarTokens(ResolveTaggedString(label));
            if (string.IsNullOrWhiteSpace(state))
            {
                state = letter.def?.LabelCap ?? "RimCord_Event".Translate();
            }

            RecordLetterEvent(letter, letter.def, state, text);
        }

        public static void NotifyReceiveLetterArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            foreach (var arg in args)
            {
                if (arg is Letter letter)
                {
                    NotifyLetter(letter);
                    return;
                }
            }

            string state = null;
            string text = null;
            LetterDef def = null;

            foreach (var arg in args)
            {
                if (def == null && arg is LetterDef letterDef)
                {
                    def = letterDef;
                    continue;
                }

                string value = ResolveTextArgument(arg);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (state == null)
                {
                    state = value;
                }
                else if (text == null)
                {
                    text = value;
                }
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                state = def?.LabelCap ?? "RimCord_Event".Translate();
            }

            RecordLetterEvent(null, def, state, text);
        }

        private static void RecordLetterEvent(Letter letter, LetterDef def, string state, string text)
        {
            state = StripGrammarTokens(TrimAndLimit(state, MaxDetailsLength));
            string detectionText = StripGrammarTokens(TrimAndLimit(CombineLabelAndBody(state, text), MaxDetailsLength));
            bool isManInBlack = IsManInBlackLetter(letter, def, state, detectionText);
            if (isManInBlack && string.IsNullOrWhiteSpace(state))
            {
                state = "Man in black";
            }

            string details = BuildBriefDetails(state, text);
            bool isUrgent = !isManInBlack && IsUrgentLetter(def);
            bool isMentalBreak = !isManInBlack && IsMentalBreakLetter(def, state, detectionText);
            var settings = RimCordMod.Settings;

            if (settings != null)
            {
                if (!settings.ShowLetterEvents)
                {
                    return;
                }

                if (isMentalBreak && !settings.ShowThreatAlerts)
                {
                    return;
                }
            }

            if (ShouldIgnoreLetter(letter, state, detectionText))
            {
                return;
            }

            if (IsDuplicateEvent(state, details))
            {
                return;
            }

            PresenceEventQueue.Enqueue(state, details, durationSeconds: DefaultDurationSeconds, isUrgent: isUrgent, isMentalBreak: isMentalBreak, isThreatAlert: isMentalBreak);
            RememberDuplicateKey(state, details);

            if (RimCordMod.PresenceManager != null)
            {
                RimCordMod.PresenceManager.RecordLetterEvent(state, details, isMentalBreak);
            }
        }

        private static string TryGetLetterText(Letter letter)
        {
            if (letter == null)
            {
                return null;
            }

            if (GetMouseoverMethod != null)
            {
                try
                {
                    return GetMouseoverMethod.Invoke(letter, Array.Empty<object>()) as string;
                }
                catch
                {
                }
            }

            return null;
        }

        private static string ResolveTextArgument(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is TaggedString tagged)
            {
                return ResolveTaggedString(tagged);
            }

            if (value is string text)
            {
                return StripGrammarTokens(text);
            }

            return null;
        }

        private static string ResolveTaggedString(TaggedString value)
        {
            if (value.NullOrEmpty())
            {
                return null;
            }

            string resolved = value.Resolve();
            string combined = StripGrammarTokens(resolved);
            return string.IsNullOrWhiteSpace(combined) ? null : combined.Trim();
        }

        private static string CombineLabelAndBody(string label, string body)
        {
            string normalizedLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            string normalizedBody = string.IsNullOrWhiteSpace(body) ? null : body.Trim();

            if (normalizedLabel == null && normalizedBody == null)
            {
                return null;
            }

            if (normalizedLabel == null)
            {
                return normalizedBody;
            }

            if (normalizedBody == null)
            {
                return normalizedLabel;
            }

            if (normalizedBody.StartsWith(normalizedLabel, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedBody;
            }

            return string.Format("{0}: {1}", normalizedLabel, normalizedBody);
        }

        private static string BuildBriefDetails(string state, string body)
        {
            string fallback = StripGrammarTokens(TrimAndLimit(state, MaxBriefDetailsLength));
            string summary = ExtractBriefSummary(body);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return fallback;
            }

            string cleanState = StripGrammarTokens(state);
            if (!string.IsNullOrEmpty(cleanState) && summary.StartsWith(cleanState, StringComparison.OrdinalIgnoreCase))
            {
                summary = summary.Substring(cleanState.Length).TrimStart(' ', ':', '-', '.', '!', '?');
            }

            if (string.IsNullOrWhiteSpace(summary) || string.Equals(summary, cleanState, StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            return TrimAndLimit(summary, MaxBriefDetailsLength);
        }

        private static string ExtractBriefSummary(string value)
        {
            string clean = StripGrammarTokens(value);
            if (string.IsNullOrWhiteSpace(clean))
            {
                return null;
            }

            int sentenceEnd = FindSentenceEnd(clean);
            if (sentenceEnd >= 0 && sentenceEnd < MaxBriefDetailsLength)
            {
                return clean.Substring(0, sentenceEnd + 1).Trim();
            }

            return TrimAndLimit(clean, MaxBriefDetailsLength);
        }

        private static int FindSentenceEnd(string value)
        {
            int maxSearch = Math.Min(value.Length, MaxBriefDetailsLength);
            for (int i = 8; i < maxSearch; i++)
            {
                char c = value[i];
                if (c == '.' || c == '!' || c == '?')
                {
                    return i;
                }
            }

            return -1;
        }

        private static string TrimAndLimit(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            string trimmed = value.Replace('\n', ' ').Replace("\r", string.Empty).Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            int length = Math.Min(maxLength, trimmed.Length);
            string shortened = trimmed.Substring(0, length).TrimEnd();
            return shortened + "...";
        }

        private static bool IsUrgentLetter(LetterDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (def == LetterDefOf.ThreatBig || def == LetterDefOf.ThreatSmall || def == LetterDefOf.NegativeEvent)
            {
                return true;
            }

            if (def.pauseMode == AutomaticPauseMode.MajorThreat || def.pauseMode == AutomaticPauseMode.AnyThreat || def.forcedSlowdown)
            {
                return true;
            }

            string identity = def.defName?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(identity))
            {
                if (identity.Contains("threat") || identity.Contains("raid") || identity.Contains("attack") || identity.Contains("danger"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripGrammarTokens(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            string result = GrammarStarPattern.Replace(value, string.Empty);
            result = GrammarSlashPattern.Replace(result, string.Empty);
            result = BracketPattern.Replace(result, string.Empty);
            result = TagPattern.Replace(result, string.Empty);
            result = WhitespacePattern.Replace(result, " ");
            return result.Trim();
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source != null && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMentalBreakLetter(LetterDef def, string state, string details)
        {
            if (def != LetterDefOf.NegativeEvent)
                return false;

            string combined = string.Concat(state, " ", details);
            return ContainsIgnoreCase(combined, "mental") || ContainsIgnoreCase(combined, "berserk") ||
                   ContainsIgnoreCase(combined, "tantrum") || ContainsIgnoreCase(combined, "breakdown") ||
                   ContainsIgnoreCase(combined, "wander") || ContainsIgnoreCase(combined, "binge") ||
                   ContainsIgnoreCase(combined, "hide") || ContainsIgnoreCase(combined, "insult") ||
                   ContainsIgnoreCase(combined, "murderous") || ContainsIgnoreCase(combined, "sad");
        }

        private static bool IsManInBlackLetter(Letter letter, LetterDef def, string state, string details)
        {
            string defName = def?.defName ?? letter?.def?.defName;
            if (string.Equals(defName, "StrangerInBlackJoin", StringComparison.OrdinalIgnoreCase))
                return true;

            string combined = string.Concat(state, " ", details);
            return ContainsIgnoreCase(combined, "man in black")
                || ContainsIgnoreCase(combined, "woman in black")
                || ContainsIgnoreCase(combined, "stranger in black");
        }

        private static bool ShouldIgnoreLetter(Letter letter, string state, string details)
        {
            string combined = string.Concat(state, " ", details);
            if (string.IsNullOrWhiteSpace(combined))
                return false;

            return ContainsIgnoreCase(combined, "raid");
        }

        private static bool IsDuplicateEvent(string state, string details)
        {
            if ((DateTime.UtcNow - lastRecordedAtUtc).TotalSeconds > DuplicateWindowSeconds)
            {
                return false;
            }

            return string.Equals(state, lastRecordedState, StringComparison.Ordinal)
                && string.Equals(details, lastRecordedDetails, StringComparison.Ordinal);
        }

        private static void RememberDuplicateKey(string state, string details)
        {
            lastRecordedState = state;
            lastRecordedDetails = details;
            lastRecordedAtUtc = DateTime.UtcNow;
        }
    }
}
