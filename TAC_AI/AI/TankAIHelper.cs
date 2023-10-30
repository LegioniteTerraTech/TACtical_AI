using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;
using TAC_AI.Templates;
using TAC_AI.World;
using TerraTech.Network;
using TerraTechETCUtil;
using UnityEngine;

namespace TAC_AI.AI
{
    /// <summary>
    /// This AI either runs normally in Singleplayer, or on the Server in Multiplayer
    /// </summary>
    public class TankAIHelper : MonoBehaviour, IWorldTreadmill
    {
        private static bool updateErrored = false;

        public Tank tank;
        public AITreeType.AITypes lastAIType;
        //Tweaks (controlled by Module)
        /// <summary> The type of vehicle the AI controls </summary>
        public AIDriverType DriverType { get; set; } = AIDriverType.AutoSet;
        /// <summary> The task the AI will perform </summary>
        public AIType DediAI = AIType.Escort;
        /// <summary> How to attack the enemy </summary>
        public EAttackMode AttackMode = EAttackMode.Circle; // How to attack the enemy
        private AlliedOperationsController _OpsController;
        internal AlliedOperationsController OpsController
        {
            get
            {
                if (_OpsController != null)
                {
                    return _OpsController;
                }
                else
                {
                    _OpsController = new AlliedOperationsController(this);
                    return _OpsController;
                }
            }
        }
        public List<ModuleAIExtension> AIList;
        public AIERepair.DesignMemory TechMemor { get; internal set; }
        public void InsureTechMemor(string context, bool doFirstSave)
        {
            if (TechMemor.IsNull())
            {
                TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                TechMemor.Initiate(doFirstSave);

                DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + " Setup for DesignMemory (" + context + ")");
            }
        }

        // Checking Booleans
        public bool AIDriving { 
            get 
            {
                if (AIAlign != AIAlignment.Static)
                {
                    if (RTSControlled)
                        return true;
                    if (tank.PlayerFocused)
                    {
                        return false;
                    }
                    else
                    {
                        bool IsPlayerRemoteControlled = false;
                        try
                        {
                            if (ManNetwork.IsNetworked)
                                IsPlayerRemoteControlled = ManNetwork.inst.GetAllPlayerTechs().Contains(tank);
                        }
                        catch { }
                        return !IsPlayerRemoteControlled;
                    }
                }
                else
                    return false;

            } 
        }
        public bool Allied => AIAlign == AIAlignment.Player;
        public bool IsPlayerControlled => AIAlign == AIAlignment.PlayerNoAI || AIAlign == AIAlignment.Player;
        public bool ActuallyWorks => hasAI || tank.PlayerFocused;
        public bool SetToActive => lastAIType == AITreeType.AITypes.Escort || lastAIType == AITreeType.AITypes.Guard;
        public bool NotInBeam => BeamTimeoutClock == 0;
        public bool CanCopyControls => !IsMultiTech || tank.PlayerFocused;
        public bool CanUseBuildBeam => !(tank.IsAnchored && !PlayerAllowAutoAnchoring);
        public bool CanAutoAnchor => AutoAnchor && PlayerAllowAutoAnchoring && !AttackEnemy && tank.Anchors.NumPossibleAnchors >= 1 && DelayedAnchorClock >= 15 && CanAnchorSafely;
        public bool CanAnchorSafely => !lastEnemyGet || (lastEnemyGet && lastCombatRange > AIGlobals.SafeAnchorDist);
        public bool MovingAndOrHasTarget => tank.IsAnchored ? lastEnemyGet : DriverType == AIDriverType.Pilot || (DriveDirDirected > EDriveFacing.Neutral && (ForceSetDrive || DoSteerCore));
        public bool UsingPathfinding => ControlCore.DrivePathing >= EDrivePathing.Path;

        // Settables in ModuleAIExtension - "turns on" functionality on the host Tech, none of these force it off
        /// <summary> Should the other AIs ignore collision with this Tech? </summary>
        public bool IsMultiTech = false;
        /// <summary> Should the AI chase the enemy? </summary>
        public bool ChaseThreat = true;
        /// <summary> Should the AI Auto-BuildBeam on flip? </summary>
        public bool RequestBuildBeam = true;

        // Player Toggleable
        /// <summary> Should the AI take combat calculations and retreat if nesseary? </summary>
        public bool AdvancedAI => Allied ? (AISetSettings.AdvancedAI && AILimitSettings.AdvancedAI) : AISetSettings.AdvancedAI;
        /// <summary> Should the AI only follow player movement while in MT mode? </summary>
        public bool AllMT => Allied ? (AISetSettings.AllMT && AILimitSettings.AllMT) : AISetSettings.AllMT; 
        /// <summary> Should the AI ram the enemy? </summary>
        public bool FullMelee => Allied ? (AISetSettings.FullMelee && AILimitSettings.FullMelee) : AISetSettings.FullMelee;
        /// <summary> Should the AI circle the enemy? </summary>
        public bool SideToThreat => Allied ? (AISetSettings.SideToThreat && AILimitSettings.SideToThreat) : AISetSettings.SideToThreat;

        // Repair Auxilliaries
        /// <summary> Auto-repair builds the tech to the last memory state. 
        /// This does not save between play sessions. </summary>
        public bool AutoRepair => Allied ? (AISetSettings.AutoRepair && AILimitSettings.AutoRepair) : AISetSettings.AutoRepair;
        /// <summary> Draw from player inventory reserves </summary>
        public bool UseInventory => Allied ? (AISetSettings.UseInventory && AILimitSettings.UseInventory) : AISetSettings.UseInventory;


        // Additional
        /// <summary> Should the AI toggle the anchor when it is still? </summary>
        public bool AutoAnchor = false;
        /// <summary> Should the AI avoid two techs at once? </summary>
        public bool SecondAvoidence = false;

        // Distance operations - Automatically accounts for tech sizes
        public AISettingsSet AISetSettings = AISettingsSet.DefaultSettable;
        public AISettingsLimit AILimitSettings = default;
        /// <summary> The range the AI will linger from the enemy while attacking if PursueThreat is true </summary>
        public float MinCombatRange => AISetSettings.CombatRange;
        /// <summary>  How far should we pursue the enemy? </summary>
        public float MaxCombatRange => AISetSettings.ChaseRange;
        /// <summary> The range the AI will linger from the target objective in general </summary>
        public float MaxObjectiveRange => AISetSettings.ObjectiveRange;
        internal float JobSearchRange
        {
            get => AISetSettings.GetJobRange(tank);
            set => AISetSettings.ScanRange = value;
        }


