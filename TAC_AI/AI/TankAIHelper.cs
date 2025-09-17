using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;
using TAC_AI.Templates;
using TAC_AI.World;
using TerraTechETCUtil;
using UnityEngine;

namespace TAC_AI.AI
{
    /// <summary>
    /// This AI either runs normally in Singleplayer, or on the Server in Multiplayer
    /// </summary>
    public class TankAIHelper : MonoBehaviour, IWorldTreadmill
    {
        internal static bool updateErrored = false;

        public Tank tank;
        public AITreeType.AITypes lastAIType;
        //Tweaks (controlled by Module)
        /// <summary> The type of vehicle the AI controls </summary>
        public AIDriverType DriverType
        {
            get => driveType;
            private set
            {
                if (driveType != value)
                {
                    //DebugTAC_AI.Assert("Driver Type change " + driveType + " -> " + value);
                    driveType = value;
                }
            }
        }
        private AIDriverType driveType = AIDriverType.AutoSet;
        public void SetDriverType(AIDriverType driverType)
        {
            DriverType = driverType;
            MovementAIControllerDirty = true;
        }
        private bool MCD = false;
        public bool MovementAIControllerDirty
        {
            get => MCD;
            set
            {
                //DebugTAC_AI.Assert("MovementAIControllerDirty set " + value);
                MCD = value;
            }
        }
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

                DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + tank.name + " Setup for DesignMemory (" + context + ")");
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
                    return !tank.TechIsActivePlayer();
                }
                else
                    return false;

            }
        }
        public bool Allied => AIAlign == AIAlignment.Player;
        public bool IsPlayerControlled => AIAlign == AIAlignment.PlayerNoAI || AIAlign == AIAlignment.Player;
        public bool ActuallyWorks => hasAI || tank.PlayerFocused;
        public bool SetToActive => lastAIType != AITreeType.AITypes.Idle;
        public bool NotInBeam => BeamTimeoutClock == 0;
        public bool CanCopyControls => !IsMultiTech || tank.PlayerFocused;
        public bool CanUseBuildBeam => !(tank.IsAnchored && !PlayerAllowAutoAnchoring);
        public bool CanAutoAnchor => AutoAnchor && PlayerAllowAutoAnchoring && !AttackEnemy && tank.Anchors.NumPossibleAnchors > 0 
            && tank.Anchors.NumIsAnchored == 0 && DelayedAnchorClock >= AIGlobals.BaseAnchorMinimumTimeDelay && CanAnchorNow;
        public bool CanAutoUnanchor => AutoAnchor && PlayerAllowAutoAnchoring && tank.Anchors.NumIsAnchored > 0;
        public bool CanAnchorNow => CanAttemptAnchor && CanAnchorSafely;
        public bool CanAnchorSafely => !lastEnemyGet || (lastEnemyGet && lastCombatRange > AIGlobals.SafeAnchorDist);
        public bool CanAttemptAnchor => anchorAttempts <= AIGlobals.MaxAnchorAttempts;
        public bool MovingAndOrHasTarget => tank.IsAnchored ? lastEnemyGet : DriverType == AIDriverType.Pilot ||
            (DriveDirDirected > EDriveFacing.Neutral && (ThrottleState == AIThrottleState.ForceSpeed || DoSteerCore));
        public bool UsingPathfinding => ControlCore.DrivePathing >= EDrivePathing.Path;

        // Settables in ModuleAIExtension - "turns on" functionality on the host Tech, none of these force it off
        /// <summary> Should the other mimic AIs ignore controls from this Tech? 
        /// Additionally when anchored, ignore collision with this Tech? </summary>
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
        /// <summary> Spacing: The range the AI will linger from the enemy while attacking if PursueThreat is true </summary>
        public float MinCombatRange => AISetSettings.CombatSpacing;
        /// <summary> Chase: How far should we pursue the enemy? </summary>
        public float MaxCombatRange => AISetSettings.CombatChase;
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
        public AIRunState RunState {
            get => _RunState;
            set
            {
                if (_RunState != value)
                {
                    switch (value)
                    {
                        case AIRunState.Off:
                        case AIRunState.Default:
                        case AIRunState.Advanced:
                            break;
                        default:
                            throw new InvalidOperationException("TankAIHelper.RunState set to invalid state " + value);
                    }
                    _RunState = value;
                }
            }
        }
        private AIRunState _RunState = AIRunState.Advanced;      // Disable the AI to make way for Default AI


        /// <summary>
        /// 0 is off, 1 is enemy, 2 is obsticle
        /// </summary>
        public AIWeaponState ActiveAimState = AIWeaponState.Normal;
        public AIWeaponType WeaponAimType = AIWeaponType.Unknown;
        public void ResetToNormalAimState()
        {
            SuppressFiring(false);
            WeaponState = AIWeaponState.Normal;
            ActiveAimState = AIWeaponState.Normal;
        }
        public bool NeedsLineOfSight => WeaponAimType == AIWeaponType.Direct;
        public bool BlockedLineOfSight = false;

        public AIAlignment AIAlign = AIAlignment.Static;             // 0 is static, 1 is ally, 2 is enemy
        public AIWeaponState WeaponState = AIWeaponState.Normal;    // 0 is sleep, 1 is target, 2 is obsticle, 3 is mimic
        public bool UpdateDirectorsAndPathing = false;       // Collision avoidence active this FixedUpdate frame?
        public bool UsingAirControls = false; // Use the not-VehicleAICore cores
        internal int FrustrationMeter = 0;  // tardiness buildup before we use our guns to remove obsticles
        internal float Urgency = 0;         // tardiness buildup before we just ignore obstructions
        internal float UrgencyOverload = 0; // builds up too much if our max speed was set too high

        /// <summary>
        /// Repairs requested?
        /// </summary>
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
        }
        // */ public bool PendingDamageCheck = true;

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
        internal float lastPathPointRange = 0;
        public float NextFindTargetTime = 0;      // Updates to wait before target swatching

        //AutoCollection
        internal bool hasAI = false;    // Has an active AI module
        /// <summary>
        /// Set to dirty when we make any changes to the AI
        /// </summary>
        internal AIDirtyState dirtyAI = AIDirtyState.Not;  // Update Player AI state if needed
        public enum AIDirtyState
        {
            Not,
            /// <summary>Reboots the AI if it just changed alignment</summary>
            Dirty,
            /// <summary>Forces the AI to reboot as if it was just loaded into the world, very costly.</summary>
            DirtyAndReboot,
        }
        internal bool dirtyExtents = false;    // The Tech has new blocks attached recently

        internal float EstTopSped = 0;
        internal float recentSpeed = 1;
        private int anchorAttempts = 0;
        internal float lastTechExtents = 1;
        internal float lastAuxVal = 0;
        public Visible lastPlayer;
        public Visible lastEnemyGet { get => lastEnemy; }
        internal Visible lastEnemy { get; set; } = null;
        public bool PreserveEnemyTarget => RTSControlled && RTSDestInternal == RTSDisabled;
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
        internal IAIFollowable lastBasePos;
        internal bool foundBase = false;
        internal bool foundGoal = false;

        // MultiTech AI Handling
        internal HashSet<Tank> MultiTechsAffiliated = new HashSet<Tank>();
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
        public float GroundOffsetHeight = AIGlobals.GroundOffsetGeneralAir;           // flote above ground this dist
        internal Snapshot lastBuiltTech = null;
        internal Vector3 PathPoint => MovementController.PathPoint;

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
        internal bool IsDirectedMovingFromDest => ControlOperator.DriveDest == EDriveDest.FromLastDestination || 
            (ThrottleState == AIThrottleState.ForceSpeed && DriveVar < -0.01f);

        /// <summary> Drive direction </summary>
        internal EDriveFacing DriveDirDirected => ControlOperator.DriveDir;
        /// <summary> Move to a dynamic target </summary>
        internal EDriveDest DriveDestDirected => ControlOperator.DriveDest;
        private EControlCoreSet ControlCore = EControlCoreSet.Default;
        public string GetCoreControlString()
        {
            return ControlCore.ToString();
        }
        public void SetCoreControlStop()
        {
            SetCoreControl(EControlCoreSet.Default);
        }
        public void SetCoreControl(EControlCoreSet cont)
        {
            ControlCore = cont;
        }

        /// <summary> Do we steer to target destination? </summary>
        internal bool DoSteerCore => ControlCore.DriveDir > EDriveFacing.Neutral;

        /// <summary> Drive AWAY from target </summary>
        internal bool AdviseAwayCore => ControlCore.DriveDest == EDriveDest.FromLastDestination;

        //Finals
        /// <summary> Leave at 0 to disable automatic spacing </summary>
        public float AutoSpacing = 0;              // Minimum radial spacing distance from destination
        public float DriveVar { get; set; } = 0; // Forwards drive (-1, 1)
        public float GetDrive => MovementController.GetDrive;

        public AIThrottleState ThrottleState { get; set; } = AIThrottleState.FullSpeed;
        /// <summary> SHOULD WE FIRE GUNS </summary>
        public bool AttackEnemy = false;// Enemy nearby?
        public bool AvoidStuff { get; internal set; } = true;            // Try avoiding allies and obsticles
        /*
        internal bool AvoidStuff {
            get { return _AvoidStuff; }
            set {
                if (!value)
                    DebugTAC_AI.Log("AvoidStuff disabled by: " + StackTraceUtility.ExtractStackTrace().ToString());
                _AvoidStuff = value;
            }
        }*/

        internal AIAnchorState AnchorState = AIAnchorState.None;
        internal bool AnchorStateAIInsure = false;

        public bool FIRE_ALL { get; internal set; } = false;   // hold down tech's spacebar
        internal bool FullBoost = false;            // hold down boost button
        internal bool LightBoost = false;           // moderated booster pressing
        internal bool FirePROPS = false;            // hold down prop button
        internal bool ForceSetBeam = false;         // activate build beam
        public bool CollectedTarget = false;      // this Tech's storage objective status (resources, blocks, energy)
        public bool Retreat { get; internal set; } = false;              // ignore enemy position and follow intended destination (but still return fire)

        public bool Avoiding { get; internal set; } = false;             // We are currently avoiding something
        public bool IsTryingToUnjam { get; internal set; } = false;      // Is this tech unjamming?
        public bool PendingHeightCheck
        {// Queue a driving depth check for a naval tech - or any tech that really needs this lol

            get => _LowestPointOnTech == -1;
            set
            {
                if (value)
                    _LowestPointOnTech = -1;
                else if (_LowestPointOnTech == -1)
                    _LowestPointOnTech = 0;
            }
        }
        public float LowestPointOnTech
        {// The lowest point in relation to the tech's block-based center
            get
            {
                if (_LowestPointOnTech == -1)
                    GetLowestPointOnTech();
                return _LowestPointOnTech;
            }
            set => _LowestPointOnTech = value;
        }
        private float _LowestPointOnTech = 0;       // the lowest point in relation to the tech's block-based center
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
        /// <summary>
        /// FALSE when RTS mode is following an enemy or obsticle
        /// </summary>
        public bool IsGoingToPositionalRTSDest => RTSDestInternal != RTSDisabled;
        public static IntVector3 RTSDisabled => AIGlobals.RTSDisabled;
        public ManWorldRTS.CommandLink RTSCommand = default;
        public IntVector3 RTSDestination
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
                        DebugTAC_AI.LogWarnPlayerOnce("RTSDestination Server update Critical error", e);
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

        /// <summary>
        /// The position we use when drawing the lines for the RTS UI
        /// </summary>
        public Vector3 DriveTargetLocation
        {
            get
            {
                if (RTSControlled && IsGoingToPositionalRTSDest)
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

        public static bool OverrideAndFlyAway(TankAIHelper helper, ExtControlStatus control)
        {
            if (control == ExtControlStatus.MaintainersAndDirectors)
            {
                if (helper.DriverType != AIDriverType.Astronaut)
                {
                    var enemy = helper.GetComponent<EnemyMind>();
                    if (enemy)
                        enemy.EvilCommander = EnemyHandling.Starship;
                    helper.SetDriverType(AIDriverType.Astronaut);
                }
                helper.Unanchor();
                helper.MaxBoost();
                return false;
            }
            return true;
        }
        public enum ExtControlStatus
        {
            Operators,
            MaintainersAndDirectors,
            Recycle,
        }
        /// <summary>
        /// Force the tech to be controlled by external means.  
        /// Returning true lets the AI in this mod OVERRIDE what you put in!.
        /// </summary>
        public Func<TankAIHelper, ExtControlStatus, bool> AIControlOverride = null;
        public bool PlayerAllowAutoAnchoring = false;   // Allow auto-anchor
        /// <summary> Set the AI back to Escort next update </summary>
        public bool ExpectAITampering = false;


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
            MovementAIControllerDirty = true;
            AIECore.AddHelper(this);
            ResetAISettings();
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

                if (DriverType == AIDriverType.AutoSet)
                    ExecuteAutoSetNoCalibrate();
                else
                    SetDriverType(DriverType);
                /*
                if (tank.AI.TryGetCurrentAIType(out var aiGet) && aiGet != AITreeType.AITypes.Idle)
                {
                    ForceAllAIsToEscort(true, false);
                    //ForceRebuildAlignment();
                }*/
                dirtyAI = AIDirtyState.Dirty;
                dirtyExtents = true;
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("DelayedSubscribe Critical error", e);
            }
        }

        public void ResetAISettings()
        {
            AILimitSettings.Recalibrate();
            AISetSettings = new AISettingsSet(AILimitSettings);
        }
        private void OnBlockAttached(TankBlock newBlock, Tank tank)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": On Attach " + tank.name);
            EstTopSped = 1;
            //LastBuildClock = 0;
            PendingHeightCheck = true;
            dirtyExtents = true;
            dirtyAI = AIDirtyState.Dirty;
            if (AIAlign == AIAlignment.Player)
            {
                try
                {
                    if (!tank.FirstUpdateAfterSpawn && !PendingDamageCheck && TechMemor)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Saved TechMemor for " + tank.name);
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
        private void OnBlockDetaching(TankBlock removedBlock, Tank tank)
        {
            EstTopSped = 1;
            recentSpeed = 1;
            PendingHeightCheck = true;
            PendingDamageCheck = true;
            dirtyExtents = true;
            if (AIAlign == AIAlignment.Player)
            {
                try
                {
                    removedBlock.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                }
                catch { }
                dirtyAI = AIDirtyState.Dirty;
            }
        }
        internal void Recycled()
        {
            DropBlock();
            AttackEnemy = false;
            lastSuppressedState = false;
            ResetToNormalAimState();
            FinishedRepairEvent.EnsureNoSubscribers();
            maxBlockCount = 0;
            DamageThreshold = 0;
            PlayerAllowAutoAnchoring = false;
            isRTSControlled = false;
            DriverType = AIDriverType.AutoSet;
            AttackMode = EAttackMode.AutoSet;
            MovementAIControllerDirty = true;
            DediAI = AIType.Escort;
            NextFindTargetTime = 0;
            RemoveBookmarkBuilder();
            if (TechMemor.IsNotNull())
            {
                TechMemor.Remove();
                TechMemor = null;
            }
            ResetOnSwitchAlignments(null);
            ResetAISettings();
            enabled = false;
        }

        public void SetRTSState(bool RTSEnabled)
        {
            RTSControlled = RTSEnabled;
            foreach (ModuleAIExtension AIEx in AIList)
            {
                if (AIEx)
                    AIEx.RTSActive = isRTSControlled;
                else
                    DebugTAC_AI.Log(KickStart.ModID + ": NULL ModuleAIExtension IN " + tank.name);
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
                    DebugTAC_AI.Log(KickStart.ModID + ": Host changed AI");
                    //DebugTAC_AI.Log(KickStart.ModID + ": Anonymous sender error");
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
                            Unanchor();
                            PlayerAllowAutoAnchoring = true;
                        }
                        else
                        {
                            TryInsureAnchor();
                            PlayerAllowAutoAnchoring = false;
                        }
                        DriverType = driver;
                    }
                    MovementAIControllerDirty = true;

                    //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(tank);
                    //overlay.Update();
                }
                else
                    DebugTAC_AI.Log(KickStart.ModID + ": TrySetAITypeRemote - Invalid request received - player tried to change AI of Tech that wasn't theirs");
            }
            else
                DebugTAC_AI.Log(KickStart.ModID + ": TrySetAITypeRemote - Invalid request received - Tried to change AI type when not connected to a server!? \n  The UI handles this automatically!!!\n" + StackTraceUtility.ExtractStackTrace());
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

        private void ReValidateAI()
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
            DebugTAC_AI.Info(KickStart.ModID + ": AI list for Tech " + tank.name + " has " + AIList.Count() + " entries");
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

            MovementAIControllerDirty = true;

            if (AttackMode == EAttackMode.AutoSet)
                AttackMode = EWeapSetup.GetAttackStrat(tank, this);
        }
        /// <summary>
        /// Does not remove EnemyMind
        /// </summary>
        public void RefreshAI()
        {
            AvoidStuff = true;
            UsingAirControls = false;
            RunState = AIRunState.Advanced;
            MultiTechsAffiliated.Clear();

            ReValidateAI();

            ProcessControl(Vector3.zero, Vector3.zero, Vector3.zero, false, false);
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
                DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is considered an Anchored Tech with the given conditions and will auto-anchor.");
                if (!tank.IsAnchored)
                {
                    TryAnchor();
                    ForceAllAIsToEscort();
                }
            }*/
        }
        /// <summary>
        /// ONLY CALL when we are actually switching alignments or recycling
        /// </summary>
        public void ResetOnSwitchAlignments(Tank unused)
        {
            DebugTAC_AI.Assert(MovementController == null, "MovementController is null.  How is this possible?!");
            //DebugTAC_AI.Log(KickStart.ModID + ": Resetting all for " + tank.name);
            maxBlockCount = tank.blockman.blockCount;
            AttackEnemy = false;
            lastSuppressedState = false;
            lastAIType = AITreeType.AITypes.Idle;
            AttackMode = EAttackMode.AutoSet;
            dirtyExtents = true;
            dirtyAI = AIDirtyState.Dirty;
            PlayerAllowAutoAnchoring = !tank.IsAnchored;
            ExpectAITampering = false;
            GroundOffsetHeight = AIGlobals.GroundOffsetGeneralAir;
            Provoked = 0;
            ActionPause = 0;
            KeepEnemyFocus = false;
            MultiTechsAffiliated.Clear();

            AIAlign = AIAlignment.Static;
            RunState = AIRunState.Advanced;
            AnchorState = AIAnchorState.None;
            AnchorStateAIInsure = false;
            WeaponAimType = AIWeaponType.Unknown;
            BlockedLineOfSight = false;
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
            //DebugTAC_AI.LogSpecific(tank, "Target released ResetOnSwitchAlignments()1");
            lastLockOnTarget = null;
            lastCloseAlly = null;
            theBase = null;
            theResource = null;
            IsTryingToUnjam = false;
            DropBlock();
            isRTSControlled = false;
            RTSDestInternal = RTSDisabled;
            lastTargetGatherTime = 0;
            ChaseThreat = true;
            tank.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            World.ManWorldRTS.ReleaseControl(this);
            var Funds = tank.gameObject.GetComponent<RLoadedBases.EnemyBaseFunder>();
            if (Funds.IsNotNull())
                Funds.OnRecycle(tank);
            var Mem = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
            if (Mem.IsNotNull() && !BookmarkBuilder.Exists(tank))
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

            if (DriverType == AIDriverType.AutoSet)
                DriverType = AIECore.HandlingDetermine(tank, this);
            MovementAIControllerDirty = true;

            ProcessControl(Vector3.zero, Vector3.zero, Vector3.zero, false, false);
            tank.control.SetBeamControlState(false);
            tank.control.FireControl = false;

            ResetToNormalAimState();

            //enabled = false; // why the heck did I put this here? this is WHY EVERYTHING WAS BROKEN

            //TankDescriptionOverlay overlay = (TankDescriptionOverlay)GUIAIManager.bubble.GetValue(tank);
            //overlay.Update();
        }

        private void SetupDefaultMovementAIController()
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

        private bool RecalMoveAIControllerNPT(EnemyMind enemy)
        {
            if (enemy.IsNotNull())
            {
                if ((enemy.EvilCommander == EnemyHandling.Stationary || enemy.StartedAnchored) && AnchorState != AIAnchorState.Unanchor)
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
                if (enemy.EvilCommander == EnemyHandling.Chopper || enemy.EvilCommander == EnemyHandling.Airplane)
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
                    return false;
                }
                return true;
            }
            else
                throw new Exception("RecalibrateMovementAIController for " + tank.name + " was NonPlayer but no EnemyMind present!");
        }
        private bool RecalMoveAIControllerPlayer()
        {
            if (DriverType == AIDriverType.Stationary && AnchorState != AIAnchorState.Unanchor)
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
                return false;
            }
            return true;
        }
        /// <summary>
        /// Automatically sets the movement controller.  MUST be automatically done once we are ABSOLUTELY SURE
        ///   we have selected a new DriverType!
        /// Was previously: TestForFlyingAIRequirement
        /// </summary>
        /// <returns>True if the AI can fly</returns>
        private void RecalibrateMovementAIController()
        {
            try
            {
                //DebugTAC_AI.Assert("RecalibrateMovementAIController for " + tank.name + ", type " + DriverType);
                UsingAirControls = false;
                var enemy = gameObject.GetComponent<EnemyMind>();
                if (AIAlign == AIAlignment.NonPlayer)
                {
                    if (!RecalMoveAIControllerNPT(enemy))
                        return;
                }
                else
                {
                    if (!RecalMoveAIControllerPlayer())
                        return;
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
                return;
            }
            finally
            {
                MovementAIControllerDirty = false;
            }
        }

        public void ExecuteAutoSet()
        {
            ExecuteAutoSetNoCalibrate();
            MovementAIControllerDirty = true;
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
                    DebugTAC_AI.LogError(KickStart.ModID + ": Encountered illegal AIDriverType on Allied AI Driver HandlingDetermine!");
                    break;
            }
            DebugTAC_AI.Log(KickStart.ModID + ": ExecuteAutoSetNoCalibrate() " + tank.name + " guessing driver is " + DriverType);
        }

        /// <summary>
        /// React when hit by an attack from another Tech. 
        /// Must be un-subbed and resubbed when switching to and from enemy
        /// </summary>
        /// <param name="dingus"></param>
        internal void OnHit(ManDamage.DamageInfo dingus)
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
                if (ManWorldRTS.PlayerIsInRTS)
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
        internal void OnSwitchAI(bool resetRTSstate)
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
            MovementAIControllerDirty = true;
            //World.PlayerRTSControl.ReleaseControl(this);
        }
        public void SetAIControl(AITreeType.AITypes type)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": ForceAllAIsToEscort() - Setting AIType to " + type);
            tank.AI.SetBehaviorType(type);
            //DebugTAC_AI.Log(KickStart.ModID + ": ForceAllAIsToEscort() - Set AIType");
        }
        public void ForceAllAIsToEscort(bool Do)
        {
            //Needed to return AI mode back to Escort on unanchor as unanchoring causes it to go to idle
            //DebugTAC_AI.Log(KickStart.ModID + ": ForceAllAIsToEscort()");
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
                        SetAIControl(AITreeType.AITypes.Escort);
                        lastAIType = AITreeType.AITypes.Escort;
                    }
                    //DebugTAC_AI.Log(KickStart.ModID + ": ForceAllAIsToEscort() - Getting AIType");
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes type))
                        DebugTAC_AI.Info(KickStart.ModID + ": AI type is " + type.ToString());
                    //DebugTAC_AI.Log(KickStart.ModID + ": ForceAllAIsToEscort() - Got AIType");
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
                        SetAIControl(AITreeType.AITypes.Idle);
                        lastAIType = AITreeType.AITypes.Idle;
                    }
                }
                dirtyAI = AIDirtyState.Dirty;
                MovementAIControllerDirty = true;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        public void WakeAIForChange(bool immedeateRebuildAlignment = false)
        {
            ForceAllAIsToEscort(true);
            MovementAIControllerDirty = true;
            if (immedeateRebuildAlignment)
                ForceRebuildAlignment();
        }


        // ----------------------------  GUI Formatter  ---------------------------- 
        internal string GetActionStatus(out bool cantDo)
        {
            cantDo = false;
            if (tank.IsPlayer)
            {
                if (!KickStart.AutopilotPlayer)
                    return "(Autopilot Disabled)";
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
            if (Retreat && !IsMultiTech)
            {
                if (tank.IsAnchored)
                    return "Holding the line!";
                return "Retreat!";
            }
            string output = "At Destination";
            try
            {
                if (RTSControlled)
                {
                    GetActionOperatorsPositional(ref output, ref cantDo);
                }
                else
                {
                    if (AIAlign == AIAlignment.NonPlayer)
                    {
                        GetActionOperatorsNonPlayer(ref output, ref cantDo);
                    }
                    else
                    {
                        GetActionOperatorsAllied(ref output, ref cantDo);
                    }
                }
                if (AIGlobals.ShowDebugFeedBack)
                {
                    output =  "[" + DriverType + "]\n" + output + "\nDirect [" + ControlCore.DriveDir + "], Dest [" + ControlCore.DriveDest +
                        "]\nWeaponState [" + WeaponState +
                        "]\nMinRange [" + AutoSpacing.ToString("0.000") +
                        (ThrottleState == AIThrottleState.ForceSpeed ? ("]\nDrive[F] [" + DriveVar.ToString("0.000") + ", " +
                        GetDrive.ToString("0.000")) :
                        ("]\nDrive [" + GetDrive.ToString("0.000"))) +
                        "]\nThrottle [" + ThrottleState + "]";
                }
            }
            catch
            {
                output = "Loading...";
            }
            return output;
        }
        private void GetActionOperatorsPositional(ref string output, ref bool cantDo)
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
                    {
                        output = "Fighting " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    }
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
                    AIControllerAir Air = MovementController as AIControllerAir;
                    if (Air.AICore is AirplaneAICore plane)
                    {
                        if (plane.PerformDiveAttack > 0)
                        {
                            switch (plane.PerformDiveAttack)
                            {
                                case 1:
                                    output += "\nDive - turn to face target";
                                    break;
                                case 2:
                                    output += "\nDive - diving!";
                                    break;
                                default:
                                    output += "\nDive - Code [" + plane.PerformDiveAttack + "]";
                                    break;
                            }
                        }
                        if (plane.PerformUTurn > 0)
                        {
                            switch (plane.PerformUTurn)
                            {
                                case 1:
                                    output += "\nU-Turn - facing from target";
                                    break;
                                case 2:
                                    output += "\nU-Turn - point upwards";
                                    break;
                                case 3:
                                    output += "\nU-Turn - turn to face target";
                                    break;
                                default:
                                    output += "\nU-Turn - Code [" + plane.PerformUTurn + "]";
                                    break;
                            }
                        }
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
        private void GetActionOperatorsAllied(ref string output, ref bool cantDo)
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
                        if ((bool)theResource?.tank)
                            output = "Copying Player";
                        else
                        {
                            cantDo = true;
                            output = "Searching for Player";
                        }
                    }
                    else
                    {
                        if ((bool)theResource?.tank)
                            output = "Copying " + (theResource.name.NullOrEmpty() ? "unknown" : theResource.name);
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
        private void GetActionOperatorsNonPlayer(ref string output, ref bool cantDo)
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
        private void GetActionOperatorsNonPlayerCombat(EnemyMind mind, ref string output, ref bool cantDo)
        {
            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    if (BlockedLineOfSight)
                        output = "Hiding from Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else if (ControlCore.DriveDest == EDriveDest.ToLastDestination)
                        output = "Moving to " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                        output = "Running from " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    break;
                case EAttackMode.Ranged:
                    if (BlockedLineOfSight)
                        output = "Lining up on Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else if (ControlCore.DriveDest == EDriveDest.ToLastDestination)
                        output = "Closing in on Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else
                        output = "Spacing from Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    break;
                default:
                    if (BlockedLineOfSight)
                        output = "Finding Target " + (lastEnemyGet.name.NullOrEmpty() ? "unknown" : lastEnemyGet.name);
                    else if (ControlCore.DriveDest == EDriveDest.ToLastDestination)
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
                return LocalSafeVelocity.z;
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
        public bool IsTechMovingAbs(float minSpeed)
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
                return Mathf.Abs(LocalSafeVelocity.z) > minSpeed || Mathf.Abs(GetDrive) < 0.5f;
            }
        }
        /// <summary>
        /// Note when we AREN'T 3D navigating, we take the forwards VELOCITY of the tech, 
        ///   so reversing results in NEGATIVE VALUE
        /// </summary>
        public bool IsTechMovingSigned(float minSpeed)
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
                if (minSpeed < 0)
                    return LocalSafeVelocity.z < minSpeed || Mathf.Abs(GetDrive) < 0.5f;
                return LocalSafeVelocity.z > minSpeed || Mathf.Abs(GetDrive) < 0.5f;
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
                return Mathf.Abs(LocalSafeVelocity.z) > minSpeed;
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
                    DebugTAC_AI.Log(KickStart.ModID + ": The Tech's Team: " + tank.Team + " | RTS Mode: " + RTSControlled);
                    foreach (Tank thatTech in ManNetwork.inst.GetAllPlayerTechs())
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": " + thatTech.name + " | of " + thatTech.netTech.Team);
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
        private void GetLowestPointOnTech()
        {
            float lowest = 0;
            IEnumerable<IntVector3> lowCells = tank.blockman.GetLowestOccupiedCells();
            Quaternion forwardGrid = tank.rootBlockTrans.localRotation;
            foreach (IntVector3 intVector in lowCells)
            {
                Vector3 cellPosLocal = forwardGrid * intVector;
                if (cellPosLocal.y < lowest)
                    lowest = cellPosLocal.y;
            }
            DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  lowest point set " + lowest);
            _LowestPointOnTech = lowest;
            PendingHeightCheck = false;
        }
        public bool TestIsLowestPointOnTech(TankBlock block)
        {
            bool isTrue = false;
            if (block == null)
                return false;
            Quaternion forward = AIGlobals.LookRot(tank.rootBlockTrans.forward, tank.rootBlockTrans.up);
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
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  lowest point set " + LowestPointOnTech);
            }
            return isTrue;
        }


        // ----------------------------  Primary Operations  ---------------------------- 
        /// <summary>
        /// Controls the Tech.  Main interface for ALL AI Tech Controls(excluding Neutral)
        /// Returns true for all cases this AI fully takes over control
        /// </summary>
        /// <param name="thisControl"></param>
        public bool ControlTech(TankControl thisControl)
        {
            if (ManNetwork.IsNetworked)
            {
                if (ManNetwork.IsHost)
                {
                    if (tank.TechIsActivePlayer())
                    {
                        if (Singleton.playerTank == tank && RTSControlled)
                        {
                            UpdateTechControl(thisControl);
                            return true;
                        }
                        else
                            SuppressFiring(false);
                    }
                    else
                    {
                        if (tank.FirstUpdateAfterSpawn)
                        {
                            if (!tank.IsAnchored && AutoAnchor)
                            {
                                AnchorIgnoreChecks(true);
                            }
                            // let the icon update
                        }
                        else if (AIControlOverride != null && !AIControlOverride(this, ExtControlStatus.MaintainersAndDirectors))
                        {   // override EVERYTHING
                            return true;
                            //return false;
                        }
                        else if (RunState == AIRunState.Advanced)
                        {
                            if (AIAlign == AIAlignment.Player)
                            {
                                //DebugTAC_AI.Log(KickStart.ModID + ": AI Valid!");
                                //DebugTAC_AI.Log(KickStart.ModID + ": (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                                //tankAIHelp.AIState &&
                                if (SetToActive)
                                {
                                    //DebugTAC_AI.Log(KickStart.ModID + ": Running BetterAI");
                                    //DebugTAC_AI.Log(KickStart.ModID + ": Patched Tank ExecuteControl(TankAIHelper)");
                                    UpdateTechControl(thisControl);
                                    return true;
                                }
                            }
                            else if (AIAlign == AIAlignment.NonPlayer)// && KickStart.enablePainMode)
                            {   // This should turn off ONLY for land enemy AI!
                                UpdateTechControl(thisControl);
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    if (KickStart.AllowPlayerRTSHUD && KickStart.AutopilotPlayer && Singleton.playerTank == tank)
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
                if (!tank.PlayerFocused || KickStart.AutopilotPlayer)
                {
                    if (tank.FirstUpdateAfterSpawn)
                    {
                        if (tank.GetComponent<RequestAnchored>())
                        {
                            AnchorIgnoreChecks();
                        }
                        // let the icon update
                    }
                    else if (AIControlOverride != null && AIControlOverride(this, ExtControlStatus.MaintainersAndDirectors))
                    {   // override EVERYTHING
                        return true;
                    }
                    else if (RunState == AIRunState.Advanced)
                    {
                        if (AIAlign == AIAlignment.Player)
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI Valid!");
                            //DebugTAC_AI.Log(KickStart.ModID + ": (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                            if (tank.PlayerFocused)
                            {
                                //SetRTSState(true);
                                UpdateTechControl(thisControl);
                                return true;
                            }
                            else if (SetToActive)
                            {
                                //DebugTAC_AI.Log(KickStart.ModID + ": Running BetterAI");
                                //DebugTAC_AI.Log(KickStart.ModID + ": Patched Tank ExecuteControl(TankAIHelper)");
                                UpdateTechControl(thisControl);
                                return true;
                            }
                        }
                        else if (AIAlign == AIAlignment.NonPlayer)//KickStart.enablePainMode 
                        {   // This should turn off ONLY for land enemy AI!
                            UpdateTechControl(thisControl);
                            return true;
                        }
                    }
                }
            }
            SuppressFiring(false);
            return false;
        }
        private void UpdateTechControl(TankControl thisControl)
        {   // The interface method for actually handling the tank - note that this fires at a different rate
            if (AIControlOverride != null && !AIControlOverride(this, ExtControlStatus.MaintainersAndDirectors))
                return;
            CurHeight = -500;

            if (MovementController is null)
            {
                DebugTAC_AI.Log("NULL MOVEMENT CONTROLLER");
            }

            AIEBeam.BeamMaintainer(thisControl, this, tank);
            if (UpdateDirectorsAndPathing)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Fired CollisionAvoidUpdate!");
                try
                {
                    AIEWeapons.WeaponDirector(thisControl, this, tank);
                }
                catch (Exception e)
                {
                    DebugTAC_AI.LogWarnPlayerOnce("AI " + tank.name + ": WeaponDirector error", e);
                }

                try
                {
                    if (!IsTryingToUnjam)
                    {
                        Avoiding = false;
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
                    DebugTAC_AI.LogWarnPlayerOnce("AI " + tank.name + ": DriveDirector error", e);
                }

                UpdateDirectorsAndPathing = false; // incase they fall out of sync
            }
            if (NotInBeam)
            {
                try
                {
                    AIEWeapons.WeaponMaintainer(this, tank);
                }
                catch (Exception e)
                {
                    DebugTAC_AI.LogWarnPlayerOnce("AI " + tank.name + ":  WeaponMaintainer error", e);
                }
                try
                {
                    MovementController.DriveMaintainer(ref ControlCore);
                }
                catch (Exception e)
                {
                    DebugTAC_AI.LogWarnPlayerOnce("AI " + tank.name + ": DriveMaintainer error", e);
                }
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

            CheckTryRepairAllied();

            BoltsFired = false;
            Attempt3DNavi = false;
            if (ActionPause > 0)
                ActionPause -= KickStart.AIClockPeriod;

            if (tank.PlayerFocused)
            {
                //updateCA = true;
                if (KickStart.AllowPlayerRTSHUD)
                {
#if DEBUG
                        if (ManWorldRTS.PlayerIsInRTS && ManWorldRTS.DevCamLock == DebugCameraLock.LockTechToCam)
                        {
                            if (tank.rbody)
                            {
                                tank.rbody.MovePosition(Singleton.cameraTrans.position + (Vector3.up * 75));
                                return;
                            }
                        }
#endif
                    if (KickStart.AutopilotPlayer)
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
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Fired DelayedUpdate!");

                //updateCA = true;
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  current mode " + DediAI.ToString());

                DetermineCombat();

                if (RTSControlled && !IsMultiTech)
                {   //Overrides the Allied Operations for RTS Use
                    RunRTSNavi(); // need to put a flagger FOR multitech ai - DID
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
            DetermineCombatEnemy();
            if (light)
                RCore.BeEvilLight(this, tank);
            else
            {
                RCore.BeEvil(this, tank);
            }
        }

        /// <summary>
        /// Note to self: fix this mess that interferes with everything, but correctly blocks the call of OpsController.Execute() (it doesn't support the RTS AI)
        /// </summary>
        /// <param name="isPlayerTech"></param>
        private void RunRTSNavi(bool isPlayerTech = false)
        {   // Alternative Operator for RTS

            //ProceedToObjective = true;
            EControlOperatorSet direct = GetDirectedControl();
            if (DriverType == AIDriverType.Pilot)
            {
                if (DediAI == AIType.Escort && !IsGoingToPositionalRTSDest && 
                    (lastEnemyGet == null || !lastEnemyGet.isActive))
                    RTSDestination = tank.boundsCentreWorldNoCheck;

                GetDistanceFromTask2D(lastDestinationCore, 0);
                //lastOperatorRange = (DodgeSphereCenter - lastDestinationCore).magnitude;
                Attempt3DNavi = true;
                BGeneral.ResetValues(this, ref direct);
                AvoidStuff = true;

                float range = (MaxObjectiveRange * 4) + lastTechExtents;
                // The range is nearly quadrupled here due to dogfighting conditions
                direct.DriveDest = EDriveDest.ToLastDestination;
                if (AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius) ||
                    AIEPathing.ObstructionAwarenessTerrain(DodgeSphereCenter, this, DodgeSphereRadius))
                    ThrottleState = AIThrottleState.Yield;
                if (lastEnemyGet != null && lastEnemyGet.isActive)
                {
                    direct.SetLastDest(lastEnemyGet.tank.boundsCentreWorldNoCheck);
                }


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
                GetDistanceFromTask(lastDestinationCore);
                bool needsToSlowDown = IsOrbiting();

                Attempt3DNavi = DriverType == AIDriverType.Astronaut;
                BGeneral.ResetValues(this, ref direct);
                AvoidStuff = true;
                if (needsToSlowDown || AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius)
                    || AIEPathing.ObstructionAwarenessSetPieceAny(DodgeSphereCenter, this, DodgeSphereRadius))
                    ThrottleState = AIThrottleState.Yield;
                if (DediAI == AIType.Escort && !IsGoingToPositionalRTSDest &&
                    (lastEnemyGet == null || !lastEnemyGet.isActive))
                {
                    direct.STOP(this);
                    BGeneral.RTSCombat(this, tank);
                    SetDirectedControl(direct);
                    return;
                }

                bool MoveQueue = ManWorldRTS.HasMovementQueue(this);
                direct.DriveToFacingTowards();
                if (lastOperatorRange < (lastTechExtents * 2) + 32 && !MoveQueue)
                {
                    //Things are going smoothly
                    SettleDown();
                    ThrottleState = AIThrottleState.PivotOnly;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  RTS - resting");
                    if (DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                        DelayedAnchorClock++;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": " + AutoAnchor + " | " + PlayerAllowAnchoring + " | " + (tank.Anchors.NumPossibleAnchors >= 1) + " | " + (DelayedAnchorClock >= AIGlobals.BaseAnchorMinimumTimeDelay) + " | " + !DANGER);
                    if (CanAutoAnchor)
                    {
                        if (!tank.IsAnchored)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Setting camp!");
                            TryInsureAnchor();
                        }
                    }
                }
                else
                {   // Time to go!
                    anchorAttempts = 0;
                    DelayedAnchorClock = 0;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  RTS - Moving");
                    if (unanchorCountdown > 0)
                        unanchorCountdown--;
                    if (AutoAnchor && PlayerAllowAutoAnchoring && tank.Anchors.NumPossibleAnchors >= 1)
                    {
                        if (tank.Anchors.NumIsAnchored > 0)
                        {
                            unanchorCountdown = 15;
                            Unanchor();
                        }
                    }
                    if (!AutoAnchor && tank.IsAnchored)
                    {
                        BGeneral.RTSCombat(this, tank);
                        SetDirectedControl(direct);
                        return;
                    }
                    if (!IsTechMovingAbs(EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                    {   //OBSTRUCTION MANAGEMENT
                        //Urgency += KickStart.AIClockPeriod / 2f;
                        //if (Urgency > 15)
                        //{
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  DOOR STUCK");
                        TryHandleObstruction(true, lastOperatorRange, false, true, ref direct);
                        //}
                    }
                    else
                    {
                        //var val = LocalSafeVelocity.z;
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Output " + val + " | TopSpeed/2 " + (EstTopSped / 2) + " | TopSpeed/4 " + (EstTopSped / 4));
                        //Things are going smoothly
                        /*
                        ThrottleState = AIThrottleState.ForceSpeed;
                        float driveVal = Mathf.Min(1, lastOperatorRange / 10);
                        DriveVar = driveVal;
                        */
                        if (MoveQueue)
                            AutoSpacing = 0;
                        else
                            AutoSpacing = Mathf.Max(lastTechExtents + 2, 0.5f);
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
            /*
            if (!KickStart.AllowStrategicAI)
            {
                RTSControlled = false;
                return;
            }*/
            switch (DediAI)
            {
                case AIType.MTTurret:
                case AIType.MTStatic:
                case AIType.MTMimic:
                    IsMultiTech = true;
                    break;
                default:
                    IsMultiTech = false;
                    break;
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
                if (AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius) ||
                    AIEPathing.ObstructionAwarenessTerrain(DodgeSphereCenter, this, DodgeSphereRadius))
                    ThrottleState = AIThrottleState.Yield;

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
                bool needsToSlowDown = IsOrbiting();

                Attempt3DNavi = mind.EvilCommander == EnemyHandling.Starship;
                AvoidStuff = true;
                bool AutoAnchor = mind.CommanderSmarts >= EnemySmarts.Meh;
                if (needsToSlowDown || AIEPathing.ObstructionAwarenessAny(DodgeSphereCenter, this, DodgeSphereRadius)
                    || AIEPathing.ObstructionAwarenessSetPieceAny(DodgeSphereCenter, this, DodgeSphereRadius))
                    ThrottleState = AIThrottleState.Yield;
                bool MoveQueue = ManWorldRTS.HasMovementQueue(this);
                if (lastOperatorRange < (lastTechExtents * 2) + 32 && !MoveQueue)
                {
                    //Things are going smoothly
                    SettleDown();
                    ThrottleState = AIThrottleState.PivotOnly;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  RTS - resting");
                    if (DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                        DelayedAnchorClock++;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": " + AutoAnchor + " | " + PlayerAllowAnchoring + " | " + (tank.Anchors.NumPossibleAnchors >= 1) + " | " + (DelayedAnchorClock >= 15) + " | " + !DANGER);
                    if (AutoAnchor && !AttackEnemy && tank.Anchors.NumPossibleAnchors >= 1
                        && DelayedAnchorClock >= AIGlobals.BaseAnchorMinimumTimeDelay && CanAnchorNow)
                    {
                        if (!tank.IsAnchored && anchorAttempts <= AIGlobals.MaxAnchorAttempts)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Setting camp!");
                            TryInsureAnchor();
                            anchorAttempts++;
                        }
                    }
                }
                else
                {   // Time to go!
                    anchorAttempts = 0;
                    DelayedAnchorClock = 0;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  RTS - Moving");
                    if (unanchorCountdown > 0)
                        unanchorCountdown--;
                    if (AutoAnchor && tank.Anchors.NumPossibleAnchors >= 1)
                    {
                        if (tank.Anchors.NumIsAnchored > 0)
                        {
                            unanchorCountdown = 15;
                            Unanchor();
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
                        //var val = LocalSafeVelocity.z;
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Output " + val + " | TopSpeed/2 " + (EstTopSped / 2) + " | TopSpeed/4 " + (EstTopSped / 4));
                        //Things are going smoothly
                        if (MoveQueue)
                            AutoSpacing = 0;
                        else
                            AutoSpacing = Mathf.Max(lastTechExtents + 2, 0.5f);
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
        internal void OnPreUpdate()
        {
            if (MovementController == null)
            {
                DebugTAC_AI.Assert(true, "MOVEMENT CONTROLLER IS NULL");
                //SetupDefaultMovementAIController();
                RecalibrateMovementAIController();
            }
            recentSpeed = GetSpeed();
            if (recentSpeed < 1)
                recentSpeed = 1;
            UpdateLastTechExtentsIfNeeded();
            CheckRebuildAlignment();
            UpdateCollectors();
        }
        internal void OnPostUpdate()
        {
            ManageAILockOn();
            UpdateBlockHold();
            RunPostOps();
            ShowCollisionAvoidenceDebugThisFrame();
        }
        private static List<Tank> TempMultiTechRecalibrate = new List<Tank>();
        private void UpdateLastTechExtentsIfNeeded()
        {//Handler for the improved AI, gets the job done.
            try
            {
                if (dirtyExtents)
                {
                    dirtyExtents = false;
                    tank.blockman.CheckRecalcBlockBounds();
                    lastTechExtents = (tank.blockBounds.size.magnitude / 2) + 2;
                    // Insure we are STILL the tracking target!
                    TempMultiTechRecalibrate.AddRange(MultiTechsAffiliated);
                    MultiTechsAffiliated.Clear();
                    foreach (var item in TempMultiTechRecalibrate)
                    {
                        if (item == null || !item.visible.isActive)
                            continue;
                        var otherHelp = item.GetHelperInsured();
                        MultiTechsAffiliated.Add(item);
                        float extendedExts = (item.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).AbsMax() + otherHelp.lastTechExtents;

                        if (extendedExts > lastTechExtents)
                            lastTechExtents = extendedExts;
                    }
                    TempMultiTechRecalibrate.Clear();
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
                    DebugTAC_AI.LogWarnPlayerOnce("UpdateLastTechExtentsIfNeeded() Critical error", e);
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
                if (MovementAIControllerDirty)
                    RecalibrateMovementAIController();
                if (RunState == AIRunState.Advanced)
                {
                    switch (AIAlign)
                    {
                        case AIAlignment.Player: // Player-Controlled techs
                            UpdateDirectorsAndPathing = true;
                            break;
                        case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                            if (KickStart.enablePainMode)
                                UpdateDirectorsAndPathing = true;
                            break;
                        default:// Static tech
                            DriveVar = 0;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                if (!updateErrored)
                {
                    DebugTAC_AI.LogWarnPlayerOnce("OnUpdateHostAIDirectors() Critical error", e);
                    updateErrored = true;
                }
            }
        }
        internal void OnUpdateHostAIOperations()
        {
            try
            {
                UpdatePhysicsInfo();
                bool OverrideControl = AIControlOverride != null;
                if (OverrideControl)
                {
                    CheckEnemyAndAiming();
                    if (!AIControlOverride(this, ExtControlStatus.Operators))
                        return;
                }
                switch (DediAI)
                {
                    case AIType.MTTurret:
                    case AIType.MTStatic:
                    case AIType.MTMimic:
                        IsMultiTech = true;
                        break;
                    default:
                        IsMultiTech = false;
                        break;
                }
                switch (AIAlign)
                {
                    case AIAlignment.Player: // Player-Controlled techs
                        if (!OverrideControl)
                            CheckEnemyAndAiming();
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
                            switch (RunState)
                            {
                                case AIRunState.Off:
                                    break;
                                case AIRunState.Default:
                                    if (!OverrideControl)
                                        CheckEnemyAndAiming();
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
                                    break;
                                case AIRunState.Advanced:
                                    if (!OverrideControl)
                                        CheckEnemyAndAiming();
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
                                    break;
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
                    //DebugTAC_AI.LogWarnPlayerOnce("OnUpdateHostAIOperations() Critical error", e);
                    updateErrored = true;
                    throw new Exception("OnUpdateHostAIOperations() Critical error", e);
                }
            }
        }

        /// <summary>
        /// MULTIPLAYER AI NON-HOST
        /// </summary>
        internal void OnUpdateClientAIDirectors()
        {
            if (MovementAIControllerDirty)
                RecalibrateMovementAIController();
            switch (AIAlign)
            {
                case AIAlignment.Static:// Static tech
                    DriveVar = 0;
                    break;
                case AIAlignment.Player: // Player-Controlled techs
                    UpdateDirectorsAndPathing = true;
                    break;
                case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                    UpdateDirectorsAndPathing = true;
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
                    if (EstTopSped < recentSpeed)
                        EstTopSped = recentSpeed;
                    break;
            }
        }

        /// <summary>
        /// CALL when we change ANYTHING in the tech's AI.
        /// </summary>
        internal void OnTechTeamChange(bool rebootSameAIAlign = false)
        {
            dirtyAI = rebootSameAIAlign ? AIDirtyState.DirtyAndReboot : AIDirtyState.Dirty;
            PlayerAllowAutoAnchoring = !tank.IsAnchored;
            ResetToNormalAimState();
        }
        internal void ForceRebuildAlignment(bool rebootSameAIAlign = false)
        {
            dirtyAI = rebootSameAIAlign ? AIDirtyState.DirtyAndReboot : AIDirtyState.Dirty;
            CheckRebuildAlignment();
        }
        private void CheckRebuildAlignment()
        {
            if (tank.blockman.blockCount == 0)
                return; // IT'S NOT READY YET
            if (dirtyAI != AIDirtyState.Not)
            {
                bool rebootSameAIAlign = dirtyAI == AIDirtyState.DirtyAndReboot;
                //DebugTAC_AI.Assert(KickStart.ModID + ": CheckRebuildAlignment() for " + tank.name);
                dirtyAI = AIDirtyState.Not;
                var aI = tank.AI;
                hasAI = aI.CheckAIAvailable();

                lastLockOnTarget = null;
                AttackEnemy = false;
                lastSuppressedState = false;
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
                                if (hasAI || (ManWorldRTS.PlayerIsInRTS && tank.PlayerFocused))
                                {
                                    //Player-Allied AI
                                    if (AIAlign != AIAlignment.Player || rebootSameAIAlign)
                                    {
                                        ResetOnSwitchAlignments(tank);
                                        RemoveEnemyMatters();
                                        AIAlign = AIAlignment.Player;
                                        RefreshAI();
                                        DebugTAC_AI.Log(KickStart.ModID + ": Allied AI " + tank.name + ":  Checked up and good to go! (NonHostClient)");
                                    }
                                }
                                else
                                {   // Static tech
                                    DriveVar = 0;
                                    if (AIAlign != AIAlignment.PlayerNoAI || rebootSameAIAlign)
                                    {   // Reset and ready for static tech
                                        DebugTAC_AI.Log(KickStart.ModID + ": PlayerNoAI Tech " + tank.name + ": reset (NonHostClient)");
                                        ResetOnSwitchAlignments(tank);
                                        RemoveEnemyMatters();
                                        AIAlign = AIAlignment.PlayerNoAI;
                                    }
                                }
                            }
                            else if (!tank.IsNeutral())
                            {
                                //Enemy AI
                                if (AIAlign != AIAlignment.NonPlayer || rebootSameAIAlign)
                                {
                                    ResetOnSwitchAlignments(tank);
                                    AIAlign = AIAlignment.NonPlayer;
                                    DebugTAC_AI.Log(KickStart.ModID + ": Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech! (NonHostClient)");
                                    RCore.GenerateEnemyAI(this, tank);
                                }
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIAlign != AIAlignment.Static || rebootSameAIAlign)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log(KickStart.ModID + ": Static Tech " + tank.name + ": reset (NonHostClient)");
                                    ResetOnSwitchAlignments(tank);
                                    RemoveEnemyMatters();
                                    AIAlign = AIAlignment.Static;
                                }
                            }
                            return;
                        }
                        else if (dirtyExtents)
                        {
                            dirtyExtents = false;
                            tank.netTech.SaveTechData();
                        }
                        if (ManSpawn.IsPlayerTeam(tank.Team))
                        {   //MP
                            if (hasAI || (ManWorldRTS.PlayerIsInRTS && tank.PlayerFocused))
                            {
                                //Player-Allied AI
                                if (AIAlign != AIAlignment.Player || rebootSameAIAlign)
                                {
                                    ResetOnSwitchAlignments(tank);
                                    RemoveEnemyMatters();
                                    AIAlign = AIAlignment.Player;
                                    RefreshAI();
                                    if ((bool)TechMemor && !BookmarkBuilder.Exists(tank))
                                        TechMemor.SaveTech();
                                    DebugTAC_AI.Log(KickStart.ModID + ": Allied AI " + tank.name + ":  Checked up and good to go!");
                                }
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIAlign != AIAlignment.PlayerNoAI || rebootSameAIAlign)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log(KickStart.ModID + ": PlayerNoAI Tech " + tank.name + ": reset");
                                    ResetOnSwitchAlignments(tank);
                                    RemoveEnemyMatters();
                                    AIEBases.SetupBookmarkBuilder(this);
                                    AIAlign = AIAlignment.PlayerNoAI;
                                }
                            }
                        }
                        else if (!tank.IsNeutral())
                        {
                            //Enemy AI
                            if (AIAlign != AIAlignment.NonPlayer || rebootSameAIAlign)
                            {
                                ResetOnSwitchAlignments(tank);
                                AIAlign = AIAlignment.NonPlayer;
                                Enemy.RCore.GenerateEnemyAI(this, tank);
                                DebugTAC_AI.Log(KickStart.ModID + ": Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech!");
                            }
                        }
                        else
                        {   // Static tech
                            DriveVar = 0;
                            if (AIAlign != AIAlignment.Static || rebootSameAIAlign)
                            {   // Reset and ready for static tech
                                DebugTAC_AI.Log(KickStart.ModID + ": Static Tech " + tank.name + ": reset");
                                ResetOnSwitchAlignments(tank);
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
                            if (hasAI || (World.ManWorldRTS.PlayerIsInRTS && tank.PlayerFocused))
                            {
                                //Player-Allied AI
                                if (AIAlign != AIAlignment.Player || rebootSameAIAlign)
                                {
                                    ResetOnSwitchAlignments(tank);
                                    RemoveEnemyMatters();
                                    AIAlign = AIAlignment.Player;
                                    RefreshAI();
                                    if ((bool)TechMemor && !BookmarkBuilder.Exists(tank))
                                        TechMemor.SaveTech();
                                    DebugTAC_AI.Log(KickStart.ModID + ": Allied AI " + tank.name + ":  Checked up and good to go!");
                                }
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIAlign != AIAlignment.PlayerNoAI || rebootSameAIAlign)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log(KickStart.ModID + ": PlayerNoAI Tech " + tank.name + ": reset");
                                    ResetOnSwitchAlignments(tank);
                                    RemoveEnemyMatters();
                                    AIEBases.SetupBookmarkBuilder(this);
                                    AIAlign = AIAlignment.PlayerNoAI;
                                }
                            }
                        }
                        else if (!tank.IsNeutral())
                        {   //MP is NOT supported!
                            //Enemy AI
                            if (AIAlign != AIAlignment.NonPlayer || rebootSameAIAlign)
                            {
                                ResetOnSwitchAlignments(tank);
                                DebugTAC_AI.Log(KickStart.ModID + ": Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech!");
                                AIAlign = AIAlignment.NonPlayer;
                                Enemy.RCore.GenerateEnemyAI(this, tank);
                            }
                        }
                        else
                        {   // Static tech
                            DriveVar = 0;
                            if (AIAlign != AIAlignment.Static || rebootSameAIAlign)
                            {   // Reset and ready for static tech
                                DebugTAC_AI.Log(KickStart.ModID + ": Static Tech " + tank.name + ": reset");
                                ResetOnSwitchAlignments(tank);
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
                        DebugTAC_AI.LogWarnPlayerOnce("RebuildAlignment() Critical error", e);
                        updateErrored = true;
                    }
                }
            }
        }

        private void RunPostOps()
        {
            if (ExpectAITampering)
            {
                WakeAIForChange();
                ExpectAITampering = false;
            }
            else
            {
                switch (AnchorState)
                {
                    case AIAnchorState.None:
                    case AIAnchorState.Anchored:
                        break;
                    case AIAnchorState.Anchor:
                    case AIAnchorState.AnchorStaticAI:
                        DoAnchor(false);
                        break;
                    case AIAnchorState.ForceAnchor:
                        DoAnchor(true);
                        break;
                    case AIAnchorState.Unanchor:
                        DoUnAnchor();
                        WakeAIForChange();
                        break;
                    default:
                        var temp = AnchorState;
                        AnchorState = AIAnchorState.None;
                        throw new NotImplementedException("unknown AnchorState - " + temp);
                }
            }
        }

        // ----------------------------  Pathfinding Processor  ---------------------------- 
        internal float DriveControl
        {
            set => tank.control.DriveControl = value;
        }
        internal void UpdateVanillaAvoidence()
        {
            tank.control.m_Movement.m_USE_AVOIDANCE = AvoidStuff;
        }
        public bool FixControlReversal(float Drive)
        {
            var thisControl = tank.control;
            return (thisControl.ActiveScheme == null || !thisControl.ActiveScheme.ReverseSteering) &&
                Drive < -0.01f &&
                Vector3.Dot(SafeVelocity, thisControl.Tech.rootBlockTrans.forward) < 0f;
        }
        internal void ProcessControl(Vector3 DriveVal, Vector3 TurnVal, Vector3 Throttle, bool props, bool jets)
        {
            tank.control.CollectMovementInput(DriveVal, TurnVal, Throttle, props, jets);
        }
        internal void SteerControl(Vector3 direction, float throttle)
        {
            tank.control.m_Movement.FaceDirection(tank, direction, throttle);
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
        /// <summary> Velocity in World Space </summary>
        public Vector3 SafeVelocity { get; private set; } = Vector3.zero;
        /// <summary> Velocity in Local Space </summary>
        public Vector3 LocalSafeVelocity { get; private set; } = Vector3.zero;
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
        public Vector3 GetDirAuto(Tank tank)
        {
            if (IsDirectedMovingFromDest)
                return GetDir(tank);
            return GetOtherDir(tank);
        }
        /// <summary>
        /// Gets the opposite direction of the target tech for offset avoidence, accounting for size
        /// </summary>
        /// <param name="targetToAvoid"></param>
        /// <returns></returns>
        internal Vector3 GetOtherDir(Tank targetToAvoid)
        {
            //What actually does the avoidence
            //DebugTAC_AI.Log(KickStart.ModID + ": GetOtherDir");
            Vector3 inputOffset = tank.boundsCentreWorldNoCheck - targetToAvoid.boundsCentreWorldNoCheck;
            float inputSpacing = targetToAvoid.GetCheapBounds() + lastTechExtents + DodgeStrength;
            Vector3 Final = tank.boundsCentreWorldNoCheck + (inputOffset.normalized * inputSpacing);
            return Final;
        }
        /// <summary>
        /// [For reversed inputs] Gets the direction of the target tech for offset avoidence, accounting for size
        /// </summary>
        /// <param name="targetToAvoid"></param>
        /// <returns></returns>
        internal Vector3 GetDir(Tank targetToAvoid)
        {
            //What actually does the avoidence
            //DebugTAC_AI.Log(KickStart.ModID + ": GetDir");
            Vector3 inputOffset = tank.boundsCentreWorldNoCheck - targetToAvoid.boundsCentreWorldNoCheck;
            float inputSpacing = targetToAvoid.GetCheapBounds() + lastTechExtents + DodgeStrength;
            Vector3 Final = tank.boundsCentreWorldNoCheck - (inputOffset.normalized * inputSpacing);
            return Final;
        }

        internal static List<KeyValuePair<Vector3, float>> posWeights = new List<KeyValuePair<Vector3, float>>();
        internal Vector3 AvoidAssist(Vector3 targetIn, bool AvoidStatic = true)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            //IsLikelyJammed = false;
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AvoidAssist IS NaN!!");
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
                    lastCloseAlly = AIEPathing.SecondClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, 
                        out lastAllyDist, out float lastAuxVal, this);
                    if (lastCloseAlly && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastCloseAlly2 && lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDirAuto(lastCloseAlly) + GetDirAuto(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            Avoiding = true;
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));
                        }
                        else
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            Avoiding = true;
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
                    lastCloseAlly = AIEPathing.ClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out lastAllyDist, this);
                    //DebugTAC_AI.Log(KickStart.ModID + ": Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log(KickStart.ModID + ": Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        //IsLikelyJammed = true;
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                        Vector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        Avoiding = true;
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
                this.lastCloseAlly = lastCloseAlly;
                return posCombined / totalWeight;
            }
            catch (Exception e)
            {
                if (IsDirectedMovingFromDest)
                    DebugTAC_AI.LogWarnPlayerOnce("AvoidAssist()[INVERTED] Critical error", e);
                DebugTAC_AI.LogWarnPlayerOnce("AvoidAssist() Critical error", e);
                return targetIn;
            }
        }
        
        
        /// <summary>
        /// When moving AWAY from target
        /// </summary>
        /// <param name="targetIn"></param>
        /// <returns></returns>
        internal Vector3 AvoidAssistInv_OBS(Vector3 targetIn, bool AvoidStatic = true)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target - REVERSED
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AvoidAssistInv IS NaN!!");
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
                    lastCloseAlly = AIEPathing.SecondClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2,
                        out lastAllyDist, out float lastAuxVal, this);
                    if (lastCloseAlly && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastCloseAlly2 && lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDir(lastCloseAlly) + GetDir(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            Avoiding = true;
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));

                        }
                        else
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDir(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            Avoiding = true;
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
                    lastCloseAlly = AIEPathing.ClosestAlly(AlliesAlt, tank.boundsCentreWorldNoCheck, out lastAllyDist, this);
                    //DebugTAC_AI.Log(KickStart.ModID + ": Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log(KickStart.ModID + ": Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        //IsLikelyJammed = true;
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI);
                        Vector3 ProccessedVal = GetDir(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        Avoiding = true;
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
                this.lastCloseAlly = lastCloseAlly;
                return posCombined / totalWeight;
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("AvoidAssistInv() Critical error", e);
                return targetIn;
            }
        }
        internal Vector3 AvoidAssistPrecise(Vector3 targetIn, bool AvoidStatic = true, bool IgnoreDestructable = false)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            //  MORE DEMANDING THAN THE ABOVE!
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AvoidAssistPrecise IS NaN!!");
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
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, 
                        out lastAllyDist, out float lastAuxVal, this);
                    if (lastCloseAlly && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastCloseAlly2 && lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI, IgnoreDestructable);
                            Vector3 ProccessedVal = GetDirAuto(lastCloseAlly) + GetDirAuto(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            Avoiding = true;
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));
                        }
                        else
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI, IgnoreDestructable);
                            Vector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            Avoiding = true;
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
                    lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, tank.boundsCentreWorldNoCheck, out lastAllyDist, this);
                    //DebugTAC_AI.Log(KickStart.ModID + ": Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log(KickStart.ModID + ": Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, AvoidStatic, out obst, AdvancedAI, IgnoreDestructable);
                        Vector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        Avoiding = true;
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
                this.lastCloseAlly = lastCloseAlly;
                return posCombined / totalWeight;
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("AvoidAssistPrecise() Critical error", e);
                return targetIn;
            }
        }
        internal Vector3 AvoidAssistPrediction(Vector3 targetIn, float Foresight)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            //IsLikelyJammed = false;
            if (!AvoidStuff || tank.IsAnchored)
                return targetIn;
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AvoidAssistPrediction IS NaN!!");
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
                    lastCloseAlly = AIEPathing.SecondClosestAlly(AlliesAlt, posOffset, out Tank lastCloseAlly2,
                        out lastAllyDist, out float lastAuxVal, this);
                    if (lastCloseAlly && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        if (lastCloseAlly2 && lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDirAuto(lastCloseAlly) + GetDirAuto(lastCloseAlly2);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 8));
                            Avoiding = true;
                            posWeights.Add(new KeyValuePair<Vector3, float>(ProccessedVal, 2));
                        }
                        else
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                            Vector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                            if (obst)
                                posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                            Avoiding = true;
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
                    lastCloseAlly = AIEPathing.ClosestAlly(AlliesAlt, posOffset, out lastAllyDist, this);
                    //DebugTAC_AI.Log(KickStart.ModID + ": Ally is " + lastAllyDist + " dist away");
                    //DebugTAC_AI.Log(KickStart.ModID + ": Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                    //if (lastCloseAlly == null)
                    //    DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                    if (lastCloseAlly != null && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                        //IsLikelyJammed = true;
                        Vector3 obstOff = AIEPathing.ObstDodgeOffset(tank, this, true, out obst, AdvancedAI);
                        Vector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                        if (obst)
                            posWeights.Add(new KeyValuePair<Vector3, float>(obstOff, 4));
                        Avoiding = true;
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
                this.lastCloseAlly = lastCloseAlly;
                return posCombined / totalWeight;
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("AvoidAssistPrediction() Critical error", e);
                return targetIn;
            }
        }
        /// <summary>
        /// An airborne version of the Player AI pathfinding which handles obstructions
        /// </summary>
        /// <param name="targetIn"></param>
        /// <param name="predictionOffset"></param>
        /// <returns></returns>
        internal Vector3 AvoidAssistAirSpacing(Vector3 targetIn, float Responsiveness)
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
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, DSO, out Tank lastCloseAlly2,
                        out lastAllyDist, out float lastAuxVal, this);
                    if (lastCloseAlly && lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                    {
                        if (lastCloseAlly2 && lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                        {
                            IntVector3 ProccessedVal2 = GetDirAuto(lastCloseAlly) + GetDirAuto(lastCloseAlly2);
                            Avoiding = true;
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                        Avoiding = true;
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, DSO, out lastAllyDist, this);
                this.lastCloseAlly = lastCloseAlly;
                if (lastCloseAlly == null)
                {
                    // DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                    return targetIn;
                }
                if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                {
                    IntVector3 ProccessedVal = GetDirAuto(lastCloseAlly);
                    Avoiding = true;
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("AvoidAssistAirSpacing() Critical error", e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AvoidAssistAirSpacing IS NaN!!");
                //TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }
        

        private void UpdatePhysicsInfo()
        {
            if (tank.rbody.IsNotNull())
            {
                var velo = tank.rbody.velocity;
                if (!velo.IsNaN() && !float.IsInfinity(velo.x)
                    && !float.IsInfinity(velo.z) && !float.IsInfinity(velo.y))
                {
                    DodgeSphereCenter = tank.boundsCentreWorldNoCheck + velo.Clamp(lowMaxBoundsVelo, highMaxBoundsVelo);
                    DodgeSphereRadius = lastTechExtents + Mathf.Clamp(recentSpeed / 2f, 1f, 63f); // Strict
                    SafeVelocity = velo;
                    LocalSafeVelocity = tank.rootBlockTrans.InverseTransformVector(velo);
                    return;
                }
            }
            DodgeSphereCenter = tank.boundsCentreWorldNoCheck;
            DodgeSphereRadius = lastTechExtents;
            SafeVelocity = Vector3.zero;
            LocalSafeVelocity = Vector3.zero;
        }
        public bool IsOrbiting(float minimumCloseInSpeedSqr = AIGlobals.MinimumCloseInSpeedSqr)
        {
            return GetPathPointDeltaDistSq() * (KickStart.AIClockPeriod / 40) < 
                Mathf.Max(minimumCloseInSpeedSqr, EstTopSped / 3) && !Avoiding &&
                Vector3.Dot((PathPoint - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) < 0.5f;
        }
        public bool IsOrbiting_LEGACY(Vector3 taskLocation, float orbitDistDelta, float minimumCloseInSpeed = AIGlobals.MinimumCloseInSpeedSqr)
        {
            return orbitDistDelta * (KickStart.AIClockPeriod / 40) < minimumCloseInSpeed &&
                Vector3.Dot((taskLocation - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) < 0.35f;
        }
        /// <summary>
        /// IMPORTANT - Sets lastOperatorRange!!!
        /// </summary>
        /// <param name="taskLocation"></param>
        /// <param name="additionalSpacing"></param>
        /// <returns></returns>
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
        public float GetPathPointDeltaDistSq()
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
                float distPrev = lastPathPointRange;
                lastPathPointRange = (tank.boundsCentreWorldNoCheck + veloFlat - PathPoint).sqrMagnitude;
                return lastPathPointRange - distPrev;
            }
            else
                return GetPathPointDeltaDistSq2D();
        }
        public float GetPathPointDeltaDistSq2D()
        {
            Vector3 veloFlat;
            if ((bool)tank.rbody)   // So that drifting is minimized
            {
                veloFlat = SafeVelocity;
                veloFlat.y = 0;
            }
            else
                veloFlat = Vector3.zero;
            float distPrev = lastPathPointRange;
            lastPathPointRange = (tank.boundsCentreWorldNoCheck.ToVector2XZ() + veloFlat.ToVector2XZ() - PathPoint.ToVector2XZ()).sqrMagnitude;
            return lastPathPointRange - distPrev;
        }
        public void SetDistanceFromTaskUnneeded()
        {
            lastOperatorRange = 96; //arbitrary
        }
        public bool AutoHandleObstruction(ref EControlOperatorSet direct, float dist = 0, bool useRush = false, bool useGun = true, float div = 4)
        {
            if (!IsTechMovingAbs(EstTopSped / div))
            {
                TryHandleObstruction(!AIECore.Feedback, dist, useRush, useGun, ref direct);
                return true;
            }
            return false;
        }
        public void TryHandleObstruction(bool hasMessaged, float dist, bool useRush, bool useGun, ref EControlOperatorSet direct)
        {
            //Something is in the way - try fetch the scenery to shoot at
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Obstructed");
            if (!hasMessaged)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Can't move there - something's in the way!");
            }

            ControlCore.FlagBusyUnstucking();
            IsTryingToUnjam = false;
            ThrottleState = AIThrottleState.FullSpeed;
            if (direct.DriveDir == EDriveFacing.Backwards)
            {   // we are likely driving backwards
                ThrottleState = AIThrottleState.ForceSpeed;
                DriveVar = -1;

                if (Urgency >= 0)
                    Urgency += KickStart.AIClockPeriod / 5f;
                if (UrgencyOverload > AIGlobals.UrgencyOverloadReconsideration)
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
                    ThrottleState = AIThrottleState.ForceSpeed;
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
                        ThrottleState = AIThrottleState.ForceSpeed;
                        DriveVar = 1;
                    }
                    else
                    {
                        ControlCore.DriveToFacingTowards();
                        //ControlCore.lastDestination = tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * 50);
                        ThrottleState = AIThrottleState.ForceSpeed;
                        DriveVar = 1;
                        ForceSetBeam = true;
                    }
                }
                else if (45 < FrustrationMeter)
                {   //Shoot the freaking tree
                    FrustrationMeter += KickStart.AIClockPeriod;
                    UrgencyOverload += KickStart.AIClockPeriod;
                    if (useGun)
                        RemoveObstruction();
                    ThrottleState = AIThrottleState.ForceSpeed;
                    DriveVar = -0.5f;
                }
                else
                {   // Gun the throttle
                    FrustrationMeter += KickStart.AIClockPeriod;
                    UrgencyOverload += KickStart.AIClockPeriod;
                    ThrottleState = AIThrottleState.ForceSpeed;
                    DriveVar = -1f;
                }
            }
            else
            {   // we are likely driving forwards
                ThrottleState = AIThrottleState.ForceSpeed;
                DriveVar = 1;

                if (Urgency >= 0)
                    Urgency += KickStart.AIClockPeriod / 5f;
                if (UrgencyOverload > AIGlobals.UrgencyOverloadReconsideration)
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
                    ThrottleState = AIThrottleState.ForceSpeed;
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
                        ThrottleState = AIThrottleState.ForceSpeed;
                        DriveVar = -1;
                    }
                    else
                    {
                        ControlCore.DriveAwayFacingTowards();
                        //ControlCore.lastDestination = tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward * -50);
                        ThrottleState = AIThrottleState.ForceSpeed;
                        DriveVar = -1;
                        ForceSetBeam = true;
                    }
                }
                else if (AIGlobals.UnjamUpdateFire < FrustrationMeter)
                {
                    //Shoot the freaking tree
                    FrustrationMeter += KickStart.AIClockPeriod;
                    UrgencyOverload += KickStart.AIClockPeriod;
                    if (useGun)
                        RemoveObstruction();
                    ThrottleState = AIThrottleState.ForceSpeed;
                    DriveVar = 0.5f;
                }
                else
                {   // Gun the throttle
                    FrustrationMeter += KickStart.AIClockPeriod;
                    UrgencyOverload += KickStart.AIClockPeriod;
                    ThrottleState = AIThrottleState.ForceSpeed;
                    DriveVar = 1f;
                }
            }
        }
        private Transform GetObstruction(float searchRad)
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
                //DebugTAC_AI.Log(KickStart.ModID + ": GetObstruction - DID NOT HIT ANYTHING");
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
            //DebugTAC_AI.Log(KickStart.ModID + ": GetObstruction - found " + ObstList.ElementAt(bestStep).name);
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
        internal void AimAndFireWeapons(Vector3 aimWorld, float aimRadius)
        {
            if (maxBlockCount < AIGlobals.SmolTechBlockThreshold)
                FireAllWeapons();
            tank.control.TargetPositionWorld = aimWorld;
            tank.control.TargetRadiusWorld = aimRadius;
        }
        internal void FireAllWeapons() => tank.control.FireControl = true;
        internal void MaxBoost() => tank.control.BoostControlJets = true;
        internal void MaxProps() => tank.control.BoostControlJets = true;

        private static int TargetMask = Globals.inst.layerScenery.mask | Globals.inst.layerSceneryCoarse.mask |
            Globals.inst.layerSceneryFader.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerLandmark.mask;
        private float LastWeapCheck = 0;
        /// <summary> Do ONLY ONCE </summary>
        private void SyncLineOfSight()
        {
            try
            {
                if (WeaponAimType == AIWeaponType.Unknown)
                {
                    int WeaponsNeedLOS = 0;
                    int WeaponsNoNeedLOS = 0;
                    foreach (var item in tank.blockman.IterateBlocks())
                    {
                        BlockDetails BD = BlockIndexer.GetBlockDetails(item.BlockType);
                        if (BD.IsWeapon && !BD.IsCab)
                        {
                            if (BD.IsMelee || BD.IsShortRanged)
                                WeaponsNoNeedLOS++;
                            else
                            {
                                var gun = item.GetComponent<ModuleWeaponGun>();
                                if (gun && gun.AimWithTrajectory() && gun.GetRange() > 500 &&
                                    (gun.m_SeekingRounds || gun.GetVelocity() < 60f))
                                    WeaponsNoNeedLOS++;
                                else
                                    WeaponsNeedLOS++;
                            }
                        }
                    }
                    if (WeaponsNeedLOS < WeaponsNoNeedLOS)
                        WeaponAimType = AIWeaponType.Indirect;
                    else
                        WeaponAimType = AIWeaponType.Direct;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("CheckCanHitTarget() Critical error", e);
            }
        }

        private bool lastSuppressedState = false;
        internal void SuppressFiring(bool Disable)
        {
            try
            {
                if (lastSuppressedState != Disable)
                {
                    DebugTAC_AI.Info(KickStart.ModID + ": AI " + tank.name + " of Team " + tank.Team + ":  Disabled weapons: " + Disable);
                    tank.Weapons.enabled = !Disable;
                    if (Disable)
                        tank.control.Weapons.AimAtTarget(tank, tank.boundsCentreWorldNoCheck, 0);
                    lastSuppressedState = Disable;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogWarnPlayerOnce("SuppressFiring() Critical error", e);
            }
        }
        private void CheckEnemyAndAiming()
        {
            if (LastWeapCheck < Time.time)
            {
                LastWeapCheck = Time.time + AIGlobals.TargetValidationDelay;
                SyncLineOfSight();
                BlockedLineOfSight = false;
                if (lastEnemyGet)
                {
                    if (!lastEnemyGet.isActive || lastEnemyGet.tank.blockman.blockCount == 0 ||
                        !Tank.IsEnemy(tank.Team, lastEnemyGet.tank.Team))
                    {
                        lastEnemy = null;
                        //DebugTAC_AI.LogSpecific(tank, "Target released CheckEnemyAndAiming()1");
                        //Debug.Assert(true, KickStart.ModID + ": Tech " + tank.name + " has valid, live target but it has no blocks.  How is this possible?!"); 
                    }
                    else
                    {
                        Vector3 pos = tank.boundsCentreWorld + Vector3.up;
                        Vector3 vec = lastEnemy.tank.boundsCentreWorld - pos;
                        float targetDistance = vec.magnitude;
                        if (NeedsLineOfSight)
                        {
                            if (Physics.Raycast(pos, vec.normalized, out RaycastHit hit,
                                MaxCombatRange, TargetMask, QueryTriggerInteraction.Ignore) && hit.distance < targetDistance)
                            {
                                BlockedLineOfSight = true;
                            }
                        }
                        if (targetDistance > MaxCombatRange && !PreserveEnemyTarget) // RTS Controlled to target something that can move
                        {
                            lastEnemy = null;
                            //DebugTAC_AI.LogSpecific(tank, "Target released CheckEnemyAndAiming()2");
                        }
                    }
                }
            }
        }
        public Visible TryRefreshEnemyAllied()
        {
            //tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
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
                    lastEnemy = playerTarget;
                    return lastEnemy;
                }
            }
            if (MovementController is AIControllerAir air && air.FlyStyle == AIControllerAir.FlightType.Aircraft)
            {
                lastEnemy = FindEnemyAir(false);
            }
            else
                lastEnemy = FindEnemy(false);
            return lastEnemy;
        }
        public Visible TryRefreshEnemyEnemy(bool InvertBullyPriority, int pos = 1)
        {
            if (MovementController is AIControllerAir air && air.FlyStyle == AIControllerAir.FlightType.Aircraft)
            {
                lastEnemy = FindEnemyAir(InvertBullyPriority, pos);
            }
            else
                lastEnemy = FindEnemy(InvertBullyPriority, pos);
            return lastEnemy;
        }
        private void UpdateTargetCombatFocus()
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
        internal float UpdateEnemyDistance(Vector3 enemyPosition)
        {
            _lastCombatRange = (enemyPosition - tank.boundsCentreWorldNoCheck).magnitude;
            return _lastCombatRange;
        }
        internal float IgnoreEnemyDistance()
        {
            _lastCombatRange = float.MaxValue;
            return _lastCombatRange;
        }
        private void DetermineCombat()
        {
            bool DoNotEngage = false;
            if (lastEnemyGet?.tank)
                if (!Tank.IsEnemy(tank.Team, lastEnemyGet.tank.Team))
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
                    if (!RTSControlled && MaxCombatRange * 4 < (lastPlayer.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude)
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
                    if (!RTSControlled && MaxCombatRange < (lastPlayer.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude)
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
            Retreat = AIGlobals.IsNotAttract && AIECore.RetreatingTeams.Contains(tank.Team);

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

        private float lastTargetGatherTime = 0;
        private List<Tank> targetCache = new List<Tank>();
        private List<Tank> GatherTargetTechsInRange(float gatherRangeSqr)
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
                    /*
                    if (cTank.Team == tank.Team && ManBaseTeams.TryGetBaseTeamDynamicOnly(tank.Team, out var teamI) &&
                        !teamI.IsInfighting)
                    {
                        TankAIManager.ForceReloadAll();
                        if (cTank.Team == tank.Team && ManBaseTeams.TryGetBaseTeamDynamicOnly(tank.Team, out teamI) &&
                        !teamI.IsInfighting)
                            throw new InvalidOperationException("Infighting in team " + teamI.teamName + " when they were not set to be! " +
                                "FORCE reloading TankAIManager did not CHANGE ANYTHING");
                        else
                            throw new InvalidOperationException("Infighting in team " + teamI.teamName + " when they were not set to be!  " +
                                "FORCE reloading TankAIManager fixed it?");
                    }
                    */

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
        private Visible FindEnemy(bool InvertBullyPriority, int pos = 1)
        {
            //if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
            //    return null; // We NO ATTACK
            Visible target = lastEnemyGet;

            // We begin the search
            float TargetRangeSqr = MaxCombatRange * MaxCombatRange;
            Vector3 scanCenter = tank.boundsCentreWorldNoCheck;

            if (target?.tank)
            {
                if (!target.isActive || !ManBaseTeams.IsEnemy(tank.Team, target.tank.Team))
                {
                    //DebugTAC_AI.Log("Target lost");
                    target = null;
                }
                else if (PreserveEnemyTarget || KeepEnemyFocus || NextFindTargetTime <= Time.time) // Carry on chasing the target
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
                List<Tank> techs = GatherTargetTechsInRange(TargetRangeSqr);
                int max = techs.Count();
                int launchCount = UnityEngine.Random.Range(0, max);
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (cTank != tank && cTank.visible.isActive && ManBaseTeams.IsEnemy(tank.Team, cTank.Team))
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
                List<Tank> techs = GatherTargetTechsInRange(TargetRangeSqr);
                int launchCount = techs.Count();
                if (InvertBullyPriority)
                {
                    int BlockCount = 0;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank != tank && ManBaseTeams.IsEnemy(tank.Team, cTank.Team))
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
                        if (cTank != tank && ManBaseTeams.IsEnemy(tank.Team, cTank.Team))
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
                if (pos == 1 && AIGlobals.UseVanillaTargetFetching)
                    return tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);

                float TargRange2 = TargetRangeSqr;
                float TargRange3 = TargetRangeSqr;

                Visible target2 = null;
                Visible target3 = null;

                List<Tank> techs = GatherTargetTechsInRange(TargetRangeSqr);
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
                            if (cTank != tank && ManBaseTeams.IsEnemy(tank.Team, cTank.Team))
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
                            if (cTank != tank && ManBaseTeams.IsEnemy(tank.Team, cTank.Team))
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
                            if (cTank != tank && ManBaseTeams.IsEnemy(tank.Team, cTank.Team))
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
                DebugTAC_AI.Log(KickStart.ModID + ": Tech " + Tank.name + " Could not find target with FindEnemy, resorting to defaults");
                return Tank.Vision.GetFirstVisibleTechIsEnemy(Tank.Team);
            }
            */
            return target;
        }
        private Visible FindEnemyAir(bool InvertBullyPriority, int pos = 1)
        {
            //if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
            //    return null; // We NO ATTACK
            Visible target = lastEnemyGet;

            // We begin the search
            float TargetRangeSqr = MaxCombatRange * MaxCombatRange;
            Vector3 scanCenter = tank.boundsCentreWorldNoCheck;

            if (target != null)
            {
                if (!target.isActive || !ManBaseTeams.IsEnemy(tank.Team, target.tank.Team))
                {
                    //DebugTAC_AI.Log("Target lost");
                    target = null;
                }
                else if (PreserveEnemyTarget || KeepEnemyFocus || NextFindTargetTime <= Time.time) // Carry on chasing the target
                {
                    return target;
                }
                else if ((target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRangeSqr)
                {
                    //DebugTAC_AI.Log("Target out of range");
                    target = null;
                }
            }
            float altitudeHigh = float.MinValue;

            if (AttackMode == EAttackMode.Random)
            {
                List<Tank> techs = GatherTargetTechsInRange(TargetRangeSqr);
                scanCenter = DodgeSphereCenter;
                int launchCount = techs.Count();
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (ManBaseTeams.IsEnemy(tank.Team, cTank.Team) && cTank != tank)
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
                List<Tank> techs = GatherTargetTechsInRange(TargetRangeSqr);
                int launchCount = techs.Count();
                if (InvertBullyPriority)
                {
                    altitudeHigh = float.MaxValue;
                    int BlockCount = 0;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (ManBaseTeams.IsEnemy(tank.Team, cTank.Team) && cTank != tank)
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
                        if (ManBaseTeams.IsEnemy(tank.Team, cTank.Team) && cTank != tank)
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

                List<Tank> techs = GatherTargetTechsInRange(TargetRangeSqr);
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
                            if (ManBaseTeams.IsEnemy(tank.Team, cTank.Team) && cTank != tank)
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
                            if (ManBaseTeams.IsEnemy(tank.Team, cTank.Team) && cTank != tank)
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
                            if (cTank != tank && ManBaseTeams.IsEnemy(tank.Team, cTank.Team))
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
            if (DriverType != AIDriverType.Stationary && targetTank.rbody.IsNotNull())
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
        private void ManageAILockOn()
        {
            switch (ActiveAimState)
            {
                case AIWeaponState.Enemy:
                    if (lastEnemyGet.IsNotNull())
                    {   // Allow the enemy AI to finely select targets
                        //DebugTAC_AI.Log(KickStart.ModID + ": Overriding targeting to aim at " + lastEnemy.name + "  pos " + lastEnemy.tank.boundsCentreWorldNoCheck);
                        lastLockOnTarget = lastEnemyGet;
                    }
                    break;
                case AIWeaponState.Obsticle:
                    if (Obst?.gameObject)
                    {
                        var resTarget = Obst.GetComponent<Visible>();
                        if (resTarget)
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": Overriding targeting to aim at obstruction");
                            lastLockOnTarget = resTarget;
                        }
                    }
                    break;
                case AIWeaponState.Mimic:
                    if (lastCloseAlly.IsNotNull())
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Overriding targeting to aim at player's target");
                        var helperAlly = lastCloseAlly.GetHelperInsured();
                        if (helperAlly.ActiveAimState == AIWeaponState.Enemy)
                            lastLockOnTarget = helperAlly.lastEnemyGet;
                    }
                    break;
            }

            if (lastLockOnTarget)
            {
                bool playerAim = tank.PlayerFocused && !ManWorldRTS.PlayerIsInRTS;
                if (!lastLockOnTarget.isActive || (playerAim && !tank.control.FireControl))
                {   // Cannot do as camera breaks
                    lastLockOnTarget = null;
                    return;
                }
                if (lastLockOnTarget == tank.visible)
                {
                    DebugTAC_AI.Assert("Tech " + (tank.name.NullOrEmpty() ? "<NULL>" : tank.name) + " tried to lock-on to itself!!!");
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
        public void TryInsureAnchor()
        {
            if (!tank.IsAnchored && CanAnchorNow)
            {
                AnchorState = AIAnchorState.Anchor;
            }
        }
        /// <summary>
        /// IGNORES CHECKS
        /// </summary>
        public void AnchorIgnoreChecks(bool forced = false)
        {
            if (forced)
            {
                DebugTAC_AI.LogDevOnlyAssert(KickStart.ModID + ": AI " + tank.name + ":  TryReallyAnchor(true)");
                AnchorState = AIAnchorState.ForceAnchor;
            }
            else
                AnchorState = AIAnchorState.Anchor;
        }
        public void AdjustAnchors()
        {
            bool prevAnchored = tank.IsAnchored;
            //DebugTAC_AI.Assert("AdjustAnchors()");
            DoUnAnchor();
            if (!tank.IsAnchored)
            {
                AnchorIgnoreChecks(prevAnchored);
            }
        }

        public void Unanchor()
        {
            //DebugTAC_AI.Assert("Unanchor()");
            //DoUnAnchor();
            AnchorState = AIAnchorState.Unanchor;
            AnchorStateAIInsure = true;
        }

        public void AnchorStatic()
        {
            //DebugTAC_AI.Assert("AnchorStatic()");
            AnchorState = AIAnchorState.AnchorStaticAI;
        }



        private void DoAnchorStatic()
        {
            SetDriverType(AIDriverType.Stationary);
        }

        private void DoAnchor(bool forced)
        {
            if (!tank.IsAnchored && anchorAttempts <= AIGlobals.MaxAnchorAttempts)
            {
                anchorAttempts++;
                //DebugTAC_AI.LogDevOnly(KickStart.ModID + ": AI " + tank.name + ":  TryReallyAnchor(" + forced + ")");
                tank.Anchors.TryAnchorAll(true);
                if (tank.Anchors.NumIsAnchored > 0)
                {
                    ExpectAITampering = true;
                    anchorAttempts = 0;
                    if (AnchorState == AIAnchorState.AnchorStaticAI)
                        DoAnchorStatic();
                    AnchorState = AIAnchorState.Anchored;
                    return;
                }

                bool worked = false;
                Vector3 startPosTrans = tank.trans.position;
                tank.FixupAnchors(false);
                //tank.FixupAnchors(true, true); // Breaks everything
                if (tank.Anchors.NumIsAnchored > 0)
                {
                    ExpectAITampering = true;
                    anchorAttempts = 0;
                    if (AnchorState == AIAnchorState.AnchorStaticAI)
                        DoAnchorStatic();
                    AnchorState = AIAnchorState.Anchored;
                    return;
                }
                Vector3 startPos = tank.visible.centrePosition;
                Quaternion tankFore = AIGlobals.LookRot(tank.trans.forward.SetY(0).normalized, Vector3.up);
                tank.visible.Teleport(startPos, tankFore, true, true);
                //Quaternion tankStartRot = tank.trans.rotation;
                for (int step = 0; step < 6; step++)
                {
                    if (!tank.IsAnchored)
                    {
                        Vector3 newPos = startPos + Vector3.down;
                        newPos.y += step / 4f;
                        tank.visible.Teleport(newPos, tankFore, false, true);
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
                        worked = true;
                    }
                    else
                    {
                        tank.trans.position = startPosTrans + (Vector3.up * 0.1f);
                    }
                }
                if (worked)
                {
                    ExpectAITampering = true;
                    anchorAttempts = 0;
                    if (AnchorState == AIAnchorState.AnchorStaticAI)
                        DoAnchorStatic();
                    RecalibrateMovementAIController();
                }
                ExpectAITampering = true;
                // Reset to ground so it doesn't go flying off into space
                tank.visible.Teleport(startPos, tankFore, true, true);
                AnchorState = AIAnchorState.None;
            }
        }
        private void DoUnAnchor()
        {
            //DebugTAC_AI.Log("DoUnAnchor()");
            if (tank.IsAnchored || tank.Anchors.NumIsAnchored > 0)
            {
                //DebugTAC_AI.Log("DoUnAnchor() - activated");
                tank.Anchors.UnanchorAll(true);
                if (!tank.IsAnchored && AIAlign == AIAlignment.Player)
                {
                    //DebugTAC_AI.Log("DoUnAnchor() - success");
                    WakeAIForChange();
                }
                AnchorState = AIAnchorState.None;
            }
            anchorAttempts = 0;
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
            if (heldBlock?.visible)
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
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "'s grabbed block was thefted!");
                        DropBlock();
                    }
                    else if (ManPointer.inst.targetVisible == heldBlock.visible)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "'s grabbed block was grabbed by player!");
                        DropBlock();
                    }
                    else
                    {
                        Vector3 moveVec;
                        if (blockHoldOffset)
                        {
                            moveVec = tank.trans.TransformPoint(blockHoldPos) - heldBlock.transform.position;
                            float dotVal = Vector3.Dot(moveVec.normalized, Vector3.down);
                            if (dotVal > 0.75f)
                                moveVec.y += moveVec.ToVector2XZ().magnitude / 3;
                            else
                            {
                                moveVec.y -= moveVec.ToVector2XZ().magnitude / 3;
                            }
                            Vector3 finalPos = heldBlock.transform.position;
                            finalPos += moveVec / ((100 / AIGlobals.BlockAttachDelay) * Time.fixedDeltaTime);
                            if (finalPos.y < tank.trans.TransformPoint(blockHoldPos).y)
                                finalPos.y = tank.trans.TransformPoint(blockHoldPos).y;
                            heldBlock.transform.position = finalPos;
                            if (heldBlock.rbody)
                            {
                                if (tank.rbody)
                                    heldBlock.rbody.velocity = SafeVelocity.SetY(0);
                                heldBlock.rbody.AddForce(-(TankAIManager.GravVector * heldBlock.AverageGravityScaleFactor), ForceMode.Acceleration);
                                Vector3 forward = tank.trans.TransformDirection(blockHoldRot * Vector3.forward);
                                Vector3 up = tank.trans.TransformDirection(blockHoldRot * Vector3.up);
                                Quaternion rotChangeWorld = AIGlobals.LookRot(forward, up);
                                heldBlock.rbody.MoveRotation(Quaternion.RotateTowards(heldBlock.transform.rotation, rotChangeWorld, 
                                    (360 / AIGlobals.BlockAttachDelay) * Time.fixedDeltaTime));
                            }
                            heldBlock.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0.25f);
                        }
                        else
                        {
                            moveVec = tank.boundsCentreWorldNoCheck + (Vector3.up * (lastTechExtents + 3)) - heldBlock.visible.centrePosition;
                            moveVec = Vector3.ClampMagnitude(moveVec * 4, AIGlobals.ItemGrabStrength);
                            if (heldBlock.rbody)
                                heldBlock.rbody.AddForce(moveVec - (TankAIManager.GravVector * heldBlock.AverageGravityScaleFactor), ForceMode.Acceleration);
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
                DebugTAC_AI.Assert(true, KickStart.ModID + ": Tech " + tank.name + " attempted to illegally grab NULL Visible");
            }
            else if (ManNetwork.IsNetworked)
            {
                //DebugTAC_AI.Assert(true, KickStart.ModID + ": Tech " + tank.name + " called HoldBlock in networked environment. This is not supported!");if (TB.block)
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
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "'s target block was thefted by a tractor beam!");
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
                            DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "'s target block HAS NO RBODY");
                    }
                }
            }
            else
                DebugTAC_AI.Assert(true, KickStart.ModID + ": Tech " + tank.name + " attempted to illegally grab "
                    + (!TB.name.NullOrEmpty() ? TB.name : "NULL")
                    + " of type " + TB.type + " when they are only allowed to grab blocks");
            return false;
        }
        internal bool HoldBlock(Visible TB, RawBlockMem BM)
        {
            if (!TB)
            {
                DebugTAC_AI.Assert(true, KickStart.ModID + ": Tech " + tank.name + " attempted to illegally grab NULL Visible");
            }
            else if (ManNetwork.IsNetworked)
            {
                DebugTAC_AI.Assert(true, KickStart.ModID + ": Tech " + tank.name + " called HoldBlock in networked environment. This is not supported!");
            }
            else if (TB.block)
            {
                if (TB.isActive)
                {
                    if (TB.InBeam)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "'s target block was thefted by a tractor beam!");
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
                            DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "'s target block HAS NO RBODY");
                    }
                }
            }
            else
                DebugTAC_AI.Assert(true, KickStart.ModID + ": Tech " + tank.name + " attempted to illegally grab "
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
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Allowing approach");
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
                if (root != null)
                {
                    DamageThreshold = (1f - (root.visible.damageable.Health / (float)root.damage.maxHealth)) * 100;
                    lastBlockCount = blockC;
                }
                // Else we have NO ROOT and therefore no blocks(?!?),
                //   do nothing because putting in zero will immedeately break things
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
            if (BookmarkBuilder.TryGet(tank, out BookmarkBuilder builder))
            {
                AILimitSettings.OverrideForBuilder();
                if (TechMemor.IsNull())
                {
                    builder.HookUp(this);
                    DebugTAC_AI.Assert(KickStart.ModID + ": Tech " + tank.name + "TryRepairStatic has a BookmarkBuilder but NO TechMemor!");
                }
                if (lastEnemyGet != null)
                {   // Combat repairs (combat mechanic)
                    //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " RepairCombat");
                    AIERepair.RepairStepper(this, tank, TechMemor, true, Combat: true);
                }
                else
                {   // Repairs in peacetime
                    AIERepair.RepairStepper(this, tank, TechMemor);
                }
            }
            UpdateDamageThreshold();
        }
        /// <summary> Do ONLY ONCE </summary>
        public void CheckTryRepairAllied()
        {
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
                TryRepairAllied();
        }
        private void TryRepairAllied()
        {
            bool builderExists = BookmarkBuilder.TryGet(tank, out BookmarkBuilder builder);
            if (builderExists && TechMemor.IsNull())
            {
                builder.HookUp(this);
                DebugTAC_AI.Assert(KickStart.ModID + ": Tech " + tank.name + "TryRepairAllied has a BookmarkBuilder but NO TechMemor!");
            }
            if (builderExists || (AutoRepair && (!tank.PlayerFocused || ManWorldRTS.PlayerIsInRTS) && (KickStart.AISelfRepair || tank.IsAnchored)))
            {
                if (builderExists)
                {
                    AISetSettings.OverrideForBuilder();
                    AILimitSettings.OverrideForBuilder();
                }
                if (lastEnemyGet != null)
                {   // Combat repairs (combat mechanic)
                    //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " RepairCombat");
                    AIERepair.RepairStepper(this, tank, TechMemor, AdvancedAI, Combat: true);
                }
                else
                {   // Repairs in peacetime
                    //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " Repair");
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
            if (BookmarkBuilder.TryGet(tank, 
                out BookmarkBuilder Builder))
                Builder.Finish(this);
        }


        // ----------------------------  Debug Collector  ---------------------------- 
        private void ShowCollisionAvoidenceDebugThisFrame()
        {
            if (AIGlobals.ShowDebugFeedBack && Input.GetKey(KeyCode.LeftShift))// && AIECore.debugVisuals)
            {
                try
                {
                    Vector3 boundsC = tank.boundsCentreWorldNoCheck;
                    Vector3 boundsCUp = tank.boundsCentreWorldNoCheck + (Vector3.up * lastTechExtents);
                    DebugExtUtilities.DrawDirIndicatorCircle(boundsC + (Vector3.up * 128), Vector3.up, Vector3.forward, JobSearchRange, Color.blue);
                    if (tank.IsAnchored && !CanAutoAnchor)
                    {
                        DebugExtUtilities.DrawDirIndicatorRecPrizExt(boundsC, Vector3.one * lastTechExtents, Color.yellow);
                        if (lastEnemyGet != null && lastEnemyGet.isActive)
                        {
                            DebugExtUtilities.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MaxCombatRange, new Color(1, 0.6f, 0.6f));
                            DebugExtUtilities.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MinCombatRange, Color.red);
                            DebugExtUtilities.DrawDirIndicator(lastEnemyGet.tank.boundsCentreWorldNoCheck,
                                lastEnemyGet.tank.boundsCentreWorldNoCheck + Vector3.up * lastEnemyGet.GetCheapBounds(), Color.red);
                        }
                    }
                    else
                    {
                        DebugExtUtilities.DrawDirIndicatorSphere(boundsC, lastTechExtents, Color.yellow);
                        DebugExtUtilities.DrawDirIndicatorSphere(DodgeSphereCenter, DodgeSphereRadius, Color.gray);
                        if (Attempt3DNavi)
                        {
                            DebugExtUtilities.DrawDirIndicatorSphere(boundsC, MaxObjectiveRange, Color.cyan);
                            if (lastEnemyGet != null && lastEnemyGet.isActive)
                            {
                                DebugExtUtilities.DrawDirIndicatorSphere(boundsC, MaxCombatRange, new Color(1, 0.6f, 0.6f));
                                DebugExtUtilities.DrawDirIndicatorSphere(boundsC, MinCombatRange, Color.red);
                                DebugExtUtilities.DrawDirIndicator(lastEnemyGet.tank.boundsCentreWorldNoCheck,
                                    lastEnemyGet.tank.boundsCentreWorldNoCheck + Vector3.up * lastEnemyGet.GetCheapBounds(), Color.red);
                            }
                        }
                        else
                        {
                            DebugExtUtilities.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MaxObjectiveRange, Color.cyan);
                            if (lastEnemyGet != null && lastEnemyGet.isActive)
                            {
                                DebugExtUtilities.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MaxCombatRange, new Color(1, 0.6f, 0.6f));
                                DebugExtUtilities.DrawDirIndicatorCircle(boundsCUp, Vector3.up, Vector3.forward, MinCombatRange, Color.red);
                                DebugExtUtilities.DrawDirIndicator(lastEnemyGet.tank.boundsCentreWorldNoCheck,
                                    lastEnemyGet.tank.boundsCentreWorldNoCheck + Vector3.up * lastEnemyGet.GetCheapBounds(), Color.red);
                            }
                        }
                    }
                    if (lastPlayer != null && lastPlayer.isActive)
                    {
                        DebugExtUtilities.DrawDirIndicator(lastPlayer.tank.boundsCentreWorldNoCheck,
                            lastPlayer.tank.boundsCentreWorldNoCheck + Vector3.up * lastPlayer.GetCheapBounds(), Color.white);
                    }
                    if (Obst != null)
                    {
                        float rad = 6;
                        if (Obst.GetComponent<Visible>())
                            rad = Obst.GetComponent<Visible>().Radius;
                        DebugExtUtilities.DrawDirIndicator(Obst.position, Obst.position + Vector3.up * rad, Color.gray);
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
