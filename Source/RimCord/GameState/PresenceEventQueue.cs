using System;

namespace RimCord.GameState
{
    public class QueuedPresenceEvent
    {
        public string State;
        public string Details;
        public bool IsUrgent;
        public bool IsThreatAlert;
        public bool IsMentalBreak;
        public string ImageKey;
        public string ImageText;
        public DateTime ExpiresAtUtc;

        public void Clear()
        {
            State = null;
            Details = null;
            IsUrgent = false;
            IsThreatAlert = false;
            IsMentalBreak = false;
            ImageKey = null;
            ImageText = null;
            ExpiresAtUtc = DateTime.MinValue;
        }
    }

    public static class PresenceEventQueue
    {
        private static readonly QueuedPresenceEvent eventInstance = new QueuedPresenceEvent();
        private static bool hasActiveEvent;
        private const int DefaultDurationSeconds = 5;

        public static void Reset()
        {
            ClearCurrentEvent();
        }

        public static void ClearCurrentEvent()
        {
            hasActiveEvent = false;
            eventInstance.Clear();
        }

        public static void ClearThreatEvent()
        {
            if (hasActiveEvent && eventInstance.IsThreatAlert)
            {
                ClearCurrentEvent();
            }
        }

        public static void ClearMentalBreakEvent()
        {
            if (hasActiveEvent && eventInstance.IsMentalBreak)
            {
                ClearCurrentEvent();
            }
        }

        public static void Enqueue(string state, string details, int durationSeconds = 5, bool isUrgent = false, string imageKey = null, string imageText = null, bool isMentalBreak = false, bool isThreatAlert = false)
        {
            if (string.IsNullOrEmpty(state) && string.IsNullOrEmpty(details))
                return;

            bool showThreatAlerts = RimCordMod.Settings == null ||
                (RimCordMod.Settings.ShowLetterEvents && RimCordMod.Settings.ShowThreatAlerts);
            if (showThreatAlerts && !isMentalBreak && RaidTracker.IsRaidActive())
                return;

            if (durationSeconds <= 0)
                durationSeconds = DefaultDurationSeconds;

            eventInstance.State = state;
            eventInstance.Details = details;
            eventInstance.IsUrgent = isUrgent;
            eventInstance.IsThreatAlert = isThreatAlert;
            eventInstance.IsMentalBreak = isMentalBreak;
            eventInstance.ImageKey = imageKey;
            eventInstance.ImageText = imageText;
            eventInstance.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(durationSeconds);
            hasActiveEvent = true;
        }

        public static QueuedPresenceEvent GetCurrentEvent()
        {
            if (!hasActiveEvent)
                return null;

            if (DateTime.UtcNow >= eventInstance.ExpiresAtUtc)
            {
                ClearCurrentEvent();
                return null;
            }

            return eventInstance;
        }
    }
}