        // Allied AI Operating Allowed types (self-filling)
        // WARNING - These values are set to TRUE when called.
        private AIEnabledModes AIWorkingModes = AIEnabledModes.None;
        public bool isAssassinAvail //Is there an Assassin-enabled AI on this tech?
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Assassin); }
            set { AIWorkingModes |= AIEnabledModes.Assassin; }
        }
        public bool isAegisAvail    //Is there an Aegis-enabled AI on this tech?
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Aegis); }
            set { AIWorkingModes |= AIEnabledModes.Aegis; }
        }
        public bool isProspectorAvail  //Is there a Prospector-enabled AI on this tech?
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Prospector); }
            set { AIWorkingModes |= AIEnabledModes.Prospector; }
        }
        public bool isScrapperAvail   //Is there a Scrapper-enabled AI on this tech?
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Scrapper); }
            set { AIWorkingModes |= AIEnabledModes.Scrapper; }
        }
        public bool isEnergizerAvail   //Is there a Energizer-enabled AI on this tech?
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Energizer); }
            set { AIWorkingModes |= AIEnabledModes.Energizer; }
        }
        public bool isAviatorAvail
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Aviator); }
            set { AIWorkingModes |= AIEnabledModes.Aviator; }
        }
        public bool isAstrotechAvail
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Astrotech); }
            set { AIWorkingModes |= AIEnabledModes.Astrotech; }
        }
        public bool isBuccaneerAvail
        {
            get { return AIWorkingModes.HasFlag(AIEnabledModes.Buccaneer); }
            set { AIWorkingModes |= AIEnabledModes.Buccaneer; }
        }
        /*
        public bool isAssassinAvail = false;    //Is there an Assassin-enabled AI on this tech?
        public bool isAegisAvail = false;       //Is there an Aegis-enabled AI on this tech?

        public bool isProspectorAvail = false;  //Is there a Prospector-enabled AI on this tech?
        public bool isScrapperAvail = false;    //Is there a Scrapper-enabled AI on this tech?
        public bool isEnergizerAvail = false;   //Is there a Energizer-enabled AI on this tech?

        public bool isAviatorAvail = false;
        public bool isAstrotechAvail = false;
        public bool isBuccaneerAvail = false;
        */


        // Action Handlers


        // General AI Handling
        public bool Hibernate = false;      // Disable the AI to make way for Default AI

        /// <summary>
        /// 0 is off, 1 is enemy, 2 is obsticle
        /// </summary>
        public AIWeaponState ActiveAimState = AIWeaponState.Normal;

        public AIAlignment AIAlign = AIAlignment.Static;             // 0 is static, 1 is ally, 2 is enemy
        public AIWeaponState WeaponState = AIWeaponState.Normal;    // 0 is sleep, 1 is target, 2 is obsticle, 3 is mimic
        public bool UpdatePathfinding = false;       // Collision avoidence active this FixedUpdate frame?
        public bool UsingAirControls = false; // Use the not-VehicleAICore cores
        internal int FrustrationMeter = 0;  // tardiness buildup before we use our guns to remove obsticles
        internal float Urgency = 0;         // tardiness buildup before we just ignore obstructions
        internal float UrgencyOverload = 0; // builds up too much if our max speed was set too high

        /// <summary>
        /// Repairs requested?
        /// </summary>
        public bool PendingDamageCheck = true;
        /*
        private bool damageCheck = true;
        public bool PendingDamageCheck
        {
            get { return damageCheck; }
            set
            {
                DebugTAC_AI.Log("PendingDamageCheck set by: " + StackTraceUtility.ExtractStackTrace());
                damageCheck = value;
            }
        }*/

        public float DamageThreshold = 0;   // How much damage have we taken? (100 is total destruction)

        // Directional Handling
        /// <summary>
        /// IN WORLD SPACE
        /// Handles all Director/Operator decisions
        /// </summary>
        internal Vector3 lastDestinationOp => ControlOperator.lastDestination; // Where we drive to in the world
        /// <summary>
        /// IN WORLD SPACE
        /// Handles all Core decisions
        /// </summary>
        internal Vector3 lastDestinationCore => ControlCore.lastDestination;// Vector3.zero;    // Where we drive to in the world

        /*
        internal Vector3 lastDestination {
            get { return lastDestinationBugTest; }
            set {
                DebugTAC_AI.Log("lastDestination set by: " + StackTraceUtility.ExtractStackTrace());
                lastDestinationBugTest = value; 
            }
        }
        internal Vector3 lastDestinationBugTest = Vector3.zero;    // Where we drive to in the world
        */
        internal float lastOperatorRange { get { return _lastOperatorRange; } private set { _lastOperatorRange = value; } }
        private float _lastOperatorRange = 0;
        internal float lastCombatRange => _lastCombatRange;
        private float _lastCombatRange = 0;
        public float NextFindTargetTime = 0;      // Updates to wait before target swatching

        //AutoCollection
        internal bool hasAI = false;    // Has an active AI module
        internal bool dirtyAI = true;  // Update Player AI state if needed
        internal bool dirty = true;    // The Tech has new blocks attached recently

        internal float EstTopSped = 0;
        internal float recentSpeed = 1;
        internal int anchorAttempts = 0;
        internal float lastTechExtents = 1;
        internal float lastAuxVal = 0;
        public Visible lastPlayer;
        public Visible lastEnemyGet { get => lastEnemy; }
        internal Visible lastEnemy { get; set; } = null;
        public Visible lastLockOnTarget;
        public Transform Obst;
        internal Tank lastCloseAlly;
        // Non-Tech specific objective AI Handling
        internal float lastBaseExtremes = 10;
        /// <summary>
        /// Counts also as [recharge home, block rally]
        /// </summary>
        internal Tank theBase = null;
        /// <summary>
        /// Counts also as [loose block, target enemy, target to charge]
        /// </summary>
        internal Visible theResource = null;
        /// <summary>
        /// The EXACT transform that we want to close in on
        /// </summary>
        internal Transform lastBasePos;
        internal bool foundBase = false;
        internal bool foundGoal = false;

        // MultiTech AI Handling
        internal bool MTMimicHostAvail = false;
        internal bool MTLockedToTechBeam = false;
        internal Vector3 MTOffsetPos = Vector3.zero;
        internal Vector3 MTOffsetRot = Vector3.forward;
        internal Vector3 MTOffsetRotUp = Vector3.up;

        //  !!ADVANCED!!
        /// <summary>
        /// Use 3D navigation  (VehicleAICore)
        /// Normally this AI navigates on a 2D plane but this enables it to follow height.
        /// </summary>
        internal bool Attempt3DNavi = false;
        /// <summary>
        /// In WORLD space rotation, position relative from Tech mass center
        /// </summary>
        internal Vector3 Navi3DDirect = Vector3.zero;   // Forwards facing for 3D
        /// <summary>
        /// In WORLD space rotation, position relative from Tech mass center
        /// </summary>
        internal Vector3 Navi3DUp = Vector3.zero;       // Upwards direction for 3D
        public float GroundOffsetHeight = 35;           // flote above ground this dist
        internal Snapshot lastBuiltTech = null;

        //Timestep
        internal short DelayedAnchorClock = 0;
        internal short LightBoostFeatheringClock = 50;
        internal float RepairStepperClock = 0;
        internal short BeamTimeoutClock = 0;
        internal int WeaponDelayClock = 0;
        internal int actionPause { get; set; } = 0;              // when [val > 0], used to halt other actions 
        public int ActionPause 
        { 
            get => actionPause; 
            private set => actionPause = value; 
        }
        internal short unanchorCountdown = 0;         // aux warning for unanchor

        // Hierachy System:
        //   Operations --[ControlPre]-> Maintainer --[ControlPost]-> Core
        //Drive Direction Handlers
        // We need to tell the AI some important information:
        //  Target Destination
        //  Direction to point while heading to the target
        //  Driving direction in relation to driving to the target
        private EControlOperatorSet ControlOperator = EControlOperatorSet.Default;
        internal EControlOperatorSet GetDirectedControl()
        {
            return ControlOperator;
        }
        internal void SetDirectedControl(EControlOperatorSet cont)
        {
            ControlOperator = cont;
        }
        internal bool IsDirectedMoving => ControlOperator.DriveDest != EDriveDest.None;
        internal bool IsDirectedMovingToDest => ControlOperator.DriveDest > EDriveDest.FromLastDestination;
        internal bool IsDirectedMovingFromDest => ControlOperator.DriveDest == EDriveDest.FromLastDestination;

        /// <summary> Drive direction </summary>
        internal EDriveFacing DriveDirDirected => ControlOperator.DriveDir;
        /// <summary> Move to a dynamic target </summary>
        internal EDriveDest DriveDestDirected => ControlOperator.DriveDest;
        private EControlCoreSet ControlCore = EControlCoreSet.Default;
        internal string GetCoreControlString()
        {
            return ControlCore.ToString();
        }
        internal void SetCoreControl(EControlCoreSet cont)
        {
            ControlCore = cont;
        }

        /// <summary> Do we steer to target destination? </summary>
        internal bool DoSteerCore => ControlCore.DriveDir > EDriveFacing.Neutral;

        /// <summary> Drive AWAY from target </summary>
        internal bool AdviseAwayCore => ControlCore.DriveDest == EDriveDest.FromLastDestination;

        //Finals
        /// <summary> Leave at 0 to disable automatic spacing </summary>
        internal float MinimumRad = 0;              // Minimum radial spacing distance from destination
        internal float DriveVar { get; set; }  = 0;                // Forwards drive (-1, 1)

        internal bool Yield = false;                // Slow down and moderate top speed
        internal bool PivotOnly = false;            // Only aim at target
        /// <summary> SHOULD WE FIRE GUNS </summary>
        internal bool AttackEnemy { get; set; } = false;          // Enemy nearby?
        internal bool AvoidStuff = true;            // Try avoiding allies and obsticles
        /*
        internal bool AvoidStuff {
            get { return _AvoidStuff; }
            set {
                if (!value)
                    DebugTAC_AI.Log("AvoidStuff disabled by: " + StackTraceUtility.ExtractStackTrace().ToString());
                _AvoidStuff = value;
            }
        }*/

        internal bool FIRE_ALL { get; set; } = false;             // hold down tech's spacebar
        internal bool FullBoost = false;                // hold down boost button
        internal bool LightBoost = false;         // moderated booster pressing
        internal bool FirePROPS = false;            // hold down prop button
        internal bool ForceSetBeam = false;         // activate build beam
        internal bool ForceSetDrive = false;        // Force the drive (cab forwards!) to a specific set value
        internal bool CollectedTarget = false;      // this Tech's storage objective status (resources, blocks, energy)
        internal bool Retreat = false;              // ignore enemy position and follow intended destination (but still return fire)

        internal bool IsTryingToUnjam = false;      // Is this tech unjamming?
        internal bool JustUnanchored = false;       // flag to switch the AI back to enabled on unanchor
        internal bool PendingHeightCheck = false;   // queue a driving depth check for a naval tech
        internal float LowestPointOnTech = 0;       // the lowest point in relation to the tech's block-based center
        internal bool BoltsFired = false;

        /// <summary>
        /// ONLY SET EXTERNALLY BY NETWORKING
        /// </summary>
        public bool isRTSControlled { get; internal set; } = false;
        public bool RTSControlled
        {
            get { return isRTSControlled; }
            set
            {
                if (isRTSControlled != value)
                {
                    if (ManNetwork.IsNetworked)
                        NetworkHandler.TryBroadcastRTSControl(tank.netTech.netId.Value, value);
                    //DebugTAC_AI.Assert(true, "RTSControlled set to " + value);
                    isRTSControlled = value;
                    foreach (ModuleAIExtension AIEx in AIList)
                    {
                        AIEx.RTSActive = isRTSControlled;
                    }
                }
            }
        } // force the tech to be controlled by RTS
        public bool IsGoingToRTSDest => RTSDestInternal != RTSDisabled;
        public static IntVector3 RTSDisabled => AIGlobals.RTSDisabled;
        internal IntVector3 RTSDestination
        {
            get
            {
                if (RTSDestInternal == RTSDisabled)
                {
                    if (lastEnemyGet != null)
                        return new IntVector3(lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    else if (Obst != null)
                        return new IntVector3(Obst.position + Vector3.up);
                    return new IntVector3(tank.boundsCentreWorldNoCheck);
                }
                return new IntVector3(RTSDestInternal);
            }
            set
            {
                if (ManNetwork.IsNetworked)
                {
                    try
                    {
                        if (tank.netTech)
                            NetworkHandler.TryBroadcastRTSCommand(tank.netTech.netId.Value, RTSDestInternal);
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Error on RTSDestination Server update!!!\n" + e);
                    }
                }

                if (value == RTSDisabled)
                    RTSDestInternal = RTSDisabled;
                else if (DriverType == AIDriverType.Astronaut || DriverType == AIDriverType.Pilot)
                    RTSDestInternal = AIEPathing.OffsetFromGroundA(new IntVector3(value), this, AIGlobals.GroundOffsetRTSAir);
                else
                    RTSDestInternal = new IntVector3(value);
                foreach (ModuleAIExtension AIEx in AIList)
                {
                    AIEx.SaveRTS(this, RTSDestInternal);
                }
            }
        }
        private IntVector3 RTSDestInternal = RTSDisabled;

        public Vector3 DriveTargetLocation
        {
            get
            {
                if (RTSControlled && IsGoingToRTSDest)
                    return RTSDestination;
                else
                    return MovementController.GetDestination();
            }
        }

        /// <summary>
        /// ONLY CALL FROM NETWORK HANDLER AND NON-PLAYER AI!
        /// </summary>
        /// <param name="Pos"></param>
        internal void DirectRTSDest(Vector3 Pos)
        {
            RTSDestInternal = Pos;
            foreach (ModuleAIExtension AIEx in AIList)
            {
                AIEx.SaveRTS(this, RTSDestInternal);
            }
        }

        public bool OverrideAllControls = false;        // force the tech to be controlled by external means
        public bool PlayerAllowAutoAnchoring = false;   // Allow auto-anchor
        public bool ExpectAITampering = false;          // Set the AI back to Escort next update


        // ----------------------------  AI Cores  ---------------------------- 
        public IMovementAIController MovementController;
        public AIEAutoPather autoPather => (MovementController is AIControllerDefault def) ? def.Pathfinder : null;


        // ----------------------------  Awareness Subscriptions  ---------------------------- 
        public TankAIHelper Subscribe()
        {
            if (tank != null)
            {
                DebugTAC_AI.Assert("Game attempted to fire Subscribe for TankAIHelper twice.");
                return this;
            }
            AILimitSettings = new AISettingsLimit(this);
            tank = GetComponent<Tank>();
            Vector3 _ = tank.boundsCentreWorld;
            AIList = new List<ModuleAIExtension>();
            ManWorldTreadmill.inst.AddListener(this);
            tank.DamageEvent.Subscribe(OnHit);
            if (DriverType == AIDriverType.AutoSet)
                DriverType = AIECore.HandlingDetermine(tank, this);
            SetupDefaultMovementAIController();
            AIECore.AllHelpers.Add(this);
            Invoke("DelayedSubscribe", 0.1f);
            return this;
        }
        public void DelayedSubscribe()
        {
            try
            {
                lastTechExtents = (tank.blockBounds.size.magnitude / 2) + 2;
                if (lastTechExtents < 1)
                {
                    Debug.LogError("lastTechExtents is below 1: " + lastTechExtents);
                    lastTechExtents = 1;
                }
                maxBlockCount = tank.blockman.blockCount;
            }
            catch (Exception e)
            {
                Debug.Log("DelayedSubscribe - Error " + e);
            }
        }

        public void OnBlockAttached(TankBlock newBlock, Tank tank)
        {
            //DebugTAC_AI.Log("TACtical_AI: On Attach " + tank.name);
            EstTopSped = 1;
            //LastBuildClock = 0;
            PendingHeightCheck = true;
            dirty = true;
            dirtyAI = true;
            if (AIAlign == AIAlignment.Player)
            {
                try
                {
                    if (!tank.FirstUpdateAfterSpawn && !PendingDamageCheck && TechMemor)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Saved TechMemor for " + tank.name);
                        TechMemor.SaveTech();
                    }
                }
                catch { }
            }
            else if (AIAlign == AIAlignment.NonPlayer)
            {
                if (newBlock.GetComponent<ModulePacemaker>())
                    tank.Holders.SetHeartbeatSpeed(TechHolders.HeartbeatSpeed.Fast);
            }
        }
        public void OnBlockDetaching(TankBlock removedBlock, Tank tank)
        {
            EstTopSped = 1;
            recentSpeed = 1;
            PendingHeightCheck = true;
            PendingDamageCheck = true;
            dirty = true;
            if (AIAlign == AIAlignment.Player)
            {
                try
                {
                    removedBlock.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                }
                catch { }
                dirtyAI = true;
            }
        }
        internal void Recycled()
        {
            DropBlock();
            SuppressFiring(false);
            FinishedRepairEvent.EnsureNoSubscribers();
            maxBlockCount = 0;
            DamageThreshold = 0;
            PlayerAllowAutoAnchoring = false;
            isRTSControlled = false;
            DriverType = AIDriverType.AutoSet;
            DediAI = AIType.Escort;
            NextFindTargetTime = 0;
            RemoveBookmarkBuilder();
            if (TechMemor.IsNotNull())
            {
                TechMemor.Remove();
                TechMemor = null;
            }
            ResetAll(null);
        }

        public void SetRTSState(bool RTSEnabled)
        {
            RTSControlled = RTSEnabled;
            foreach (ModuleAIExtension AIEx in AIList)
            {
                if (AIEx)
                    AIEx.RTSActive = isRTSControlled;
                else
                    DebugTAC_AI.Log("TACtical_AI: NULL ModuleAIExtension IN " + tank.name);
            }
        }
        public void OnMoveWorldOrigin(IntVector3 move)
        {
            if (RTSDestInternal != RTSDisabled)
                RTSDestInternal += move;
            ControlOperator.SetLastDest(ControlOperator.lastDestination + move);

            if (MovementController != null)
                MovementController.OnMoveWorldOrigin(move);
        }
        /// <summary>
        /// ONLY CALL FOR NETWORK HANDLER!
        /// </summary>
        public void TrySetAITypeRemote(NetPlayer sender, AIType type, AIDriverType driver)
        {
            if (ManNetwork.IsNetworked)
            {
                if (sender == null)
                {
                    DebugTAC_AI.Log("TACtical_AI: Host changed AI");
                    //DebugTAC_AI.Log("TACtical_AI: Anonymous sender error");
                    //return;
                }
                if (sender.CurTech?.Team == tank.Team)
                {
                    if (type != AIType.Null)
                    {
                        OnSwitchAI(true);
                        DediAI = type;
                    }
                    if (driver != AIDriverType.Null)
                    {
                        OnSwitchAI(false);
                        if (DriverType == AIDriverType.Stationary && driver != AIDriverType.Stationary)
                        {
                            UnAnchor();
                            PlayerAllowAutoAnchoring = true;
                        }
                        else
                        {
                            TryAnchor();
                            PlayerAllowAutoAnchoring = false;
                        }
                        DriverType = driver;
                    }
                    RecalibrateMovementAIController();

                    //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(tank);
                    //overlay.Update();
                }
                else
                    DebugTAC_AI.Log("TACtical_AI: TrySetAITypeRemote - Invalid request received - player tried to change AI of Tech that wasn't theirs");
            }
            else
                DebugTAC_AI.Log("TACtical_AI: TrySetAITypeRemote - Invalid request received - Tried to change AI type when not connected to a server!? \n  The UI handles this automatically!!!\n" + StackTraceUtility.ExtractStackTrace());
        }
        
        public bool CanDoBlockReplacement()
        {
            foreach (ModuleAIExtension AIEx in AIList)
            {
                if (AIEx.SelfRepairAI)
                    return true;
            }
            return false;
        }

        public void ReValidateAI()
        {
            AutoAnchor = false;
            SecondAvoidence = false;// Should the AI avoid two techs at once?
            ChaseThreat = true;
            ActionPause = 0;

            if (tank.PlayerFocused)
            {   // player gets full control
                AIWorkingModes = AIEnabledModes.All;
            }
            else
            {
                AIWorkingModes = AIEnabledModes.None;
                /*
                isAegisAvail = false;
                isAssassinAvail = false;

                isProspectorAvail = false;
                isScrapperAvail = false;
                isEnergizerAvail = false;

                isAstrotechAvail = false;
                isAviatorAvail = false;
                isBuccaneerAvail = false;
                */
            }

            AIList.Clear();
            foreach (ModuleAIBot bot in tank.blockman.IterateBlockComponents<ModuleAIBot>())
            {
                var AIE = bot.gameObject.GetComponent<ModuleAIExtension>();
                if (AIE.IsNotNull())
                {
                    AIList.Add(AIE);
                }
            }
            DebugTAC_AI.Info("TACtical_AI: AI list for Tech " + tank.name + " has " + AIList.Count() + " entries");
            /// Gather the AI stats from all the AI modules on the Tech
            foreach (ModuleAIExtension AIEx in AIList)
            {
                // Combat
                if (AIEx.Aegis)
                    isAegisAvail = true;
                if (AIEx.Assault)
                    isAssassinAvail = true;

                // Collectors
                if (AIEx.Prospector)
                    isProspectorAvail = true;
                if (AIEx.Scrapper)
                    isScrapperAvail = true;
                if (AIEx.Energizer)
                    isEnergizerAvail = true;

                // Pilots
                if (AIEx.Aviator)
                    isAviatorAvail = true;
                if (AIEx.Buccaneer)
                    isBuccaneerAvail = true;
                if (AIEx.Astrotech)
                    isAstrotechAvail = true;

                // Auxillary Functions
                if (AIEx.AutoAnchor)
                    AutoAnchor = true;
                if (AIEx.AdvAvoidence)
                    SecondAvoidence = true;

                if (AIEx.RTSActive)
                {
                    SetRTSState(true);
                    RTSDestination = AIEx.GetRTSScenePos();
                }
            }
            AILimitSettings.Recalibrate();
            // REMOVE any AI states that have been removed!!!
            switch (DediAI)
            {
                case AIType.Aegis:
                    if (isAegisAvail) break;
                    DediAI = AIType.Escort;
                    break;
                case AIType.Assault:
                    if (isAssassinAvail) break;
                    DediAI = AIType.Escort;
                    break;
                case AIType.Prospector:
                    if (isProspectorAvail) break;
                    DediAI = AIType.Escort;
                    break;
                case AIType.Scrapper:
                    if (isScrapperAvail) break;
                    DediAI = AIType.Escort;
                    break;
                case AIType.Energizer:
                    if (isEnergizerAvail) break;
                    DediAI = AIType.Escort;
                    break;
                case AIType.Aviator:
                    if (isAviatorAvail) break;
                    DriverType = AIDriverType.Tank;
                    DediAI = AIType.Escort;
                    break;
                case AIType.Buccaneer:
                    if (isBuccaneerAvail) break;
                    DriverType = AIDriverType.Tank;
                    DediAI = AIType.Escort;
                    break;
                case AIType.Astrotech:
                    if (isAstrotechAvail) break;
                    DriverType = AIDriverType.Tank;
                    DediAI = AIType.Escort;
                    break;
            }

            if (DriverType == AIDriverType.AutoSet)
            {
                ExecuteAutoSetNoCalibrate();
            }
            else if (AIECore.ShouldBeStationary(tank, this))
                DriverType = AIDriverType.Stationary;

            RecalibrateMovementAIController();

            AttackMode = EWeapSetup.GetAttackStrat(tank, this);
        }
        /// <summary>
        /// Does not remove EnemyMind
        /// </summary>
        public void RefreshAI()
        {
            AvoidStuff = true;
            UsingAirControls = false;

            ReValidateAI();

            ProcessControl(Vector3.zero, Vector3.zero,Vector3.zero, false, false);
            tank.control.SetBeamControlState(false);
            tank.control.FireControl = false;

            if (CanDoBlockReplacement() || AIEBases.CheckIfTechNeedsToBeBuilt(this))
            {
                InsureTechMemor("RefreshAI", false);
            }
            else
            {
                if (TechMemor.IsNotNull())
                {
                    TechMemor.Remove();
                    TechMemor = null;
                }
            }
            try
            {
                tank.AttachEvent.Unsubscribe(OnBlockAttached);
                tank.DetachEvent.Unsubscribe(OnBlockDetaching);
            }
            catch { }

            try
            {
                tank.AttachEvent.Subscribe(OnBlockAttached);
                tank.DetachEvent.Subscribe(OnBlockDetaching);
            }
            catch { }
            AIEBases.SetupTechAutoConstruction(this);

            /*
            if (hasAnchorableAI)
            {
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is considered an Anchored Tech with the given conditions and will auto-anchor.");
                if (!tank.IsAnchored)
                {
                    TryAnchor();
                    ForceAllAIsToEscort();
                }
            }*/
        }
        public void ResetAll(Tank unused)
        {
            DebugTAC_AI.Assert(MovementController == null, "MovementController is null.  How is this possible?!");
            //DebugTAC_AI.Log("TACtical_AI: Resetting all for " + tank.name);
            maxBlockCount = tank.blockman.blockCount;
            SuppressFiring(false);
            lastAIType = AITreeType.AITypes.Idle;
            dirty = true;
            dirtyAI = true;
            PlayerAllowAutoAnchoring = !tank.IsAnchored;
            ExpectAITampering = false;
            GroundOffsetHeight = AIGlobals.GroundOffsetGeneralAir;
            Provoked = 0;
            ActionPause = 0;
            KeepEnemyFocus = false;

            AIAlign = AIAlignment.Static;
            Hibernate = false;
            PendingDamageCheck = true;
            ActiveAimState = 0;
            RepairStepperClock = 0;
            AvoidStuff = true;
            EstTopSped = 1;
            recentSpeed = 1;
            anchorAttempts = 0;
            DelayedAnchorClock = 0;
            foundBase = false;
            foundGoal = false;
            lastBasePos = null;
            lastPlayer = null;
            lastEnemy = null;
            lastLockOnTarget = null;
            lastCloseAlly = null;
            theBase = null;
            theResource = null;
            IsTryingToUnjam = false;
            JustUnanchored = false;
            DropBlock();
            isRTSControlled = false;
            RTSDestInternal = RTSDisabled;
            lastTargetGatherTime = 0;
            ChaseThreat = true;
            tank.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            World.ManPlayerRTS.ReleaseControl(this);
            var Funds = tank.gameObject.GetComponent<RLoadedBases.EnemyBaseFunder>();
            if (Funds.IsNotNull())
                Funds.OnRecycle(tank);
            var Mem = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
            if (Mem.IsNotNull() && !GetComponent<BookmarkBuilder>())
            {
                Mem.Remove();
                TechMemor = null;
            }
            var Mind = tank.gameObject.GetComponent<EnemyMind>();
            if (Mind.IsNotNull())
                Mind.SetForRemoval();
            var Select = tank.gameObject.GetComponent<SelectHalo>();
            if (Select.IsNotNull())
                Select.Remove();
            BookmarkBuilder[] Pnt = tank.gameObject.GetComponents<BookmarkBuilder>();
            if (Pnt.Count() > 1)
            {
                DestroyImmediate(Pnt[0]);
            }

            if (DriverType == AIDriverType.AutoSet)
                DriverType = AIECore.HandlingDetermine(tank, this);
            RecalibrateMovementAIController();

            ProcessControl(Vector3.zero, Vector3.zero, Vector3.zero, false, false);
            tank.control.SetBeamControlState(false);
            tank.control.FireControl = false;
            enabled = false;

            //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(tank);
            //overlay.Update();
        }

        public void SetupDefaultMovementAIController()
        {
            if (MovementController != null)
            {
                IMovementAIController controller = MovementController;
                MovementController = null;
                if (controller != null)
                {
                    controller.Recycle();
                }
            }
            UsingAirControls = false;
            MovementController = gameObject.GetOrAddComponent<AIControllerDefault>();
            MovementController.Initiate(tank, this, null);
        }
        /// <summary>
        /// Was previously: TestForFlyingAIRequirement
        /// </summary>
        /// <returns>True if the AI can fly</returns>
        public bool RecalibrateMovementAIController()
        {
            DebugTAC_AI.Info("RecalibrateMovementAIController for " + tank.name);
            UsingAirControls = false;
            var enemy = gameObject.GetComponent<EnemyMind>();
            if (AIAlign == AIAlignment.NonPlayer)
            {
                if (enemy.IsNotNull())
                {
                    if (enemy.StartedAnchored)
                    {
                        if (!(MovementController is AIControllerStatic))
                        {
                            IMovementAIController controller = MovementController;
                            MovementController = null;
                            if (controller != null)
                            {
                                controller.Recycle();
                            }
                        }
                        DriverType = AIDriverType.Stationary;
                        MovementController = gameObject.GetOrAddComponent<AIControllerStatic>();
                        MovementController.Initiate(tank, this, enemy);
                        return false;
                    }
                    if (enemy.EvilCommander == Enemy.EnemyHandling.Chopper || enemy.EvilCommander == Enemy.EnemyHandling.Airplane)
                    {
                        if (!(MovementController is AIControllerAir))
                        {
                            IMovementAIController controller = MovementController;
                            MovementController = null;
                            if (controller != null)
                            {
                                controller.Recycle();
                            }
                        }
                        MovementController = gameObject.GetOrAddComponent<AIControllerAir>();
                        MovementController.Initiate(tank, this, enemy);
                        UsingAirControls = true;
                        return true;
                    }
                }
                else
                    throw new Exception("RecalibrateMovementAIController for " + tank.name + " was NonPlayer but no EnemyMind present!");
            }
            else
            {
                if (DriverType == AIDriverType.Stationary)
                {
                    if (!(MovementController is AIControllerStatic))
                    {
                        IMovementAIController controller = MovementController;
                        MovementController = null;
                        if (controller != null)
                        {
                            controller.Recycle();
                        }
                    }
                    MovementController = gameObject.GetOrAddComponent<AIControllerStatic>();
                    MovementController.Initiate(tank, this);
                    return false;
                }
                else if (DriverType == AIDriverType.Pilot)
                {
                    if (!(MovementController is AIControllerAir))
                    {
                        IMovementAIController controller = MovementController;
                        MovementController = null;
                        if (controller != null)
                        {
                            controller.Recycle();
                        }
                    }
                    MovementController = gameObject.GetOrAddComponent<AIControllerAir>();
                    MovementController.Initiate(tank, this);
                    UsingAirControls = true;
                    return true;
                }
            }
            if (!(MovementController is AIControllerDefault))
            {
                IMovementAIController controller = MovementController;
                MovementController = null;
                if (controller != null)
                {
                    controller.Recycle();
                }
            }
            MovementController = gameObject.GetOrAddComponent<AIControllerDefault>();
            MovementController.Initiate(tank, this, enemy);
            return false;
        }

        public void ExecuteAutoSet()
        {
            ExecuteAutoSetNoCalibrate();
            RecalibrateMovementAIController();
        }
        public void ExecuteAutoSetNoCalibrate()
        {
            DriverType = AIECore.HandlingDetermine(tank, this);
            switch (DriverType)
            {
                case AIDriverType.Astronaut:
                    if (!isAstrotechAvail)
                        DriverType = AIDriverType.Tank;
                    break;
                case AIDriverType.Pilot:
                    if (!isAviatorAvail)
                        DriverType = AIDriverType.Tank;
                    break;
                case AIDriverType.Sailor:
                    if (!isBuccaneerAvail)
                        DriverType = AIDriverType.Tank;
                    break;
                case AIDriverType.AutoSet:
                    DriverType = AIDriverType.Tank;
                    break;
                case AIDriverType.Tank:
                case AIDriverType.Stationary:
                    break;
                default:
                    DebugTAC_AI.LogError("TACtical_AI: Encountered illegal AIDriverType on Allied AI Driver HandlingDetermine!");
                    break;
            }
        }

        /// <summary>
        /// React when hit by an attack from another Tech. 
        /// Must be un-subbed and resubbed when switching to and from enemy
        /// </summary>
        /// <param name="dingus"></param>
        public void OnHit(ManDamage.DamageInfo dingus)
        {
            if (dingus.SourceTank && dingus.Damage > AIGlobals.DamageAlertThreshold)
            {
                if (SetPursuit(dingus.SourceTank.visible))
                {
                    if (tank.IsAnchored)
                    {
                        // Execute remote orders to allied units - Attack that threat!
                        AIECore.RequestFocusFirePlayer(tank, lastEnemyGet, RequestSeverity.AllHandsOnDeck);
                    }
                    else
                    {
                        // Execute remote orders to allied units - Attack that threat!
                        switch (DediAI)
                        {
                            case AIType.Prospector:
                            case AIType.Scrapper:
                            case AIType.Energizer:
                                AIECore.RequestFocusFirePlayer(tank, lastEnemyGet, RequestSeverity.Warn);
                                break;
                            default:
                                AIECore.RequestFocusFirePlayer(tank, lastEnemyGet, RequestSeverity.ThinkMcFly);
                                break;
                        }
                    }
                }
                Provoked = AIGlobals.ProvokeTime;
                FIRE_ALL = true;
                if (ManPlayerRTS.PlayerIsInRTS)
                {
                    if (tank.IsAnchored)
                    {
                        PlayerRTSUI.RTSDamageWarnings(0.5f, 0.25f);
                        ManEnemySiege.BigF5broningWarning("Base is Under Attack", true);
                    }
                    else if (tank.PlayerFocused)
                    {
                        PlayerRTSUI.RTSDamageWarnings(1.5f, 0.75f);
                        ManEnemySiege.BigF5broningWarning("You are under attack", false);
                    }
                    else
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.RadarOn);
                    }
                }
            }
        }
        public void OnSwitchAI(bool resetRTSstate)
        {
            AvoidStuff = true;
            EstTopSped = 1;
            foundBase = false;
            foundGoal = false;
            lastBasePos = null;
            lastPlayer = null;
            lastCloseAlly = null;
            theBase = null;
            IsTryingToUnjam = false;
            JustUnanchored = false;
            ChaseThreat = true;
            ActionPause = 0;
            DropBlock();
            if (resetRTSstate)
            {
                isRTSControlled = false;
                foreach (ModuleAIExtension AIEx in AIList)
                {
                    AIEx.RTSActive = isRTSControlled;
                }
                tank.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            }
            //World.PlayerRTSControl.ReleaseControl(this);
        }
        public void ForceAllAIsToEscort(bool Do, bool RebuildAlignmentDelayed)
        {
            //Needed to return AI mode back to Escort on unanchor as unanchoring causes it to go to idle
            try
            {
                if (Do)
                {
                    if (ManNetwork.IsNetworked && tank.netTech.IsNotNull())
                    {
                        Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.SetAIMode, new SetAIModeMessage
                        {
                            m_AIAction = AITreeType.AITypes.Escort
                        }, tank.netTech.netId);
                    }
                    else
                    {
                        tank.AI.SetBehaviorType(AITreeType.AITypes.Escort);
                        lastAIType = AITreeType.AITypes.Escort;
                    }
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes type))
                        DebugTAC_AI.Info("TACtical_AI: AI type is " + type.ToString());
                }
                else
                {
                    if (ManNetwork.IsNetworked && tank.netTech.IsNotNull())
                    {
                        Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.SetAIMode, new SetAIModeMessage
                        {
                            m_AIAction = AITreeType.AITypes.Idle
                        }, tank.netTech.netId);
                    }
                    else
                    {
                        tank.AI.SetBehaviorType(AITreeType.AITypes.Idle);
                        lastAIType = AITreeType.AITypes.Idle;
                    }
                }
                if (!RebuildAlignmentDelayed)
                    ForceRebuildAlignment();
                else
                    dirtyAI = true;
            }
            catch { }
        }


        // ----------------------------  GUI Formatter  ---------------------------- 
        public string GetActionStatus(out bool cantDo)
        {
            cantDo = false;
            if (tank.IsPlayer)
            {
                if (!ManPlayerRTS.autopilotPlayer)
                    return "Autopilot Disabled";
            }
            else if (AIAlign != AIAlignment.NonPlayer)
            {
                if (!ActuallyWorks)
                    return "No AI Modules";
                else if (!SetToActive)
                {
                    if (AIAlign != AIAlignment.NonPlayer)
                        return "Idle (Off)";
                }
            }
            if (Retreat)
            {
                return "Retreat!";
            }
            string output = "At Destination";
            try
            {
                if (RTSControlled)
                {
                    GetActionOperatorsPositional(ref output, ref cantDo);
                    return output;
                }

                if (AIAlign == AIAlignment.NonPlayer)
                {
                    GetActionOperatorsNonPlayer(ref output, ref cantDo);
                }
                else
                {
                    GetActionOperatorsAllied(ref output, ref cantDo);
                }
            }
            catch
            {
                output = "Loading...";
            }
            return output;
        }
        public void GetActionOperatorsPositional(ref string output, ref bool cantDo)
        {
            if (tank.IsAnchored)
            {
                if (lastEnemyGet)
                    output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                else
                    output = "Stationary";
                return;
            }
            switch (DriverType)
            {
                case AIDriverType.Astronaut:
                    if (lastEnemyGet)
                        output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                    {
                        if (WeaponState == AIWeaponState.Obsticle)
                            output = "Removing Obstruction";
                        else
                        {
                            switch (ControlOperator.DriveDest)
                            {
                                case EDriveDest.FromLastDestination:
                                    output = "Moving from destination";
                                    break;
                                case EDriveDest.ToLastDestination:
                                    output = "Moving to destination";
                                    break;
                                case EDriveDest.ToBase:
                                    output = "Moving to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                    break;
                                case EDriveDest.ToMine:
                                    output = "Moving to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                    break;
                                default:
                                    output = "Arrived at destination";
                                    break;
                            }
                        }
                    }
                    break;
                case AIDriverType.Pilot:
                    if (lastEnemyGet)
                        output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                    {
                        if (MovementController is AIControllerAir air)
                        {
                            if (air.Grounded)
                            {
                                cantDo = true;
                                output = "Unable to takeoff";
                            }
                            else
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Crashed";
                                else
                                {
                                    switch (ControlOperator.DriveDest)
                                    {
                                        case EDriveDest.FromLastDestination:
                                            output = "Flying from destination";
                                            break;
                                        case EDriveDest.ToLastDestination:
                                            output = "Flying to destination";
                                            break;
                                        case EDriveDest.ToBase:
                                            output = "Flying to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                            break;
                                        case EDriveDest.ToMine:
                                            output = "Flying to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                            break;
                                        default:
                                            output = "Arrived at destination";
                                            break;
                                    }
                                }
                            }
                        }
                        else
                            output = "Unhandled error in switch";
                    }
                    break;
                case AIDriverType.Sailor:
                    if (lastEnemyGet)
                        output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                    {
                        if (WeaponState == AIWeaponState.Obsticle)
                            output = "Stuck & Beached";
                        else
                        {
                            switch (ControlOperator.DriveDest)
                            {
                                case EDriveDest.FromLastDestination:
                                    output = "Sailing from destination";
                                    break;
                                case EDriveDest.ToLastDestination:
                                    output = "Sailing to destination";
                                    break;
                                case EDriveDest.ToBase:
                                    output = "Sailing to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                    break;
                                case EDriveDest.ToMine:
                                    output = "Sailing to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                    break;
                                default:
                                    output = "Arrived at destination";
                                    break;
                            }
                        }
                    }
                    break;
                case AIDriverType.Stationary:
                    if (lastEnemyGet)
                        output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                    {
                        output = "Stationary Base";
                    }
                    break;
                default:
                    if (lastEnemyGet)
                        output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                    {
                        if (WeaponState == AIWeaponState.Obsticle)
                            output = "Stuck on an obsticle";
                        else
                        {
                            switch (ControlOperator.DriveDest)
                            {
                                case EDriveDest.FromLastDestination:
                                    output = "Driving from destination";
                                    break;
                                case EDriveDest.ToLastDestination:
                                    output = "Driving to destination";
                                    break;
                                case EDriveDest.ToBase:
                                    output = "Driving to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                    break;
                                case EDriveDest.ToMine:
                                    output = "Driving to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                    break;
                                default:
                                    output = "Arrived at destination";
                                    break;
                            }
                        }
                    }
                    break;
            }
        }
        public void GetActionOperatorsAllied(ref string output, ref bool cantDo)
        {
            switch (DediAI)
            {
                case AIType.Aegis:
                    if (lastEnemyGet)
                        output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else if (theResource)
                        output = "Protecting " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                    else
                        output = "Looking for Ally";
                    break;
                case AIType.Assault:
                    if (DriveDestDirected == EDriveDest.ToBase)
                    {
                        if (theBase)
                        {
                            if (WeaponState == AIWeaponState.Obsticle)
                                output = "Removing Obstruction";
                            else if (recentSpeed > 8)
                                output = "Returning to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                            else if (GetEnergyPercent() <= 0.95f)
                                output = "Recharging batteries...";
                            else
                                output = "Scouting for Enemies";
                        }
                        else
                            output = "Cannot find base!";
                    }
                    else
                    {
                        if (theResource)
                        {
                            if (lastEnemyGet)
                            {
                                output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                            }
                            else
                                output = "Moving out to enemy";
                        }
                        else
                            output = "Scouting for Enemies";
                    }
                    break;
                case AIType.Energizer:
                    if (DriveDestDirected == EDriveDest.ToBase)
                    {
                        if (theBase)
                        {
                            if (WeaponState == AIWeaponState.Obsticle)
                                output = "Removing Obstruction";
                            else if (recentSpeed > 8)
                                output = "Returning to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                            else if (GetEnergyPercent() <= 0.95f)
                                output = "Recharging batteries...";
                            else
                                output = "Waiting for charge request...";
                        }
                        else
                        {
                            cantDo = true;
                            output = "No Charging Base!";
                        }
                    }
                    else
                    {
                        if (theResource)
                        {
                            if (WeaponState == AIWeaponState.Obsticle)
                                output = "Removing Obstruction";
                            else if (recentSpeed > 8)
                                output = "Requester " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                            else
                                output = "Charging Ally";
                        }
                        else
                            output = "Waiting for charge request...";
                    }
                    break;
                case AIType.Escort:
                    switch (DriverType)
                    {
                        case AIDriverType.Astronaut:
                            if (lastEnemyGet)
                                output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                            else
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else
                                {
                                    switch (ControlOperator.DriveDest)
                                    {
                                        case EDriveDest.FromLastDestination:
                                            output = "Moving from Player";
                                            break;
                                        case EDriveDest.ToLastDestination:
                                            output = "Moving to Player";
                                            break;
                                        case EDriveDest.ToBase:
                                            output = "Moving to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                            break;
                                        case EDriveDest.ToMine:
                                            output = "Moving to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                            break;
                                        default:
                                            output = "Floating Escort";
                                            break;
                                    }
                                }
                            }
                            break;
                        case AIDriverType.Pilot:
                            if (lastEnemyGet)
                                output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                            else
                            {
                                if (MovementController is AIControllerAir air)
                                {
                                    if (air.Grounded)
                                    {
                                        cantDo = true;
                                        output = "Can't takeoff, Too damaged / parts missing";
                                    }
                                    else
                                    {
                                        if (WeaponState == AIWeaponState.Obsticle)
                                            output = "Crashed";
                                        else
                                        {
                                            switch (ControlOperator.DriveDest)
                                            {
                                                case EDriveDest.FromLastDestination:
                                                    output = "Flying from Player";
                                                    break;
                                                case EDriveDest.ToLastDestination:
                                                    output = "Flying to Player";
                                                    break;
                                                case EDriveDest.ToBase:
                                                    output = "Flying to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                                    break;
                                                case EDriveDest.ToMine:
                                                    output = "Flying to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                                    break;
                                                default:
                                                    output = "Flying Escort";
                                                    break;
                                            }
                                        }
                                    }
                                }
                                else
                                    output = "Unhandled error in switch";
                            }
                            break;
                        case AIDriverType.Sailor:
                            if (lastEnemyGet)
                                output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                            else
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Stuck & Beached";
                                else
                                {
                                    switch (ControlOperator.DriveDest)
                                    {
                                        case EDriveDest.FromLastDestination:
                                            output = "Sailing from Player";
                                            break;
                                        case EDriveDest.ToLastDestination:
                                            output = "Sailing to Player";
                                            break;
                                        case EDriveDest.ToBase:
                                            output = "Sailing to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                            break;
                                        case EDriveDest.ToMine:
                                            output = "Sailing to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                            break;
                                        default:
                                            output = "Sailing Escort";
                                            break;
                                    }
                                }
                            }
                            break;
                        default:
                            if (lastEnemyGet)
                                output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                            else
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Stuck on an obsticle";
                                else
                                {
                                    switch (ControlOperator.DriveDest)
                                    {
                                        case EDriveDest.FromLastDestination:
                                            output = "Driving from Player";
                                            break;
                                        case EDriveDest.ToLastDestination:
                                            output = "Driving to Player";
                                            break;
                                        case EDriveDest.ToBase:
                                            output = "Driving to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                            break;
                                        case EDriveDest.ToMine:
                                            output = "Driving to " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
                                            break;
                                        default:
                                            output = "Land Escort";
                                            break;
                                    }
                                }
                            }
                            break;
                    }
                    break;
                case AIType.MTMimic:
                    if (!AllMT)
                    {
                        if ((bool)lastCloseAlly)
                            output = "Copying Player";
                        else
                        {
                            cantDo = true;
                            output = "Searching for Player";
                        }
                    }
                    else
                    {
                        if ((bool)lastCloseAlly)
                            output = "Copying " + (lastCloseAlly.name.NullOrEmpty() ? "unknown" : lastCloseAlly.name);
                        else
                        {
                            cantDo = true;
                            output = "Searching for Ally";
                        }
                    }
                    break;
                case AIType.MTStatic:
                    if (AttackEnemy)
                        output = "Weapons Active";
                    else
                        output = "Weapons Primed";
                    break;
                case AIType.MTTurret:
                    if ((bool)lastEnemyGet)
                    {
                        if (AttackEnemy)
                            output = "Shooting at " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                        else
                            output = "Aiming at " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    }
                    else
                        output = "Face the Danger";
                    break;
                case AIType.Prospector:
                    if (DriveDestDirected == EDriveDest.ToBase)
                    {
                        if ((bool)theBase)
                        {
                            if (WeaponState == AIWeaponState.Obsticle)
                                output = "Removing Obstruction";
                            else if (recentSpeed > 8)
                                output = "Returning to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                            else
                                output = "Unloading resources...";
                        }
                        else
                        {
                            cantDo = true;
                            output = "No Receiver Base!";
                        }
                    }
                    else
                    {
                        if ((bool)theResource?.resdisp)
                        {
                            IEnumerable<ChunkTypes> CT = theResource.resdisp.AllDispensableItems();
                            if (recentSpeed > 8)
                            {
                                if (!CT.Any())
                                    output = "Going to remove rocks";
                                else
                                    output = "Going to dig " + StringLookup.GetItemName(theResource.m_ItemType); //theResource.name;
                                                                                                                 //StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                            }
                            else
                            {
                                if (!CT.Any())
                                    output = "Clearing rocks";
                                else
                                    output = "Mining " + StringLookup.GetItemName(theResource.m_ItemType);//theResource.name;
                                                                                                          //output = "Mining " + StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                            }
                        }
                        else
                        {
                            if (ActionPause > 0)
                            {
                                output = "Reversing for " + ActionPause + "...";
                            }
                            else
                                output = "No resources in " + (JobSearchRange + AIGlobals.FindItemScanRangeExtension) + " meters";
                        }
                    }
                    break;
                case AIType.Scrapper:
                    if (DriveDestDirected == EDriveDest.ToBase)
                    {
                        if ((bool)theBase)
                        {
                            if (WeaponState == AIWeaponState.Obsticle)
                                output = "Removing Obstruction";
                            else if (recentSpeed > 8)
                                output = "Returning to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                            else
                                output = "Unloading blocks...";
                        }
                        else
                        {
                            cantDo = true;
                            output = "No Collection Base!";
                        }
                    }
                    else
                    {
                        if ((bool)theResource?.block)
                        {
                            BlockTypes BT = theResource.block.BlockType;
                            if (WeaponState == AIWeaponState.Obsticle)
                                output = "Removing Obstruction";
                            else if (recentSpeed > 8)
                            {
                                if (BT == BlockTypes.GSOAIController_111)
                                    output = "Fetching unknown block";
                                else
                                    output = "Fetching " + StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Block, (int)BT));
                            }
                            else
                            {
                                if (BT == BlockTypes.GSOAIController_111)
                                    output = "Grabbing unknown block";
                                else
                                    output = "Grabbing " + StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Block, (int)BT));
                            }
                        }
                        else
                            output = "No blocks in " + (JobSearchRange + AIGlobals.FindItemScanRangeExtension) + " meters";
                    }
                    break;
            }
        }
        public void GetActionOperatorsNonPlayer(ref string output, ref bool cantDo)
        {
            var mind = GetComponent<EnemyMind>();
            /*
            if (PursuingTarget)
            {
                output = "Getting revenge for comrade";
                return;
            }*/
            switch (mind.CommanderMind)
            {
                case EnemyAttitude.Homing:
                    if (lastEnemyGet)
                        GetActionOperatorsNonPlayerCombat(mind, ref output, ref cantDo);
                    else
                    {
                        output = "Looking for trouble (Homing)!";
                    }
                    break;
                case EnemyAttitude.Miner:
                    if (lastEnemyGet)
                        GetActionOperatorsNonPlayerCombat(mind, ref output, ref cantDo);
                    else
                    {
                        if (DriveDestDirected == EDriveDest.ToBase)
                        {
                            if ((bool)theBase)
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else if (recentSpeed > 8)
                                    output = "Returning to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                else
                                    output = "Unloading resources...";
                            }
                            else
                            {
                                cantDo = true;
                                output = "No Receiver Base!";
                            }
                        }
                        else
                        {
                            if ((bool)theResource?.resdisp)
                            {
                                IEnumerable<ChunkTypes> CT = theResource.resdisp.AllDispensableItems();
                                if (recentSpeed > 8)
                                {
                                    if (CT.Any())
                                        output = "Going to remove rocks";
                                    else
                                        output = "Going to dig " + StringLookup.GetItemName(theResource.m_ItemType);//theResource.name;
                                                                                                                    //StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                                }
                                else
                                {
                                    if (CT.Any())
                                        output = "Clearing rocks";
                                    else
                                        output = "Mining " + StringLookup.GetItemName(theResource.m_ItemType);//theResource.name;
                                                                                                              //output = "Mining " + StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                                }
                            }
                            else
                                output = "No resources in " + (JobSearchRange + AIGlobals.FindItemScanRangeExtension) + " meters";
                        }
                    }
                    break;
                case EnemyAttitude.Junker:
                    if (lastEnemyGet)
                        GetActionOperatorsNonPlayerCombat(mind, ref output, ref cantDo);
                    else
                    {
                        if (DriveDestDirected == EDriveDest.ToBase)
                        {
                            if ((bool)theBase)
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else if (recentSpeed > 8)
                                    output = "Returning to " + (theBase.name.NullOrEmpty() ? "unknown" : theBase.name);
                                else
                                    output = "Unloading blocks...";
                            }
                            else
                            {
                                cantDo = true;
                                output = "No Collection Base!";
                            }
                        }
                        else
                        {
                            if ((bool)theResource?.block)
                            {
                                BlockTypes BT = theResource.block.BlockType;
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else if (recentSpeed > 8)
                                {
                                    if (BT == BlockTypes.GSOAIController_111)
                                        output = "Fetching unknown block";
                                    else
                                        output = "Fetching " + StringLookup.GetItemName(theResource.m_ItemType);
                                }
                                else
                                {
                                    if (BT == BlockTypes.GSOAIController_111)
                                        output = "Grabbing unknown block";
                                    else
                                        output = "Grabbing " + StringLookup.GetItemName(theResource.m_ItemType);
                                }
                            }
                            else
                                output = "No blocks in " + (JobSearchRange + AIGlobals.FindItemScanRangeExtension) + " meters";
                        }
                    }
                    break;
                case EnemyAttitude.OnRails:
                    if (lastEnemyGet)
                        output = "Enemy in range = " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                    {
                        output = "Script Commanded";
                    }
                    break;
                case EnemyAttitude.NPCBaseHost:
                    if (lastEnemyGet)
                        GetActionOperatorsNonPlayerCombat(mind, ref output, ref cantDo);
                    else
                    {
                        output = "Managing Base";
                    }
                    break;
                case EnemyAttitude.Boss:
                    if (lastEnemyGet)
                        GetActionOperatorsNonPlayerCombat(mind, ref output, ref cantDo);
                    else
                    {
                        output = "Plotting next attack...";
                    }
                    break;
                case EnemyAttitude.Invader:
                    if (lastEnemyGet)
                        GetActionOperatorsNonPlayerCombat(mind, ref output, ref cantDo);
                    else
                    {
                        output = "Invading";
                    }
                    break;
                default:
                    if (lastEnemyGet)
                        GetActionOperatorsNonPlayerCombat(mind, ref output, ref cantDo);
                    else
                    {
                        GetActionOperatorsPositional(ref output, ref cantDo);
                    }
                    break;
            }
        }
        public void GetActionOperatorsNonPlayerCombat(EnemyMind mind, ref string output, ref bool cantDo)
        {
            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    if (ControlCore.DriveDest == EDriveDest.ToLastDestination)
                        output = "Moving to " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                        output = "Running from " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    break;
                case EAttackMode.Ranged:
                    if (ControlCore.DriveDest == EDriveDest.ToLastDestination)
                        output = "Closing in on Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                        output = "Spacing from Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    break;
                default:
                    if (ControlCore.DriveDest == EDriveDest.ToLastDestination)
                        output = "Moving to Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                        output = "Moving from Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    break;
            }
        }


        // ----------------------------  Information Handling  ---------------------------- 
        private int maxBlockCount = 1;
        private int lastBlockCount = 1;
        public bool CanDetectHealth()
        {
            return true;//TechMemor || AdvancedAI; 
        }
        public float GetHealth()
        {
            return GetHealthPercent() * (maxBlockCount * 10);
        }
        public float GetHealthMax()
        {
            return maxBlockCount * 10;
        }
        /// <summary>
        /// 100 for max, 0 for pretty much destroyed
        /// </summary>
        /// <returns></returns>
        public float GetHealth100()
        {
            if (!CanDetectHealth())
                return 100;
            return 100 - DamageThreshold;
        }
        public float GetHealthPercent()
        {
            if (!CanDetectHealth())
                return 1;
            return (100 - DamageThreshold) / 100;
        }
        public float GetSpeed()
        {
            if (tank.rbody.IsNull())
                return 0; // Slow/Stopped
            if (IsTryingToUnjam)
                return 0;
            if (Attempt3DNavi || MovementController is AIControllerAir)
            {
                return SafeVelocity.magnitude;
            }
            else
            {
                if (!(bool)tank.rootBlockTrans)
                    return 0; // There's some sort of error in play
                return tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).z;
            }
        }
        public bool CanStoreEnergy()
        {
            var energy = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            return energy.storageTotal > 1;
        }
        public float GetEnergy()
        {
            var energy = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            if (energy.storageTotal < 1)
                return 0;

            return energy.storageTotal - energy.spareCapacity;
        }
        public float GetEnergyMax()
        {
            var energy = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            if (energy.storageTotal < 1)
                return 1;

            return energy.storageTotal;
        }
        public float GetEnergyPercent()
        {
            var energy = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            if (energy.storageTotal < 1)
                return 0;

            return (energy.storageTotal - energy.spareCapacity) / energy.storageTotal;
        }
        public bool IsTechMoving(float minSpeed)
        {
            if (tank.rbody.IsNull())
                return true; // Stationary techs do not get the panic message
            if (IsTryingToUnjam)
                return false;
            if (Attempt3DNavi || MovementController is AIControllerAir)
            {
                return SafeVelocity.sqrMagnitude > minSpeed * minSpeed;
            }
            else
            {
                if (!(bool)tank.rootBlockTrans)
                    return false;
                return tank.rootBlockTrans.InverseTransformDirection(SafeVelocity).z > minSpeed || Mathf.Abs(tank.control.CurState.m_InputMovement.z) < 0.5f;
            }
        }
        public bool IsTechMovingActual(float minSpeed)
        {
            if (tank.rbody.IsNull())
                return true; // Stationary techs do not get the panic message
            if (IsTryingToUnjam)
                return false;
            if (Attempt3DNavi || MovementController is AIControllerAir)
            {
                return SafeVelocity.sqrMagnitude > minSpeed * minSpeed;
            }
            else
            {
                if (!(bool)tank.rootBlockTrans)
                    return false;
                return tank.rootBlockTrans.InverseTransformDirection(SafeVelocity).z > minSpeed;
            }
        }
        public bool HasAnchorAI()
        {
            foreach (var AIEx in AIList)
            {
                if (AIEx.GetComponent<ModuleAnchor>())
                {
                    if (ManWorld.inst.GetTerrainHeight(AIEx.transform.position, out float height))
                        if (AIEx.GetComponent<ModuleAnchor>().HeightOffGroundForMaxAnchor() > height)
                            return true;
                }
            }
            return false;
        }
        public Visible GetPlayerTech()
        {
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    /*
                    DebugTAC_AI.Log("TACtical_AI: The Tech's Team: " + tank.Team + " | RTS Mode: " + RTSControlled);
                    foreach (Tank thatTech in ManNetwork.inst.GetAllPlayerTechs())
                    {
                        DebugTAC_AI.Log("TACtical_AI: " + thatTech.name + " | of " + thatTech.netTech.Team);
                    }*/
                    foreach (Tank thatTech in ManNetwork.inst.GetAllPlayerTechs())
                    {
                        if (thatTech.Team == tank.Team)
                        {
                            return thatTech.visible;
                        }
                    }
                }
                catch { }
            }
            else
            {
                try
                {
                    return Singleton.playerTank.visible;
                }
                catch { }
            }
            return lastPlayer;
        }
        public void GetLowestPointOnTech()
        {
            float lowest = 0;
            List<TankBlock> lowBlocks = tank.blockman.GetLowestBlocks();
            Quaternion forward = Quaternion.LookRotation(tank.rootBlockTrans.forward, tank.rootBlockTrans.up);
            for (int step = 0; step < lowBlocks.Count; step++)
            {
                TankBlock block = lowBlocks[step];
                IntVector3[] filledCells = block.filledCells;
                foreach (IntVector3 intVector in filledCells)
                {
                    Vector3 Locvec = block.cachedLocalPosition + block.cachedLocalRotation * intVector;
                    Vector3 cellPosLocal = (forward * Locvec) - tank.rootBlockTrans.InverseTransformPoint(tank.boundsCentreWorldNoCheck);
                    if (cellPosLocal.y < lowest)
                    {
                        lowest = cellPosLocal.y;
                    }
                }
            }
            DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  lowest point set " + lowest);
            LowestPointOnTech = lowest;
        }
        public bool TestIsLowestPointOnTech(TankBlock block)
        {
            bool isTrue = false;
            if (block == null)
                return false;
            Quaternion forward = Quaternion.LookRotation(tank.rootBlockTrans.forward, tank.rootBlockTrans.up);
            IntVector3[] filledCells = block.filledCells;
            foreach (IntVector3 intVector in filledCells)
            {
                Vector3 Locvec = block.cachedLocalPosition + block.cachedLocalRotation * intVector;
                Vector3 cellPosLocal = (forward * Locvec) - tank.rootBlockTrans.InverseTransformPoint(tank.boundsCentreWorldNoCheck);
                if (cellPosLocal.y < LowestPointOnTech)
                {
                    LowestPointOnTech = cellPosLocal.y;
                    isTrue = true;
                }
            }
            if (isTrue)
            {
                DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  lowest point set " + LowestPointOnTech);
            }
            return isTrue;
        }


        // ----------------------------  Primary Operations  ---------------------------- 
        /// <summary>
        /// Controls the Tech.  Main interface for ALL AI Tech Controls(excluding Neutral)
        /// </summary>
        /// <param name="thisControl"></param>
        public bool ControlTech(TankControl thisControl)
        {
            enabled = true;
            if (ManNetwork.IsNetworked)
            {
                if (ManNetwork.IsHost)
                {
                    if (tank.TechIsMPPlayerControlled())
                    {
                        if (Singleton.playerTank == tank && RTSControlled)
                        {
                            UpdateTechControl(thisControl);
                            return true;
                        }
                    }
                    else
                    {
                        if (tank.FirstUpdateAfterSpawn)
                        {
                            if (!tank.IsAnchored && tank.GetComponent<RequestAnchored>())
                            {
                                TryReallyAnchor(true);
                            }
                            // let the icon update
                        }
                        else if (AIAlign == AIAlignment.Player)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI Valid!");
                            //DebugTAC_AI.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                            //tankAIHelp.AIState && 
                            if (JustUnanchored)
                            {
                                ForceAllAIsToEscort(true, false);
                                JustUnanchored = false;
                            }
                            else if (SetToActive)
                            {
                                //DebugTAC_AI.Log("TACtical_AI: Running BetterAI");
                                //DebugTAC_AI.Log("TACtical_AI: Patched Tank ExecuteControl(TankAIHelper)");
                                UpdateTechControl(thisControl);
                                return true;
                            }
                        }
                        else if (OverrideAllControls)
                        {   // override EVERYTHING
                            UnAnchor();
                            thisControl.BoostControlJets = true;
                            return true;
                            //return false;
                        }
                        else if (KickStart.enablePainMode && AIAlign == AIAlignment.NonPlayer)
                        {
                            if (!Hibernate)
                            {
                                UpdateTechControl(thisControl);
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    if (KickStart.AllowStrategicAI && ManPlayerRTS.autopilotPlayer && Singleton.playerTank == tank && ManPlayerRTS.PlayerIsInRTS)
                    {
                        if (tank.PlayerFocused)
                        {
                            if (!RTSControlled)
                                SetRTSState(true);
                            if (RTSDestInternal == RTSDisabled)
                                RTSDestination = tank.boundsCentreWorldNoCheck;
                            UpdateTechControl(thisControl);
                            return true;
                        }
                    }
                }
            }
            else
            {
                if (!tank.PlayerFocused || (KickStart.AllowStrategicAI && ManPlayerRTS.autopilotPlayer && ManPlayerRTS.PlayerIsInRTS))
                {
                    if (tank.FirstUpdateAfterSpawn)
                    {
                        if (tank.GetComponent<RequestAnchored>())
                        {
                            TryReallyAnchor();
                        }
                        // let the icon update
                    }
                    else if (AIAlign == AIAlignment.Player)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: AI Valid!");
                        //DebugTAC_AI.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                        if (JustUnanchored)
                        {
                            ForceAllAIsToEscort(true, false);
                            JustUnanchored = false;
                        }
                        else if (tank.PlayerFocused)
                        {
                            //SetRTSState(true);
                            UpdateTechControl(thisControl);
                            return true;
                        }
                        else if (SetToActive)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: Running BetterAI");
                            //DebugTAC_AI.Log("TACtical_AI: Patched Tank ExecuteControl(TankAIHelper)");
                            UpdateTechControl(thisControl);
                            return true;
                        }
                    }
                    else if (OverrideAllControls)
                    {   // override EVERYTHING
                        UnAnchor();
                        thisControl.BoostControlJets = true;
                        return true;
                    }
                    else if (KickStart.enablePainMode && AIAlign == AIAlignment.NonPlayer)
                    {
                        if (!Hibernate)
                        {
                            UpdateTechControl(thisControl);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private void UpdateTechControl(TankControl thisControl)
        {   // The interface method for actually handling the tank - note that this fires at a different rate
            if (OverrideAllControls)
                return;
            CurHeight = -500;

            if (MovementController is null)
            {
                DebugTAC_AI.Log("NULL MOVEMENT CONTROLLER");
            }

            AIEBeam.BeamMaintainer(thisControl, this, tank);
            if (UpdatePathfinding)
            {
                //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Fired CollisionAvoidUpdate!");
                try
                {
                    AIEWeapons.WeaponDirector(thisControl, this, tank);

                    if (!IsTryingToUnjam)
                    {
                        EControlCoreSet coreCont = new EControlCoreSet(ControlOperator);
                        if (RTSControlled)
                            MovementController.DriveDirectorRTS(ref coreCont);
                        else
                            MovementController.DriveDirector(ref coreCont);
                        //coreCont.MergePrevCommands(ControlDirected);
                        SetCoreControl(coreCont);
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Potential error in DriveDirector (or WeaponDirector)! " + e);
                }

                UpdatePathfinding = false; // incase they fall out of sync
            }
            try
            {
                if (NotInBeam)
                {
                    AIEWeapons.WeaponMaintainer(thisControl, this, tank);
                    MovementController.DriveMaintainer(thisControl, ref ControlCore);
                }
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Potential error in DriveMaintainer (or WeaponMaintainer)!");
            }
        }

        private void RunStaticOperations()
        {
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
                TryRepairStatic();
        }
        private void RunAlliedOperations()
        {
            var aI = tank.AI;

            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
                TryRepairAllied();
            BoltsFired = false;

            if (!tank.IsAnchored && lastAIType == AITreeType.AITypes.Idle && ExpectAITampering)
            {
                ForceAllAIsToEscort(true, false);
                ExpectAITampering = false;
            }
            UpdateCalcCrashAvoidenceSphere();

            if (tank.PlayerFocused)
            {
                //updateCA = true;
                if (ActionPause > 0)
                    ActionPause -= KickStart.AIClockPeriod;
                if (KickStart.AllowStrategicAI)
                {
                    Attempt3DNavi = false;
#if DEBUG
                        if (ManPlayerRTS.PlayerIsInRTS && ManPlayerRTS.DevCamLock == DebugCameraLock.LockTechToCam)
                        {
                            if (tank.rbody)
                            {
                                tank.rbody.MovePosition(Singleton.cameraTrans.position + (Vector3.up * 75));
                                return;
                            }
                        }
#endif
                    if (ManPlayerRTS.autopilotPlayer)
                    {
                        DetermineCombat();
                        if (RTSControlled)
                        {
                            //DebugTAC_AI.Log("RTS PLAYER");
                            RunRTSNavi(true);
                        }
                        else
                            OpsController.Execute();
                    }
                }
                return;
            }
            else
                UpdateTargetCombatFocus();
            if (!aI.TryGetCurrentAIType(out lastAIType))
            {
                lastAIType = AITreeType.AITypes.Idle;
                return;
            }
            if (SetToActive)
            {
                //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Fired DelayedUpdate!");
                Attempt3DNavi = false;

                //updateCA = true;
                if (ActionPause > 0)
                    ActionPause -= KickStart.AIClockPeriod;
                //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  current mode " + DediAI.ToString());

                DetermineCombat();

                if (RTSControlled)
                {   //Overrides the Allied Operations for RTS Use
                    RunRTSNavi();
                }
                else
                    OpsController.Execute();
            }
        }
        /// <summary>
        /// Hands control over to Enemy.RCore
        /// </summary>
        private void RunEnemyOperations(bool light = false)
        {
            //BEGIN THE PAIN!
            //updateCA = true;
            if (ActionPause > 0)
                ActionPause -= KickStart.AIClockPeriod;
            UpdateCalcCrashAvoidenceSphere();
            DetermineCombatEnemy();
            if (light)
                RCore.BeEvilLight(this, tank);
            else
            {
                RCore.BeEvil(this, tank);
            }
        }

        private void RunRTSNavi(bool isPlayerTech = false)
        {   // Alternative Operator for RTS
            if (!KickStart.AllowStrategicAI)
                return;

            //ProceedToObjective = true;
            EControlOperatorSet direct = GetDirectedControl();
            if (DriverType == AIDriverType.Pilot)
            {
                lastOperatorRange = (DodgeSphereCenter - lastDestinationCore).magnitude;
                Attempt3DNavi = true;
                BGeneral.ResetValues(this, ref direct);
                AvoidStuff = true;

                float range = (MaxObjectiveRange * 4) + lastTechExtents;
                // The range is nearly quadrupled here due to dogfighting conditions
                direct.DriveDest = EDriveDest.ToLastDestination;
                Yield = AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius) ||
                    AIEPathing.ObstructionAwarenessTerrain(DodgeSphereCenter, this, DodgeSphereRadius);

                if (tank.wheelGrounded)
                {
                    if (!AutoHandleObstruction(ref direct, lastOperatorRange, true, true))
                        SettleDown();
                }
                else
                {
                    if (lastOperatorRange < (lastTechExtents * 2) + 5)
                    {

                    }
                    else if (lastOperatorRange > range)
                    {   // Far behind, must catch up
                        FullBoost = true; // boost in forwards direction towards objective
                    }
                    else
                    {

                    }
                }
            }
            else
            {
                float prevDist = lastOperatorRange;
                GetDistanceFromTask(lastDestinationCore);
                bool needsToSlowDown = IsOrbiting(lastDestinationCore, lastOperatorRange - prevDist);

                Attempt3DNavi = DriverType == AIDriverType.Astronaut;
                BGeneral.ResetValues(this, ref direct);
                AvoidStuff = true;
                Yield = needsToSlowDown || AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius)
                    || AIEPathing.ObstructionAwarenessSetPieceAny(DodgeSphereCenter, this, DodgeSphereRadius);

                direct.DriveToFacingTowards();
                if (lastOperatorRange < (lastTechExtents * 2) + 32 && !ManPlayerRTS.HasMovementQueue(this))
                {
                    //Things are going smoothly
                    SettleDown();
                    ForceSetDrive = true;
                    DriveVar = 0;
                    PivotOnly = true;
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  RTS - resting");
                    if (DelayedAnchorClock < 15)
                        DelayedAnchorClock++;
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": " + AutoAnchor + " | " + PlayerAllowAnchoring + " | " + (tank.Anchors.NumPossibleAnchors >= 1) + " | " + (DelayedAnchorClock >= 15) + " | " + !DANGER);
                    if (CanAutoAnchor)
                    {
                        if (!tank.IsAnchored && anchorAttempts <= AIGlobals.AlliedAnchorAttempts)
                        {
                            DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Setting camp!");
                            TryAnchor();
                            anchorAttempts++;
                        }
                    }
                }
                else
                {   // Time to go!
                    anchorAttempts = 0;
                    DelayedAnchorClock = 0;
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  RTS - Moving");
                    if (unanchorCountdown > 0)
                        unanchorCountdown--;
                    if (AutoAnchor && PlayerAllowAutoAnchoring && !isPlayerTech && tank.Anchors.NumPossibleAnchors >= 1)
                    {
                        if (tank.Anchors.NumIsAnchored > 0)
                        {
                            unanchorCountdown = 15;
                            UnAnchor();
                        }
                    }
                    if (!AutoAnchor && !isPlayerTech && tank.IsAnchored)
                    {
                        BGeneral.RTSCombat(this, tank);
                        SetDirectedControl(direct);
                        return;
                    }
                    if (!IsTechMoving(EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                    {   //OBSTRUCTION MANAGEMENT
                        //Urgency += KickStart.AIClockPeriod / 2f;
                        //if (Urgency > 15)
                        //{
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  DOOR STUCK");
                        TryHandleObstruction(true, lastOperatorRange, false, true, ref direct);
                        //}
                    }
                    else
                    {
                        //var val = tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).z;
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Output " + val + " | TopSpeed/2 " + (EstTopSped / 2) + " | TopSpeed/4 " + (EstTopSped / 4));
                        //Things are going smoothly
                        ForceSetDrive = true;
                        float driveVal = Mathf.Min(1, lastOperatorRange / 10);
                        DriveVar = driveVal;
                        SettleDown();
                    }
                }
            }
            SetDirectedControl(direct);
            BGeneral.RTSCombat(this, tank);
        }
        internal void RunRTSNaviEnemy(EnemyMind mind)
        {   // Alternative Operator for RTS
            //DebugTAC_AI.Log("RunRTSNaviEnemy - " + tank.name);
            if (!KickStart.AllowStrategicAI)
            {
                RTSControlled = false;
                return;
            }

            EControlOperatorSet direct = GetDirectedControl();
            BGeneral.ResetValues(this, ref direct);
            if (mind.EvilCommander == EnemyHandling.Airplane)
            {
                lastOperatorRange = (DodgeSphereCenter - lastDestinationCore).magnitude;
                Attempt3DNavi = true;
                AvoidStuff = true;

                float range = (MaxObjectiveRange * 4) + lastTechExtents;
                // The range is quadrupled here due to dogfighting conditions
                direct.DriveDest = EDriveDest.ToLastDestination;
                Yield = AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius) ||
                    AIEPathing.ObstructionAwarenessTerrain(DodgeSphereCenter, this, DodgeSphereRadius);

                if (tank.wheelGrounded)
                {
                    if (!AutoHandleObstruction(ref direct, lastOperatorRange, true, true))
                        SettleDown();
                }
                else
                {
                    if (lastOperatorRange < (lastTechExtents * 2) + 5)
                    {

                    }
                    else if (lastOperatorRange > range)
                    {   // Far behind, must catch up
                        FullBoost = true; // boost in forwards direction towards objective
                    }
                    else
                    {

                    }
                }
            }
            else
            {
                float prevDist = lastOperatorRange;
                GetDistanceFromTask(lastDestinationCore);
                bool needsToSlowDown = IsOrbiting(lastDestinationCore, lastOperatorRange - prevDist);

                Attempt3DNavi = mind.EvilCommander == EnemyHandling.Starship;
                AvoidStuff = true;
                bool AutoAnchor = mind.CommanderSmarts >= EnemySmarts.Meh;
                Yield = needsToSlowDown || AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius)
                    || AIEPathing.ObstructionAwarenessSetPieceAny(DodgeSphereCenter, this, DodgeSphereRadius);

                if (lastOperatorRange < (lastTechExtents * 2) + 32 && !ManPlayerRTS.HasMovementQueue(this))
                {
                    //Things are going smoothly
                    SettleDown();
                    ForceSetDrive = true;
                    DriveVar = 0;
                    PivotOnly = true;
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  RTS - resting");
                    if (DelayedAnchorClock < 15)
                        DelayedAnchorClock++;
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": " + AutoAnchor + " | " + PlayerAllowAnchoring + " | " + (tank.Anchors.NumPossibleAnchors >= 1) + " | " + (DelayedAnchorClock >= 15) + " | " + !DANGER);
                    if (AutoAnchor && !AttackEnemy && tank.Anchors.NumPossibleAnchors >= 1
                        && DelayedAnchorClock >= 15 && CanAnchorSafely)
                    {
                        if (!tank.IsAnchored && anchorAttempts <= AIGlobals.AlliedAnchorAttempts)
                        {
                            DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Setting camp!");
                            TryAnchor();
                            anchorAttempts++;
                        }
                    }
                }
                else
                {   // Time to go!
                    anchorAttempts = 0;
                    DelayedAnchorClock = 0;
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  RTS - Moving");
                    if (unanchorCountdown > 0)
                        unanchorCountdown--;
                    if (AutoAnchor && tank.Anchors.NumPossibleAnchors >= 1)
                    {
                        if (tank.Anchors.NumIsAnchored > 0)
                        {
                            unanchorCountdown = 15;
                            UnAnchor();
                        }
                    }
                    if (!AutoAnchor && tank.IsAnchored)
                    {
                        RGeneral.RTSCombat(this, tank, mind);
                        SetDirectedControl(direct);
                        return;
                    }
                    if (!IsTechMovingActual(EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    {   //OBSTRUCTION MANAGEMENT
                        TryHandleObstruction(true, lastOperatorRange, false, true, ref direct);
                    }
                    else
                    {
                        //var val = tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).z;
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Output " + val + " | TopSpeed/2 " + (EstTopSped / 2) + " | TopSpeed/4 " + (EstTopSped / 4));
                        //Things are going smoothly
                        ForceSetDrive = true;
                        float driveVal = Mathf.Min(1, lastOperatorRange / 10);
                        DriveVar = driveVal;
                        SettleDown();
                    }
                }
            }
            SetDirectedControl(direct);
            RGeneral.RTSCombat(this, tank, mind);
        }

        // Lets the AI do the planning
        /// <summary>
        /// Processing center for AI brains
        /// </summary>
        // OnPreUpdate -> Directors -> Operations -> OnPostUpdate
        public void OnPreUpdate()
        {
            if (MovementController == null)
            {
                DebugTAC_AI.Assert(true, "MOVEMENT CONTROLLER IS NULL");
                SetupDefaultMovementAIController();
                RecalibrateMovementAIController();
            }
            recentSpeed = GetSpeed();
            if (recentSpeed < 1)
                recentSpeed = 1;
            UpdateLastTechExtentsIfNeeded();
            RebuildAlignment();
            UpdateCollectors();
        }
        public void OnPostUpdate()
        {
            ManageAILockOn();
            UpdateBlockHold();
            ShowDebugThisFrame();
        }
        private void UpdateLastTechExtentsIfNeeded()
        {//Handler for the improved AI, gets the job done.
            try
            {
                if (dirty)
                {
                    dirty = false;
                    tank.blockman.CheckRecalcBlockBounds();
                    lastTechExtents = (tank.blockBounds.size.magnitude / 2) + 2;
                    if (lastTechExtents < 1)
                    {
                        Debug.LogError("lastTechExtents is below 1: " + lastTechExtents);
                        lastTechExtents = 1;
                    }
                    if (!PendingDamageCheck)
                        maxBlockCount = tank.blockman.blockCount;
                }
            }
            catch (Exception e)
            {
                if (!updateErrored)
                {
                    DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN UpdateLastTechExtentsIfNeeded()!!! - " + e);
                    updateErrored = true;
                }
            }
        }

        // AI Actions
        // if (!OverrideAllControls), then { Directors -> Operations }
        internal void OnUpdateHostAIDirectors()
        {
            try
            {
                switch (AIAlign)
                {
                    case AIAlignment.Player: // Player-Controlled techs
                        UpdatePathfinding = true;
                        break;
                    case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                        if (KickStart.enablePainMode)
                        {
                            if (!Hibernate)
                            {
                                UpdatePathfinding = true;
                            }
                        }
                        break;
                    default:// Static tech
                        DriveVar = 0;
                        break;
                }
            }
            catch (Exception e)
            {
                if (!updateErrored)
                {
                    DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN UpdateHostAIActions!!! - " + e);
                    updateErrored = true;
                }
            }
        }
        internal void OnUpdateHostAIOperations()
        {
            try
            {
                switch (AIAlign)
                {
                    case AIAlignment.Player: // Player-Controlled techs
                        CheckEnemyErrorState();
                        if (IsTryingToUnjam)
                        {
                            TryHandleObstruction(true, lastOperatorRange, false, true, ref ControlOperator);
                        }
                        else
                            RunAlliedOperations();
                        if (EstTopSped < recentSpeed)
                            EstTopSped = recentSpeed;
                        break;
                    case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                        if (KickStart.enablePainMode)
                        {
                            if (Hibernate)
                            {
                                CheckEnemyErrorState();
                                if (IsTryingToUnjam)
                                {
                                    TryHandleObstruction(true, lastOperatorRange, false, true, ref ControlOperator);
                                    var mind = GetComponent<EnemyMind>();
                                    if (mind)
                                        RCore.ScarePlayer(mind, this, tank);
                                }
                                else
                                    RunEnemyOperations(true);
                                if (EstTopSped < recentSpeed)
                                    EstTopSped = recentSpeed;
                            }
                            else
                            {
                                CheckEnemyErrorState();
                                if (IsTryingToUnjam)
                                {
                                    TryHandleObstruction(true, lastOperatorRange, false, true, ref ControlOperator);
                                    var mind = GetComponent<EnemyMind>();
                                    if (mind)
                                        RCore.ScarePlayer(mind, this, tank);
                                }
                                else
                                    RunEnemyOperations();
                                if (EstTopSped < recentSpeed)
                                    EstTopSped = recentSpeed;
                            }
                        }
                        break;
                    default:// Static tech
                        DriveVar = 0;
                        RunStaticOperations();
                        break;
                }
            }
            catch (Exception e)
            {
                if (!updateErrored)
                {
                    DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN UpdateHostAIActions!!! - " + e);
                    updateErrored = true;
                }
            }
        }

        /// <summary>
        /// MULTIPLAYER AI NON-HOST
        /// </summary>
        internal void OnUpdateClientAIDirectors()
        {
            switch (AIAlign)
            {
                case AIAlignment.Static:// Static tech
                    DriveVar = 0;
                    break;
                case AIAlignment.Player: // Player-Controlled techs
                    UpdatePathfinding = true;
                    break;
                case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                    if (!Hibernate)
                    {
                        UpdatePathfinding = true;
                    }
                    break;
            }
        }
        internal void OnUpdateClientAIOperations()
        {
            switch (AIAlign)
            {
                case AIAlignment.Static:// Static tech
                    DriveVar = 0;
                    break;
                case AIAlignment.Player: // Player-Controlled techs
                    if (EstTopSped < recentSpeed)
                        EstTopSped = recentSpeed;
                    break;
                case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                    if (!Hibernate)
                    {
                        if (EstTopSped < recentSpeed)
                            EstTopSped = recentSpeed;
                    }
                    break;
            }
        }

        /// <summary>
        /// CALL when we change ANYTHING in the tech's AI.
        /// </summary>
        internal void OnTechTeamChange()
        {
            dirtyAI = true;
            PlayerAllowAutoAnchoring = !tank.IsAnchored;
        }
        internal void ForceRebuildAlignment()
        {
            dirtyAI = true;
            RebuildAlignment();
        }
        private void RebuildAlignment()
        {
            if (dirtyAI)
            {
                dirtyAI = false;
                var aI = tank.AI;
                hasAI = aI.CheckAIAvailable();

                lastLockOnTarget = null;
                SuppressFiring(false);
                try
                {
                    TankAIManager.UpdateTechTeam(tank);
                    if (ManNetwork.IsNetworked)
                    {   // Multiplayer
                        if (!ManNetwork.IsHost)// && tank != Singleton.playerTank)
                        {   // Is Client
                            if (ManSpawn.IsPlayerTeam(tank.Team))
                            {   //MP
                                if (hasAI || (ManPlayerRTS.PlayerIsInRTS && tank.PlayerFocused))
                                {
                                    //Player-Allied AI
                                    if (AIAlign != AIAlignment.Player)
                                    {
                                        ResetAll(tank);
                                        RemoveEnemyMatters();
                                        AIAlign = AIAlignment.Player;
                                        RefreshAI();
                                        DebugTAC_AI.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go! (NonHostClient)");
                                    }
                                }
                                else
                                {   // Static tech
                                    DriveVar = 0;
                                    if (AIAlign != AIAlignment.PlayerNoAI)
                                    {   // Reset and ready for static tech
                                        DebugTAC_AI.Log("TACtical_AI: PlayerNoAI Tech " + tank.name + ": reset (NonHostClient)");
                                        ResetAll(tank);
                                        RemoveEnemyMatters();
                                        AIAlign = AIAlignment.PlayerNoAI;
                                    }
                                }
                            }
                            else if (!tank.IsNeutral())
                            {
                                //Enemy AI
                                if (AIAlign != AIAlignment.NonPlayer)
                                {
                                    ResetAll(tank);
                                    AIAlign = AIAlignment.NonPlayer;
                                    DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech! (NonHostClient)");
                                    RCore.RandomizeBrain(this, tank);
                                }
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIAlign != AIAlignment.Static)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset (NonHostClient)");
                                    ResetAll(tank);
                                    RemoveEnemyMatters();
                                    AIAlign = AIAlignment.Static;
                                }
                            }
                            return;
                        }
                        else if (dirty)
                        {
                            dirty = false;
                            tank.netTech.SaveTechData();
                        }
                        if (ManSpawn.IsPlayerTeam(tank.Team))
                        {   //MP
                            if (hasAI || (ManPlayerRTS.PlayerIsInRTS && tank.PlayerFocused))
                            {
                                //Player-Allied AI
                                if (AIAlign != AIAlignment.Player)
                                {
                                    ResetAll(tank);
                                    RemoveEnemyMatters();
                                    AIAlign = AIAlignment.Player;
                                    RefreshAI();
                                    if ((bool)TechMemor && !GetComponent<BookmarkBuilder>())
                                        TechMemor.SaveTech();
                                    DebugTAC_AI.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go!");
                                }
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIAlign != AIAlignment.PlayerNoAI)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log("TACtical_AI: PlayerNoAI Tech " + tank.name + ": reset");
                                    ResetAll(tank);
                                    RemoveEnemyMatters();
                                    AIEBases.SetupBookmarkBuilder(this);
                                    AIAlign = AIAlignment.PlayerNoAI;
                                }
                            }
                        }
                        else if (KickStart.enablePainMode && !tank.IsNeutral())
                        {
                            //Enemy AI
                            if (AIAlign != AIAlignment.NonPlayer)
                            {
                                ResetAll(tank);
                                AIAlign = AIAlignment.NonPlayer;
                                Enemy.RCore.RandomizeBrain(this, tank);
                                DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech!");
                            }
                            if (GetComponent<EnemyMind>())
                                SuppressFiring(!GetComponent<EnemyMind>().AttackAny);
                        }
                        else
                        {   // Static tech
                            DriveVar = 0;
                            if (AIAlign != AIAlignment.Static)
                            {   // Reset and ready for static tech
                                DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset");
                                ResetAll(tank);
                                RemoveEnemyMatters();
                                AIEBases.SetupBookmarkBuilder(this);
                                AIAlign = AIAlignment.Static;
                            }
                        }
                    }
                    else
                    {
                        if (ManSpawn.IsPlayerTeam(tank.Team))//aI.CheckAIAvailable()
                        {   //MP is somewhat supported
                            if (hasAI || (World.ManPlayerRTS.PlayerIsInRTS && tank.PlayerFocused))
                            {
                                //Player-Allied AI
                                if (AIAlign != AIAlignment.Player)
                                {
                                    ResetAll(tank);
                                    RemoveEnemyMatters();
                                    AIAlign = AIAlignment.Player;
                                    RefreshAI();
                                    if ((bool)TechMemor && !GetComponent<BookmarkBuilder>())
                                        TechMemor.SaveTech();
                                    DebugTAC_AI.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go!");
                                }
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIAlign != AIAlignment.PlayerNoAI)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log("TACtical_AI: PlayerNoAI Tech " + tank.name + ": reset");
                                    ResetAll(tank);
                                    RemoveEnemyMatters();
                                    AIEBases.SetupBookmarkBuilder(this);
                                    AIAlign = AIAlignment.PlayerNoAI;
                                }
                            }
                        }
                        else if (KickStart.enablePainMode && !tank.IsNeutral())
                        {   //MP is NOT supported!
                            //Enemy AI
                            if (AIAlign != AIAlignment.NonPlayer)
                            {
                                ResetAll(tank);
                                DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech!");
                                AIAlign = AIAlignment.NonPlayer;
                                Enemy.RCore.RandomizeBrain(this, tank);
                            }
                        }
                        else
                        {   // Static tech
                            DriveVar = 0;
                            if (AIAlign != AIAlignment.Static)
                            {   // Reset and ready for static tech
                                DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset");
                                ResetAll(tank);
                                RemoveEnemyMatters();
                                AIEBases.SetupBookmarkBuilder(this);
                                AIAlign = AIAlignment.Static;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!updateErrored)
                    {
                        DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN RebuildAlignment!!! - " + e);
                        updateErrored = true;
                    }
                }
            }
        }


        // ----------------------------  Pathfinding Processor  ---------------------------- 
        public void ProcessControl(Vector3 DriveVal, Vector3 TurnVal, Vector3 Throttle, bool props, bool jets)
        {
            tank.control.CollectMovementInput(DriveVal, TurnVal, Throttle, props, jets);
        }
        internal float DodgeStrength
        {
            get
            {
                if (UsingAirControls)
                    return AIGlobals.AirborneDodgeStrengthMultiplier * lastOperatorRange;
                return AIGlobals.DefaultDodgeStrengthMultiplier * lastOperatorRange;
            }
        }
        public Vector3 DodgeSphereCenter { get; private set; } = Vector3.zero;
        /// <summary> World Rotation </summary>
        public Vector3 SafeVelocity { get; private set; } = Vector3.zero;
        public float DodgeSphereRadius { get; private set; } = 1;
        private float CurHeight = 0;
        /// <summary>
        /// AboveGround
        /// </summary>
        public float GetFrameHeight()
        {
            if (CurHeight == -500)
            {
                //ManWorld.inst.GetTerrainHeight(tank.boundsCentreWorldNoCheck, out float height);
                //CurHeight = height;
                CurHeight = AIEPathMapper.GetAltitudeCached(tank.boundsCentreWorldNoCheck);
            }
            return CurHeight;
        }
        /// <summary>
        /// Gets the opposite direction of the target tech for offset avoidence, accounting for size
        /// </summary>
        /// <param name="targetToAvoid"></param>
        /// <returns></returns>
        public Vector3 GetOtherDir(Tank targetToAvoid)
        {
            //What actually does the avoidence
            //DebugTAC_AI.Log("TACtical_AI: GetOtherDir");
            Vector3 inputOffset = tank.boundsCentreWorldNoCheck - targetToAvoid.boundsCentreWorldNoCheck;
            float inputSpacing = targetToAvoid.GetCheapBounds() + lastTechExtents + DodgeStrength;
            Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.boundsCentreWorldNoCheck;
            return Final;
        }
        /// <summary>
        /// [For reversed inputs] Gets the direction of the target tech for offset avoidence, accounting for size
        /// </summary>
        /// <param name="targetToAvoid"></param>
        /// <returns></returns>
        public Vector3 GetDir(Tank targetToAvoid)
        {
            //What actually does the avoidence
            //DebugTAC_AI.Log("TACtical_AI: GetDir");
            Vector3 inputOffset = tank.boundsCentreWorldNoCheck - targetToAvoid.boundsCentreWorldNoCheck;
            float inputSpacing = targetToAvoid.GetCheapBounds() + lastTechExtents + DodgeStrength;
            Vector3 Final = -(inputOffset.normalized * inputSpacing) + tank.boundsCentreWorldNoCheck;
            return Final;
        }

        public static List<KeyValuePair<Vector3, float>> posWeights = new List<KeyValuePair<Vector3, float>>();
        public Vector3 AvoidAssist(Vector3 targetIn, bool AvoidStatic = true)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            //IsLikelyJammed = false;
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                //TankAIManager.FetchAllAllies();
                return targetIn;
            }
            try
            {
                bool obst;
                Tank lastCloseAlly;
                float lastAllyDist;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                posWeights.Clear();
                if (SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));
                        }
                        else
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                        }
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                else
                {
                    lastCloseAlly = AIEPathing.ClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                    //DebugTAC_AI.Log("TACtical_AI: Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log("TACtical_AI: Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        //IsLikelyJammed = true;
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                        Vector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                if (posWeights.Count == 0)
                    return targetIn;
                Vector3 posCombined = targetIn;
                float totalWeight = 1;
                foreach (var item in posWeights)
                {
                    totalWeight += item.Value;
                    posCombined += item.Key * item.Value;
                }
                return posCombined / totalWeight;
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Crash on Avoid " + e);
                return targetIn;
            }
        }
        /// <summary>
        /// When moving AWAY from target
        /// </summary>
        /// <param name="targetIn"></param>
        /// <returns></returns>
        public Vector3 AvoidAssistInv(Vector3 targetIn, bool AvoidStatic = true)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target - REVERSED
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                //TankAIManager.FetchAllAllies();
                return targetIn;
            }
            try
            {
                bool obst;
                Tank lastCloseAlly;
                float lastAllyDist;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                posWeights.Clear();
                if (SecondAvoidence && AlliesAlt.Count() > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffsetInv(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDir(lastCloseAlly) + GetDir(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));

                        }
                        else
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffsetInv(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDir(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                        }
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffsetInv(tank, this, AvoidStatic, out obst, AdvancedAI);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                else
                {
                    lastCloseAlly = AIEPathing.ClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                    //DebugTAC_AI.Log("TACtical_AI: Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log("TACtical_AI: Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        //IsLikelyJammed = true;
                        Vector3 obstOff = AIEPathing.ObstDodgeOffsetInv(tank, this, AvoidStatic, out obst, AdvancedAI);
                        Vector3 ProccessedVal = GetDir(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffsetInv(tank, this, AvoidStatic, out obst, AdvancedAI);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                if (posWeights.Count == 0)
                    return targetIn;
                Vector3 posCombined = targetIn;
                float totalWeight = 1;
                foreach (var item in posWeights)
                {
                    totalWeight += item.Value;
                    posCombined += item.Key * item.Value;
                }
                return posCombined / totalWeight;
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Crash on Avoid " + e);
                return targetIn;
            }
        }
        public Vector3 AvoidAssistPrecise(Vector3 targetIn, bool AvoidStatic = true, bool IgnoreDestructable = false)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            //  MORE DEMANDING THAN THE ABOVE!
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                //TankAIManager.FetchAllAllies();
                return targetIn;
            }
            try
            {
                bool obst;
                Tank lastCloseAlly;
                float lastAllyDist;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                posWeights.Clear();
                if (SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI, IgnoreDestructable);
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));
                        }
                        else
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI, IgnoreDestructable);
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                        }
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                else
                {
                    lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                    //DebugTAC_AI.Log("TACtical_AI: Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log("TACtical_AI: Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI, IgnoreDestructable);
                        Vector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI, IgnoreDestructable);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                if (posWeights.Count == 0)
                    return targetIn;
                Vector3 posCombined = targetIn;
                float totalWeight = 1;
                foreach (var item in posWeights)
                {
                    totalWeight += item.Value;
                    posCombined += item.Key * item.Value;
                }
                return posCombined / totalWeight;
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on Avoid Allied" + e);
                return targetIn;
            }
        }
        public Vector3 AvoidAssistPrediction(Vector3 targetIn, float Foresight)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            //IsLikelyJammed = false;
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                //TankAIManager.FetchAllAllies();
                return targetIn;
            }
            try
            {
                bool obst;
                Tank lastCloseAlly;
                float lastAllyDist;
                Vector3 posOffset = tank.boundsCentreWorldNoCheck + (SafeVelocity * Foresight);
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                posWeights.Clear();
                if (SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAlly(AlliesAlt, posOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));
                        }
                        else
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                        }
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                else
                {
                    lastCloseAlly = AIEPathing.ClosestAlly(AlliesAlt, posOffset, out lastAllyDist, tank);
                    //DebugTAC_AI.Log("TACtical_AI: Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log("TACtical_AI: Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        //IsLikelyJammed = true;
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                        Vector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 1));
                    }
                    else
                    {
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 2));
                    }
                }
                if (posWeights.Count == 0)
                    return targetIn;
                Vector3 posCombined = targetIn;
                float totalWeight = 1;
                foreach (var item in posWeights)
                {
                    totalWeight += item.Value;
                    posCombined += item.Key * item.Value;
                }
                return posCombined / totalWeight;
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Crash on Avoid " + e);
                return targetIn;
            }
        }
        /// <summary>
        /// An airborne version of the Player AI pathfinding which handles obstructions
        /// </summary>
        /// <param name="targetIn"></param>
        /// <param name="predictionOffset"></param>
        /// <returns></returns>
        public Vector3 AvoidAssistAirSpacing(Vector3 targetIn, float Responsiveness)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                Vector3 DSO = DodgeSphereCenter / Responsiveness;
                float moveSpace = (DSO - tank.boundsCentreWorldNoCheck).magnitude;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                if (SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, DSO, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                    {
                        if (lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                        {
                            IntVector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2);
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, DSO, out lastAllyDist, tank);
                if (lastCloseAlly == null)
                    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                {
                    IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
                //TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }

        private void UpdateCalcCrashAvoidenceSphere()
        {
            if (tank.rbody.IsNotNull())
            {
                var velo = tank.rbody.velocity;
                if (!velo.IsNaN() && !float.IsInfinity(velo.x)
                    && !float.IsInfinity(velo.z) && !float.IsInfinity(velo.y))
                {
                    DodgeSphereCenter = tank.boundsCentreWorldNoCheck + velo.Clamp(lowMaxBoundsVelo, highMaxBoundsVelo);
                    DodgeSphereRadius = lastTechExtents + Mathf.Clamp(recentSpeed / 2, 1, 63); // Strict
                    SafeVelocity = tank.rbody.velocity;
                    return;
                }
            }
            DodgeSphereCenter = tank.boundsCentreWorldNoCheck;
            DodgeSphereRadius = 1;
            SafeVelocity = Vector3.zero;
        }
        public bool IsOrbiting(Vector3 taskLocation, float orbitDistDelta, float minimumCloseInSpeed = AIGlobals.MinimumCloseInSpeed)
        {
            return orbitDistDelta * (KickStart.AIClockPeriod / 40) < minimumCloseInSpeed &&
                Vector3.Dot((taskLocation - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) < 0;
        }
        public float GetDistanceFromTask(Vector3 taskLocation, float additionalSpacing = 0)
        {
            if (Attempt3DNavi)
            {
                Vector3 veloFlat;
                if ((bool)tank.rbody)   // So that drifting is minimized
                {
                    veloFlat = SafeVelocity;
                    veloFlat.y = 0;
                }
                else
                    veloFlat = Vector3.zero;
                lastOperatorRange = (tank.boundsCentreWorldNoCheck + veloFlat - taskLocation).magnitude - additionalSpacing;
                return lastOperatorRange;
            }
            else
            {
                return GetDistanceFromTask2D(taskLocation, additionalSpacing);
            }
        }
        public float GetDistanceFromTask2D(Vector3 taskLocation, float additionalSpacing = 0)
        {
            Vector3 veloFlat;
            if ((bool)tank.rbody)   // So that drifting is minimized
            {
                veloFlat = SafeVelocity;
                veloFlat.y = 0;
            }
            else
                veloFlat = Vector3.zero;
            lastOperatorRange = (tank.boundsCentreWorldNoCheck.ToVector2XZ() + veloFlat.ToVector2XZ() - taskLocation.ToVector2XZ()).magnitude - additionalSpacing;
            return lastOperatorRange;
        }
        public void SetDistanceFromTaskUnneeded()
        {
            lastOperatorRange = 96; //arbitrary
        }
        public bool AutoHandleObstruction(ref EControlOperatorSet direct, float dist = 0, bool useRush = false, bool useGun = true, float div = 4)
        {
            if (!IsTechMoving(EstTopSped / div))
            {
                TryHandleObstruction(!AIECore.Feedback, dist, useRush, useGun, ref direct);
                return true;
            }
            return false;
        }
        public void TryHandleObstruction(bool hasMessaged, float dist, bool useRush, bool useGun, ref EControlOperatorSet direct)
        {
            //Something is in the way - try fetch the scenery to shoot at
            //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Obstructed");
            if (!hasMessaged)
            {
                //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Can't move there - something's in the way!");
            }

            ControlCore.FlagBusyUnstucking();
            IsTryingToUnjam = false;
            PivotOnly = false;
            if (direct.DriveDir == EDriveFacing.Backwards)
            {   // we are likely driving backwards
                ForceSetDrive = true;
                DriveVar = -1;

                UrgencyOverload += KickStart.AIClockPeriod / 2f;
                if (Urgency >= 0)
                    Urgency += KickStart.AIClockPeriod / 5f;
                if (UrgencyOverload > 80)
                {
                    //Are we just randomly angry for too long? let's fix that
                    AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                    EstTopSped = 1;
                    AvoidStuff = true;
                    UrgencyOverload = 0;
                }
                else if (useRush && dist > MaxObjectiveRange * 2)
                {
                    //SCREW IT - GO FULL SPEED WE ARE TOO FAR BEHIND!
                    if (useGun)
                        RemoveObstruction();
                    ForceSetDrive = true;
                    DriveVar = -1f;
                    Urgency += KickStart.AIClockPeriod / 5f;
                }
                else if (AIGlobals.UnjamUpdateStart < FrustrationMeter)
                {
                    IsTryingToUnjam = true;
                    //Try build beaming to clear debris
                    FrustrationMeter += KickStart.AIClockPeriod;
                    if (AIGlobals.UnjamUpdateEnd < FrustrationMeter)
                    {
                        FrustrationMeter = 45;
                    }
                    else if (AIGlobals.UnjamUpdateDrop < FrustrationMeter)
                    {
                        ControlCore.DriveToFacingTowards();
                        //ControlCore.lastDestination = tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * 50);
                        ForceSetBeam = false;
                        ForceSetDrive = true;
                        DriveVar = 1;
                    }
                    else
                    {
                        ControlCore.DriveToFacingTowards();
                        //ControlCore.lastDestination = tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * 50);
                        ForceSetDrive = true;
                        DriveVar = 1;
                        ForceSetBeam = true;
                    }
                }
                else if (45 < FrustrationMeter)
                {   //Shoot the freaking tree
                    FrustrationMeter += KickStart.AIClockPeriod;
                    if (useGun)
                        RemoveObstruction();
                    ForceSetDrive = true;
                    DriveVar = -0.5f;
                }
                else
                {   // Gun the throttle
                    FrustrationMeter += KickStart.AIClockPeriod;
                    ForceSetDrive = true;
                    DriveVar = -1f;
                }
            }
            else
            {   // we are likely driving forwards
                ForceSetDrive = true;
                DriveVar = 1;

                UrgencyOverload += KickStart.AIClockPeriod / 2f;
                if (Urgency >= 0)
                    Urgency += KickStart.AIClockPeriod / 5f;
                if (UrgencyOverload > 80)
                {
                    //Are we just randomly angry for too long? let's fix that
                    AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                    EstTopSped = 1;
                    AvoidStuff = true;
                    UrgencyOverload = 0;
                }
                else if (useRush && dist > MaxObjectiveRange * 2)
                {
                    //SCREW IT - GO FULL SPEED WE ARE TOO FAR BEHIND!
                    if (useGun)
                        RemoveObstruction();
                    ForceSetDrive = true;
                    DriveVar = 1f;
                    Urgency += KickStart.AIClockPeriod / 5f;
                }
                else if (AIGlobals.UnjamUpdateStart < FrustrationMeter)
                {
                    IsTryingToUnjam = true;
                    //Try build beaming to clear debris
                    FrustrationMeter += KickStart.AIClockPeriod;
                    if (AIGlobals.UnjamUpdateEnd < FrustrationMeter)
                    {
                        FrustrationMeter = 45;
                    }
                    else if (AIGlobals.UnjamUpdateDrop < FrustrationMeter)
                    {
                        ForceSetBeam = false;
                        ControlCore.DriveAwayFacingTowards();
                        //ControlCore.lastDestination = tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * -50);
                        ForceSetDrive = true;
                        DriveVar = -1;
                    }
                    else
                    {
                        ControlCore.DriveAwayFacingTowards();
                        //ControlCore.lastDestination = tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * -50);
                        ForceSetDrive = true;
                        DriveVar = -1;
                        ForceSetBeam = true;
                    }
                }
                else if (25 < FrustrationMeter)
                {
                    //Shoot the freaking tree
                    FrustrationMeter += KickStart.AIClockPeriod;
                    if (useGun)
                        RemoveObstruction();
                    ForceSetDrive = true;
                    DriveVar = 0.5f;
                }
                else
                {   // Gun the throttle
                    FrustrationMeter += KickStart.AIClockPeriod;
                    ForceSetDrive = true;
                    DriveVar = 1f;
                }
            }
        }
        public Transform GetObstruction(float searchRad)
        {
            List<Visible> ObstList;
            if (tank.rbody)
                ObstList = AIEPathing.ObstructionAwareness(tank.boundsCentreWorldNoCheck + SafeVelocity, this, searchRad);
            else
                ObstList = AIEPathing.ObstructionAwareness(tank.boundsCentreWorldNoCheck, this, searchRad);
            int bestStep = 0;
            float bestValue = 250000; // 500
            int steps = ObstList.Count;
            if (steps <= 0)
            {
                //DebugTAC_AI.Log("TACtical_AI: GetObstruction - DID NOT HIT ANYTHING");
                return null;
            }
            for (int stepper = 0; steps > stepper; stepper++)
            {
                float temp = Mathf.Clamp((ObstList.ElementAt(stepper).centrePosition - tank.boundsCentreWorldNoCheck).sqrMagnitude - ObstList.ElementAt(stepper).Radius, 0, 500);
                if (bestValue > temp && temp != 0)
                {
                    bestStep = stepper;
                    bestValue = temp;
                }
            }
            //DebugTAC_AI.Log("TACtical_AI: GetObstruction - found " + ObstList.ElementAt(bestStep).name);
            return ObstList.ElementAt(bestStep).trans;
        }
        public void RemoveObstruction(float searchRad = 12)
        {
            // Shoot at the scenery obsticle infront of us
            if (Obst == null)
            {
                Obst = GetObstruction(searchRad);
                Urgency += KickStart.AIClockPeriod / 5f;
            }
            FIRE_ALL = true;
        }
        /// <summary>
        /// Stop shooting and panicing due to a high Urgency and/or being too far from the player
        /// </summary>
        public void SettleDown()
        {
            UrgencyOverload = 0;
            Urgency = 0;
            FrustrationMeter = 0;
            Obst = null;
        }


        // ----------------------------  General Targeting  ----------------------------
        private bool lastSuppressedState = false;
        internal void SuppressFiring(bool Disable)
        {
            if (lastSuppressedState != Disable)
            {
                DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + " of Team " + tank.Team + ":  Disabled weapons: " + Disable);
                tank.Weapons.enabled = !Disable;
                lastSuppressedState = Disable;
            }
        }
        public void CheckEnemyErrorState()
        {
            if (lastEnemyGet?.tank)
            {
                if (!tank.IsEnemy(lastEnemyGet.tank.Team) || !lastEnemyGet.isActive
                    || lastEnemyGet.tank.blockman.blockCount == 0)
                {
                    lastEnemy = null;
                    //Debug.Assert(true, "TACtical_AI: Tech " + tank.name + " has valid, live target but it has no blocks.  How is this possible?!"); 
                }
            }
            else
                lastEnemy = null;
        }
        public Visible GetEnemyAllied()
        {
            Visible target = lastEnemyGet;
            if (Provoked == 0)
                target = null;
            else
            {
                float TargetRangeSqr = MaxCombatRange * MaxCombatRange;
                Vector3 scanCenter = tank.boundsCentreWorldNoCheck;
                if (target != null && (!target.isActive || !target.tank.IsEnemy(tank.Team) || 
                    (target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRangeSqr))
                {
                    //DebugTAC_AI.Log("Target lost");
                    target = null;
                }
                else if (NextFindTargetTime >= Time.time)
                {
                    if ((bool)lastPlayer)
                    {
                        Visible playerTarget = lastPlayer.tank.Weapons.GetManualTarget();
                        if (playerTarget)
                        {
                            // If the player fires while locked-on to a neutral/SubNeutral, the AI will assume this
                            //   is an attack request
                            Provoked = 0;
                            EndPursuit();
                            target = playerTarget;
                            return target;
                        }
                    }
                    return target;
                }
            }

            if ((bool)lastPlayer)
            {
                Visible playerTarget = lastPlayer.tank.Weapons.GetManualTarget();
                if (playerTarget && playerTarget.tank != null && playerTarget.isActive && 
                    playerTarget.tank.CentralBlock)
                {
                    // If the player fires while locked-on to a neutral/SubNeutral, the AI will assume this
                    //   is an attack request
                    Provoked = 0;
                    EndPursuit();
                    target = playerTarget;
                }
            }
            if (!target)
            {
                if (MovementController is AIControllerAir air && air.FlyStyle == AIControllerAir.FlightType.Aircraft)
                {
                    target = FindEnemyAir(false);
                }
                else
                    target = FindEnemy(false);
                if (target)
                {
                    if (AIGlobals.IsNonAggressiveTeam(target.tank.Team))
                        return null; // Don't want to accidently fire at a neutral close nearby
                }
            }
            return target;
        }
        public void UpdateTargetCombatFocus()
        {
            if (Provoked <= 0)
            {
                if (lastEnemyGet)
                {
                    if (!InRangeOfTarget(MaxCombatRange))
                    {
                        EndPursuit();
                    }
                }
                else
                    EndPursuit();
                Provoked = 0;
            }
            else
                Provoked -= KickStart.AIClockPeriod;
        }
        public float UpdateEnemyDistance(Vector3 enemyPosition)
        {
            _lastCombatRange = (enemyPosition - tank.boundsCentreWorldNoCheck).magnitude;
            return _lastCombatRange;
        }
        public float IgnoreEnemyDistance()
        {
            _lastCombatRange = float.MaxValue;
            return _lastCombatRange;
        }
        private void DetermineCombat()
        {
            bool DoNotEngage = false;
            if (lastEnemyGet?.tank)
                if (!tank.IsEnemy(lastEnemyGet.tank.Team))
                    lastEnemy = null;
            if (AIECore.RetreatingTeams.Contains(tank.Team))
            {
                Retreat = true;
                return;
            }

#if !STEAM
                if (KickStart.isAnimeAIPresent)
                {
                    if (AnimeAICompat.PollShouldRetreat(tank, this, out bool verdict))
                    {
                        Retreat = verdict;
                        return;
                    }
                }
#endif

            if (DediAI == AIType.Assault && lastBasePos.IsNotNull())
            {
                if (MaxCombatRange * 2 < (lastBasePos.position - tank.boundsCentreWorldNoCheck).magnitude)
                {
                    DoNotEngage = true;
                }
                else if (AdvancedAI)
                {
                    //WIP
                    if (DamageThreshold > 30)
                    {
                        DoNotEngage = true;
                    }
                }
            }
            else if (lastPlayer.IsNotNull())
            {
                if (DriverType == AIDriverType.Pilot)
                {
                    if (MaxCombatRange * 4 < (lastPlayer.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude)
                    {
                        DoNotEngage = true;
                    }
                    else if (AdvancedAI)
                    {
                        //WIP
                        if (DamageThreshold > 20)
                        {
                            DoNotEngage = true;
                        }
                    }
                }
                else if (DediAI != AIType.Assault)
                {
                    if (MaxCombatRange < (lastPlayer.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude)
                    {
                        DoNotEngage = true;
                    }
                    else if (AdvancedAI)
                    {
                        //WIP
                        if (DamageThreshold > 30)
                        {
                            DoNotEngage = true;
                        }
                    }
                }
            }
            Retreat = DoNotEngage;
        }
        private void DetermineCombatEnemy()
        {
            //bool DoNotEngage = false;
            Retreat = AIGlobals.AIAttract && AIECore.RetreatingTeams.Contains(tank.Team);

#if !STEAM
                if (KickStart.isAnimeAIPresent)
                {
                    if (AnimeAICompat.PollShouldRetreat(tank, this, out bool verdict))
                    {
                        Retreat = verdict;
                        return;
                    }
                }
#endif
        }

        private static bool UseVanillaTargetFetching = false;
        private float lastTargetGatherTime = 0;
        private List<Tank> targetCache = new List<Tank>();
        private List<Tank> GatherTechsInRange(float gatherRangeSqr)
        {
            if (lastTargetGatherTime > Time.time)
            {
                return targetCache;
            }
            lastTargetGatherTime = Time.time + AIGlobals.TargetCacheRefreshInterval;
            targetCache.Clear();
            foreach (Tank cTank in TankAIManager.GetTargetTanks(tank.Team))
            {
                if (cTank != tank && cTank.visible.isActive)
                {
                    float dist = (cTank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).sqrMagnitude;
                    if (dist < gatherRangeSqr)
                    {
                        targetCache.Add(cTank);
                    }
                }
            }
            return targetCache;
        }
        /// <summary>
        ///  Gets the enemy position based on current position and AI preferences
        /// </summary>
        /// <param name="inRange">value > 0</param>
        /// <param name="pos">MAX 3</param>
        /// <returns></returns>
        public Visible FindEnemy(bool InvertBullyPriority, int pos = 1)
        {
            //if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
            //    return null; // We NO ATTACK
            Visible target = lastEnemyGet;

            // We begin the search
            float TargetRangeSqr = MaxCombatRange * MaxCombatRange;
            Vector3 scanCenter = tank.boundsCentreWorldNoCheck;

            if (target?.tank)
            {
                if (!target.isActive || !target.tank.IsEnemy(tank.Team))
                {
                    //DebugTAC_AI.Log("Target lost");
                    target = null;
                }
                else if (KeepEnemyFocus || NextFindTargetTime <= Time.time) // Carry on chasing the target
                {
                    return target;
                }
                else if ((target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRangeSqr)
                {
                    //DebugTAC_AI.Log("Target out of range");
                    target = null;
                }
            }

            if (AttackMode == EAttackMode.Random)
            {
                List<Tank> techs = GatherTechsInRange(TargetRangeSqr);
                int max = techs.Count();
                int launchCount = UnityEngine.Random.Range(0, max);
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (cTank.IsEnemy(tank.Team) && cTank != tank && cTank.visible.isActive)
                    {
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                        if (dist < TargetRangeSqr)
                        {
                            target = cTank.visible;
                        }
                    }
                }
                NextFindTargetTime = Time.time + AIGlobals.PestererSwitchDelay;
            }
            else if (AttackMode == EAttackMode.Strong)
            {
                List<Tank> techs = GatherTechsInRange(TargetRangeSqr);
                int launchCount = techs.Count();
                if (InvertBullyPriority)
                {
                    int BlockCount = 0;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(tank.Team) && cTank != tank)
                        {
                            float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                            if (cTank.blockman.blockCount > BlockCount && dist < TargetRangeSqr)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                else
                {
                    int BlockCount = 262144;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(tank.Team) && cTank != tank)
                        {
                            float dist = (cTank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).sqrMagnitude;
                            if (cTank.blockman.blockCount < BlockCount && dist < TargetRangeSqr)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                NextFindTargetTime = Time.time + AIGlobals.ScanDelay;
            }
            else
            {
                NextFindTargetTime = Time.time + AIGlobals.ScanDelay;
                if (AttackMode == EAttackMode.Chase && target != null)
                {
                    if (target.isActive)
                        return target;
                }
                if (pos == 1 && UseVanillaTargetFetching)
                    return tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);

                float TargRange2 = TargetRangeSqr;
                float TargRange3 = TargetRangeSqr;

                Visible target2 = null;
                Visible target3 = null;

                List<Tank> techs = GatherTechsInRange(TargetRangeSqr);
                int launchCount = techs.Count();

                Tank cTank;
                float dist;
                int step;
                switch (pos)
                {
                    case 2:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank != tank && cTank.IsEnemy(tank.Team))
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRangeSqr)
                                {
                                    if (TargetRangeSqr < TargRange2)
                                    {
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRangeSqr = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                            }
                        }
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    case 3:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(tank.Team) && cTank != tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRangeSqr)
                                {
                                    if (TargetRangeSqr < TargRange2)
                                    {
                                        if (TargRange2 < TargRange3)
                                        {
                                            TargRange3 = dist;
                                            target3 = cTank.visible;
                                        }
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRangeSqr = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    if (TargRange2 < TargRange3)
                                    {
                                        TargRange3 = dist;
                                        target3 = cTank.visible;
                                    }
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                                else if (dist < TargRange3)
                                {
                                    TargRange3 = dist;
                                    target3 = cTank.visible;
                                }
                            }
                        }
                        if (pos >= 3 && !(bool)target3)
                            return target3;
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    default:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank != tank && cTank.IsEnemy(tank.Team))
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRangeSqr)
                                {
                                    TargetRangeSqr = dist;
                                    target = cTank.visible;
                                }
                            }
                        }
                        break;
                }
            }
            /*
            if (target.IsNull())
            {
                DebugTAC_AI.Log("TACtical_AI: Tech " + Tank.name + " Could not find target with FindEnemy, resorting to defaults");
                return Tank.Vision.GetFirstVisibleTechIsEnemy(Tank.Team);
            }
            */
            return target;
        }
        public Visible FindEnemyAir(bool InvertBullyPriority, int pos = 1)
        {
            //if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
            //    return null; // We NO ATTACK
            Visible target = lastEnemyGet;

            // We begin the search
            float TargetRangeSqr = MaxCombatRange * MaxCombatRange;
            Vector3 scanCenter = tank.boundsCentreWorldNoCheck;

            if (target != null)
            {
                if (!target.isActive || !target.tank.IsEnemy(tank.Team))
                {
                    //DebugTAC_AI.Log("Target lost");
                    target = null;
                }
                else if (KeepEnemyFocus || NextFindTargetTime <= Time.time) // Carry on chasing the target
                {
                    return target;
                }
                else if ((target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRangeSqr)
                {
                    //DebugTAC_AI.Log("Target out of range");
                    target = null;
                }
            }
            float altitudeHigh = -256;

            if (AttackMode == EAttackMode.Random)
            {
                List<Tank> techs = GatherTechsInRange(TargetRangeSqr);
                scanCenter = DodgeSphereCenter;
                int launchCount = techs.Count();
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (cTank.IsEnemy(tank.Team) && cTank != tank)
                    {
                        if (altitudeHigh < cTank.boundsCentreWorldNoCheck.y)
                        {   // Priority is other aircraft
                            if (AIEPathing.AboveHeightFromGround(cTank.boundsCentreWorldNoCheck))
                                altitudeHigh = AIEPathing.OffsetFromGroundA(cTank.boundsCentreWorldNoCheck, this).y;
                            else
                                altitudeHigh = cTank.boundsCentreWorldNoCheck.y;
                        }
                        else
                            continue;
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                        if (dist < TargetRangeSqr)
                        {
                            TargetRangeSqr = dist;
                            target = cTank.visible;
                        }
                    }
                }
                NextFindTargetTime = Time.time + AIGlobals.PestererSwitchDelay;
            }
            else if (AttackMode == EAttackMode.Strong)
            {
                List<Tank> techs = GatherTechsInRange(TargetRangeSqr);
                int launchCount = techs.Count();
                if (InvertBullyPriority)
                {
                    altitudeHigh = 2199;
                    int BlockCount = 0;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(tank.Team) && cTank != tank)
                        {
                            if (altitudeHigh > cTank.boundsCentreWorldNoCheck.y)
                            {   // Priority is bases or lowest target
                                if (!AIEPathing.AboveHeightFromGround(cTank.boundsCentreWorldNoCheck))
                                    altitudeHigh = AIEPathing.OffsetFromGroundA(cTank.boundsCentreWorldNoCheck, this).y;
                                else
                                    altitudeHigh = cTank.boundsCentreWorldNoCheck.y;
                            }
                            else
                                continue;
                            float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                            if (cTank.blockman.blockCount > BlockCount && dist < TargetRangeSqr)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                else
                {
                    int BlockCount = 262144;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(tank.Team) && cTank != tank)
                        {
                            if (altitudeHigh < cTank.boundsCentreWorldNoCheck.y)
                            {   // Priority is other aircraft
                                if (AIEPathing.AboveHeightFromGround(cTank.boundsCentreWorldNoCheck))
                                    altitudeHigh = AIEPathing.OffsetFromGroundA(cTank.boundsCentreWorldNoCheck, this).y;
                                else
                                    altitudeHigh = cTank.boundsCentreWorldNoCheck.y;
                            }
                            else
                                continue;
                            float dist = (cTank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).sqrMagnitude;
                            if (cTank.blockman.blockCount < BlockCount && dist < TargetRangeSqr)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                NextFindTargetTime = Time.time + AIGlobals.ScanDelay;
            }
            else
            {
                NextFindTargetTime = Time.time + AIGlobals.ScanDelay;
                if (AttackMode == EAttackMode.Chase && target != null)
                {
                    if (target.isActive)
                        return target;
                }
                float TargRange2 = TargetRangeSqr;
                float TargRange3 = TargetRangeSqr;

                Visible target2 = null;
                Visible target3 = null;

                List<Tank> techs = GatherTechsInRange(TargetRangeSqr);
                int launchCount = techs.Count();
                Tank cTank;
                float dist;
                int step;
                switch (pos)
                {
                    case 2:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(tank.Team) && cTank != tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRangeSqr)
                                {
                                    if (TargetRangeSqr < TargRange2)
                                    {
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRangeSqr = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                            }
                        }
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    case 3:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(tank.Team) && cTank != tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRangeSqr)
                                {
                                    if (TargetRangeSqr < TargRange2)
                                    {
                                        if (TargRange2 < TargRange3)
                                        {
                                            TargRange3 = dist;
                                            target3 = cTank.visible;
                                        }
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRangeSqr = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    if (TargRange2 < TargRange3)
                                    {
                                        TargRange3 = dist;
                                        target3 = cTank.visible;
                                    }
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                                else if (dist < TargRange3)
                                {
                                    TargRange3 = dist;
                                    target3 = cTank.visible;
                                }
                            }
                        }
                        if (pos >= 3 && !(bool)target3)
                            return target3;
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    default:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(tank.Team) && cTank != tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRangeSqr)
                                {
                                    TargetRangeSqr = dist;
                                    target = cTank.visible;
                                }
                            }
                        }
                        break;
                }
            }
            return target;
        }
        public Vector3 InterceptTargetDriving(Visible targetTank)
        {
            if (AdvancedAI)   // Rough Target leading
            {
                return RoughPredictTarget(targetTank.tank);
            }
            else
                return targetTank.tank.boundsCentreWorldNoCheck;
        }
        private const float MaxBoundsVelo = 350;
        private static Vector3 lowMaxBoundsVelo = -new Vector3(MaxBoundsVelo, MaxBoundsVelo, MaxBoundsVelo);
        private static Vector3 highMaxBoundsVelo = new Vector3(MaxBoundsVelo, MaxBoundsVelo, MaxBoundsVelo);
        public Vector3 RoughPredictTarget(Tank targetTank)
        {
            if (targetTank.rbody.IsNotNull())
            {
                var velo = targetTank.rbody.velocity;
                if (!velo.IsNaN() && lastCombatRange <= AIGlobals.EnemyExtendActionRange && !float.IsInfinity(velo.x)
                    && !float.IsInfinity(velo.z) && !float.IsInfinity(velo.y))
                {
                    return targetTank.boundsCentreWorldNoCheck + (velo.Clamp(lowMaxBoundsVelo, highMaxBoundsVelo) *
                        (lastCombatRange * AIGlobals.TargetVelocityLeadPredictionMulti));
                }
            }
            return targetTank.boundsCentreWorldNoCheck;
        }


        // ----------------------------  Lock-On Targeting  ---------------------------- 
        public void ManageAILockOn()
        {
            switch (ActiveAimState)
            {
                case AIWeaponState.Enemy:
                    if (lastEnemyGet.IsNotNull())
                    {   // Allow the enemy AI to finely select targets
                        //DebugTAC_AI.Log("TACtical_AI: Overriding targeting to aim at " + lastEnemy.name + "  pos " + lastEnemy.tank.boundsCentreWorldNoCheck);
                        lastLockOnTarget = lastEnemyGet;
                    }
                    break;
                case AIWeaponState.Obsticle:
                    if (Obst.IsNotNull())
                    {
                        var resTarget = Obst.GetComponent<Visible>();
                        if (resTarget)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: Overriding targeting to aim at obstruction");
                            lastLockOnTarget = resTarget;
                        }
                    }
                    break;
                case AIWeaponState.Mimic:
                    if (lastCloseAlly.IsNotNull())
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Overriding targeting to aim at player's target");
                        var helperAlly = lastCloseAlly.GetHelperInsured();
                        if (helperAlly.ActiveAimState == AIWeaponState.Enemy)
                            lastLockOnTarget = helperAlly.lastEnemyGet;
                    }
                    break;
            }

            if (lastLockOnTarget)
            {
                bool playerAim = tank.PlayerFocused && !ManPlayerRTS.PlayerIsInRTS;
                if (!lastLockOnTarget.isActive || (playerAim && !tank.control.FireControl))
                {   // Cannot do as camera breaks
                    lastLockOnTarget = null;
                    return;
                }
                if (lastLockOnTarget == tank.visible)
                {
                    DebugTAC_AI.Assert("Tech " + tank.name + " tried to lock-on to itself!!!");
                    lastLockOnTarget = null;
                    return;
                }
                if (!playerAim && lastLockOnTarget.resdisp && ActiveAimState != AIWeaponState.Obsticle)
                {
                    lastLockOnTarget = null;
                    return;
                }

                float maxDist;
                if (ManNetwork.IsNetworked)
                    maxDist = tank.Weapons.m_ManualTargetingSettingsMAndKB.m_ManualTargetingRadiusMP;
                else
                    maxDist = tank.Weapons.m_ManualTargetingSettingsMAndKB.m_ManualTargetingRadiusSP;
                if (_lastCombatRange > maxDist)
                {
                    lastLockOnTarget = null;
                }
            }
        }


        // ----------------------------  Chase Handling  ---------------------------- 
        public int Provoked = 0;           // Were we hit from afar?
        public bool KeepEnemyFocus { get; private set; } = false;     // Chasing specified target?
        /// <summary>
        /// Set a target to chase after
        /// </summary>
        /// <param name="target"></param>
        /// <returns>true if PursuingTarget is true</returns>
        public bool SetPursuit(Visible target)
        {
            if (!KeepEnemyFocus)
            {
                if ((bool)target)
                {
                    if ((bool)target.tank)
                    {
                        lastEnemy = target;
                        ControlOperator.SetLastDest(target.tank.boundsCentreWorldNoCheck);
                        KeepEnemyFocus = true;
                        return true;
                    }
                }
            }
            else if (target == null)
                KeepEnemyFocus = false;
            return false;
        }
        public void EndPursuit()
        {
            if (KeepEnemyFocus)
            {
                KeepEnemyFocus = false;
            }
        }
        public bool InRangeOfTarget(float distance)
        {
            return InRangeOfTarget(lastEnemyGet, distance);
        }
        public bool InRangeOfTarget(Visible target, float distance)
        {
            return (target.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).sqrMagnitude <= distance * distance;
        }


        // ----------------------------  Anchor Management  ---------------------------- 

        private static MethodInfo MI = typeof(TechAnchors).GetMethod("ConfigureJoint", BindingFlags.NonPublic | BindingFlags.Instance);
        internal void TryAnchor()
        {
            if (CanAnchorSafely && !tank.IsAnchored)
            {
                //DebugTAC_AI.Assert(true,"TACtical_AI: AI " + tank.name + ":  Trying to anchor " + StackTraceUtility.ExtractStackTrace());
                tank.FixupAnchors(false);
                if (!tank.IsAnchored)
                {
                    tank.Anchors.TryAnchorAll(true);
                    if (tank.IsAnchored)
                        return;
                    Vector3 startPos = tank.visible.centrePosition;
                    Quaternion tankFore = Quaternion.LookRotation(tank.trans.forward.SetY(0).normalized, Vector3.up);
                    tank.visible.Teleport(startPos, tankFore, true);
                    //Quaternion tankStartRot = tank.trans.rotation;
                    for (int step = 0; step < 16; step++)
                    {
                        if (!tank.IsAnchored)
                        {
                            Vector3 newPos = startPos + new Vector3(0, -4, 0);
                            newPos.y += step / 2f;
                            tank.visible.Teleport(newPos, tankFore, false);
                            tank.Anchors.TryAnchorAll();
                        }
                        if (tank.IsAnchored)
                            break;
                        tank.FixupAnchors(true);
                    }
                }
                //TryReallyAnchor();
            }
        }
        /// <summary>
        /// IGNORES CHECKS
        /// </summary>
        internal void TryReallyAnchor(bool forced = false)
        {
            if (!tank.IsAnchored)
            {
                bool worked = false;
                Vector3 startPosTrans = tank.trans.position;
                tank.FixupAnchors(false);
                Vector3 startPos = tank.visible.centrePosition;
                Quaternion tankFore = Quaternion.LookRotation(tank.trans.forward.SetY(0).normalized, Vector3.up);
                tank.visible.Teleport(startPos, tankFore, true);
                //Quaternion tankStartRot = tank.trans.rotation;
                for (int step = 0; step < 16; step++)
                {
                    if (!tank.IsAnchored)
                    {
                        Vector3 newPos = startPos + new Vector3(0, -4, 0);
                        newPos.y += step / 2f;
                        tank.visible.Teleport(newPos, tankFore, false);
                        tank.Anchors.TryAnchorAll();
                    }
                    if (tank.IsAnchored)
                    {
                        worked = true;
                        break;
                    }
                    tank.FixupAnchors(true);
                }
                var anchors = tank.blockman.IterateBlockComponents<ModuleAnchor>();
                if (!worked && anchors.Count() > 0)
                {
                    if (AIGlobals.IsAttract || forced)
                    {
                        DebugTAC_AI.Assert(true, (AIGlobals.IsAttract ? "(ATTRACT BASE)" : "(FORCED)") + " screw you i'm anchoring anyways, I don't give a f*bron about your anchor checks!");
                        foreach (var item in anchors)
                        {
                            item.AnchorToGround();
                            if (item.AnchorGeometryActive)
                            {
                                tank.Anchors.AddAnchor(item);
                            }
                        }
                        tank.grounded = true;
                        MI.Invoke(tank.Anchors, new object[0]);
                    }
                    else
                    {
                        tank.trans.position = startPosTrans - (Vector3.down * 0.1f);
                    }
                }
                ExpectAITampering = true;
                // Reset to ground so it doesn't go flying off into space
                tank.visible.Teleport(startPos, tankFore, true);
            }
        }
        internal void AdjustAnchors()
        {
            bool prevAnchored = tank.IsAnchored;
            UnAnchor();
            if (!tank.IsAnchored)
            {
                TryReallyAnchor(prevAnchored);
            }
        }
        internal void UnAnchor()
        {
            if (tank.Anchors.NumIsAnchored > 0)
                tank.Anchors.UnanchorAll(true);
            if (!tank.IsAnchored && AIAlign == AIAlignment.Player)
                ForceAllAIsToEscort(true, false);
            JustUnanchored = true;
        }


        // ----------------------------  Logistics  ---------------------------- 
        public TankBlock HeldBlock => heldBlock;
        private TankBlock heldBlock;
        private Vector3 blockHoldPos = Vector3.zero;
        private Quaternion blockHoldRot = Quaternion.identity;
        private bool blockHoldOffset = false;
        /// <summary> Hold blocks for self-build-repair and scavenging operations </summary>
        private void UpdateBlockHold()
        {
            if (heldBlock)
            {
                if (!ManNetwork.IsNetworked)
                {
                    if (!heldBlock.visible.isActive)
                    {
                        try
                        {
                            DropBlock();
                        }
                        catch { }
                        heldBlock = null;
                    }
                    else if (heldBlock.visible.InBeam || heldBlock.IsAttached)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "'s grabbed block was thefted!");
                        DropBlock();
                    }
                    else if (ManPointer.inst.targetVisible == heldBlock.visible)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "'s grabbed block was grabbed by player!");
                        DropBlock();
                    }
                    else
                    {
                        Vector3 moveVec;
                        if (blockHoldOffset)
                        {
                            moveVec = tank.transform.TransformPoint(blockHoldPos) - heldBlock.transform.position;
                            float dotVal = Vector3.Dot(moveVec.normalized, Vector3.down);
                            if (dotVal > 0.75f)
                                moveVec.y += moveVec.ToVector2XZ().magnitude / 3;
                            else
                            {
                                moveVec.y -= moveVec.ToVector2XZ().magnitude / 3;
                            }
                            Vector3 finalPos = heldBlock.transform.position;
                            finalPos += moveVec / ((100 / AIGlobals.BlockAttachDelay) * Time.fixedDeltaTime);
                            if (finalPos.y < tank.transform.TransformPoint(blockHoldPos).y)
                                finalPos.y = tank.transform.TransformPoint(blockHoldPos).y;
                            heldBlock.transform.position = finalPos;
                            if (tank.rbody)
                                heldBlock.rbody.velocity = tank.rbody.velocity.SetY(0);
                            heldBlock.rbody.AddForce(-(Physics.gravity * heldBlock.AverageGravityScaleFactor), ForceMode.Acceleration);
                            Vector3 forward = tank.trans.TransformDirection(blockHoldRot * Vector3.forward);
                            Vector3 up = tank.trans.TransformDirection(blockHoldRot * Vector3.up);
                            Quaternion rotChangeWorld = Quaternion.LookRotation(forward, up);
                            heldBlock.rbody.MoveRotation(Quaternion.RotateTowards(heldBlock.trans.rotation, rotChangeWorld, (360 / AIGlobals.BlockAttachDelay) * Time.fixedDeltaTime));
                            heldBlock.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0.25f);
                        }
                        else
                        {
                            moveVec = tank.boundsCentreWorldNoCheck + (Vector3.up * (lastTechExtents + 3)) - heldBlock.visible.centrePosition;
                            moveVec = Vector3.ClampMagnitude(moveVec * 4, AIGlobals.ItemGrabStrength);
                            heldBlock.rbody.AddForce(moveVec - (Physics.gravity * heldBlock.AverageGravityScaleFactor), ForceMode.Acceleration);
                            heldBlock.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0.25f);
                        }
                    }
                }
                else if (ManNetwork.IsHost)
                {   //clip it into the Tech to send to inventory 
                    if (!heldBlock.visible.isActive)
                    {
                        DropBlock();
                    }
                    else if (heldBlock.visible.InBeam || heldBlock.IsAttached)
                    {
                        DropBlock();
                    }
                    else
                    {
                        if (tank.CentralBlock)
                            heldBlock.visible.centrePosition = tank.CentralBlock.centreOfMassWorld;
                        else
                            heldBlock.visible.centrePosition = tank.boundsCentreWorldNoCheck;
                    }
                }
            }
        }
        /// <summary>
        /// Returns true if the block was grabbed
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        internal bool HoldBlock(Visible TB)
        {
            if (!TB)
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: Tech " + tank.name + " attempted to illegally grab NULL Visible");
            }
            else if (ManNetwork.IsNetworked)
            {
                //DebugTAC_AI.Assert(true, "TACtical_AI: Tech " + tank.name + " called HoldBlock in networked environment. This is not supported!");if (TB.block)
                if (TB.block && Singleton.playerTank)
                {
                    TB.Teleport(Singleton.playerTank.boundsCentreWorld, Quaternion.identity);
                }
            }
            else if (TB.block)
            {
                if (TB.isActive)
                {
                    if (TB.InBeam)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "'s target block was thefted by a tractor beam!");
                    }
                    else
                    {
                        if (TB.rbody)
                        {
                            ColliderSwapper CS;
                            if (heldBlock && heldBlock != TB.block)
                            {
                                DropBlock();
                            }
                            blockHoldOffset = false;
                            if (ManNetwork.IsNetworked)
                                return true;
                            heldBlock = TB.block;
                            CS = heldBlock.GetComponent<ColliderSwapper>();
                            if (CS)
                                CS.EnableCollision(false);

                            return true;
                        }
                        else
                            DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "'s target block HAS NO RBODY");
                    }
                }
            }
            else
                DebugTAC_AI.Assert(true, "TACtical_AI: Tech " + tank.name + " attempted to illegally grab "
                    + (!TB.name.NullOrEmpty() ? TB.name : "NULL")
                    + " of type " + TB.type + " when they are only allowed to grab blocks");
            return false;
        }
        internal bool HoldBlock(Visible TB, RawBlockMem BM)
        {
            if (!TB)
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: Tech " + tank.name + " attempted to illegally grab NULL Visible");
            }
            else if (ManNetwork.IsNetworked)
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: Tech " + tank.name + " called HoldBlock in networked environment. This is not supported!");
            }
            else if (TB.block)
            {
                if (TB.isActive)
                {
                    if (TB.InBeam)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "'s target block was thefted by a tractor beam!");
                    }
                    else
                    {
                        if (TB.rbody)
                        {
                            ColliderSwapper CS;
                            if (heldBlock && heldBlock != TB.block)
                            {
                                DropBlock();
                            }
                            blockHoldOffset = true;
                            blockHoldPos = BM.p;
                            blockHoldRot = new OrthoRotation(BM.r);
                            if (ManNetwork.IsNetworked)
                                return true;
                            heldBlock = TB.block;
                            CS = heldBlock.GetComponent<ColliderSwapper>();
                            if (CS)
                                CS.EnableCollision(false);

                            return true;
                        }
                        else
                            DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "'s target block HAS NO RBODY");
                    }
                }
            }
            else
                DebugTAC_AI.Assert(true, "TACtical_AI: Tech " + tank.name + " attempted to illegally grab "
                    + (!TB.name.NullOrEmpty() ? TB.name : "NULL")
                    + " of type " + TB.type + " when they are only allowed to grab blocks");
            return false;
        }
        internal void DropBlock(Vector3 throwDirection)
        {
            if (heldBlock)
            {
                if (heldBlock.rbody)
                {
                    // if ((heldBlock.visible.centrePosition - tank.boundsCentreWorldNoCheck).magnitude > 16)
                    //     heldBlock.visible.centrePosition = tank.boundsCentreWorldNoCheck + (Vector3.up * (lastTechExtents + 3));
                    heldBlock.rbody.velocity = throwDirection.normalized * AIGlobals.ItemThrowVelo;
                }
                var CS = heldBlock.GetComponent<ColliderSwapper>();
                if (CS)
                    CS.EnableCollision(true);
                heldBlock.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0);
                heldBlock = null;
            }
        }
        internal void DropBlock()
        {
            if (heldBlock)
            {
                var CS = heldBlock.GetComponent<ColliderSwapper>();
                if (CS)
                    CS.EnableCollision(true);
                heldBlock.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0);
                heldBlock = null;
            }
        }

        /// <summary> Allow allies to approach mobile base techs </summary>
        private bool denyCollect = false;
        internal bool techIsApproaching = false;
        internal TankAIHelper ApproachingTech;
        /// <summary> Allow allies to approach mobile base techs </summary>
        /// <param name="Approaching">Tech to approach</param>
        public void SlowForApproacher(TankAIHelper Approaching)
        {
            if (AvoidStuff)
            {
                AvoidStuff = false;
                IsTryingToUnjam = false;
                CancelInvoke();
                //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Allowing approach");
                Invoke("EndSlowForApproacher", 2);
            }
            if (!techIsApproaching)
                ApproachingTech = Approaching;
            techIsApproaching = true;
        }
        private void EndSlowForApproacher()
        {
            if (!AvoidStuff)
            {
                AvoidStuff = true;
            }

            techIsApproaching = false;
            ApproachingTech = null;
        }
        /// <summary> Drop all items in collectors (Aircraft resource payload drop) </summary>
        public void DropAllItemsInCollectors()
        {
            denyCollect = true;
            CancelInvoke("StopDropAllItems");
            Invoke("StopDropAllItems", 2);
        }
        private void UpdateCollectors()
        {
            if (denyCollect)
            {
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.GetComponent<ModuleItemConsume>() && !hold.IsEmpty && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        // AIRDROP
                        hold.DropAll();
                    }
                }
            }
        }
        private void StopDropAllItems()
        {
            denyCollect = false;
        }


        // ----------------------------  Self-Repair  ---------------------------- 
        internal Event<TankAIHelper> FinishedRepairEvent = new Event<TankAIHelper>();
        internal void UpdateDamageThreshold()
        {
            int blockC = tank.blockman.blockCount;
            if (maxBlockCount <= blockC)
                maxBlockCount = blockC;
            if (maxBlockCount == 1)
            {
                var root = tank.blockman.GetRootBlock();
                DamageThreshold = (1f - (root.visible.damageable.Health / (float)root.damage.maxHealth)) * 100;
                lastBlockCount = blockC;
            }
            else
            {
                if (lastBlockCount != blockC)
                {
                    DamageThreshold = (1f - (blockC / (float)maxBlockCount)) * 100;
                    lastBlockCount = blockC;
                }
            }
        }
        private void TryRepairStatic()
        {
            BookmarkBuilder builder = GetComponent<BookmarkBuilder>();
            if (builder)
            {
                AILimitSettings.OverrideForBuilder();
                if (TechMemor.IsNull())
                {
                    builder.HookUp(this);
                    DebugTAC_AI.Assert("TACtical_AI: Tech " + tank.name + "TryRepairStatic has a BookmarkBuilder but NO TechMemor!");
                }
                if (lastEnemyGet != null)
                {   // Combat repairs (combat mechanic)
                    //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " RepairCombat");
                    AIERepair.RepairStepper(this, tank, TechMemor, true, Combat: true);
                }
                else
                {   // Repairs in peacetime
                    AIERepair.RepairStepper(this, tank, TechMemor);
                }
            }
            UpdateDamageThreshold();
        }
        private void TryRepairAllied()
        {
            BookmarkBuilder builder = GetComponent<BookmarkBuilder>();
            if (builder && TechMemor.IsNull())
            {
                builder.HookUp(this);
                DebugTAC_AI.Assert("TACtical_AI: Tech " + tank.name + "TryRepairAllied has a BookmarkBuilder but NO TechMemor!");
            }
            if (builder || (AutoRepair && (!tank.PlayerFocused || ManPlayerRTS.PlayerIsInRTS) && (KickStart.AISelfRepair || tank.IsAnchored)))
            {
                if (builder)
                {
                    AISetSettings.OverrideForBuilder();
                    AILimitSettings.OverrideForBuilder();
                }
                if (lastEnemyGet != null)
                {   // Combat repairs (combat mechanic)
                    //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " RepairCombat");
                    AIERepair.RepairStepper(this, tank, TechMemor, AdvancedAI, Combat: true);
                }
                else
                {   // Repairs in peacetime
                    //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Repair");
                    if (AdvancedAI) // faster for smrt
                        AIERepair.InstaRepair(tank, TechMemor, KickStart.AIClockPeriod);
                    else
                        AIERepair.RepairStepper(this, tank, TechMemor);
                }
            }
            UpdateDamageThreshold();
        }
        public void DelayedRepairUpdate()
        {   //OBSOLETE until further notice
            // Dynamic timescaled update that fires when needed, less for slow techs, fast for large techs
        }
        private void RemoveEnemyMatters()
        {
            var AISettings = tank.GetComponent<AIBookmarker>();
            if (AISettings.IsNotNull())
                DestroyImmediate(AISettings);
        }
        private void RemoveBookmarkBuilder()
        {
            BookmarkBuilder Builder = tank.GetComponent<BookmarkBuilder>();
            if (Builder.IsNotNull())
                Builder.Finish(this);
        }


        // ----------------------------  Debug Collector  ---------------------------- 
        public void ShowDebugThisFrame()
        {
            if (DebugRawTechSpawner.ShowDebugFeedBack && AIECore.debugVisuals)
            {
                try
                {
                    Vector3 boundsC = tank.boundsCentreWorldNoCheck;
                    Vector3 boundsCUp = tank.boundsCentreWorldNoCheck + (Vector3.up * lastTechExtents);
                    DebugRawTechSpawner.DrawDirIndicatorCircle(boundsC + (Vector3.up * 128), Vector3.up, Vector3.forward, JobSearchRange, Color.blue);
                    if (tank.IsAnchored && !CanAutoAnchor)
                    {
                        DebugRawTechSpawner.DrawDirIndicatorRecPrizExt(boundsC, Vector3.one * lastTechExtents, Color.yellow);
                        if (lastEnemyGet != null && lastEnemyGet.isActive)
                        {
                            DebugRawTechSpawner.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MaxCombatRange, new Color(1, 0.6f, 0.6f));
                            DebugRawTechSpawner.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MinCombatRange, Color.red);
                            DebugRawTechSpawner.DrawDirIndicator(lastEnemyGet.tank.boundsCentreWorldNoCheck,
                                lastEnemyGet.tank.boundsCentreWorldNoCheck + Vector3.up * lastEnemyGet.GetCheapBounds(), Color.red);
                        }
                    }
                    else
                    {
                        DebugRawTechSpawner.DrawDirIndicatorSphere(boundsC, lastTechExtents, Color.yellow);
                        DebugRawTechSpawner.DrawDirIndicatorSphere(DodgeSphereCenter, DodgeSphereRadius, Color.gray);
                        if (Attempt3DNavi)
                        {
                            DebugRawTechSpawner.DrawDirIndicatorSphere(boundsC, MaxObjectiveRange, Color.cyan);
                            if (lastEnemyGet != null && lastEnemyGet.isActive)
                            {
                                DebugRawTechSpawner.DrawDirIndicatorSphere(boundsC, MaxCombatRange, new Color(1, 0.6f, 0.6f));
                                DebugRawTechSpawner.DrawDirIndicatorSphere(boundsC, MinCombatRange, Color.red);
                                DebugRawTechSpawner.DrawDirIndicator(lastEnemyGet.tank.boundsCentreWorldNoCheck,
                                    lastEnemyGet.tank.boundsCentreWorldNoCheck + Vector3.up * lastEnemyGet.GetCheapBounds(), Color.red);
                            }
                        }
                        else
                        {
                            DebugRawTechSpawner.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MaxObjectiveRange, Color.cyan);
                            if (lastEnemyGet != null && lastEnemyGet.isActive)
                            {
                                DebugRawTechSpawner.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MaxCombatRange, new Color(1, 0.6f, 0.6f));
                                DebugRawTechSpawner.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MinCombatRange, Color.red);
                                DebugRawTechSpawner.DrawDirIndicator(lastEnemyGet.tank.boundsCentreWorldNoCheck,
                                    lastEnemyGet.tank.boundsCentreWorldNoCheck + Vector3.up * lastEnemyGet.GetCheapBounds(), Color.red);
                            }
                        }
                    }
                    if (lastPlayer != null && lastPlayer.isActive)
                    {
                        DebugRawTechSpawner.DrawDirIndicator(lastPlayer.tank.boundsCentreWorldNoCheck,
                            lastPlayer.tank.boundsCentreWorldNoCheck + Vector3.up * lastPlayer.GetCheapBounds(), Color.white);
                    }
                    if (Obst != null)
                    {
                        float rad = 6;
                        if (Obst.GetComponent<Visible>())
                            rad = Obst.GetComponent<Visible>().Radius;
                        DebugRawTechSpawner.DrawDirIndicator(Obst.position, Obst.position + Vector3.up * rad, Color.gray);
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("Error on Debug Draw " + e);
                }
            }
        }

    }
}
