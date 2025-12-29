using HarmonyLib;
using RimCord.GameState;
using RimWorld;
using Verse;

namespace RimCord.HarmonyPatches
{
    [HarmonyPatch(typeof(IncidentWorker_Raid))]
    [HarmonyPatch("TryExecuteWorker", typeof(IncidentParms))]
    public static class RaidIncidentPatch
    {
        public static void Postfix(bool __result, IncidentParms parms)
        {
            if (!__result || parms == null)
            {
                return;
            }

            if (!(parms.target is Map))
            {
                return;
            }

            try
            {
                RaidTracker.NotifyRaidTriggered(parms);
            }
            catch (System.Exception ex)
            {
                RimCordLogger.Warning("Failed to track raid incident: {0}", ex.Message);
            }
        }
    }
}
