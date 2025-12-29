using Verse;

namespace RimCord.GameState
{
    public class QueuedPresenceEvent
    {
        public string State;
        public string Details;
        public bool IsUrgent;
        public string ImageKey;
        public string ImageText;
        public int DurationTicks;
        public int StartedAtTick;

        public void Clear()
        {
            State = null;
            Details = null;
            IsUrgent = false;
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
            hasActiveEvent = false;
            eventInstance.Clear();
        }

        public static void Enqueue(string state, string details, int durationSeconds = 5, bool isUrgent = false, string imageKey = null, string imageText = null, bool isMentalBreak = false)
        {
            if (string.IsNullOrEmpty(state) && string.IsNullOrEmpty(details))
                return;

            if (RaidTracker.IsRaidActive() && !isMentalBreak)
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
                hasActiveEvent = false;
                return null;
            }

            return eventInstance;
        }
    }
}
