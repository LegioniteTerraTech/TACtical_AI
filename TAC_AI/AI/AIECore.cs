﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTech.Network;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.Templates;
using TAC_AI.World;
using RandomAdditions;

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

        //public static List<ResourceDispenser> Minables;
        public static List<Visible> Minables;
        public static List<ModuleHarvestReciever> Depots;
        public static List<ModuleHarvestReciever> BlockHandlers;
        public static List<ModuleChargerTracker> Chargers;
        public static List<int> RetreatingTeams;
        public static bool moreThan2Allies;
        public static bool PlayerIsInNonCombatZone = false;
        private static bool PlayerCombatLastState = false;
        //private static int lastTechCount = 0;

        // legdev
        internal static bool Feedback = false;// set this to true to get AI feedback testing


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
        public static bool FetchClosestResource(Vector3 tankPos, float MaxScanRange, out Visible theResource)
        {
            bool fired = false;
            theResource = null;
            float bestValue = MaxScanRange * MaxScanRange;// MAX SCAN RANGE
            int run = Minables.Count;
            for (int step = 0; step < run; step++)
            {
                var trans = Minables.ElementAt(step);
                if (trans.isActive)
                {
                    var res = trans.GetComponent<ResourceDispenser>();
                    if (!res.IsDeactivated && res.visible.isActive)
                    {
                        //Debug.Log("TACtical_AI:Skipped over inactive");
                        if (!trans.GetComponent<Damageable>().Invulnerable)
                        {
                            //Debug.Log("TACtical_AI: Skipped over invincible");
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
                List<Tank> AlliesAlt = AIEPathing.AllyList(helper.tank);
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
                //Debug.Log("TACtical_AI:ClosestAllyProcess " + closestTank.name);
            }
            catch //(Exception e)
            {
                //Debug.Log("TACtical_AI: Crash on ClosestAllyProcess " + e);
            }
            return fired;
        }

        // Assassin
        public static bool FindTarget(Tank tank, TankAIHelper helper, Visible targetIn, out Visible target)
        {   // Grants a much larger target search range

            float TargetRange = helper.RangeToChase * 2;
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

            foreach (var cTank in ManTechs.inst.IterateTechs())
            {
                if (cTank.IsEnemy(tank.Team) && cTank != tank)
                {
                    float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                    if (dist <= TargetRange)
                    {
                        TargetRange = dist;
                        target = cTank.visible;
                    }
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
            if (KickStart.isAnimeAIPresent)
            {   // we send the action commentary to Anime AI mod
#if !STEAM
                AnimeAICompat.TransmitStatus(tech, message);
#endif
            }
            if (!hasMessaged && Feedback)
            {
                hasMessaged = true;
                DebugTAC_AI.Log("TACtical_AI: AI " + message);
            }
            return hasMessaged;
        }
        public static void AIMessage(Tank tech, string message)
        {
#if !STEAM
            if (KickStart.isAnimeAIPresent)
            {   // we send the action commentary to Anime AI mod
                AnimeAICompat.TransmitStatus(tech, message);
            }
#endif
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
                            foreach (Tank tech in RBases.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupNeutralInfo("Fall back!", worPos);
                            }
                        }
                        else if (AIGlobals.IsFriendlyBaseTeam(Team))
                        {
                            foreach (Tank tech in RBases.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupAllyInfo("Fall back!", worPos);
                            }
                        }
                        else
                        {
                            foreach (Tank tech in RBases.TeamActiveMobileTechs(Team))
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
                            foreach (Tank tech in RBases.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupNeutralInfo("Engage!", worPos);
                            }
                        }
                        else if (AIGlobals.IsFriendlyBaseTeam(Team))
                        {
                            foreach (Tank tech in RBases.TeamActiveMobileTechs(Team))
                            {
                                WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                                AIGlobals.PopupAllyInfo("Engage!", worPos);
                            }
                        }
                        else
                        {
                            foreach (Tank tech in RBases.TeamActiveMobileTechs(Team))
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

        public static AIDriverType HandlingDetermine(Tank tank, TankAIHelper helper)
        {
            var BM = tank.blockman;

            if (tank.IsAnchored && !helper.PlayerAllowAutoAnchoring)
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


        public class TankAIManager : MonoBehaviour
        {
            internal static FieldInfo rangeOverride = typeof(ManTechs).GetField("m_SleepRangeFromCamera", BindingFlags.NonPublic | BindingFlags.Instance);

            internal static TankAIManager inst;
            private static Tank lastPlayerTech;

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
                //Allies = new List<Tank>();
                Minables = new List<Visible>();
                Depots = new List<ModuleHarvestReciever>();
                BlockHandlers = new List<ModuleHarvestReciever>();
                Chargers = new List<ModuleChargerTracker>();
                RetreatingTeams = new List<int>();
                teamsIndexed = new Dictionary<int, TeamIndex>();
                Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Subscribe(OnTankAddition);
                Singleton.Manager<ManTechs>.inst.TankTeamChangedEvent.Subscribe(OnTankChange);
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(OnTankRemoval);
                Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Subscribe(OnPlayerTechChange);
                //QueueUpdater.Subscribe(FetchAllAllies);
                DebugTAC_AI.Log("TACtical_AI: Created AIECore Manager.");

                // Only change if no other mod changed
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
                Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Unsubscribe(OnTankAddition);
                Singleton.Manager<ManTechs>.inst.TankTeamChangedEvent.Unsubscribe(OnTankChange);
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Unsubscribe(OnTankRemoval);
                Singleton.Manager<ManTechs>.inst.PlayerTankChangedEvent.Unsubscribe(OnPlayerTechChange);
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


            private static void OnTankAddition(Tank tonk)
            {
                IndexTech(tonk);
                var helper = InsureHelper(tonk);

                if (tonk.GetComponents<TankAIHelper>().Count() > 1)
                    DebugTAC_AI.Log("TACtical_AI: ASSERT: THERE IS MORE THAN ONE TankAIHelper ON " + tonk.name + "!!!");

                //Debug.Log("TACtical_AI: Allied AI " + tankInfo.name + ":  Called OnSpawn");
                //if (tankInfo.gameObject.GetComponent<TankAIHelper>().AIState != 0)
                helper.ResetAll(tonk);
                helper.OnTechTeamChange();

                //QueueUpdater.Send();
            }
            private static void OnTankChange(Tank tonk, ManTechs.TeamChangeInfo info)
            {
                RemoveTech(tonk);
                var helper = InsureHelper(tonk);
                helper.ResetAll(tonk);
                helper.OnTechTeamChange();
                IndexTech(tonk);
                //QueueUpdater.Send();
            }
            private static void OnTankRemoval(Tank tonk, ManDamage.DamageInfo info)
            {
                var helper = InsureHelper(tonk);
                TechRemovedEvent.Send(helper);
                helper.Recycled();
                RemoveTech(tonk);
                //Debug.Log("TACtical_AI: Allied AI " + tankInfo.name + ":  Called OnDeathOrRemoval");

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
                        helper = InsureHelper(tonk);
                        helper.OnTechTeamChange();
                    }
                    try
                    {
                        if (lastPlayerTech)
                        {
                            helper = InsureHelper(lastPlayerTech);
                            helper.OnTechTeamChange();
                        }
                    }
                    catch { }
                    lastPlayerTech = tonk;
                }
            }


            private static TankAIHelper InsureHelper(Tank tonk)
            {
                var helper = tonk.GetComponent<TankAIHelper>();
                if (!helper)
                {
                    helper = tonk.gameObject.AddComponent<TankAIHelper>().Subscribe();
                }
                return helper;
            }
            public static List<Tank> GetTeamTanks(int Team)
            {
                if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                {
                    return TIndex.Team;
                }
                return new List<Tank>();
            }
            public static List<Tank> GetNonEnemyTanks(int Team)
            {
                if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                {
                    return TIndex.NonHostile;
                }
                return new List<Tank>();
            }
            public static List<Tank> GetTargetTanks(int Team)
            {
                if (teamsIndexed.TryGetValue(Team, out TeamIndex TIndex))
                {
                    return TIndex.Targets;
                }
                return new List<Tank>();
            }
            private static void IndexTech(Tank tonk)
            {
                if (tonk == null)
                    return;
                if (teamsIndexed.TryGetValue(tonk.Team, out TeamIndex TIndex))
                {
                    if (!tonk.IsEnemy(tonk.Team))
                    {
                        TIndex.NonHostile.Add(tonk);
                        TIndex.NonHostile.RemoveAll(delegate (Tank cand) { return cand == null; });
                    }
                }
                else
                {
                    TeamIndex TI = new TeamIndex();
                    if (!tonk.IsEnemy(tonk.Team))
                    {
                        TI.NonHostile.Add(tonk);
                    }
                    teamsIndexed.Add(tonk.Team, TI);
                }
                foreach (KeyValuePair<int, TeamIndex> TI in teamsIndexed)
                {
                    if (tonk.IsEnemy(TI.Key))
                    {
                        TI.Value.Targets.Add(tonk);
                        TI.Value.Targets.RemoveAll(delegate (Tank cand) { return cand == null || cand.blockman.blockCount == 0; });
                    }
                    else
                    {
                        TI.Value.NonHostile.Add(tonk);
                        TI.Value.NonHostile.RemoveAll(delegate (Tank cand) { return cand == null || cand.blockman.blockCount == 0; });
                    }
                }
            }
            private static void RemoveTech(Tank tonk)
            {
                int count = teamsIndexed.Count;
                for (int step = 0; count > step;)
                {
                    KeyValuePair<int, TeamIndex> TI = teamsIndexed.ElementAt(step);
                    bool isEnemy = tonk.IsEnemy(TI.Key);
                    if (tonk.Team == TI.Key || !isEnemy)
                    {
                        TI.Value.NonHostile.Remove(tonk);
                        if (TI.Value.NonHostile.Count == 0)
                        {
                            teamsIndexed.Remove(tonk.Team);
                            count--;
                            continue;
                        }
                    }
                    else if (isEnemy)
                    {
                        TI.Value.Targets.Remove(tonk);
                    }
                    step++;
                }
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
                            //Debug.Log("TACtical_AI: Added " + Allies.ElementAt(AllyCount));
                            AllyCount++;
                        }
                    }
                    //Debug.Log("TACtical_AI: Fetched allied tech list for AIs...");
                    if (AllyCount > 2)
                        moreThan2Allies = true;
                }
                catch  (Exception e)
                {
                    Debug.Log("TACtical_AI: FetchAllAllies - Error on fetchlist");
                    Debug.Log(e);
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
                Invoke("ConstructErrorBlocksListDelayed", 0.01f);
            }
            public void ConstructErrorBlocksListDelayed()
            {
                AIERepair.ConstructErrorBlocksList();
            }

            private void Update()
            {
                if (!ManPauseGame.inst.IsPaused)
                {
                    if (!AIGlobals.IsAttract)
                    {
                        //DebugTAC_AI.Log("Resetting Camera...");
                        CameraManager.inst.GetCamera<TankCamera>().SetFollowTech(null);
                        CustomAttract.UseFollowCam = false;
                    }

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
                            PlayerIsInNonCombatZone = true;
                        if (PlayerCombatLastState != PlayerIsInNonCombatZone)
                        {
                            PlayerCombatLastState = PlayerIsInNonCombatZone;
                        }
                        lastCombatTime = 0;
                        terrainHeight = ManWorld.inst.ProjectToGround(Singleton.playerPos).y;
                    }
                    else
                        lastCombatTime += Time.deltaTime;
                }
                ManageTimeRunner();
            }
        }
        public class TeamIndex
        {   // 
            public List<Tank> Team = new List<Tank>();
            public List<Tank> NonHostile = new List<Tank>();
            public List<Tank> Targets = new List<Tank>();
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
            public AIERepair.DesignMemory TechMemor;

            // Checking Booleans
            public bool ActuallyWorks => hasAI || tank.PlayerFocused;

            public bool SetToActive => lastAIType == AITreeType.AITypes.Escort || lastAIType == AITreeType.AITypes.Guard;


            public bool NotInBeam => BeamTimeoutClock == 0;
            public bool CanCopyControls => !IsMultiTech;
            public bool CanUseBuildBeam => !(tank.IsAnchored && !PlayerAllowAutoAnchoring);
            public bool CanAutoAnchor => AutoAnchor && PlayerAllowAutoAnchoring && !AttackEnemy && tank.Anchors.NumPossibleAnchors >= 1 && DelayedAnchorClock >= 15 && CanAnchorSafely;
            public bool CanAnchorSafely => !lastEnemy || (lastEnemy && lastRangeEnemy > AIGlobals.SafeAnchorDist);
            public bool MovingAndOrHasTarget => tank.IsAnchored ? lastEnemy : DriverType == AIDriverType.Pilot || (DriveDir != EDriveFacing.Neutral && (ForceSetDrive || Steer));

            // Constants
            internal float DodgeStrength 
            { 
                get 
                { 
                    if (UsingAirControls)
                        return AIGlobals.AirborneDodgeStrengthMultiplier * lastRange;
                    return AIGlobals.DefaultDodgeStrengthMultiplier * lastRange; 
                } 
            }



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

            // Distance operations - Automatically accounts for tech sizes
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

            /// <summary>
            /// 0 is off, 1 is enemy, 2 is obsticle
            /// </summary>
            public AIWeaponState ActiveAimState = AIWeaponState.Normal;

            public AIAlignment AIState = AIAlignment.Static;             // 0 is static, 1 is ally, 2 is enemy
            public AIWeaponState WeaponState = AIWeaponState.Normal;    // 0 is sleep, 1 is target, 2 is obsticle, 3 is mimic
            public bool UpdatePathfinding = false;       // Collision avoidence active this FixedUpdate frame?
            public bool UsingAirControls = false; // Use the not-VehicleAICore cores
            internal int FrustrationMeter = 0;  // tardiness buildup before we use our guns to remove obsticles
            internal float Urgency = 0;         // tardiness buildup before we just ignore obstructions
            internal float UrgencyOverload = 0; // builds up too much if our max speed was set too high

            /// <summary>
            /// Repairs requested?
            /// </summary>
            public bool PendingDamageCheck = false;    // Is this tech damaged?

            /*
            public bool PendingDamageCheck
            {
                get { return _PendingDamageCheck; }
                set
                {
                    Debug.Log("PendingDamageCheck set by: " + StackTraceUtility.ExtractStackTrace());
                    _PendingDamageCheck = value;
                }
            }*/

            public float DamageThreshold = 0;   // How much damage have we taken? (100 is total destruction)
            //internal float Oops = 0;

            // Directional Handling
            
            /// <summary>
            /// IN WORLD SPACE
            /// Handles all operator decisions
            /// </summary>
            internal Vector3 lastDestination = Vector3.zero;    // Where we drive to in the world

            /*
            internal Vector3 lastDestination {
                get { return lastDestinationBugTest; }
                set {
                    Debug.Log("lastDestination set by: " + StackTraceUtility.ExtractStackTrace());
                    lastDestinationBugTest = value; 
                }
            }
            internal Vector3 lastDestinationBugTest = Vector3.zero;    // Where we drive to in the world
            */
            internal float lastRange = 0;
            internal float lastRangeEnemy = 0;

            //AutoCollection
            internal float DetectionRange => tank.Vision.SearchRadius > 0 ? tank.Vision.SearchRadius : 100;
            internal bool hasAI = false;    // Has an active AI module
            internal bool dirtyAI = true;  // Update Player AI state if needed
            internal bool dirty = true;    // The Tech has new blocks attached recently

            internal float EstTopSped = 0;
            internal float recentSpeed = 1;
            internal int anchorAttempts = 0;
            internal float lastTechExtents = 1;
            internal float lastAuxVal = 0;

            public Visible lastPlayer;
            public Visible lastEnemy;
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
            internal short DirectorUpdateClock = 0;
            internal short OperationsUpdateClock = 500;
            internal short DelayedAnchorClock = 0;
            internal short FeatherBoostersClock = 50;
            internal float RepairStepperClock = 0;    
            internal short BeamTimeoutClock = 0;
            internal int WeaponDelayClock = 0;
            internal int ActionPause = 0;               // when [val > 0], used to halt other actions 
            internal short unanchorCountdown = 0;         // aux warning for unanchor

            //Drive Direction Handlers
            /// <summary> Do we steer to target destination? </summary>
            internal bool Steer = false;

            /// <summary> Drive direction </summary>
            internal EDriveFacing DriveDir = EDriveFacing.Neutral;

            /// <summary> Drive AWAY from target </summary>
            internal bool AdviseAway = false;

            /// <summary>
            /// Move to a dynamic target
            /// </summary>
            internal EDriveDest DriveDest = EDriveDest.None;
            internal bool IsMovingAnyDest => DriveDest != EDriveDest.None;
            internal bool IsMovingToDest => DriveDest > EDriveDest.FromLastDestination;
            internal bool IsMovingFromDest => DriveDest == EDriveDest.FromLastDestination;

            //Finals
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
            internal bool BOOST = false;                // hold down boost button
            internal bool FeatherBoost = false;         // moderated booster pressing
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
            public bool isRTSControlled = false;
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
                            AIEx.WasRTS = isRTSControlled;
                        }
                    }
                }
            } // force the tech to be controlled by RTS
            public bool IsGoingToRTSDest => RTSDestInternal != Vector3.zero;
            internal Vector3 RTSDestination {
                get
                {
                    if (RTSDestInternal == Vector3.zero)
                    {
                        if (lastEnemy != null)
                            return lastEnemy.tank.boundsCentreWorldNoCheck;
                        else if (Obst != null)
                            return Obst.position + Vector3.up;
                        return tank.boundsCentreWorldNoCheck;
                    }
                    return RTSDestInternal;
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

                    if (value == Vector3.zero)
                        RTSDestInternal = value;
                    else if (DriverType == AIDriverType.Astronaut || DriverType == AIDriverType.Pilot)
                        RTSDestInternal = AIEPathing.OffsetFromGroundA(value, this, AIGlobals.RTSAirGroundOffset);
                    else
                        RTSDestInternal = value;
                    foreach (ModuleAIExtension AIEx in AIList)
                    {
                        AIEx.WasRTS = isRTSControlled;
                        AIEx.RTSPos = RTSDestInternal;
                    }
                }
            }
            private Vector3 RTSDestInternal = Vector3.zero;

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
            /// ONLY CALL FROM NETWORK HANDLER!
            /// </summary>
            /// <param name="Pos"></param>
            internal void DirectRTSDest(Vector3 Pos)
            {
                RTSDestInternal = Pos;
                foreach (ModuleAIExtension AIEx in AIList)
                {
                    AIEx.WasRTS = isRTSControlled;
                    AIEx.RTSPos = RTSDestInternal;
                }
            }

            public bool OverrideAllControls = false;        // force the tech to be controlled by external means
            public bool PlayerAllowAutoAnchoring = false;   // Allow auto-anchor
            public bool ExpectAITampering = false;          // Set the AI back to Escort next update
            internal EventNoParams FinishedRepairEvent = new EventNoParams();


            // AI Core
            public IMovementAIController MovementController;

            // Troubleshooting
            //internal bool RequirementsFailiure = false;



            //-----------------------------
            //         SUBCRIPTIONS
            //-----------------------------
            public TankAIHelper Subscribe()
            {
                tank = GetComponent<Tank>();
                Vector3 _ = tank.boundsCentreWorld;
                AIList = new List<ModuleAIExtension>();
                ManWorldTreadmill.inst.AddListener(this);
                Invoke("DelayedSubscribe", 0.1f);
                return this;
            }
            public void DelayedSubscribe()
            {
                try
                {
                    lastTechExtents = tank.blockBounds.size.magnitude + 1;
                    cachedBlockCount = tank.blockman.blockCount;
                }
                catch { }
            }

            public void OnAttach(TankBlock newBlock, Tank tank)
            {
                //Debug.Log("TACtical_AI: On Attach " + tank.name);
                TankAIHelper thisInst = tank.GetComponent<TankAIHelper>();
                thisInst.EstTopSped = 1;
                //thisInst.LastBuildClock = 0;
                thisInst.PendingHeightCheck = true;
                thisInst.dirty = true;
                dirtyAI = true;
                if (thisInst.AIState == AIAlignment.Player)
                {
                    try
                    {
                        if (!thisInst.PendingDamageCheck && thisInst.TechMemor)
                        {
                            thisInst.TechMemor.SaveTech();
                        }
                    }
                    catch { }
                }
                else if (thisInst.AIState == AIAlignment.NonPlayer)
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
                if (thisInst.AIState == AIAlignment.Player)
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
            }


            /*
            public void OnRecycle(Tank tank)
            {
                //Debug.Log("TACtical_AI: Allied AI " + tank.name + ":  Called OnRecycle");
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
                        //Debug.Log("TACtical_AI: Anonymous sender error");
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
                        AIEx.WasRTS = isRTSControlled;
                    else
                        DebugTAC_AI.Log("TACtical_AI: NULL ModuleAIExtension IN " + tank.name);
                }
            }
            public void OnMoveWorldOrigin(IntVector3 move)
            {
                if (RTSDestInternal != Vector3.zero)
                    RTSDestInternal += move;
                lastDestination += move;
                
                MovementController.OnMoveWorldOrigin(move);
            }


            public void ResetAll(Tank tank)
            {
                //Debug.Log("TACtical_AI: Resetting all for " + tank.name);
                cachedBlockCount = tank.blockman.blockCount;
                SuppressFiring(false);
                lastAIType = AITreeType.AITypes.Idle;
                dirty = true;
                dirtyAI = true;
                PlayerAllowAutoAnchoring = !tank.IsAnchored;
                ExpectAITampering = false;
                GroundOffsetHeight = AIGlobals.GeneralAirGroundOffset;

                AIState = AIAlignment.Static;
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
                RTSDestination = Vector3.zero;
                tank.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                World.ManPlayerRTS.ReleaseControl(this);
                var Funds = tank.gameObject.GetComponent<RBases.EnemyBaseFunder>();
                if (Funds.IsNotNull())
                    Funds.OnRecycle(tank);
                var Mind = tank.gameObject.GetComponent<EnemyMind>();
                if (Mind.IsNotNull())
                    Mind.SetForRemoval();
                var Mem = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (Mem.IsNotNull())
                    Mem.Remove();
                var Select = tank.gameObject.GetComponent<SelectHalo>();
                if (Select.IsNotNull())
                    Select.Remove();
                var Pnt = tank.gameObject.GetComponents<BookmarkBuilder>();
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


            /// <summary>
            /// Was previously: TestForFlyingAIRequirement
            /// </summary>
            /// <returns>True if the AI can fly</returns>
            public bool RecalibrateMovementAIController()
            {
                UsingAirControls = false;
                var enemy = gameObject.GetComponent<EnemyMind>();
                if ((DriverType == AIDriverType.Stationary || (tank.IsAnchored && !PlayerAllowAutoAnchoring)) && !enemy)
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
                    MovementController.Initiate(tank, this, null);
                    UsingAirControls = false;
                    return false;
                }
                else if (DriverType == AIDriverType.Pilot && AIState == AIAlignment.Player)
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
                else if (AIState == AIAlignment.NonPlayer && enemy.IsNotNull())
                {
                    if (enemy && enemy.EvilCommander == Enemy.EnemyHandling.Chopper || enemy.EvilCommander == Enemy.EnemyHandling.Airplane)
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
                    }
                    return true;
                }
                else
                {
                    if (!(MovementController is AIControllerDefault))
                    {
                        IMovementAIController controller = MovementController;
                        MovementController = null;
                        if (controller != null)
                        {
                            controller.Recycle();
                        }
                        MovementController = gameObject.GetOrAddComponent<AIControllerDefault>();
                        MovementController.Initiate(tank, this, enemy);
                    }
                    return false;
                }
            }


            /// <summary>
            /// Does not remove EnemyMind
            /// </summary>
            public void RefreshAI()
            {
                AvoidStuff = true;
                IdealRangeCombat = 25;
                AutoAnchor = false;
                FullMelee = false;      // Should the AI ram the enemy?
                AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
                SecondAvoidence = false;// Should the AI avoid two techs at once?
                OnlyPlayerMT = true;
                SideToThreat = false;
                UsingAirControls = false;
                useInventory = false;

                if (tank.PlayerFocused)
                {   // player gets full control
                    isAegisAvail = true;
                    isAssassinAvail = true;

                    isProspectorAvail = true;
                    isScrapperAvail = true;
                    isEnergizerAvail = true;

                    isAstrotechAvail = true;
                    isAviatorAvail = true;
                    isBuccaneerAvail = true;
                }
                else
                {
                    isAegisAvail = false;
                    isAssassinAvail = false;

                    isProspectorAvail = false;
                    isScrapperAvail = false;
                    isEnergizerAvail = false;

                    isAstrotechAvail = false;
                    isAviatorAvail = false;
                    isBuccaneerAvail = false;
                }

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
                    {
                        AIList.Add(AIE);
                    }
                }
                DebugTAC_AI.Info("TACtical_AI: AI list for Tech " + tank.name + " has " + AIList.Count() + " entries");
                bool hasAnchorableAI = false;
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
                        AdvancedAI = true;
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
                    if (AIEx.InventoryUser)
                        useInventory = true;
                    if (AIEx.GetComponent<ModuleAnchor>())
                    {
                        if (ManWorld.inst.GetTerrainHeight(AIEx.transform.position, out float height))
                            if (AIEx.GetComponent<ModuleAnchor>().HeightOffGroundForMaxAnchor() > height)
                                hasAnchorableAI = true;
                    }

                    // Engadgement Ranges
                    if (AIEx.MinCombatRange > IdealRangeCombat)
                        IdealRangeCombat = AIEx.MinCombatRange;
                    if (AIEx.MaxCombatRange > RangeToChase)
                        RangeToChase = AIEx.MaxCombatRange;

                    if (AIEx.WasRTS)
                    {
                        SetRTSState(true);
                        RTSDestination = AIEx.RTSPos;
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
                    ExecuteAutoSet();
                }

                MovementController = null;
                EnemyMind enemy = gameObject.GetComponent<EnemyMind>();

                RecalibrateMovementAIController();

                if (allowAutoRepair)
                {
                    if (TechMemor.IsNull())
                    {
                        TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                        TechMemor.Initiate();

                        DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + " Setup for DesignMemory (RefreshAI)");
                    }
                }
                else
                {
                    if (TechMemor.IsNotNull())
                        TechMemor.Remove();
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
                RecalibrateMovementAIController();
            }

            public void OnSwitchAI(bool resetRTSstate)
            {
                AvoidStuff = true;
                EstTopSped = 1;
                foundBase = false;
                foundGoal = false;
                lastBasePos = null;
                lastPlayer = null;
                lastEnemy = null;
                lastCloseAlly = null;
                theBase = null;
                IsTryingToUnjam = false;
                JustUnanchored = false;
                DropBlock();
                if (resetRTSstate)
                {
                    isRTSControlled = false;
                    foreach (ModuleAIExtension AIEx in AIList)
                    {
                        AIEx.WasRTS = isRTSControlled;
                    }
                    tank.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                }
                //World.PlayerRTSControl.ReleaseControl(this);
            }
            public void ForceAllAIsToEscort(bool Do = true)
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
                    ForceRebuildAlignment();
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
                //Debug.Log("TACtical_AI: GetOtherDir");
                Vector3 inputOffset = tank.transform.position - targetToAvoid.transform.position;
                float inputSpacing = targetToAvoid.GetCheapBounds() + lastTechExtents + DodgeStrength;
                Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
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
                //Debug.Log("TACtical_AI: GetDir");
                Vector3 inputOffset = tank.transform.position - targetToAvoid.transform.position;
                float inputSpacing = targetToAvoid.GetCheapBounds() + lastTechExtents + DodgeStrength;
                Vector3 Final = -(inputOffset.normalized * inputSpacing) + tank.transform.position;
                return Final;
            }


            // Collision Avoidence
            public Vector3 AvoidAssist(Vector3 targetIn)
            {
                //The method to determine if we should avoid an ally nearby while navigating to the target
                //IsLikelyJammed = false;
                if (AvoidStuff)
                {
                    try
                    {
                        bool obst;
                        Tank lastCloseAlly;
                        float lastAllyDist;
                        if (SecondAvoidence && moreThan2Allies)// MORE processing power
                        {
                            lastCloseAlly = AIEPathing.SecondClosestAlly(tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                            if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace)
                            {
                                if (lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.ExtraSpace)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    //IsLikelyJammed = true;
                                    Vector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2) + AIEPathing.ObstDodgeOffset(tank, this, out obst, AdvancedAI);
                                    if (obst)
                                        return (targetIn + ProccessedVal2) / 4;
                                    else
                                        return (targetIn + ProccessedVal2) / 3;

                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                                //IsLikelyJammed = true;
                                Vector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, this, out obst, AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 3;
                                else
                                    return (targetIn + ProccessedVal) / 2;
                            }
                            else
                            {
                                Vector3 ProccessedVal = AIEPathing.ObstDodgeOffset(tank, this, out obst, AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 2;
                                else
                                    return targetIn;
                            }
                        }
                        lastCloseAlly = AIEPathing.ClosestAlly(tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                        //Debug.Log("TACtical_AI: Ally is " + lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        //if (lastCloseAlly == null)
                        //    Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, this, out obst, AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 3;
                            else
                                return (targetIn + ProccessedVal) / 2;
                        }
                        else
                        {
                            Vector3 ProccessedVal = AIEPathing.ObstDodgeOffset(tank, this, out obst, AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 2;
                            else
                                return targetIn;
                        }
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Crash on Avoid " + e);
                        return targetIn;
                    }
                }
                if (targetIn.IsNaN())
                {
                    DebugTAC_AI.Log("TACtical_AI: AvoidAssist IS NaN!!");
                    //TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }
            public Vector3 AvoidAssistInv(Vector3 targetIn)
            {
                //The method to determine if we should avoid an ally nearby while navigating to the target - REVERSED
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                //IsLikelyJammed = false;
                if (AvoidStuff)
                {
                    try
                    {
                        Tank lastCloseAlly;
                        float lastAllyDist;
                        if (SecondAvoidence && moreThan2Allies)// MORE processing power
                        {
                            lastCloseAlly = AIEPathing.SecondClosestAlly(tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                            if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace)
                            {
                                if (lastAuxVal < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    //IsLikelyJammed = true;
                                    Vector3 ProccessedVal2 = GetDir(lastCloseAlly) + GetDir(lastCloseAlly2) - AIEPathing.ObstDodgeOffsetInv(tank, thisInst, out bool obst2, AdvancedAI);
                                    if (obst2)
                                        return (targetIn + ProccessedVal2) / 4;
                                    else
                                        return (targetIn + ProccessedVal2) / 3;

                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                                //IsLikelyJammed = true;
                                Vector3 ProccessedVal = GetDir(lastCloseAlly) + AIEPathing.ObstDodgeOffsetInv(tank, thisInst, out bool obst, AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 3;
                                else
                                    return (targetIn + ProccessedVal) / 2;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAlly(tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                        //Debug.Log("TACtical_AI: Ally is " + lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        //if (lastCloseAlly == null)
                        //    Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //IsLikelyJammed = true;
                            Vector3 ProccessedVal = GetDir(lastCloseAlly) + AIEPathing.ObstDodgeOffsetInv(tank, thisInst, out bool obst, AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 3;
                            else
                                return (targetIn + ProccessedVal) / 2;
                        }
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Crash on Avoid " + e);
                        return targetIn;
                    }
                }
                if (targetIn.IsNaN())
                {
                    DebugTAC_AI.Log("TACtical_AI: AvoidAssist IS NaN!!");
                    //TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }
            public Vector3 AvoidAssistPrecise(Vector3 targetIn)
            {
                //The method to determine if we should avoid an ally nearby while navigating to the target
                //  MORE DEMANDING THAN THE ABOVE!
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                if (AvoidStuff)
                {
                    try
                    {
                        bool obst;
                        Tank lastCloseAlly;
                        float lastAllyDist;
                        if (SecondAvoidence && moreThan2Allies)// MORE processing power
                        {
                            lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                            if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace)
                            {
                                if (lastAuxVal < lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.ExtraSpace)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    Vector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2) + AIEPathing.ObstDodgeOffset(tank, thisInst, out obst, AdvancedAI);
                                    if (obst)
                                        return (targetIn + ProccessedVal2) / 4;
                                    else
                                        return (targetIn + ProccessedVal2) / 3;
                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);

                                Vector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, out obst, AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 3;
                                else
                                    return (targetIn + ProccessedVal) / 2;
                            }
                            else
                            {
                                Vector3 ProccessedVal = AIEPathing.ObstDodgeOffset(tank, this, out obst, AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 2;
                                else
                                    return targetIn;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out lastAllyDist, tank);
                        //Debug.Log("TACtical_AI: Ally is " + lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        //if (lastCloseAlly == null)
                        //    Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            Vector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, out obst, AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 3;
                            else
                                return (targetIn + ProccessedVal) / 2;
                        }
                        else
                        {
                            Vector3 ProccessedVal = AIEPathing.ObstDodgeOffset(tank, this, out obst, AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 2;
                            else
                                return targetIn;
                        }
                    }
                    catch //(Exception e)
                    {
                        //Debug.Log("TACtical_AI: Crash on Avoid Allied" + e);
                        return targetIn;
                    }
                }
                if (targetIn.IsNaN())
                {
                    DebugTAC_AI.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                    //TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }


            // Obstruction Management
            public void AutoHandleObstruction(float dist = 0, bool useRush = false, bool useGun = true)
            {
                if (!IsTechMoving(EstTopSped / 4))
                {
                    TryHandleObstruction(true, dist, useRush, useGun);
                }
            }
            public void TryHandleObstruction(bool hasMessaged, float dist, bool useRush, bool useGun)
            {
                //Something is in the way - try fetch the scenery to shoot at
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Obstructed");
                if (!hasMessaged)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Can't move there - something's in the way!");
                }

                IsTryingToUnjam = false;
                if (ForceSetDrive && DriveVar < 0)
                {   // we are likely driving backwards
                    ForceSetDrive = true;
                    DriveVar = -1;

                    UrgencyOverload += KickStart.AIClockPeriod / 2f;
                    if (Urgency >= 0)
                        Urgency += KickStart.AIClockPeriod / 5f;
                    if (UrgencyOverload > 50)
                    {
                        //Are we just randomly angry for too long? let's fix that
                        AIMessage(tech: tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                        EstTopSped = 1;
                        AvoidStuff = true;
                        UrgencyOverload = 0;
                    }
                    else if (useRush && dist > RangeToStopRush * 2)
                    {
                        //SCREW IT - GO FULL SPEED WE ARE TOO FAR BEHIND!
                        if (useGun)
                            RemoveObstruction();
                        ForceSetDrive = true;
                        DriveVar = -1f;
                        Urgency += KickStart.AIClockPeriod / 5f;
                    }
                    else if (75 < FrustrationMeter)
                    {
                        IsTryingToUnjam = true;
                        //Try build beaming to clear debris
                        FrustrationMeter += KickStart.AIClockPeriod;
                        if (225 < FrustrationMeter)
                        {
                            FrustrationMeter = 0;
                        }
                        else if (150 < FrustrationMeter)
                        {
                            ForceSetBeam = false;
                            ForceSetDrive = true;
                            DriveVar = 1;
                        }
                        else
                            ForceSetBeam = true;
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
                    if (UrgencyOverload > 50)
                    {
                        //Are we just randomly angry for too long? let's fix that
                        AIMessage(tech: tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                        EstTopSped = 1;
                        AvoidStuff = true;
                        UrgencyOverload = 0;
                    }
                    else if (useRush && dist > RangeToStopRush * 2)
                    {
                        //SCREW IT - GO FULL SPEED WE ARE TOO FAR BEHIND!
                        if (useGun)
                            RemoveObstruction();
                        ForceSetDrive = true;
                        DriveVar = 1f;
                        Urgency += KickStart.AIClockPeriod / 5f;
                    }
                    else if (75 < FrustrationMeter)
                    {
                        IsTryingToUnjam = true;
                        //Try build beaming to clear debris
                        FrustrationMeter += KickStart.AIClockPeriod;
                        if (225 < FrustrationMeter)
                        {
                            FrustrationMeter = 0;
                        }
                        else if (150 < FrustrationMeter)
                        {
                            DriveDest = EDriveDest.None;
                            ForceSetBeam = false;
                            ForceSetDrive = true;
                            DriveVar = -1;
                        }
                        else
                        {
                            DriveDest = EDriveDest.FromLastDestination;
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
                    ObstList = AIEPathing.ObstructionAwareness(tank.boundsCentreWorldNoCheck + tank.rbody.velocity, this, searchRad);
                else
                    ObstList = AIEPathing.ObstructionAwareness(tank.boundsCentreWorldNoCheck, this, searchRad);
                int bestStep = 0;
                float bestValue = 250000; // 500
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
            public void RemoveObstruction(float searchRad = 12)
            {
                // Shoot at the scenery obsticle infront of us
                if (Obst == null)
                {
                    Obst = GetObstruction(searchRad);
                    Urgency += KickStart.AIClockPeriod / 5;
                }
                FIRE_NOW = true;
            }
            public void SettleDown()
            {
                UrgencyOverload = 0;
                Urgency = 0;
                FrustrationMeter = 0;
                Obst = null;
            }


            //-----------------------------
            //           CHECKS
            //-----------------------------
            private void DetermineCombat()
            {
                bool DoNotEngage = false;
                if (lastEnemy?.tank)
                    if (!tank.IsEnemy(lastEnemy.tank.Team))
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
                    if (RangeToChase * 2 < (lastBasePos.position - tank.boundsCentreWorldNoCheck).magnitude)
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
                        if (RangeToChase * 4 < (lastPlayer.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude)
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
                        if (RangeToChase < (lastPlayer.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude)
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
                bool DoNotEngage = false;
                if (lastEnemy?.tank)
                    if (!tank.IsEnemy(lastEnemy.tank.Team))
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
            }



            public string GetActionStatus(out bool cantDo)
            {
                cantDo = false;
                if (tank.IsPlayer)
                {
                    if (!RTSControlled)
                        return "Autopilot Disabled";
                }
                else
                {
                    if (!ActuallyWorks)
                        return "No AI Modules";
                    else if (!SetToActive)
                        return "Idle (Off)";
                }
                string output = "At Destination";
                if (RTSControlled)
                {
                    switch (DriverType)
                    {
                        case AIDriverType.Astronaut:
                            if (lastEnemy)
                                output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                            else
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else
                                    output = "Space Orders";
                            }
                            break;
                        case AIDriverType.Pilot:
                            if (lastEnemy)
                                output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
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
                                            output = "Flying Orders";
                                    }
                                }
                                else
                                    output = "Unhandled error in switch";
                            }
                            break;
                        case AIDriverType.Sailor:
                            if (lastEnemy)
                                output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                            else
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Stuck & Beached";
                                else
                                    output = "Sailing Orders";
                            }
                            break;
                        default:
                            if (lastEnemy)
                                output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                            else
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Stuck on an obsticle";
                                else
                                    output = "Land Orders";
                            }
                            break;
                    }
                    return output;
                }

                switch (DediAI)
                {
                    case AIType.Aegis:
                        if (lastEnemy)
                            output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                        else if (lastCloseAlly)
                            output = "Following " + (lastCloseAlly.name.NullOrEmpty() ? "unknown" : lastCloseAlly.name);
                        else
                            output = "Looking for Ally";
                        break;
                    case AIType.Assault:
                        if (DriveDest == EDriveDest.ToBase)
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
                                if (lastEnemy)
                                {
                                    output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                                }
                                else
                                    output = "Moving out to enemy";
                            }
                            else
                                output = "Scouting for Enemies";
                        }
                        break;
                    case AIType.Energizer:
                        if (DriveDest == EDriveDest.ToBase)
                        {
                            if (theBase)
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else if(recentSpeed > 8)
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
                                else if(recentSpeed > 8)
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
                                if (lastEnemy)
                                    output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                                else
                                {
                                    if (WeaponState == AIWeaponState.Obsticle)
                                        output = "Removing Obstruction";
                                    else
                                        output = "Floating Escort";
                                }
                                break;
                            case AIDriverType.Pilot:
                                if (lastEnemy)
                                    output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
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
                                                output = "Flying Escort";
                                        }
                                    }
                                    else
                                        output = "Unhandled error in switch";
                                }
                                break;
                            case AIDriverType.Sailor:
                                if (lastEnemy)
                                    output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                                else
                                {
                                    if (WeaponState == AIWeaponState.Obsticle)
                                        output = "Stuck & Beached";
                                    else
                                        output = "Sailing Escort";
                                }
                                break;
                            default:
                                if (lastEnemy)
                                    output = "Fighting " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                                else
                                {
                                    if (WeaponState == AIWeaponState.Obsticle)
                                        output = "Stuck on an obsticle";
                                    else
                                        output = "Land Escort";
                                }
                                break;
                        }
                        break;
                    case AIType.MTMimic:
                        if (OnlyPlayerMT)
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
                        if ((bool)lastEnemy)
                        {
                            if (AttackEnemy)
                                output = "Shooting at " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                            else
                                output = "Aiming at " + (lastEnemy.name.NullOrEmpty() ? "unknown" : lastEnemy.name);
                        }
                        else
                            output = "Face the Danger";
                        break;
                    case AIType.Prospector:
                        if (DriveDest == EDriveDest.ToBase)
                        {
                            if ((bool)theBase)
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else if(recentSpeed > 8)
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
                                        output = "Going to dig " + theResource.name;
                                    //StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                                }
                                else
                                {
                                    if (CT.Count == 0)
                                        output = "Clearing rocks";
                                    else
                                        output = "Mining " + theResource.name;
                                    //output = "Mining " + StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Chunk, (int)CT));
                                }
                            }
                            else
                                output = "No resources in " + (DetectionRange + AIGlobals.FindItemExtension) + " meters";
                        }
                        break;
                    case AIType.Scrapper:
                        if (DriveDest == EDriveDest.ToBase)
                        {
                            if ((bool)theBase)
                            {
                                if (WeaponState == AIWeaponState.Obsticle)
                                    output = "Removing Obstruction";
                                else if(recentSpeed > 8)
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
                                else if(recentSpeed > 8)
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
                                output = "No blocks in " + (DetectionRange + AIGlobals.FindItemExtension) + " meters";
                        }
                        break;
                }
                return output;
            }

            private int cachedBlockCount = 1;
            public bool CanDetectHealth()
            {
                return TechMemor || AdvancedAI; 
            }
            /// <summary>
            /// 100 for max, 0 for pretty much destroyed
            /// </summary>
            /// <returns></returns>
            public float GetHealth()
            {
                if (!CanDetectHealth())
                    return 100;
                return 100 - DamageThreshold;
            }
            public float GetSpeed()
            {
                if (tank.rbody.IsNull())
                    return 0; // Slow/Stopped
                if (IsTryingToUnjam)
                    return 0;
                if (Attempt3DNavi || MovementController is AIControllerAir)
                {
                    return tank.rbody.velocity.magnitude;
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

                return energy.spareCapacity - energy.storageTotal;
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
                    return tank.rbody.velocity.sqrMagnitude > minSpeed * minSpeed;
                }
                else
                {
                    if (!(bool)tank.rootBlockTrans)
                        return false;
                    return tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).z > minSpeed || Mathf.Abs(tank.control.DriveControl) < 0.5f;
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
                    return tank.rbody.velocity.sqrMagnitude > minSpeed * minSpeed;
                }
                else
                {
                    if (!(bool)tank.rootBlockTrans)
                        return false;
                    return tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).z > minSpeed;
                }
            }
            public Visible GetPlayerTech()
            {
                if (ManNetwork.IsNetworked)
                {
                    try
                    {
                        /*
                        Debug.Log("TACtical_AI: The Tech's Team: " + tank.Team + " | RTS Mode: " + RTSControlled);
                        foreach (Tank thatTech in ManNetwork.inst.GetAllPlayerTechs())
                        {
                            Debug.Log("TACtical_AI: " + thatTech.name + " | of " + thatTech.netTech.Team);
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
                if (lastEnemy?.tank)
                {
                    if (lastEnemy.tank.blockman.blockCount == 0)
                    {
                        lastEnemy = null;
                        //Debug.Assert(true, "TACtical_AI: Tech " + tank.name + " has valid, live target but it has no blocks.  How is this possible?!");
                    }
                }
            }


            //-----------------------------
            //           ACTIONS
            //-----------------------------


            public void ManageAILockOn()
            {
                switch (ActiveAimState)
                {
                    case AIWeaponState.Enemy:
                        if (lastEnemy.IsNotNull())
                        {   // Allow the enemy AI to finely select targets
                            //Debug.Log("TACtical_AI: Overriding targeting to aim at " + lastEnemy.name + "  pos " + lastEnemy.tank.boundsCentreWorldNoCheck);

                            if (lastPlayer.IsNotNull())
                            {
                                var playerTarg = lastPlayer.tank.Weapons.GetManualTarget();
                                if (playerTarg != null)
                                {
                                    if ((bool)playerTarg.tank)
                                    {
                                        try
                                        {
                                            if (playerTarg.tank.CentralBlock && playerTarg.isActive)
                                            {   // Relay position from player to allow artillery support
                                                lastLockedTarget = playerTarg;
                                                return;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            lastLockedTarget = lastEnemy;
                        }
                        break;
                    case AIWeaponState.Obsticle:
                        if (Obst.IsNotNull())
                        {
                            var resTarget = Obst.GetComponent<Visible>();
                            if (resTarget)
                            {
                                //Debug.Log("TACtical_AI: Overriding targeting to aim at obstruction");
                                lastLockedTarget = resTarget;
                            }
                        }
                        break;
                    case AIWeaponState.Mimic:
                        if (lastCloseAlly.IsNotNull())
                        {
                            //Debug.Log("TACtical_AI: Overriding targeting to aim at player's target");
                            var helperAlly = lastCloseAlly.GetComponent<TankAIHelper>();
                            if (helperAlly.ActiveAimState == AIWeaponState.Enemy)
                                lastLockedTarget = helperAlly.lastEnemy;
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
                    if ((lastLockedTarget.centrePosition - tank.boundsCentreWorldNoCheck).sqrMagnitude > maxDist * maxDist)
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
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Allowing approach");
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
                                heldBlock.visible.SetInteractionTimeout(0.25f);
                            }
                            else
                            {
                                moveVec = tank.boundsCentreWorldNoCheck + (Vector3.up * (lastTechExtents + 3)) - heldBlock.visible.centrePosition;
                                moveVec = Vector3.ClampMagnitude(moveVec * 4, AIGlobals.ItemGrabStrength);
                                heldBlock.rbody.AddForce(moveVec - (Physics.gravity * heldBlock.AverageGravityScaleFactor), ForceMode.Acceleration);
                                heldBlock.visible.SetInteractionTimeout(0.25f);
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
            internal bool HoldBlock(Visible TB, BlockMemory BM)
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
                    heldBlock.visible.SetInteractionTimeout(0);
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
                    heldBlock.visible.SetInteractionTimeout(0);
                    heldBlock = null;
                }
            }


            // Handle Anchors
            internal void TryAnchor()
            {
                if (CanAnchorSafely && !tank.IsAnchored)
                {
                    DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Trying to anchor");
                    tank.Anchors.TryAnchorAll(true);
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
                            DebugTAC_AI.Assert(true, "(ATTRACT BASE) screw you i'm anchoring anyways, I don't give a f*bron about your anchor checks!");
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
                if (!tank.IsAnchored && AIState == AIAlignment.Player)
                    ForceAllAIsToEscort(true);
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
                                if (tank.GetComponent<RequestAnchored>())
                                {
                                    TryReallyAnchor();
                                }
                                // let the icon update
                            }
                            else if (AIState == AIAlignment.Player)
                            {
                                //Debug.Log("TACtical_AI: AI Valid!");
                                //Debug.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                                //tankAIHelp.AIState && 
                                if (JustUnanchored)
                                {
                                    ForceAllAIsToEscort();
                                    JustUnanchored = false;
                                }
                                else if (SetToActive)
                                {
                                    //Debug.Log("TACtical_AI: Running BetterAI");
                                    //Debug.Log("TACtical_AI: Patched Tank ExecuteControl(TankAIHelper)");
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
                            else if (KickStart.enablePainMode && AIState == AIAlignment.NonPlayer)
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
                    if (!tank.PlayerFocused || (KickStart.AllowStrategicAI && ManPlayerRTS.autopilotPlayer && ManPlayerRTS.PlayerIsInRTS))//&& !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                    {
                        if (tank.FirstUpdateAfterSpawn)
                        {
                            if (tank.GetComponent<RequestAnchored>())
                            {
                                TryReallyAnchor();
                            }
                            // let the icon update
                        }
                        else if (AIState == AIAlignment.Player)
                        {
                            //Debug.Log("TACtical_AI: AI Valid!");
                            //Debug.Log("TACtical_AI: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                            if (JustUnanchored)
                            {
                                ForceAllAIsToEscort();
                                JustUnanchored = false;
                            }
                            else if (tank.PlayerFocused)
                            {
                                SetRTSState(true);
                                UpdateTechControl(thisControl);
                                return true;
                            }
                            else if (SetToActive)
                            {
                                //Debug.Log("TACtical_AI: Running BetterAI");
                                //Debug.Log("TACtical_AI: Patched Tank ExecuteControl(TankAIHelper)");
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
                        else if (KickStart.enablePainMode && AIState == AIAlignment.NonPlayer)
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

                if (MovementController is null)
                {
                    DebugTAC_AI.Log("NULL MOVEMENT CONTROLLER");
                }

                AIEBeam.BeamMaintainer(thisControl, this, tank);
                if (UpdatePathfinding)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Fired CollisionAvoidUpdate!");
                    try
                    {
                        AIEWeapons.WeaponDirector(thisControl, this, tank);

                        AdviseAway = false;
                        if (RTSControlled)
                            MovementController.DriveDirectorRTS();
                        else
                            MovementController.DriveDirector();
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Potential error in DriveDirector (or WeaponDirector)!");
                    }

                    UpdatePathfinding = false; // incase they fall out of sync
                }
                try
                {
                    if (NotInBeam)
                    {
                        AIEWeapons.WeaponMaintainer(thisControl, this, tank);
                        MovementController.DriveMaintainer(thisControl);
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
            public void FixedUpdate()
            {   
                if (KickStart.EnableBetterAI)
                {
                    UpdateOperatorsAndDirectorClock();
                }
            }
            private void UpdateOperatorsAndDirectorClock()
            {//Handler for the improved AI, gets the job done.
                try
                {
                    RebuildAlignment();
                    UpdateCollectors();
                    UpdateBlockHold();
                    ManageAILockOn();
                    if (ManNetwork.IsNetworked)
                    {
                        if (!ManNetwork.IsHost)// && tank != Singleton.playerTank)
                        {
                            if (dirty)
                            {
                                dirty = false;
                                tank.blockman.CheckRecalcBlockBounds();
                                lastTechExtents = tank.blockBounds.size.magnitude + 1;
                                if (!PendingDamageCheck)
                                    cachedBlockCount = tank.blockman.blockCount;
                            }
                            UpdateClientAIActions();
                            return;
                        }
                        else if (dirty)
                        {
                            dirty = false;
                            tank.blockman.CheckRecalcBlockBounds();
                            lastTechExtents = tank.blockBounds.size.magnitude + 1;
                            tank.netTech.SaveTechData();
                            if (!PendingDamageCheck)
                                cachedBlockCount = tank.blockman.blockCount;
                        }
                        UpdateHostAIActions(KickStart.AIClockPeriod);
                    }
                    else
                    {
                        if (dirty)
                        {
                            dirty = false;
                            tank.blockman.CheckRecalcBlockBounds();
                            lastTechExtents = tank.blockBounds.size.magnitude + 1;
                            if (!PendingDamageCheck)
                                cachedBlockCount = tank.blockman.blockCount;
                        }
                        UpdateHostAIActions(KickStart.AIClockPeriod);
                    }
                }
                catch (Exception e)
                {
                    if (!errored)
                    {
                        DebugTAC_AI.LogError("TACtical_AI: CRITICAL ERROR IN UpdateOperators!!! - " + e);
                        errored = true;
                    }
                }
            }


            // AI Actions
            private void UpdateHostAIActions(int AIClockPeriod)
            {
                try
                {
                    switch (AIState)
                    {
                        case AIAlignment.Player: // Player-Controlled techs
                            if (OverrideAllControls)
                                return;

                            DirectorUpdateClock++;
                            if (DirectorUpdateClock >= KickStart.AIDodgeCheapness)
                            {
                                UpdatePathfinding = true;
                                DirectorUpdateClock = 0;
                            }

                            recentSpeed = GetSpeed();
                            if (recentSpeed < 1)
                                recentSpeed = 1;
                            OperationsUpdateClock++;
                            if (OperationsUpdateClock >= AIClockPeriod)
                            {
                                CheckEnemyErrorState();
                                if (IsTryingToUnjam)
                                    TryHandleObstruction(true, lastRange, false, true);
                                else
                                    RunAlliedOperations();
                                OperationsUpdateClock = 0;
                                if (EstTopSped < recentSpeed)
                                    EstTopSped = recentSpeed;
                            }
                            break;
                        case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                            if (KickStart.enablePainMode)
                            {
                                if (OverrideAllControls)
                                    return;
                                if (Hibernate)
                                {
                                    recentSpeed = GetSpeed();
                                    if (recentSpeed < 1)
                                        recentSpeed = 1;
                                    OperationsUpdateClock++;
                                    if (OperationsUpdateClock >= AIClockPeriod)
                                    {
                                        CheckEnemyErrorState();
                                        if (IsTryingToUnjam)
                                        {
                                            TryHandleObstruction(true, lastRange, false, true);
                                            var mind = GetComponent<EnemyMind>();
                                            if (mind)
                                                RCore.ScarePlayer(mind, this, tank);
                                        }
                                        else
                                            RunEnemyOperations(true);
                                        OperationsUpdateClock = 0;
                                        if (EstTopSped < recentSpeed)
                                            EstTopSped = recentSpeed;
                                    }
                                }
                                else
                                {
                                    DirectorUpdateClock++;
                                    if (DirectorUpdateClock >= KickStart.AIDodgeCheapness)
                                    {
                                        UpdatePathfinding = true;
                                        DirectorUpdateClock = 0;
                                    }
                                    else
                                        UpdatePathfinding = false;

                                    recentSpeed = GetSpeed();
                                    if (recentSpeed < 1)
                                        recentSpeed = 1;
                                    OperationsUpdateClock++;
                                    if (OperationsUpdateClock >= AIClockPeriod)
                                    {
                                        CheckEnemyErrorState();
                                        if (IsTryingToUnjam)
                                        {
                                            TryHandleObstruction(true, lastRange, false, true);
                                            var mind = GetComponent<EnemyMind>();
                                            if (mind)
                                                RCore.ScarePlayer(mind, this, tank);
                                        }
                                        else
                                            RunEnemyOperations();
                                        OperationsUpdateClock = 0;
                                        if (EstTopSped < recentSpeed)
                                            EstTopSped = recentSpeed;
                                    }
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

            /// <summary>
            /// MULTIPLAYER AI NON-HOST
            /// </summary>
            private void UpdateClientAIActions()
            {
                switch (AIState)
                {
                    case AIAlignment.Static:// Static tech
                        DriveVar = 0;
                        break;
                    case AIAlignment.Player: // Player-Controlled techs
                        if (OverrideAllControls)
                            return;

                        DirectorUpdateClock++;
                        if (DirectorUpdateClock >= KickStart.AIDodgeCheapness)
                        {
                            UpdatePathfinding = true;
                            DirectorUpdateClock = 0;
                        }

                        recentSpeed = GetSpeed();
                        if (recentSpeed < 1)
                            recentSpeed = 1;
                        OperationsUpdateClock++;
                        if (OperationsUpdateClock >= KickStart.AIClockPeriod)//Mathf.Max(25 / recentSpeed, 5)
                        {
                            // RunAlliedOperations(); // Do not call on client
                            OperationsUpdateClock = 0;
                            if (EstTopSped < recentSpeed)
                                EstTopSped = recentSpeed;
                        }
                        break;
                    case AIAlignment.NonPlayer: // Enemy / Enemy Base Team
                        if (OverrideAllControls)
                            return;
                        if (!Hibernate)
                        {
                            DirectorUpdateClock++;
                            if (DirectorUpdateClock >= KickStart.AIDodgeCheapness)
                            {
                                UpdatePathfinding = true;
                                DirectorUpdateClock = 0;
                            }
                            else
                                UpdatePathfinding = false;

                            recentSpeed = GetSpeed();
                            if (recentSpeed < 1)
                                recentSpeed = 1;
                            OperationsUpdateClock++;
                            if (OperationsUpdateClock >= KickStart.AIClockPeriod)
                            {
                                // RunEnemyOperations();
                                OperationsUpdateClock = 0;
                                if (EstTopSped < recentSpeed)
                                    EstTopSped = recentSpeed;
                            }
                        }
                        break;
                }
            }


            /// <summary>
            /// CALL when we change ANYTHING in the tech's AI.
            /// </summary>
            internal void OnTechTeamChange()
            {
                Invoke("DelayedExtents", 0.1f);
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
                    var aI = tank.AI;
                    hasAI = aI.CheckAIAvailable();

                    lastLockedTarget = null;
                    SuppressFiring(false);
                    try
                    {
                        if (ManNetwork.IsNetworked)
                        {   // Multiplayer
                            if (!ManNetwork.IsHost)// && tank != Singleton.playerTank)
                            {   // Is Client
                                if (ManSpawn.IsPlayerTeam(tank.Team))
                                {   //MP
                                    if (hasAI|| (World.ManPlayerRTS.PlayerIsInRTS && tank.PlayerFocused))
                                    {
                                        //Player-Allied AI
                                        if (AIState != AIAlignment.Player)
                                        {
                                            ResetAll(tank);
                                            RemoveEnemyMatters();
                                            AIState = AIAlignment.Player;
                                            RefreshAI();
                                            DebugTAC_AI.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go! (NonHostClient)");
                                        }
                                    }
                                    else
                                    {   // Static tech
                                        DriveVar = 0;
                                        if (AIState != AIAlignment.Static)
                                        {   // Reset and ready for static tech
                                            DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset (NonHostClient)");
                                            ResetAll(tank);
                                            RemoveEnemyMatters();
                                        }
                                    }
                                }
                                else if (!tank.IsNeutral())
                                {
                                    //Enemy AI
                                    if (AIState != AIAlignment.NonPlayer)
                                    {
                                        ResetAll(tank);
                                        AIState = AIAlignment.NonPlayer;
                                        DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech! (NonHostClient)");
                                        RCore.RandomizeBrain(this, tank);
                                    }
                                }
                                else
                                {   // Static tech
                                    DriveVar = 0;
                                    if (AIState != AIAlignment.Static)
                                    {   // Reset and ready for static tech
                                        DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset (NonHostClient)");
                                        ResetAll(tank);
                                        RemoveEnemyMatters();
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
                                    if (AIState != AIAlignment.Player)
                                    {
                                        ResetAll(tank);
                                        RemoveEnemyMatters();
                                        AIState = AIAlignment.Player;
                                        RefreshAI();
                                        if ((bool)TechMemor)
                                            TechMemor.SaveTech();
                                        DebugTAC_AI.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go!");
                                    }
                                }
                                else
                                {   // Static tech
                                    DriveVar = 0;
                                    if (AIState != AIAlignment.Static)
                                    {   // Reset and ready for static tech
                                        DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset");
                                        ResetAll(tank);
                                        RemoveEnemyMatters();
                                    }
                                }
                            }
                            else if (KickStart.enablePainMode && !tank.IsNeutral())
                            {
                                //Enemy AI
                                if (AIState != AIAlignment.NonPlayer)
                                {
                                    ResetAll(tank);
                                    AIState = AIAlignment.NonPlayer;
                                    Enemy.RCore.RandomizeBrain(this, tank);
                                    DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech!");
                                }
                                if (GetComponent<EnemyMind>())
                                    SuppressFiring(!GetComponent<EnemyMind>().AttackAny);
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIState != AIAlignment.Static)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset");
                                    ResetAll(tank);
                                    RemoveEnemyMatters();
                                    AIState = AIAlignment.Static;
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
                                    if (AIState != AIAlignment.Player)
                                    {
                                        ResetAll(tank);
                                        RemoveEnemyMatters();
                                        AIState = AIAlignment.Player;
                                        RefreshAI();
                                        if ((bool)TechMemor)
                                            TechMemor.SaveTech();
                                        DebugTAC_AI.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go!");
                                    }
                                }
                                else
                                {   // Static tech
                                    DriveVar = 0;
                                    if (AIState != AIAlignment.Static)
                                    {   // Reset and ready for static tech
                                        DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset");
                                        ResetAll(tank);
                                        RemoveEnemyMatters();
                                        AIState = AIAlignment.Static;
                                    }
                                }
                            }
                            else if (KickStart.enablePainMode && !tank.IsNeutral())
                            {   //MP is NOT supported!
                                //Enemy AI
                                if (AIState != AIAlignment.NonPlayer)
                                {
                                    ResetAll(tank);
                                    DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + " of Team " + tank.Team + ":  Ready to kick some Tech!");
                                    AIState = AIAlignment.NonPlayer;
                                    Enemy.RCore.RandomizeBrain(this, tank);
                                }
                            }
                            else
                            {   // Static tech
                                DriveVar = 0;
                                if (AIState != AIAlignment.Static)
                                {   // Reset and ready for static tech
                                    DebugTAC_AI.Log("TACtical_AI: Static Tech " + tank.name + ": reset");
                                    ResetAll(tank);
                                    RemoveEnemyMatters();
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

                    dirtyAI = false;
                }
            }
            private void TryRepairAllied()
            {
                if (allowAutoRepair && (!tank.PlayerFocused || ManPlayerRTS.PlayerIsInRTS) && (KickStart.AllowAISelfRepair || tank.IsAnchored))
                {
                    if (lastEnemy != null)
                    {   // Combat repairs (combat mechanic)
                        //Debug.Log("TACtical_AI: Tech " + tank.name + " RepairCombat");
                        AIERepair.RepairStepper(this, tank, TechMemor, AdvancedAI, Combat: true);
                    }
                    else
                    {   // Repairs in peacetime
                        //Debug.Log("TACtical_AI: Tech " + tank.name + " Repair");
                        if (AdvancedAI) // faster for smrt
                            AIERepair.InstaRepair(tank, TechMemor, KickStart.AIClockPeriod);
                        else
                            AIERepair.RepairStepper(this, tank, TechMemor);
                    }
                }
                else if (AdvancedAI)
                {
                    if (cachedBlockCount != 0)
                        DamageThreshold = (1f - (tank.blockman.blockCount / (float)cachedBlockCount)) * 100;
                    else
                        cachedBlockCount = tank.blockman.blockCount;
                }
            }


            private void RunAlliedOperations()
            {
                var aI = tank.AI;

                if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
                    TryRepairAllied();
                BoltsFired = false;

                if (!tank.IsAnchored && lastAIType == AITreeType.AITypes.Idle && ExpectAITampering)
                {
                    ForceAllAIsToEscort(true);
                    ExpectAITampering = false;
                }

                if (tank.PlayerFocused)
                {
                    //updateCA = true;
                    if (ActionPause > 0)
                        ActionPause--;
                    if (KickStart.AllowStrategicAI)
                    {
#if DEBUG
                        if (ManPlayerRTS.PlayerIsInRTS && ManPlayerRTS.DevLockToCam)
                        {
                            if (tank.rbody)
                            {
                                tank.rbody.MovePosition(Singleton.cameraTrans.position + (Vector3.up * 75));
                                return;
                            }
                        }
#endif
                        if (World.ManPlayerRTS.autopilotPlayer)
                        {
                            DetermineCombat();
                            if (RTSControlled)
                            {
                                RunRTSNavi(true);
                            }
                            else
                                OpsController.Execute();
                        }
                    }
                    return;
                }
                if (!aI.TryGetCurrentAIType(out lastAIType))
                {
                    lastAIType = AITreeType.AITypes.Idle;
                    return;
                }
                if (SetToActive)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Fired DelayedUpdate!");
                    Attempt3DNavi = false;

                    //updateCA = true;
                    if (ActionPause > 0)
                        ActionPause -= KickStart.AIClockPeriod;
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  current mode " + DediAI.ToString());

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
                DetermineCombatEnemy();
                if (light)
                    RCore.RunLightEvilOp(this, tank);
                else
                    RCore.BeEvil(this, tank);
            }
            private void RunRTSNavi(bool isPlayer = false)
            {   // Alternative Operator for RTS
                if (!KickStart.AllowStrategicAI)
                    return;

                Vector3 veloFlat = Vector3.zero;
                if ((bool)tank.rbody)   // So that drifting is minimized
                {
                    veloFlat = tank.rbody.velocity;
                    veloFlat.y = 0;
                }

                lastRange = (tank.boundsCentreWorldNoCheck + veloFlat - lastDestination).magnitude;
                //ProceedToObjective = true;
                if (DriverType == AIDriverType.Pilot)
                {
                    Attempt3DNavi = true;
                    BGeneral.ResetValues(this);
                    AvoidStuff = true;

                    float range = (RangeToStopRush * 4) + lastTechExtents;
                    // The range is nearly quadrupled here due to dogfighting conditions
                    DriveDest = EDriveDest.ToLastDestination;
                    if (tank.wheelGrounded)
                    {
                        if (!IsTechMoving(EstTopSped / 4))
                            TryHandleObstruction(!Feedback, lastRange, true, true);
                        else
                            SettleDown();
                    }
                    if (lastRange < (lastTechExtents * 2) + 5)
                    {

                    }
                    else if (lastRange > range)
                    {   // Far behind, must catch up
                        BOOST = true; // boost in forwards direction towards objective
                    }
                    else
                    {

                    }
                }
                else
                {
                    Attempt3DNavi = DriverType == AIDriverType.Astronaut;
                    BGeneral.ResetValues(this);
                    AvoidStuff = true;
                    if (lastRange < (lastTechExtents * 2) + 32 && !ManPlayerRTS.HasMovementQueue(this))
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
                        if (AutoAnchor && PlayerAllowAutoAnchoring && !isPlayer && tank.Anchors.NumPossibleAnchors >= 1)
                        {
                            if (tank.Anchors.NumIsAnchored > 0)
                            {
                                unanchorCountdown = 15;
                                UnAnchor();
                            }
                        }
                        if (!AutoAnchor && !isPlayer && tank.IsAnchored)
                        {
                            BGeneral.RTSCombat(this, tank);
                            return;
                        }
                        float driveVal = Mathf.Min(1, lastRange / 10);
                        if (!IsTechMovingActual((EstTopSped * driveVal) / 6) && lastRange > 32)
                        {   //OBSTRUCTION MANAGEMENT
                            Urgency += KickStart.AIClockPeriod / 2;
                            if (Urgency > 15)
                            {
                                //Debug.Log("TACtical_AI: AI " + tank.name + ":  DOOR STUCK");
                                TryHandleObstruction(true, lastRange, false, true);
                            }
                        }
                        else
                        {
                            //var val = tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).z;
                            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Output " + val + " | TopSpeed/2 " + (EstTopSped / 2) + " | TopSpeed/4 " + (EstTopSped / 4));
                            //Things are going smoothly
                            ForceSetDrive = true;
                            DriveVar = driveVal;
                            SettleDown();
                        }
                    }
                }
                BGeneral.RTSCombat(this, tank);
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
                var Builder = tank.GetComponent<BookmarkBuilder>();
                if (Builder.IsNotNull())
                    DestroyImmediate(Builder);
            }
            

        }
    }
}
