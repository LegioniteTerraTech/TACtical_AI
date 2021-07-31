using System;
using System.Reflection;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using ModHelper.Config;
using Nuterra.NativeOptions;
using TAC_AI.AI;
using TAC_AI.Templates;


namespace TAC_AI
{
    // Previously an extension to RandomAdditions, TACtical AI is the AI branch of the mod.
    //
    public class KickStart
    {
        const string ModName = "TACtical AIs";

        internal static bool testEnemyAI = true;
        internal static int MaxEnemySplitLimit = 20;// How many techs that can exist for each team before giving up on splitting?
        internal static int MaxEnemyBaseLimit = 3;  // How many bases are allowed to exist in one instance
        internal static int MaxEnemyHQLimit = 1;    // How many HQs are allowed to exist in one instance
        public static int AIClockPeriod = 5;        // How frequently we update

        public static bool EnableBetterAI = true;
        public static int AIDodgeCheapness = 30;
        public static bool MuteNonPlayerRacket = true;
        public static bool enablePainMode = true;
        public static bool EnemiesHaveCreativeInventory = false;
        public static bool AllowEnemiesToStartBases = true;
        public static bool AllowAirEnemiesToSpawn = true;
        public static bool AllowSeaEnemiesToSpawn = true;
        public static bool DesignsToLog = false;

        //public static bool DestroyTreesInWater = false;


        internal static bool isWaterModPresent = false;
        internal static bool isTougherEnemiesPresent = false;
        internal static bool isWeaponAimModPresent = false;
        internal static bool isBlockInjectorPresent = false;
        internal static bool isPopInjectorPresent = false;
        internal static bool isAnimeAIPresent = false;

        public static int Difficulty = 50;  
        // 50 means the full AI range is used
        // -50 means only the simpleton AI spawns
        // 150 means only the smartest AI spawns

        public static int LowerDifficulty { get { return Mathf.Clamp(Difficulty - 50, 0, 99); } }
        public static int UpperDifficulty { get { return Mathf.Clamp(Difficulty + 50, 1, 100); } }

        // NativeOptions Parameters
        public static OptionToggle betterAI;
        public static OptionRange dodgePeriod;
        public static OptionToggle muteNonPlayerBuildRacket;
        public static OptionToggle painfulEnemies;
        public static OptionRange diff;
        public static OptionToggle infEnemySupplies;
        public static OptionToggle enemyBaseSpawn;
        public static OptionToggle enemyAirSpawn;
        public static OptionToggle enemySeaSpawn;


        internal static bool firedAfterBlockInjector = false;
        public static bool SpecialAttract = false;
        public static int SpecialAttractNum = 0;
        public static int retryForBote = 0;
        public static Vector3 SpecialAttractPos;

        public static float WaterHeight 
        { 
            get 
            {
                float outValue = -25;
                    try { outValue = WaterMod.QPatch.WaterHeight; } catch { }
                return outValue;
            }
        }

