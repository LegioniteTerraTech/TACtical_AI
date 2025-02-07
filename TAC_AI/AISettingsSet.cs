using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI
{
    public interface AISettings
    {
        /// <summary>Range to stop before enemy</summary>
        float CombatSpacing { get; }
        /// <summary>Range to pursue enemies</summary>
        float CombatChase { get; }
        /// <summary>Maximum Range to stray from objective</summary>
        float ObjectiveRange { get; }
        bool shouldChase { get; }


        /// <summary>Should the AI take combat calculations and retreat if nesseary?</summary>
        bool AdvancedAI { get; }
        /// <summary>Should the AI only follow player movement while in MT mode?</summary>
        bool AllMT { get; }
        /// <summary>Should the AI ram the enemy?</summary>
        bool FullMelee { get; }
        /// <summary>Should the AI circle the enemy?</summary>
        bool SideToThreat { get; }

        // Repair Auxilliaries
        /// <summary>Allied auto-repair</summary>
        bool AutoRepair { get; }
        /// <summary>Draw from player inventory reserves</summary>
        bool UseInventory { get; }
    }
    public struct AISettingsLimit : AISettings
    {
        private TankAIHelper helper;

        /// <summary>Range to stop before enemy</summary>
        public float CombatSpacing { get => combatRange; }
        private float combatRange;
        /// <summary>Range to pursue enemies</summary>
        public float CombatChase { get => chaseRange; }
        private float chaseRange;
        /// <summary>Maximum Range to stray from objective</summary>
        public float ObjectiveRange { get => AIGlobals.DefaultMaxObjectiveRange; }
        public bool shouldChase => CombatChase > 0;


        /// <summary>Should the AI take combat calculations and retreat if nesseary?</summary>
        public bool AdvancedAI { get => advancedAI; }
        private bool advancedAI;
        /// <summary>Should the AI only follow player movement while in MT mode?</summary>
        public bool AllMT { get => allMT; }
        private bool allMT;
        /// <summary>Should the AI ram the enemy?</summary>
        public bool FullMelee { get => fullMelee; }
        private bool fullMelee;
        /// <summary>Should the AI circle the enemy?</summary>
        public bool SideToThreat { get => sideToThreat; }
        private bool sideToThreat;

        // Repair Auxilliaries
        /// <summary>Allied auto-repair</summary>
        public bool AutoRepair => helper.TechMemor;
        /// <summary>Draw from player inventory reserves</summary>
        public bool UseInventory { get => useInventory; }
        private bool useInventory;


        public AISettingsLimit(TankAIHelper helperInst)
        {
            helper = helperInst;

            combatRange = 25;
            chaseRange = AIGlobals.DefaultMaxTargetingRange;

            advancedAI = false;
            allMT = false;
            fullMelee = false;
            sideToThreat = false;
            useInventory = false;
        }

        public bool OverrideAdvAI { set => advancedAI = value; }

        public void OverrideForBuilder(bool setTrue = true)
        {
            advancedAI = setTrue;
            useInventory = setTrue;
        }

        public void Recalibrate()
        {
            combatRange = 25;
            fullMelee = false;
            advancedAI = false;
            allMT = false;
            sideToThreat = false;
            foreach (ModuleAIExtension AIEx in helper.AIList)
            {
                if (AIEx.AdvancedAI)
                    advancedAI = true;
                if (AIEx.MeleePreferred)
                    fullMelee = true;
                if (AIEx.MTForAll)
                    allMT = true;
                if (AIEx.SidePreferred)
                    sideToThreat = true;
                if (AIEx.InventoryUser)
                    useInventory = true;

                // Engadgement Ranges
                if (AIEx.MinCombatRange > combatRange)
                    combatRange = AIEx.MinCombatRange;
                if (AIEx.MaxCombatRange > chaseRange)
                    chaseRange = AIEx.MaxCombatRange;
            }
        }
    }

    public struct AISettingsSet : AISettings
    {
        /// <summary>Spacing: Range to stop before enemy</summary>
        public float CombatSpacing { get => combatSpacing; set => combatSpacing = value; }
        private float combatSpacing;
        /// <summary>Chase: Range to pursue enemies</summary>
        public float CombatChase { get => combatChase; set => combatChase = value; }
        private float combatChase;
        /// <summary>Maximum Range to stray from objective</summary>
        public float ObjectiveRange { get => objectiveRange; set => objectiveRange = value; }
        private float objectiveRange;
        /// <summary>Maximum Range to search for job articles</summary>
        public float ScanRange { get => scanRange; set => scanRange = value; }
        private float scanRange;
        public bool shouldChase => CombatChase > 0;


        /// <summary>Should the AI take combat calculations and retreat if nesseary?</summary>
        public bool AdvancedAI { get => advancedAI; set => advancedAI = value; }
        private bool advancedAI;
        /// <summary>Should the AI only follow player movement while in MT mode?</summary>
        public bool AllMT { get => allMT; set => allMT = value; }
        private bool allMT;
        /// <summary>Should the AI ram the enemy?</summary>
        public bool FullMelee { get => fullMelee; set => fullMelee = value; }
        private bool fullMelee;
        /// <summary>Should the AI circle the enemy?</summary>
        public bool SideToThreat { get => sideToThreat; set => sideToThreat = value; }
        private bool sideToThreat;

        // Repair Auxilliaries
        /// <summary>Allied auto-repair</summary>
        public bool AutoRepair { get => autoRepair; set => autoRepair = value; }
        private bool autoRepair;
        /// <summary>Draw from player inventory reserves</summary>
        public bool UseInventory { get => useInventory; set => useInventory = value; }
        private bool useInventory;

        public static AISettingsSet DefaultSettable => new AISettingsSet(
            50, 
            5000,
            AIGlobals.DefaultMaxTargetingRange, 
            AIGlobals.DefaultMaxObjectiveRange, 
            true);

        public AISettingsSet(float combatR, float searchR, float chaseR, float jobR, bool toggleDefault)
        {
            combatSpacing = combatR;
            objectiveRange = searchR;
            combatChase = chaseR;
            scanRange = jobR;

            advancedAI = toggleDefault;
            allMT = toggleDefault;
            fullMelee = toggleDefault;
            sideToThreat = toggleDefault;
            autoRepair = toggleDefault;
            useInventory = toggleDefault;
        }

        /// <summary>
        /// Get it from Tech save data
        /// </summary>
        /// <param name="blockSpec"></param>
        public AISettingsSet(TankPreset.BlockSpec blockSpec)
        {
            SetIfPossible(blockSpec, "CombatR", out combatSpacing);
            SetIfPossible(blockSpec, "SearchR", out objectiveRange);
            SetIfPossible(blockSpec, "ChaseR", out combatChase);
            SetIfPossible(blockSpec, "JobR", out scanRange);

            SetIfPossible(blockSpec, "Melee", out fullMelee, true);
            SetIfPossible(blockSpec, "Side", out sideToThreat, true);
            SetIfPossible(blockSpec, "Advanced", out advancedAI, true);
            SetIfPossible(blockSpec, "PlayerMT", out allMT, true);
            SetIfPossible(blockSpec, "Repair", out autoRepair, true);
            SetIfPossible(blockSpec, "Inventory", out useInventory, true);
        }

        public AISettingsSet(AISettings refr)
        {
            combatSpacing = refr.CombatSpacing;
            objectiveRange = refr.ObjectiveRange;
            combatChase = refr.CombatChase;
            scanRange = AIGlobals.DefaultMaxObjectiveRange;

            advancedAI = refr.AdvancedAI;
            allMT = refr.AllMT;
            fullMelee = refr.AllMT;
            sideToThreat = refr.SideToThreat;
            autoRepair = refr.AutoRepair;
            useInventory = refr.UseInventory;
        }

        public void OverrideForBuilder(bool setTrue = true)
        {
            autoRepair = setTrue;
            advancedAI = setTrue;
            useInventory = setTrue;
        }


        // Utilities
        public void ClampMaxFloats(AISettingsSet settings)
        {
            CombatSpacing = Mathf.Min(CombatSpacing, settings.CombatSpacing);
            ObjectiveRange = Mathf.Min(ObjectiveRange, settings.ObjectiveRange);
            CombatChase = Mathf.Min(CombatChase, settings.CombatChase);
        }
        public float GetJobRange(Tank tank)
        {
            if (ScanRange == 0)
                return tank.Vision.SearchRadius > 100 ? tank.Vision.SearchRadius : 100;
            return ScanRange;
        }

        public void Sync(TankAIHelper help, AISettings limiter)
        {
            if (help.Allied)
            {
                CombatSpacing = Mathf.Min(CombatSpacing, limiter.CombatSpacing);
                CombatChase = Mathf.Min(CombatChase, limiter.CombatChase);
                ObjectiveRange = Mathf.Min(ObjectiveRange, limiter.ObjectiveRange);

                AdvancedAI = AdvancedAI && limiter.AdvancedAI;
                AllMT = AllMT && limiter.AllMT;
                FullMelee = FullMelee && limiter.FullMelee;
                SideToThreat = SideToThreat && limiter.SideToThreat;

                AutoRepair = AutoRepair && limiter.AutoRepair;
                UseInventory = UseInventory && limiter.UseInventory;
            }
        }

        internal void GUIDisplay(AISettings lim, ref bool delta)
        {
            GUIAIManager.StatusLabelButtonToggle(new Rect(20, 145, 80, 30), "RAM+", lim.FullMelee, ref fullMelee,
                "Smart Melee with target", "Need GeoCorp A.I.", ref delta);
            GUIAIManager.StatusLabelButtonToggle(new Rect(100, 145, 80, 30), "Side", lim.SideToThreat, ref sideToThreat,
                "Attacks broadside to target", "Need Venture A.I.", ref delta);
            GUIAIManager.StatusLabelButtonToggle(new Rect(20, 175, 80, 30), "CPU+", lim.AdvancedAI, ref advancedAI,
                "Smarter overall Logic", "Need Hawkeye or Venture A.I.", ref delta);
            GUIAIManager.StatusLabelButtonToggle(new Rect(100, 175, 80, 30), "Multi+", lim.AllMT, ref allMT,
                "Multi-Tech works with other A.I.", "Need GeoCorp A.I.", ref delta);
            GUIAIManager.StatusLabelButtonToggle(new Rect(20, 205, 80, 30), "Repair", lim.AutoRepair, ref autoRepair,
                "Replace missing blocks", "Need GeoCorp or Better Future A.I.", ref delta);
            GUIAIManager.StatusLabelButtonToggle(new Rect(100, 205, 80, 30), "SCU", lim.UseInventory, ref useInventory,
                "Repair using inventory blocks", "Need Better Future A.I.", ref delta);
        }



        // Serialization
        [Flags]
        public enum AIToggleFlags : byte
        {
            None = 0,
            Melee = 1,
            Side = 2,
            Advanced = 4,
            AllMT = 8,
            AutoRepair = 16,
            Inventory = 32,
        }
        internal static void StringSerial(Tank tank, bool saving, ref string serial)
        {
            if (saving)
            {
                var helper = tank.GetHelperInsured();
                var set = helper.AISetSettings;
                AIToggleFlags flags = AIToggleFlags.None;
                if (set.UseInventory)
                    flags |= AIToggleFlags.Inventory;
                if (set.SideToThreat)
                    flags |= AIToggleFlags.Side;
                if (set.FullMelee)
                    flags |= AIToggleFlags.Melee;
                if (set.AutoRepair)
                    flags |= AIToggleFlags.AutoRepair;
                if (set.AllMT)
                    flags |= AIToggleFlags.AllMT;
                if (set.AdvancedAI)
                    flags |= AIToggleFlags.Advanced;
                serial = ((int)flags).ToString();
            }
            else
            {
                if (int.TryParse(serial, out int val))
                {
                    AIToggleFlags flags = (AIToggleFlags)val;
                    var helper = tank.GetHelperInsured();
                    helper.AISetSettings.AdvancedAI = flags.HasFlag(AIToggleFlags.Advanced);
                    helper.AISetSettings.AllMT = flags.HasFlag(AIToggleFlags.AllMT);
                    helper.AISetSettings.AutoRepair = flags.HasFlag(AIToggleFlags.AutoRepair);
                    helper.AISetSettings.FullMelee = flags.HasFlag(AIToggleFlags.Melee);
                    helper.AISetSettings.SideToThreat = flags.HasFlag(AIToggleFlags.Side);
                    helper.AISetSettings.UseInventory = flags.HasFlag(AIToggleFlags.Inventory);
                }
            }
        }

        internal void LoadToAI(Tank tank)
        {
            var helper = tank.GetHelperInsured();
            helper.AISetSettings = this;
        }

        internal void SaveFromAI(TankPreset.BlockSpec blockSpec)
        {
            SaveIfPossible(blockSpec, "CombatR", CombatSpacing);
            SaveIfPossible(blockSpec, "SearchR", ObjectiveRange);
            SaveIfPossible(blockSpec, "ChaseR", CombatChase);
            SaveIfPossible(blockSpec, "JobR", ScanRange);

            SaveIfPossible(blockSpec, "Melee", FullMelee);
            SaveIfPossible(blockSpec, "Side", SideToThreat);
            SaveIfPossible(blockSpec, "Advanced", AdvancedAI);
            SaveIfPossible(blockSpec, "PlayerMT", AllMT);
            SaveIfPossible(blockSpec, "Repair", AutoRepair);
            SaveIfPossible(blockSpec, "Inventory", UseInventory);
        }
        private static void SetIfPossible(TankPreset.BlockSpec blockSpec, string paramName, out float val, float defaultVal = 5000)
        {
            var str = blockSpec.Retrieve(typeof(ModuleAIExtension), paramName);
            if (!str.NullOrEmpty() && float.TryParse(str, out float result))
                val = result;
            else
                val = defaultVal;
        }
        private static void SetIfPossible(TankPreset.BlockSpec blockSpec, string paramName, out bool val, bool defaultVal)
        {
            var str = blockSpec.Retrieve(typeof(ModuleAIExtension), paramName);
            if (!str.NullOrEmpty() && bool.TryParse(str, out bool result))
                val = result;
            else
                val = defaultVal;
        }
        private static void SaveIfPossible(TankPreset.BlockSpec blockSpec, string paramName, float value)
        {
            blockSpec.Store(typeof(ModuleAIExtension), paramName, value.ToString());
        }
        private static void SaveIfPossible(TankPreset.BlockSpec blockSpec, string paramName, bool value)
        {
            blockSpec.Store(typeof(ModuleAIExtension), paramName, value.ToString());
        }
    }
}
