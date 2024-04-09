using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using TAC_AI.AI.Movement;
using SafeSaves;
using TerraTechETCUtil;

#if !STEAM
using ModHelper.Config;
#else
using ModHelper;
#endif
using Nuterra.NativeOptions;


namespace TAC_AI
{
    // Previously an extension to RandomAdditions, TACtical AI is the AI branch of the mod.
    //
    public class KickStart
    {
        internal const string ModID = "Advanced AI";
        internal const string ModCommandID = "TAC_AI";


        public static FactionSubTypes factionAttractOST = FactionSubTypes.NULL;

#if STEAM
        public static bool ShouldBeActive = false;//
        public static bool UseClassicRTSControls = true;//
#else
        public static bool ShouldBeActive = true;//
        public static bool UseClassicRTSControls = false;//
#endif
        public static bool UseNumpadForGrouping = false;//
        internal static KeyCode RetreatHotkey = KeyCode.I;// The key to press to retreat!
        public static int RetreatHotkeySav = (int)RetreatHotkey;//
        internal static KeyCode CommandHotkey = KeyCode.K;// The key to press to toggle RTS
        public static int CommandHotkeySav = (int)CommandHotkey;//
        internal static KeyCode CommandBoltsHotkey = KeyCode.X;// The key to press to toggle RTS
        public static int CommandBoltsHotkeySav = (int)CommandBoltsHotkey;//
        internal static KeyCode MultiSelect = KeyCode.LeftShift;// The key to hold to select multiple
        public static int MultiSelectKeySav = (int)MultiSelect;//
        internal static KeyCode ModeSelect = KeyCode.J;// The key to hold to select multiple
        public static int ModeSelectKeySav = (int)ModeSelect;//
        //internal static bool testEnemyAI = true; // OBSOLETE

        public static float TerrainHeight = ManWorld.inst.TileSize;

        internal static int EnemyTeamTechLimit = 6;// Allow the bases plus 6 additional capacity of the AIs' choosing

        public static float SavedDefaultEnemyFragility;

        public static int MaxEnemyWorldCapacity
        {
            get
            {
                return AIPopMaxLimit;
                /*
                 // Abandoned due to too many issues, instead I have raised the max pop limit
                if ((1 / Time.deltaTime) <= 20)
                {   // game lagging too much - hold back
                    return AIPopMaxLimit + MaxEnemyBaseLimit;
                }
                return AIPopMaxLimit + (MaxBasesPerTeam * MaxEnemyBaseLimit) + 1;*/
            }
        }// How many techs that can exist before giving up tech splitting?
        internal static int MaxEnemyBaseLimit = 3;  // How many different enemy team bases are allowed to exist in one instance
        internal static int MaxEnemyHQLimit = 1;    // How many HQs are allowed to exist in one instance
        /// <summary>
        /// Maker bases (excludes defenses)
        /// </summary>
        internal static int MaxBasesPerTeam = 6;    // How many base expansions can a single team perform?

        /// <summary>
        /// For handling Operations
        /// </summary>
        internal static short AIClockPeriod // How frequently we update
        {
            get
            {
                return ManNetwork.IsNetworked ? AIGlobals.NetAIClockPeriod : AIClockPeriodSet;
            }
        }
        public static short AIClockPeriodSet = 10;        // How frequently we update

#if STEAM
        public static bool EnableBetterAI = false;  // This is toggled based on if the mod is "enabled" by official
#else
        public static bool EnableBetterAI = true;
#endif
        internal static bool AutopilotPlayer
        {
            get
            {
                if (ManPlayerRTS.PlayerIsInRTS)
                    return AllowStrategicAI && AutopilotPlayerRTS;
                return AutopilotPlayerMain;
            }
        }
        internal static bool AutopilotPlayerMain = false;
        internal static bool AutopilotPlayerRTS = false;

        /// <summary>
        /// For handing Directors
        /// </summary>
        public static int AIDodgeCheapness = 20;
        public static int AIPopMaxLimit = 8;
        public static bool MuteNonPlayerRacket = true;
        public static bool DisplayEnemyEvents = true;
        public static bool AllowOverleveledBlockDrops { get { return EnemyBlockDropChance == 100; } } // Obsolete - true when 
        public static bool enablePainMode = true;
        public static bool EnemyEradicators = false;    // Insanely large or powerful Techs
        public static bool EnemiesHaveCreativeInventory = false;

        public static bool AISelfRepair => ManNetwork.IsNetworked ? AllowAISelfRepairInMP : AllowAISelfRepair;
        public static bool AllowAISelfRepair = true;
        public static bool AllowAISelfRepairInMP = false;

        public static int ForceRemoveOverEnemyMaxCap = 4;
        internal static bool AllowEnemiesToStartBases { get { return MaxEnemyBaseLimit != 0; } }
        internal static bool AllowEnemyBaseExpand { get { return MaxBasesPerTeam != 0; } }
        public static int LandEnemyOverrideChance {
            get { return LandEnemyOverrideChanceSav; }
            internal set { LandEnemyOverrideChanceSav = value; }
        }
#if STEAM
        public static int LandEnemyOverrideChanceSav = 20;
#else
        public static int LandEnemyOverrideChanceSav = 10;
#endif
        public static bool AllowAirEnemiesToSpawn = true;
        public static float AirEnemiesSpawnRate = 1;
        public static bool AllowSeaEnemiesToSpawn = true;
        public static bool TryForceOnlyPlayerSpawns = false;
        public static bool AllowEnemiesToMine = true;
        public static bool DesignsToLog = false;
        public static bool CommitDeathMode = false;
        public static bool CatMode = false;

        public static bool AllowStrategicAI = true;
        public static bool CullFarEnemyBases = true;
        public static float EnemySellGainModifier = 1; // multiply enemy sell gains by this value

        //public static bool DestroyTreesInWater = false;

        // Set on startup

        // MOD SUPPORT
        internal static bool IsRandomAdditionsPresent = false;
        internal static bool isWaterModPresent = false;
        internal static bool isControlBlocksPresent = false;
        internal static bool isTweakTechPresent = false;
        //internal static bool isTougherEnemiesPresent = false; // OBSOLETE
        internal static bool isWeaponAimModPresent = false;
        internal static bool isBlockInjectorPresent = false;
        internal static bool isPopInjectorPresent = false;
        internal static bool isAnimeAIPresent = false;

