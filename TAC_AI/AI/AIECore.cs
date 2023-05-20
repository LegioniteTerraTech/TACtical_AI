using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTech.Network;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.Templates;
using TAC_AI.World;
using RandomAdditions;
using TerraTechETCUtil;

namespace TAC_AI.AI
{
    /*
        Summary of functions: Handles Allied AI and Enemy AI
        
        AIECore contains the macro parameters that direct AI execution

        AI Control is handled via 2 separate execution flows.
        
        Flow 1 - Execution of plan:
        TankControl.Update -> ModuleTechController.ExecuteControl -> TankAIHelper.BetterAI
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

        //-------------------------------------
        //           LIVE VARIABLES
        //-------------------------------------
        // Note: All neutrals are under -1 (-256) for this mod
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static HashSet<SceneryTypes> IndestructableScenery = new HashSet<SceneryTypes>
        {
            SceneryTypes.Pillar, SceneryTypes.ScrapPile,
        };


        public static Event<Tank, string> AIMessageEvent = new Event<Tank, string>();

        public static List<TankAIHelper> AllHelpers;
        //public static List<ResourceDispenser> Minables;
        public static List<Visible> Minables;
        public static List<ModuleHarvestReciever> Depots;
        public static List<ModuleHarvestReciever> BlockHandlers;
        public static List<ModuleChargerTracker> Chargers;
        public static HashSet<int> RetreatingTeams;
        public static bool PlayerIsInNonCombatZone => _playerIsInNonCombatZone;
        private static bool _playerIsInNonCombatZone = false;
        private static bool PlayerCombatLastState = false;
        //private static int lastTechCount = 0;

        // legdev
        internal static bool Feedback = false;// set this to true to get AI feedback testing
#if DEBUG
        internal static bool debugVisuals = true;// set this to true to get AI visual testing
#else
        internal static bool debugVisuals = false;// set this to true to get AI visual testing
#endif


        // Mining
        public static bool FetchClosestChunkReceiver(Vector3 tankPos, float MaxScanRange, out Transform finalPos, out Tank theBase, int team)
        {
            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = MaxScanRange * MaxScanRange;// MAX SCAN RANGE
            foreach (ModuleHarvestReciever reciever in Depots)
            {
                if (!reciever.tank.boundsCentreWorldNoCheck.Approximately(tankPos, 1) && reciever.tank.Team == team)
                {
                    float temp = (reciever.trans.position - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp != 0)
                    {
                        fired = true;
                        theBase = reciever.tank;
                        bestValue = temp;
                        finalPos = reciever;
                    }
                }
            }
            return fired;
        }
        public static bool FetchClosestResource(Vector3 tankPos, float MaxScanRange, float MaxDepth, out Visible theResource)
        {
            bool fired = false;
            theResource = null;
            float bestValue = MaxScanRange * MaxScanRange;// MAX SCAN RANGE
            int run = Minables.Count;
            for (int step = 0; step < run; step++)
            {
                var trans = Minables.ElementAt(step);
                if (trans.isActive && trans.trans.position.y <= MaxDepth)
                {
                    var res = trans.GetComponent<ResourceDispenser>();
                    if (!res.IsDeactivated && res.visible.isActive)
                    {
                        //DebugTAC_AI.Log("TACtical_AI:Skipped over inactive");
                        if (!trans.GetComponent<Damageable>().Invulnerable && 
                            !IndestructableScenery.Contains(res.GetSceneryType()))
                        {
                            //DebugTAC_AI.Log("TACtical_AI: Skipped over invincible");
                            float temp = (trans.trans.position - tankPos).sqrMagnitude;
                            if (bestValue > temp && temp != 0)
                            {
                                theResource = trans;
                                fired = true;
                                bestValue = temp;
                            }
                            continue;
                        }
                    }
                }
                Minables.Remove(trans);//it's invalid and must be removed
                step--;
                run--;
            }
            return fired;
        }

        // Scavenging - Under Construction!
        //private static List<Visible> looseBlocksCache = new List<Visible>();

        public static bool FetchClosestBlockReceiver(Vector3 tankPos, float MaxScanRange, out Transform finalPos, out Tank theBase, int team)
        {
            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = MaxScanRange * MaxScanRange;// MAX SCAN RANGE
            foreach (ModuleHarvestReciever reciever in BlockHandlers)
            {
                if (!reciever.tank.boundsCentreWorldNoCheck.Approximately(tankPos, 1) && reciever.tank.Team == team)
                {
                    float temp = (reciever.trans.position - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp != 0)
                    {
                        fired = true;
                        theBase = reciever.trans.root.GetComponent<Tank>();
                        bestValue = temp;
                        finalPos = reciever;
                    }
                }
            }
            return fired;
        }
        public static bool FetchLooseBlocks(Vector3 tankPos, float MaxScanRange, out Visible theResource)
        {
            bool fired = false;
            theResource = null;
            float bestValue = MaxScanRange * MaxScanRange;// MAX SCAN RANGE
            foreach (Visible vis in ManWorld.inst.TileManager.IterateVisibles(ObjectTypes.Block, tankPos, MaxScanRange))
            {
                if (!vis?.block || !vis.isActive)
                    continue;
                if (vis.block.IsAttached || vis.InBeam || !vis.IsInteractible || ManPointer.inst.DraggingItem == vis)
                    continue;   // no grab aquired blocks
                try
                {
                    float temp = (vis.centrePosition - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp > 1)
                    {
                        fired = true;
                        bestValue = temp;
                        theResource = vis;
                    }
                }
                catch { }
            }
            //DebugTAC_AI.Log("found Block " + fired);
            return fired;
        }


        internal static FieldInfo blocksGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);


        // Multi-Techs
        public static bool FetchCopyableAlly(Vector3 tankPos, TankAIHelper helper, out float distanceSqr, out Visible ToFetch)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            distanceSqr = 62500;
            int bestStep = -1;
            bool fired = false;
            ToFetch = null;
            try
            {
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(helper.tank);
                for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                {
                    Tank ally = AlliesAlt.ElementAt(stepper);
                    if (ally.GetHelperInsured().CanCopyControls)
                    {
                        float temp = (ally.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                        if (distanceSqr > temp && temp > 1)
                        {
                            distanceSqr = temp;
                            bestStep = stepper;
                            fired = true;
                        }
                    }
                }
                if (bestStep == -1)
                    return false;
                ToFetch = AlliesAlt.ElementAt(bestStep).visible;
                //DebugTAC_AI.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
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
                if (charge.tank != tank && charge.tank.Team == team && charge.CanTransferCharge(tank))
                {
                    float temp = (charge.trans.position - tankPos).sqrMagnitude;
                    if (bestValue > temp && temp > 1)
                    {
                        fired = true;
                        theBase = charge.tank;
                        bestValue = temp;
                        finalPos = charge.trans;
                    }
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
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(helper.tank);
                for (int stepper = 0; AlliesAlt.Count > stepper; stepper++)
                {
                    Tank ally = AlliesAlt.ElementAt(stepper);
                    float temp = (ally.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    EnergyRegulator.EnergyState eState = ally.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                    bool hasCapacity = eState.storageTotal > 200;
                    bool needsCharge = (eState.storageTotal - eState.spareCapacity) / eState.storageTotal < AIGlobals.minimumChargeFractionToConsider;
                    if (hasCapacity && needsCharge)
                    {
                        if (Range > temp && temp > 1)
                        {
                            Range = temp;
                            bestStep = stepper;
                            fired = true;
                        }
                    }
                }
                toCharge = AlliesAlt.ElementAt(bestStep).visible;
                //DebugTAC_AI.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
            }
            return fired;
        }

        // Assassin
        public static bool FindTarget(Tank tank, TankAIHelper helper, Visible targetIn, out Visible target)
        {   // Grants a much larger target search range

            float TargetRange = helper.MaxCombatRange * 2;
            TargetRange *= TargetRange;
            Vector3 scanCenter = tank.boundsCentreWorldNoCheck;
            target = targetIn;
            if (target != null)
            {
                if (target.tank == null)
                {
                    target = null;
                    return false;
                }
                else if ((target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRange)
                    target = null;
            }

            foreach (var cTank in TankAIManager.GetTargetTanks(tank.Team))
            {
                float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                if (dist <= TargetRange)
                {
                    TargetRange = dist;
                    target = cTank.visible;
                }
            }

            return target;
        }

        // Universal
        /*
        public static float Extremes(Vector3 input)
        {
            return Mathf.Max(Mathf.Max(input.x, input.y), input.z);
        }
        */
        public static float ExtremesAbs(Vector3 input)
        {
            return Mathf.Max(Mathf.Max(Mathf.Abs(input.x), Mathf.Abs(input.y)), Mathf.Abs(input.z));
        }
        public static bool AIMessage(Tank tech, ref bool hasMessaged, string message)
        {
            AIMessageEvent.Send(tech, message);
            if (!hasMessaged && Feedback)
            {
                hasMessaged = true;
                DebugTAC_AI.Log("TACtical_AI: AI " + message);
            }
            return hasMessaged;
        }
        public static void AIMessage(Tank tech, string message)
        {
            AIMessageEvent.Send(tech, message);
            if (Feedback)
                DebugTAC_AI.Log("TACtical_AI: AI " + message);
        }
        public static void TeamRetreat(int Team, bool Retreat, bool Sending = false)
        {
            try
            {
                if (Retreat)
                {
                    if (!RetreatingTeams.Contains(Team))
                    {
                        RetreatingTeams.Add(Team);
                        if (Sending && ManNetwork.IsNetworked)
                            NetworkHandler.TryBroadcastNewRetreatState(Team, true);
                        int playerTeam = Singleton.Manager<ManPlayer>.inst.PlayerTeam;
                        if (Team == playerTeam)
                        {
                            foreach (Tank tech in TankAIManager.GetTeamTanks(playerTeam))
                            {
                                if (!tech.IsAnchored && tech.GetComponent<TankAIHelper>().lastAIType != AITreeType.AITypes.Idle)
                                {
                                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                    AIGlobals.PopupPlayerInfo("Fall back!", worPos);
                                }
                            }
                        }
                        else if (AIGlobals.IsNeutralBaseTeam(Team))
                        {
                            foreach (Tank tech in AIECore.TankAIManager.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupNeutralInfo("Fall back!", worPos);
                            }
                        }
                        else if (AIGlobals.IsFriendlyBaseTeam(Team))
                        {
                            foreach (Tank tech in AIECore.TankAIManager.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupAllyInfo("Fall back!", worPos);
                            }
                        }
                        else
                        {
                            foreach (Tank tech in AIECore.TankAIManager.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupEnemyInfo("Fall back!", worPos);
                            }
                        }
                    }
                }
                else
                {
                    if (RetreatingTeams.Remove(Team))
                    {
                        if (Sending && ManNetwork.IsNetworked)
                            NetworkHandler.TryBroadcastNewRetreatState(Team, false);
                        if (Team == Singleton.Manager<ManPlayer>.inst.PlayerTeam)
                        {
                            foreach (Tank tech in TankAIManager.GetTeamTanks(Singleton.Manager<ManPlayer>.inst.PlayerTeam))
                            {
                                if (!tech.IsAnchored && tech.GetComponent<TankAIHelper>().lastAIType != AITreeType.AITypes.Idle)
                                {
                                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                    AIGlobals.PopupPlayerInfo("Engage!", worPos);
                                }
                            }
                        }
                        else if (AIGlobals.IsNeutralBaseTeam(Team))
                        {
                            foreach (Tank tech in AIECore.TankAIManager.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupNeutralInfo("Engage!", worPos);
                            }
                        }
                        else if (AIGlobals.IsFriendlyBaseTeam(Team))
                        {
                            foreach (Tank tech in AIECore.TankAIManager.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupAllyInfo("Engage!", worPos);
                            }
                        }
                        else
                        {
                            foreach (Tank tech in AIECore.TankAIManager.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupEnemyInfo("Engage!", worPos);
                            }
                        }
                    }
                }
            }
            catch { DebugTAC_AI.Log("TACtical_AI: TeamRetreat encountered an error, perhaps in Attract?"); }
        }
        public static void ToggleTeamRetreat(int Team)
        {
            if (RetreatingTeams.Contains(Team))
            {
                TeamRetreat(Team, false, true);
            }
            else
            {
                TeamRetreat(Team, true, true);
            }
        }

