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
        internal const string ModName = "TACtical AIs";

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
        public static bool AllowAISelfRepair = true;
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
                DebugTAC_AI.Info("TACtical_AI: GUI - Releasing control of " + (focused.NullOrEmpty() ? "unnamed" : focused));
                GUI.FocusControl(null);
                GUI.UnfocusWindow();
                GUIUtility.hotControl = 0;
            }
            else
            {
                if (focused == Name)
                {
                    DebugTAC_AI.Info("TACtical_AI: GUI - Releasing control of " + (focused.NullOrEmpty() ? "unnamed" : focused));
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
                float outValue = -100;
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
                DebugTAC_AI.Log("TACtical_AI: Found RandomAdditions!  Enabling advanced AI for parts!");
                IsRandomAdditionsPresent = true;
            }
            else IsRandomAdditionsPresent = false;

            if (LookForMod("WaterMod"))
            {
                DebugTAC_AI.Log("TACtical_AI: Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }
            else isWaterModPresent = false;

            if (LookForMod("Control Block"))
            {
                DebugTAC_AI.Log("TACtical_AI: Control Blocks!  Letting RawTech loader override unassigned swivels to auto-target!");
                isControlBlocksPresent = true;
            }
            else isControlBlocksPresent = false;

            if (LookForMod("WeaponAimMod"))
            {
                DebugTAC_AI.Log("TACtical_AI: Found WeaponAimMod!  Halting aim-related changes and letting WeaponAimMod take over!");
                isWeaponAimModPresent = true;
            }
            else isWeaponAimModPresent = false;

            if (LookForMod("TweakTech"))
            {
                DebugTAC_AI.Log("TACtical_AI: Found TweakTech!  Applying changes to AI!");
                isTweakTechPresent = true;
            }
            else isTweakTechPresent = false;
            /*
            if (LookForMod("TougherEnemies"))
            {
                DebugTAC_AI.Log("TACtical_AI: Found Tougher Enemies!  MAKING THE PAIN REAL!");
                isTougherEnemiesPresent = true;
            }*/

            if (LookForMod("BlockInjector"))
            {
                DebugTAC_AI.Log("TACtical_AI: Found Block Injector!  Setting up modded base support!");
                isBlockInjectorPresent = true;
            }
            else isBlockInjectorPresent = false;

            if (LookForMod("PopulationInjector"))
            {
                DebugTAC_AI.Log("TACtical_AI: Found Population Injector!  Holding off on using built-in spawning system!");
                isPopInjectorPresent = true;
            }
            else isPopInjectorPresent = false;

            if (LookForMod("AnimeAI"))
            {
                DebugTAC_AI.Log("TACtical_AI: Found Anime AI!  Hooking into commentary system and actions!");
                isAnimeAIPresent = true;
            }
            else isAnimeAIPresent = false;
            return true;
        }

        public static void PatchMod()
        {
            DebugTAC_AI.Log("TACtical_AI: Patch Call");
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
                    DebugTAC_AI.Log("TACtical_AI: Patched");
                    hasPatched = true;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on patch");
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
                DebugTAC_AI.Log("TACtical_AI: DLL is " + assemble.GetName());
                ManSafeSaves.RegisterSaveSystem(assemble);
                HasHookedUpToSafeSaves = true;
            }
            catch { 
                DebugTAC_AI.Log("TACtical_AI: Error on RegisterSaveSystem");
                DebugTAC_AI.ErrorReport("Error on hooking to SafeSaves");
            }
        }

        public static void MainOfficialInit()
        {
            //Where the fun begins
#if STEAM
            DebugTAC_AI.Log("TACtical_AI: MAIN (Steam Workshop Version) startup");
            if (!VALIDATE_MODS())
            {
                return;
            }
#else
            DebugTAC_AI.Log("TACtical_AI: Startup was invoked by TTSMM!  Set-up to handle LATE initialization.");
#endif

            //Initiate the madness
            PatchMod();

            HookToSafeSaves();

            AIECore.TankAIManager.Initiate();
            GUIAIManager.Initiate();
            RawTechExporter.Initiate();
            RLoadedBases.BaseFunderManager.Initiate();
            ManEnemyWorld.Initiate();
            SpecialAISpawner.Initiate();
            GUIEvictionNotice.Initiate();


            AIERepair.RefreshDelays();
            // Because official fails to init this while switching modes
            SpecialAISpawner.DetermineActiveOnModeType();
            AIECore.TankAIManager.inst.CorrectBlocksList();

            InitSettings();

            if (!isPopInjectorPresent)
                OverrideEnemyMax();
#if STEAM
            EnableBetterAI = true;
#endif
        }

#if STEAM
        public static IEnumerator<float> MainOfficialInitIterate()
        {
            //Where the fun begins
            DebugTAC_AI.Log("TACtical_AI: MAIN [ITERATOR] (Steam Workshop Version) startup");
            if (!VALIDATE_MODS())
            {
                yield return 1f;
            }

            //Initiate the madness
            PatchMod();
            yield return 0.1f;

            HookToSafeSaves();
            yield return 0.16f;

            AIECore.TankAIManager.Initiate();
            yield return 0.24f;

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

            GUIEvictionNotice.Initiate();
            AIERepair.RefreshDelays();
            yield return 0.72f;

            // Because official fails to init this while switching modes
            SpecialAISpawner.DetermineActiveOnModeType();
            yield return 0.80f;
            AIECore.TankAIManager.inst.CorrectBlocksList();
            yield return 0.95f;

            InitSettings();
            yield return 1f;

            EnableBetterAI = true;
        }
        private static bool launched = false;
        public static void InitSettings()
        {
            if (launched)
                return;
            launched = true;
            if (isNativeOptionsPresent && isConfigHelperPresent)
            {
                try
                {
                    KickStartConfigHelper.PushExtModConfigHandling();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on Option & Config setup");
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
                    DebugTAC_AI.Log("TACtical_AI: Error on NativeOptions setup");
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
                    DebugTAC_AI.Log("TACtical_AI: Error on ConfigHelper setup");
                    DebugTAC_AI.Log(e);
                    DebugTAC_AI.ErrorReport("Error on hooks with ConfigHelper");
                }
            }

        }

        public static void DeInitCheck()
        {
            if (AIECore.TankAIManager.inst)
            {
                AIECore.TankAIManager.inst.CheckNextFrameNeedsDeInit();
            }
        }

        public static void DeInitALL()
        {
            float timeStart = Time.realtimeSinceStartup;
            DebugTAC_AI.Log("TAC_AI: Doing mod DeInit. Garbage Cleanup... current " + GC.GetTotalMemory(false));
            EnableBetterAI = false;
            if (hasPatched)
            {
                try
                {
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(ModulePatches), "TACtical_AI");
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(UIPatches), "TACtical_AI");
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(ManagerPatches), "TACtical_AI");
                    MassPatcher.MassUnPatchAllWithin(harmonyInstance, typeof(GlobalPatches), "TACtical_AI");
                    harmonyInstance.UnpatchAll("legionite.tactical_ai");
                    hasPatched = false;
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on un-patch");
                    DebugTAC_AI.Log(e);
                }
            }

            // DE-INIT ALL
            SpecialAISpawner.DeInitiate();
            ManEnemyWorld.DeInit();
            RLoadedBases.BaseFunderManager.DeInit();
            RawTechExporter.DeInit();
            GUIAIManager.DeInit();
            AIECore.TankAIManager.DeInit();
            GUIEvictionNotice.DeInit();
            try
            {
                ManSafeSaves.UnregisterSaveSystem(Assembly.GetExecutingAssembly());
                HasHookedUpToSafeSaves = false;
            }
            catch { }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            DebugTAC_AI.Log("TAC_AI: Garbage Cleanup finished with " + GC.GetTotalMemory(false) + " remaining.");
            DebugTAC_AI.Log("TAC_AI: Operation took " + (Time.realtimeSinceStartup - timeStart) + " seconds.");
        }

