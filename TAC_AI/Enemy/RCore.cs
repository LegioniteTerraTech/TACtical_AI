using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;
using TAC_AI.AI.Movement;
using TAC_AI.World;
#if !STEAM
using Control_Block;
#endif

namespace TAC_AI.AI.Enemy
{
    public static class RCore
    {
        /*
            RELIES ON EVERYTHING IN THE "AI" FOLDER TO FUNCTION PROPERLY!!!  
                [excluding the Designators in said folder]
        */

        internal static FieldInfo charge = typeof(ModuleShieldGenerator).GetField("m_EnergyDeficit", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge2 = typeof(ModuleShieldGenerator).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge3 = typeof(ModuleShieldGenerator).GetField("m_Shield", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo generator = typeof(ModuleEnergy).GetField("m_OutputConditions", BindingFlags.NonPublic | BindingFlags.Instance);

        // Main host of operations
        public static void BeEvil(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //Debug.Log("TACtical_AI: enemy AI active!");
            var Mind = thisInst.MovementController.EnemyMind;
            if (Mind.IsNull())
            {
                Mind = tank.GetComponent<EnemyMind>();
                DebugTAC_AI.Log("TACtical_AI: Updating MovementController for " + tank.name);
                thisInst.MovementController.UpdateEnemyMind(Mind);
                //RandomizeBrain(thisInst, tank);
                //return;
            }
            RunEvilOperations(Mind, thisInst, tank);
            ScarePlayer(Mind, thisInst, tank);
        }
        public static void ScarePlayer(EnemyMind mind, AIECore.TankAIHelper thisInst, Tank tank)
        {
            //Debug.Log("TACtical_AI: enemy AI active!");
            try
            {
                if (thisInst.AttackEnemy)
                {
                    var tonk = thisInst.lastEnemy;
                    if (tonk?.tank)
                    {
                        bool player = tonk.tank.PlayerFocused;
                        if (player && Singleton.playerTank)
                        {
                            if (Mode<ModeMain>.inst != null)
                                Mode<ModeMain>.inst.SetPlayerInDanger(true, true);
                            Singleton.Manager<ManMusic>.inst.SetDanger(ManMusic.DangerContext.Circumstance.Enemy, tank, tonk.tank);
                        }
                        else if (ManPlayerRTS.PlayerIsInRTS)
                        {
                            if (tonk.tank.Team == ManPlayer.inst.PlayerTeam)
                            {
                                if (Mode<ModeMain>.inst != null)
                                    Mode<ModeMain>.inst.SetPlayerInDanger(true, true);
                                Singleton.Manager<ManMusic>.inst.SetDanger(ManMusic.DangerContext.Circumstance.Enemy, tank, tonk.tank);
                            }
                        }
                    }
                }
            }
            catch (Exception e){ DebugTAC_AI.Log("error " + e); }
        }

        // Begin the AI tree
        public static void RunLightEvilOp(AIECore.TankAIHelper thisInst, Tank tank)
        {
            var Mind = thisInst.MovementController.EnemyMind;
            if (Mind.IsNull())
            {
                Mind = tank.GetComponent<EnemyMind>();
                DebugTAC_AI.Log("TACtical_AI: Updating MovementController for " + tank.name);
                thisInst.MovementController.UpdateEnemyMind(Mind);
                //RandomizeBrain(thisInst, tank);
                //return;
            }
            if (Mind.StartedAnchored)
            {
                Mind.EvilCommander = EnemyHandling.Stationary;// NO MOVE FOOL
                if (!tank.IsAnchored && !Mind.Hurt)
                {
                    if (thisInst.anchorAttempts < AIGlobals.NPTAnchorAttempts)
                    {
                        //Debug.Log("TACtical_AI: Trying to anchor " + tank.name);
                        thisInst.TryReallyAnchor();
                        thisInst.anchorAttempts++;
                        if (tank.IsAnchored)
                            thisInst.anchorAttempts = 0;
                    }
                }
            }
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
            {
                if ((Mind.AllowRepairsOnFly || Mind.StartedAnchored || Mind.BuildAssist) && thisInst.TechMemor)
                {
                    bool venPower = false;
                    if (Mind.MainFaction == FactionTypesExt.VEN) venPower = true;
                    RRepair.EnemyRepairStepper(thisInst, tank, Mind, venPower);// longer while fighting
                }
            }
        }
        public static void RunEvilOperations(EnemyMind Mind, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (Mind.StartedAnchored)
            {
                Mind.EvilCommander = EnemyHandling.Stationary;// NO MOVE FOOL
                if (!tank.IsAnchored && !Mind.Hurt)
                {
                    if (thisInst.anchorAttempts < AIGlobals.NPTAnchorAttempts)
                    {
                        //Debug.Log("TACtical_AI: Trying to anchor " + tank.name);
                        thisInst.TryReallyAnchor();
                        thisInst.anchorAttempts++;
                        if (tank.IsAnchored)
                            thisInst.anchorAttempts = 0;
                    }
                }
            }


            RBolts.ManageBolts(thisInst, tank, Mind);
            TestShouldCommitDie(tank, Mind);
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
            {
                if ((Mind.AllowRepairsOnFly || Mind.StartedAnchored || Mind.BuildAssist) && thisInst.TechMemor)
                {
                    bool venPower = false;
                    if (Mind.MainFaction == FactionTypesExt.VEN) venPower = true;
                    RRepair.EnemyRepairStepper(thisInst, tank, Mind, venPower);// longer while fighting
                }
            }
            if (Mind.Provoked <= 0)
            {
                if (thisInst.lastEnemy)
                {
                    if (!Mind.InRangeOfTarget())
                    {
                        Mind.EndAggro();
                    }
                }
                else
                    Mind.EndAggro();
                Mind.Provoked = 0;
            }
            else
                Mind.Provoked -= KickStart.AIClockPeriod;


            // Attack handling
            switch (Mind.CommanderAlignment)
            {
                case EnemyStanding.Enemy:
                    BeHostile(thisInst, tank);
                    break;
                case EnemyStanding.SubNeutral:
                    BeSubNeutral(thisInst, tank);
                    break;
                case EnemyStanding.Neutral:
                    BeNeutral(thisInst, tank);
                    break;
                case EnemyStanding.Friendly:
                    BeFriendly(thisInst, tank);
                    break;
            }

            //CommanderMind is handled in each seperate class
            if (AIGlobals.IsBaseTeam(tank.Team))
            {
                ProccessIfRetreat(thisInst, tank, Mind);
            }
            Mind.EnemyOpsController.Execute();
        }
        public static Vector3 GetTargetCoordinates(Tank tank, Visible target, EnemyMind mind)
        {
            if (mind.CommanderSmarts >= EnemySmarts.Smrt)   // Rough Target leading
            {
                if (target.tank.rbody.IsNull())
                    return target.tank.boundsCentreWorldNoCheck;
                else
                    return target.tank.rbody.velocity + target.tank.boundsCentreWorldNoCheck;
            }
            else
                return target.tank.boundsCentreWorldNoCheck;
        }

        /// <summary>
        /// Will populate if there's desync issues between clients
        /// </summary>
        /// <param name="Mind"></param>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void RunEvilOperationsNetworked(EnemyMind Mind, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (Mind.StartedAnchored)
            {
                Mind.EvilCommander = EnemyHandling.Stationary;// NO MOVE FOOL
                if (!tank.IsAnchored && !Mind.Hurt)
                {
                    if (thisInst.anchorAttempts < AIGlobals.NPTAnchorAttempts)
                    {
                        //Debug.Log("TACtical_AI: Trying to anchor " + tank.name);
                        thisInst.TryReallyAnchor();
                        thisInst.anchorAttempts++;
                        if (tank.IsAnchored)
                            thisInst.anchorAttempts = 0;
                    }
                }
            }


            RBolts.ManageBolts(thisInst, tank, Mind);
            TestShouldCommitDie(tank, Mind);
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
            {
                if ((Mind.AllowRepairsOnFly || Mind.StartedAnchored || Mind.BuildAssist) && thisInst.TechMemor)
                {
                    bool venPower = false;
                    if (Mind.MainFaction == FactionTypesExt.VEN) venPower = true;
                    RRepair.EnemyRepairStepper(thisInst, tank, Mind, venPower);// longer while fighting
                }
            }
            if (Mind.Provoked <= 0)
            {
                if (thisInst.lastEnemy)
                {
                    if (!Mind.InRangeOfTarget())
                    {
                        Mind.EndAggro();
                    }
                }
                else
                    Mind.EndAggro();
                Mind.Provoked = 0;
            }
            else
                Mind.Provoked -= KickStart.AIClockPeriod;


            // Attack handling
            switch (Mind.CommanderAlignment)
            {
                case EnemyStanding.Enemy:
                    BeHostile(thisInst, tank);
                    break;
                case EnemyStanding.SubNeutral:
                    BeSubNeutral(thisInst, tank);
                    break;
                case EnemyStanding.Neutral:
                    BeNeutral(thisInst, tank);
                    break;
                case EnemyStanding.Friendly:
                    BeFriendly(thisInst, tank);
                    break;
            }

            //CommanderMind is handled in each seperate class
            if (AIGlobals.IsBaseTeam(tank.Team))
            {

            }
            Mind.EnemyOpsController.Execute();
        }


        // AI Morals
        public static void BeHostile(AIECore.TankAIHelper thisInst, Tank tank)
        {
            var Mind = thisInst.MovementController.EnemyMind;
            CombatChecking(thisInst, tank, Mind);
        }
        public static void BeSubNeutral(AIECore.TankAIHelper thisInst, Tank tank)
        {
            var Mind = thisInst.MovementController.EnemyMind;
            if (Mind.Hurt && Mind.Provoked > 0)
            {   // If we were hit, then we fight back the attacker
                if (thisInst.lastEnemy?.tank)
                {
                    int teamAttacker = thisInst.lastEnemy.tank.Team;
                    if (AIGlobals.IsBaseTeam(teamAttacker) || teamAttacker == ManPlayer.inst.PlayerTeam)
                    {
                        ManEnemyWorld.ChangeTeam(tank.Team, AIGlobals.GetRandomEnemyBaseTeam());
                        RandomSetMindAttack(Mind, tank);
                        Mind.CommanderAlignment = EnemyStanding.Enemy;
                        return;
                    }
                }
            }
            RGeneral.Monitor(thisInst, tank, Mind);
        }
        public static void BeNeutral(AIECore.TankAIHelper thisInst, Tank tank)
        {
            var Mind = thisInst.MovementController.EnemyMind;
            if (Mind.Hurt && thisInst.PendingDamageCheck && Mind.Provoked > 0)
            {   // If we were hit & lost blocks, then we fight back the attacker
                if (thisInst.lastEnemy?.tank)
                {
                    if (thisInst.lastEnemy.tank.Team == ManPlayer.inst.PlayerTeam)
                    {
                        ManEnemyWorld.ChangeTeam(tank.Team, AIGlobals.GetRandomEnemyBaseTeam());
                        RandomSetMindAttack(Mind, tank);
                        Mind.CommanderAlignment = EnemyStanding.Enemy;
                        return;
                    }
                    else if (AIGlobals.IsBaseTeam(thisInst.lastEnemy.tank.Team))
                    {
                        ManEnemyWorld.ChangeTeam(tank.Team, AIGlobals.GetRandomAllyBaseTeam());
                        RandomSetMindAttack(Mind, tank);
                        Mind.CommanderAlignment = EnemyStanding.Enemy;
                        return;
                    }
                }
            }
            thisInst.lastEnemy = null;
        }
        public static void BeFriendly(AIECore.TankAIHelper thisInst, Tank tank)
        {
            var Mind = thisInst.MovementController.EnemyMind;
            // Can't really damage an ally
            CombatChecking(thisInst, tank, Mind);
        }

        public static void CombatChecking(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind Mind)
        {
            switch (Mind.EvilCommander)
            {
                case EnemyHandling.Airplane:
                    EnemyOperations.RAircraft.EnemyDogfighting(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Stationary:
                    RGeneral.BaseAttack(thisInst, tank, Mind);
                    break;
                default:
                    switch (Mind.CommanderAttack)
                    {
                        case EnemyAttack.Coward:
                            RGeneral.SelfDefense(thisInst, tank, Mind);
                            break;
                        case EnemyAttack.Spyper:
                            RGeneral.AimAttack(thisInst, tank, Mind);
                            break;
                        case EnemyAttack.Grudge:
                            RGeneral.AidAttack(thisInst, tank, Mind);
                            //RGeneral.HoldGrudge(thisInst, tank, Mind); - now handled within FindEnemy
                            break;
                        default:
                            RGeneral.AidAttack(thisInst, tank, Mind);
                            break;
                    }
                    break;
            }
        }

        public static bool ProccessIfRetreat(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind Mind)
        {
            RBases.EnemyBaseFunder funds = RBases.GetTeamFunder(tank.Team);
            if (thisInst.Retreat && funds)
            {
                BGeneral.ResetValues(thisInst);
                thisInst.theBase = funds.Tank;
                thisInst.lastBaseExtremes = funds.Tank.visible.GetCheapBounds();

                Vector3 veloFlat = Vector3.zero;
                if ((bool)tank.rbody)   // So that drifting is minimized
                {
                    veloFlat = tank.rbody.velocity;
                    veloFlat.y = 0;
                }
                float dist = (thisInst.theBase.boundsCentreWorldNoCheck + veloFlat - tank.boundsCentreWorldNoCheck).magnitude;
                bool messaged = true;
                thisInst.lastDestination = funds.Tank.boundsCentreWorldNoCheck;
                StopByBase(thisInst, tank, dist, ref messaged);
                
                return true;
            }
            else
            {
                EnemyPresence EP = ManEnemyWorld.GetTeam(tank.Team);
                if (EP != null)
                {
                    EnemyBaseUnit EBU = UnloadedBases.GetTeamFunder(EP);
                    if (EBU != null)
                    {
                        BGeneral.ResetValues(thisInst);
                        thisInst.Steer = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        thisInst.lastDestination = EBU.PosScene;
                        // yes we will drive off-scene to retreat home
                        return true;
                    }
                }
            }
            return false;
        }
        public static void StopByBase(AIECore.TankAIHelper thisInst, Tank tank, float dist, ref bool hasMessaged)
        {
            if (thisInst.theBase == null)
                return; // There's no base!
            float girth = thisInst.lastBaseExtremes + thisInst.lastTechExtents;
            if (dist < girth + 3)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving room to base... |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
                thisInst.AvoidStuff = false;
                thisInst.AdviseAway = true;
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
                thisInst.SettleDown();
            }
            else if (dist < girth + 7)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
                thisInst.AvoidStuff = false;
                thisInst.Yield = true;
                thisInst.PivotOnly = true;
                thisInst.SettleDown();
            }
        }