        // MISC
        public static bool IsTankValid(Transform trans)
        {
            var AICommand = trans.root.GetComponent<TankAIHelper>();
            if (AICommand.IsNotNull() && !KickStart.isWeaponAimModPresent)
            {
                var tank = trans.root.GetComponent<Tank>();
                if (tank.IsNotNull())
                    return true;
            }
            return false;
        }
        public static bool IsTechAimingScenery(Transform trans)
        {
            var AICommand = trans.root.GetComponent<TankAIHelper>();
            if (AICommand.IsNotNull() && !KickStart.isWeaponAimModPresent)
            {
                var tank = trans.root.GetComponent<Tank>();
                if (tank.IsNotNull())
                    if (!tank.PlayerFocused && AICommand.ActiveAimState == AIWeaponState.Obsticle)
                        return true;
            }
            return false;
        }

        public static bool HasOmniCore(TankBlock block)
        {
            return block.GetComponent<ModuleOmniCore>() && !block.GetComponent<ModuleWheels>();
        }

        public static bool ShouldBeStationary(Tank tank, TankAIHelper helper)
        {
            if (helper.AutoAnchor)
            {
                if (tank.IsAnchored && !helper.PlayerAllowAutoAnchoring)
                    return true;
            }
            else if (tank.IsAnchored)
            {
                return true;
            }
            return false;
        }
        public static AIDriverType HandlingDetermine(Tank tank, TankAIHelper helper)
        {
            var BM = tank.blockman;

            if (ShouldBeStationary(tank, helper))
            {
                return AIDriverType.Stationary;
            }

            if (KickStart.IsRandomAdditionsPresent)
            {
                try
                {
                    foreach (var item in BM.IterateBlocks())
                    {
                        if (HasOmniCore(item))
                            return AIDriverType.Astronaut;
                    }
                }
                catch { };
            }

            bool isFlying = false;
            bool isFlyingDirectionForwards = true;
            List<ModuleBooster> Engines = BM.IterateBlockComponents<ModuleBooster>().ToList();
            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            foreach (ModuleBooster module in Engines)
            {
                //Get the slowest spooling one
                List<FanJet> jets = module.transform.GetComponentsInChildren<FanJet>().ToList();
                foreach (FanJet jet in jets)
                {
                    if (jet.spinDelta <= 10)
                    {
                        biasDirection -= tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForwards) * jet.force;
                    }
                }
                List<BoosterJet> boosts = module.transform.GetComponentsInChildren<BoosterJet>().ToList();
                foreach (BoosterJet boost in boosts)
                {
                    //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                    boostBiasDirection -= tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection));
                }
            }
            boostBiasDirection.Normalize();
            biasDirection.Normalize();

