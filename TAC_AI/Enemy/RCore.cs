using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
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
            //DebugTAC_AI.Log("TACtical_AI: enemy AI active!");
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
        public static void BeEvilLight(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //DebugTAC_AI.Log("TACtical_AI: enemy AI active!");
            var Mind = thisInst.MovementController.EnemyMind;
            if (Mind.IsNull())
            {
                Mind = tank.GetComponent<EnemyMind>();
                DebugTAC_AI.Log("TACtical_AI: Updating MovementController for " + tank.name);
                thisInst.MovementController.UpdateEnemyMind(Mind);
                //RandomizeBrain(thisInst, tank);
                //return;
            }
            RunLightEvilOp(Mind, thisInst, tank);
            ScarePlayer(Mind, thisInst, tank);
        }
        public static void ScarePlayer(EnemyMind mind, AIECore.TankAIHelper thisInst, Tank tank)
        {
            //DebugTAC_AI.Log("TACtical_AI: enemy AI active!");
            try
            {
                if (mind.CommanderAlignment == EnemyStanding.Enemy)
                {
                    var tonk = thisInst.lastEnemyGet;
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
        public static void RunLightEvilOp(EnemyMind Mind, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (Mind.StartedAnchored)
            {
                Mind.EvilCommander = EnemyHandling.Stationary;// NO MOVE FOOL
                if (!tank.IsAnchored && !Mind.Hurt)
                {
                    if (thisInst.anchorAttempts < AIGlobals.NPTAnchorAttempts)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Trying to anchor " + tank.name);
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
        }
        private static void RunEvilOperations(EnemyMind Mind, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (Mind.StartedAnchored)
            {
                Mind.EvilCommander = EnemyHandling.Stationary;// NO MOVE FOOL
                if (!tank.IsAnchored && !Mind.Hurt)
                {
                    if (thisInst.anchorAttempts < AIGlobals.NPTAnchorAttempts)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Trying to anchor " + tank.name);
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
            if (Mind.AIControl.Provoked <= 0)
            {
                if (thisInst.lastEnemyGet && thisInst.lastEnemyGet.isActive)
                {
                    if (!Mind.InMaxCombatRangeOfTarget())
                    {
                        Mind.EndAggro();
                    }
                }
                else
                    Mind.EndAggro();
                Mind.AIControl.Provoked = 0;
            }
            else
                Mind.AIControl.Provoked -= KickStart.AIClockPeriod;


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

            if (thisInst.RTSControlled)
                thisInst.RunRTSNaviEnemy(Mind);
            else
                Mind.EnemyOpsController.Execute();
            //CommanderMind is handled in each seperate class
            if (AIGlobals.IsBaseTeam(tank.Team))
            {
                EControlOperatorSet direct = thisInst.GetDirectedControl();
                if (ProccessIfRetreat(thisInst, tank, Mind, ref direct))
                {
                    thisInst.SetDirectedControl(direct);
                    return;
                }
                thisInst.SetDirectedControl(direct);
            }
        }


        public static Vector3 GetTargetCoordinates(AIECore.TankAIHelper thisInst, Visible target, EnemyMind mind)
        {
            if (mind.CommanderSmarts >= EnemySmarts.Smrt)   // Rough Target leading
            {
                return thisInst.RoughPredictTarget(target.tank);
            }
            else
                return target.tank.boundsCentreWorldNoCheck;
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
            if (Mind.Hurt && Mind.AIControl.Provoked > 0)
            {   // If we were hit, then we fight back the attacker
                if (thisInst.lastEnemyGet?.tank)
                {
                    int teamAttacker = thisInst.lastEnemyGet.tank.Team;
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
            if (Mind.Hurt && thisInst.PendingDamageCheck && Mind.AIControl.Provoked > 0)
            {   // If we were hit & lost blocks, then we fight back the attacker
                if (thisInst.lastEnemyGet?.tank)
                {
                    if (thisInst.lastEnemyGet.tank.Team == ManPlayer.inst.PlayerTeam)
                    {
                        ManEnemyWorld.ChangeTeam(tank.Team, AIGlobals.GetRandomEnemyBaseTeam());
                        RandomSetMindAttack(Mind, tank);
                        Mind.CommanderAlignment = EnemyStanding.Enemy;
                        return;
                    }
                    else if (AIGlobals.IsBaseTeam(thisInst.lastEnemyGet.tank.Team))
                    {
                        ManEnemyWorld.ChangeTeam(tank.Team, AIGlobals.GetRandomAllyBaseTeam());
                        RandomSetMindAttack(Mind, tank);
                        Mind.CommanderAlignment = EnemyStanding.Friendly;
                        return;
                    }
                }
            }
            thisInst.lastEnemy = null;
            thisInst.AttackEnemy = false;
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
                        case EAttackMode.Safety:
                            RGeneral.SelfDefense(thisInst, tank, Mind);
                            break;
                        case EAttackMode.Ranged:
                            RGeneral.AimAttack(thisInst, tank, Mind);
                            break;
                        case EAttackMode.Chase:
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

        public static bool ProccessIfRetreat(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind Mind, ref EControlOperatorSet direct)
        {
            if (thisInst.Retreat)
            {
                return GetRetreatLocation(thisInst, tank, Mind, ref direct);
            }
            return false;
        }
        public static bool GetRetreatLocation(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind Mind, ref EControlOperatorSet direct)
        {
            RLoadedBases.EnemyBaseFunder funds = RLoadedBases.GetTeamFunder(tank.Team);
            if (funds)
            {
                BGeneral.ResetValues(thisInst, ref direct);
                thisInst.theBase = funds.Tank;
                thisInst.lastBaseExtremes = funds.Tank.visible.GetCheapBounds();

                Vector3 veloFlat = Vector3.zero;
                if ((bool)tank.rbody)   // So that drifting is minimized
                {
                    veloFlat = thisInst.SafeVelocity;
                    veloFlat.y = 0;
                }
                float dist = (thisInst.theBase.boundsCentreWorldNoCheck + veloFlat - tank.boundsCentreWorldNoCheck).magnitude;
                bool messaged = true;
                direct.lastDestination = funds.Tank.boundsCentreWorldNoCheck;
                StopByBase(thisInst, tank, dist, ref messaged, ref direct);
                return true;
            }
            else
            {
                if (thisInst.lastEnemy?.tank)
                {
                    BGeneral.ResetValues(thisInst, ref direct);
                    Vector3 runVec = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).normalized;
                    direct.lastDestination = tank.boundsCentreWorldNoCheck + (runVec * 150);
                    direct.DriveAwayFacingAway();
                }
                else
                {
                    NP_Presence EP = ManEnemyWorld.GetTeam(tank.Team);
                    if (EP != null)
                    {
                        NP_BaseUnit EBU = UnloadedBases.GetSetTeamMainBase(EP);
                        if (EBU != null)
                        {
                            BGeneral.ResetValues(thisInst, ref direct);
                            direct.DriveToFacingTowards();
                            direct.lastDestination = EBU.PosScene;
                            // yes we will drive off-scene to retreat home
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public static void StopByBase(AIECore.TankAIHelper thisInst, Tank tank, float dist, ref bool hasMessaged, ref EControlOperatorSet direct)
        {
            if (thisInst.theBase == null)
                return; // There's no base!
            float girth = thisInst.lastBaseExtremes + thisInst.lastTechExtents;
            if (dist < girth + 3)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving room to base... |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetHelperInsured().AllowApproach(thisInst);
                thisInst.AvoidStuff = false;
                direct.DriveDest = EDriveDest.FromLastDestination;
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
                thisInst.SettleDown();
            }
            else if (dist < girth + 7)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetHelperInsured().AllowApproach(thisInst);
                thisInst.AvoidStuff = false;
                thisInst.Yield = true;
                thisInst.PivotOnly = true;
                thisInst.SettleDown();
            }
        }



        // AI SETUP
        public static void RandomizeBrain(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //DebugTAC_AI.Log("TACtical_AI: offset " + tank.boundsCentreWorldNoCheck);
            var toSet = tank.gameObject.GetComponent<EnemyMind>();
            if (!toSet)
            {
                toSet = tank.gameObject.AddComponent<EnemyMind>();
                toSet.Initiate();
            }
            thisInst.lastPlayer = null;

            thisInst.RecalibrateMovementAIController();

            toSet.sceneStationaryPos = tank.boundsCentreWorldNoCheck;
            toSet.Refresh();

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
                toSet.CommanderAttack = EAttackMode.Chase;
                return true;
            }

            //Determine Attitude
            bool playerIsSmall = false;
            if (Singleton.playerTank)
                playerIsSmall = Singleton.playerTank.blockman.blockCount < BM.blockCount + 5;
            if (!playerIsSmall && BM.blockCount + BM.IterateBlockComponents<ModuleDrill>().Count() <= BM.IterateBlockComponents<ModuleTechController>().Count())
            {   // Unarmed - Runner
                toSet.CommanderMind = EnemyAttitude.Default;
                toSet.CommanderAttack = EAttackMode.Safety;
            }
            else if (BM.blockCount > AIGlobals.BossTechSize && KickStart.MaxEnemyHQLimit > RLoadedBases.GetAllTeamsEnemyHQCount())
            {   // Boss
                toSet.InvertBullyPriority = true;
                toSet.CommanderMind = EnemyAttitude.Boss;
                toSet.CommanderAttack = EAttackMode.Strong;
            }
            else if (EWeapSetup.HasArtilleryWeapon(BM))
            {   // Artillery
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EAttackMode.Ranged;
                fired = true;
            }
            else if (IsHarvester(BM) || toSet.MainFaction == FactionTypesExt.GC)
            {   // Miner
                if (AIGlobals.EnemyBaseMakerChance >= UnityEngine.Random.Range(0, 100))
                {
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
                    toSet.InvertBullyPriority = true;
                }
                else
                {
                    if (tank.IsPopulation)
                        toSet.CommanderMind = EnemyAttitude.NPCBaseHost;
                    else
                        toSet.CommanderMind = EnemyAttitude.Miner;
                    toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank, toSet);
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
                toSet.CommanderAttack = EAttackMode.Circle;
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
        private static List<ModuleBooster> engineGetCache = new List<ModuleBooster>();
        private static List<FanJet> jetGetCache = new List<FanJet>();
        private static List<BoosterJet> boosterGetCache = new List<BoosterJet>();
        public static void BlockSetEnemyHandling(Tank tank, EnemyMind toSet, bool ForceAllBubblesUp = false)
        {
            var BM = tank.blockman;
            // We have to do it this way since modded blocks don't work well with the defaults
            bool isFlying = false;
            bool isFlyingDirectionForwards = true;
            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            int FoilCount = 0;
            int MovingFoilCount = 0;
            int modControlCount = 0;
            int modBoostCount = 0;
            int modHoverCount = 0;
            int modGyroCount = 0;
            int modWheelCount = 0;
            int modAGCount = 0;
            int modGunCount = 0;
            int modDrillCount = 0;
            int modTeslaCount = 0;

            foreach (TankBlock bloc in BM.IterateBlocks())
            {
                var booster = bloc.GetComponent<ModuleBooster>();
                if (booster)
                {
                    //Get the slowest spooling one
                    booster.transform.GetComponentsInChildren(jetGetCache);
                    foreach (FanJet jet in jetGetCache)
                    {
                        if (jet.spinDelta <= 10)
                        {
                            biasDirection -= tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForwards) * jet.force;
                        }
                    }
                    jetGetCache.Clear();
                    booster.transform.GetComponentsInChildren(boosterGetCache);
                    foreach (BoosterJet boost in boosterGetCache)
                    {
                        //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                        boostBiasDirection -= tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection));
                    }
                    boosterGetCache.Clear();
                }

                var wing = bloc.GetComponent<ModuleWing>();
                if (wing)
                {
                    ModuleWing.Aerofoil[] foils = wing.m_Aerofoils;
                    FoilCount += foils.Length;
                    foreach (ModuleWing.Aerofoil Afoil in foils)
                    {
                        if (Afoil.flapAngleRangeActual > 0 && Afoil.flapTurnSpeed > 0)
                            MovingFoilCount++;
                    }
                }
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

            DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + "  Has block count " + BM.blockCount + "  | " + modBoostCount + " | " + modAGCount);


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
                toSet.CommanderAttack = EAttackMode.Safety;
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
            if (tank.Anchors.NumAnchored > 0 || tank.GetComponent<RequestAnchored>())
            {
                toSet.StartedAnchored = true;
                toSet.EvilCommander = EnemyHandling.Stationary;
            }
            if (toSet.CommanderSmarts > EnemySmarts.Meh)
            {
                thisInst.InsureTechMemor("FinalCleanup", true);
                toSet.CommanderBolts = EnemyBolts.AtFullOnAggro;// allow base function
            }

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
                thisInst.RecalibrateMovementAIController();
                return;
            }

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
                        RLoadedBases.SetupBaseAI(thisInst, tank, toSet);
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is is a base Tech");
                    }
                    if (toSet.EvilCommander != EnemyHandling.Wheeled)
                        toSet.CommanderAttack = EAttackMode.Chase;
                    if (toSet.CommanderAttack == EAttackMode.Safety)
                        toSet.CommanderAttack = EAttackMode.Circle;
                    if (toSet.CommanderAttack == EAttackMode.Ranged)
                        toSet.CommanderAttack = EAttackMode.Chase;
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

                if (!KickStart.AllowEnemiesToMine && toSet.CommanderMind == EnemyAttitude.Miner)
                    toSet.CommanderMind = EnemyAttitude.Default;
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

            thisInst.SecondAvoidence = toSet.CommanderSmarts >= EnemySmarts.Smrt;
            thisInst.AILimitSettings.AdvancedAI = toSet.CommanderSmarts >= EnemySmarts.Meh;
            if (toSet.CommanderMind == EnemyAttitude.Homing)
                thisInst.AILimitSettings.ScanRange = AIGlobals.EnemyExtendActionRange;
            else
                thisInst.AILimitSettings.ScanRange = AIGlobals.DefaultEnemyScanRange;

            switch (toSet.CommanderMind)
            {
                case EnemyAttitude.Homing:
                    toSet.MaxCombatRange = AIGlobals.EnemyExtendActionRange;
                    break;
                case EnemyAttitude.Miner:
                case EnemyAttitude.Junker:
                    toSet.MaxCombatRange = AIGlobals.PassiveMaxCombatRange;
                    break;
                case EnemyAttitude.NPCBaseHost:
                    toSet.MaxCombatRange = AIGlobals.BaseFounderMaxCombatRange;
                    break;
                case EnemyAttitude.Boss:
                    toSet.MaxCombatRange = AIGlobals.BossMaxCombatRange;
                    break;
                case EnemyAttitude.Invader:
                    toSet.MaxCombatRange = AIGlobals.InvaderMaxCombatRange;
                    break;
                default:
                    if (thisInst.AttackMode == EAttackMode.Ranged)
                        toSet.MaxCombatRange = AIGlobals.SpyperMaxCombatRange;
                    else
                        toSet.MaxCombatRange = AIGlobals.DefaultEnemyMaxCombatRange;

                    break;
            }
            if (thisInst.AttackMode == EAttackMode.Ranged)
                toSet.MinCombatRange = AIGlobals.MinCombatRangeSpyper;
            else if (thisInst.Attempt3DNavi)
                toSet.MinCombatRange = AIGlobals.SpacingRangeHoverer;
            else
                toSet.MinCombatRange = AIGlobals.MinCombatRangeDefault;

            thisInst.RecalibrateMovementAIController();
        }
        public static void CheckShouldMakeBase(AIECore.TankAIHelper thisInst, EnemyMind toSet, Tank tank)
        {
            switch (toSet.CommanderMind)
            {
                case EnemyAttitude.NPCBaseHost:
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
                    if (toSet.CommanderAttack == EAttackMode.Circle)// Circle breaks the harvester AI in some attack cases
                    {
                        switch (toSet.EvilCommander)
                        {
                            case EnemyHandling.Naval:
                                toSet.CommanderAttack = EAttackMode.Ranged;
                                break;
                            case EnemyHandling.Starship:
                            case EnemyHandling.Airplane:
                            case EnemyHandling.Chopper:
                                toSet.CommanderAttack = EAttackMode.Random;
                                break;
                            default:
                                toSet.CommanderAttack = EAttackMode.Safety;
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
                if (!tank.grounded && RLoadedBases.TeamActiveMakerBaseCount(tank.Team) > 0 && AIEPathing.AboveTheSea(mind.AIControl))
                {
                    DebugTAC_AI.Log("TACtical_AI: Recycling " + tank.name + " back to team " + tank.Team + " because it was stuck in the water");
                    RLoadedBases.RecycleTechToTeam(tank);
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
                DebugTAC_AI.Log("TACtical_AI: Setting a Control Block...");

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
