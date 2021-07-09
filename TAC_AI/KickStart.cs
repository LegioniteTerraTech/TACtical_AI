using System;
using System.Reflection;
using Harmony;
using UnityEngine;
using ModHelper.Config;
using Nuterra.NativeOptions;


namespace TAC_AI
{
    // Previously an extension to RandomAdditions, TACtical AI is the AI branch of the mod.
    //
    public class KickStart
    {
        const string ModName = "TACtical AIs";

        internal static bool testEnemyAI = true;
        internal static int MaxEnemySplitLimit = 20;
        internal static int MaxEnemyBaseLimit = 3;  // How many bases are allowed to exist in one instance
        internal static int MaxEnemyHQLimit = 1;    // How many HQs are allowed to exist in one instance
        public static int AIClockPeriod = 5;        // How frequently we update

        public static bool EnableBetterAI = true;
        public static int AIDodgeCheapness = 30;
        public static bool MuteNonPlayerRacket = true;
        public static bool enablePainMode = false;
        public static bool EnemiesHaveCreativeInventory = false;
        public static bool AllowEnemiesToStartBases = false;
        public static bool DesignsToLog = false;

        internal static bool isWaterModPresent = false;
        internal static bool isTougherEnemiesPresent = false;
        internal static bool isWeaponAimModPresent = false;
        internal static bool isBlockInjectorPresent = false;

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


        internal static bool firedAfterBlockInjector = false;


        public static void Main()
        {
            //Where the fun begins

            //Initiate the madness
            HarmonyInstance harmonyInstance = HarmonyInstance.Create("legionite.tactical_ai");
            try
            {
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Error on patch");
                Debug.Log(e);
            }
            AI.AIECore.TankAIManager.Initiate();
            GUIAIManager.Initiate();

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

            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");
            thisModConfig.BindConfig<KickStart>(null, "MuteNonPlayerRacket");
            thisModConfig.BindConfig<KickStart>(null, "enablePainMode");
            thisModConfig.BindConfig<KickStart>(null, "Difficulty");
            thisModConfig.BindConfig<KickStart>(null, "EnemiesHaveCreativeInventory");
            thisModConfig.BindConfig<KickStart>(null, "AllowEnemiesToStartBases");
            thisModConfig.BindConfig<KickStart>(null, "DesignsToLog");

            var TACAI = ModName;
            betterAI = new OptionToggle("<b>Rebuilt AI</b> \n(Toggle this OFF and Save your Techs & Worlds to keep!)", TACAI, EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { EnableBetterAI = betterAI.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            dodgePeriod = new OptionRange("AI Dodge Processing Shoddiness", TACAI, AIDodgeCheapness, 1, 61, 5);
            dodgePeriod.onValueSaved.AddListener(() => { AIDodgeCheapness = (int)dodgePeriod.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            muteNonPlayerBuildRacket = new OptionToggle("Mute Non-Player Build Racket", TACAI, MuteNonPlayerRacket);
            muteNonPlayerBuildRacket.onValueSaved.AddListener(() => { MuteNonPlayerRacket = muteNonPlayerBuildRacket.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            if (isTougherEnemiesPresent || testEnemyAI)
            {
                painfulEnemies = new OptionToggle("Painful Enemies", TACAI, enablePainMode);
                painfulEnemies.onValueSaved.AddListener(() => { enablePainMode = painfulEnemies.SavedValue; thisModConfig.WriteConfigJsonFile(); });
                diff = new OptionRange("AI Difficulty", TACAI, Difficulty, -50, 150, 25);
                diff.onValueSaved.AddListener(() => { Difficulty = (int)diff.SavedValue; thisModConfig.WriteConfigJsonFile(); });
                enemyBaseSpawn = new OptionToggle("Enemies Can Start Bases", TACAI, AllowEnemiesToStartBases);
                enemyBaseSpawn.onValueSaved.AddListener(() => { AllowEnemiesToStartBases = enemyBaseSpawn.SavedValue; thisModConfig.WriteConfigJsonFile(); });
                infEnemySupplies = new OptionToggle("Enemies Have Unlimited Parts", TACAI, EnemiesHaveCreativeInventory);
                infEnemySupplies.onValueSaved.AddListener(() => { EnemiesHaveCreativeInventory = infEnemySupplies.SavedValue; thisModConfig.WriteConfigJsonFile(); });
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