            if (biasDirection == Vector3.zero && boostBiasDirection != Vector3.zero)
            {
                isFlying = true;
                if (boostBiasDirection.y > 0.6)
                    isFlyingDirectionForwards = false;
            }
            else if (biasDirection != Vector3.zero)
            {
                isFlying = true;
                if (biasDirection.y > 0.6)
                    isFlyingDirectionForwards = false;
            }
            DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + " Has bias of" + biasDirection + " and a boost bias of" + boostBiasDirection);

            int FoilCount = 0;
            int MovingFoilCount = 0;
            foreach (ModuleWing module in BM.IterateBlockComponents<ModuleWing>())
            {
                //Get teh slowest spooling one
                List<ModuleWing.Aerofoil> foils = module.m_Aerofoils.ToList();
                FoilCount += foils.Count();
                foreach (ModuleWing.Aerofoil Afoil in foils)
                {
                    if (Afoil.flapAngleRangeActual > 0 && Afoil.flapTurnSpeed > 0)
                        MovingFoilCount++;
                }
            }

            List<TankBlock> blocs = BM.IterateBlocks().ToList();
            int modBoostCount = 0;
            int modHoverCount = 0;
            int modGyroCount = 0;
            int modWheelCount = 0;
            int modAGCount = 0;
            int modGunCount = 0;
            int modDrillCount = 0;

            foreach (TankBlock bloc in blocs)
            {
                if (bloc.GetComponent<ModuleBooster>())
                    modBoostCount++;
                if (bloc.GetComponent<ModuleHover>())
                    modHoverCount++;
                if (bloc.GetComponent<ModuleGyro>())
                    modGyroCount++;
                if (bloc.GetComponent<ModuleWheels>())
                    modWheelCount++;
                if (bloc.GetComponent<ModuleAntiGravityEngine>())
                    modAGCount++;
                if (bloc.GetComponent<ModuleWeaponGun>())
                    modGunCount++;
                if (bloc.GetComponent<ModuleDrill>())
                    modDrillCount++;
            }
            //Debug.Info("TACtical_AI: Tech " + tank.name + "  Has block count " + blocs.Count() + "  | " + modBoostCount + " | " + modAGCount);


            if (tank.IsAnchored)
            {
                return AIDriverType.Tank;
            }
            else if ((modHoverCount > 3) || (modBoostCount > 2 && (modHoverCount > 2 || modAGCount > 0)))
            {
                return AIDriverType.Astronaut;
            }
            else if (MovingFoilCount > 4 && isFlying && isFlyingDirectionForwards)
            {
                return AIDriverType.Pilot;
            }
            else if (modGyroCount > 0 && isFlying && !isFlyingDirectionForwards)
            {
                return AIDriverType.Pilot;
            }
            else if (KickStart.isWaterModPresent && FoilCount > 0 && modGyroCount > 0 && modBoostCount > 0 && (modWheelCount < 4 || modHoverCount > 1))
            {
                return AIDriverType.Sailor;
            }
            else
                return AIDriverType.Tank;
        }

        public static void RequestFocusFireALL(Tank tank, Visible Target, RequestSeverity priority)
        {
            if (Target.IsNull() || tank.IsNull())
                return;
            if (Target.tank.IsNull())
                return;
            int Team = tank.Team;
            if (tank.IsAnchored)
                AIMessage(tank, "Player Base " + tank.name + " is under attack!  Concentrate all fire on " + Target.tank.name + "!");
            else
                AIMessage(tank, tank.name + ": Requesting assistance!  Cover me!");
            if (!TankAIManager.targetingRequests.ContainsKey(Team))
                TankAIManager.targetingRequests.Add(Team, new KeyValuePair<RequestSeverity, Visible>(priority, Target));
        }

        public class TankAIManager : MonoBehaviour
        {
            internal static FieldInfo rangeOverride = typeof(ManTechs).GetField("m_SleepRangeFromCamera", BindingFlags.NonPublic | BindingFlags.Instance);

            internal static TankAIManager inst;
            private static Tank lastPlayerTech;

            public static Dictionary<int, KeyValuePair<RequestSeverity, Visible>> targetingRequests = new Dictionary<int, KeyValuePair<RequestSeverity, Visible>>();

            //public static EventNoParams QueueUpdater = new EventNoParams();
            private static Dictionary<int, TeamIndex> teamsIndexed;
            public static Event<TankAIHelper> TechRemovedEvent = new Event<TankAIHelper>();
            private static float lastCombatTime = 0;
            internal static float terrainHeight = 0;
            private static float TargetTime = DefaultTime;
            internal static float LastRealTime = 0;
            internal static float DeltaRealTime = 0;

            private const float DefaultTime = 1.0f;
            private const float SlowedTime = 0.25f;
            private const float FastTime = 3f; // fooling around
            private const float ChangeRate = 1.5f;

            public void ManageTimeRunner()
            {
                if (Time.timeScale > 0)
                {
                    DeltaRealTime = Time.realtimeSinceStartup - LastRealTime;
                    if (ManPlayerRTS.PlayerIsInRTS && !ManNetwork.IsNetworked)
                    {
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            TargetTime = SlowedTime;
                        }
                        else if (Input.GetKey(KeyCode.RightControl))
                        {
                            TargetTime = FastTime;
                        }
                        else
                        {
                            TargetTime = DefaultTime;
                        }
                    }
                    else
                    {
                        TargetTime = DefaultTime;
                    }
                    Time.timeScale = Mathf.MoveTowards(Time.timeScale, TargetTime, ChangeRate * DeltaRealTime);
                }
                LastRealTime = Time.realtimeSinceStartup;
            }

            internal static void Initiate()
            {
                if (inst)
                    return;
                inst = new GameObject("AIManager").AddComponent<TankAIManager>();
                AIEPathMapper.inst = inst.gameObject.AddComponent<AIEPathMapper>();
                //Allies = new List<Tank>();
                Minables = new List<Visible>();
                Depots = new List<ModuleHarvestReciever>();
                BlockHandlers = new List<ModuleHarvestReciever>();
                Chargers = new List<ModuleChargerTracker>();
                RetreatingTeams = new HashSet<int>();
                teamsIndexed = new Dictionary<int, TeamIndex>();
                AllHelpers = new List<TankAIHelper>();
                Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Subscribe(OnTankAddition);
                Singleton.Manager<ManTechs>.inst.TankTeamChangedEvent.Subscribe(OnTankChange);
                Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Subscribe(OnPlayerTechChange);
                //QueueUpdater.Subscribe(FetchAllAllies);
                DebugTAC_AI.Log("TACtical_AI: Created AIECore Manager.");

                // Only change if no other mod changed
                DebugTAC_AI.Log("TACtical_AI: Current AI interaction range is " + (float)rangeOverride.GetValue(ManTechs.inst) + ".");
                if ((float)rangeOverride.GetValue(ManTechs.inst) == 200f)
                {   // more than twice the range
                    rangeOverride.SetValue(ManTechs.inst, AIGlobals.EnemyExtendActionRange);
                    DebugTAC_AI.Log("TACtical_AI: Extended enemy Tech interaction range to " + AIGlobals.EnemyExtendActionRange + ".");
                }
            }
#if STEAM
            internal void CheckNextFrameNeedsDeInit()
            {
                Invoke("DeInitCallToKickStart", 0.001f);
            }
            internal void DeInitCallToKickStart()
            {
                if (!KickStart.ShouldBeActive)
                    KickStart.DeInitALL();
            }
            internal static void DeInit()
            {
                if (!inst)
                    return;
                AIEPathMapper.ResetAll();
                Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Unsubscribe(OnTankAddition);
                Singleton.Manager<ManTechs>.inst.TankTeamChangedEvent.Unsubscribe(OnTankChange);
                Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Unsubscribe(OnPlayerTechChange);
                DestroyAllHelpers();
                AllHelpers = null;
                teamsIndexed = null;
                RetreatingTeams = null;
                Chargers = null;
                BlockHandlers = null;
                Depots = null;
                Minables = null;
                inst.enabled = false;
                Destroy(inst.gameObject);
                inst = null;
                DebugTAC_AI.Log("TACtical_AI: De-Init AIECore Manager.");

                // Only change if no other mod changed
                if ((float)rangeOverride.GetValue(ManTechs.inst) == AIGlobals.EnemyExtendActionRange)
                {   // more than twice the range
                    rangeOverride.SetValue(ManTechs.inst, 200);
                    DebugTAC_AI.Log("TACtical_AI: Un-Extended enemy Tech interaction range to default 200.");
                }
            }
