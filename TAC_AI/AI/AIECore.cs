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

        internal static HashSet<SceneryTypes> IndestructableScenery = new HashSet<SceneryTypes>
        {
            SceneryTypes.Pillar, SceneryTypes.ScrapPile, 
        };


        public static Event<Tank, string> AIMessageEvent = new Event<Tank, string>();

        private static List<TankAIHelper> AllHelpers = new List<TankAIHelper>();
        internal static int HelperCountNoCheck => AllHelpers.Count;
        internal static int HelperCountChecked => IterateAllHelpers().Count();
        internal static void AddHelper(TankAIHelper helper) => AllHelpers.Add(helper);
        internal static void DestroyAllHelpers()
        {
            foreach (var item in new List<TankAIHelper>(AllHelpers))
            {
                UnityEngine.Object.Destroy(item);
            }
            AllHelpers.Clear();
        }
        internal static IEnumerable<TankAIHelper> IterateAllHelpers()
        {
            foreach (var item in AllHelpers)
            {
                if (item != null && item.enabled)
                    yield return item;
            }
        }
        internal static IEnumerable<TankAIHelper> IterateAllHelpers(Func<TankAIHelper, bool> delegator)
        {
            foreach (var item in AllHelpers)
            {
                if (item != null && delegator(item))
                    yield return item;
            }
        }
        internal static List<Visible> Minables;
        internal static List<ModuleHarvestReciever> Depots;
        internal static List<ModuleHarvestReciever> BlockHandlers;
        internal static List<ModuleChargerTracker> Chargers;
        internal static HashSet<int> RetreatingTeams;
        public static bool PlayerIsInNonCombatZone => _playerIsInNonCombatZone;
        internal static bool _playerIsInNonCombatZone = false;
        internal static bool PlayerCombatLastState = false;
        //private static int lastTechCount = 0;

        // legdev
        internal static bool Feedback = false;// set this to true to get AI feedback testing
        /*
#if DEBUG
        internal static bool debugVisuals = true;// set this to true to get AI visual testing
#else
        internal static bool debugVisuals = false;// set this to true to get AI visual testing
#endif
        */

        public static Func<int, IEnumerable<Tank>> GetTeamTanks => TankAIManager.GetTeamTanks;
        public static Func<int, IEnumerable<Tank>> GetNonEnemyTanks => TankAIManager.GetNonEnemyTanks;
        public static Func<int, IEnumerable<Tank>> GetTargetTanks => TankAIManager.GetTargetTanks;


        // Mining
        public static bool FetchClosestChunkReceiver(Vector3 tankPos, float MaxScanRange, out IAIFollowable finalPos, out Tank theBase, int team, bool includeTradingStations = false)
        {
            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = MaxScanRange * MaxScanRange;// MAX SCAN RANGE
            foreach (ModuleHarvestReciever reciever in Depots)
            {
                if (!reciever.tank.boundsCentreWorldNoCheck.Approximately(tankPos, 1) && (reciever.tank.Team == team || 
                    (includeTradingStations && reciever.tank.Team == ManSpawn.NeutralTeam)))
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
                        //DebugTAC_AI.Log(KickStart.ModID + ":Skipped over inactive");
                        if (!trans.GetComponent<Damageable>().Invulnerable && 
                            !IndestructableScenery.Contains(res.GetSceneryType()))
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": Skipped over invincible");
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

        public static bool FetchClosestBlockReceiver(Vector3 tankPos, float MaxScanRange, out IAIFollowable finalPos, out Tank theBase, int team)
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
            Tank bestStep = null;
            ToFetch = null;
            try
            {
                foreach (var ally in AIEPathing.AllyList(helper.tank))
                {
                    if (ally.GetHelperInsured().CanCopyControls)
                    {
                        float temp = (ally.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                        if (distanceSqr > temp && temp > 1)
                        {
                            distanceSqr = temp;
                            bestStep = ally;
                        }
                    }
                }
                if (bestStep == null)
                    return false;
                ToFetch = bestStep.visible;
                //DebugTAC_AI.Log(KickStart.ModID + ":ClosestAllyProcess " + closestTank.name);
                return true;
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Crash on ClosestAllyProcess " + e);
            }
            return false;
        }


        // Charging
        // Charging
        public static bool ChargedChargerExists(Tank tank, float MaxScanRange, int team)
        {
            if (team == -2)
                team = Singleton.Manager<ManPlayer>.inst.PlayerTeam;
            Vector3 tankPos = tank.boundsCentreWorldNoCheck;

            float scanRange = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            foreach (ModuleChargerTracker charge in Chargers)
            {
                if (charge.tank != tank && charge.tank.Team == team && charge.CanTransferCharge(tank))
                {
                    float temp = (charge.trans.position - tankPos).sqrMagnitude;
                    if (scanRange > temp && temp > 1)
                        return true;
                }
            }
            return false;
        }
        public static bool FetchChargedChargers(Tank tank, float MaxScanRange, out IAIFollowable finalPos, out Tank theBase, int team)
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
                        finalPos = charge;
                    }
                }
            }
            return fired;
        }
        public static bool FetchLowestChargeAlly(Vector3 tankPos, TankAIHelper helper, out Visible toCharge)
        {
            // Finds the closest ally and outputs their respective distance as well as their being
            float Range = 62500;
            Tank bestStep = null;
            toCharge = null;
            try
            {
                foreach (var ally in AIEPathing.AllyList(helper.tank))
                {
                    float temp = (ally.boundsCentreWorldNoCheck - tankPos).sqrMagnitude;
                    TechEnergy.EnergyState eState = ally.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
                    bool hasCapacity = eState.storageTotal > 200;
                    bool needsCharge = (eState.storageTotal - eState.spareCapacity) / eState.storageTotal < AIGlobals.minimumChargeFractionToConsider;
                    if (hasCapacity && needsCharge)
                    {
                        if (Range > temp && temp > 1)
                        {
                            Range = temp;
                            bestStep = ally;
                        }
                    }
                }
                if (bestStep == null)
                    return false;
                toCharge = bestStep.visible;
                //DebugTAC_AI.Log(KickStart.ModID + ":ClosestAllyProcess " + closestTank.name);
                return bestStep != null;
            }
            catch //(Exception e)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Crash on ClosestAllyProcess " + e);
            }
            return false;
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
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + message);
            }
            return hasMessaged;
        }
        public static void AIMessage(Tank tech, string message)
        {
            AIMessageEvent.Send(tech, message);
            if (Feedback)
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + message);
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
                        AIWiki.hintNPTRetreat.Show();
                        foreach (Tank tech in TankAIManager.TeamActiveMobileTechs(Team))
                        {
                            WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                            AIGlobals.PopupColored("Fall back!", Team, worPos);
                        }
                    }
                }
                else
                {
                    if (RetreatingTeams.Remove(Team))
                    {
                        if (Sending && ManNetwork.IsNetworked)
                            NetworkHandler.TryBroadcastNewRetreatState(Team, false);
                        AIWiki.hintNPTRetreat.Show();
                        foreach (Tank tech in TankAIManager.TeamActiveMobileTechs(Team))
                        {
                            WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tech.visible);
                            AIGlobals.PopupColored("Engage!", Team, worPos);
                        }
                    }
                }
            }
            catch { DebugTAC_AI.Log(KickStart.ModID + ": TeamRetreat encountered an error, perhaps in Attract?"); }
        }
        internal static void ToggleTeamRetreat(int Team)
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
            BlockDetails BD = new BlockDetails(block.BlockType);
            return BD.IsOmniDirectional && !BD.HasWheels;
        }

        public static bool ShouldBeStationary(Tank tank, TankAIHelper helper)
        {
            if (helper.AnchorState == AIAnchorState.Unanchor)
                return false;
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
                helper.AnchorStatic();
                if (helper.DriverType != AIDriverType.AutoSet)
                    return helper.DriverType;
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

            bool canFloat = false;
            bool isFlying = false;
            bool isFlyingDirectionForwards = true;
            bool isOmniEngine = false;
            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            int FoilCount = 0;
            int MovingFoilCount = 0;

            int modBoostCount = 0;
            int modHoverCount = 0;
            int modGyroCount = 0;
            int modWheelCount = 0;
            int modAGCount = 0;
            int modGunCount = 0;
            int modDrillCount = 0;

            foreach (TankBlock bloc in BM.IterateBlocks())
            {
                BlockDetails BD = new BlockDetails(bloc.BlockType);
                if (BD.IsBasic)
                    continue;
                if (BD.DoesMovement)
                {
                    if (BD.HasWings)
                        modBoostCount++;
                    if (BD.HasHovers)
                        modHoverCount++;
                    if (BD.FloatsOnWater)
                        canFloat = true;
                    if (BD.IsGyro)
                        modGyroCount++;
                    if (BD.HasWheels)
                        modWheelCount++;
                    else if (BD.IsOmniDirectional)
                        isOmniEngine = true;
                    if (BD.HasAntiGravity)
                        modAGCount++;
                    if (BD.HasWings)
                    {
                        var aero = bloc.GetComponent<ModuleWing>();
                        if (aero)
                        {
                            //Get teh slowest spooling one
                            foreach (ModuleWing.Aerofoil Afoil in aero.m_Aerofoils)
                            {
                                if (Afoil.flapAngleRangeActual > 0 && Afoil.flapTurnSpeed > 0)
                                    MovingFoilCount++;
                                FoilCount++;
                            }
                        }
                    }
                    bool boosters = false;
                    if (BD.HasFans)
                    {
                        //Get the slowest spooling one
                        foreach (FanJet jet in bloc.transform.GetComponentsInChildren<FanJet>())
                        {
                            if ((float)RawTechBase.spinDat.GetValue(jet) <= 10)
                            {
                                biasDirection -= tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForward) * (float)RawTechBase.thrustRate.GetValue(jet);
                            }
                        }
                        boosters = true;
                    }
                    if (BD.HasBoosters)
                    {
                        //Get the slowest spooling one
                        foreach (BoosterJet boost in bloc.transform.GetComponentsInChildren<BoosterJet>())
                        {
                            //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                            boostBiasDirection -= tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalThrustDirection));
                        }
                        boosters = true;
                    }
                    if (boosters)
                        modBoostCount++;
                }
                if (BD.IsWeapon)
                {
                    if (bloc.GetComponent<ModuleWeaponGun>())
                        modGunCount++;
                    if (BD.IsMelee)
                        modDrillCount++;
                }
            }
            DebugTAC_AI.Info(KickStart.ModID + ": Tech " + tank.name + " Has bias of" + biasDirection + " and a boost bias of" + boostBiasDirection);

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
            //Debug.Info(KickStart.ModID + ": Tech " + tank.name + "  Has block count " + blocs.Count() + "  | " + modBoostCount + " | " + modAGCount);


            if (tank.IsAnchored)
            {
                return AIDriverType.Tank;
            }
            else if (BM.blockCount == 1)
            {
                if (isOmniEngine)
                    return AIDriverType.Astronaut;
                else if (isFlyingDirectionForwards && MovingFoilCount > 3)
                    return AIDriverType.Pilot;
                else if (!isFlyingDirectionForwards)
                    return AIDriverType.Pilot;
                else if (canFloat && modWheelCount == 0)
                    return AIDriverType.Sailor;
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

        /// <summary>
        /// This one is more expensive.  If you know if the Helper is player-controlled or not, use 
        ///  RequestFocusFirePlayer for players or RLoadedBases.RequestFocusFireNPT for Non-Player Techs
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="target"></param>
        /// <param name="priority"></param>
        public static void RequestFocusFire(Tank tank, Visible target, RequestSeverity priority)
        {
            if (target.IsNull() || tank.IsNull())
                return;
            if (tank.Team == ManPlayer.inst.PlayerTeam)
                RequestFocusFirePlayer(tank, target, priority);
            else
            {
                var mind = tank.GetComponent<EnemyMind>();
                if (mind)
                    RLoadedBases.RequestFocusFireNPTs(mind, target, priority);
            }
        }
        public static void RequestFocusFirePlayer(Tank tank, Visible target, RequestSeverity priority)
        {
            if (target.IsNull() || tank.IsNull())
                return;
            if (target.tank.IsNull())
                return;
            int Team = tank.Team;
            if (tank.IsAnchored)
                AIMessage(tank, "Player Base " + tank.name + " is under attack!  Concentrate all fire on " + target.tank.name + "!");
            else
                AIMessage(tank, tank.name + ": Requesting assistance!  Cover me!");
            if (!TankAIManager.targetingRequests.ContainsKey(Team))
                TankAIManager.targetingRequests.Add(Team, new KeyValuePair<RequestSeverity, Visible>(priority, target));
        }
    }
}
