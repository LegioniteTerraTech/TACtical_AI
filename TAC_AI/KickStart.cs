using System;
using System.Reflection;
using Harmony;
using UnityEngine;
using ModHelper.Config;
using Nuterra.NativeOptions;


namespace TAC_AI
{
    // A mod that does exactly as it says on the tin
    //   A bunch of random additions that make make little to no sense, but they be.
    //
    public class KickStart
    {
        const string ModName = "TACtical AIs";

        public static bool EnableBetterAI = true;
        public static bool isWaterModPresent = false;
        public static int AIDodgeCheapness = 30;

        // NativeOptions Parameters
        public static OptionToggle betterAI;
        public static OptionRange dodgePeriod;


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


            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "EnableBetterAI");
            thisModConfig.BindConfig<KickStart>(null, "AIDodgeCheapness");


            var TACAI = ModName;
            betterAI = new OptionToggle("Rebuilt AI", TACAI, EnableBetterAI);
            betterAI.onValueSaved.AddListener(() => { EnableBetterAI = betterAI.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            dodgePeriod = new OptionRange("AI Dodging (Higher = better performance but less AI accuraccy)", TACAI, AIDodgeCheapness, 1, 61, 5);
            dodgePeriod.onValueSaved.AddListener(() => { AIDodgeCheapness = (int)dodgePeriod.SavedValue; thisModConfig.WriteConfigJsonFile(); });
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
