using Verse;

namespace RimCord
{
    internal static class RimCordLogger
    {
        private const string Prefix = "<color=#FFA500>[RimCord]</color>";

        private static string BuildMessage(string message)
        {
            return string.Format("{0} {1}", Prefix, message ?? string.Empty);
        }

        public static void Info(string message)
        {
            Log.Message(BuildMessage(message));
        }

        public static void Info(string format, params object[] args)
        {
            Log.Message(BuildMessage(string.Format(format, args)));
        }

        public static void Warning(string message)
        {
            Log.Warning(BuildMessage(message));
        }

        public static void Warning(string format, params object[] args)
        {
            Log.Warning(BuildMessage(string.Format(format, args)));
        }

        // these do nothing in release, used for debugging without spamming the log
        public static void SilentInfo(string message) { }
        public static void SilentInfo(string format, params object[] args) { }

        public static void Error(string message)
        {
            Log.Error(BuildMessage(message));
        }

        public static void Error(string format, params object[] args)
        {
            Log.Error(BuildMessage(string.Format(format, args)));
        }
    }
}
