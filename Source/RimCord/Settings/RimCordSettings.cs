using System;
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

        public bool EnableCustomButton = false;
        public string CustomButtonLabel = "Watch live";
        public string CustomButtonUrl = string.Empty;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableRichPresence, "EnableRichPresence", true);
            Scribe_Values.Look(ref ShowColonyName, "ShowColonyName", true);
            Scribe_Values.Look(ref ShowColonistCount, "ShowColonistCount", true);

            Scribe_Values.Look(ref ShowBiome, "ShowBiome", false);
            Scribe_Values.Look(ref ShowStorytellerIcon, "ShowStorytellerIcon", true);

            Scribe_Values.Look(ref EnableCustomButton, "EnableCustomButton", false);
            Scribe_Values.Look(ref CustomButtonLabel, "CustomButtonLabel", "Watch live");
            Scribe_Values.Look(ref CustomButtonUrl, "CustomButtonUrl", string.Empty);
        }

        private UnityEngine.Vector2 settingsScrollPosition = UnityEngine.Vector2.zero;
        private float settingsViewHeight = 600f;

        public void DoWindowContents(UnityEngine.Rect inRect)
        {
            float viewWidth = inRect.width - 16f;
            if (viewWidth < 0f)
            {
                viewWidth = inRect.width;
            }

            var viewRect = new UnityEngine.Rect(0f, 0f, viewWidth, settingsViewHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("RimCord_Settings_Header".Translate(), -1f, "RimCord_Settings_HeaderDesc".Translate());

            listing.Gap(12f);

            listing.CheckboxLabeled("RimCord_EnableRichPresence".Translate(), ref EnableRichPresence, "RimCord_EnableRichPresenceDesc".Translate());
            
            listing.Gap(6f);

            if (EnableRichPresence)
            {
                listing.Gap(6f);
                listing.CheckboxLabeled("RimCord_ShowColonyName".Translate(), ref ShowColonyName, "RimCord_ShowColonyNameDesc".Translate());
                
                listing.Gap(6f);
                listing.CheckboxLabeled("RimCord_ShowColonistCount".Translate(), ref ShowColonistCount, "RimCord_ShowColonistCountDesc".Translate());
                

                
                listing.Gap(6f);
                listing.CheckboxLabeled("RimCord_ShowBiome".Translate(), ref ShowBiome, "RimCord_ShowBiomeDesc".Translate());
                
                listing.Gap(6f);
                listing.CheckboxLabeled("RimCord_ShowStorytellerIcon".Translate(), ref ShowStorytellerIcon, "RimCord_ShowStorytellerIconDesc".Translate());
                


                listing.CheckboxLabeled("RimCord_EnableCustomButton".Translate(), ref EnableCustomButton, "RimCord_EnableCustomButtonDesc".Translate());
                if (EnableCustomButton)
                {
                    listing.Gap(3f);
                    // Discord hides buttons from the user who sets them, only others can see it
                    UnityEngine.GUI.color = UnityEngine.Color.gray;
                    listing.Label("RimCord_ButtonNote".Translate());
                    UnityEngine.GUI.color = UnityEngine.Color.white;
                    listing.Gap(3f);
                    listing.Label("RimCord_CustomButtonLabel".Translate());
                    CustomButtonLabel = listing.TextEntry(CustomButtonLabel ?? string.Empty);
                    CustomButtonLabel = TrimAndLimit(CustomButtonLabel, 32);
                    listing.Label("RimCord_CustomButtonUrl".Translate());
                    CustomButtonUrl = listing.TextEntry(CustomButtonUrl ?? string.Empty);
                    CustomButtonUrl = TrimAndLimit(CustomButtonUrl, 256);
                    
                    // warn user if URL doesnt meet Discord requirements
                    if (!string.IsNullOrWhiteSpace(CustomButtonUrl) && !IsValidButtonUrl(CustomButtonUrl))
                    {
                        UnityEngine.GUI.color = UnityEngine.Color.yellow;
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

        public new void Write()
        {
            SanitizeCustomFields();

            if (RimCordMod.PresenceManager != null)
            {
                if (!EnableRichPresence)
                {
                    RimCordMod.PresenceManager.Shutdown();
                }
                else
                {
                    RimCordMod.PresenceManager.Initialize();
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

            // strip out any weird control chars that could break things
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

        // Discord only accepts HTTPS urls for button links
        private static string SanitizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            url = url.Trim();

            // Discord requires HTTPS
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // make sure its actually a valid URL
            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri))
            {
                return string.Empty;
            }

            // double check scheme after parsing (some edge cases slip through)
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // Discord limit is 512 but we cap at 256 to be safe
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
