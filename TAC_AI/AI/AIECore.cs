using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.Templates;

namespace TAC_AI.AI
{
    /*
        Summary of functions: Handles Allied AI, and enemy AI if TougherEnemies is installed or overriden to do so.
        
        AIECore contains the macro parameters that direct AI execution

        AI Control is handled via 2 separate execution flows.
        
        Flow 1 - Execution of plan:
        TanControl.Update -> ModuleTechController.ExecuteControl -> TankAIHelper.BetterAI
         - AI Movement Controller (IMovementAIController) handles all execution.
             - AI Movement Director - Tells the AI how to navigate safely and avoid obsticles along the way
             - AI Movement Maintainer - Makes the AI drive to the director's coordinates
             - AI Core - Each Core implements Director and Maintainer, and contain the details of how to move (Classes like AiplaneAICore vs VehicleAICore)

        Flow 2 - Planning flow:
        TankAIHelper.FixedUpdate -> AlliedOperationsController.Execute
         - AI types are reset/refreshed - Tells the AI which Allied/Enemy Operations to run, and which IMovementAIController to use
            VVVVV
         - Allied Operations are executed - Handles the destinations AI should drive to, and how to do it (Classes like BEscort or RWheeled)
         - Enemy Operations are executed

          As such, it's important to note that:
            AI Set Types and Attitudes - fires on change and on spawn/load
            Operations - must fire constantly (but can be slowed) to maintain consistant operation
            Movement Directors - are the major CPU bottlenecks of this mod
            Movement Maintainers - must be fired every Update to prevent AIs from bugging out on drive/fire operations

        Important to note that this Allied AI will not fire Explosive Bolts under any cirumstances. 
            The player should do that on their own accord as Explosive Bolts cost resources to make.
    */
    public class AIECore
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);

        public static List<Tank> Allies = new List<Tank>();    //Single-player only
        //public static List<ResourceDispenser> Minables;
        public static List<Visible> Minables;
        public static List<ModuleHarvestReciever> Depots;
        public static List<ModuleHarvestReciever> BlockHandlers;
        public static List<ModuleChargerTracker> Chargers;
        public static bool moreThan2Allies;
        //private static int lastTechCount = 0;

        public const float minimumChargeFractionToConsider = 0.75f;
        // legdev
        internal const bool Feedback = false;// set this to true to get AI feedback testing


        // Mining
        public static bool FetchClosestChunkReceiver(Vector3 tankPos, float MaxScanRange, out Transform finalPos, out Tank theBase, int team)
        {
            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            foreach (ModuleHarvestReciever reciever in Depots)
            {
                float temp = (reciever.trans.position - tankPos).sqrMagnitude;
                bool takesChunks = reciever.holder.Acceptance.HasFlag(ModuleItemHolder.AcceptFlags.Chunks);
                if (bestValue > temp && temp != 0 && !reciever.tank.boundsCentreWorldNoCheck.Approximately(tankPos, 1) && reciever.tank.Team == team && takesChunks)
                {
                    fired = true;
                    theBase = reciever.tank;
                    bestValue = temp;
                    finalPos = reciever;
                }
            }
            return fired;
        }
        public static bool FetchClosestResource(Vector3 tankPos, float MaxScanRange, out Visible theResource)
        {
            bool fired = false;
            theResource = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            int run = Minables.Count;
            for (int step = 0; step < run; step++)
            {
                var trans = Minables.ElementAt(step);
                if (!trans.gameObject.GetComponent<ResourceDispenser>().IsDeactivated)
                {
                    //Debug.Log("TACtical_AI:Skipped over inactive");
                    if (!trans.gameObject.GetComponent<Damageable>().Invulnerable)
                    {
                        //Debug.Log("TACtical_AI: Skipped over invincible");
                        float temp = (trans.centrePosition - tankPos).sqrMagnitude;
                        if (bestValue > temp && temp != 0)
                        {
                            theResource = trans;
                            fired = true;
                            bestValue = temp;
                        }
                        continue;
                    }
                }
                Minables.Remove(trans);//it's invalid and must be destroyed
                step--;
                run--;
            }
            return fired;
        }

        // Scavenging - Under Construction!
        public static bool FetchClosestBlockReceiver(Vector3 tankPos, float MaxScanRange, out Transform finalPos, out Tank theBase, int team)
        {
            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            foreach (ModuleHarvestReciever reciever in Depots)
            {
                float temp = (reciever.trans.position - tankPos).sqrMagnitude;
                bool takesBlocks = reciever.holder.Acceptance.HasFlag(ModuleItemHolder.AcceptFlags.Blocks);
                if (bestValue > temp && temp != 0 && !reciever.tank.boundsCentreWorldNoCheck.Approximately(tankPos, 1) && reciever.tank.Team == team && takesBlocks)
                {
                    fired = true;
                    theBase = reciever.trans.root.GetComponent<Tank>();
                    bestValue = temp;
                    finalPos = reciever;
                }
            }
            return fired;
        }
        public static bool FetchLooseBlocks(Vector3 tankPos, float MaxScanRange, out Visible theResource)
        {
            bool fired = false;
            theResource = null;
            float bestValue = MaxScanRange;// MAX SCAN RANGE
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tankPos, MaxScanRange, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Block })))
            {
                if (vis.block.IsAttached || vis.InBeam)
                    continue;   // no grab aquired blocks
                float temp = (vis.centrePosition - tankPos).sqrMagnitude;
                if (bestValue > temp && temp > 1)
                {
                    fired = true;
                    bestValue = temp;
                    theResource = vis;
                }
            }
            return fired;
        }
       
        // Charging
        public static bool FetchChargedChargers(Tank tank, float MaxScanRange, out Transform finalPos, out Tank theBase, int team)
        {
            if (team == -2)
                team = Singleton.Manager<ManPlayer>.inst.PlayerTeam;
            Vector3 tankPos = tank.boundsCentreWorldNoCheck;

            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            foreach (ModuleChargerTracker charge in Chargers)
            {
                float temp = (charge.trans.position - tankPos).sqrMagnitude;
                if (bestValue > temp && temp > 1 && charge.tank != tank && charge.tank.Team == team && charge.CanTransferCharge(tank))
                {
                    fired = true;
                    theBase = charge.tank;
                    bestValue = temp;
                    finalPos = charge.trans;
                }
            }
            return fired;
        }
        public static bool FetchLowestChargeAlly(Vector3 tankPos, TankAIHelper helper, out Visible toCharge)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            float Range = 62500;
            int bestStep = 0;
            bool fired = false;
            toCharge = null;
            try
            {
                if (ManNetwork.inst.IsMultiplayer())
                {
                    List<Tank> AlliesAlt = Enemy.RPathfinding.AllyList(helper.tank);
                    for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                    {
                        Tank ally = AlliesAlt.ElementAt(stepper);
                        float temp = (ally.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                        EnergyRegulator.EnergyState eState = ally.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                        bool hasCapacity = eState.storageTotal > 200;
                        bool needsCharge = eState.currentAmount / eState.storageTotal < minimumChargeFractionToConsider;
                        if (Range > temp && temp > 1 && hasCapacity && needsCharge)
                        {
                            Range = temp;
                            bestStep = stepper;
                            fired = true;
                        }
                    }
                    toCharge = AlliesAlt.ElementAt(bestStep).visible;
                }
                else
                {
                    for (int stepper = 0; Allies.Count > stepper; stepper++)
                    {
                        Tank ally = Allies.ElementAt(stepper);
                        float temp = (ally.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                        EnergyRegulator.EnergyState eState = ally.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                        bool hasCapacity = eState.storageTotal > 200;
                        bool needsCharge = eState.currentAmount / eState.storageTotal < minimumChargeFractionToConsider;
                        if (Range > temp && temp > 1 && hasCapacity && needsCharge)
                        {
                            Range = temp;
                            bestStep = stepper;
                            fired = true;
                        }
                    }
                    toCharge = Allies.ElementAt(bestStep).visible;
                }
                //Debug.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //Debug.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
            }
            return fired;
        }

        // Assassin
        public static bool FindTarget(Tank tank, TankAIHelper helper, Visible targetIn,  out Visible target)
        {   // Grants a much larger target search range

            float TargetRange = helper.RangeToChase * 2;
            Vector3 scanCenter = tank.boundsCentreWorldNoCheck;
            target = targetIn;
            if (target != null)
            {
                if ((target.tank.boundsCentreWorldNoCheck - scanCenter).magnitude > TargetRange)
                    target = null;
            }

            List<Tank> techs = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
            int launchCount = techs.Count();
            for (int step = 0; step < launchCount; step++)
            {
                Tank cTank = techs.ElementAt(step);
                if (cTank.IsEnemy(tank.Team) && cTank != tank)
                {
                    float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
                    if (dist < TargetRange)
                    {
                        TargetRange = dist;
                        target = cTank.visible;
                    }
                }
            }

            return target;
        }

        // Universal
        public static float Extremes(Vector3 input)
        {
            return Mathf.Max(Mathf.Max(input.x, input.y), input.z);
        }
        public static float ExtremesAbs(Vector3 input)
        {
            return Mathf.Max(Mathf.Max(Mathf.Abs(input.x), Mathf.Abs(input.y)), Mathf.Abs(input.z));
        }
        public static bool AIMessage(Tank tech, ref bool hasMessaged, string message)
        {
            if (KickStart.isAnimeAIPresent)
            {   // we send the action commentary to Anime AI mod
                AnimeAI.TransmitStatus(tech, message);
            }
            if (!hasMessaged && Feedback)
            {
                hasMessaged = true;
                Debug.Log("TACtical_AI: AI " + message);
            }
            return hasMessaged;
        }

        public class TankAIManager : MonoBehaviour
        {
            internal static FieldInfo rangeOverride = typeof(ManTechs).GetField("m_SleepRangeFromCamera", BindingFlags.NonPublic | BindingFlags.Instance);

            public static TankAIManager inst;

            public static EventNoParams QueueUpdater = new EventNoParams();
            public static void Initiate()
            {
                inst = new GameObject("AIManager").AddComponent<TankAIManager>();
                Allies = new List<Tank>();
                Minables = new List<Visible>();
                Depots = new List<ModuleHarvestReciever>();
                Chargers = new List<ModuleChargerTracker>();
                Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Subscribe(OnTankAddition);
                Singleton.Manager<ManTechs>.inst.TankTeamChangedEvent.Subscribe(OnTankChange);
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(OnTankRemoval);
                QueueUpdater.Subscribe(FetchAllAllies);
                Debug.Log("TACtical_AI: Created AIECore Manager.");

                // Only change if no other mod changed
                if ((float)rangeOverride.GetValue(ManTechs.inst) == 200f)
                {   // more than twice the range
                    rangeOverride.SetValue(ManTechs.inst, KickStart.EnemyExtendActionRange);
                }
            }
            public static void OnTankAddition(Tank tonk)
            {
                QueueUpdater.Send();
            }
            public static void OnTankChange(Tank tonk, ManTechs.TeamChangeInfo info)
            {
                QueueUpdater.Send();
            }
            public static void OnTankRemoval(Tank tonk, ManDamage.DamageInfo info)
            {
                QueueUpdater.Send();
            }

            public static void FetchAllAllies()
            {
                if (ManNetwork.inst.IsMultiplayer())
                    return; // Doesn't work in MP, cannot use opimised searcher.
                Allies.Clear();
                int AllyCount = 0;
                var allTechs = Singleton.Manager<ManTechs>.inst;
                int techCount = allTechs.CurrentTechs.Count();
                List<Tank> techs = allTechs.CurrentTechs.ToList();
                moreThan2Allies = false;
                try
                {
                    for (int stepper = 0; techCount > stepper; stepper++)
                    {
                        if (techs.ElementAt(stepper).IsFriendly() && !techs.ElementAt(stepper).gameObject.GetComponent<TankAIHelper>().IsMultiTech)
                        {   //Exclude MTs from this event
                            Allies.Add(techs.ElementAt(stepper));
                            //Debug.Log("TACtical_AI: Added " + Allies.ElementAt(AllyCount));
                            AllyCount++;
                        }
                    }
                    Debug.Log("TACtical_AI: Fetched allied tech list for AIs...");
                    if (AllyCount > 2)
                        moreThan2Allies = true;
                }
                catch  (Exception e)
                {
                    Debug.Log("TACtical_AI: Error on fetchlist");
                    Debug.Log(e);
                }
                if (AllyCount > 2)
                    moreThan2Allies = true;
                else
                    moreThan2Allies = false;
            }

            public void WarnPlayers()
            {
                try
                {
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>Warning: This server is using advanced AI!</b>");
                    Singleton.Manager<UIMPChat>.inst.AddMissionMessage("<b>  If you are inexperienced, I would suggest you play safe.</b>");
                }
                catch { }
            }
            /*
            private void Update()
            {
                if (Singleton.Manager<ManTechs>.inst.Count != lastTechCount)
                {
                    lastTechCount = Singleton.Manager<ManTechs>.inst.Count;
                    FetchAllAllies();
                }
            }
            */
        }


        public class TankAIHelper : MonoBehaviour
        {
            public Tank tank;
            public AITreeType.AITypes lastAIType;
            //Tweaks (controlled by Module)
            public AIType DediAI = AIType.Escort;
            private AlliedOperationsController _OpsController;
            public AlliedOperationsController OpsController
            {
                get
                {
                    if (this._OpsController != null)
                    {
                        return this._OpsController;
                    }
                    else
                    {
                        this._OpsController = new AlliedOperationsController(this);
                        return this._OpsController;
                    }
                }
            }

            public List<ModuleAIExtension> AIList;
            public AIERepair.DesignMemory TechMemor;

            // Constants
            internal const int DodgeStrength = 60;  //The motivation in trying to move away from a tech in the way
                                                    //250

            // Settables in ModuleAIExtension
            //   "turns on" functionality on the host Tech, none of these force it off
            public bool IsMultiTech = false;    // Should the other AIs ignore collision with this Tech?
            public bool PursueThreat = true;    // Should the AI chase the enemy?
            public bool RequestBuildBeam = true;// Should the AI Auto-BuildBeam on flip?

            public bool FullMelee = false;      // Should the AI ram the enemy?
            public bool OnlyPlayerMT = false;   // Should the AI only follow player movement while in MT mode?
            public bool AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
            public bool SecondAvoidence = false;// Should the AI avoid two techs at once?
            public bool SideToThreat = false;   // Should the AI circle the enemy?

            public float RangeToChase = 50;    // How far should we pursue the enemy?
            public float RangeToStopRush = 20;  // The range the AI will linger from the player
            public float IdealRangeCombat = 25; // The range the AI will linger from the enemy if PursueThreat is true
            public int AnchorAimDampening = 45; // How much do we dampen anchor movements by?

            public bool AutoAnchor = false;      // Should the AI toggle the anchor when it is still?

            // Repair Auxilliaries
            public bool allowAutoRepair = false;// Allied auto-repair
            public bool useInventory = false;   // Draw from player inventory reserves

            // Allied AI Operating Allowed types (self-filling)
            //   I'll convert these to flags later
            public bool isAssassinAvail = false;    //Is there an Assassin-enabled AI on this tech?
            public bool isAegisAvail = false;       //Is there an Aegis-enabled AI on this tech?

            public bool isProspectorAvail = false;  //Is there a Prospector-enabled AI on this tech?
            public bool isScrapperAvail = false;    //Is there a Scrapper-enabled AI on this tech?
            public bool isEnergizerAvail = false;   //Is there a Energizer-enabled AI on this tech?

            public bool isAviatorAvail = false;
            public bool isAstrotechAvail = false;
            public bool isBuccaneerAvail = false;

            
            // Action Handlers


            // General AI Handling
            public bool Hibernate = false;      // Disable the AI to make way for Default AI

            public int OverrideAim = 0; // 0 is off, 1 is enemy, 2 is obsticle

            public int AIState = 0;             // 0 is static, 1 is ally, 2 is enemy
            public int lastMoveAction = 0;      // [pending update]
            public int lastWeaponAction = 0;    // 0 is sleep, 1 is target, 2 is obsticle, 3 is mimic
            public bool updateCA = false;       // Collision avoidence active this FixedUpdate frame?
            public bool useAirControls = false; // Use the not-VehicleAICore cores
            internal int FrustrationMeter = 0;  // tardiness buildup before we use our guns to remove obsticles
            internal float Urgency = 0;         // tardiness buildup before we just ignore obstructions
            internal float UrgencyOverload = 0; // builds up too much if our max speed was set too high
            public bool PendingSystemsCheck;    // Is this tech damaged?
            public int AttemptedRepairs = 0;    // How many times have we tried fix
            public float DamageThreshold = 0;   // How much damage have we taken? (100 is total destruction)
            //internal float Oops = 0;

            // Directional Handling
            internal Vector3 lastDestination = Vector3.zero;    // Where we drive to in the world
            internal float lastRange = 0;

            //AutoCollection
            internal float EstTopSped = 0;
            internal float recentSpeed = 1;
            internal int anchorAttempts = 0;
            internal float lastTechExtents = 0;
            internal float lastAuxVal = 0;

            public Visible lastPlayer;
            public Visible lastEnemy;
            public Transform Obst;

            internal Tank LastCloseAlly;

            // Non-Tech specific objective AI Handling
            /// <summary>
            /// Counts also as [recharge home, block rally]
            /// </summary>
            internal bool ProceedToBase = false;
            /// <summary>
            /// Counts also as [loose block, target enemy, target to charge]
            /// </summary>
            internal bool ProceedToMine = false;
            internal float lastBaseExtremes = 10;

            /// <summary>
            /// Counts also as [recharge home, block rally]
            /// </summary>
            internal Tank theBase = null;
            /// <summary>
            /// Counts also as [loose block, target enemy, target to charge]
            /// </summary>
            internal Visible theResource = null;  

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
            internal bool Attempt3DNavi = false;            // Use 3D navigation  (VehicleAICore)
            internal Vector3 Navi3DDirect = Vector3.zero;   // Forwards facing for 3D
            internal Vector3 Navi3DUp = Vector3.zero;       // Upwards direction for 3D
            public float GroundOffsetHeight = 35;           // flote above ground this dist

            //Timestep
            internal int DirectorUpdateClock = 0;
            internal int OperationsUpdateClock = 500;
            //internal int DelayedUpdateClock = 500;
            internal int DirectionalHandoffDelay = 0;
            internal int DelayedAnchorClock = 0;
            internal int featherBoostersClock = 50;
            internal int repairStepperClock = 0;    
            internal int beamTimeoutClock = 0;
            internal int WeaponDelayClock = 0;
            //internal int LastBuildClock = 0;  
            internal int ActionPause = 0;               // when [val > 0], used to halt other actions 
            internal int unanchorCountdown = 0;         // aux warning for unanchor

            //Drive Direction Handlers
            /// <summary> Do we steer to target destination? </summary>
            internal bool Steer = false;

            /// <summary> Drive direction </summary>
            internal EDriveType DriveDir = EDriveType.Neutral;

            /// <summary> Drive AWAY from target </summary>
            internal bool AdviseAway = false;

            //Finals
            internal float MinimumRad = 0;              // Minimum radial spacing distance from destination
            internal float DriveVar = 0;                // Forwards drive (-1, 1)
            //internal bool IsLikelyJammed = false;
            internal bool Yield = false;                // Slow down and moderate top speed
            internal bool PivotOnly = false;            // Only aim at target
            internal bool ProceedToObjective = false;   // Drive to target
            internal bool MoveFromObjective = false;    // Drive from target POINTING AT TARGET [in relation to DriveDir]
            internal bool DANGER = false;               // Enemy nearby?
            internal bool AvoidStuff = true;            // Try avoiding allies and obsticles
            internal bool FIRE_NOW = false;             // hold down tech's spacebar
            internal bool BOOST = false;                // hold down boost button
            internal bool featherBoost = false;         // moderated booster pressing
            internal bool forceBeam = false;            // activate build beam
            internal bool forceDrive = false;           // Force the drive (cab forwards!) to a specific set value
            internal bool areWeFull = false;            // this Tech's storage objective status (resources, blocks, energy)
            internal bool Retreat = false;              // ignore enemy position and follow intended destination (but still return fire)

            internal bool JustUnanchored = false;       // flag to switch the AI back to enabled on unanchor
            internal bool PendingHeightCheck = false;   // queue a driving depth check for a naval tech
            internal float LowestPointOnTech = 0;       // the lowest point in relation to the tech's block-based center

            public bool OverrideAllControls = false;    // force the tech to be controlled by external means

            // AI Core
            public IMovementAIController MovementController;


            public void Subscribe(Tank tank)
            {
                Vector3 _ = tank.boundsCentreWorld;
                this.tank = tank;
                tank.AttachEvent.Subscribe(OnAttach);
                tank.DetachEvent.Subscribe(OnDetach);
                //tank.TankRecycledEvent.Subscribe(OnRecycle);
                this.AIList = new List<ModuleAIExtension>();
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(OnDeathOrRemoval);
                Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Subscribe(OnSpawn);
            }

            public void OnAttach(TankBlock newBlock, Tank tank)
            {
                if (tank != this.tank)
                    return;
                this.EstTopSped = 1;
                //this.LastBuildClock = 0;
                this.PendingHeightCheck = true;
                if (AIState == 1)
                {
                    try
                    {
                        if ((bool)TechMemor)
                        {
                            TechMemor.SaveTech();
                        }
                    }
                    catch { }
                }
            }
            public void OnDetach(TankBlock newBlock, Tank tank)
            {
                this.EstTopSped = 1;
                this.recentSpeed = 1;
                this.PendingHeightCheck = true;
                if (AIState == 1)
                {
                    try
                    {
                        if (!ManNetwork.IsNetworked)
                        {
                            if ((bool)TechMemor)
                            {
                                PendingSystemsCheck = true;
                                if (tank.blockman.LastRemovedBlocks.Contains(ManPointer.inst.targetVisible.block))
                                    TechMemor.SaveTech();
                            }
                        }
                        else if (ManNetwork.IsHost)
                        {
                            if ((bool)TechMemor)
                            {
                                PendingSystemsCheck = true;
                                if ((bool)lastPlayer)
                                    if (lastEnemy != null)// only save when not in combat
                                        TechMemor.SaveTech();
                            }
                        }
                    }
                    catch { }
                }
            }

            public void OnSpawn(Tank tankInfo)
            {
                if (tankInfo == tank)
                {
                    //Debug.Log("TACtical_AI: Allied AI " + tankInfo.name + ":  Called OnSpawn");
                    if (tankInfo.gameObject.GetComponent<TankAIHelper>().AIState != 0)
                        this.ResetAll(tankInfo);
                }
            }
            public void OnDeathOrRemoval(Tank tankInfo, ManDamage.DamageInfo damage)
            {
                if (tankInfo == tank)
                {
                    //Debug.Log("TACtical_AI: Allied AI " + tankInfo.name + ":  Called OnDeathOrRemoval");
                    //tankInfo.gameObject.GetComponent<TankAIHelper>().DediAI = AIType.Escort;
                    //this.ResetAll(tankInfo);
                    this.OverrideAllControls = false;
                }
            }
            /*
            public void OnRecycle(Tank tank)
            {
                //Debug.Log("TACtical_AI: Allied AI " + tank.name + ":  Called OnRecycle");
                tank.gameObject.GetComponent<TankAIHelper>().DediAI = AIType.Escort;
                this.ResetAll(tank);
            }
            */
            public void TrySetAITypeRemote(NetPlayer sender, AIType type)
            {
                if (ManNetwork.IsNetworked)
                {
                    if (sender == null)
                    {
                        Debug.Log("TACtical_AI: Host changed AI");
                        //Debug.Log("TACtical_AI: Anonymous sender error");
                        //return;
                    }
                    if (sender.TechTeamID == this.tank.Team)
                    {
                        OnSwitchAI();
                        DediAI = type;
                    }
                    else
                        Debug.Log("TACtical_AI: TrySetAITypeRemote - Invalid request received - player tried to change AI of Tech that wasn't theirs");
                }
                else
                    Debug.Log("TACtical_AI: TrySetAITypeRemote - Invalid request received - Tried to change AI type when not connected to a server!? \n  The UI handles this automatically!!!\n" + StackTraceUtility.ExtractStackTrace());
            }


            public void ResetToDefaultAIController()
            {
                if (!(this.MovementController is AIControllerDefault))
                {
                    //Debug.Log("TACtical_AI: Resetting Back to Default AI for " + tank.name);
                    IMovementAIController controller = this.MovementController;
                    this.MovementController = null;
                    if (controller != null)
                    {
                        controller.Recycle();
                    }
                    this.MovementController = gameObject.AddComponent<AIControllerDefault>();
                    this.MovementController.Initiate(this.tank, this);
                }
            }

            public void ResetAll(Tank tank)
            {
                //Debug.Log("TACtical_AI: Resetting all for " + tank.name);
                this.Hibernate = false;
                this.AIState = 0;
                this.lastAIType = AITreeType.AITypes.Idle;
                this.EstTopSped = 1;
                this.recentSpeed = 1;
                this.anchorAttempts = 0;
                this.DelayedAnchorClock = 0;
                this.foundBase = false;
                this.foundGoal = false;
                this.useInventory = false;
                this.lastBasePos = null;
                this.lastPlayer = null;
                this.lastEnemy = null;
                this.LastCloseAlly = null;
                this.theBase = null;
                this.JustUnanchored = false;
                this.OverrideAllControls = false;
                var Mind = tank.gameObject.GetComponent<Enemy.EnemyMind>();
                if (Mind.IsNotNull())
                    Mind.SetForRemoval();
                var Mem = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (Mem.IsNotNull())
                    Mem.Remove();

                this.ResetToDefaultAIController();

                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
                control3D.m_State.m_Beam = false;
                control3D.m_State.m_BoostJets = false;
                control3D.m_State.m_BoostProps = false;
                control3D.m_State.m_Fire = false;
                control3D.m_State.m_InputMovement = Vector3.zero;
                control3D.m_State.m_InputRotation = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);
            }

            public bool TestForFlyingAIRequirement()
            {
                var enemy = gameObject.GetComponent<Enemy.EnemyMind>();
                if (AIState == 1 && DediAI == AIType.Aviator)
                {
                    if (!(this.MovementController is AIControllerAir))
                    {
                        IMovementAIController controller = this.MovementController;
                        this.MovementController = null;
                        if (controller != null)
                        {
                            controller.Recycle();
                        }
                    }
                    this.MovementController = gameObject.GetOrAddComponent<AIControllerAir>();
                    this.MovementController.Initiate(tank, this);
                    return true;
                }
                else if (AIState == 2 && gameObject.GetComponent<Enemy.EnemyMind>().IsNotNull())
                {
                    if (enemy && enemy.EvilCommander == Enemy.EnemyHandling.Chopper || enemy.EvilCommander == Enemy.EnemyHandling.Airplane)
                    {
                        if (!(this.MovementController is AIControllerAir))
                        {
                            IMovementAIController controller = this.MovementController;
                            this.MovementController = null;
                            if (controller != null)
                            {
                                controller.Recycle();
                            }
                        }
                        this.MovementController = gameObject.GetOrAddComponent<AIControllerAir>();
                        this.MovementController.Initiate(tank, this, enemy);
                    }
                    return true;
                }
                else
                {
                    if (this.MovementController is AIControllerAir pilot)
                    {
                        this.MovementController = null;
                        pilot.Recycle();
                        this.MovementController = gameObject.GetOrAddComponent<AIControllerDefault>();
                        this.MovementController.Initiate(tank, this, enemy);
                    }
                    return false;
                }
            }

            public void RefreshAI()
            {
                AutoAnchor = false;     
                FullMelee = false;      // Should the AI ram the enemy?
                AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
                SecondAvoidence = false;// Should the AI avoid two techs at once?
                OnlyPlayerMT = true;
                SideToThreat = false;
                useInventory = false;

                isAegisAvail = false;
                isAssassinAvail = false;

                isProspectorAvail = false;
                isScrapperAvail = false;
                isEnergizerAvail = false;

                isAstrotechAvail = false;
                isAviatorAvail = false;
                isBuccaneerAvail = false;

                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
                control3D.m_State.m_Beam = false;
                control3D.m_State.m_BoostJets = false;
                control3D.m_State.m_BoostProps = false;
                control3D.m_State.m_Fire = false;
                control3D.m_State.m_InputMovement = Vector3.zero;
                control3D.m_State.m_InputRotation = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);

                AIList.Clear();
                List<ModuleAIBot> AIs = tank.blockman.IterateBlockComponents<ModuleAIBot>().ToList();
                foreach (ModuleAIBot bot in AIs)
                {
                    var AIE = bot.gameObject.GetComponent<ModuleAIExtension>();
                    if (AIE.IsNotNull())
                        AIList.Add(AIE);
                }
                Debug.Log("TACtical_AI: AI list for Tech " + tank.name + " has " + AIList.Count() + " entries");

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
                    if (AIEx.MeleePreferred)
                        FullMelee = true;
                    if (AIEx.AdvAvoidence)
                        SecondAvoidence = true;
                    if (AIEx.MTForAll)
                        OnlyPlayerMT = false;
                    if (AIEx.SidePreferred)
                        SideToThreat = true;
                    if (AIEx.SelfRepairAI)
                        allowAutoRepair = true;

                    // Engadgement Ranges
                    if (AIEx.MinCombatRange > IdealRangeCombat)
                        IdealRangeCombat = AIEx.MinCombatRange;
                    if (AIEx.MaxCombatRange > RangeToChase)
                        RangeToChase = AIEx.MaxCombatRange;
                }
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
                        DediAI = AIType.Escort;
                        break;
                    case AIType.Buccaneer:
                        if (isBuccaneerAvail) break;
                        DediAI = AIType.Escort;
                        break;
                    case AIType.Astrotech:
                        if (isAstrotechAvail) break;
                        DediAI = AIType.Escort;
                        break;
                }

                this.MovementController = null;
                Enemy.EnemyMind enemy = gameObject.GetComponent<Enemy.EnemyMind>();

                if (!isAviatorAvail)
                {
                    if (DediAI == AIType.Aviator)
                        DediAI = AIType.Escort;

                    AIControllerAir airController = gameObject.GetComponent<AIControllerAir>();
                    if (airController.IsNotNull())
                    {
                        airController.Recycle();
                    }

                    this.MovementController = gameObject.GetOrAddComponent<AIControllerDefault>();
                }
                else if (DediAI == AIType.Aviator)
                {
                    this.TestForFlyingAIRequirement();
                }

                if (this.MovementController != null)
                {
                    this.MovementController.Initiate(tank, this, enemy);
                }
                else
                {
                    this.ResetToDefaultAIController();
                }

                if (allowAutoRepair)
                {
                    if (TechMemor.IsNull())
                        tank.gameObject.AddComponent<AIERepair.DesignMemory>().Initiate();
                }
                else
                {
                    if (TechMemor.IsNotNull())
                        TechMemor.Remove();
                }
            }

            public void OnSwitchAI()
            {
                this.AvoidStuff = true;
                this.EstTopSped = 1;
                this.foundBase = false;
                this.foundGoal = false;
                this.lastBasePos = null;
                this.lastPlayer = null;
                this.lastEnemy = null;
                this.LastCloseAlly = null;
                this.theBase = null;
                this.JustUnanchored = false;
            }
            public void ForceAllAIsToEscort()
            {
                //Needed to return AI mode back to Escort on unanchor as unanchoring causes it to go to idle
                try
                {
                    tank.AI.SetBehaviorType(AITreeType.AITypes.Escort);
                }
                catch { }
            }

            private void DetermineCombat()
            {
                bool DoNotEngage = false;
                if (this.lastPlayer.IsNotNull())
                {
                    if (this.lastBasePos.IsNotNull())
                    {
                        if (this.IdealRangeCombat * 2 < (this.lastBasePos.position - this.tank.boundsCentreWorldNoCheck).magnitude && this.DediAI == AIType.Assault)
                            DoNotEngage = true;
                    }
                    if (this.IdealRangeCombat < (this.lastPlayer.tank.boundsCentreWorldNoCheck - this.tank.boundsCentreWorldNoCheck).magnitude && this.DediAI != AIType.Assault)
                        DoNotEngage = true;
                    else if (this.AdvancedAI)
                    {
                        //WIP
                        if (this.DamageThreshold > 30)
                        {
                            DoNotEngage = true;
                        }
                    }
                }
                this.Retreat = DoNotEngage;
            }

            /// <summary>
            /// Main controller for ALL AI
            /// </summary>
            /// <param name="thisControl"></param>
            public void BetterAI(TankControl thisControl)
            {
                // The interface method for actually handling the tank - note that this fires at a different rate
                this.lastTechExtents = Extremes(this.tank.blockBounds.extents);

                if (OverrideAllControls)
                    return; 

                if (this.MovementController is null)
                {
                    Debug.Log("NULL MOVEMENT CONTROLLER");
                }

                AIEBeam.BeamMaintainer(thisControl, this, this.tank);
                if (this.updateCA)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Fired CollisionAvoidUpdate!");
                    try
                    {
                        AIEWeapons.WeaponDirector(thisControl, this, this.tank);

                        this.AdviseAway = false;
                        this.MovementController.DriveDirector();
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: AI " + tank.name + ":  Potential error in DriveDirector (or WeaponDirector)!");
                    }

                    this.updateCA = false; // incase they fall out of sync
                }
                try
                {
                    AIEWeapons.WeaponMaintainer(thisControl, this, this.tank);
                    this.MovementController.DriveMaintainer(thisControl);
                }
                catch
                {
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  Potential error in DriveMaintainer (or WeaponMaintainer)!");
                }
            }
            
            /// <summary>
            /// Gets the opposite direction of the target tech, accounting for size
            /// </summary>
            /// <param name="targetToAvoid"></param>
            /// <returns></returns>
            public Vector3 GetOtherDir(Tank targetToAvoid)
            {
                //What actually does the avoidence
                //Debug.Log("TACtical_AI: GetOtherDir");
                Vector3 inputOffset = tank.transform.position - targetToAvoid.transform.position;
                float inputSpacing = Extremes(targetToAvoid.blockBounds.extents) + this.lastTechExtents + DodgeStrength;
                Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
                return Final;
            }


            // Collision Avoidence
            public Vector3 AvoidAssist(Vector3 targetIn)
            {
                //The method to determine if we should avoid an ally nearby while navigating to the target
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                //thisInst.IsLikelyJammed = false;
                if (thisInst.AvoidStuff)
                {
                    try
                    {
                        Tank lastCloseAlly;
                        float lastAllyDist;
                        if (thisInst.SecondAvoidence && moreThan2Allies)// MORE processing power
                        {
                            lastCloseAlly = AIEPathing.SecondClosestAlly(tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                            if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                            {
                                if (lastAuxVal < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    //thisInst.IsLikelyJammed = true;
                                    IntVector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst2, thisInst.AdvancedAI);
                                    if (obst2)
                                        return (targetIn + ProccessedVal2) / 4;
                                    else
                                        return (targetIn + ProccessedVal2) / 3;

                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                                //thisInst.IsLikelyJammed = true;
                                IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 3;
                                else
                                    return (targetIn + ProccessedVal) / 2;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAlly(tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                        //Debug.Log("TACtical_AI: Ally is " + thisInst.lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        //if (lastCloseAlly == null)
                        //    Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //thisInst.IsLikelyJammed = true;
                            IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 3;
                            else
                                return (targetIn + ProccessedVal) / 2;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Crash on Avoid " + e);
                        return targetIn;
                    }
                }
                if (targetIn.IsNaN())
                {
                    Debug.Log("TACtical_AI: AvoidAssist IS NaN!!");
                    TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }
            public Vector3 AvoidAssistPrecise(Vector3 targetIn)
            {
                //The method to determine if we should avoid an ally nearby while navigating to the target
                //  MORE DEMANDING THAN THE ABOVE!
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                if (thisInst.AvoidStuff)
                {
                    try
                    {
                        Tank lastCloseAlly;
                        float lastAllyDist;
                        if (thisInst.SecondAvoidence && moreThan2Allies)// MORE processing power
                        {
                            lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                            if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                            {
                                if (lastAuxVal < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    IntVector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst2, thisInst.AdvancedAI);
                                    if (obst2)
                                        return (targetIn + ProccessedVal2) / 4;
                                    else
                                        return (targetIn + ProccessedVal2) / 3;
                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);

                                IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 3;
                                else
                                    return (targetIn + ProccessedVal) / 2;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                        //Debug.Log("TACtical_AI: Ally is " + thisInst.lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        //if (lastCloseAlly == null)
                        //    Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 3;
                            else
                                return (targetIn + ProccessedVal) / 2;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Crash on Avoid " + e);
                        return targetIn;
                    }
                }
                if (targetIn.IsNaN())
                {
                    Debug.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                    TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }

            // Obstruction Management
            public void TryHandleObstruction(bool hasMessaged, float dist, bool useRush, bool useGun)
            {
                //Something is in the way - try fetch the scenery to shoot at
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Obstructed");
                if (!hasMessaged)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Can't move there - something's in the way!");
                }

                this.forceDrive = true;
                this.DriveVar = 1;

                this.UrgencyOverload += KickStart.AIClockPeriod / 2;
                if (this.Urgency > 0)
                    this.Urgency += KickStart.AIClockPeriod / 5;
                if (this.UrgencyOverload > 50)
                {
                    //Are we just randomly angry for too long? let's fix that
                    AIMessage(tech: tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                    this.EstTopSped = 1;
                    this.AvoidStuff = true;
                    this.UrgencyOverload = 0;
                }
                else if (useRush && dist > this.RangeToStopRush * 2)
                {
                    //SCREW IT - GO FULL SPEED WE ARE TOO FAR BEHIND!
                    if (useGun)
                        RemoveObstruction(); 
                    this.forceDrive = true;
                    this.DriveVar = 1f;
                    this.Urgency += KickStart.AIClockPeriod / 5;
                }
                else if (10 < this.FrustrationMeter)
                {
                    //Try build beaming to clear debris
                    this.FrustrationMeter += KickStart.AIClockPeriod / 5;
                    if (30 < this.FrustrationMeter)
                    {
                        this.FrustrationMeter = 0;
                    }
                    else if (15 < this.FrustrationMeter)
                    {
                        this.forceDrive = true;
                        this.DriveVar = -1;
                    }
                    else
                        this.forceBeam = true;
                }
                else
                {
                    //Shoot the freaking tree
                    this.FrustrationMeter += KickStart.AIClockPeriod / 5;
                    if (useGun)
                        RemoveObstruction();
                    this.forceDrive = true;
                    this.DriveVar = 0.5f;
                }
            }
            /*
            public Transform GetObstruction() //VERY expensive operation - only use if absoluetely nesseary
            {
                // Get the scenery that's obstructing if there's any (ignores monuments to be fair to Enemy AI)
                LayerMask Filter = Globals.inst.layerScenery.mask;
                float ext = Extremes(tank.blockBounds.extents);
                Physics.SphereCast(tank.rbody.centerOfMass - tank.transform.InverseTransformVector(Vector3.forward * ext), ext, tank.blockman.GetRootBlock().transform.forward, out RaycastHit Pummel, 100, Filter, QueryTriggerInteraction.Ignore);
                Transform Obstruction;
                try
                {
                    var vaildTar = Pummel.collider.transform.parent.parent.parent.gameObject.transform;
                    if (vaildTar != null)
                    {
                        Obstruction = vaildTar;
                        Debug.Log("TACtical_AI: GetObstruction - found " + Obstruction.name);
                        return Obstruction;
                    }
                    Debug.Log("TACtical_AI: GetObstruction - Expected Scenery but got " + Pummel.collider.transform.parent.parent.parent.gameObject.name + " instead");
                    //avoid _GameManager - crashes can happen
                    //Debug.Log("TACtical_AI: GetObstruction - host gameobject " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(Pummel.collider.transform.root.gameObject, Pummel.collider.transform.root.gameObject.name));

                }
                catch
                {
                    //Debug.Log("TACtical_AI: GetObstruction - DID NOT HIT ANYTHING");
                }
                return null;
            }
            */
            public Transform GetObstruction()
            {
                List<Visible> ObstList = AIEPathing.ObstructionAwareness(tank.boundsCentreWorldNoCheck, this);
                int bestStep = 0;
                float bestValue = 500;
                int steps = ObstList.Count;
                if (steps <= 0)
                {
                    //Debug.Log("TACtical_AI: GetObstruction - DID NOT HIT ANYTHING");
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
                //Debug.Log("TACtical_AI: GetObstruction - found " + ObstList.ElementAt(bestStep).name);
                return ObstList.ElementAt(bestStep).trans;
            }
            public void RemoveObstruction()
            {
                // Shoot at the scenery obsticle infront of us
                if (Obst == null)
                {
                    this.Obst = GetObstruction();
                    this.Urgency += KickStart.AIClockPeriod / 5;
                }
                this.FIRE_NOW = true;
            }
            public void SettleDown()
            {
                this.UrgencyOverload = 0;
                this.Urgency = 0;
                this.FrustrationMeter = 0;
                this.Obst = null;
            }


            public void AllowApproach()
            {
                if (AvoidStuff)
                {
                    AvoidStuff = false;
                    CancelInvoke();
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Allowing approach");
                    Invoke("StopAllowApproach", 2);
                }
            }
            private void StopAllowApproach()
            {
                if (!AvoidStuff)
                {
                    AvoidStuff = true;
                }
            }

            public bool IsTechMoving(float minSpeed)
            {
                if (tank.rbody.IsNull())
                    return false;
                return tank.rbody.velocity.sqrMagnitude > minSpeed * minSpeed;
            }
            public Visible GetPlayerTech()
            {
                if (ManNetwork.IsNetworked)
                {
                    foreach (Visible thatTech in tank.Vision.IterateVisibles(ObjectTypes.Vehicle))
                    {
                        try
                        {
                            if ((bool)thatTech.tank.netTech.NetPlayer.CurTech == thatTech)
                                return thatTech;
                        }
                        catch { }
                    }
                }
                else
                {
                    /*
                    foreach (Visible thatTech in tank.Vision.IterateVisibles(ObjectTypes.Vehicle))
                    {
                        if (thatTech.tank.PlayerFocused)
                            return thatTech;
                    }*/
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
                Debug.Log("TACtical_AI: AI " + tank.name + ":  lowest point set " + lowest);
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
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  lowest point set " + LowestPointOnTech);
                }
                return isTrue;
            }

            public void TryRepairAllied()
            {
                if (allowAutoRepair)
                {
                    if (lastEnemy != null)
                    {   // Combat repairs (combat mechanic)
                        if (AdvancedAI) // must be smrt
                            AIERepair.RepairStepper(this, tank, TechMemor, 40);
                        else
                            AIERepair.RepairStepper(this, tank, TechMemor, 65);
                    }
                    else
                    {   // Repairs in peacetime
                        if (AdvancedAI) // faster for smrt
                            AIERepair.InstaRepair(tank, TechMemor, KickStart.AIClockPeriod);
                        else
                            AIERepair.RepairStepper(this, tank, TechMemor, 25);
                    } 
                }
            }


            public void RunAlliedOperations()
            {
                var aI = tank.AI;
                TryRepairAllied();

                if (!aI.TryGetCurrentAIType(out this.lastAIType))
                {
                    this.lastAIType = AITreeType.AITypes.Idle;
                    return;
                }
                if (this.lastAIType == AITreeType.AITypes.Escort || this.lastAIType == AITreeType.AITypes.Guard)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Fired DelayedUpdate!");
                    this.Attempt3DNavi = false;

                    //this.updateCA = true;
                    if (this.ActionPause > 0)
                        this.ActionPause--;
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  current mode " + this.DediAI.ToString());

                    this.OpsController.Execute();
                    this.DetermineCombat();
                }
            }

            /// <summary>
            /// Extension for TougherEnemies to toggle
            /// </summary>
            public void RunEnemyOperations()
            {
                //BEGIN THE PAIN!
                //this.updateCA = true;
                if (this.ActionPause > 0)
                    this.ActionPause--;
                Enemy.RCore.BeEvil(this, tank);
            }

            public void DelayedRepairUpdate()
            {   //OBSOLETE until further notice
                // Dynamic timescaled update that fires when needed, less for slow techs, fast for large techs
            }
            public void RemoveEnemyMatters()
            {
                var AISettings = tank.GetComponent<AIBookmarker>();
                if (AISettings.IsNotNull())
                    DestroyImmediate(AISettings);
                var Builder = tank.GetComponent<BookmarkBuilder>();
                if (Builder.IsNotNull())
                    DestroyImmediate(Builder);
            }


            public void FixedUpdate()
            {
                //Handler for the improved AI, messy but gets the job done.
                if (KickStart.EnableBetterAI) //&& !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                {
                    var aI = tank.AI;
                    if (tank.IsFriendly() && aI.CheckAIAvailable())//aI.CheckAIAvailable()
                    {   //MP is NOT supported!
                        //Player-Allied AI
                        if (this.AIState != 1)
                        {
                            ResetAll(this.tank);
                            RemoveEnemyMatters();
                            this.AIState = 1;
                            this.RefreshAI();
                            if ((bool)TechMemor)
                                TechMemor.SaveTech();
                            Debug.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go!");
                        }

                        if (OverrideAllControls)
                            return;

                        this.DirectorUpdateClock++;
                        if (this.DirectorUpdateClock > KickStart.AIDodgeCheapness)
                        {
                            this.updateCA = true;
                            this.DirectorUpdateClock = 0;
                        }

                        if (this.recentSpeed < 1)
                            this.recentSpeed = 1;
                        this.OperationsUpdateClock++;
                        if (this.OperationsUpdateClock > KickStart.AIClockPeriod)//Mathf.Max(25 / this.recentSpeed, 5)
                        {
                            this.recentSpeed = tank.GetForwardSpeed();
                            RunAlliedOperations();
                            this.OperationsUpdateClock = 0;
                            if (this.EstTopSped < this.recentSpeed)
                                this.EstTopSped = this.recentSpeed;
                        }
                    }
                    else if ((KickStart.testEnemyAI || KickStart.isTougherEnemiesPresent) && KickStart.enablePainMode && tank.IsEnemy() && !ManSpawn.IsPlayerTeam(tank.Team))
                    {   //MP is NOT supported!
                        //Enemy AI
                        if (this.AIState != 2)
                        {
                            ResetAll(this.tank);
                            this.AIState = 2;
                            Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Ready to kick some Tech!");
                            Enemy.RCore.RandomizeBrain(this, tank);
                        }
                        if (OverrideAllControls)
                            return;
                        if (!this.Hibernate)
                        {
                            this.DirectorUpdateClock++;
                            if (this.DirectorUpdateClock > KickStart.AIDodgeCheapness)
                            {
                                this.updateCA = true;
                                this.DirectorUpdateClock = 0;
                            }
                            else
                                this.updateCA = false;

                            if (this.recentSpeed < 1)
                                this.recentSpeed = 1;
                            this.OperationsUpdateClock++;
                            if (this.OperationsUpdateClock > KickStart.AIClockPeriod)
                            {
                                this.recentSpeed = tank.GetForwardSpeed();
                                RunEnemyOperations();
                                this.OperationsUpdateClock = 0;
                                if (this.EstTopSped < this.recentSpeed)
                                    this.EstTopSped = this.recentSpeed;
                            }
                        }
                    }
                    else
                    {   // Static tech
                        this.DriveVar = 0;
                        if (this.AIState > 0)
                        {   // Reset and ready for static tech
                            Debug.Log("TACtical_AI: Static Tech " + tank.name + ": reset");
                            ResetAll(this.tank);
                            RemoveEnemyMatters();
                            this.AIState = 0;
                        }
                    }
                }
            }

        }
    }
}
