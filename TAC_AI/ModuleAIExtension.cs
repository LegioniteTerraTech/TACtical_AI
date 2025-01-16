using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using SafeSaves;
using TerraTechETCUtil;
using System.Runtime.Remoting.Messaging;

public class ModuleAIExtension : TAC_AI.ModuleAIExtension { }
namespace TAC_AI
{
    [AutoSaveComponent]
    public class ModuleAIExtension : ExtModule
    {
        public AIDriverType PreferedDriver = AIDriverType.AutoSet;

        // Set by saves ingame
        [SSaveField]
        public AIDriverType SavedAIDriver;
        [SSaveField]
        public AIType SavedAI;
        [SSaveField]
        public EAttackMode AttackMode;
        [SSaveField]
        public int LastTargetVisibleID = -1;
        [SSaveField]
        public bool RTSActive = false;
        [SSaveField]
        public Vector3 RTSInTilePos = Vector3.zero;
        [SSaveField]
        public IntVector2 RTSPosTile = IntVector2.zero;
        [SSaveField]
        public bool WasMobileAnchor = false;
        [SSaveField]
        public string BooleanSerial = null;

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
            "InventoryUser" = false;// Can the AI use the player Inventory?
            "Builder" = false;      // Can the AI build new Techs?
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
        public bool AidAI = false;          // Should the AI be willing to sacrifice themselves for their owner's (or asset's) safety?
        public bool SelfRepairAI = false;   // Can the AI self-repair?
        public bool InventoryUser = false;  // Can the AI use the player Inventory?
        public bool Builder = false;        // Can the AI build new Techs?
        //public bool AnimeAI = false;      // Do we attempt a hookup to the AnimeAI mod and display a character for this AI?

        /// <summary>
        /// Range to chase enemy
        /// </summary>
        public float MaxCombatRange = 100;
        /// <summary>
        /// Minimum range to enemy
        /// </summary>
        public float MinCombatRange = 50;

        /// <summary>
        /// Changed from OnFirstAttach
        /// </summary>
        protected override void Pool()
        {
            if (block.IsAttached)
                OnAttach();
        }
        public void DelayedSub()
        {
            if (block.IsAttached)
                LoadToTech();
        }

