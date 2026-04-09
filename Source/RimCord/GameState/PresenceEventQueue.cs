using Verse;

namespace RimCord.GameState
{
    public class QueuedPresenceEvent
    {
        public string State;
        public string Details;
        public bool IsUrgent;
        public bool IsThreatAlert;
        public string ImageKey;
        public string ImageText;
        public int DurationTicks;
        public int StartedAtTick;

        public void Clear()
        {
            State = null;
            Details = null;
            IsUrgent = false;
            IsThreatAlert = false;
            ImageKey = null;
            ImageText = null;
            DurationTicks = 0;
            StartedAtTick = 0;
        }
    }

    public static class PresenceEventQueue
    {
        private static readonly QueuedPresenceEvent eventInstance = new QueuedPresenceEvent();
        private static bool hasActiveEvent;
        private const int DefaultDurationTicks = 300;

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

        public static void Enqueue(string state, string details, int durationSeconds = 5, bool isUrgent = false, string imageKey = null, string imageText = null, bool isMentalBreak = false, bool isThreatAlert = false)
        {
            if (string.IsNullOrEmpty(state) && string.IsNullOrEmpty(details))
                return;

            bool showThreatAlerts = RimCordMod.Settings == null || RimCordMod.Settings.ShowThreatAlerts;
            if (RaidTracker.IsRaidActive() && !isMentalBreak && showThreatAlerts)
                return;

            int durationTicks = durationSeconds * 60;
            if (durationTicks <= 0)
                durationTicks = DefaultDurationTicks;

            var tickManager = Find.TickManager;
            int ticksGame = tickManager != null ? tickManager.TicksGame : 0;

            eventInstance.State = state;
            eventInstance.Details = details;
            eventInstance.DurationTicks = durationTicks;
            eventInstance.IsUrgent = isUrgent;
            eventInstance.IsThreatAlert = isThreatAlert;
            eventInstance.ImageKey = imageKey;
            eventInstance.ImageText = imageText;
            eventInstance.StartedAtTick = ticksGame;
            hasActiveEvent = true;
        }

        public static QueuedPresenceEvent GetCurrentEvent()
        {
            if (!hasActiveEvent)
                return null;

            var tickManager = Find.TickManager;
            int ticksGame = tickManager != null ? tickManager.TicksGame : 0;
            int elapsed = ticksGame - eventInstance.StartedAtTick;

            if (tickManager == null || elapsed < 0 || elapsed > eventInstance.DurationTicks)
            {
                ClearCurrentEvent();
                return null;
            }

            return eventInstance;
        }
    }
}
