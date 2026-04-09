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

            string details = StripGrammarTokens(TrimAndLimit(CombineLabelAndBody(state, text), MaxDetailsLength));
            bool isUrgent = IsUrgentLetter(letter.def);
            bool isMentalBreak = IsMentalBreakLetter(letter, state, details);
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

            if (ShouldIgnoreLetter(letter, state, details))
            {
                return;
            }

            PresenceEventQueue.Enqueue(state, details, durationSeconds: DefaultDurationSeconds, isUrgent: isUrgent, isMentalBreak: isMentalBreak, isThreatAlert: isMentalBreak);

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

        private static bool IsMentalBreakLetter(Letter letter, string state, string details)
        {
            if (letter.def != LetterDefOf.NegativeEvent)
                return false;

            string combined = string.Concat(state, " ", details);
            return ContainsIgnoreCase(combined, "mental") || ContainsIgnoreCase(combined, "berserk") ||
                   ContainsIgnoreCase(combined, "tantrum") || ContainsIgnoreCase(combined, "breakdown") ||
                   ContainsIgnoreCase(combined, "wander") || ContainsIgnoreCase(combined, "binge") ||
                   ContainsIgnoreCase(combined, "hide") || ContainsIgnoreCase(combined, "insult") ||
                   ContainsIgnoreCase(combined, "murderous") || ContainsIgnoreCase(combined, "sad");
        }

        private static bool ShouldIgnoreLetter(Letter letter, string state, string details)
        {
            string combined = string.Concat(state, " ", details);
            if (string.IsNullOrWhiteSpace(combined))
                return false;

            return ContainsIgnoreCase(combined, "raid");
        }
    }
}
