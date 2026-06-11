using System;
using RimCord.GameState;
using Verse;

namespace RimCord
{
    public class RimCordSettings : ModSettings
    {
        public bool EnableRichPresence = true;
        public bool ShowColonyName = false;
        public bool ShowColonistCount = true;
        public bool ShowInGameYear = true;
        public bool ShowInGameQuadrum = true;
        public bool EnablePauseDetection = true;
        public int PauseDisplayDelaySeconds = 60;
        public bool ShowMainMenuModCount = true;

        public bool ShowBiome = false;
        public bool ShowStorytellerIcon = true;
        public bool ShowLetterEvents = true;
        public bool ShowThreatAlerts = true;
        public bool ShowGameConditions = true;

        public bool EnableCustomButton = false;
        public string CustomButtonLabel = GetDefaultButtonLabel();
        public string CustomButtonUrl = string.Empty;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableRichPresence, "EnableRichPresence", true);
            Scribe_Values.Look(ref ShowColonyName, "ShowColonyName", false);
            Scribe_Values.Look(ref ShowColonistCount, "ShowColonistCount", true);
            Scribe_Values.Look(ref ShowInGameYear, "ShowInGameYear", true);
            Scribe_Values.Look(ref ShowInGameQuadrum, "ShowInGameQuadrum", true);
            Scribe_Values.Look(ref EnablePauseDetection, "EnablePauseDetection", true);
            Scribe_Values.Look(ref PauseDisplayDelaySeconds, "PauseDisplayDelaySeconds", 60);
            ClampPauseDisplayDelay();
            Scribe_Values.Look(ref ShowMainMenuModCount, "ShowMainMenuModCount", true);

            Scribe_Values.Look(ref ShowBiome, "ShowBiome", false);
            Scribe_Values.Look(ref ShowStorytellerIcon, "ShowStorytellerIcon", true);
            Scribe_Values.Look(ref ShowLetterEvents, "ShowLetterEvents", true);
            Scribe_Values.Look(ref ShowThreatAlerts, "ShowThreatAlerts", true);
            Scribe_Values.Look(ref ShowGameConditions, "ShowGameConditions", true);

