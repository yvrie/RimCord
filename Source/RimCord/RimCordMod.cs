using HarmonyLib;
using RimCord.HarmonyPatches;
using System;
using System.Reflection;
using Verse;

namespace RimCord
{
    [StaticConstructorOnStartup]
    public class RimCordMod : Mod
    {
        public static RimCordMod Instance { get; private set; }
        public static RimCordSettings Settings { get; private set; }
        public static PresenceManager PresenceManager { get; set; }
        public static bool IsInitialized { get; private set; }

        private static readonly object harmonyLock = new object();
        private static bool harmonyInitialized;
        private static Harmony harmonyInstance;
        private const string HarmonyId = "com.l0venote.rimcord";

        static RimCordMod()
        {
            try
            {
                InitializeHarmony();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimCord] Critical error during static initialization: {ex}");
            }
        }

        public RimCordMod(ModContentPack content) : base(content)
        {
            Instance = this;
            
            try
            {
                Settings = GetSettings<RimCordSettings>();
            }
            catch (Exception ex)
            {
                RimCordLogger.Error("Failed to load settings: {0}", ex.Message);
                Settings = new RimCordSettings();
            }

            try
            {
                PresenceManager = new PresenceManager();
                PresenceManager.Initialize();
                MenuPresenceUpdater.Initialize();
            }
            catch (Exception ex)
            {
                RimCordLogger.Error("Failed to initialize PresenceManager: {0}", ex.Message);
            }

            RimCordLogger.Info("Initialized successfully.");
            IsInitialized = true;
        }

        private static void InitializeHarmony()
        {
            lock (harmonyLock)
            {
                if (harmonyInitialized)
                    return;

                try
                {
                    harmonyInstance = new Harmony(HarmonyId);
                    harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                    harmonyInitialized = true;
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimCord] Failed to apply Harmony patches: {ex}");
                    throw;
                }
            }
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect) => Settings.DoWindowContents(inRect);

        public override string SettingsCategory() => "RimCord";

        public override void WriteSettings()
        {
            base.WriteSettings();
            Settings.Write();
        }
    }
}
