﻿using System;
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

        // Set by saves ingame
        [SSaveField]
        public AIDriverType SavedAIDriver;
        [SSaveField]
        public AIType SavedAI;
        [SSaveField]
        public bool WasRTS = false;
        [SSaveField]
        public Vector3 RTSPos = Vector3.zero;
        [SSaveField]
        public bool WasMobileAnchor = false;

        /*
        // What can this new AI do? 
        //   PRETTY MUCH ALL OF THE BELOW - except the ones with '>' by them for now.
        // COMBAT
        Escort,     // Good ol' player defender                     (Classic player defense numbnut)
        Assault,    // Run off and attack the enemies on your radar (Runs off (beyond radar range!) to attack enemies)
        Aegis,      // Protects the nearest non-player allied Tech  (Follows nearest ally, will chase enemy some distance)

        // RESOURCES
        Prospector, // Harvest Chunks and return them to base       (Returns chunks when full to nearest receiver)
        Scrapper,   // Grab loose blocks but avoid combat           (Return to nearest base when threatened)
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
            if (TankBlock.IsAttached)
                OnAttach();
        }
        public void DelayedSub()
        {
            TankBlock.SubToBlockAttachConnected(OnAttach, OnDetach);
            if (TankBlock.IsAttached)
                LoadToTech();
        }
        public void OnAttach()
        {
            TankBlock.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            if (!KickStart.EnableBetterAI)
                return;
            var tankInst = TankBlock.transform.root.GetComponent<Tank>();
            if (!tankInst)
            {
                DebugTAC_AI.Log("TACtical_AI: ModuleAIExtention - TANK IS NULL!!!");
                return;
            }
            SavedAI = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>().DediAI;
            //var thisInst = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>();
            //thisInst.AIList.Add(this);
            //thisInst.RefreshAI();
        }
        public void OnDetach()
        {
            TankBlock.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            TankBlock.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            if (!KickStart.EnableBetterAI)
                return;
            if (!TankBlock?.transform?.root?.GetComponent<AIECore.TankAIHelper>())
            {
                DebugTAC_AI.Info("TACtical_AI: ModuleAIExtention - TankAIHelper IS NULL AND BLOCK IS LOOSE");
                return;
            }
            var thisInst = TankBlock.transform.root.GetComponent<AIECore.TankAIHelper>();
            //thisInst.AIList.Remove(this);
            //if (!TankBlock.IsBeingRecycled())
            //    thisInst.RefreshAI();
            SavedAI = AIType.Escort;
        }

        public void AlterExisting()
        {
            try
            {
                var name = gameObject.name;
                //DebugTAC_AI.Log("TACtical_AI: Init new AI for " + name);
                if (name == "GSO_AI_Module_Guard_111")
                {
                    Aegis = true;
                    Prospector = true;
                    Buccaneer = true;
                    AidAI = true;
                    //SelfRepairAI = true; // testing
                }
                else if (name == "GSO_AIAnchor_121")
                {
                    Aegis = true;
                    AidAI = true;
                    MaxCombatRange = 150;
                }
                else if (name == "GC_AI_Module_Guard_222")
                {
                    AutoAnchor = true; // temp testing
                    Prospector = true;
                    Energizer = true;
                    Scrapper = true;  // Temp Testing
                    SelfRepairAI = true; // EXTREMELY POWERFUL
                    MTForAll = true;
                    MeleePreferred = true;
                }
                else if (name == "VEN_AI_Module_Guard_111")
                {
                    Aviator = true;
                    Buccaneer = true;
                    SidePreferred = true;
                    MaxCombatRange = 300;
                }
                else if (name == "HE_AI_Module_Guard_112")
                {
                    Assault = true;
                    Aviator = true;
                    Astrotech = true;
                    AdvancedAI = true;
                    MinCombatRange = 50;
                    MaxCombatRange = 200;
                }
                else if (name == "HE_AI_Turret_111")
                {
                    Assault = true;
                    AdvancedAI = true;
                    MinCombatRange = 50;
                    MaxCombatRange = 225;
                }
                else if (name == "BF_AI_Module_Guard_212")
                {
                    Astrotech = true;
                    AdvAvoidence = true;
                    SelfRepairAI = true; // EXTREMELY POWERFUL
                    InventoryUser = true;
                    MinCombatRange = 60;
                    MaxCombatRange = 250;
                }
                /*
                else if (name == "RR_AI_Module_Guard_212")
                {
                    Energizer = true;
                    AdvAvoidence = true;
                    MinCombatRange = 160;
                    MaxCombatRange = 220;
                }
                else if (name == "SJ_AI_Module_Guard_122")
                {
                    Prospector = true;
                    Scrapper = true;
                    MTForAll = true;
                    MinCombatRange = 60;
                    MaxCombatRange = 120;
                }
                else if (name == "TSN_AI_Module_Guard_312")
                {
                    AutoAnchor = true;
                    Buccaneer = true;
                    AdvAvoidence = true;
                    MinCombatRange = 150;
                    MaxCombatRange = 250;
                }
                else if (name == "LEG_AI_Module_Guard_112")
                {   //Incase Legion happens and the AI needs help lol
                    AutoAnchor = true;
                    Assault = true;
                    Aegis = true;
                    Prospector = true;
                    Scrapper = true;
                    Energizer = true;
                    Assault = true;
                    Aviator = true;
                    Buccaneer = true;
                    Astrotech = true;
                    AidAI = true;
                    AdvancedAI = true;
                    AdvAvoidence = true;
                    SidePreferred = true;
                    MeleePreferred = true;
                    MaxCombatRange = 200;
                }
                else if (name == "TAC_AI_Module_Plex_323")
                {
                    AutoAnchor = true;
                    Aviator = true;
                    Buccaneer = true;
                    Astrotech = true;
                    AidAI = true;
                    AnimeAI = true;
                    AdvancedAI = true;
                    AdvAvoidence = true;
                    MinCombatRange = 100;
                    MaxCombatRange = 400;
                }
                */
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: CRASH ON HANDLING EXISTING AIS");
                DebugTAC_AI.Log(e);
            }
        }


        [Serializable]
        // Now obsolete
        public class SerialData : Module.SerialData<SerialData>
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
                        DebugTAC_AI.Info("AI State was saved as " + SavedAIDriver + " | " + SavedAI);
                        thisInst.RefreshAI();
                        thisInst.RecalibrateMovementAIController();
                        if (WasMobileAnchor)
                        { 
                        }
                    }
                    if (WasRTS)
                    {
                        thisInst.RTSDestination = RTSPos;
                    }
                    return true;
                }
            }
            catch (Exception e){ DebugTAC_AI.Log("TACtical AI: error on fetch " + e); }
            return false;
        }

        internal bool Serialize(bool Saving)
        {
            KickStart.TryHookUpToSafeSavesIfNeeded();
            if (Saving)
                return this.SerializeToSafe();
            bool deserial = this.DeserializeFromSafe();
            DebugTAC_AI.Info("AI State was saved as " + SavedAIDriver + " | " + SavedAI + " | loaded " + deserial);
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
                        WasMobileAnchor = Helper.PlayerAllowAutoAnchoring;
                        Serialize(true);
                        // OBSOLETE - CAN CAUSE CRASHES
                        //serialData.Store(blockSpec.saveState);
                        //DebugTAC_AI.Log("TACtical AI: Saved " + SavedAI.ToString() + " in gameObject " + gameObject.name);
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
                                        thisInst.RecalibrateMovementAIController();
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
                                thisInst.PlayerAllowAutoAnchoring = WasMobileAnchor;
                                //DebugTAC_AI.Log("TACtical AI: Loaded " + SavedAI.ToString() + " from gameObject " + gameObject.name);
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