            Scribe_Values.Look(ref EnableCustomButton, "EnableCustomButton", false);
            Scribe_Values.Look(ref CustomButtonLabel, "CustomButtonLabel", GetDefaultButtonLabel());
            Scribe_Values.Look(ref CustomButtonUrl, "CustomButtonUrl", string.Empty);
        }

        private static string GetDefaultButtonLabel()
        {
            return RimCordText.SafeTranslate(RimCordText.DefaultButtonLabel);
        }

        private UnityEngine.Vector2 settingsScrollPosition = UnityEngine.Vector2.zero;
        private float settingsViewHeight = 600f;
        private string pauseDisplayDelayBuffer;
        private int selectedSettingsSection;

        public void DoWindowContents(UnityEngine.Rect inRect)
        {
            float viewWidth = inRect.width - 16f;
            if (viewWidth < 0f)
                viewWidth = inRect.width;

            var viewRect = new UnityEngine.Rect(0f, 0f, viewWidth, settingsViewHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.CheckboxLabeled("RimCord_EnableRichPresence".Translate(), ref EnableRichPresence, "RimCord_EnableRichPresenceDesc".Translate());

            if (EnableRichPresence)
            {
                listing.Gap(8f);
                DrawSectionSelector(listing);

                switch (selectedSettingsSection)
                {
                    case 1:
                        DrawMainMenuSection(listing);
                        break;
                    case 2:
                        DrawEventsSection(listing);
                        break;
                    case 3:
                        DrawCustomButtonSection(listing);
                        break;
                    default:
                        DrawStatusSection(listing);
                        break;
                }

                listing.Gap(24f);
            }

            settingsViewHeight = Math.Max(listing.CurHeight + 12f, inRect.height + 1f);
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawSectionHeader(Listing_Standard listing, string title)
        {
            listing.Gap(10f);
            listing.GapLine(6f);
            listing.Gap(4f);
            UnityEngine.GUI.color = new UnityEngine.Color(0.7f, 0.85f, 1f);
            listing.Label(title);
            UnityEngine.GUI.color = UnityEngine.Color.white;
            listing.Gap(4f);
        }

        private void DrawSectionSelector(Listing_Standard listing)
        {
            var row = listing.GetRect(34f);
            float gap = 6f;
            float width = (row.width - gap * 3f) / 4f;
            DrawSectionButton(new UnityEngine.Rect(row.x, row.y, width, row.height), "RimCord_Settings_Section_Status".Translate(), 0);
            DrawSectionButton(new UnityEngine.Rect(row.x + (width + gap), row.y, width, row.height), "RimCord_Settings_Section_MainMenu".Translate(), 1);
            DrawSectionButton(new UnityEngine.Rect(row.x + (width + gap) * 2f, row.y, width, row.height), "RimCord_Settings_Section_Events".Translate(), 2);
            DrawSectionButton(new UnityEngine.Rect(row.x + (width + gap) * 3f, row.y, width, row.height), "RimCord_Settings_Section_Button".Translate(), 3);
            listing.Gap(4f);
        }

        private void DrawSectionButton(UnityEngine.Rect rect, string label, int section)
        {
            var previousColor = UnityEngine.GUI.color;
            if (selectedSettingsSection == section)
            {
                UnityEngine.GUI.color = new UnityEngine.Color(0.7f, 0.85f, 1f);
            }

            if (Widgets.ButtonText(rect, label))
            {
                selectedSettingsSection = section;
                settingsScrollPosition = UnityEngine.Vector2.zero;
            }

            UnityEngine.GUI.color = previousColor;
        }

        private void DrawStatusSection(Listing_Standard listing)
        {
            DrawSectionHeader(listing, "RimCord_Settings_Section_Status".Translate());

            listing.CheckboxLabeled("RimCord_ShowColonyName".Translate(), ref ShowColonyName, "RimCord_ShowColonyNameDesc".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimCord_ShowColonistCount".Translate(), ref ShowColonistCount, "RimCord_ShowColonistCountDesc".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimCord_ShowInGameYear".Translate(), ref ShowInGameYear, "RimCord_ShowInGameYearDesc".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimCord_ShowInGameQuadrum".Translate(), ref ShowInGameQuadrum, "RimCord_ShowInGameQuadrumDesc".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimCord_EnablePauseDetection".Translate(), ref EnablePauseDetection, "RimCord_EnablePauseDetectionDesc".Translate());
            listing.Gap(4f);
            DrawPauseDisplayDelayField(listing, EnablePauseDetection);
            listing.Gap(4f);
            listing.CheckboxLabeled("RimCord_ShowBiome".Translate(), ref ShowBiome, "RimCord_ShowBiomeDesc".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimCord_ShowStorytellerIcon".Translate(), ref ShowStorytellerIcon, "RimCord_ShowStorytellerIconDesc".Translate());
        }

        private void DrawMainMenuSection(Listing_Standard listing)
        {
            DrawSectionHeader(listing, "RimCord_Settings_Section_MainMenu".Translate());

            listing.CheckboxLabeled("RimCord_ShowMainMenuModCount".Translate(), ref ShowMainMenuModCount, "RimCord_ShowMainMenuModCountDesc".Translate());
        }

        private void DrawEventsSection(Listing_Standard listing)
        {
            DrawSectionHeader(listing, "RimCord_Settings_Section_Events".Translate());

            listing.CheckboxLabeled("RimCord_ShowLetterEvents".Translate(), ref ShowLetterEvents, "RimCord_ShowLetterEventsDesc".Translate());
            listing.Gap(3f);
            DrawIndentedCheckbox(listing, "RimCord_ShowThreatAlerts".Translate(), ref ShowThreatAlerts, "RimCord_ShowThreatAlertsDesc".Translate(), ShowLetterEvents);

            listing.Gap(4f);
            listing.CheckboxLabeled("RimCord_ShowGameConditions".Translate(), ref ShowGameConditions, "RimCord_ShowGameConditionsDesc".Translate());
        }

        private void DrawCustomButtonSection(Listing_Standard listing)
        {
            DrawSectionHeader(listing, "RimCord_Settings_Section_Button".Translate());

            listing.CheckboxLabeled("RimCord_EnableCustomButton".Translate(), ref EnableCustomButton, "RimCord_EnableCustomButtonDesc".Translate());

            listing.Gap(4f);
            UnityEngine.GUI.color = new UnityEngine.Color(0.8f, 0.8f, 0.8f);
            listing.Label("RimCord_ButtonNote".Translate());
            UnityEngine.GUI.color = UnityEngine.Color.white;

            listing.Gap(8f);
            DrawFullWidthTextField(listing, "RimCord_CustomButtonLabel".Translate(), ref CustomButtonLabel, 32);

            listing.Gap(8f);
            DrawFullWidthTextField(listing, "RimCord_CustomButtonUrl".Translate(), ref CustomButtonUrl, 256);

            if (!string.IsNullOrWhiteSpace(CustomButtonUrl) && !IsValidButtonUrl(CustomButtonUrl))
            {
                listing.Gap(3f);
                UnityEngine.GUI.color = new UnityEngine.Color(1f, 0.75f, 0.1f);
                listing.Label("RimCord_UrlWarning".Translate());
                UnityEngine.GUI.color = UnityEngine.Color.white;
            }
        }

        private void DrawPauseDisplayDelayField(Listing_Standard listing, bool enabled)
        {
            if (pauseDisplayDelayBuffer == null)
            {
                pauseDisplayDelayBuffer = PauseDisplayDelaySeconds.ToString();
            }

            var labelRect = listing.GetRect(Text.LineHeight);
            labelRect.xMin += 22f;
            var previousColor = UnityEngine.GUI.color;
            if (!enabled)
            {
                UnityEngine.GUI.color = new UnityEngine.Color(1f, 1f, 1f, 0.4f);
            }
            Widgets.Label(labelRect, "RimCord_PauseDisplayDelaySeconds".Translate());
            TooltipHandler.TipRegion(labelRect, "RimCord_PauseDisplayDelaySecondsDesc".Translate().ToString());
            UnityEngine.GUI.color = previousColor;

            var entryRect = listing.GetRect(Text.LineHeight);
            entryRect.xMin += 22f;
            if (!enabled)
            {
                pauseDisplayDelayBuffer = PauseDisplayDelaySeconds.ToString();
                previousColor = UnityEngine.GUI.color;
                UnityEngine.GUI.color = new UnityEngine.Color(1f, 1f, 1f, 0.4f);
                Widgets.TextField(entryRect, pauseDisplayDelayBuffer);
                UnityEngine.GUI.color = previousColor;
                return;
            }

            string enteredValue = Widgets.TextField(entryRect, pauseDisplayDelayBuffer);
            TooltipHandler.TipRegion(entryRect, "RimCord_PauseDisplayDelaySecondsDesc".Translate().ToString());
            if (enteredValue != pauseDisplayDelayBuffer)
            {
                pauseDisplayDelayBuffer = enteredValue.Trim();
                if (int.TryParse(pauseDisplayDelayBuffer, out int parsedDelay))
                {
                    PauseDisplayDelaySeconds = ClampPauseDisplayDelay(parsedDelay);
                }
            }

            if (!int.TryParse(pauseDisplayDelayBuffer, out int displayedDelay) || displayedDelay < 0 || displayedDelay > 3600)
            {
                listing.Gap(3f);
                UnityEngine.GUI.color = new UnityEngine.Color(1f, 0.75f, 0.1f);
                listing.Label("RimCord_PauseDisplayDelayRangeWarning".Translate());
                UnityEngine.GUI.color = UnityEngine.Color.white;
            }
        }

        private static void DrawIndentedCheckbox(Listing_Standard listing, string label, ref bool value, string tooltip, bool enabled)
        {
            var row = listing.GetRect(Text.LineHeight);
            row.xMin += 22f;
            var previousColor = UnityEngine.GUI.color;
            if (!enabled)
            {
                UnityEngine.GUI.color = new UnityEngine.Color(1f, 1f, 1f, 0.4f);
            }

            bool displayedValue = value;
            Widgets.CheckboxLabeled(row, label, ref displayedValue);
            TooltipHandler.TipRegion(row, tooltip);
            UnityEngine.GUI.color = previousColor;

            if (enabled)
            {
                value = displayedValue;
            }
        }

        private static void DrawFullWidthTextField(Listing_Standard listing, string label, ref string value, int maxLength)
        {
            listing.Label(label);
            listing.Gap(2f);

            var fieldRect = listing.GetRect(Text.LineHeight + 8f);
            fieldRect.height = Text.LineHeight + 6f;
            value = Widgets.TextField(fieldRect, value ?? string.Empty);
            value = TrimAndLimit(value, maxLength);
        }

        public new void Write()
        {
            SanitizeCustomFields();
            ClampPauseDisplayDelay();

            if (!ShowLetterEvents)
            {
                PresenceEventQueue.ClearCurrentEvent();
                RaidTracker.Reset();
                MentalBreakTracker.Reset();
            }
            else if (!ShowThreatAlerts)
            {
                PresenceEventQueue.ClearThreatEvent();
                RaidTracker.Reset();
                MentalBreakTracker.Reset();
            }

            if (RimCordMod.PresenceManager != null)
            {
                if (!EnableRichPresence)
                {
                    PresenceEventQueue.ClearCurrentEvent();
                    RaidTracker.Reset();
                    MentalBreakTracker.Reset();
                    RimCordMod.PresenceManager.Shutdown();
                }
                else
                {
                    RimCordMod.PresenceManager.Initialize();
                    RimCordMod.PresenceManager.Update(force: true);
                }
            }
        }

        private static string TrimAndLimit(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private void SanitizeCustomFields()
        {
            CustomButtonLabel = SanitizeDisplayText(CustomButtonLabel, 32);
            CustomButtonUrl = SanitizeUrl(CustomButtonUrl);
        }

        private void ClampPauseDisplayDelay()
        {
            PauseDisplayDelaySeconds = ClampPauseDisplayDelay(PauseDisplayDelaySeconds);
            pauseDisplayDelayBuffer = PauseDisplayDelaySeconds.ToString();
        }

        private static int ClampPauseDisplayDelay(int delaySeconds)
        {
            if (delaySeconds < 0)
            {
                return 0;
            }

            if (delaySeconds > 3600)
            {
                return 3600;
            }

            return delaySeconds;
        }

        private static string SanitizeDisplayText(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sanitized = new System.Text.StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsControl(c) && c != ' ')
                {
                    continue;
                }
                sanitized.Append(c);
            }

            string result = sanitized.ToString().Trim();
            return TrimAndLimit(result, maxLength);
        }

        private static string SanitizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            url = url.Trim();

            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri))
            {
                return string.Empty;
            }

            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (url.Length > 256)
            {
                return string.Empty;
            }

            return url;
        }

        internal static bool IsValidButtonUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return !string.IsNullOrEmpty(SanitizeUrl(url));
        }
    }
}