        private static ExtUsageHint.UsageHint hintGSOa = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintGSOa",
            AltUI.ObjectiveString("GSO's") + " anchored A.I. has extended range and base capabilities.");
        private static ExtUsageHint.UsageHint hintGSOm = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintGSOm",
            AltUI.ObjectiveString("GSO's") + " mobile A.I. can " + AltUI.HighlightString("Mine") + ", " + 
            AltUI.HighlightString("Protect") + " vehicles, " + AltUI.HighlightString("Build") +
            " techs, and sail " + AltUI.HighlightString("Ships") + ".", 10);

        private static ExtUsageHint.UsageHint hintGCm = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintGCm",
            AltUI.ObjectiveString("GeoCorp's") + " mobile A.I. can " + AltUI.HighlightString("Mine") + ", " + 
            AltUI.HighlightString("Mimic") + " all, block " + AltUI.HighlightString("Repair") + 
            ", and " + AltUI.HighlightString("Fetch") + " blocks.", 10);

        private static ExtUsageHint.UsageHint hintVENm = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintVENm",
            AltUI.ObjectiveString("Venture's") + " mobile A.I. can " + AltUI.HighlightString("Plan") + " paths, " +
            AltUI.HighlightString("Pilot") + " air, " + AltUI.HighlightString("Broadside") +
            ", and sail " + AltUI.HighlightString("Ships") + ".", 10);

        private static ExtUsageHint.UsageHint hintHEa = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintHEa",
            AltUI.ObjectiveString("Hawkeye's") + " anchored A.I. has extended range and base capabilities.");
        private static ExtUsageHint.UsageHint hintHEm = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintHEm",
            AltUI.ObjectiveString("Hawkeye's") + " mobile A.I. " + AltUI.HighlightString("Plan") + " paths, " +
            AltUI.HighlightString("Pilot") + " air, handle " + AltUI.HighlightString("Space") +
            ", and " + AltUI.HighlightString("Scout") + " out " + AltUI.EnemyString("Enemies") + ".", 10);

        private static ExtUsageHint.UsageHint hintBFm = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintBFm",
            AltUI.ObjectiveString("Better Future's") + " mobile A.I. " + AltUI.HighlightString("Plan") + " paths, handle " +
            AltUI.HighlightString("Space") + ", block " + AltUI.HighlightString("Repair") +
            ", and use the " + AltUI.HighlightString("Inventory") + ".", 10);

        private static ExtUsageHint.UsageHint hintSJm = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleAIExtension.hintSJm",
            AltUI.ObjectiveString("Space Junkers'") + " mobile A.I. can " + AltUI.HighlightString("Plan") + " paths, " + 
            AltUI.HighlightString("Mine") + ", " + AltUI.HighlightString("Fetch") + " blocks, block "
            + AltUI.HighlightString("Repair") + ", and sail " + AltUI.HighlightString("Ships") + ".", 10);

        public override void OnGrabbed()
        {
            switch (ManSpawn.inst.GetCorporation(block.BlockType))
            {
                case FactionSubTypes.GSO:
                    if (GetComponent<ModuleAnchor>())
                        hintGSOa.Show();
                    else
                        hintGSOm.Show();
                    break;
                case FactionSubTypes.GC:
                    hintGCm.Show();
                    break;
                case FactionSubTypes.EXP:
                    break;
                case FactionSubTypes.VEN:
                    hintVENm.Show();
                    break;
                case FactionSubTypes.HE:
                    if (GetComponent<ModuleAnchor>())
                        hintHEa.Show();
                    else
                        hintHEm.Show();
                    break;
                case FactionSubTypes.SPE:
                    break;
                case FactionSubTypes.BF:
                    hintBFm.Show();
                    break;
                default:
                    break;
            }
        }
        public override void OnAttach()
        {
            block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            block.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec, bool>(OnSerializeSnapshot));
            if (!KickStart.EnableBetterAI)
                return;
            var tankInst = block.transform.root.GetComponent<Tank>();
            if (!tankInst)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ModuleAIExtention - TANK IS NULL!!!");
                return;
            }
            SavedAI = block.tank.GetHelperInsured().DediAI;
            //var helper = block.tank.GetHelperInsured();
            //helper.AIList.Add(this);
            //helper.RefreshAI();
        }
        public override void OnDetach()
        {
            block.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            block.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec, bool>(OnSerializeSnapshot));
            if (!KickStart.EnableBetterAI)
                return;
            if (!block?.tank)
            {
                DebugTAC_AI.Info(KickStart.ModID + ": ModuleAIExtention - Tank IS NULL AND BLOCK IS LOOSE");
                return;
            }
            var helper = block.tank.GetHelperInsured();
            //helper.AIList.Remove(this);
            //if (!TankBlock.IsBeingRecycled())
            //    helper.RefreshAI();
            SavedAI = AIType.Escort;
        }

        public void AlterExisting()
        {
            try
            {
                var name = gameObject.name;
                //DebugTAC_AI.Log(KickStart.ModID + ": Init new AI for " + name);
                if (name == "GSO_AI_Module_Guard_111")
                {
                    Aegis = true;
                    Prospector = true;
                    Buccaneer = true;
                    Builder = true;
                    AidAI = true;
                    AdvAvoidence = true;
                    //SelfRepairAI = true; // testing
                }
                else if (name == "GSO_AIAnchor_121")
                {
                    Aegis = true;
                    Prospector = true;
                    Buccaneer = true;
                    Builder = true;
                    AidAI = true;
                    MaxCombatRange = 150;
                }
                else if (name == "GC_AI_Module_Guard_222")
                {
                    Prospector = true;
                    Energizer = true;
                    Scrapper = true; 
                    AutoAnchor = true;
                    //SelfRepairAI = true; // EXTREMELY POWERFUL
                    MTForAll = true;
                    MeleePreferred = true;
                    AdvAvoidence = true;
                }
                else if (name == "VEN_AI_Module_Guard_111")
                {
                    Aviator = true;
                    Buccaneer = true;
                    SidePreferred = true;
                    AdvAvoidence = true;
                    MaxCombatRange = 300;
                }
                else if (name == "HE_AI_Module_Guard_112")
                {
                    Assault = true;
                    Aviator = true;
                    Astrotech = true;
                    AdvancedAI = true;
                    AdvAvoidence = true;
                    MinCombatRange = 50;
                    MaxCombatRange = 200;
                }
                else if (name == "HE_AI_Turret_111")
                {
                    Assault = true;
                    Aviator = true;
                    Astrotech = true;
                    AdvancedAI = true;
                    Builder = true;
                    MinCombatRange = 50;
                    MaxCombatRange = 225;
                }
                else if (name == "BF_AI_Module_Guard_212")
                {
                    Astrotech = true;
                    SelfRepairAI = true; // EXTREMELY POWERFUL
                    InventoryUser = true;
                    AdvAvoidence = true;
                    MinCombatRange = 60;
                    MaxCombatRange = 250;
                }
                else if (name == "SJ_AI_Module_Guard_122")
                {
                    Assault = true;
                    Prospector = true;
                    Scrapper = true;
                    Buccaneer = true;
                    AutoAnchor = true;
                    MinCombatRange = 30;
                    MaxCombatRange = 125;
                    AdvAvoidence = true;
                }
                /*
                else if (name == "RR_AI_Module_Guard_212")
                {
                    Energizer = true;
                    AdvAvoidence = true;
                    MinCombatRange = 160;
                    MaxCombatRange = 220;
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
                    Builder = true;
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
                if (tank.Team == ManPlayer.inst.PlayerTeam)
                    OnGrabbed();
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": CRASH ON HANDLING EXISTING AIS");
                DebugTAC_AI.Log(e);
            }
        }

        public void SaveRTS(TankAIHelper help, IntVector3 posInternal)
        {
            RTSActive = help.RTSControlled;
            WorldPosition WP = WorldPosition.FromScenePosition(posInternal);
            RTSInTilePos = WP.TileRelativePos;
            RTSPosTile = WP.TileCoord;
            //DebugTAC_AI.Log("GetRTSScenePos - " + RTSPosTile + " | " + RTSInTilePos);
        }

        [Serializable]
        // Now obsolete
        public class SerialData : Module.SerialData<SerialData>
        {
            public AIType savedMode;
            public bool wasRTS;
            public Vector3 RTSPos;
        }
        public Vector3 GetRTSScenePos()
        {
            //DebugTAC_AI.Info("GetRTSScenePos - " + RTSPosTile + " | " + RTSInTilePos);
            return new WorldPosition(RTSPosTile, RTSInTilePos).ScenePosition;
        }
        private bool LoadToTech()
        {
            try
            {
                if (Serialize(false))
                {
                    var helper = block.tank.GetHelperInsured();
                    if (helper.DediAI != SavedAI || helper.DriverType != SavedAIDriver)
                    {
                        if (KickStart.TransferLegacyIfNeeded(SavedAI, out AIType newtype, out AIDriverType driver))
                        {
                            SavedAIDriver = driver;
                            SavedAI = newtype;
                        }
                        helper.SetDriverType(SavedAIDriver);
                        helper.DediAI = SavedAI;
                        DebugTAC_AI.Info("AI State was saved as " + SavedAIDriver + " | " + SavedAI);
                        helper.dirtyAI = true;
                        if (WasMobileAnchor)
                        { 
                        }
                    }
                    if (RTSActive)
                    {
                        helper.RTSDestination = GetRTSScenePos();
                    }
                    return true;
                }
            }
            catch (Exception e){ DebugTAC_AI.Log(KickStart.ModID + ": error on fetch " + e); }
            return false;
        }

        internal bool Serialize(bool Saving)
        {
            KickStart.TryHookUpToSafeSavesIfNeeded();
            if (Saving)
            {
                var helper = tank.GetHelperInsured();
                SaveRTS(helper, helper.RTSDestination);
                AISettingsSet.StringSerial(tank, true, ref BooleanSerial);
                return this.SerializeToSafe();
            }
            bool deserial = this.DeserializeFromSafe();
            AISettingsSet.StringSerial(tank, false, ref BooleanSerial);
            //DebugTAC_AI.Log("AI State was saved as " + SavedAIDriver + " | " + SavedAI + " | loaded " + deserial);
            //DebugTAC_AI.Log("GetRTSScenePos - " + RTSPosTile + " | " + RTSInTilePos);
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
                        var Helper = block.tank.GetHelperInsured();
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
                        AttackMode = Helper.AttackMode;
                        WasMobileAnchor = Helper.PlayerAllowAutoAnchoring;
                        switch (Helper.DediAI)
                        {
                            case AIType.Assault:
                                if (Helper.lastEnemy)
                                    LastTargetVisibleID = Helper.lastEnemy.ID;
                                else
                                    LastTargetVisibleID = -1;
                                break;
                            case AIType.Aegis:
                                if (Helper.theResource)
                                    LastTargetVisibleID = Helper.theResource.ID;
                                else
                                    LastTargetVisibleID = -1;
                                break;
                            case AIType.Prospector:
                                if (Helper.theResource)
                                    LastTargetVisibleID = Helper.theResource.ID;
                                else
                                    LastTargetVisibleID = -1;
                                break;
                            case AIType.Scrapper:
                                if (Helper.theResource)
                                    LastTargetVisibleID = Helper.theResource.ID;
                                else
                                    LastTargetVisibleID = -1;
                                break;
                            case AIType.Energizer:
                                if (Helper.theResource)
                                    LastTargetVisibleID = Helper.theResource.ID;
                                else
                                    LastTargetVisibleID = -1;
                                break;
                            default:
                                LastTargetVisibleID = -1;
                                break;
                        }
                        Serialize(true);
                        // OBSOLETE - CAN CAUSE CRASHES
                        //serialData.Store(blockSpec.saveState);
                        //DebugTAC_AI.Log(KickStart.ModID + ": Saved " + SavedAI.ToString() + " in gameObject " + gameObject.name);
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
                                var helper = block.tank.GetHelperInsured();
                                if (helper.DediAI != serialData2.savedMode)
                                {
                                    helper.DediAI = serialData2.savedMode;
                                    helper.RefreshAI();
                                    //if (serialData2.savedMode == AIType.Aviator)
                                    //    helper.RecalibrateMovementAIController();
                                }
                                SavedAI = serialData2.savedMode;
                                if (serialData2.wasRTS)
                                {
                                    RTSActive = true;
                                    helper.RTSDestination = serialData2.RTSPos;
                                }
                                if (KickStart.TransferLegacyIfNeeded(SavedAI, out AIType newtype, out AIDriverType driver))
                                {
                                    SavedAIDriver = driver;
                                    SavedAI = newtype;
                                }
                                helper.SetDriverType(SavedAIDriver);
                                helper.DediAI = SavedAI;
                                helper.PlayerAllowAutoAnchoring = WasMobileAnchor;
                                helper.AttackMode = AttackMode;
                                if (LastTargetVisibleID != -1)
                                {
                                    TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(LastTargetVisibleID);
                                    if (TV != null)
                                    {
                                        switch (TV.ObjectType)
                                        {
                                            case ObjectTypes.Vehicle:
                                                if (TV.visible != null)
                                                {
                                                    if (ManBaseTeams.IsEnemy(tank.Team, TV.visible.tank.Team))
                                                    {
                                                        if (helper.DediAI == AIType.Assault)
                                                        {
                                                            helper.theResource = TV.visible;
                                                            helper.foundGoal = TV.visible;
                                                        }
                                                        else
                                                        {
                                                            helper.lastEnemy = TV.visible;
                                                        }
                                                    }
                                                    else if (helper.DediAI == AIType.Aegis)
                                                    {
                                                        helper.theResource = TV.visible;
                                                        helper.foundGoal = TV.visible;
                                                    }
                                                }
                                                break;
                                            case ObjectTypes.Block:
                                                if (helper.DediAI == AIType.Scrapper)
                                                {
                                                    helper.theResource = TV.visible;
                                                    helper.foundGoal = TV.visible;
                                                }
                                                else
                                                    DebugTAC_AI.LogError("AI " + helper.name + " expected to be Scrapper for visible ID " +
                                                        LastTargetVisibleID + " of type " + TV.ObjectType + " but was " + helper.DediAI + " instead");
                                                break;
                                            case ObjectTypes.Scenery:
                                                if (helper.DediAI == AIType.Prospector)
                                                {
                                                    helper.theResource = TV.visible;
                                                    helper.foundGoal = TV.visible;
                                                }
                                                else
                                                    DebugTAC_AI.LogError("AI " + helper.name + " expected to be Prospector for visible ID " +
                                                        LastTargetVisibleID + " of type " + TV.ObjectType + " but was " + helper.DediAI + " instead");
                                                break;
                                            default:
                                                DebugTAC_AI.LogError("AI " + helper.name + " wasn't expecting a " + TV.ObjectType + " for visible ID " +
                                                    LastTargetVisibleID);
                                                break;
                                        }
                                    }
                                }
                                //DebugTAC_AI.Log(KickStart.ModID + ": Loaded " + SavedAI.ToString() + " from gameObject " + gameObject.name);
                                TankAIManager.AILoadedEvent.Send(helper);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { } // MP caused error - cannot resolve
        }


        internal void OnSerializeSnapshot(bool saving, TankPreset.BlockSpec blockSpec, bool tankPresent)
        {
            if (!tankPresent)
                return;
            if (saving)
            {
                tank.GetHelperInsured().AISetSettings.SaveFromAI(blockSpec);
                DebugTAC_AI.Info("AI State(SNAPSHOT) was saved");
            }
            else
            {
                new AISettingsSet(blockSpec).LoadToAI(tank);
            }

        }
    }

}