#else
        public static void Main()
        {
            //Where the fun begins
            DebugTAC_AI.Log("TACtical_AI: MAIN (TTMM Version) startup");
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

                AIECore.TankAIManager.Initiate();
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
                    DebugTAC_AI.Log("TACtical_AI: Error on Option & Config setup");
                    DebugTAC_AI.Log(e);
                }
            }
            catch
            {
                try
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on TTMM Init setup - there are missing dependancies!");
                    DebugTAC_AI.FatalError("Make sure you have installed SafeSaves (and RandomAdditions if it crashes again on start) in TTMM.");
                }
                catch { };
            }
        }

#endif


        public static void DelayedBaseLoader()
        {
            DebugTAC_AI.Log("TACtical_AI: LAUNCHED MODDED BLOCKS BASE VALIDATOR");
            BlockIndexer.ConstructBlockLookupList();
            TempManager.ValidateAllStringTechs();
            DebugRawTechSpawner.Initiate();
            firedAfterBlockInjector = true;
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
                catch { DebugTAC_AI.Log("TACtical_AI: Error on RegisterSaveSystem"); }
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
        public static FactionTypesExt GetCorpExtended(BlockTypes type)
        {
            return (FactionTypesExt)Singleton.Manager<ManSpawn>.inst.GetCorporation(type);
        }
        public static bool IsFactionExtension(FactionTypesExt ext)
        {
            return ext >= FactionTypesExt.AER && ext <= FactionTypesExt.LOL;
        }
        public static FactionSubTypes CorpExtToCorp(FactionTypesExt corpExt)
        {
            switch (corpExt)
            {
                case FactionTypesExt.SPE:
                //case FactionTypesExt.GSO:
                case FactionTypesExt.GT:
                case FactionTypesExt.IEC:
                    return FactionSubTypes.GSO;
                //case FactionTypesExt.GC:
                case FactionTypesExt.EFF:
                case FactionTypesExt.LK:
                    return FactionSubTypes.GC;
                //case FactionTypesExt.VEN:
                case FactionTypesExt.OS:
                    return FactionSubTypes.VEN;
                //case FactionTypesExt.HE:
                case FactionTypesExt.BL:
                case FactionTypesExt.TAC:
                    return FactionSubTypes.HE;
                //case FactionTypesExt.BF:
                case FactionTypesExt.DL:
                case FactionTypesExt.EYM:
                case FactionTypesExt.HS:
                    return FactionSubTypes.BF;
                case FactionTypesExt.EXP:
                    return FactionSubTypes.EXP;
            }
            return (FactionSubTypes)corpExt;
        }
        public static FactionTypesExt CorpExtToVanilla(FactionTypesExt corpExt)
        {
            switch (corpExt)
            {
                case FactionTypesExt.SPE:
                //case FactionTypesExt.GSO:
                case FactionTypesExt.GT:
                case FactionTypesExt.IEC:
                    return FactionTypesExt.GSO;
                //case FactionTypesExt.GC:
                case FactionTypesExt.EFF:
                case FactionTypesExt.LK:
                    return FactionTypesExt.GC;
                //case FactionTypesExt.VEN:
                case FactionTypesExt.OS:
                    return FactionTypesExt.VEN;
                //case FactionTypesExt.HE:
                case FactionTypesExt.BL:
                case FactionTypesExt.TAC:
                    return FactionTypesExt.HE;
                //case FactionTypesExt.BF:
                case FactionTypesExt.DL:
                case FactionTypesExt.EYM:
                case FactionTypesExt.HS:
                    return FactionTypesExt.BF;
                case FactionTypesExt.EXP:
                    return FactionTypesExt.EXP;
            }
            return corpExt;
        }
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
            thisModConfig.BindConfig<KickStart>(null, "EnemySellGainModifier");
            thisModConfig.BindConfig<KickStart>(null, "CullFarEnemyBases");
            thisModConfig.BindConfig<KickStart>(null, "AllowEnemiesToMine");

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
        public static OptionToggle ragnarok;
        public static OptionToggle enemyStrategic;
        public static OptionToggle enemyMiners;
        public static OptionToggle useKeypadForGroups;
        public static OptionToggle enemyBaseCulling;

        internal static void PushExtModOptionsHandling()
        {
            var TACAI = KickStart.ModName + " - A.I. Settings";
#if !STEAM  // Because this toggle is reserved for the loading and unloading of the mod in STEAM release
            betterAI = new OptionToggle("<b>Enable Mod</b>", TACAI, KickStart.EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { KickStart.EnableBetterAI = betterAI.SavedValue; });
#endif
            retreatHotkey = new OptionKey("Retreat Button", TACAI, KickStart.RetreatHotkey);
            retreatHotkey.onValueSaved.AddListener(() =>
            {
                KickStart.RetreatHotkey = retreatHotkey.SavedValue;
                KickStart.RetreatHotkeySav = (int)KickStart.RetreatHotkey;
            });
            aiSelfRepair = new OptionToggle("Allow Mobile A.I.s to Build", TACAI, KickStart.AllowAISelfRepair);
            aiSelfRepair.onValueSaved.AddListener(() => { KickStart.AllowAISelfRepair = aiSelfRepair.SavedValue; });
            dodgePeriod = new OptionRange("A.I. Dodge Processing Laziness", TACAI, KickStart.AIDodgeCheapness - 1, 0, 60, 5);
            dodgePeriod.onValueSaved.AddListener(() => { KickStart.AIDodgeCheapness = (int)dodgePeriod.SavedValue + 1; });
            aiUpkeepRefresh = new OptionRange("A.I. Awareness Update Laziness", TACAI, KickStart.AIClockPeriodSet, 5, 50, 5);
            aiUpkeepRefresh.onValueSaved.AddListener(() => { KickStart.AIClockPeriodSet = (short)aiUpkeepRefresh.SavedValue; });
            aiPathing = new OptionRange("A.I. Pathfinding Speed", TACAI, AIEPathMapper.PathRequestsToCalcPerFrame, 0, 6, 1);
            aiPathing.onValueSaved.AddListener(() => { 
                AIEPathMapper.PathRequestsToCalcPerFrame = (int)aiPathing.SavedValue;
            });

            muteNonPlayerBuildRacket = new OptionToggle("Mute Non-Player Build Racket", TACAI, KickStart.MuteNonPlayerRacket);
            muteNonPlayerBuildRacket.onValueSaved.AddListener(() => { KickStart.MuteNonPlayerRacket = muteNonPlayerBuildRacket.SavedValue; });
            exportReadableRAW = new OptionToggle("Export .JSON instead of .RAWTECH", TACAI, RawTechExporter.ExportJSONInsteadOfRAWTECH);
            exportReadableRAW.onValueSaved.AddListener(() => { RawTechExporter.ExportJSONInsteadOfRAWTECH = exportReadableRAW.SavedValue; });

            var TACAIRTS = KickStart.ModName + " - Real-Time Strategy [RTS] Mode";
            enemyStrategic = new OptionToggle("Enable RTS A.I.", TACAIRTS, KickStart.AllowStrategicAI);//\nRandomAdditions and TweakTech highly advised for best experience
            enemyStrategic.onValueSaved.AddListener(() => {
                KickStart.AllowStrategicAI = enemyStrategic.SavedValue;
                if (KickStart.AllowStrategicAI)
                {
                    ManPlayerRTS.DelayedInitiate();
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

            var TACAIEnemies = KickStart.ModName + " - Non-Player Techs (NPT) General";
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
            infEnemySupplies = new OptionToggle("All NPT Techs Cheat Blocks", TACAIEnemies, KickStart.EnemiesHaveCreativeInventory);
            infEnemySupplies.onValueSaved.AddListener(() => { KickStart.EnemiesHaveCreativeInventory = infEnemySupplies.SavedValue; });
            enemyBaseCount = new OptionRange("Max Unique NPT Base Teams Loaded [0-6]", TACAIEnemies, KickStart.MaxEnemyBaseLimit, 0, 6, 1);
            enemyBaseCount.onValueSaved.AddListener(() => { KickStart.MaxEnemyBaseLimit = (int)enemyBaseCount.SavedValue; });
            enemyExpandLim = new OptionRange("Max NPT Anchored Techs [0-12]", TACAIEnemies, KickStart.MaxBasesPerTeam, 0, 12, 3);
            enemyExpandLim.onValueSaved.AddListener(() => { KickStart.MaxBasesPerTeam = (int)enemyExpandLim.SavedValue; });
            enemyBaseCulling = new OptionToggle("Remove Distant NPT Bases", TACAIEnemies, KickStart.CullFarEnemyBases);
            enemyBaseCulling.onValueSaved.AddListener(() => { KickStart.CullFarEnemyBases = enemyBaseCulling.SavedValue; });

            if (!KickStart.isPopInjectorPresent)
            {
                var TACAIEnemiesPop = KickStart.ModName + " - Non-Player Techs (NPT) Spawning";
                enemyMaxCount = new OptionRange("Max NPT Techs Loaded [6-32]", TACAIEnemiesPop, KickStart.AIPopMaxLimit, 6, 32, 1);
                enemyMaxCount.onValueSaved.AddListener(() =>
                {
                    KickStart.AIPopMaxLimit = (int)enemyMaxCount.SavedValue;
                    KickStart.OverrideEnemyMax();
                });
                landEnemyChangeChance = new OptionRange("NPT Land RawTechs Chance", TACAIEnemiesPop, KickStart.LandEnemyOverrideChance, 0, 100, 5);
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
                enemySeaSpawn = new OptionToggle("NPT Ship Spawning (Need Water Mod)", TACAIEnemiesPop, KickStart.AllowSeaEnemiesToSpawn);
                enemySeaSpawn.onValueSaved.AddListener(() => { KickStart.AllowSeaEnemiesToSpawn = enemySeaSpawn.SavedValue; });
                playerMadeTechsOnly = new OptionToggle("Disable Built-In Spawn Pool (\"Raw Techs\" Folder Only)", TACAIEnemiesPop, KickStart.TryForceOnlyPlayerSpawns);
                playerMadeTechsOnly.onValueSaved.AddListener(() => { KickStart.TryForceOnlyPlayerSpawns = playerMadeTechsOnly.SavedValue; });
                permitEradication = new OptionToggle("<b>Eradicators</b> - Huge NPT Tech Spawns - Requires Beefy Computer", TACAIEnemiesPop, KickStart.EnemyEradicators);
                permitEradication.onValueSaved.AddListener(() => { KickStart.EnemyEradicators = permitEradication.SavedValue; });
                ragnarok = new OptionToggle("<b>Ragnarok - Death To All (Spawns have no restrictions)</b> - Requires Beefy Computer", TACAIEnemiesPop, KickStart.CommitDeathMode);
                ragnarok.onValueSaved.AddListener(() =>
                {
                    KickStart.CommitDeathMode = ragnarok.SavedValue;
                    OverrideManPop.ChangeToRagnarokPop(KickStart.CommitDeathMode);
                });
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
    public static class TankExtentions
    {
        public static bool IsTeamFounder(this TechData tank)
        {
            if (tank == null)
            {
                DebugTAC_AI.LogError("TACtical_AI: IsTeamFounder - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.Name.Contains('Ω') || tank.Name.Contains('⦲');
        }
        public static bool IsTeamFounder(this Tank tank)
        {
            if (!tank)
            {
                DebugTAC_AI.LogError("TACtical_AI: IsTeamFounder - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.name.Contains('Ω') || tank.name.Contains('⦲');
        }
        public static bool IsBase(this TechData tank)
        {
            if (tank == null)
            {
                DebugTAC_AI.LogError("TACtical_AI: IsBase - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.CheckIsAnchored() || tank.Name.Contains('¥') || tank.Name.Contains(RawTechLoader.turretChar);
        }
        public static bool IsBase(this Tank tank)
        {
            if (!tank)
            {
                DebugTAC_AI.LogError("TACtical_AI: IsBase - CALLED ON NULL OBJECT");
                return false;
            }
            return tank.IsAnchored || tank.name.Contains('¥') || tank.name.Contains(RawTechLoader.turretChar);
        }
        public static AIECore.TankAIHelper GetHelperInsured(this Tank tank)
        {
            AIECore.TankAIHelper help = tank.GetComponent<AIECore.TankAIHelper>();
            if (!help)
            {
                help = tank.gameObject.AddComponent<AIECore.TankAIHelper>().Subscribe();
            }
            return help;
        }
        public static float GetCheapBounds(this Visible vis)
        {
            if (!vis)
            {
                DebugTAC_AI.LogError("TACtical_AI: GetCheapBounds - CALLED ON NULL OBJECT");
                return 1;
            }
            if (!vis.tank)
                return vis.Radius;
            AIECore.TankAIHelper help = vis.GetComponent<AIECore.TankAIHelper>();
            if (!help)
            {
                help = vis.gameObject.AddComponent<AIECore.TankAIHelper>().Subscribe();
            }
            return help.lastTechExtents;
        }
        public static float GetCheapBounds(this Tank tank)
        {
            return tank.GetHelperInsured().lastTechExtents;
        }
        private static List<FactionTypesExt> toSortCache = new List<FactionTypesExt>();
        public static FactionTypesExt GetMainCorpExt(this Tank tank)
        {
            foreach (TankBlock BlocS in tank.blockman.IterateBlocks())
            {
                toSortCache.Add(GetBlockCorpExt(BlocS.BlockType));
            }
            toSortCache = SortCorps(toSortCache);
            FactionTypesExt final = toSortCache.First();
            toSortCache.Clear();

            //DebugTAC_AI.Log("TACtical_AI: GetMainCorpExt - Selected " + final + " for main corp");
            return final;
        }
        public static FactionTypesExt GetMainCorpExt(this TechData tank)
        {
            foreach (TankPreset.BlockSpec BlocS in tank.m_BlockSpecs)
            {
                toSortCache.Add(GetBlockCorpExt(BlocS.m_BlockType));
            }
            toSortCache = SortCorps(toSortCache);
            var first = toSortCache.First();
            toSortCache.Clear();
            return first;//(FactionTypesExt)tank.GetMainCorporations().First();
        }
        internal static FactionTypesExt GetBlockCorpExt(BlockTypes BT)
        {
            int BTval = (int)BT;
            if (BTval < 5000)// Payload's range
                return (FactionTypesExt)Singleton.Manager<ManSpawn>.inst.GetCorporation(BT);

            if (BTval >= 300000 && BTval <= 303999) // Old Star
            {   // This should work until Pachu makes a VEN Block
                if (Singleton.Manager<ManSpawn>.inst.GetCorporation(BT) == FactionSubTypes.VEN)
                    return FactionTypesExt.OS;
            }
            if (BTval >= 419000 && BTval <= 419999) // Lemon Kingdom - Mobile Kingdom
                return FactionTypesExt.LK;
            if (BTval == 584147)
                return FactionTypesExt.TAC;
            if (BTval >= 584200 && BTval <= 584599) // Technocratic AI Colony - Power Density
                return FactionTypesExt.TAC;
            if (BTval >= 584600 && BTval <= 584750) // Emperical Forge Fabrication - Unit Count
                return FactionTypesExt.EFF;
            if (BTval >= 911000 && BTval <= 912000) // GreenTech - Eco Rangers
                return FactionTypesExt.GT;

            return (FactionTypesExt)Singleton.Manager<ManSpawn>.inst.GetCorporation(BT);
        }

        private static readonly List<KeyValuePair<int, FactionTypesExt>> sorter = new List<KeyValuePair<int, FactionTypesExt>>();
        private static List<FactionTypesExt> SortCorps(List<FactionTypesExt> unsorted)
        {
            List<FactionTypesExt> distinct = unsorted.Distinct().ToList();
            foreach (FactionTypesExt FTE in distinct)
            {
                int countOut = unsorted.FindAll(delegate (FactionTypesExt cand) { return cand == FTE; }).Count();
                sorter.Add(new KeyValuePair<int, FactionTypesExt>(countOut, FTE));
            }
            distinct.Clear();
            foreach (KeyValuePair<int, FactionTypesExt> intBT in sorter.OrderByDescending(x => x.Key))
            {
                distinct.Add(intBT.Value);
            }
            sorter.Clear();
            return distinct;
        }

        private const ModuleItemHolder.AcceptFlags flagB = ModuleItemHolder.AcceptFlags.Blocks;
        internal static bool BlockLoaded(this ModuleItemHolder MIH)
        {
            ModuleItemHolderMagnet mag = MIH.GetComponent<ModuleItemHolderMagnet>();
            if (mag)
            {
                if (!mag.IsOperating)
                    return false;
            }
            else
            {
                if (!MIH.IsEmpty && MIH.Acceptance == flagB && MIH.IsFlag(ModuleItemHolder.Flags.Collector))
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool BlockNotFullAndAvail(this ModuleItemHolder MIH)
        {
            ModuleItemHolderMagnet mag = MIH.GetComponent<ModuleItemHolderMagnet>();
            if (MIH.GetComponent<ModuleItemHolderMagnet>())
            {
                if (!mag.IsOperating)
                    return false;

            }
            else
            {
                if (!MIH.IsFull && MIH.Acceptance == flagB && MIH.IsFlag(ModuleItemHolder.Flags.Collector))
                {
                    return true;
                }
            }
            return false;
        }


        private static readonly List<BlockManager.BlockAttachment> tempCache = new List<BlockManager.BlockAttachment>(64);
        internal static bool CanAttachBlock(this Tank tank, TankBlock TB, IntVector3 posOnTechGrid, OrthoRotation rotOnTech)
        {
            if (tank.blockman.blockCount != 0)
            {
                tempCache.Clear();
                tank.blockman.TryGetBlockAttachments(TB, posOnTechGrid, rotOnTech, tempCache);
                return tempCache.Count != 0;
            }
            return false;
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