        // AI SETUP
        public static void RandomizeBrain(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //Debug.Log("TACtical_AI: offset " + tank.boundsCentreWorldNoCheck);
            var toSet = tank.gameObject.GetComponent<EnemyMind>();
            if (!toSet)
            {
                toSet = tank.gameObject.AddComponent<EnemyMind>();
                toSet.Initiate();
            }
            thisInst.lastPlayer = null;

            thisInst.ResetToDefaultAIController();

            toSet.sceneStationaryPos = tank.boundsCentreWorldNoCheck;
            toSet.Refresh();
            toSet.Range = 100;
            try
            {
                toSet.Range = (int)thisInst.DetectionRange + 50;
            }
            catch { }

            bool isMissionTech = RMission.SetupMissionAI(thisInst, tank, toSet);
            if (isMissionTech)
            {
                if (!(bool)tank)
                    return;
                FinalCleanup(thisInst, toSet, tank);
                if (ManNetwork.IsNetworked && ManNetwork.IsHost)
                {
                    NetworkHandler.TryBroadcastNewEnemyState(tank.netTech.netId.Value, toSet.CommanderSmarts);
                }
                return;
            }


            //add Smartness
            AutoSetIntelligence(toSet, tank);

            //Determine driving method
            BlockSetEnemyHandling(tank, toSet);
            bool setEnemy = SetSmartAIStats(thisInst, tank, toSet);
            if (!setEnemy)
            {
                RandomSetMindAttack(toSet, tank);
            }
            FinalCleanup(thisInst, toSet, tank);
            if (tank.Anchors.NumAnchored > 0 || tank.GetComponent<RequestAnchored>())
            {
                toSet.StartedAnchored = true;
                toSet.EvilCommander = EnemyHandling.Stationary;
            }
            if (ManNetwork.IsNetworked && ManNetwork.IsHost)
            {
                NetworkHandler.TryBroadcastNewEnemyState(tank.netTech.netId.Value, toSet.CommanderSmarts);
            }
        }


