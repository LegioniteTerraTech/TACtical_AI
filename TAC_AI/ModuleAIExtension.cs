using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using SafeSaves;

public class ModuleAIExtension : TAC_AI.ModuleAIExtension { }
namespace TAC_AI
{
    [AutoSaveComponent]
    public class ModuleAIExtension : MonoBehaviour
    {
        TankBlock TankBlock;

        [SSaveField]
        public AIDriverType SavedAIDriver;
        [SSaveField]
        public AIType SavedAI;
        [SSaveField]
        public bool WasRTS = false;
        [SSaveField]
        public Vector3 RTSPos = Vector3.zero;

        /*
        // What can this new AI do? 
        //   PRETTY MUCH ALL OF THE BELOW - except the ones with '>' by them for now.
        // COMBAT
        Escort,     // Good ol' player defender                     (Classic player defense numbnut)
        Assault,    // Run off and attack the enemies on your radar (Runs off (beyond radar range!) to attack enemies)
        Aegis,      // Protects the nearest non-player allied Tech  (Follows nearest ally, will chase enemy some distance)

        // RESOURCES
        Prospector, // Harvest Chunks and return them to base       (Returns chunks when full to nearest receiver)
        >Scrapper,   // Grab loose blocks but avoid combat           (Return to nearest base when threatened)
        Energizer,  // Charges up and/or heals other techs          (Return to nearest base when out of power)

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
            "AutoAnchor": false,    // Should the AI anchor and un-anchor automatically?
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
        public bool AutoAnchor = false;     // Should the AI handle anchors automatically?
        public bool MeleePreferred = false; // Should the AI ram the enemy?
        public bool SidePreferred = false;  // Should the AI orbit the enemy?
        public bool AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
        public bool AdvAvoidence = false;   // Should the AI avoid two allied techs at once?
        public bool MTForAll = false;       // Should the AI listen to non-player Tech MT commands?
        public bool AidAI = false;          // Should the AI be willing to sacrifice themselves for their owner's safety?
        public bool SelfRepairAI = false;   // Can the AI self-repair?
        public bool InventoryUser = false;  // Can the AI use the player Inventory?
        //public bool AnimeAI = false;      // Do we attempt a hookup to the AnimeAI mod and display a character for this AI?

        public float MaxCombatRange = 100;  // Range to chase enemy
        public float MinCombatRange = 50;  // Minimum range to enemy

        /// <summary>
        /// Changed to OnFirstAttach
        /// </summary>
        public void OnPool()
        {
            if (TankBlock)
                return;
            TankBlock = gameObject.GetComponent<TankBlock>();
            Invoke("DelayedSub", 1f);
            OnAttach();
        }
        public void DelayedSub()
        {
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
            LoadToTech();
        }
        public void OnAttach()
        {
            if (!KickStart.EnableBetterAI)
                return;
            var tankInst = TankBlock.transform.root.GetComponent<Tank>();
            if (tankInst)
            {
                if (!tankInst.GetComponent<AIECore.TankAIHelper>())
                {
                    Debug.Log("TACtical_AI: ModuleAIExtention - TankAIHelper IS NULL - making new...");
                    tankInst.gameObject.AddComponent<AIECore.TankAIHelper>().Subscribe(tankInst);
                }
            }
            else
            {
                //Debug.Log("TACtical_AI: ModuleAIExtention - TankAIHelper IS NULL AND BLOCK IS LOOSE");
                //Debug.Log("TACtical_AI: ModuleAIExtention - TANK IS NULL!!!");
                return;
            }
            TankBlock.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            SavedAI = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>().DediAI;
            //var thisInst = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>();
            //thisInst.AIList.Add(this);
            //thisInst.RefreshAI();
        }
        public void OnDetach()
        {
            if (!KickStart.EnableBetterAI)
                return;
            if (!TankBlock?.transform?.root?.GetComponent<AIECore.TankAIHelper>())
            {
                if (TankBlock?.transform?.root)
                {
                    Debug.Log("TACtical_AI: ModuleAIExtention - TankAIHelper IS NULL - making new...");
                    TankBlock.transform.root.gameObject.AddComponent<AIECore.TankAIHelper>().Subscribe(TankBlock.transform.root.GetComponent<Tank>());
                }
                else
                {
                    Debug.Log("TACtical_AI: ModuleAIExtention - TankAIHelper IS NULL AND BLOCK IS LOOSE");
                }
                return;
            }
            var thisInst = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>();
            //thisInst.AIList.Remove(this);
            //if (!TankBlock.IsBeingRecycled())
            //    thisInst.RefreshAI();
            TankBlock.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            SavedAI = AIType.Escort;
        }

        [Serializable]
        // Now obsolete
        private new class SerialData : Module.SerialData<SerialData>
        {
            public AIType savedMode;
            public bool wasRTS;
            public Vector3 RTSPos;
        }
        private bool LoadToTech()
        {
            try
            {
                if (Serialize(false))
                {
                    var thisInst = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>();
                    if (thisInst.DediAI != SavedAI || thisInst.DriverType != SavedAIDriver)
                    {
                        if (KickStart.TransferLegacyIfNeeded(SavedAI, out AIType newtype, out AIDriverType driver))
                        {
                            SavedAIDriver = driver;
                            SavedAI = newtype;
                        }
                        thisInst.DriverType = SavedAIDriver;
                        thisInst.DediAI = SavedAI;
                        Debug.Log("AI State was saved as " + SavedAIDriver + " | " + SavedAI);
                        thisInst.RefreshAI();
                        if (SavedAIDriver == AIDriverType.Pilot)
                            thisInst.TestForFlyingAIRequirement();
                    }
                    if (WasRTS)
                    {
                        thisInst.RTSDestination = RTSPos;
                    }
                    return true;
                }
            }
            catch (Exception e){ Debug.Log("TACtical AI: error on fetch " + e); }
            return false;
        }

        internal bool Serialize(bool Saving)
        {
            if (Saving)
                return this.SerializeToSafe();
            bool deserial = this.DeserializeFromSafe();
            Debug.Log("AI State was saved as " + SavedAIDriver + " | " + SavedAI + " | loaded " + deserial);
            return deserial;
        }
        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (saving)
                {   //Save to snap
                    if (KickStart.EnableBetterAI && !Singleton.Manager<ManScreenshot>.inst.TakingSnapshot)
                    {   //Allow resaving of Techs but not saving this to snapshot to prevent bugs
                        var Helper = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>();
                        /*
                        SerialData serialData;
                        if (Helper.RTSControlled)
                        {
                            serialData = new SerialData()
                            {
                                savedMode = Helper.DediAI,
                                wasRTS = Helper.RTSControlled,
                                RTSPos = Helper.RTSDestination,
                            };
                        }
                        else
                        {
                            serialData = new SerialData()
                            {
                                savedMode = Helper.DediAI,
                            };
                        }
                        */
                        SavedAIDriver = Helper.DriverType;
                        SavedAI = Helper.DediAI;
                        WasRTS = Helper.RTSControlled;
                        RTSPos = Helper.RTSDestination;
                        Serialize(true);
                        // OBSOLETE - CAN CAUSE CRASHES
                        //serialData.Store(blockSpec.saveState);
                        //Debug.Log("TACtical AI: Saved " + SavedAI.ToString() + " in gameObject " + gameObject.name);
                    }
                }
                else
                {   //Load from save
                    try
                    {
                        if (!LoadToTech())
                        {
                            SerialData serialData2 = Module.SerialData<SerialData>.Retrieve(blockSpec.saveState);
                            if (serialData2 != null)
                            {
                                var thisInst = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>();
                                if (thisInst.DediAI != serialData2.savedMode)
                                {
                                    thisInst.DediAI = serialData2.savedMode;
                                    thisInst.RefreshAI();
                                    if (serialData2.savedMode == AIType.Aviator)
                                        thisInst.TestForFlyingAIRequirement();
                                }
                                SavedAI = serialData2.savedMode;
                                if (serialData2.wasRTS)
                                {
                                    WasRTS = true;
                                    thisInst.RTSDestination = serialData2.RTSPos;
                                }
                                if (KickStart.TransferLegacyIfNeeded(SavedAI, out AIType newtype, out AIDriverType driver))
                                {
                                    SavedAIDriver = driver;
                                    SavedAI = newtype;
                                }
                                thisInst.DriverType = SavedAIDriver;
                                thisInst.DediAI = SavedAI;
                                //Debug.Log("TACtical AI: Loaded " + SavedAI.ToString() + " from gameObject " + gameObject.name);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { } // MP caused error - cannot resolve
        }
    }

}