#endif
            private static void DestroyAllHelpers()
            {
                foreach (var item in new List<TankAIHelper>(AllHelpers))
                {
                    Destroy(item);
                }
                AllHelpers.Clear();
            }


            private static void OnTankAddition(Tank tonk)
            {
                var helper = tonk.GetHelperInsured();
                //IndexTech(tonk, tonk.Team);

                if (tonk.GetComponents<TankAIHelper>().Count() > 1)
                    throw new InvalidOperationException("TACtical_AI: ASSERT: THERE IS MORE THAN ONE TankAIHelper ON " + tonk.name + "!!!");

                //DebugTAC_AI.Log("TACtical_AI: Allied AI " + tankInfo.name + ":  Called OnSpawn");
                //if (tankInfo.gameObject.GetComponent<TankAIHelper>().AIState != 0)
                //helper.ResetAll(tonk);
                //helper.OnTechTeamChange();

                //QueueUpdater.Send();
            }
            private static void OnTankChange(Tank tonk, ManTechs.TeamChangeInfo info)
            {
                if (tonk == null)
                {
                    DebugTAC_AI.Log("TACtical_AI: OnTankChange tonk is NULL");
                    return;
                }
                var helper = tonk.GetHelperInsured();
                //RemoveTech(tonk);
                //helper.ResetAll(tonk);
                helper.OnTechTeamChange();
                //IndexTech(tonk, info.m_NewTeam);
                DebugTAC_AI.Log("TACtical_AI: AI Helper " + tonk.name + ":  Called OnTankChange");
                //QueueUpdater.Send();
            }
            private static void OnTankRecycled(Tank tonk)
            {
                var helper = tonk.GetHelperInsured();
                TechRemovedEvent.Send(helper);
                helper.Recycled();
                RemoveTech(tonk);
                //DebugTAC_AI.Log("TACtical_AI: Allied AI " + tonk.name + ":  Called OnTankRecycled");

                helper.OverrideAllControls = false;

                var mind = tonk.GetComponent<EnemyMind>();
                if ((bool)mind)
                {
#if !STEAM
                    ALossReact loss = ALossReact.Land;
                    switch (mind.EvilCommander)
                    {
                        case EnemyHandling.Naval:
                            loss = ALossReact.Sea;
                            break;
                        case EnemyHandling.Stationary:
                            loss = ALossReact.Base;
                            break;
                        case EnemyHandling.Airplane:
                        case EnemyHandling.Chopper:
                            loss = ALossReact.Air;
                            break;
                        case EnemyHandling.Starship:
                            loss = ALossReact.Space;
                            break;
                    }
                    AnimeAICompat.RespondToLoss(tonk, loss);
#endif
                }
                //QueueUpdater.Send();
            }
            private static void OnPlayerTechChange(Tank tonk, bool yes)
            {
                TankAIHelper helper;
                if (lastPlayerTech != tonk)
                {
                    if (tonk != null)
                    {
                        helper = tonk.GetHelperInsured();
                        helper.OnTechTeamChange();
                    }
                    try
                    {
                        if (lastPlayerTech)
                        {
                            helper = tonk.GetHelperInsured();
                            helper.OnTechTeamChange();
                        }
                    }
                    catch { }
                    lastPlayerTech = tonk;
                }
            }

            /// <summary> DO NOT ALTER </summary>
            private static HashSet<Tank> emptyHash = new HashSet<Tank>();
            public static HashSet<Tank> GetTeamTanks(int Team)
            {
                if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                {
                    //RemoveAllInvalid(TIndex.Teammates);
                    return TIndex.Teammates;
                }
                return emptyHash;
            }
            public static HashSet<Tank> GetNonEnemyTanks(int Team)
            {
                if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                {
                    //RemoveAllInvalid(TIndex.NonHostile);
                    return TIndex.NonHostile;
                }
                return emptyHash;
            }
            public static HashSet<Tank> GetTargetTanks(int Team)
            {
                if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                {
                    //RemoveAllInvalid(TIndex.Targets);
                    return TIndex.Targets;
                }
                return emptyHash;
            }
            private static void RemoveAllInvalid(HashSet<Tank> list)
            {
                for (int step = list.Count - 1; step > -1; step--)
                {
                    var ele = list.ElementAt(step);
                    if (ele?.visible == null || !ele.visible.isActive)
                    {
                        DebugTAC_AI.Assert("TACtical AI: RemoveAllInvalid - Tech indexes were desynced - a Tech that was null or had no blocks was in the collection!");
                        list.Remove(ele);
                    }
                }
            }
            internal static void UpdateTechTeam(Tank tonk)
            {
                RemoveTech(tonk);
                IndexTech(tonk, tonk.Team);
            }
            private static void IndexTech(Tank tonk, int Team)
            {
                if (tonk?.visible == null || !tonk.visible.isActive)
                    return;
                tonk.TankRecycledEvent.Subscribe(OnTankRecycled);
                try
                {
                    if (!teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                    {
                        TeamIndex TI = new TeamIndex();
                        foreach (var item in ManTechs.inst.IterateTechs())
                        {
                            if (item == null || item == tonk)
                                continue;
                            if (item.IsEnemy(Team))
                                TI.Targets.Add(item);
                            else
                                TI.NonHostile.Add(item);
                        }
                        //RemoveAllInvalid(TI.Targets);
                        //RemoveAllInvalid(TI.NonHostile);
                        teamsIndexed.Add(Team, TI);
                    }
                    foreach (KeyValuePair<int, TeamIndex> TI in teamsIndexed)
                    {
                        if (Tank.IsEnemy(TI.Key, Team))
                        {
                            TI.Value.Targets.Add(tonk);
                            //RemoveAllInvalid(TI.Value.Targets);
                        }
                        else
                        {
                            if (TI.Key == Team)
                            {
                                TI.Value.Teammates.Add(tonk);
                                //RemoveAllInvalid(TI.Value.Teammates);
                            }
                            TI.Value.NonHostile.Add(tonk);
                            //RemoveAllInvalid(TI.Value.NonHostile);
                        }
                    }
                    //DebugTAC_AI.Log("IndexTech added " + tonk.name + " of team " + Team);
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("Error in IndexTech " + e);
                }
            }
            private static void RemoveTech(Tank tonk)
            {
                if (tonk != null)
                    tonk.TankRecycledEvent.Unsubscribe(OnTankRecycled);
                for (int step = teamsIndexed.Count - 1; 0 <= step; step--)
                {
                    KeyValuePair<int, TeamIndex> TI = teamsIndexed.ElementAt(step);
                    TI.Value.Teammates.Remove(tonk);
                    if (TI.Value.Teammates.Count == 0)
                        teamsIndexed.Remove(tonk.Team);
                    else
                    {
                        TI.Value.Targets.Remove(tonk);
                        TI.Value.NonHostile.Remove(tonk);
                    }
                }
                //DebugTAC_AI.Log("RemoveTech " + tonk.name);
            }


            /*
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
                            //DebugTAC_AI.Log("TACtical_AI: Added " + Allies.ElementAt(AllyCount));
                            AllyCount++;
                        }
                    }
                    //DebugTAC_AI.Log("TACtical_AI: Fetched allied tech list for AIs...");
                    if (AllyCount > 2)
                        moreThan2Allies = true;
                }
                catch  (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: FetchAllAllies - Error on fetchlist");
                    DebugTAC_AI.Log(e);
                }
                if (AllyCount > 2)
                    moreThan2Allies = true;
                else
                    moreThan2Allies = false;
            }*/

            public void WarnPlayers()
            {
                try
                {
                    SendChatServer("Warning: This server is using Advanced AI!  If you are new to the game, I would suggest you play safe. RTS Mode: " + KickStart.AllowStrategicAI + "");
                }
                catch { }
            }
            internal static string SendChatServer(string chatMsg)
            {
                try
                {
                    if (ManNetwork.IsHost)
                        Singleton.Manager<ManNetworkLobby>.inst.LobbySystem.CurrentLobby.SendChat("[SERVER] " + chatMsg, -1, (uint)TTNetworkID.Invalid.m_NetworkID);
                }
                catch { }
                return chatMsg;
            }
            public void CorrectBlocksList()
            {
                BlockIndexer.ConstructBlockLookupListDelayed();
            }

            // AI comms
            private static List<Tank> retreiveCache = new List<Tank>();
            public static List<Tank> TeamActiveMobileTechs(int Team)
            {
                retreiveCache.Clear();
                foreach (var item in ManTechs.inst.IterateTechs())
                {
                    if (item.Team == Team && !item.IsBase())
                        retreiveCache.Add(item);
                }
                return retreiveCache;
            }
            public static List<Tank> TeamActiveMobileTechsInCombat(int Team)
            {
                retreiveCache.Clear();
                foreach (var item in ManTechs.inst.IterateTechs())
                {
                    if (item.Team == Team && !item.IsBase())
                    {
                        var help = item.GetHelperInsured();
                        if (help && help.AttackEnemy && help.lastEnemyGet)
                        {
                            retreiveCache.Add(item);
                        }
                    }
                }
                return retreiveCache;
            }
            private void RunFocusFireRequests()
            {
                foreach (KeyValuePair<int, KeyValuePair<RequestSeverity, Visible>> request in targetingRequests)
                {
                    ProcessFocusFireRequestAllied(request.Key, request.Value.Value, request.Value.Key);
                }
                targetingRequests.Clear();
            }
            private static void ProcessFocusFireRequestAllied(int requestingTeam, Visible Target, RequestSeverity Priority)
            {
                try
                {
                    switch (Priority)
                    {
                        case RequestSeverity.ThinkMcFly:
                            foreach (Tank tech in GetTeamTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<TankAIHelper>();
                                if (!tech.IsAnchored && !helper.Retreat && helper.DediAI == AIType.Aegis)
                                {
                                    helper.Provoked = AIGlobals.ProvokeTimeShort;
                                    if (!(bool)helper.lastEnemyGet)
                                        helper.SetPursuit(Target);
                                }
                            }
                            break;
                        case RequestSeverity.Warn:
                            foreach (Tank tech in GetNonEnemyTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<TankAIHelper>();
                                if (!tech.IsAnchored && !helper.Retreat && (!ManSpawn.IsPlayerTeam(tech.Team) || helper.DediAI == AIType.Aegis))
                                {
                                    helper.Provoked = AIGlobals.ProvokeTime;
                                    if (!(bool)helper.lastEnemyGet)
                                        helper.SetPursuit(Target);
                                }
                            }
                            break;
                        case RequestSeverity.SameTeam:
                            foreach (Tank tech in GetTeamTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<TankAIHelper>();
                                helper.Provoked = AIGlobals.ProvokeTime;
                                if (!(bool)helper.lastEnemyGet)
                                    helper.SetPursuit(Target);
                            }
                            break;
                        case RequestSeverity.AllHandsOnDeck:
                            foreach (Tank tech in GetNonEnemyTanks(requestingTeam))
                            {
                                var helper = tech.GetComponent<TankAIHelper>();
                                if (!tech.IsAnchored || (ManSpawn.IsPlayerTeam(tech.Team) && (helper.DediAI == AIType.Aegis || helper.AdvancedAI)))
                                {
                                    helper.Provoked = AIGlobals.ProvokeTime;
                                    if (!(bool)helper.lastEnemyGet)
                                        helper.SetPursuit(Target);
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch { }
            }

            private void Update()
            {
                /*
                foreach (var item in ManTechs.inst.IterateTechs())
                {
                    if (item == null || Tank.IsEnemy(item.Team, item.Team))
                        continue;
                    if (teamsIndexed.TryGetValue(item.Team, out TeamIndex TIndex))
                    {
                        if (!TIndex.Teammates.Contains(item))
                        {
                            DebugTAC_AI.Log("Tech " + item.name + " is not registered in teamsIndexed!");
                            IndexTech(item, item.Team);
                        }
                    }
                    else
                    {
                        DebugTAC_AI.Log("Tech team " + item.Team + " for tech " + item.name + " is not registered in teamsIndexed!");
                        IndexTech(item, item.Team);
                    }
                    if (item.IsEnemy() && !GetTargetTanks(item.Team).Contains(Singleton.playerTank))
                        DebugTAC_AI.Log("Tech team " + item.Team + " has player as enemy but this is not in the quick lookup!");
                }*/
                if (!ManPauseGame.inst.IsPaused)
                {
                    if (!AIGlobals.IsAttract)
                    {
                        //DebugTAC_AI.Log("Resetting Camera...");
                        CameraManager.inst.GetCamera<TankCamera>().SetFollowTech(null);
                        CustomAttract.UseFollowCam = false;
                    }
                    if (Input.GetKeyDown(KeyCode.Quote))
                        debugVisuals = !debugVisuals;

                    if (Input.GetKeyDown(KickStart.RetreatHotkey) && ManHUD.inst.HighlightedOverlay == null)
                        ToggleTeamRetreat(Singleton.Manager<ManPlayer>.inst.PlayerTeam);
                    if (Singleton.playerTank)
                    {
                        var helper = Singleton.playerTank.GetComponent<TankAIHelper>();
                        if (Input.GetMouseButton(0) && Singleton.playerTank.control.FireControl && ManPointer.inst.targetVisible)
                        {
                            Visible couldBeObst = ManPointer.inst.targetVisible;
                            if (couldBeObst.GetComponent<ResourceDispenser>())
                            {
                                if ((couldBeObst.centrePosition - Singleton.playerTank.visible.centrePosition).sqrMagnitude <= 10000)
                                {
                                    if (!Singleton.playerTank.Vision.GetFirstVisibleTechIsEnemy(Singleton.playerTank.Team))
                                    {
                                        helper.Obst = couldBeObst.transform;
                                        helper.ActiveAimState = AIWeaponState.Obsticle;
                                        goto conclusion;
                                    }
                                }
                            }
                        }
                        if (helper.Obst != null)
                        {
                            helper.Obst = null;
                            helper.ActiveAimState = AIWeaponState.Normal;
                        }
                    conclusion:;
                    }
                    if (lastCombatTime > 6)
                    {
                        if (ManEncounterPlacement.IsOverlappingEncounter(Singleton.playerPos, 64, false))
                            _playerIsInNonCombatZone = true;
                        if (PlayerCombatLastState != _playerIsInNonCombatZone)
                        {
                            PlayerCombatLastState = _playerIsInNonCombatZone;
                        }
                        lastCombatTime = 0;
                        terrainHeight = ManWorld.inst.ProjectToGround(Singleton.playerPos).y;
                    }
                    else
                        lastCombatTime += Time.deltaTime;
                }
                ManageTimeRunner();
                RunFocusFireRequests();
            }

            // Why?  Because this distributes the processing load equally across frames!
            private const int UpdateFramesPerSecond = 40;

            private List<TankAIHelper> helpersActive = new List<TankAIHelper>();
            private int clockHelperStepDirectors = 0;
            private int clockHelperStepOperations = 0;

            private float DirectorUpdateClock = 0;
            private float OperationsUpdateClock = 500;
            private int DirectorsToUpdateThisFrame()
            {
                DirectorUpdateClock += (float)helpersActive.Count / KickStart.AIDodgeCheapness;
                int count = Mathf.FloorToInt(DirectorUpdateClock);
                DirectorUpdateClock -= count;
                return count;
            }
            private int OperationsToUpdateThisFrame()
            {
                OperationsUpdateClock += (float)helpersActive.Count / KickStart.AIClockPeriod;
                int count = Mathf.FloorToInt(OperationsUpdateClock);
                OperationsUpdateClock -= count;
                return count;
            }
            private void UpdateAllHelpers()
            {
                if (!KickStart.EnableBetterAI)
                    return;
                for (int step = 0; step < AllHelpers.Count; step++)
                {
                    var helper = AllHelpers[step];
                    if (helper != null && helper.isActiveAndEnabled)
                        helpersActive.Add(helper);
                }
                foreach (var item in helpersActive)
                {
                    item.OnPreUpdate();
                }
                StaggerUpdateAllHelpersDirAndOps();
                foreach (var item in helpersActive)
                {
                    item.OnPostUpdate();
                }
                helpersActive.Clear();
            }
            private void StaggerUpdateAllHelpersDirAndOps()
            {
                int numDirUpdate = Mathf.Min(helpersActive.Count, DirectorsToUpdateThisFrame());
                int numOpUpdate = Mathf.Min(helpersActive.Count, OperationsToUpdateThisFrame());
                if (ManNetwork.IsHost)
                {
                    while (numDirUpdate > 0)
                    {
                        if (clockHelperStepDirectors >= helpersActive.Count)
                            clockHelperStepDirectors = 0;
                        helpersActive[clockHelperStepDirectors].OnUpdateHostAIDirectors();
                        clockHelperStepDirectors++;
                        numDirUpdate--;
                    }
                    while (numOpUpdate > 0)
                    {
                        if (clockHelperStepOperations >= helpersActive.Count)
                            clockHelperStepOperations = 0;
                        helpersActive[clockHelperStepOperations].OnUpdateHostAIOperations();
                        clockHelperStepOperations++;
                        numOpUpdate--;
                    }
                }
                else
                {
                    while (numDirUpdate > 0)
                    {
                        if (clockHelperStepDirectors >= helpersActive.Count)
                            clockHelperStepDirectors = 0;
                        helpersActive[clockHelperStepDirectors].OnUpdateClientAIDirectors();
                        clockHelperStepDirectors++;
                        numDirUpdate--;
                    }
                    while (numOpUpdate > 0)
                    {
                        if (clockHelperStepOperations >= helpersActive.Count)
                            clockHelperStepOperations = 0;
                        helpersActive[clockHelperStepOperations].OnUpdateClientAIOperations();
                        clockHelperStepOperations++;
                        numOpUpdate--;
                    }
                }
            }
            private void FixedUpdate()
            {
                if (!ManPauseGame.inst.IsPaused && KickStart.EnableBetterAI)
                {
                    UpdateAllHelpers();
                }
            }


            internal class GUIManaged
            {
                private static bool controlledDisp = false;
                private static bool typesDisp = false;
                private static HashSet<AIType> enabledTabs = null;
                public static void GUIGetTotalManaged()
                {
                    if (enabledTabs == null)
                    {
                        enabledTabs = new HashSet<AIType>();
                    }
                    GUILayout.Box("--- Helpers --- ");
                    int activeCount = 0;
                    int baseCount = 0;
                    Dictionary<AIAlignment, int> alignments = new Dictionary<AIAlignment, int>();
                    foreach (AIAlignment item in Enum.GetValues(typeof(AIAlignment)))
                    {
                        alignments.Add(item, 0);
                    }
                    Dictionary<AIType, int> types = new Dictionary<AIType, int>();
                    foreach (AIType item in Enum.GetValues(typeof(AIType)))
                    {
                        types.Add(item, 0);
                    }
                    for (int step = 0; step < AllHelpers.Count; step++)
                    {
                        var helper = AllHelpers[step];
                        if (helper != null && helper.isActiveAndEnabled)
                        {
                            activeCount++;
                            alignments[helper.AIAlign]++;
                            types[helper.DediAI]++;
                            if (helper.tank.IsAnchored)
                                baseCount++;
                        }
                    }
                    GUILayout.Label("  Capacity: " + KickStart.MaxEnemyWorldCapacity);
                    GUILayout.Label("  Num Bases: " + baseCount);
                    if (GUILayout.Button("Total: " + AllHelpers.Count + " | Active: " + activeCount)) 
                        controlledDisp = !controlledDisp;
                    if (controlledDisp)
                    {
                        foreach (var item in alignments)
                        {
                            GUILayout.Label("  Alignment: " + item.Key.ToString() + " - " + item.Value);
                        }
                    }
                    if (GUILayout.Button("Types: " + types.Count))
                        typesDisp = !typesDisp;
                    if (typesDisp)
                    {
                        foreach (var item in types)
                        {
                            if (GUILayout.Button("Type: " + item.Key.ToString() + " - " + item.Value))
                            {
                                if (enabledTabs.Contains(item.Key))
                                    enabledTabs.Remove(item.Key);
                                else
                                    enabledTabs.Add(item.Key);
                            }
                            if (enabledTabs.Contains(item.Key))
                            {
                                foreach (var item2 in AllHelpers.FindAll(x => x != null &&
                                x.isActiveAndEnabled && x.DediAI == item.Key))
                                {
                                    Vector3 pos = item2.tank.boundsCentreWorldNoCheck;
                                    GUILayout.Label("  Tech: " + item2.tank.name + " | Pos: " + pos);
                                    DebugRawTechSpawner.DrawDirIndicator(pos, pos + new Vector3(0, 32, 0), Color.white);
                                }
                            }
                        }
                    }
                }
            }
        }
        public class TeamIndex
        {   // 
            public HashSet<Tank> Teammates = new HashSet<Tank>();
            public HashSet<Tank> NonHostile = new HashSet<Tank>();
            public HashSet<Tank> Targets = new HashSet<Tank>();
        }

        /// <summary>
        /// This AI either runs normally in Singleplayer, or on the Server in Multiplayer
        /// </summary>
        public class TankAIHelper : MonoBehaviour, IWorldTreadmill
        {
            public Tank tank;
            public AITreeType.AITypes lastAIType;
            //Tweaks (controlled by Module)
            /// <summary>
            /// The type of vehicle the AI controls
            /// </summary>
            public AIDriverType DriverType = AIDriverType.AutoSet;
            /// <summary>
            /// The task the AI will perform
            /// </summary>
            public AIType DediAI = AIType.Escort;
            public EAttackMode AttackMode = EAttackMode.Circle; // How to attack the enemy
            private AlliedOperationsController _OpsController;
            public AlliedOperationsController OpsController
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
            public bool IsPlayerControlled => AIAlign == AIAlignment.PlayerNoAI || AIAlign == AIAlignment.Player;
            public bool ActuallyWorks => hasAI || tank.PlayerFocused;

            public bool SetToActive => lastAIType == AITreeType.AITypes.Escort || lastAIType == AITreeType.AITypes.Guard;


            public bool Allied => AIAlign == AIAlignment.Player;
            public bool NotInBeam => BeamTimeoutClock == 0;
            public bool CanCopyControls => !IsMultiTech || tank.PlayerFocused;
            public bool CanUseBuildBeam => !(tank.IsAnchored && !PlayerAllowAutoAnchoring);
            public bool CanAutoAnchor => AutoAnchor && PlayerAllowAutoAnchoring && !AttackEnemy && tank.Anchors.NumPossibleAnchors >= 1 && DelayedAnchorClock >= 15 && CanAnchorSafely;
            public bool CanAnchorSafely => !lastEnemyGet || (lastEnemyGet && lastCombatRange > AIGlobals.SafeAnchorDist);
            public bool MovingAndOrHasTarget => tank.IsAnchored ? lastEnemyGet : DriverType == AIDriverType.Pilot || (DriveDirDirected > EDriveFacing.Neutral && (ForceSetDrive || DoSteerCore));
            public bool UsingPathfinding => ControlCore.DrivePathing >= EDrivePathing.Path;

            // Constants
            internal float DodgeStrength
            {
                get
                {
                    if (UsingAirControls)
                        return AIGlobals.AirborneDodgeStrengthMultiplier * lastOperatorRange;
                    return AIGlobals.DefaultDodgeStrengthMultiplier * lastOperatorRange;
                }
            }



            // Settables in ModuleAIExtension - "turns on" functionality on the host Tech, none of these force it off
            public bool IsMultiTech = false;    // Should the other AIs ignore collision with this Tech?
            public bool ChaseThreat = true;    // Should the AI chase the enemy?
            public bool RequestBuildBeam = true;// Should the AI Auto-BuildBeam on flip?

            // Player Toggleable
            public bool AdvancedAI
            // Should the AI take combat calculations and retreat if nesseary?
            {
                get => Allied ? (AISetSettings.AdvancedAI && AILimitSettings.AdvancedAI) : AILimitSettings.AdvancedAI;
                set => AILimitSettings.AdvancedAI = value;
            }
            public bool AllMT
            // Should the AI only follow player movement while in MT mode?
            {
                get => Allied ? (AISetSettings.AllMT && AILimitSettings.AllMT) : AILimitSettings.AllMT;
                set => AILimitSettings.AllMT = value;
            }
            public bool FullMelee
            // Should the AI ram the enemy?
            {
                get => Allied ? (AISetSettings.FullMelee && AILimitSettings.FullMelee) : AILimitSettings.FullMelee;
                set => AILimitSettings.FullMelee = value;
            }

            public bool SideToThreat
            // Should the AI circle the enemy?
            {
                get => Allied ? (AISetSettings.SideToThreat && AILimitSettings.SideToThreat) : AILimitSettings.SideToThreat;
                set => AILimitSettings.SideToThreat = value;
            }

            // Repair Auxilliaries
            public bool AutoRepair      // Allied auto-repair
            {
                get => Allied ? (AISetSettings.AutoRepair && AILimitSettings.AutoRepair) : AILimitSettings.AutoRepair;
                set => AILimitSettings.AutoRepair = value;
            }
            public bool UseInventory    // Draw from player inventory reserves
            {
                get => Allied ? (AISetSettings.UseInventory && AILimitSettings.UseInventory) : AILimitSettings.UseInventory;
                set => AILimitSettings.UseInventory = value;
            }


            // Additional
            public bool AutoAnchor = false;      // Should the AI toggle the anchor when it is still?
            public bool SecondAvoidence = false;// Should the AI avoid two techs at once?

            // Distance operations - Automatically accounts for tech sizes
            public AISettings AISetSettings = AISettings.DefaultSettable;
            public AISettings AILimitSettings = AISettings.DefaultLimit;
            /// <summary> The range the AI will linger from the enemy while attacking if PursueThreat is true </summary>
            public float MinCombatRange => Allied ? Mathf.Min(AISetSettings.CombatRange, AILimitSettings.CombatRange) : AILimitSettings.CombatRange;
            /// <summary>  How far should we pursue the enemy? </summary>
            public float MaxCombatRange => Allied ? Mathf.Min(AISetSettings.ChaseRange, AILimitSettings.ChaseRange) : AILimitSettings.ChaseRange;
            /// <summary> The range the AI will linger from the target objective in general </summary>
            public float MaxObjectiveRange => Allied ? Mathf.Min(AISetSettings.ObjectiveRange, AILimitSettings.ObjectiveRange) : AILimitSettings.ObjectiveRange;
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
                                                //internal float Oops = 0;

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
            private float lastLockOnDistance = 0;
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
            internal Visible lastEnemy { get; set; }  = null;
            public Visible lastLockedTarget;
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

            //Timestep
            internal short DelayedAnchorClock = 0;
            internal short LightBoostFeatheringClock = 50;
            internal float RepairStepperClock = 0;
            internal short BeamTimeoutClock = 0;
            internal int WeaponDelayClock = 0;
            internal int ActionPause = 0;               // when [val > 0], used to halt other actions 
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
            internal bool IsDirectedMovingAnyDest => ControlOperator.DriveDest != EDriveDest.None;
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
            /// <summary> Leave at 0 to disable automatic spacing</summary>
            internal float MinimumRad = 0;              // Minimum radial spacing distance from destination
            internal float DriveVar = 0;                // Forwards drive (-1, 1)

            internal bool Yield = false;                // Slow down and moderate top speed
            internal bool PivotOnly = false;            // Only aim at target

            /// <summary>
            /// SHOULD WE FIRE GUNS
            /// </summary>
            internal bool AttackEnemy = false;          // Enemy nearby?
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


            internal bool FIRE_NOW = false;             // hold down tech's spacebar
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
            public bool RTSControlled {
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
            public static IntVector3 RTSDisabled = IntVector3.invalid;
            internal IntVector3 RTSDestination {
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
            internal Event<TankAIHelper> FinishedRepairEvent = new Event<TankAIHelper>();


            // AI Core
            public IMovementAIController MovementController;
            public AIEAutoPather autoPather => (MovementController is AIControllerDefault def) ? def.Pathfinder : null;

            // Troubleshooting
            //internal bool RequirementsFailiure = false;



            //-----------------------------
            //         SUBCRIPTIONS
            //-----------------------------
            public TankAIHelper Subscribe()
            {
                if (tank != null)
                {
                    DebugTAC_AI.Assert("Game attempted to fire Subscribe for TankAIHelper twice.");
                    return this;
                }
                tank = GetComponent<Tank>();
                Vector3 _ = tank.boundsCentreWorld;
                AIList = new List<ModuleAIExtension>();
                ManWorldTreadmill.inst.AddListener(this);
                tank.DamageEvent.Subscribe(OnHit);
                if (DriverType == AIDriverType.AutoSet)
                    DriverType = HandlingDetermine(tank, this);
                SetupDefaultMovementAIController();
                AllHelpers.Add(this);
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
                    cachedBlockCount = tank.blockman.blockCount;
                }
                catch (Exception e)
                {
                    Debug.Log("DelayedSubscribe - Error " + e);
                }
            }

            public void OnAttach(TankBlock newBlock, Tank tank)
            {
                //DebugTAC_AI.Log("TACtical_AI: On Attach " + tank.name);
                TankAIHelper thisInst = tank.GetComponent<TankAIHelper>();
                thisInst.EstTopSped = 1;
                //thisInst.LastBuildClock = 0;
                thisInst.PendingHeightCheck = true;
                thisInst.dirty = true;
                dirtyAI = true;
                if (thisInst.AIAlign == AIAlignment.Player)
                {
                    try
                    {
                        if (!tank.FirstUpdateAfterSpawn && !thisInst.PendingDamageCheck && thisInst.TechMemor)
                        {
                            //DebugTAC_AI.Log("TACtical_AI: Saved TechMemor for " + tank.name);
                            thisInst.TechMemor.SaveTech();
                        }
                    }
                    catch { }
                }
                else if (thisInst.AIAlign == AIAlignment.NonPlayer)
                {
                    if (newBlock.GetComponent<ModulePacemaker>())
                        tank.Holders.SetHeartbeatSpeed(TechHolders.HeartbeatSpeed.Fast);
                }
            }
            public void OnDetach(TankBlock removedBlock, Tank tank)
            {
                TankAIHelper thisInst = tank.GetComponent<TankAIHelper>();
                thisInst.EstTopSped = 1;
                thisInst.recentSpeed = 1;
                thisInst.PendingHeightCheck = true;
                thisInst.PendingDamageCheck = true;
                thisInst.dirty = true;
                if (thisInst.AIAlign == AIAlignment.Player)
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
                cachedBlockCount = 0;
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


            /*
            public void OnRecycle(Tank tank)
            {
                //DebugTAC_AI.Log("TACtical_AI: Allied AI " + tank.name + ":  Called OnRecycle");
                tank.gameObject.GetComponent<TankAIHelper>().DediAI = AIType.Escort;
                ResetAll(tank);
            }
            */
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
                ControlOperator.lastDestination += move;

                if (MovementController != null)
                    MovementController.OnMoveWorldOrigin(move);
            }


            public void ResetAll(Tank unused)
            {
                DebugTAC_AI.Assert(MovementController == null, "MovementController is null.  How is this possible?!");
                //DebugTAC_AI.Log("TACtical_AI: Resetting all for " + tank.name);
                cachedBlockCount = tank.blockman.blockCount;
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
                lastLockedTarget = null;
                lastCloseAlly = null;
                theBase = null;
                IsTryingToUnjam = false;
                JustUnanchored = false;
                OverrideAllControls = false;
                DropBlock();
                isRTSControlled = false;
                RTSDestination = RTSDisabled;
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
                    DriverType = HandlingDetermine(tank, this);
                RecalibrateMovementAIController();

                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
                control3D.m_State.m_Beam = false;
                control3D.m_State.m_BoostJets = false;
                control3D.m_State.m_BoostProps = false;
                control3D.m_State.m_Fire = false;
                control3D.m_State.m_InputMovement = Vector3.zero;
                control3D.m_State.m_InputRotation = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);

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

            public void ReValidateAI()
            {
                AILimitSettings.CombatRange = 25;
                AutoAnchor = false;
                AILimitSettings.FullMelee = false; // Should the AI ram the enemy?
                AILimitSettings.AdvancedAI = false;// Should the AI take combat calculations and retreat if nesseary?
                AILimitSettings.AllMT = false;
                AILimitSettings.SideToThreat = false;
                SecondAvoidence = false;// Should the AI avoid two techs at once?
                UseInventory = false;
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
                List<ModuleAIBot> AIs = tank.blockman.IterateBlockComponents<ModuleAIBot>().ToList();
                foreach (ModuleAIBot bot in AIs)
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
                    if (AIEx.AdvancedAI)
                        AILimitSettings.AdvancedAI = true;
                    if (AIEx.AutoAnchor)
                        AutoAnchor = true;
                    if (AIEx.MeleePreferred)
                        AILimitSettings.FullMelee = true;
                    if (AIEx.AdvAvoidence)
                        SecondAvoidence = true;
                    if (AIEx.MTForAll)
                        AILimitSettings.AllMT = true;
                    if (AIEx.SidePreferred)
                        AILimitSettings.SideToThreat = true;
                    if (AIEx.SelfRepairAI)
                        AutoRepair = true;
                    if (AIEx.InventoryUser)
                        UseInventory = true;

                    // Engadgement Ranges
                    if (AIEx.MinCombatRange > MinCombatRange)
                        AILimitSettings.CombatRange = AIEx.MinCombatRange;
                    if (AIEx.MaxCombatRange > MaxCombatRange)
                        AILimitSettings.ChaseRange = AIEx.MaxCombatRange;

                    if (AIEx.RTSActive)
                    {
                        SetRTSState(true);
                        RTSDestination = AIEx.GetRTSScenePos();
                    }
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
                else if (ShouldBeStationary(tank, this))
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

                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
                control3D.m_State.m_Beam = false;
                control3D.m_State.m_BoostJets = false;
                control3D.m_State.m_BoostProps = false;
                control3D.m_State.m_Fire = false;
                control3D.m_State.m_InputMovement = Vector3.zero;
                control3D.m_State.m_InputRotation = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);

                bool check = AIEBases.CheckIfTechNeedsToBeBuilt(this);
                if (AutoRepair || check)
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
                    tank.AttachEvent.Unsubscribe(OnAttach);
                    tank.DetachEvent.Unsubscribe(OnDetach);
                }
                catch { }

                try
                {
                    tank.AttachEvent.Subscribe(OnAttach);
                    tank.DetachEvent.Subscribe(OnDetach);
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

            public void ExecuteAutoSet()
            {
                ExecuteAutoSetNoCalibrate();
                RecalibrateMovementAIController();
            }
            public void ExecuteAutoSetNoCalibrate()
            {
                DriverType = HandlingDetermine(tank, this);
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
                            RequestFocusFireALL(tank, lastEnemyGet, RequestSeverity.AllHandsOnDeck);
                        }
                        else
                        {
                            // Execute remote orders to allied units - Attack that threat!
                            switch (DediAI)
                            {
                                case AIType.Prospector:
                                case AIType.Scrapper:
                                case AIType.Energizer:
                                    RequestFocusFireALL(tank, lastEnemyGet, RequestSeverity.Warn);
                                    break;
                                default:
                                    RequestFocusFireALL(tank, lastEnemyGet, RequestSeverity.ThinkMcFly);
                                    break;
                            }
                        }
                    }
                    Provoked = AIGlobals.ProvokeTime;
                    FIRE_NOW = true;
                    if (ManPlayerRTS.PlayerIsInRTS)
                    {
                        if (tank.IsAnchored)
                        {
                            PlayerRTSUI.RTSDamageWarnings(0.5f, 0.25f);
                            ManEnemySiege.BigF5broningWarning("Base is Under Attack");
                        }
                        else if (tank.PlayerFocused)
                        {
                            PlayerRTSUI.RTSDamageWarnings(1.5f, 0.75f);
                            ManEnemySiege.BigF5broningWarning("You are under attack");
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


            // Collision Avoidence
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
                    //AIECore.TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }



            // Obstruction Management
            public bool AutoHandleObstruction(ref EControlOperatorSet direct, float dist = 0, bool useRush = false, bool useGun = true, float div = 4)
            {
                if (!IsTechMoving(EstTopSped / div))
                {
                    TryHandleObstruction(!Feedback, dist, useRush, useGun, ref direct);
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
                        AIMessage(tech: tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
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
                    {
                        //Shoot the freaking tree
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
                        AIMessage(tech: tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
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
                FIRE_NOW = true;
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

            // Target Management
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
                            ControlOperator.lastDestination = target.tank.boundsCentreWorldNoCheck;
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

            public Visible GetEnemyAllied()
            {
                Visible target = lastEnemyGet;
                if (Provoked == 0)
                    target = null;
                else
                {
                    float TargetRangeSqr = MaxCombatRange * MaxCombatRange;
                    Vector3 scanCenter = tank.boundsCentreWorldNoCheck;
                    if (!target.isActive || !target.tank.IsEnemy(tank.Team) || (target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRangeSqr)
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
                    if (playerTarget?.tank != null && playerTarget.isActive && playerTarget.tank.CentralBlock)
                    {
                        // If the player fires while locked-on to a neutral/SubNeutral, the AI will assume this
                        //   is an attack request
                        Provoked = 0;
                        EndPursuit();
                        target = playerTarget;
                    }
                }
                if (target == null)
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


            //-----------------------------
            //           CHECKS
            //-----------------------------
            private void DetermineCombat()
            {
                bool DoNotEngage = false;
                if (lastEnemyGet?.tank)
                    if (!tank.IsEnemy(lastEnemyGet.tank.Team))
                        lastEnemy = null;
                if (RetreatingTeams.Contains(tank.Team))
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
                Retreat = RetreatingTeams.Contains(tank.Team);

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


            public string GetActionStatus(out bool cantDo)
            {
                cantDo = false;
                if (tank.IsPlayer)
                {
                    if (!RTSControlled)
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
                        if ((bool)AttackEnemy)
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
                                List<ChunkTypes> CT = theResource.resdisp.AllDispensableItems().ToList();
                                if (recentSpeed > 8)
                                {
                                    if (CT.Count == 0)
                                        output = "Going to remove rocks";
                                    else
                                        output = "Going to dig " + StringLookup.GetItemName(theResource.m_ItemType); //theResource.name;
                                    //StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                                }
                                else
                                {
                                    if (CT.Count == 0)
                                        output = "Clearing rocks";
                                    else
                                        output = "Mining " + StringLookup.GetItemName(theResource.m_ItemType);//theResource.name;
                                    //output = "Mining " + StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                                }
                            }
                            else
                                output = "No resources in " + (JobSearchRange + AIGlobals.FindItemScanRangeExtension) + " meters";
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
                                    List<ChunkTypes> CT = theResource.resdisp.AllDispensableItems().ToList();
                                    if (recentSpeed > 8)
                                    {
                                        if (CT.Count == 0)
                                            output = "Going to remove rocks";
                                        else
                                            output = "Going to dig " + StringLookup.GetItemName(theResource.m_ItemType);//theResource.name;
                                        //StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                                    }
                                    else
                                    {
                                        if (CT.Count == 0)
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



            private int cachedBlockCount = 1;
            public bool CanDetectHealth()
            {
                return true;//TechMemor || AdvancedAI; 
            }
            public float GetHealth()
            {
                return GetHealthPercent() * (cachedBlockCount * 10);
            }
            public float GetHealthMax()
            {
                return cachedBlockCount * 10;
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
                var energy = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                return energy.storageTotal > 1;
            }
            public float GetEnergy()
            {
                var energy = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                if (energy.storageTotal < 1)
                    return 0;

                return energy.storageTotal - energy.spareCapacity;
            }
            public float GetEnergyMax()
            {
                var energy = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                if (energy.storageTotal < 1)
                    return 1;

                return energy.storageTotal;
            }
            public float GetEnergyPercent()
            {
                var energy = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
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
                    return tank.rootBlockTrans.InverseTransformDirection(SafeVelocity).z > minSpeed || Mathf.Abs(tank.control.DriveControl) < 0.5f;
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


            //-----------------------------
            //           ACTIONS
            //-----------------------------


            public void ManageAILockOn()
            {
                switch (ActiveAimState)
                {
                    case AIWeaponState.Enemy:
                        if (lastEnemyGet.IsNotNull())
                        {   // Allow the enemy AI to finely select targets
                            //DebugTAC_AI.Log("TACtical_AI: Overriding targeting to aim at " + lastEnemy.name + "  pos " + lastEnemy.tank.boundsCentreWorldNoCheck);
                            lastLockedTarget = lastEnemyGet;
                        }
                        break;
                    case AIWeaponState.Obsticle:
                        if (Obst.IsNotNull())
                        {
                            var resTarget = Obst.GetComponent<Visible>();
                            if (resTarget)
                            {
                                //DebugTAC_AI.Log("TACtical_AI: Overriding targeting to aim at obstruction");
                                lastLockedTarget = resTarget;
                            }
                        }
                        break;
                    case AIWeaponState.Mimic:
                        if (lastCloseAlly.IsNotNull())
                        {
                            //DebugTAC_AI.Log("TACtical_AI: Overriding targeting to aim at player's target");
                            var helperAlly = lastCloseAlly.GetComponent<TankAIHelper>();
                            if (helperAlly.ActiveAimState == AIWeaponState.Enemy)
                                lastLockedTarget = helperAlly.lastEnemyGet;
                        }
                        break;
                }

                if (lastLockedTarget)
                {
                    bool playerAim = tank.PlayerFocused && !ManPlayerRTS.PlayerIsInRTS;
                    if (!lastLockedTarget.isActive || (playerAim && !tank.control.FireControl))
                    {   // Cannot do as camera breaks
                        lastLockedTarget = null;
                        return;
                    }
                    if (lastLockedTarget == tank.visible)
                    {
                        DebugTAC_AI.Assert("Tech " + tank.name + " tried to lock-on to itself!!!");
                        lastLockedTarget = null;
                        return;
                    }
                    if (!playerAim && lastLockedTarget.resdisp && ActiveAimState != AIWeaponState.Obsticle)
                    {
                        lastLockedTarget = null;
                        return;
                    }
                    float maxDist;
                    if (ManNetwork.IsNetworked)
                    {
                        maxDist = tank.Weapons.m_ManualTargetingSettingsMAndKB.m_ManualTargetingRadiusMP;
                    }
                    else
                    {
                        maxDist = tank.Weapons.m_ManualTargetingSettingsMAndKB.m_ManualTargetingRadiusSP;
                    }
                    if (lastLockOnDistance > maxDist * maxDist)
                    {
                        lastLockedTarget = null;
                    }
                }
            }


            // Allow allies to approach mobile base techs
            internal bool techIsApproaching = false;
            internal TankAIHelper ApproachingTech;
            public void AllowApproach(TankAIHelper Approaching)
            {
                if (AvoidStuff)
                {
                    AvoidStuff = false;
                    IsTryingToUnjam = false;
                    CancelInvoke();
                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Allowing approach");
                    Invoke("StopAllowApproach", 2);
                }
                if (!techIsApproaching)
                    ApproachingTech = Approaching;
                techIsApproaching = true;
            }
            private void StopAllowApproach()
            {
                if (!AvoidStuff)
                {
                    AvoidStuff = true;
                }

                techIsApproaching = false;
                ApproachingTech = null;
            }

            // Drop all items in collectors (Aircraft resource payload drop)

            private bool denyCollect = false;
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

            // Hold blocks for self-build-repair and scavenging operations
            public TankBlock HeldBlock => heldBlock;
            private TankBlock heldBlock;
            private Vector3 blockHoldPos = Vector3.zero;
            private Quaternion blockHoldRot = Quaternion.identity;
            private bool blockHoldOffset = false;
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
                else if(TB.block)
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


            // Handle Anchors
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

            private static MethodInfo MI = typeof(TechAnchors).GetMethod("ConfigureJoint", BindingFlags.NonPublic | BindingFlags.Instance);

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
                            DebugTAC_AI.Assert(true, (AIGlobals.IsAttract ? "(ATTRACT BASE)":"(FORCED)") + " screw you i'm anchoring anyways, I don't give a f*bron about your anchor checks!");
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

            // Handle Weapons
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


            //-----------------------------
            //      PRIMARY OPERATIONS
            //-----------------------------
            // Controls the Tech
            /// <summary>
            /// Main interface for ALL AI Tech Controls(excluding Neutral)
            /// </summary>
            /// <param name="thisControl"></param>
            public bool ControlTech(TankControl thisControl)
            {
                enabled = true;
                if (ManNetwork.IsNetworked)
                {
                    if (ManNetwork.IsHost)
                    {
                        bool IsPlayerRemoteControlled = false;
                        try
                        {
                            IsPlayerRemoteControlled = ManNetwork.inst.GetAllPlayerTechs().Contains(tank);
                        }
                        catch { }
                        if (IsPlayerRemoteControlled)
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



            // Lets the AI do the planning
            private static bool errored = false;
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
                            cachedBlockCount = tank.blockman.blockCount;
                    }
                }
                catch (Exception e)
                {
                    if (!errored)
                    {
                        DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN UpdateLastTechExtentsIfNeeded()!!! - " + e);
                        errored = true;
                    }
                }
            }

            public Vector3 DodgeSphereCenter { get; private set; } = Vector3.zero;
            /// <summary> World Rotation </summary>
            public Vector3 SafeVelocity { get; private set; } = Vector3.zero;
            public float DodgeSphereRadius { get; private set; } = 1;
            public void ShowDebugThisFrame()
            {
                if (DebugRawTechSpawner.ShowDebugFeedBack && debugVisuals)
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
                    if (!errored)
                    {
                        DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN UpdateHostAIActions!!! - " + e);
                        errored = true;
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
                    if (!errored)
                    {
                        DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN UpdateHostAIActions!!! - " + e);
                        errored = true;
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

                    lastLockedTarget = null;
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
                                if ((hasAI && !tank.PlayerFocused) || (World.ManPlayerRTS.PlayerIsInRTS && tank.PlayerFocused))
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
                        if (!errored)
                        {
                            DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN RebuildAlignment!!! - " + e);
                            errored = true;
                        }
                    }
                }
            }
            private void TryRepairStatic()
            {
                BookmarkBuilder builder = GetComponent<BookmarkBuilder>();
                if (builder)
                {
                    AILimitSettings.AutoRepair = true;
                    AILimitSettings.UseInventory = true;
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
                int blockC = tank.blockman.blockCount;
                if (cachedBlockCount > blockC)
                    DamageThreshold = (1f - (blockC / (float)cachedBlockCount)) * 100;
                else
                    cachedBlockCount = blockC;
            }
            private void TryRepairAllied()
            {
                BookmarkBuilder builder = GetComponent<BookmarkBuilder>();
                if (builder && TechMemor.IsNull())
                {
                    builder.HookUp(this);
                    DebugTAC_AI.Assert("TACtical_AI: Tech " + tank.name + "TryRepairAllied has a BookmarkBuilder but NO TechMemor!");
                }
                if (builder || (AutoRepair && (!tank.PlayerFocused || ManPlayerRTS.PlayerIsInRTS) && (KickStart.AllowAISelfRepair || tank.IsAnchored)))
                {
                    if (builder)
                    {
                        AISetSettings.AutoRepair = true;
                        AILimitSettings.AutoRepair = true;
                        AISetSettings.UseInventory = true;
                        AILimitSettings.UseInventory = true;
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
                int blockC = tank.blockman.blockCount;
                if (cachedBlockCount > blockC)
                    DamageThreshold = (1f - (blockC / (float)cachedBlockCount)) * 100;
                else
                    cachedBlockCount = blockC;
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


            // Weapons targeting
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
                    scanCenter = RoughPredictTarget(lastEnemyGet.tank);
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

            public Vector3 LeadTargetAiming(Visible targetTank)
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
        }
    }
}