        internal static bool isConfigHelperPresent = false;
        internal static bool isNativeOptionsPresent = false;


        // Set ingame
        public static int Difficulty
        {
            get
            {
                if (CommitDeathMode)
                    return 9001;
                else
                    return difficulty;
            }
        }

#if STEAM
        public static int difficulty = 85; // insure that only smart enemies spawn for STEAM release for now
#else
        public static int difficulty = 50;
#endif
        // 150 means only the smartest spawn
        // 50 means the full AI range is used
        // -50 means only the simpleton AI spawns


        public static int EnemyBlockDropChance = 40;

        public static bool WarnOnEnemyLock = true;

        //Calculated
        public static int LastRawTechCount = 0;
        public static int LowerDifficulty { get { return Mathf.Clamp(Difficulty - 50, 0, 99); } }
        public static int UpperDifficulty { get { return Mathf.Clamp(Difficulty + 50, 1, 100); } }
        public static int BaseDifficulty
        {
            get
            {
                if (CommitDeathMode)
                    return 9;
                else
                {
                    return 6 + (int)(Difficulty / 50);
                }
            }
        }

        public static int lastPlayerTechPrice = 0;
        public static int EnemySpawnPriceMatching
        {
            get
            {
                if (CommitDeathMode)
                    return int.MaxValue; // allow EVERYTHING
                if (Singleton.playerTank.IsNotNull())
                {
                    lastPlayerTechPrice = RawTechTemplate.GetBBCost(Singleton.playerTank);
                }
                int priceMax = (int)((((float)(Difficulty + 50) / 100) + 0.5f) * lastPlayerTechPrice);
                // Easiest results in 50% max player cost spawns, Hardest results in 250% max player cost spawns, Regular is is 150% max player cost spawns.
                return Mathf.Max(lastPlayerTechPrice / 2, priceMax);
            }
        }
        public static bool AllowSniperSpawns { get { return LowerDifficulty >= 50; } }

        public static bool CanUseMenu { get { return !ManPauseGame.inst.IsPaused; } }

        public static bool IsIngame { get { return !ManPauseGame.inst.IsPaused && !ManPointer.inst.IsInteractionBlocked; } }

        public static void ReleaseControl(string Name = null)
        {
            string focused = GUI.GetNameOfFocusedControl();
            if (Name == null)
            {
                DebugTAC_AI.Info(KickStart.ModID + ": GUI - Releasing control of " + (focused.NullOrEmpty() ? "unnamed" : focused));
                GUI.FocusControl(null);
                GUI.UnfocusWindow();
                GUIUtility.hotControl = 0;
            }
            else
            {
                if (focused == Name)
                {
                    DebugTAC_AI.Info(KickStart.ModID + ": GUI - Releasing control of " + (focused.NullOrEmpty() ? "unnamed" : focused));
                    GUI.FocusControl(null);
                    GUI.UnfocusWindow();
                    GUIUtility.hotControl = 0;
                }
            }
        }



        internal static bool firedAfterBlockInjector = false;
        public static bool SpecialAttract = false;
        internal static AttractType SpecialAttractNum = 0;
        public static int retryForBote = 0;
        public static Vector3 SpecialAttractPos;

        public static float WaterHeight
        {
            get
            {
                float outValue = -9001;
                try
                {
                    if (isWaterModPresent)
                        outValue = GetWaterHeightCanCrash();
                }
                catch (Exception)
                {
                    //outValue = -100;
                }
                return outValue;
            }
        }
        public static float GetWaterHeightCanCrash()
        {
            return WaterMod.QPatch.WaterHeight;
        }
        public static Harmony harmonyInstance = new Harmony("legionite.tactical_ai");
        private static bool hasPatched = false;
        internal static bool HasHookedUpToSafeSaves = false;

        internal static bool isSteamManaged = false;

