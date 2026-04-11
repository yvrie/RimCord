using System;
using RimCord.GameState;
using Verse;

namespace RimCord
{
    public class RimCordSettings : ModSettings
    {
        public bool EnableRichPresence = true;
        public bool ShowColonyName = true;
        public bool ShowColonistCount = true;

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
            Scribe_Values.Look(ref ShowColonyName, "ShowColonyName", true);
            Scribe_Values.Look(ref ShowColonistCount, "ShowColonistCount", true);

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
                DrawSectionHeader(listing, "RimCord_Settings_Section_Status".Translate());

                listing.CheckboxLabeled("RimCord_ShowColonyName".Translate(), ref ShowColonyName, "RimCord_ShowColonyNameDesc".Translate());
                listing.Gap(4f);
                listing.CheckboxLabeled("RimCord_ShowColonistCount".Translate(), ref ShowColonistCount, "RimCord_ShowColonistCountDesc".Translate());
                listing.Gap(4f);
                listing.CheckboxLabeled("RimCord_ShowBiome".Translate(), ref ShowBiome, "RimCord_ShowBiomeDesc".Translate());
                listing.Gap(4f);
                listing.CheckboxLabeled("RimCord_ShowStorytellerIcon".Translate(), ref ShowStorytellerIcon, "RimCord_ShowStorytellerIconDesc".Translate());

                DrawSectionHeader(listing, "RimCord_Settings_Section_Events".Translate());

                listing.CheckboxLabeled("RimCord_ShowLetterEvents".Translate(), ref ShowLetterEvents, "RimCord_ShowLetterEventsDesc".Translate());
                listing.Gap(3f);

                var threatRow = listing.GetRect(Text.LineHeight);
                threatRow.xMin += 22f;
                if (!ShowLetterEvents)
                    UnityEngine.GUI.color = new UnityEngine.Color(1f, 1f, 1f, 0.4f);
                Widgets.CheckboxLabeled(threatRow, "RimCord_ShowThreatAlerts".Translate(), ref ShowThreatAlerts);
                TooltipHandler.TipRegion(threatRow, "RimCord_ShowThreatAlertsDesc".Translate().ToString());
                UnityEngine.GUI.color = UnityEngine.Color.white;

                listing.Gap(4f);
                listing.CheckboxLabeled("RimCord_ShowGameConditions".Translate(), ref ShowGameConditions, "RimCord_ShowGameConditionsDesc".Translate());

                DrawSectionHeader(listing, "RimCord_Settings_Section_Button".Translate());

                listing.CheckboxLabeled("RimCord_EnableCustomButton".Translate(), ref EnableCustomButton, "RimCord_EnableCustomButtonDesc".Translate());

                if (EnableCustomButton)
                {
                    listing.Gap(4f);
                    UnityEngine.GUI.color = new UnityEngine.Color(0.8f, 0.8f, 0.8f);
                    listing.Label("RimCord_ButtonNote".Translate());
                    UnityEngine.GUI.color = UnityEngine.Color.white;

                    listing.Gap(8f);
                    listing.Label("RimCord_CustomButtonLabel".Translate());
                    listing.Gap(2f);
                    CustomButtonLabel = listing.TextEntry(CustomButtonLabel ?? string.Empty);
                    CustomButtonLabel = TrimAndLimit(CustomButtonLabel, 32);

                    listing.Gap(6f);
                    listing.Label("RimCord_CustomButtonUrl".Translate());
                    listing.Gap(2f);
                    CustomButtonUrl = listing.TextEntry(CustomButtonUrl ?? string.Empty);
                    CustomButtonUrl = TrimAndLimit(CustomButtonUrl, 256);

                    if (!string.IsNullOrWhiteSpace(CustomButtonUrl) && !IsValidButtonUrl(CustomButtonUrl))
                    {
                        listing.Gap(3f);
                        UnityEngine.GUI.color = new UnityEngine.Color(1f, 0.75f, 0.1f);
                        listing.Label("RimCord_UrlWarning".Translate());
                        UnityEngine.GUI.color = UnityEngine.Color.white;
                    }
                    listing.Gap(6f);
                }
            }

            settingsViewHeight = Math.Max(listing.CurHeight + 12f, inRect.height);
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

        public new void Write()
        {
            SanitizeCustomFields();

            if (!ShowLetterEvents)
            {
                PresenceEventQueue.ClearCurrentEvent();
                RimCordMod.PresenceManager?.ClearRecentLetterEvent();
            }
            else if (!ShowThreatAlerts)
            {
                PresenceEventQueue.ClearThreatEvent();
            }

            if (RimCordMod.PresenceManager != null)
            {
                if (!EnableRichPresence)
                {
                    RimCordMod.PresenceManager.Shutdown();
                }
                else
                {
                    RimCordMod.PresenceManager.Initialize();
                    RimCordMod.PresenceManager.Update();
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