        public static void Main()
        {
            //Where the fun begins

            //Initiate the madness
            //HarmonyInstance harmonyInstance = HarmonyInstance.Create("legionite.tactical_ai");
            Harmony harmonyInstance = new Harmony("legionite.tactical_ai");
            try
            {
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Error on patch");
                Debug.Log(e);
            }

            AIECore.TankAIManager.Initiate();
            GUIAIManager.Initiate();
            RawTechExporter.Initiate();

            if (LookForMod("WaterMod"))
            {
                Debug.Log("TACtical_AI: Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }

            if (LookForMod("WeaponAimMod"))
            {
                Debug.Log("TACtical_AI: Found WeaponAimMod!  Halting aim-related changes and letting WeaponAimMod take over!");
                isWeaponAimModPresent = true;
            }

            if (LookForMod("TougherEnemies"))
            {
                Debug.Log("TACtical_AI: Found Tougher Enemies!  MAKING THE PAIN REAL!");
                isTougherEnemiesPresent = true;
            }

            if (LookForMod("BlockInjector"))
            {
                Debug.Log("TACtical_AI: Found Block Injector!  Setting up modded base support!");
                isBlockInjectorPresent = true;
            }
            if (LookForMod("PopulationInjector"))
            {
                Debug.Log("TACtical_AI: Found Population Injector!  Holding off on using built-in spawning system!");
                isPopInjectorPresent = true;
            }
            if (LookForMod("AnimeAI"))
            {
                Debug.Log("TACtical_AI: Found Anime AI!  Hooking into commentary system and actions!");
                isAnimeAIPresent = true;
            }

            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");
            thisModConfig.BindConfig<KickStart>(null, "MuteNonPlayerRacket");
            thisModConfig.BindConfig<KickStart>(null, "enablePainMode");
            thisModConfig.BindConfig<KickStart>(null, "Difficulty");
            thisModConfig.BindConfig<KickStart>(null, "EnemiesHaveCreativeInventory");
            thisModConfig.BindConfig<KickStart>(null, "AllowEnemiesToStartBases");
            thisModConfig.BindConfig<KickStart>(null, "AllowAirEnemiesToSpawn");
            thisModConfig.BindConfig<KickStart>(null, "AllowSeaEnemiesToSpawn");
            thisModConfig.BindConfig<KickStart>(null, "DesignsToLog");

            var TACAI = ModName + " - General";
            betterAI = new OptionToggle("<b>Rebuilt AI</b> \n(Toggle this OFF to uninstall and Save your Techs & Worlds to keep!)", TACAI, EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { EnableBetterAI = betterAI.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            dodgePeriod = new OptionRange("AI Dodge Processing Shoddiness", TACAI, AIDodgeCheapness, 1, 61, 5);
            dodgePeriod.onValueSaved.AddListener(() => { AIDodgeCheapness = (int)dodgePeriod.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            muteNonPlayerBuildRacket = new OptionToggle("Mute Non-Player Build Racket", TACAI, MuteNonPlayerRacket);
            muteNonPlayerBuildRacket.onValueSaved.AddListener(() => { MuteNonPlayerRacket = muteNonPlayerBuildRacket.SavedValue; thisModConfig.WriteConfigJsonFile(); });

            var TACAIEnemies = ModName + " - Enemies";
            painfulEnemies = new OptionToggle("<b>Rebuilt Enemies</b>", TACAIEnemies, enablePainMode);
            painfulEnemies.onValueSaved.AddListener(() => { enablePainMode = painfulEnemies.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            diff = new OptionRange("AI Difficulty", TACAI, Difficulty, -50, 150, 25);
            diff.onValueSaved.AddListener(() => { Difficulty = (int)diff.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            enemyBaseSpawn = new OptionToggle("Enemies Can Start Bases", TACAIEnemies, AllowEnemiesToStartBases);
            enemyBaseSpawn.onValueSaved.AddListener(() => { AllowEnemiesToStartBases = enemyBaseSpawn.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            infEnemySupplies = new OptionToggle("Enemies Have Unlimited Parts", TACAIEnemies, EnemiesHaveCreativeInventory);
            infEnemySupplies.onValueSaved.AddListener(() => { EnemiesHaveCreativeInventory = infEnemySupplies.SavedValue; thisModConfig.WriteConfigJsonFile(); });

            if (!isPopInjectorPresent)
            {
                enemyAirSpawn = new OptionToggle("Enemy Aircraft Spawning", TACAIEnemies, AllowAirEnemiesToSpawn);
                enemyAirSpawn.onValueSaved.AddListener(() => { AllowAirEnemiesToSpawn = enemyAirSpawn.SavedValue; thisModConfig.WriteConfigJsonFile(); });
                enemySeaSpawn = new OptionToggle("Enemy Ship Spawning", TACAIEnemies, AllowSeaEnemiesToSpawn);
                enemySeaSpawn.onValueSaved.AddListener(() => { AllowSeaEnemiesToSpawn = enemySeaSpawn.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            }


            // Now setup bases
            if (!isBlockInjectorPresent)
                InstantBaseLoader();
        }
        public static void DelayedBaseLoader()
        {
            Debug.Log("TACtical_AI: LAUNCHED MODDED BLOCKS BASE VALIDATOR");
            Templates.TempManager.ValidateAllStringTechs();
            firedAfterBlockInjector = true;
        }
        public static void InstantBaseLoader()
        {
            Debug.Log("TACtical_AI: LAUNCHED BASE VALIDATOR");
            Templates.TempManager.ValidateAllStringTechs();
        }

        public static bool LookForMod(string name)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith(name))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
