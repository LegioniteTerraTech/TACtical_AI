using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;

namespace TAC_AI
{
    public class ModuleAIExtension : Module
    {
        TankBlock TankBlock;

        public AIType SavedAI;

        /*
        // What can this new AI do? 
        //   PRETTY MUCH ALL OF THE BELOW - except the ones with '>' by them for now.
        // COMBAT
        Escort,     // Good ol' player defender                     (Classic player defense numbnut)
        >Assault,    // Run off and attack the enemies on your radar (Runs off (beyond radar range!) to attack enemies)
        >Aegis,      // Protects the nearest non-player allied Tech  (Follows nearest ally, will chase enemy some distance)

        // RESOURCES
        Prospector, // Harvest Chunks and return them to base       (Returns chunks when full to nearest receiver)
        >Scrapper,   // Grab loose blocks but avoid combat           (Return to nearest base when threatened)
        >Energizer,  // Charges up and/or heals other techs          (Return to nearest base when out of power)

        // MULTECH  // Enabled for all                              (MultiTech) - BuildBeam disabled, will fire at any angle.
        MTTurret,   // Only turns to aim at enemy                     Also will follow nearest tech that's Build Beaming
        MTSlave,    // Does not move on own but does shoot back     
        MTMimic,    // Copies the actions of the closest non-MT Tech in relation     

        // ADVANCED    (REQUIRES TOUGHER ENEMIES TO USE!)           (can't just do the same without the enemies attacking these ways as well...)
        Aviator,    // Flies aircraft, death from above, nuff said  (Flies above ground, by the player and keeps distance) [unload distance will break!]
        Buccaneer,  // Sails ships amongst ye seas~                 (Avoids terrain above water level)
        Astrotech,  // Flies hoverships and kicks Tech              (Follows player a certain distance above ground level and can follow into the sky)

        //The actual module to add
        "TAC_AI.ModuleAIExtension":{ // Add a special AI type to your AI Module
            // Set the ones you want your AI to support to true
            // -----COMBAT-----
            // - Escort is enabled by default since you have to corral your minions somehow
            "Assault": false,
            "Aegis": false,

            // -----RESOURCES-----
            "Prospector": false,
            "Scrapper": false,
            "Energizer": false,

            // ----TOUGHER ENEMIES----
            "Aviator": false,
            "Buccaneer": false,
            "Astrotech": false,

            // ----EXTRAS----
            "MeleePreferred": false,// Should the AI ram the enemy?
            "SidePreferred": false, // Should the AI orbit the enemy? (Partially overrides melee)
            "AdvAvoidence": false,  // Should the AI avoid two allied techs at once?
            "AdvancedAI": false,    // Should the AI take combat calculations and retreat if nesseary? (N/A atm)
            "MTForAll": false,      // Should the AI listen to other Tech MT commands?
            "AidAI": false,         // Should the AI be willing to sacrifice themselves for their owner's safety? - (N/A)
            "SelfRepairAI": false,  // Can the AI self-repair?
            "AnimeAI": false,       // Work with the AnimeAI mod and display a character for this AI? (And also allow interaction with other characters?)

            "MinCombatRange": 50,   // Min range the AI will keep from an enemy
            "MaxCombatRange": 100,  // Max range the AI will travel from it's priority defence target (or x2 assassin provoke radius from home)
        }
        */

        // Set the ones you want your AI to support to true
        //   note to self - make these flags because it's taking more RAM than it should
        // -----COMBAT-----
        // - Escort is enabled by default since you have to corral your minions somehow
        public bool Assault = false;
        public bool Aegis = false;

        // -----RESOURCES-----
        public bool Prospector = false;
        public bool Scrapper = false;
        public bool Energizer = false;

        // ----TOUGHER ENEMIES----
        public bool Aviator = false;
        public bool Buccaneer = false;
        public bool Astrotech = false;

        // ----EXTRAS----
        public bool MeleePreferred = false; // Should the AI ram the enemy?
        public bool SidePreferred = false;  // Should the AI orbit the enemy?
        public bool AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
        public bool AdvAvoidence = false;   // Should the AI avoid two allied techs at once?
        public bool MTForAll = true;        // Should the AI listen to other Tech MT commands?
        public bool AidAI = false;          // Should the AI be willing to sacrifice themselves for their owner's safety?
        public bool SelfRepairAI = false;   // Can the AI self-repair?
        //public bool AnimeAI = false;      // Do we attempt a hookup to the AnimeAI mod and display a character for this AI?

        public float MaxCombatRange = 100;  // Range to chase enemy
        public float MinCombatRange = 50;  // Minimum range to enemy

        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
        }
        public void OnAttach()
        {
            TankBlock.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            SavedAI = TankBlock.transform.root.GetComponent<AI.AIECore.TankAIHelper>().DediAI;
            var thisInst = TankBlock.transform.root.GetComponent<AI.AIECore.TankAIHelper>();
            //thisInst.AIList.Add(this);
            //thisInst.RefreshAI();
        }
        public void OnDetach()
        {
            var thisInst = TankBlock.transform.root.GetComponent<AI.AIECore.TankAIHelper>();
            //thisInst.AIList.Remove(this);
            //if (!TankBlock.IsBeingRecycled())
            //    thisInst.RefreshAI();
            TankBlock.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            SavedAI = AIType.Escort;
        }

        [Serializable]
        private new class SerialData : SerialData<SerialData>
        {
            public AIType savedMode;
        }

        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            if (saving)
            {   //Save to snap
                if (KickStart.EnableBetterAI)
                {   //Allow resaving of Techs without mod affilation
                    SerialData serialData = new SerialData()
                    {
                        savedMode = TankBlock.transform.root.GetComponent<AI.AIECore.TankAIHelper>().DediAI
                    };
                    serialData.Store(blockSpec.saveState);
                    //Debug.Log("TACtical AI: Saved " + SavedAI.ToString() + " in gameObject " + gameObject.name);
                }
            }
            else
            {   //Load from snap
                try
                {
                    SerialData serialData2 = SerialData<SerialData>.Retrieve(blockSpec.saveState);
                    if (serialData2 != null)
                    {
                        var thisInst = TankBlock.transform.root.GetComponent<AI.AIECore.TankAIHelper>();
                        if (thisInst.DediAI != serialData2.savedMode)
                        {
                            thisInst.DediAI = serialData2.savedMode;
                            thisInst.RefreshAI();
                            if (serialData2.savedMode == AIType.Aviator)
                                thisInst.TestForFlyingAIRequirement();
                        }
                        SavedAI = serialData2.savedMode;
                        //Debug.Log("TACtical AI: Loaded " + SavedAI.ToString() + " from gameObject " + gameObject.name);
                    }
                }
                catch { }
            }
        }
    }

}
