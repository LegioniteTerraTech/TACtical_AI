using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI
{
    public struct AISettings 
    {
        /// <summary>Range to stop before enemy</summary>
        public float CombatRange;
        /// <summary>Range to pursue enemies</summary>
        public float ChaseRange;
        /// <summary>Maximum Range to stray from objective</summary>
        public float ObjectiveRange;
        /// <summary>Maxiumum Range to search for job articles</summary>
        public float ScanRange;
        public bool shouldChase => ChaseRange > 0;


        public bool AdvancedAI;     // Should the AI take combat calculations and retreat if nesseary?
        public bool AllMT;   // Should the AI only follow player movement while in MT mode?
        public bool FullMelee;      // Should the AI ram the enemy?
        public bool SideToThreat;   // Should the AI circle the enemy?

        // Repair Auxilliaries
        public bool AutoRepair;// Allied auto-repair
        public bool UseInventory;   // Draw from player inventory reserves

        public static AISettings DefaultSettable => new AISettings(5000, 5000, 5000, 500, true);
        public static AISettings DefaultLimit => new AISettings(50, 20, 25, 0, false);


        public AISettings(float combatR, float searchR, float chaseR, float jobR, bool toggleDefault)
        {
            CombatRange = combatR;
            ObjectiveRange = searchR;
            ChaseRange = chaseR;
            ScanRange = jobR;

            AdvancedAI = toggleDefault;
            AllMT = toggleDefault;
            FullMelee = toggleDefault;
            SideToThreat = toggleDefault;
            AutoRepair = toggleDefault;
            UseInventory = toggleDefault;
        }

        /// <summary>
        /// Get it from Tech save data
        /// </summary>
        /// <param name="blockSpec"></param>
        public AISettings(TankPreset.BlockSpec blockSpec)
        {
            SetIfPossible(blockSpec, "CombatR", out CombatRange);
            SetIfPossible(blockSpec, "SearchR", out ObjectiveRange);
            SetIfPossible(blockSpec, "ChaseR", out ChaseRange);
            SetIfPossible(blockSpec, "JobR", out ScanRange);

            SetIfPossible(blockSpec, "Melee", out FullMelee, true);
            SetIfPossible(blockSpec, "Side", out SideToThreat, true);
            SetIfPossible(blockSpec, "Advanced", out AdvancedAI, true);
            SetIfPossible(blockSpec, "PlayerMT", out AllMT, true);
            SetIfPossible(blockSpec, "Repair", out AutoRepair, true);
            SetIfPossible(blockSpec, "Inventory", out UseInventory, true);
        }

        public void ClampMax(AISettings settings)
        {
            CombatRange = Mathf.Min(CombatRange, settings.CombatRange);
            ObjectiveRange = Mathf.Min(ObjectiveRange, settings.ObjectiveRange);
            ChaseRange = Mathf.Min(ChaseRange, settings.ChaseRange);
        }
        public float GetJobRange(Tank tank)
        {
            if (ScanRange == 0)
                return tank.Vision.SearchRadius > 100 ? tank.Vision.SearchRadius : 100;
            return ScanRange;
        }
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
            SaveIfPossible(blockSpec, "CombatR", CombatRange);
            SaveIfPossible(blockSpec, "SearchR", ObjectiveRange);
            SaveIfPossible(blockSpec, "ChaseR", ChaseRange);
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
