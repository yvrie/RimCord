using System;
using Verse;

namespace RimCord.GameState
{
    public class ActivityInfo
    {
        public string Activity { get; set; }
        public int Priority { get; set; }
        public bool IsUrgent { get; set; }
        public string RaidFactionName { get; set; }
        public DateTime? RaidStartTime { get; set; }
        public MentalBreakInfo MentalBreakInfo { get; set; }
        public string StateOverride { get; set; }
        public string DetailsOverride { get; set; }
        public string LargeImageKey { get; set; }
        public string LargeImageText { get; set; }
        public string SmallImageKey { get; set; }
        public string SmallImageText { get; set; }
    }

    public static class ActivityDetector
    {
        private static readonly ActivityInfo raidActivity = new ActivityInfo { Activity = "Raid", Priority = 100, IsUrgent = true };
        private static readonly ActivityInfo mentalBreakActivity = new ActivityInfo { Activity = "MentalBreak", Priority = 95, IsUrgent = true };
        private static readonly ActivityInfo queuedEventActivity = new ActivityInfo { Activity = "QueuedEvent", Priority = 90 };
        private static readonly ActivityInfo gameConditionActivity = new ActivityInfo { Activity = "GameCondition", Priority = 60, IsUrgent = false };

        public static ActivityInfo DetectCurrentActivity()
        {
            ActivityInfo highestPriority = null;
            int highestPriorityValue = -1;
            bool showLetterEvents = RimCordMod.Settings == null || RimCordMod.Settings.ShowLetterEvents;
            bool showThreatAlerts = RimCordMod.Settings == null || RimCordMod.Settings.ShowThreatAlerts;
            bool showGameConditions = RimCordMod.Settings == null || RimCordMod.Settings.ShowGameConditions;

            var queuedEvent = PresenceEventQueue.GetCurrentEvent();
            if (showLetterEvents && queuedEvent != null && (showThreatAlerts || !queuedEvent.IsThreatAlert))
            {
                queuedEventActivity.IsUrgent = queuedEvent.IsUrgent;
                queuedEventActivity.StateOverride = queuedEvent.State;
                queuedEventActivity.DetailsOverride = queuedEvent.Details;
                queuedEventActivity.LargeImageKey = queuedEvent.ImageKey;
                queuedEventActivity.LargeImageText = queuedEvent.ImageText;
                
                if (queuedEventActivity.Priority > highestPriorityValue)
                {
                    highestPriority = queuedEventActivity;
                    highestPriorityValue = queuedEventActivity.Priority;
                }
            }

            var map = Find.CurrentMap;
            if (map == null)
            {
                return highestPriority;
            }

            if (showThreatAlerts && RaidTracker.IsRaidActive())
            {
                raidActivity.RaidFactionName = RaidTracker.GetRaidFactionName();
                raidActivity.RaidStartTime = DateTime.UtcNow;
                
                if (raidActivity.Priority > highestPriorityValue)
                {
                    highestPriority = raidActivity;
                    highestPriorityValue = raidActivity.Priority;
                }
            }

            var mentalBreakInfo = showThreatAlerts ? MentalBreakTracker.GetActiveMentalBreakInfo() : null;
            if (mentalBreakInfo != null)
            {
                mentalBreakActivity.MentalBreakInfo = mentalBreakInfo;
                
                if (mentalBreakActivity.Priority > highestPriorityValue)
                {
                    highestPriority = mentalBreakActivity;
                    highestPriorityValue = mentalBreakActivity.Priority;
                }
            }

            string conditionLabel = showGameConditions ? GameConditionInfo.GetTopConditionLabel() : null;
            if (!string.IsNullOrEmpty(conditionLabel))
            {
                gameConditionActivity.StateOverride = conditionLabel;
                
                if (gameConditionActivity.Priority > highestPriorityValue)
                {
                    highestPriority = gameConditionActivity;
                    highestPriorityValue = gameConditionActivity.Priority;
                }
            }

            return highestPriority;
        }

        public static string GetActivityDisplayText(ActivityInfo activity, bool useLocalized = true)
        {
            if (activity == null)
                return null;

            if (useLocalized)
            {
                string key = string.Format("RimCord_Activity_{0}", activity.Activity);
                return key.Translate();
            }

            return activity.Activity;
        }
    }
}
