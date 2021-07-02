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
        public static int AIClockPeriod = 5;  // How frequently we update

        public static bool EnableBetterAI = true;
        public static int AIDodgeCheapness = 30;
        public static bool enablePainMode = false;
        public static bool EnemiesHaveCreativeInventory = false;

        internal static bool isWaterModPresent = false;
        internal static bool isTougherEnemiesPresent = false;
        internal static bool isWeaponAimModPresent = false;

        public static int Difficulty = 50;

        public static int LowerDifficulty { get { return Mathf.Clamp(Difficulty - 50, 0, 99); } }
        public static int UpperDifficulty { get { return Mathf.Clamp(Difficulty + 50, 1, 100); } }

        // NativeOptions Parameters
        public static OptionToggle betterAI;
        public static OptionRange dodgePeriod;
        public static OptionToggle painfulEnemies;
        public static OptionRange diff;
        public static OptionToggle infEnemySupplies;


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

            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");
            thisModConfig.BindConfig<KickStart>(null, "enablePainMode");
            thisModConfig.BindConfig<KickStart>(null, "Difficulty");
            thisModConfig.BindConfig<KickStart>(null, "EnemiesHaveCreativeInventory");

            var TACAI = ModName;
            betterAI = new OptionToggle("<b>Rebuilt AI</b> \n(Toggle this OFF and Save your Techs & Worlds to keep!)", TACAI, EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { EnableBetterAI = betterAI.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            dodgePeriod = new OptionRange("AI Dodge Processing Shoddiness", TACAI, AIDodgeCheapness, 1, 61, 5);
            dodgePeriod.onValueSaved.AddListener(() => { AIDodgeCheapness = (int)dodgePeriod.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            if (isTougherEnemiesPresent || testEnemyAI)
            {
                painfulEnemies = new OptionToggle("Painful Enemies", TACAI, enablePainMode);
                painfulEnemies.onValueSaved.AddListener(() => { enablePainMode = painfulEnemies.SavedValue; thisModConfig.WriteConfigJsonFile(); });
                diff = new OptionRange("AI Difficulty", TACAI, Difficulty, -50, 150, 25);
                diff.onValueSaved.AddListener(() => { Difficulty = (int)diff.SavedValue; thisModConfig.WriteConfigJsonFile(); });
                infEnemySupplies = new OptionToggle("Enemies Have Unlimited Parts", TACAI, EnemiesHaveCreativeInventory);
                infEnemySupplies.onValueSaved.AddListener(() => { EnemiesHaveCreativeInventory = infEnemySupplies.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            }
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