        public static bool VALIDATE_MODS()
        {
            isSteamManaged = LookForMod("NLogManager");

            if (!LookForMod("0Harmony"))
            {
                DebugTAC_AI.ErrorReport("This mod NEEDS Harmony to function!  Please subscribe to it on the Steam Workshop");
                return false;
            }

            isConfigHelperPresent = LookForMod("ConfigHelper");
            isNativeOptionsPresent = LookForMod("0Nuterra.NativeOptions");
            if (isConfigHelperPresent && !isNativeOptionsPresent)
            {
                DebugTAC_AI.Warning("ConfigHelper is active but NativeOptions is missing! You need both to use ingame settings.");
            }
            if (!isConfigHelperPresent && isNativeOptionsPresent)
            {
                DebugTAC_AI.Warning("NativeOptions is active but ConfigHelper is missing! You need both to use ingame settings.");
            }
            if (!isSteamManaged && (isConfigHelperPresent || isNativeOptionsPresent))
            {
                DebugTAC_AI.Warning("Ingame settings requires launching from TTSMM (with 0LogManager) to insure success");
            }

            if (LookForMod("RandomAdditions"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found RandomAdditions!  Enabling advanced AI for parts!");
                IsRandomAdditionsPresent = true;
            }
            else IsRandomAdditionsPresent = false;

            if (LookForMod("WaterMod"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }
            else isWaterModPresent = false;

            if (LookForMod("Control Block"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Control Blocks!  Letting RawTech loader override unassigned swivels to auto-target!");
                isControlBlocksPresent = true;
            }
            else isControlBlocksPresent = false;

            if (LookForMod("WeaponAimMod"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found WeaponAimMod!  Halting aim-related changes and letting WeaponAimMod take over!");
                isWeaponAimModPresent = true;
            }
            else isWeaponAimModPresent = false;

            if (LookForMod("TweakTech"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found TweakTech!  Applying changes to AI!");
                isTweakTechPresent = true;
            }
            else isTweakTechPresent = false;
            /*
            if (LookForMod("TougherEnemies"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found Tougher Enemies!  MAKING THE PAIN REAL!");
                isTougherEnemiesPresent = true;
            }*/

            if (LookForMod("BlockInjector"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found Block Injector!  Setting up modded base support!");
                isBlockInjectorPresent = true;
            }
            else isBlockInjectorPresent = false;

            if (LookForMod("PopulationInjector"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found Population Injector!  Holding off on using built-in spawning system!");
                isPopInjectorPresent = true;
            }
            else isPopInjectorPresent = false;

            if (LookForMod("AnimeAI"))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Found Anime AI!  Hooking into commentary system and actions!");
                isAnimeAIPresent = true;
            }
            else isAnimeAIPresent = false;
            return true;
        }

        public static void PatchMod()
        {
            DebugTAC_AI.Log(KickStart.ModID + ": Patch Call");
            if (!hasPatched)
            {
                try
                {
                    if (!MassPatcher.MassPatchAllWithin(harmonyInstance, typeof(GlobalPatches), "TACtical_AI"))
                        DebugTAC_AI.ErrorReport("Error on patching GlobalPatches");
                    if (!MassPatcher.MassPatchAllWithin(harmonyInstance, typeof(ManagerPatches), "TACtical_AI"))
                        DebugTAC_AI.ErrorReport("Error on patching ManagerPatches");
                    if (!MassPatcher.MassPatchAllWithin(harmonyInstance, typeof(UIPatches), "TACtical_AI"))
                        DebugTAC_AI.ErrorReport("Error on patching UIPatches");
                    if (!MassPatcher.MassPatchAllWithin(harmonyInstance, typeof(ModulePatches), "TACtical_AI"))
                        DebugTAC_AI.ErrorReport("Error on patching ModulePatches");

                    harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                    DebugTAC_AI.Log(KickStart.ModID + ": Patched");
                    hasPatched = true;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on patch");
                    DebugTAC_AI.Log(e);
                    DebugTAC_AI.ErrorReport("Error on patching base game");
                }
            }
        }

        public static void HookToSafeSaves()
        {
            try
            {
                Assembly assemble = Assembly.GetExecutingAssembly();
                DebugTAC_AI.Log(KickStart.ModID + ": DLL is " + assemble.GetName());
                ManSafeSaves.RegisterSaveSystem(assemble, OnSaveManagers, OnLoadManagers);
                HasHookedUpToSafeSaves = true;
            }
            catch { 
                DebugTAC_AI.Log(KickStart.ModID + ": Error on RegisterSaveSystem");
                DebugTAC_AI.ErrorReport("Error on hooking to SafeSaves");
            }
        }

        public static void MainOfficialInit()
        {
            //Where the fun begins
#if STEAM
            DebugTAC_AI.Log(KickStart.ModID + ": MAIN (Steam Workshop Version) startup");
            if (!VALIDATE_MODS())
            {
                return;
            }
#else
            DebugTAC_AI.Log(KickStart.ModID + ": Startup was invoked by TTSMM!  Set-up to handle LATE initialization.");
#endif
            //throw new NullReferenceException("CrashHandle");

            //Initiate the madness
            PatchMod();

            HookToSafeSaves();

            ManBaseTeams.Initiate();
            TankAIManager.Initiate();
            AIGlobals.InitSharedMenu();
            GUIAIManager.Initiate();
            RawTechExporter.Initiate();
            RLoadedBases.BaseFunderManager.Initiate();
            ManEnemyWorld.Initiate();
            SpecialAISpawner.Initiate();
            GUINPTInteraction.Initiate();


            AIERepair.RefreshDelays();
            // Because official fails to init this while switching modes
            SpecialAISpawner.DetermineActiveOnModeType();
            TankAIManager.inst.CorrectBlocksList();

            InitSettings();

            if (!isPopInjectorPresent)
                OverrideEnemyMax();
            _ = AIWiki.hintADV;
#if STEAM
            EnableBetterAI = true;
#endif
            if (CustomAttract.Attracts == null)
            {
                CustomAttract.Attracts = CustomAttract.InitAttracts;
            }
            AIWiki.InitWiki();
            ManGameMode.inst.ModeSwitchEvent.Subscribe(OnModeSwitch);
        }
        private static void OnModeSwitch()
        {
            factionAttractOST = FactionSubTypes.NULL;
            var prof = ManProfile.inst.GetCurrentUser();
            if (prof != null)
            {
                ManMusic.inst.SetMusicMixerVolume(prof.m_SoundSettings.m_MusicVolume);
            }
        }

#if STEAM
        public static IEnumerable<float> MainOfficialInitIterate()
        {
            //Where the fun begins
            DebugTAC_AI.Log(KickStart.ModID + ": MAIN [ITERATOR] (Steam Workshop Version) startup");
            if (!VALIDATE_MODS())
            {
                yield return 1f;
            }

            //Initiate the madness
            PatchMod();
            yield return 0.1f;

            HookToSafeSaves();
            yield return 0.16f;

            ManBaseTeams.Initiate();
            TankAIManager.Initiate();
            yield return 0.24f;

            AIGlobals.InitSharedMenu();
            GUIAIManager.Initiate();
            yield return 0.32f;

            RawTechExporter.Initiate();
            yield return 0.40f;

            RLoadedBases.BaseFunderManager.Initiate();
            yield return 0.48f;

            ManEnemyWorld.Initiate();
            yield return 0.56f;

            SpecialAISpawner.Initiate();
            yield return 0.64f;

            GUINPTInteraction.Initiate();
            AIERepair.RefreshDelays();
            yield return 0.72f;

            // Because official fails to init this while switching modes
            SpecialAISpawner.DetermineActiveOnModeType();
            yield return 0.80f;
            TankAIManager.inst.CorrectBlocksList();
            yield return 0.95f;

            InitSettings();
            yield return 1f;

            if (CustomAttract.Attracts == null)
            {
                CustomAttract.Attracts = CustomAttract.InitAttracts;
            }
            ManGameMode.inst.ModeSwitchEvent.Subscribe(OnModeSwitch);
            EnableBetterAI = true;
        }
        private static bool launched = false;
        public static void InitSettings()
        {
            if (launched)
                return;
            launched = true;
            GUINPTInteraction.InsureNetHooks();
            if (isNativeOptionsPresent && isConfigHelperPresent)
            {
                try
                {
                    KickStartConfigHelper.PushExtModConfigHandling();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on Option & Config setup");
                    DebugTAC_AI.Log(e);
                    DebugTAC_AI.ErrorReport("Error on hooks with ConfigHelper/NativeOptions");
                }
            }
            else if (isNativeOptionsPresent)
            {
                try
                {
                    KickStartNativeOptions.PushExtModOptionsHandling();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on NativeOptions setup");
                    DebugTAC_AI.Log(e);
                    DebugTAC_AI.ErrorReport("Error on hooks with NativeOptions");
                }
            }
            else if (isConfigHelperPresent)
            {
                try
                {
                    KickStartConfigHelper.PushExtModConfigHandlingConfigOnly();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on ConfigHelper setup");
                    DebugTAC_AI.Log(e);
                    DebugTAC_AI.ErrorReport("Error on hooks with ConfigHelper");
                }
            }

        }

        public static void DeInitCheck()
        {
            if (TankAIManager.inst)
            {
                TankAIManager.inst.CheckNextFrameNeedsDeInit();
            }
        }


        public static void DeInitALL()
        {
            ManGameMode.inst.ModeSwitchEvent.Unsubscribe(OnModeSwitch);
            float timeStart = Time.realtimeSinceStartup;
            DebugTAC_AI.Log(KickStart.ModID + ": Doing mod DeInit. Garbage Cleanup... current " + GC.GetTotalMemory(false));
            EnableBetterAI = false;
            if (hasPatched)
            {
                try
                {
                    if (CustomAttract.Attracts != null)
                    {
                        foreach (var item in CustomAttract.Attracts)
                        {
                            item.Release();
                        }
                        CustomAttract.Attracts = null;
                    }
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(ModulePatches), "TACtical_AI");
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(UIPatches), "TACtical_AI");
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(ManagerPatches), "TACtical_AI");
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(GlobalPatches), "TACtical_AI");
                    harmonyInstance.UnpatchAll("legionite.tactical_ai");
                    hasPatched = false;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on un-patch");
                    DebugTAC_AI.Log(e);
                }
            }

            // DE-INIT ALL
            ManBaseTeams.DeInit();
            SpecialAISpawner.DeInit();
            ManEnemyWorld.DeInit();
            RLoadedBases.BaseFunderManager.DeInit();
            RawTechExporter.DeInit();
            GUIAIManager.DeInit();
            TankAIManager.DeInit();
            GUINPTInteraction.DeInit();
            AIEPathMapper.DepoolUnusedTiles();
            try
            {
                ManSafeSaves.UnregisterSaveSystem(Assembly.GetExecutingAssembly(), OnSaveManagers, OnLoadManagers);
                HasHookedUpToSafeSaves = false;
            }
            catch { }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            DebugTAC_AI.Log(KickStart.ModID + ": Garbage Cleanup finished with " + GC.GetTotalMemory(false) + " remaining.");
            DebugTAC_AI.Log(KickStart.ModID + ": Operation took " + (Time.realtimeSinceStartup - timeStart) + " seconds.");
        }

#else
        public static void Main()
        {
            //Where the fun begins
            DebugTAC_AI.Log(KickStart.ModID + ": MAIN (TTMM Version) startup");
            if (!VALIDATE_MODS())
                return;
            //Initiate the madness
#if DEBUG
            DebugTAC_AI.Log("-----------------------------------------");
            DebugTAC_AI.Log("-----------------------------------------");
            DebugTAC_AI.Log("        !!! TAC_AI DEBUG MODE !!!");
            DebugTAC_AI.Log("-----------------------------------------");
            DebugTAC_AI.Log("-----------------------------------------");
#endif
            try
            {
                if (isSteamManaged)
                {   // Since TTSMM launches this instead LATE when +managettmm is active, we need to compensate by initing ALL on init in this case.
                    MainOfficialInit();
                    return;
                }
                PatchMod();
                HookToSafeSaves();

                TankAIManager.Initiate();
                GUIAIManager.Initiate();
                RawTechExporter.Initiate();
                RBases.BaseFunderManager.Initiate();
                ManEnemyWorld.Initiate();
                GUIEvictionNotice.Initiate();


                AIERepair.RefreshDelays();
                try
                {
                    KickStartOptions.PushExtModOptionsHandling();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on Option & Config setup");
                    DebugTAC_AI.Log(e);
                }
            }
            catch
            {
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on TTMM Init setup - there are missing dependancies!");
                    DebugTAC_AI.FatalError("Make sure you have installed SafeSaves (and RandomAdditions if it crashes again on start) in TTMM.");
                }
                catch { };
            }
        }

#endif


        public static void DelayedBaseLoader()
        {
            DebugTAC_AI.Log(KickStart.ModID + ": LAUNCHED MODDED BLOCKS BASE VALIDATOR");
            BlockIndexer.ConstructBlockLookupList();
            TempManager.ValidateAllStringTechs();
            DebugRawTechSpawner.Initiate();
            firedAfterBlockInjector = true;
        }


        public static void OnSaveManagers(bool Doing)
        {
            if (Doing)
            {
                ManBaseTeams.OnWorldSave();
            }
            else
            {
                ManBaseTeams.OnWorldFinishSave();
            }
        }
        public static void OnLoadManagers(bool Doing)
        {
            if (Doing)
            {
                ManBaseTeams.OnWorldPreLoad();
            }
            else
            {
                ManBaseTeams.OnWorldLoad();
            }
        }



        internal static FieldInfo limitBreak = typeof(ManPop).GetField("m_PopulationLimit", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void OverrideEnemyMax()
        {
            try
            {
                limitBreak.SetValue(ManPop.inst, AIPopMaxLimit);
            }
            catch { }
        }


        public static void TryHookUpToSafeSavesIfNeeded()
        {
            if (!HasHookedUpToSafeSaves)
            {
                try
                {
                    ManSafeSaves.RegisterSaveSystem(Assembly.GetExecutingAssembly());
                    HasHookedUpToSafeSaves = true;
                }
                catch { DebugTAC_AI.Log(KickStart.ModID + ": Error on RegisterSaveSystem"); }
            }
        }

        public static bool LookForMod(string name)
        {
            if (name == "RandomAdditions")
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.StartsWith(name))
                    {
                        if (assembly.GetType("KickStart") != null)
                            return true;
                    }
                }
            }
            else
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.StartsWith(name))
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Only call for cases where we want only vanilla corps!
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static FactionSubTypes GetCorpExtended(BlockTypes type)
        {
            return (FactionSubTypes)Singleton.Manager<ManSpawn>.inst.GetCorporation(type);
        }

