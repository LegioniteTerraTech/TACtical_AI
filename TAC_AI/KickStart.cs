﻿using System;
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

        public static bool EnableBetterAI = true;
        public static int AIDodgeCheapness = 30;
        public static bool enablePainMode = false;

        internal static bool isWaterModPresent = false;
        internal static bool isTougherEnemiesPresent = false;

        public static int Difficulty = 50;

        public static int LowerDifficulty => Mathf.Clamp(Difficulty - 50, 0, 99);
        public static int UpperDifficulty => Mathf.Clamp(Difficulty + 50, 1, 100);

        // NativeOptions Parameters
        public static OptionToggle betterAI;
        public static OptionRange dodgePeriod;
        public static OptionToggle painfulEnemies;


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

            if (LookForMod("TougherEnemies"))
            {
                Debug.Log("TACtical_AI: Found Tougher Enemies!  MAKING THE PAIN REAL!");
                isTougherEnemiesPresent = true;
            }

            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");
            thisModConfig.BindConfig<KickStart>(null, "enablePainMode");

            var TACAI = ModName;
            betterAI = new OptionToggle("<b>Rebuilt AI</b> \n(Toggle this OFF and Save your Techs & Worlds to keep!)", TACAI, EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { EnableBetterAI = betterAI.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            dodgePeriod = new OptionRange("AI Dodge Processing Shoddiness", TACAI, AIDodgeCheapness, 1, 61, 5);
            dodgePeriod.onValueSaved.AddListener(() => { AIDodgeCheapness = (int)dodgePeriod.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            if (isTougherEnemiesPresent)
            {
                painfulEnemies = new OptionToggle("Painful Enemies", TACAI, enablePainMode);
                painfulEnemies.onValueSaved.AddListener(() => { enablePainMode = painfulEnemies.SavedValue; thisModConfig.WriteConfigJsonFile(); });
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
