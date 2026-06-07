using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RimCord.GameState
{
    internal static class TextUtil
    {
        private static readonly Regex GrammarStarPattern = new Regex(@"\(\*[^)]*\)", RegexOptions.Compiled);
        private static readonly Regex GrammarSlashPattern = new Regex(@"\(/[^)]*\)", RegexOptions.Compiled);
        private static readonly Regex BracketPattern = new Regex(@"\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex TagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex WordPattern = new Regex(@"[\p{L}\p{Nd}]+", RegexOptions.Compiled);

        internal static bool ContainsIgnoreCase(string source, string value)
        {
            return source != null && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool AreEquivalent(string left, string right)
        {
            string normalizedLeft = NormalizeForComparison(left);
            string normalizedRight = NormalizeForComparison(right);
            return normalizedLeft.Length > 0
                && string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
        }

        internal static string RemoveRepeatedPrefix(string value, string prefix)
        {
            string cleanValue = Clean(value);
            string cleanPrefix = Clean(prefix);
            if (cleanValue == null || cleanPrefix == null)
            {
                return cleanValue;
            }

            if (AreEquivalent(cleanValue, cleanPrefix))
            {
                return null;
            }

            MatchCollection valueWords = WordPattern.Matches(cleanValue);
            MatchCollection prefixWords = WordPattern.Matches(cleanPrefix);
            if (prefixWords.Count == 0 || valueWords.Count < prefixWords.Count)
            {
                return cleanValue;
            }

            for (int i = 0; i < prefixWords.Count; i++)
            {
                if (!string.Equals(
                    valueWords[i].Value,
                    prefixWords[i].Value,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return cleanValue;
                }
            }

            Match lastPrefixWord = valueWords[prefixWords.Count - 1];
            string remainder = cleanValue.Substring(lastPrefixWord.Index + lastPrefixWord.Length)
                .TrimStart(' ', '\t', ':', ';', '-', '\u2013', '\u2014', '.', ',', '!', '?', '\u2026', '|', '/');

            return string.IsNullOrWhiteSpace(remainder) ? null : remainder;
        }

        private static string NormalizeForComparison(string value)
        {
            string clean = Clean(value);
            if (clean == null)
            {
                return string.Empty;
            }

            var words = new List<string>();
            foreach (Match match in WordPattern.Matches(clean))
            {
                words.Add(match.Value.ToLowerInvariant());
            }

            return string.Join(" ", words);
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string result = GrammarStarPattern.Replace(value, string.Empty);
            result = GrammarSlashPattern.Replace(result, string.Empty);
            result = BracketPattern.Replace(result, string.Empty);
            result = TagPattern.Replace(result, string.Empty);
            result = WhitespacePattern.Replace(result, " ");
            return result.Trim();
        }
    }
}