        /*
        public static FactionSubTypes CorpExtToCorp(FactionSubTypes corpExt)
        {
            switch (corpExt)
            {
                case FactionSubTypes.SPE:
                //case FactionSubTypes.GSO:
                case FactionSubTypes.GT:
                case FactionSubTypes.IEC:
                    return FactionSubTypes.GSO;
                //case FactionSubTypes.GC:
                case FactionSubTypes.EFF:
                case FactionSubTypes.LK:
                    return FactionSubTypes.GC;
                //case FactionSubTypes.VEN:
                case FactionSubTypes.OS:
                    return FactionSubTypes.VEN;
                //case FactionSubTypes.HE:
                case FactionSubTypes.BL:
                case FactionSubTypes.TAC:
                    return FactionSubTypes.HE;
                //case FactionSubTypes.BF:
                case FactionSubTypes.DL:
                case FactionSubTypes.EYM:
                case FactionSubTypes.HS:
                    return FactionSubTypes.BF;
                case FactionSubTypes.EXP:
                    return FactionSubTypes.EXP;
            }
            return (FactionSubTypes)corpExt;
        }
        public static FactionSubTypes CorpExtToVanilla(FactionSubTypes corpExt)
        {
            switch (corpExt)
            {
                case FactionSubTypes.SPE:
                //case FactionSubTypes.GSO:
                case FactionSubTypes.GT:
                case FactionSubTypes.IEC:
                    return FactionSubTypes.GSO;
                //case FactionSubTypes.GC:
                case FactionSubTypes.EFF:
                case FactionSubTypes.LK:
                    return FactionSubTypes.GC;
                //case FactionSubTypes.VEN:
                case FactionSubTypes.OS:
                    return FactionSubTypes.VEN;
                //case FactionSubTypes.HE:
                case FactionSubTypes.BL:
                case FactionSubTypes.TAC:
                    return FactionSubTypes.HE;
                //case FactionSubTypes.BF:
                case FactionSubTypes.DL:
                case FactionSubTypes.EYM:
                case FactionSubTypes.HS:
                    return FactionSubTypes.BF;
                case FactionSubTypes.EXP:
                    return FactionSubTypes.EXP;
            }
            return corpExt;
        }*/
        public static bool TransferLegacyIfNeeded(AIType type, out AIType newType, out AIDriverType driver)
        {
            newType = type;
            driver = AIDriverType.Tank;
            if (type >= AIType.Aviator)
            {
                newType = AIType.Escort;
                switch (type)
                {
                    case AIType.Astrotech:
                        driver = AIDriverType.Astronaut;
                        break;
                    case AIType.Aviator:
                        driver = AIDriverType.Pilot;
                        break;
                    case AIType.Buccaneer:
                        driver = AIDriverType.Sailor;
                        break;
                }
                return true;
            }
            return false;
        }
        public static AIType GetLegacy(AIType type, AIDriverType driver)
        {
            if (type == AIType.Escort)
            {
                switch (driver)
                {
                    case AIDriverType.Astronaut:
                        return AIType.Astrotech;
                    case AIDriverType.Pilot:
                        return AIType.Aviator;
                    case AIDriverType.Sailor:
                        return AIType.Buccaneer;
                    default:
                        return AIType.Escort;
                }
            }
            return type;
        }

    }

    public class KickStartConfigHelper
    {
        internal static void PushExtModConfigHandlingConfigOnly()
        {
            KickStart.SavedDefaultEnemyFragility = Globals.inst.moduleDamageParams.detachMeterFillFactor;

            PushExtModConfigSetup();

            OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
        }
        internal static ModConfig PushExtModConfigSetup()
        {
            ModConfig thisModConfig = new ModConfig();
            if (!thisModConfig.ReadConfigJsonFile())
            {
                LoadingHintsExt.MandatoryHints.Add(AltUI.ObjectiveString("Advanced AI") + " adds multiple layers of depth and " +
                AltUI.EnemyString("difficulty") + " to TerraTech.  " +
                AltUI.HintString("Best suited for prospecting veterans looking for a new challenge."));
            }
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "RetreatHotkeySav");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");
            thisModConfig.BindConfig<KickStart>(null, "AIClockPeriodSet");
            thisModConfig.BindConfig<AIEPathMapper>(null, "PathRequestsToCalcPerFrame");
            thisModConfig.BindConfig<KickStart>(null, "MuteNonPlayerRacket");
            thisModConfig.BindConfig<KickStart>(null, "enablePainMode");
            thisModConfig.BindConfig<KickStart>(null, "DisplayEnemyEvents");
            thisModConfig.BindConfig<RawTechExporter>(null, "ExportJSONInsteadOfRAWTECH");
            thisModConfig.BindConfig<KickStart>(null, "difficulty");
            thisModConfig.BindConfig<KickStart>(null, "AllowAISelfRepair");
            thisModConfig.BindConfig<KickStart>(null, "AllowAISelfRepairInMP");
            thisModConfig.BindConfig<KickStart>(null, "LandEnemyOverrideChanceSav");
            thisModConfig.BindConfig<KickStart>(null, "EnemyBlockDropChance");
            thisModConfig.BindConfig<KickStart>(null, "EnemyEradicators");
            thisModConfig.BindConfig<KickStart>(null, "EnemiesHaveCreativeInventory");
            thisModConfig.BindConfig<KickStart>(null, "MaxEnemyBaseLimit");
            thisModConfig.BindConfig<KickStart>(null, "MaxBasesPerTeam");
            thisModConfig.BindConfig<KickStart>(null, "AllowSeaEnemiesToSpawn");
            thisModConfig.BindConfig<KickStart>(null, "AirEnemiesSpawnRate");
            thisModConfig.BindConfig<KickStart>(null, "DesignsToLog");
            thisModConfig.BindConfig<KickStart>(null, "AIPopMaxLimit");
            thisModConfig.BindConfig<KickStart>(null, "TryForceOnlyPlayerSpawns");
            thisModConfig.BindConfig<KickStart>(null, "CommitDeathMode");
            thisModConfig.BindConfig<KickStart>(null, "CatMode");
            thisModConfig.BindConfig<KickStart>(null, "EnemySellGainModifier");
            thisModConfig.BindConfig<KickStart>(null, "CullFarEnemyBases");
            thisModConfig.BindConfig<KickStart>(null, "ForceRemoveOverEnemyMaxCap"); 

            // RTS
            thisModConfig.BindConfig<KickStart>(null, "AllowStrategicAI");
            thisModConfig.BindConfig<KickStart>(null, "UseClassicRTSControls");
            thisModConfig.BindConfig<KickStart>(null, "UseNumpadForGrouping");
            thisModConfig.BindConfig<KickStart>(null, "CommandHotkeySav");
            thisModConfig.BindConfig<KickStart>(null, "CommandBoltsHotkeySav");
            thisModConfig.BindConfig<KickStart>(null, "MultiSelectKeySav");
            thisModConfig.BindConfig<KickStart>(null, "ModeSelectKeySav");

            KickStart.RetreatHotkey = (KeyCode)KickStart.RetreatHotkeySav;
            KickStart.CommandHotkey = (KeyCode)KickStart.CommandHotkeySav;
            KickStart.CommandBoltsHotkey = (KeyCode)KickStart.CommandBoltsHotkeySav;
            KickStart.MultiSelect = (KeyCode)KickStart.MultiSelectKeySav;
            return thisModConfig;
        }
        internal static void PushExtModConfigHandling()
        {
            KickStart.SavedDefaultEnemyFragility = Globals.inst.moduleDamageParams.detachMeterFillFactor;

            var thisModConfig = PushExtModConfigSetup();
            try
            {
                KickStartNativeOptions.PushExtModOptionsHandling(thisModConfig);
            }
            catch (Exception e)
            {
                throw new Exception("PushExtModOptionsHandling within PushExtModConfigHandling hit exception - ", e);
            }
            finally
            {
                OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
            }
        }
    }

    public class KickStartNativeOptions
    {
        // NativeOptions Parameters
        public static OptionKey retreatHotkey;
        public static OptionKey commandHotKey;
        public static OptionKey commandBoltsHotKey;
        public static OptionKey groupSelectKey;
        public static OptionKey modeSelectKey;
        public static OptionToggle commandClassic;
        public static OptionToggle betterAI;
        public static OptionToggle aiSelfRepair;
        public static OptionToggle aiSelfRepair2;
        public static OptionRange dodgePeriod;
        public static OptionRange aiUpkeepRefresh; //AIClockPeriod
        public static OptionRange aiPathing;
        public static OptionToggle muteNonPlayerBuildRacket;
        public static OptionToggle allowOverLevelBlocksDrop;
        public static OptionToggle exportReadableRAW;
        public static OptionToggle displayEvents;
        public static OptionToggle painfulEnemies;
        public static OptionRange diff;
        public static OptionRange landEnemyChangeChance;
        public static OptionRange blockRecoveryChance;
        public static OptionToggle permitEradication;
        public static OptionToggle infEnemySupplies;
        public static OptionRange enemyExpandLim;
        public static OptionRange enemyAirSpawn;
        public static OptionToggle enemySeaSpawn;
        public static OptionToggle playerMadeTechsOnly;
        public static OptionRange enemyBaseCount;
        public static OptionRange enemyMaxCount;
        public static OptionRange enemyMaxCountForceLim;
        public static OptionToggle ragnarok;
        public static OptionToggle copycat;
        public static OptionToggle enemyStrategic;
        public static OptionToggle enemyMiners;
        public static OptionToggle useKeypadForGroups;
        public static OptionToggle enemyBaseCulling;

        public static OptionToggle WarnEnemyLock;

        internal static void PushExtModOptionsHandling()
        {
            var TACAI = KickStart.ModID + " - A.I. General";
#if !STEAM  // Because this toggle is reserved for the loading and unloading of the mod in STEAM release
            betterAI = new OptionToggle("<b>Enable Mod</b>", TACAI, KickStart.EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { KickStart.EnableBetterAI = betterAI.SavedValue; });
#endif
            WarnEnemyLock = new OptionToggle("Flying Enemy Warnings", TACAI, KickStart.WarnOnEnemyLock);
            WarnEnemyLock.onValueSaved.AddListener(() => { KickStart.WarnOnEnemyLock = WarnEnemyLock.SavedValue; });
            muteNonPlayerBuildRacket = new OptionToggle("Mute Non-Player Build Racket", TACAI, KickStart.MuteNonPlayerRacket);
            muteNonPlayerBuildRacket.onValueSaved.AddListener(() => { KickStart.MuteNonPlayerRacket = muteNonPlayerBuildRacket.SavedValue; });
            exportReadableRAW = new OptionToggle("Export .JSON instead of .RAWTECH", TACAI, RawTechExporter.ExportJSONInsteadOfRAWTECH);
            exportReadableRAW.onValueSaved.AddListener(() => { RawTechExporter.ExportJSONInsteadOfRAWTECH = exportReadableRAW.SavedValue; });

            retreatHotkey = new OptionKey("Retreat Button", TACAI, KickStart.RetreatHotkey);
            retreatHotkey.onValueSaved.AddListener(() =>
            {
                KickStart.RetreatHotkey = retreatHotkey.SavedValue;
                KickStart.RetreatHotkeySav = (int)KickStart.RetreatHotkey;
            });
            dodgePeriod = new OptionRange("A.I. Dodge Processing Laziness", TACAI, KickStart.AIDodgeCheapness - 1, 0, 60, 5);
            dodgePeriod.onValueSaved.AddListener(() => { KickStart.AIDodgeCheapness = (int)dodgePeriod.SavedValue + 1; });
            aiPathing = new OptionRange("A.I. Pathfinding Speed", TACAI, AIEPathMapper.PathRequestsToCalcPerFrame, 0, 6, 1);
            aiPathing.onValueSaved.AddListener(() => { 
                AIEPathMapper.PathRequestsToCalcPerFrame = (int)aiPathing.SavedValue;
            });

            var TACAISP = KickStart.ModID + " - A.I. Single Player";
            aiUpkeepRefresh = new OptionRange("A.I. Awareness Update Laziness", TACAISP, KickStart.AIClockPeriodSet, 5, 50, 5);
            aiUpkeepRefresh.onValueSaved.AddListener(() => { KickStart.AIClockPeriodSet = (short)aiUpkeepRefresh.SavedValue; });
            aiSelfRepair = new OptionToggle("Allow Mobile A.I.s to Build", TACAISP, KickStart.AllowAISelfRepair);
            aiSelfRepair.onValueSaved.AddListener(() => { KickStart.AllowAISelfRepair = aiSelfRepair.SavedValue; });

            var TACAIMP = KickStart.ModID + " - A.I. MP Host";
            aiSelfRepair2 = new OptionToggle("Mobile A.I. Building - Requires Beefy Networking", TACAIMP, KickStart.AllowAISelfRepairInMP);
            aiSelfRepair2.onValueSaved.AddListener(() => { KickStart.AllowAISelfRepairInMP = aiSelfRepair2.SavedValue; });


            var TACAIRTS = KickStart.ModID + " - Real-Time Strategy [RTS] Mode";
            enemyStrategic = new OptionToggle("Enable RTS A.I.", TACAIRTS, KickStart.AllowStrategicAI);//\nRandomAdditions and TweakTech highly advised for best experience
            enemyStrategic.onValueSaved.AddListener(() => {
                KickStart.AllowStrategicAI = enemyStrategic.SavedValue;
                if (KickStart.AllowStrategicAI)
                {
                    ModStatusChecker.EncapsulateSafeInit("Advanced AI", ManPlayerRTS.DelayedInitiate, KickStart.DeInitALL);
                }
                else
                {
                }
            });
            commandHotKey = new OptionKey("Enable RTS Overlay Hotkey", TACAIRTS, KickStart.CommandHotkey);
            commandHotKey.onValueSaved.AddListener(() =>
            {
                KickStart.CommandHotkey = commandHotKey.SavedValue;
                KickStart.CommandHotkeySav = (int)KickStart.CommandHotkey;
            });
            groupSelectKey = new OptionKey("Multi-Select Hotkey", TACAIRTS, KickStart.MultiSelect);
            groupSelectKey.onValueSaved.AddListener(() =>
            {
                KickStart.MultiSelect = groupSelectKey.SavedValue;
                KickStart.MultiSelectKeySav = (int)KickStart.MultiSelect;
            });
            commandBoltsHotKey = new OptionKey("Detonate Bolts Hotkey", TACAIRTS, KickStart.CommandBoltsHotkey);
            commandBoltsHotKey.onValueSaved.AddListener(() =>
            {
                KickStart.CommandBoltsHotkey = commandBoltsHotKey.SavedValue;
                KickStart.CommandBoltsHotkeySav = (int)KickStart.CommandBoltsHotkey;
            });
            modeSelectKey = new OptionKey("A.I. Menu Hotkey", TACAIRTS, KickStart.ModeSelect);
            modeSelectKey.onValueSaved.AddListener(() =>
            {
                KickStart.ModeSelect = modeSelectKey.SavedValue;
                KickStart.ModeSelectKeySav = (int)KickStart.ModeSelect;
            });
            commandClassic = new OptionToggle("Classic RTS Controls", TACAIRTS, KickStart.UseClassicRTSControls);
            commandClassic.onValueSaved.AddListener(() => { KickStart.UseClassicRTSControls = commandClassic.SavedValue; });
            useKeypadForGroups = new OptionToggle("Enable Keypad for Grouping - Check Num Lock", TACAIRTS, KickStart.UseNumpadForGrouping);
            useKeypadForGroups.onValueSaved.AddListener(() => { KickStart.UseNumpadForGrouping = useKeypadForGroups.SavedValue; });

            var TACAIEnemies = KickStart.ModID + " - Non-Player Techs (NPT) General";
            painfulEnemies = new OptionToggle("<b>Enable Advanced NPTs</b>", TACAIEnemies, KickStart.enablePainMode);
            painfulEnemies.onValueSaved.AddListener(() => { KickStart.enablePainMode = painfulEnemies.SavedValue; });
            diff = new OptionRange("NPT Difficulty [Easy-Medium-Hard]", TACAIEnemies, KickStart.difficulty, -50, 150, 25);
            diff.onValueSaved.AddListener(() =>
            {
                KickStart.difficulty = (int)diff.SavedValue;
                AIERepair.RefreshDelays();
            });
            displayEvents = new OptionToggle("Show NPT Events", TACAIEnemies, KickStart.DisplayEnemyEvents);
            displayEvents.onValueSaved.AddListener(() => { KickStart.DisplayEnemyEvents = displayEvents.SavedValue; });
            enemyMiners = new OptionToggle("NPTs Can Mine", TACAIEnemies, KickStart.AllowEnemiesToMine);
            enemyMiners.onValueSaved.AddListener(() => { KickStart.AllowEnemiesToMine = enemyMiners.SavedValue; });
            blockRecoveryChance = new OptionRange("NPT Block Drop Chance", TACAIEnemies, KickStart.EnemyBlockDropChance, 0, 100, 10);
            blockRecoveryChance.onValueSaved.AddListener(() => {
                KickStart.EnemyBlockDropChance = (int)blockRecoveryChance.SavedValue;
                Globals.inst.m_BlockSurvivalChance = (float)((float)KickStart.EnemyBlockDropChance / 100.0f);

                if (KickStart.EnemyBlockDropChance == 0)
                {
                    Globals.inst.moduleDamageParams.detachMeterFillFactor = 0;// Make enemies drop no blocks!
                }
                else
                    Globals.inst.moduleDamageParams.detachMeterFillFactor = KickStart.SavedDefaultEnemyFragility;
            });
            infEnemySupplies = new OptionToggle("All NPTechs Cheat Blocks", TACAIEnemies, KickStart.EnemiesHaveCreativeInventory);
            infEnemySupplies.onValueSaved.AddListener(() => { KickStart.EnemiesHaveCreativeInventory = infEnemySupplies.SavedValue; });
            enemyBaseCount = new OptionRange("Max Unique NPT Base Teams Active [0-6]", TACAIEnemies, KickStart.MaxEnemyBaseLimit, 0, 6, 1);
            enemyBaseCount.onValueSaved.AddListener(() => { KickStart.MaxEnemyBaseLimit = (int)enemyBaseCount.SavedValue; });
            enemyExpandLim = new OptionRange("Max NPT Anchored Techs [0-12]", TACAIEnemies, KickStart.MaxBasesPerTeam, 0, 12, 3);
            enemyExpandLim.onValueSaved.AddListener(() => { KickStart.MaxBasesPerTeam = (int)enemyExpandLim.SavedValue; });
            enemyBaseCulling = new OptionToggle("Remove Far NPT Bases", TACAIEnemies, KickStart.CullFarEnemyBases);
            enemyBaseCulling.onValueSaved.AddListener(() => { KickStart.CullFarEnemyBases = enemyBaseCulling.SavedValue; });

            if (!KickStart.isPopInjectorPresent)
            {
                var TACAIEnemiesPop = KickStart.ModID + " - Non-Player Techs (NPT) Spawning";
                enemyMaxCount = new OptionRange("NPTechs Spawn Limit [6-32]", TACAIEnemiesPop, KickStart.AIPopMaxLimit, 6, 32, 1);
                enemyMaxCount.onValueSaved.AddListener(() =>
                {
                    KickStart.AIPopMaxLimit = (int)enemyMaxCount.SavedValue;
                    KickStart.OverrideEnemyMax();
                });
                enemyMaxCountForceLim = new OptionRange("NPTechs Limit Overflow [1-12]", TACAIEnemiesPop, KickStart.ForceRemoveOverEnemyMaxCap, 1, 12, 1);
                enemyMaxCountForceLim.onValueSaved.AddListener(() => 
                { 
                    KickStart.ForceRemoveOverEnemyMaxCap = (int)enemyMaxCountForceLim.SavedValue; 
                });

                landEnemyChangeChance = new OptionRange("NPT Land RawTechs Chance [0-100]", TACAIEnemiesPop, KickStart.LandEnemyOverrideChance, 0, 100, 5);
                landEnemyChangeChance.onValueSaved.AddListener(() => { KickStart.LandEnemyOverrideChance = (int)landEnemyChangeChance.SavedValue; });
                enemyAirSpawn = new OptionRange("NPT Aircraft Frequency [0x - 2x - 4x]", TACAIEnemiesPop, KickStart.AirEnemiesSpawnRate, 0, 4, 0.5f);
                enemyAirSpawn.onValueSaved.AddListener(() => {
                    KickStart.AirEnemiesSpawnRate = (int)enemyAirSpawn.SavedValue;
                    if (KickStart.AirEnemiesSpawnRate == 0)
                        KickStart.AllowAirEnemiesToSpawn = false;
                    else
                    {
                        SpecialAISpawner.AirSpawnInterval = 60 / KickStart.AirEnemiesSpawnRate;
                        KickStart.AllowAirEnemiesToSpawn = true;
                    }
                });
                enemySeaSpawn = new OptionToggle("NPT Sea Spawning (Needs Water Mod)", TACAIEnemiesPop, KickStart.AllowSeaEnemiesToSpawn);
                enemySeaSpawn.onValueSaved.AddListener(() => { KickStart.AllowSeaEnemiesToSpawn = enemySeaSpawn.SavedValue; });
                playerMadeTechsOnly = new OptionToggle("Disable Built-In Spawn Pool (\"Raw Techs\" Folder Only)", TACAIEnemiesPop, KickStart.TryForceOnlyPlayerSpawns);
                playerMadeTechsOnly.onValueSaved.AddListener(() => { KickStart.TryForceOnlyPlayerSpawns = playerMadeTechsOnly.SavedValue; });
                permitEradication = new OptionToggle("<b>Eradicators</b> - Huge NPT Tech Spawns - Requires Beefy Computer", TACAIEnemiesPop, KickStart.EnemyEradicators);
                permitEradication.onValueSaved.AddListener(() => { KickStart.EnemyEradicators = permitEradication.SavedValue; });
                ragnarok = new OptionToggle("<b>Ragnarok</b> - Death To All (Anything Can Spawn) - Requires Beefy Computer", TACAIEnemiesPop, KickStart.CommitDeathMode);
                ragnarok.onValueSaved.AddListener(() =>
                {
                    KickStart.CommitDeathMode = ragnarok.SavedValue;
                    OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
                }); 
                /*
                copycat = new OptionToggle("<b>Copy Cat</b> - Enemy can use your local Tech snaps!", TACAIEnemiesPop, KickStart.CatMode);
                copycat.onValueSaved.AddListener(() =>
                {
                    KickStart.CatMode = copycat.SavedValue;
                });*/
            }
            if (KickStart.EnemyBlockDropChance == 0)
            {
                Globals.inst.moduleDamageParams.detachMeterFillFactor = 0;// Make enemies drop no blocks!
            }

            if (KickStart.AirEnemiesSpawnRate == 0)
                KickStart.AllowAirEnemiesToSpawn = false;
            else
            {
                SpecialAISpawner.AirSpawnInterval = 60 / KickStart.AirEnemiesSpawnRate;
                KickStart.AllowAirEnemiesToSpawn = true;
            }
        }

        internal static void PushExtModOptionsHandling(ModConfig thisModConfig)
        {
            PushExtModOptionsHandling();
            if (thisModConfig != null)
                NativeOptionsMod.onOptionsSaved.AddListener(() => { thisModConfig.WriteConfigJsonFile(); });
        }
    }
    public static class VectorExtentions
    {
        public static bool WithinBox(this Vector3 vec, float extents)
        {
            return vec.x >= -extents && vec.x <= extents && vec.y >= -extents && vec.y <= extents && vec.z >= -extents && vec.z <= extents;
        }
        public static bool WithinSquareXZ(this Vector3 vec, float extents)
        {
            return vec.x >= -extents && vec.x <= extents && vec.z >= -extents && vec.z <= extents;
        }
        public static bool WithinBox(this IntVector2 vec, int extents)
        {
            return vec.x >= -extents && vec.x <= extents && vec.y >= -extents && vec.y <= extents;
        }
        public static Vector3 Clamp01Box(this Vector3 vec)
        {
            return Vector3.Min(Vector3.Max(-Vector3.one, vec), Vector3.one);
        }
    }
}