        public static bool IsHarvester(BlockManager BM)
        {
            ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
            foreach (var item in BM.IterateBlockComponents<ModuleItemHolder>())
            {
                if (item.IsFlag(ModuleItemHolder.Flags.Collector) && item.Acceptance == flag)
                    return true;
            }
            return false;
        }


        // Setup functions
        public static void AutoSetIntelligence(EnemyMind toSet, Tank tank)
        {
            //add Smartness
            int randomNum = UnityEngine.Random.Range(KickStart.LowerDifficulty, KickStart.UpperDifficulty);
            if (randomNum < 35)
                toSet.CommanderSmarts = EnemySmarts.Default;
            else if (randomNum < 60)
                toSet.CommanderSmarts = EnemySmarts.Mild;
            else if (randomNum < 80)
                toSet.CommanderSmarts = EnemySmarts.Meh;
            else if (randomNum < 92)
                toSet.CommanderSmarts = EnemySmarts.Smrt;
            else
                toSet.CommanderSmarts = EnemySmarts.IntAIligent;
            if (randomNum > 92)
            {
                toSet.AllowRepairsOnFly = true;
                toSet.InvertBullyPriority = true;
            }
        }
        public static bool SetSmartAIStats(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind toSet)
        {
            bool fired = false;
            var BM = tank.blockman;

            // CHECK before burning processing
            if (AIGlobals.IsAttract)
            {
                return false;
            }
            else if (Singleton.Manager<ManGameMode>.inst.IsCurrent<ModeSumo>())
            {
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is a sumo brawler");
                toSet.CommanderSmarts = EnemySmarts.Default;
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.EvilCommander = EnemyHandling.Wheeled;
                toSet.CommanderAttack = EnemyAttack.Grudge;
                return true;
            }

            //Determine Attitude
            bool playerIsSmall = false;
            if (Singleton.playerTank)
                playerIsSmall = Singleton.playerTank.blockman.blockCount < BM.blockCount + 5;
            if (!playerIsSmall && BM.blockCount + BM.IterateBlockComponents<ModuleDrill>().Count() <= BM.IterateBlockComponents<ModuleTechController>().Count())
            {   // Unarmed - Runner
                toSet.CommanderMind = EnemyAttitude.Default;
                toSet.CommanderAttack = EnemyAttack.Coward;
            }
            else if (BM.blockCount > AIGlobals.BossTechSize && KickStart.MaxEnemyHQLimit > RBases.GetAllTeamsEnemyHQCount())
            {   // Boss
                toSet.InvertBullyPriority = true;
                toSet.CommanderMind = EnemyAttitude.Boss;
                toSet.CommanderAttack = EnemyAttack.Bully;
            }
            else if (RWeapSetup.HasArtilleryWeapon(BM))
            {   // Artillery
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Spyper;
                fired = true;
            }
            else if (IsHarvester(BM) || toSet.MainFaction == FactionTypesExt.GC)
            {   // Miner
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        if (tank.IsPopulation)
                            toSet.CommanderMind = EnemyAttitude.NPCBaseHost;
                        else
                            toSet.CommanderMind = EnemyAttitude.Miner;
                        toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank, toSet);
                        toSet.Range = 64;
                        break;
                    default:
                        if (IsHarvester(BM))
                        {
                            if (tank.IsPopulation)
                            toSet.CommanderMind = EnemyAttitude.NPCBaseHost;
                            else
                                toSet.CommanderMind = EnemyAttitude.Miner;
                        }
                        else
                            toSet.CommanderMind = EnemyAttitude.Junker;
                        //toSet.CommanderAttack = EnemyAttack.Bully;
                        toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank, toSet);
                        toSet.Range = 64;
                        toSet.InvertBullyPriority = true;
                        break;
                }
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleWeapon>().Count() > AIGlobals.HomingWeaponCount)
            {   // Over-armed
                toSet.CommanderMind = EnemyAttitude.Homing;
                //toSet.CommanderAttack = EnemyAttack.Bully;
                toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank, toSet);
                fired = true;
            }
            else if (toSet.MainFaction == FactionTypesExt.VEN)
            {   // Ven
                //toSet.CommanderMind = EnemyAttitude.Default; 
                toSet.CommanderAttack = EnemyAttack.Circle;
                toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank, toSet);
                fired = true;
            }
            else if (toSet.MainFaction == FactionTypesExt.HE)
            {   // Assault
                toSet.CommanderMind = EnemyAttitude.Homing;
                //toSet.CommanderAttack = EnemyAttack.Grudge;
                toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank, toSet);
                fired = true;
            }
            if (BM.blockCount >= AIGlobals.LethalTechSize)
            {   // DEATH TO ALL
                if (!SpecialAISpawner.Eradicators.Contains(tank))
                    SpecialAISpawner.Eradicators.Add(tank);
            }
            return fired;
        }
        public static void BlockSetEnemyHandling(Tank tank, EnemyMind toSet, bool ForceAllBubblesUp = false)
        {
            var BM = tank.blockman;

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
            /*
            int modBoostCount = BM.IterateBlockComponents<ModuleBooster>().Count();
            int modHoverCount = BM.IterateBlockComponents<ModuleHover>().Count();
            int modGyroCount = BM.IterateBlockComponents<ModuleGyro>().Count();
            */

            // We have to do it this way since modded blocks don't work well with the above
            List<TankBlock> blocs = BM.IterateBlocks().ToList();
            int modControlCount = 0;
            int modBoostCount = 0;
            int modHoverCount = 0;
            int modGyroCount = 0;
            int modWheelCount = 0;
            int modAGCount = 0;
            int modGunCount = 0;
            int modDrillCount = 0;
            int modTeslaCount = 0;

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
                var buubles = bloc.GetComponent<ModuleShieldGenerator>();
                if (ForceAllBubblesUp && buubles)
                {
                    charge.SetValue(buubles, 0);
                    charge2.SetValue(buubles, 2);
                    BubbleShield shield = (BubbleShield)charge3.GetValue(buubles);
                    shield.SetTargetScale(buubles.m_Radius);
                }

                if (bloc.GetComponent<ModuleEnergy>())
                {
                    ModuleEnergy.OutputConditionFlags flagG = (ModuleEnergy.OutputConditionFlags)generator.GetValue(bloc.GetComponent<ModuleEnergy>());
                    if (flagG.HasFlag(ModuleEnergy.OutputConditionFlags.Anchored) && flagG.HasFlag(ModuleEnergy.OutputConditionFlags.DayTime))
                        toSet.SolarsAvail = true;
                }

                if (bloc.GetComponent<ModulePacemaker>())
                    tank.Holders.SetHeartbeatSpeed(TechHolders.HeartbeatSpeed.Fast);

                if (bloc.GetComponent<ModuleTechController>())
                {
                    modControlCount++;
                }
                else
                {
                    if (bloc.GetComponent<ModuleWeaponGun>())
                        modGunCount++;
                    if (bloc.GetComponent<ModuleDrill>() || bloc.GetComponent<ModuleWeaponFlamethrower>())
                        modDrillCount++;
                    if (bloc.GetComponent<ModuleWeaponTeslaCoil>())
                        modTeslaCount++;
                }

                CheckAndHandleControlBlocks(toSet, bloc);
            }
            DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + "  Has block count " + blocs.Count() + "  | " + modBoostCount + " | " + modAGCount);


            if (tank.IsAnchored)
            {
                toSet.StartedAnchored = true;
                toSet.EvilCommander = EnemyHandling.Stationary;
                toSet.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (MovingFoilCount > 4 && isFlying && isFlyingDirectionForwards)
            {
                if ((modHoverCount > 2  && modWheelCount > 2) || modAGCount > 0)
                {
                    toSet.EvilCommander = EnemyHandling.Starship;
                }
                else
                    toSet.EvilCommander = EnemyHandling.Airplane;
            }
            else if ((modGyroCount > 0 || modWheelCount < modBoostCount) && isFlying && !isFlyingDirectionForwards)
            {
                if ((modHoverCount > 2 && modWheelCount > 2) || modAGCount > 0)
                {
                    toSet.EvilCommander = EnemyHandling.Starship;
                }
                else
                    toSet.EvilCommander = EnemyHandling.Chopper;
            }
            else if (modBoostCount > 2 &&  modAGCount > 0)
            {
                toSet.EvilCommander = EnemyHandling.Starship;
            }
            else if (KickStart.isWaterModPresent && modGyroCount > 0 && modBoostCount > 0 && modWheelCount < 4 + FoilCount)
            {
                toSet.EvilCommander = EnemyHandling.Naval;
            }
            else if (modBoostCount > 2 && modHoverCount > 2)
            {
                toSet.EvilCommander = EnemyHandling.Starship;
            }
            else if (modGunCount < 1 && modDrillCount < 1 && modBoostCount > 0)
            {
                toSet.EvilCommander = EnemyHandling.SuicideMissile;
            }
            else
                toSet.EvilCommander = EnemyHandling.Wheeled;

            if (modDrillCount + (modTeslaCount * 25) > modGunCount|| toSet.MainFaction == FactionTypesExt.GC)
                toSet.LikelyMelee = true;

            if (modGunCount > 48 || modHoverCount > 18)
            {   // DEATH TO ALL
                if (!SpecialAISpawner.Eradicators.Contains(tank))
                    SpecialAISpawner.Eradicators.Add(tank);
            }
        }
        public static void RandomSetMindAttack(EnemyMind toSet, Tank tank)
        {
            //add Attitude
            if (IsUnarmed(tank))
            {
                toSet.CommanderAttack = EnemyAttack.Coward;
                toSet.CommanderMind = EnemyAttitude.Default;
                toSet.CommanderAlignment = EnemyStanding.SubNeutral;
            }
            else
            {
                int randomNum2 = UnityEngine.Random.Range(0, 9);
                switch (randomNum2)
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                        toSet.CommanderMind = EnemyAttitude.Default;
                        break;
                    case 4:
                    case 5:
                        toSet.CommanderMind = EnemyAttitude.Junker;
                        break;
                    case 6:
                    case 7:
                        toSet.CommanderMind = EnemyAttitude.Homing;
                        break;
                    case 8:
                    case 9:
                        if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() > 0 && RawTechLoader.CanBeMiner(toSet))
                            toSet.CommanderMind = EnemyAttitude.Miner;
                        else
                            toSet.CommanderMind = EnemyAttitude.Default;
                        break;
                }
            }
            //add Attack
            toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank, toSet);
            /*
            int randomNum3 = UnityEngine.Random.Range(1, 6);
            switch (randomNum3)
            {
                case 1:
                    toSet.CommanderAttack = EnemyAttack.Circle;
                    break;
                case 2:
                    toSet.CommanderAttack = EnemyAttack.Grudge;
                    break;
                case 3:
                    toSet.CommanderAttack = EnemyAttack.Coward;
                    break;
                case 4:
                    toSet.CommanderAttack = EnemyAttack.Bully;
                    break;
                case 5:
                    toSet.CommanderAttack = EnemyAttack.Pesterer;
                    break;
                case 6:
                    if (toSet.LikelyMelee)
                    {
                        toSet.CommanderAttack = EnemyAttack.Bully;
                        toSet.InvertBullyPriority = true;
                    }
                    toSet.CommanderAttack = EnemyAttack.Spyper;
                    break;
            }*/
        }
        public static void FinalCleanup(AIECore.TankAIHelper thisInst, EnemyMind toSet, Tank tank)
        {
            if (toSet.CommanderSmarts > EnemySmarts.Meh)
            {
                if (thisInst.TechMemor.IsNull())
                {
                    thisInst.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                    toSet.TechMemor.Initiate();
                    DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + " Setup for DesignMemory (FinalCleanup)");
                }
                toSet.CommanderBolts = EnemyBolts.AtFullOnAggro;// allow base function
            }
            thisInst.SetupMovementAIController();

            bool isBaseMaker = toSet.CommanderMind == EnemyAttitude.NPCBaseHost || toSet.CommanderMind == EnemyAttitude.Boss;
            if (toSet.CommanderSmarts == EnemySmarts.Default && !isBaseMaker && toSet.EvilCommander == EnemyHandling.Wheeled)
            {
                thisInst.Hibernate = true;// enable the default AI
                switch (toSet.CommanderMind)
                {
                    case EnemyAttitude.Miner:
                    case EnemyAttitude.Junker:
                        break;
                }
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  Default enemy with Default everything");
                if (DebugRawTechSpawner.ShowDebugFeedBack)
                {
                    if (AIGlobals.IsNeutralBaseTeam(tank.Team))
                        AIGlobals.PopupNeutralInfo(toSet.EvilCommander.ToString(), WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up * thisInst.lastTechExtents)));
                    else if (AIGlobals.IsFriendlyBaseTeam(tank.Team))
                        AIGlobals.PopupAllyInfo(toSet.EvilCommander.ToString(), WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up * thisInst.lastTechExtents)));
                    else
                        AIGlobals.PopupEnemyInfo(toSet.EvilCommander.ToString(), WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up * thisInst.lastTechExtents)));
                }
                return;
            }


            if (toSet.CommanderAttack == EnemyAttack.Grudge)
                toSet.FindEnemy();

            // now handle base spawning
            if (AIGlobals.IsAttract)
            {
                if (KickStart.SpecialAttractNum == AttractType.Harvester)
                {
                    toSet.CommanderSmarts = EnemySmarts.IntAIligent;
                    if (toSet.StartedAnchored)
                    {
                        tank.FixupAnchors(true);
                        toSet.CommanderMind = EnemyAttitude.Default;
                        toSet.EvilCommander = EnemyHandling.Stationary;
                        toSet.CommanderBolts = EnemyBolts.MissionTrigger;
                        if (!tank.IsAnchored && tank.Anchors.NumPossibleAnchors > 0)
                        {
                            thisInst.TryReallyAnchor();
                        }
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is a base Tech");
                    }
                    else
                    {
                        toSet.CommanderMind = EnemyAttitude.Miner;
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is a harvester Tech");
                    }
                }
                else
                {
                    if (toSet.StartedAnchored)
                    {
                        RBases.SetupBaseAI(thisInst, tank, toSet);
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is is a base Tech");
                    }
                    if (toSet.EvilCommander != EnemyHandling.Wheeled)
                        toSet.CommanderAttack = EnemyAttack.Grudge;
                    if (toSet.CommanderAttack == EnemyAttack.Coward)
                        toSet.CommanderAttack = EnemyAttack.Circle;
                    if (toSet.CommanderAttack == EnemyAttack.Spyper)
                        toSet.CommanderAttack = EnemyAttack.Grudge;
                    if (toSet.CommanderMind == EnemyAttitude.Miner)
                        toSet.CommanderMind = EnemyAttitude.Homing;
                }
            }
            else
            {
                CheckShouldMakeBase(thisInst, toSet, tank);

                int Team = tank.Team;
                if (AIGlobals.IsEnemyBaseTeam(Team))
                {
                    toSet.CommanderAlignment = EnemyStanding.Enemy;
                }
                else if (AIGlobals.IsSubNeutralBaseTeam(Team))
                {
                    toSet.CommanderAlignment = EnemyStanding.SubNeutral;
                }
                else if (AIGlobals.IsNeutralBaseTeam(Team))
                {
                    toSet.CommanderAlignment = EnemyStanding.Neutral;
                }
                else
                {
                    toSet.CommanderAlignment = EnemyStanding.Friendly;
                }


                if (RawTechLoader.ShouldDetonateBoltsNow(toSet) && tank.FirstUpdateAfterSpawn)
                {
                    RBolts.BlowBolts(tank, toSet);
                }
                thisInst.SuppressFiring(!toSet.AttackAny);
                thisInst.FullMelee = toSet.LikelyMelee;
            }
            if (DebugRawTechSpawner.ShowDebugFeedBack)
            {
                if (AIGlobals.IsNeutralBaseTeam(tank.Team))
                    AIGlobals.PopupNeutralInfo(toSet.EvilCommander.ToString(), WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up * thisInst.lastTechExtents)));
                else if (AIGlobals.IsFriendlyBaseTeam(tank.Team))
                    AIGlobals.PopupAllyInfo(toSet.EvilCommander.ToString(), WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up * thisInst.lastTechExtents)));
                else
                    AIGlobals.PopupEnemyInfo(toSet.EvilCommander.ToString(), WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up * thisInst.lastTechExtents)));
            }
        }
        public static void CheckShouldMakeBase(AIECore.TankAIHelper thisInst, EnemyMind toSet, Tank tank)
        {
            switch (toSet.CommanderMind)
            {
                case EnemyAttitude.NPCBaseHost:
                    toSet.Range = AIGlobals.BaseFounderRange;
                    if (!tank.name.Contains('Ω'))
                        tank.SetName(tank.name + " Ω");
                    toSet.CommanderMind = EnemyAttitude.NPCBaseHost;
                    if (!AIGlobals.IsBaseTeam(tank.Team))
                    {
                        if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                            RawTechLoader.TryStartBase(tank, thisInst, BasePurpose.HarvestingNoHQ);
                        else
                            RawTechLoader.TryStartBase(tank, thisInst, BasePurpose.TechProduction);
                    }
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is a base hosting tech!!  " + toSet.EvilCommander.ToString() + " based " + toSet.CommanderAlignment.ToString() + " with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                    break;
                case EnemyAttitude.Boss:
                    if (!tank.name.Contains('⦲'))
                        tank.SetName(tank.name + " ⦲");
                    if (!AIGlobals.IsBaseTeam(tank.Team))
                        RawTechLoader.TryStartBase(tank, thisInst, BasePurpose.Headquarters);
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is a base boss with dangerous potential!  " + toSet.EvilCommander.ToString() + " based " + toSet.CommanderAlignment.ToString() + " with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                    break;
                case EnemyAttitude.Invader:
                    RawTechLoader.TryStartBase(tank, thisInst, BasePurpose.AnyNonHQ);
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is an invader looking to take over your world!  " + toSet.EvilCommander.ToString() + " based " + toSet.CommanderAlignment.ToString() + " with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                    break;
                case EnemyAttitude.Miner:
                    if (toSet.CommanderAttack == EnemyAttack.Circle)// Circle breaks the harvester AI in some attack cases
                    {
                        switch (toSet.EvilCommander)
                        {
                            case EnemyHandling.Naval:
                                toSet.CommanderAttack = EnemyAttack.Spyper;
                                break;
                            case EnemyHandling.Starship:
                            case EnemyHandling.Airplane:
                            case EnemyHandling.Chopper:
                                toSet.CommanderAttack = EnemyAttack.Pesterer;
                                break;
                            default:
                                toSet.CommanderAttack = EnemyAttack.Coward;
                                break;
                        }
                    }
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is a harvester!  " + toSet.EvilCommander.ToString() + " based " + toSet.CommanderAlignment.ToString() + " with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                    break;
                default:
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  " + toSet.EvilCommander.ToString() + " based " + toSet.CommanderAlignment.ToString() + " with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                    break;
            }
        }
        public static void SetFromScheme(EnemyMind toSet, Tank tank)
        {
            try
            {
                ControlSchemeCategory Schemer = tank.control.ActiveScheme.Category;
                switch (Schemer)
                {
                    case ControlSchemeCategory.Car:
                        toSet.EvilCommander = EnemyHandling.Wheeled;
                        break;
                    case ControlSchemeCategory.Aeroplane:
                        toSet.EvilCommander = EnemyHandling.Airplane;
                        break;
                    case ControlSchemeCategory.Helicopter:
                        toSet.EvilCommander = EnemyHandling.Chopper;
                        break;
                    case ControlSchemeCategory.AntiGrav:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Rocket:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Hovercraft:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    default:
                        string name = tank.control.ActiveScheme.CustomName;
                        if (name == "Ship" || name == "ship" || name == "Naval" || name == "naval" || name == "Boat" || name == "boat")
                        {
                            toSet.EvilCommander = EnemyHandling.Naval;
                        }
                        //Else we just default to Wheeled
                        break;
                }
            }
            catch { }//some population techs are devoid of schemes
        }

        // etc
        public static EnemyHandling EnemyHandlingDetermine(Tank tank)
        {
            var BM = tank.blockman;

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
                return EnemyHandling.Stationary;
            }
            else if ((modHoverCount > 3) || (modBoostCount > 2 && (modHoverCount > 2 || modAGCount > 0)))
            {
                return EnemyHandling.Starship;
            }
            else if (MovingFoilCount > 4 && isFlying && isFlyingDirectionForwards)
            {
                return EnemyHandling.Airplane;
            }
            else if (modGyroCount > 0 && isFlying && !isFlyingDirectionForwards)
            {
                return EnemyHandling.Chopper;
            }
            else if (KickStart.isWaterModPresent && FoilCount > 0 && modGyroCount > 0 && modBoostCount > 0 && (modWheelCount < 4 || modHoverCount > 1))
            {
                return EnemyHandling.Naval;
            }
            else if (modGunCount < 2 && modDrillCount < 2 && modBoostCount > 0)
            {
                return EnemyHandling.SuicideMissile;
            }
            else
                return EnemyHandling.Wheeled;
        }


        public static bool IsUnarmed(Tank tank)
        {
            return tank.blockman.IterateBlockComponents<ModuleTechController>().Count() >= tank.blockman.IterateBlockComponents<ModuleWeapon>().Count() + tank.blockman.IterateBlockComponents<ModuleDrill>().Count();
        }


        public static void TestShouldCommitDie(Tank tank, EnemyMind mind)
        {
            bool minion = tank.name.Contains("Minion");
            if (!tank.IsPopulation && !minion)
                return;
            if(tank.blockman.blockCount < 3 && !mind.StartedAnchored)
            {
                foreach (TankBlock lastBlock in tank.blockman.IterateBlocks())
                {
                    if (!lastBlock.damage.AboutToDie)
                        lastBlock.damage.SelfDestruct(2f);
                }
            }
            if (KickStart.isWaterModPresent && minion && mind.EvilCommander == EnemyHandling.Wheeled)
            {
                if (!tank.grounded && RBases.TeamActiveMakerBaseCount(tank.Team) > 0 && AIEPathing.AboveTheSea(tank.boundsCentreWorldNoCheck))
                {
                    DebugTAC_AI.Log("TACtical_AI: Recycling " + tank.name + " back to team " + tank.Team + " because it was stuck in the water");
                    RBases.RecycleTechToTeam(tank);
                }
            }
        }
        public static void CheckAndHandleControlBlocks(EnemyMind mind, TankBlock block)
        {
            if (!KickStart.isControlBlocksPresent)
                return;
            else
                HandleUnsetControlBlocks(mind, block);
        }
        public static void HandleUnsetControlBlocks(EnemyMind mind, TankBlock block)
        {
#if !STEAM
            try
            {
                FieldInfo wasSet = typeof(ModuleBlockMover).GetField("Deserialized", BindingFlags.NonPublic | BindingFlags.Instance);
                ModuleBlockMover mover = block.GetComponent<ModuleBlockMover>();
                if (!(bool)mover)
                    return;
                if ((bool)wasSet.GetValue(mover))
                    return;
                Debug.Log("TACtical_AI: Setting a Control Block...");

                mover.ProcessOperations.Clear();
                if (mover.IsPlanarVALUE)
                {
                    mover.ProcessOperations.Add(new InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = InputOperator.InputType.EnemyTechIsNear,
                        m_InputParam = mind.Range,
                        m_OperationType = InputOperator.OperationType.IfThen,
                        m_Strength = 0
                    });
                    mover.ProcessOperations.Add(new InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = InputOperator.InputType.AlwaysOn,
                        m_InputParam = mind.Range,
                        m_OperationType = InputOperator.OperationType.TargetPointPredictive,
                        m_Strength = 125
                    });
                    mover.ProcessOperations.Add(new InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = InputOperator.OperationType.ElseThen,
                        m_Strength = 0
                    });
                    mover.ProcessOperations.Add(new InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = InputOperator.OperationType.SetPos,
                        m_Strength = 0
                    });
                    mover.ProcessOperations.Add(new InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = InputOperator.OperationType.EndIf,
                        m_Strength = 0
                    });
                }
                else
                {
                    mover.ProcessOperations.Add(new InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = InputOperator.InputType.EnemyTechIsNear,
                        m_InputParam = mind.Range,
                        m_OperationType = InputOperator.OperationType.ShiftPos,
                        m_Strength = 2
                    });
                    mover.ProcessOperations.Add(new InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = InputOperator.OperationType.ShiftPos,
                        m_Strength = -1
                    });
                }
            }
            catch { }
#endif
        }



    }
}
