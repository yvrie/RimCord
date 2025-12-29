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

            RimCordLogger.Info("<color=#FF6B6B>M</color><color=#FFA94D>o</color><color=#FFE066>d</color> <color=#69DB7C>i</color><color=#38D9A9>n</color><color=#4DABF7>i</color><color=#748FFC>t</color><color=#DA77F2>i</color><color=#F783AC>a</color><color=#FF6B6B>l</color><color=#FFA94D>i</color><color=#FFE066>z</color><color=#69DB7C>e</color><color=#38D9A9>d</color> <color=#4DABF7>s</color><color=#748FFC>u</color><color=#DA77F2>c</color><color=#F783AC>c</color><color=#FF6B6B>e</color><color=#FFA94D>s</color><color=#FFE066>s</color><color=#69DB7C>f</color><color=#38D9A9>u</color><color=#4DABF7>l</color><color=#748FFC>l</color><color=#DA77F2>y</color><color=#F783AC>!</color>");
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
