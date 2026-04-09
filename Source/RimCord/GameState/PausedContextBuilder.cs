using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimCord.GameState
{
    public static class PausedContextBuilder
    {
        public static string GetPausedDetails(RimCordSettings settings)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                    return GetFallbackText("RimCord_Paused_Planning");

                bool showThreatAlerts = settings == null || settings.ShowThreatAlerts;
                bool showGameConditions = settings == null || settings.ShowGameConditions;

                if (showThreatAlerts)
                {
                    string raidText = GetRaidPausedText(map);
                    if (!string.IsNullOrEmpty(raidText))
                        return raidText;


                    string mentalBreakText = GetMentalBreakPausedText(map);
                    if (!string.IsNullOrEmpty(mentalBreakText))
                        return mentalBreakText;
                }

                if (showGameConditions)
                {
                    string conditionText = GetGameConditionPausedText(map);
                    if (!string.IsNullOrEmpty(conditionText))
                        return conditionText;
                }


                string traderText = GetTraderPausedText(map);
                if (!string.IsNullOrEmpty(traderText))
                    return traderText;


                string weatherText = GetWeatherTimePausedText(map);
                if (!string.IsNullOrEmpty(weatherText))
                    return weatherText;

                return GetFallbackText("RimCord_Paused_Planning");
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Error in GetPausedDetails: {0}", ex.Message);
                return GetFallbackText("RimCord_Paused_Planning");
            }
        }

        private static string GetRaidPausedText(Map map)
        {
            try
            {
                if (!RaidTracker.IsRaidActive())
                    return null;

                var lords = map.lordManager?.lords;
                if (lords == null)
                    return null;

                foreach (var lord in lords)
                {
                    if (lord?.faction == null || !lord.faction.HostileTo(Faction.OfPlayer))
                        continue;

                    var job = lord.LordJob;
                    if (job == null)
                        continue;

                    string jobTypeName = job.GetType().Name.ToLower();


                    if (jobTypeName.Contains("assault") || jobTypeName.Contains("attackcolony"))
                    {

                        if (lord.ownedPawns.Any(p => p.Position.Roofed(map) && map.roofGrid.RoofAt(p.Position)?.isNatural == false))
                            return SafeTranslate("RimCord_Paused_Raid_Interior");
                        return SafeTranslate("RimCord_Paused_Raid_Assault");
                    }
                    if (jobTypeName.Contains("sapper") || jobTypeName.Contains("breach"))
                        return SafeTranslate("RimCord_Paused_Raid_Sappers");
                    if (jobTypeName.Contains("siege"))
                        return SafeTranslate("RimCord_Paused_Raid_Siege");
                    if (jobTypeName.Contains("mechanoid"))
                        return SafeTranslate("RimCord_Paused_Raid_Mechanoid");
                }


                string factionName = RaidTracker.GetRaidFactionName();
                if (!string.IsNullOrEmpty(factionName))
                    return string.Format(SafeTranslate("RimCord_Paused_Raid_Generic"), factionName);

                return SafeTranslate("RimCord_Paused_Raid_Default");
            }
            catch
            {
                return null;
            }
        }

        private static string GetMentalBreakPausedText(Map map)
        {
            try
            {
                var mentalInfo = MentalBreakTracker.GetActiveMentalBreakInfo();
                if (mentalInfo == null)
                    return null;

                string pawnName = mentalInfo.PawnLabel ?? "Colonist";
                string mentalState = mentalInfo.MentalStateLabel ?? "having a breakdown";

                return string.Format(SafeTranslate("RimCord_Paused_MentalBreak"), pawnName, mentalState);
            }
            catch
            {
                return null;
            }
        }

        private static string GetGameConditionPausedText(Map map)
        {
            try
            {
                var conditions = map.gameConditionManager?.ActiveConditions;
                if (conditions == null || conditions.Count == 0)
                    return null;

                foreach (var condition in conditions)
                {
                    if (condition?.def == null)
                        continue;

                    string defName = condition.def.defName;


                    if (defName == "SolarFlare")
                        return SafeTranslate("RimCord_Paused_SolarFlare");
                    if (defName == "ToxicFallout")
                        return SafeTranslate("RimCord_Paused_ToxicFallout");
                    if (defName == "Eclipse")
                        return SafeTranslate("RimCord_Paused_Eclipse");
                    if (defName == "Aurora")
                        return SafeTranslate("RimCord_Paused_Aurora");
                    if (defName == "HeatWave")
                        return SafeTranslate("RimCord_Paused_HeatWave");
                    if (defName == "ColdSnap")
                        return SafeTranslate("RimCord_Paused_ColdSnap");
                    if (defName == "VolcanicWinter")
                        return SafeTranslate("RimCord_Paused_VolcanicWinter");
                    if (defName == "ToxicSpewer" || defName == "ToxicFallout")
                        return SafeTranslate("RimCord_Paused_ToxicFallout");
                    if (defName == "Flashstorm")
                        return SafeTranslate("RimCord_Paused_Flashstorm");


                    string label = condition.LabelCap;
                    if (!string.IsNullOrEmpty(label))
                        return string.Format("Paused: {0}", label);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetTraderPausedText(Map map)
        {
            try
            {

                var passingShips = map.passingShipManager?.passingShips;
                if (passingShips != null)
                {
                    foreach (var ship in passingShips)
                    {
                        if (ship is TradeShip tradeShip && tradeShip.CanTradeNow)
                            return SafeTranslate("RimCord_Paused_OrbitalTrader");
                    }
                }


                var mapPawns = map.mapPawns?.AllPawnsSpawned;
                if (mapPawns != null)
                {
                    foreach (var pawn in mapPawns)
                    {
                        if (pawn?.trader != null && !pawn.Dead && !pawn.Downed && pawn.Faction != Faction.OfPlayer)
                        {
                            return SafeTranslate("RimCord_Paused_CaravanTrader");
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetWeatherTimePausedText(Map map)
        {
            try
            {
                int hour = GenLocalDate.HourOfDay(map);
                var weather = map.weatherManager?.curWeather;


                if (hour >= 22 || hour < 5)
                    return SafeTranslate("RimCord_Paused_NightShift");


                if (weather != null)
                {
                    string defName = weather.defName?.ToLower() ?? "";
                    string label = weather.label?.ToLower() ?? "";

                    if (defName.Contains("snow") || label.Contains("snow") || defName.Contains("blizzard"))
                        return SafeTranslate("RimCord_Paused_Snow");

                    if (defName.Contains("rain") || label.Contains("rain") || defName.Contains("storm") || defName.Contains("thunderstorm"))
                        return SafeTranslate("RimCord_Paused_Rain");

                    if (defName.Contains("fog"))
                        return SafeTranslate("RimCord_Paused_Fog");
                }


                return SafeTranslate("RimCord_Paused_Peaceful");
            }
            catch
            {
                return SafeTranslate("RimCord_Paused_Planning");
            }
        }

        private static readonly Dictionary<string, string> Fallbacks = new Dictionary<string, string>
        {
            { "RimCord_Paused_Planning", "Currently planning" },
            { "RimCord_Paused_Raid_Interior", "Paused: They dropped inside!" },
            { "RimCord_Paused_Raid_Assault", "Paused: Planning counter-attack" },
            { "RimCord_Paused_Raid_Sappers", "Paused: They're ignoring the killbox" },
            { "RimCord_Paused_Raid_Siege", "Paused: They brought mortars" },
            { "RimCord_Paused_Raid_Mechanoid", "Paused: Mechanoid threat" },
            { "RimCord_Paused_Raid_Generic", "Paused: {0} is attacking" },
            { "RimCord_Paused_Raid_Default", "Paused: Under attack" },
            { "RimCord_Paused_MentalBreak", "Paused: {0} is {1}" },
            { "RimCord_Paused_SolarFlare", "Paused: Powerless (Solar Flare)" },
            { "RimCord_Paused_ToxicFallout", "Paused: Lockdown (Toxic Fallout)" },
            { "RimCord_Paused_Eclipse", "Paused: In the dark (Eclipse)" },
            { "RimCord_Paused_Aurora", "Paused: Admiring the Aurora" },
            { "RimCord_Paused_HeatWave", "Paused: Cooling off (Heat Wave)" },
            { "RimCord_Paused_ColdSnap", "Paused: Staying warm (Cold Snap)" },
            { "RimCord_Paused_VolcanicWinter", "Paused: Volcanic Winter" },
            { "RimCord_Paused_Flashstorm", "Paused: Flashstorm incoming" },
            { "RimCord_Paused_OrbitalTrader", "Paused: Haggling with orbital traders" },
            { "RimCord_Paused_CaravanTrader", "Paused: Analyzing trade deals" },
            { "RimCord_Paused_NightShift", "Paused: The night shift" },
            { "RimCord_Paused_Rain", "Paused: Stormy weather" },
            { "RimCord_Paused_Snow", "Paused: Frozen wasteland" },
            { "RimCord_Paused_Fog", "Paused: Low visibility" },
            { "RimCord_Paused_Peaceful", "Paused: Peaceful production" }
        };

        private static string SafeTranslate(string key)
        {
            try
            {
                if (LanguageDatabase.activeLanguage == null)
                    return Fallbacks.TryGetValue(key, out var fb) ? fb : key;

                return key.Translate();
            }
            catch
            {
                return Fallbacks.TryGetValue(key, out var fb) ? fb : key;
            }
        }

        private static string GetFallbackText(string key)
        {
            return Fallbacks.TryGetValue(key, out var fb) ? fb : "Currently planning";
        }
    }
}
