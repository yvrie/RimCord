using System;
using System.Collections.Generic;
using RimCord.GameState;
using RimWorld;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimCord
{
    public class PresenceManager : IDisposable
    {
        private DiscordIPC discordIPC;
        private Task<bool> connectAttemptTask;
        private readonly object syncLock = new object();
        private volatile bool isDisposed;
        private string lastPresenceState = string.Empty;
        private string lastPresenceDetails = string.Empty;
        private string lastImageKey = string.Empty;
        private bool lastPausedState;
        private string lastEventState;
        private string lastEventDetails;
        private bool lastEventIsThreatAlert;
        private static long? sessionStartTimestamp;
        private bool wasConnected;
        private string lastStorytellerKey;
        private string lastStorytellerText;
        private int lastColonistCount;
        private int reconnectAttempts;
        private long nextReconnectAllowedTick;
        private const int MaxReconnectDelayTicks = 3600;

        public PresenceManager()
        {
            discordIPC = new DiscordIPC();
        }

        public bool IsDisposed => isDisposed;

        public void Initialize()
        {
            // only set once so the playing timer doesnt reset
            if (!sessionStartTimestamp.HasValue)
            {
                sessionStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            if (RimCordMod.Settings == null)
            {
                RimCordLogger.Warning("PresenceManager.Initialize skipped because settings are not ready yet.");
                return;
            }

            if (RimCordMod.Settings.EnableRichPresence)
            {
                RequestDiscordConnection();
            }
        }

        public void Update()
        {
            try
            {
                if (RimCordMod.Settings == null)
                {
                    return;
                }

                if (!RimCordMod.Settings.EnableRichPresence)
                {
                    if (discordIPC != null && discordIPC.IsConnected)
                    {
                        discordIPC.ClearPresence();
                    }
                    return;
                }

                if (discordIPC == null)
                {
                    discordIPC = new DiscordIPC();
                }

                TickManager tickManager = null;
                try
                {
                    tickManager = Find.TickManager;
                }
                catch { }

                long currentClock = GetUpdateClock(tickManager);
                bool isPaused = (tickManager != null) ? tickManager.Paused : false;

                bool inGame = false;
                try
                {
                    inGame = Current.ProgramState == ProgramState.Playing;
                }
                catch { }

                var activity = inGame ? ActivityDetector.DetectCurrentActivity() : null;

                string currentState = null;
                string currentDetails = null;
                string currentImageKey = null;

                try
                {
                    currentState = PresenceTextBuilder.BuildState(activity, inGame, TryGetRecentEventSnapshot);
                }
                catch (Exception ex)
                {
                    RimCordLogger.Warning("Error building state: {0}", ex.Message);
                    currentState = inGame ? "RimCord_Status_Playing".Translate() : "RimCord_MainMenu".Translate();
                }

                try
                {
                    currentDetails = PresenceTextBuilder.BuildDetails(activity, inGame, RimCordMod.Settings, TryGetRecentEventSnapshot);
                }
                catch (Exception ex)
                {
                    RimCordLogger.Warning("Error building details: {0}", ex.Message);
                    currentDetails = inGame ? "RimWorld" : "RimCord_BrowsingMods".Translate();
                }

                try
                {
                    currentImageKey = GetImageKeyForContext(activity, inGame, currentClock);
                }
                catch (Exception ex)
                {
                    RimCordLogger.Warning("Error getting image key: {0}", ex.Message);
                    currentImageKey = inGame ? "rimworld" : "rimworld_menu";
                }
            
                bool shouldUpdate = true;
            
                bool isConnected = discordIPC != null && discordIPC.IsConnected;
                if (!isConnected && RimCordMod.Settings != null && RimCordMod.Settings.EnableRichPresence)
                {
                    if (currentClock >= nextReconnectAllowedTick)
                    {
                        wasConnected = false;
                        RequestDiscordConnection();
                        isConnected = discordIPC != null && discordIPC.IsConnected;
                        
                        if (!isConnected)
                        {
                            reconnectAttempts++;
                            int delayTicks = Math.Min(120 * (1 << Math.Min(reconnectAttempts, 5)), MaxReconnectDelayTicks);
                            nextReconnectAllowedTick = currentClock + delayTicks;
                        }
                    }
                }

                if (shouldUpdate && isConnected)
                {
                    bool justReconnected = !wasConnected;
                    if (justReconnected)
                        reconnectAttempts = 0;
                    wasConnected = true;

                    string currentStorytellerKey = null;
                    string currentStorytellerText = null;
                    if (inGame && RimCordMod.Settings?.ShowStorytellerIcon == true)
                    {
                        var storytellerAssets = GameState.StorytellerInfo.GetStorytellerAssets();
                        currentStorytellerKey = storytellerAssets.SmallImageKey;
                        currentStorytellerText = storytellerAssets.SmallImageText;
                    }

                    int currentColonistCount = inGame ? ColonyInfo.GetColonistCount() : 0;

                    bool stateChanged = currentState != lastPresenceState 
                        || currentDetails != lastPresenceDetails 
                        || currentImageKey != lastImageKey
                        || isPaused != lastPausedState
                        || currentStorytellerKey != lastStorytellerKey
                        || currentStorytellerText != lastStorytellerText
                        || currentColonistCount != lastColonistCount
                        || isPaused;
                    
                    if (!stateChanged && !justReconnected)
                    {
                        return;
                    }

                    UpdatePresence(currentState ?? "Main Menu", currentDetails ?? "Browsing mods and settings", activity, inGame);
                    lastPresenceState = currentState;
                    lastColonistCount = currentColonistCount;
                    lastPresenceDetails = currentDetails;
                    lastImageKey = currentImageKey;
                    lastPausedState = isPaused;
                    lastStorytellerKey = currentStorytellerKey;
                    lastStorytellerText = currentStorytellerText;
                }
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in PresenceManager.Update: {0} at {1}", ex.Message, ex.StackTrace);
            }
        }

        private (string State, string Details)? TryGetRecentEventSnapshot()
        {
                if (TryGetRecentEvent(out var state, out var details))
            {
                return (state, details);
            }

            return null;
        }

        private void RequestDiscordConnection()
        {
            if (discordIPC == null)
            {
                discordIPC = new DiscordIPC();
            }

            if (connectAttemptTask != null && !connectAttemptTask.IsCompleted)
            {
                return;
            }

            connectAttemptTask = discordIPC.ConnectAsync();
            connectAttemptTask.ContinueWith(_ =>
            {
                connectAttemptTask = null;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private string GetImageKeyForContext(ActivityInfo activity, bool inGame, long currentClock)
        {
            if (!inGame)
            {
                return "rimworld_menu";
            }

            if (activity != null)
            {
                if (activity.Activity == "Raid" && activity.IsUrgent)
                {
                    return "rimworld_raid";
                }
                
                if (activity.Activity == "MentalBreak")
                {
                    return "rimworld_mentalbreak";
                }
            }

            return "rimworld";
        }

        private string GetImageTextForContext(ActivityInfo activity, bool inGame)
        {
            if (!inGame)
            {
                return "RimCord_MainMenu".Translate();
            }

            if (activity != null)
            {
                if (activity.Activity == "Raid" && activity.IsUrgent)
                {
                    return "RimCord_UnderAttack".Translate();
                }
                
                if (activity.Activity == "MentalBreak")
                {
                    return "RimCord_MentalBreak".Translate();
                }
            }
            
            return "RimWorld";
        }

        private void UpdatePresence(string state, string details, ActivityInfo activity, bool inGame)
        {
            if (discordIPC == null || !discordIPC.IsConnected)
            {
                return;
            }

            string finalState = state;
            string finalDetails = details;


            bool isPaused = false;
            try { isPaused = Find.TickManager?.Paused ?? false; } catch { }

            if (inGame && activity != null && activity.IsUrgent && !isPaused)
            {
                if (activity.Activity == "Raid")
                {
                    try
                    {
                        var settings = RimCordMod.Settings;
                        var activityParts = new System.Collections.Generic.List<string>();
                        
                        if (settings?.ShowColonyName == true)
                        {
                            string colonyName = ColonyInfo.GetColonyName();
                            if (!string.IsNullOrEmpty(colonyName))
                                activityParts.Add(colonyName);
                        }
                        
                        int year = WorldInfo.GetYear();
                        if (year > 0)
                        {
                            string quadrum = settings?.ShowBiome == true ? WorldInfo.GetQuadrum() : null;
                            activityParts.Add(string.IsNullOrEmpty(quadrum)
                                ? string.Format("{0} {1}", "RimCord_Year".Translate(), year)
                                : string.Format("{0} {1}, {2}", "RimCord_Year".Translate(), year, quadrum));
                        }
                        
                        if (settings?.ShowBiome == true)
                        {
                            string biome = WorldInfo.GetBiomeName();
                            if (!string.IsNullOrEmpty(biome))
                                activityParts.Add(biome);
                        }
                        
                        if (activityParts.Count > 0)
                            finalDetails = string.Join(" | ", activityParts);
                    }
                    catch { }
                }
            }

            string largeImageKey = activity?.LargeImageKey;
            string largeImageText = activity?.LargeImageText;
            if (string.IsNullOrEmpty(largeImageKey))
            {
                TickManager tickManager = null;
                try { tickManager = Find.TickManager; } catch { }
                long currentClock = GetUpdateClock(tickManager);
                largeImageKey = GetImageKeyForContext(activity, inGame, currentClock);
            }
            if (string.IsNullOrEmpty(largeImageText))
            {
                largeImageText = GetImageTextForContext(activity, inGame);
            }

            string smallImageKey = activity?.SmallImageKey;
            string smallImageText = activity?.SmallImageText;
            if ((string.IsNullOrEmpty(smallImageKey) && string.IsNullOrEmpty(smallImageText)) && inGame && RimCordMod.Settings != null && RimCordMod.Settings.ShowStorytellerIcon)
            {
                try
                {
                    var storytellerAssets = StorytellerInfo.GetStorytellerAssets();
                    smallImageKey = storytellerAssets.SmallImageKey;
                    smallImageText = storytellerAssets.SmallImageText;
                }
                catch { }
            }

            string finalStateValue = finalState ?? (inGame ? "RimCord_Status_Playing".Translate().ToString() : "RimCord_MainMenu".Translate().ToString());
            string finalDetailsValue = finalDetails ?? (inGame ? "RimWorld" : "RimCord_BrowsingMods".Translate().ToString());
            
            if (string.IsNullOrEmpty(finalStateValue))
            {
                finalStateValue = inGame ? "RimCord_Status_Playing".Translate() : "RimCord_MainMenu".Translate();
            }
            
            if (string.IsNullOrEmpty(finalDetailsValue))
            {
                if (inGame)
                {
                    try
                    {
                        int defaultYear = WorldInfo.GetYear();
                        if (defaultYear > 0)
                        {
                            finalDetailsValue = string.Format("{0} {1}", RimCordText.Year.Translate(), defaultYear);
                        }
                        else
                        {
                            finalDetailsValue = "RimWorld";
                        }
                    }
                    catch
                    {
                        finalDetailsValue = "RimWorld";
                    }
                }
                else
                {
                    finalDetailsValue = "RimCord_BrowsingMods".Translate();
                }
            }

            finalStateValue = PresenceTextBuilder.LimitPresenceText(finalStateValue);
            finalDetailsValue = PresenceTextBuilder.LimitPresenceText(finalDetailsValue, PresenceTextBuilder.MaxPresenceDetailsLength);

            var buttons = BuildButtonsPayload();

            int? partySize = null;
            int? partyMax = null;
            if (inGame && RimCordMod.Settings?.ShowColonistCount == true)
            {
                int colonistCount = ColonyInfo.GetColonistCount();
                if (colonistCount > 0)
                {
                    partySize = colonistCount;
                    partyMax = colonistCount;
                }
            }

            discordIPC.UpdatePresence(
                state: finalStateValue,
                details: finalDetailsValue,
                startTimestamp: sessionStartTimestamp,
                largeImageKey: largeImageKey ?? "rimworld",
                largeImageText: largeImageText ?? (inGame ? "RimWorld" : "Main Menu"),
                smallImageKey: smallImageKey,
                smallImageText: smallImageText,
                buttons: buttons,
                partySize: partySize,
                partyMax: partyMax
            );
        }

        public void Shutdown()
        {
            lock (syncLock)
            {
                if (discordIPC != null)
                {
                    try
                    {
                        discordIPC.ClearPresence();
                    }
                    catch (Exception ex)
                    {
                        RimCordLogger.Warning("Error clearing presence during shutdown: {0}", ex.Message);
                    }

                    try
                    {
                        discordIPC.Dispose();
                    }
                    catch (Exception ex)
                    {
                        RimCordLogger.Warning("Error disposing Discord IPC: {0}", ex.Message);
                    }

                    discordIPC = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            isDisposed = true;

            if (disposing)
            {
                Shutdown();
            }
        }

        ~PresenceManager()
        {
            Dispose(false);
        }

        internal void RecordLetterEvent(string state, string details, bool isThreatAlert = false)
        {
            if (RimCordMod.Settings != null)
            {
                if (!RimCordMod.Settings.ShowLetterEvents)
                {
                    return;
                }

                if (isThreatAlert && !RimCordMod.Settings.ShowThreatAlerts)
                {
                    return;
                }
            }

            RememberLastEvent(state, details, isThreatAlert);
        }

        internal void ClearRecentLetterEvent()
        {
            lastEventState = null;
            lastEventDetails = null;
            lastEventIsThreatAlert = false;
        }

        private void RememberLastEvent(string state, string details, bool isThreatAlert)
        {
            if (string.IsNullOrEmpty(state) && string.IsNullOrEmpty(details))
            {
                return;
            }

            lastEventState = state;
            lastEventDetails = details;
            lastEventIsThreatAlert = isThreatAlert;
        }

        private bool TryGetRecentEvent(out string state, out string details)
        {
            if (RimCordMod.Settings != null)
            {
                if (!RimCordMod.Settings.ShowLetterEvents)
                {
                    state = null;
                    details = null;
                    return false;
                }

                if (lastEventIsThreatAlert && !RimCordMod.Settings.ShowThreatAlerts)
                {
                    state = null;
                    details = null;
                    return false;
                }
            }

            state = lastEventState;
            details = lastEventDetails;
            return !string.IsNullOrEmpty(state) || !string.IsNullOrEmpty(details);
        }

        private long GetUpdateClock(TickManager tickManager)
        {
            if (tickManager != null)
            {
                return tickManager.TicksGame;
            }

            return (long)(Time.realtimeSinceStartup * 60f);
        }

        private List<(string Label, string Url)> BuildButtonsPayload()
        {
            var settings = RimCordMod.Settings;
            if (settings == null || !settings.EnableCustomButton)
            {
                return null;
            }

            string label = settings.CustomButtonLabel?.Trim();
            string url = settings.CustomButtonUrl?.Trim();

            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(url))
                return null;

            if (!RimCordSettings.IsValidButtonUrl(url))
                return null;

            if (label.Length > 32)
            {
                label = label.Substring(0, 32);
            }

            return new List<(string Label, string Url)>(1) { (label, url) };
        }

    }
}
