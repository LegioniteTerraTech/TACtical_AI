using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;
using TAC_AI.AI.Movement;
using Control_Block;

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
            RunEvilOperations(thisInst, tank);
            ScarePlayer(tank);
        }
        public static void ScarePlayer(Tank tank)
        {
            //Debug.Log("TACtical_AI: enemy AI active!");
            try
            {
                var tonk = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
                if (tonk.IsNotNull())
                {
                    bool player = tonk.tank.IsPlayer;
                    if (player)
                    {
                        Singleton.Manager<ManMusic>.inst.SetDanger(ManMusic.DangerContext.Circumstance.Enemy, tank, tonk.tank);
                    }
                }
            }
            catch { }
        }

        // Begin the AI tree
        public static void RunEvilOperations(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //EnemyMind Mind = tank.GetComponent<EnemyMind>();
            var Mind = thisInst.MovementController.EnemyMind;
            if (Mind.IsNull())
            {
                Mind = tank.GetComponent<EnemyMind>();
                Debug.Log("TACtical_AI: Updating MovementController for " + tank.name);
                thisInst.MovementController.UpdateEnemyMind(Mind);
                //RandomizeBrain(thisInst, tank);
                //return;
            }
            if (Mind.queueRemove)
            {
                Debug.Log("TACtical_AI: Removing Enemy AI (delayed) for " + tank.name);
                return;
            }
            if (Mind.StartedAnchored)
            {
                if (!tank.IsAnchored)
                {
                    if (Mind.AIControl.anchorAttempts < 32)
                    {
                        //Debug.Log("TACtical_AI: Trying to anchor " + tank.name);
                        tank.FixupAnchors(true);
                        tank.Anchors.TryAnchorAll(true);
                        Mind.AIControl.anchorAttempts++;
                        if (tank.IsAnchored)
                            Mind.AIControl.anchorAttempts = 0;
                    }
                }
            }


            RBolts.ManageBolts(thisInst, tank, Mind);
            TestShouldCommitDie(tank, Mind);
            if (Mind.AllowRepairsOnFly && Mind.TechMemor)
            {
                bool venPower = false;
                if (Mind.MainFaction == FactionTypesExt.VEN) venPower = true;
                RRepair.EnemyRepairStepper(thisInst, tank, Mind, venPower);// longer while fighting
            }
            if (Mind.Provoked == EnemyMind.ProvokeTime)
            {
                if (Mind.Tank.IsAnchored)
                {
                    // Execute remote orders to allied units - Attack that threat!
                    RBases.RequestFocusFire(Mind.Tank, Mind.AIControl.lastEnemy);
                }
            }
            if (Mind.Provoked <= 0)
                Mind.Provoked = 0;
            else 
                Mind.Provoked -= KickStart.AIClockPeriod;

            if (Mind.EvilCommander == EnemyHandling.Airplane)
            {
                if (Mind.CommanderMind != EnemyAttitude.SubNeutral)
                    EnemyOperations.RAircraft.EnemyDogfighting(thisInst, tank, Mind);
                else
                {
                    if (Mind.CommanderAttack == EnemyAttack.Grudge)
                    {   // Cannot be grudge while SubNeutral aircraft 
                        thisInst.lastEnemy = null;
                        Mind.CommanderAttack = EnemyAttack.Bully;
                    }
                    if (Mind.Hurt)
                    {   // If we were hit, then we fight back the attacker
                        RandomSetMindAttack(Mind, tank);
                    }
                }
            }
            else if (Mind.CommanderMind == EnemyAttitude.SubNeutral)
            {   // other AI types
                if (Mind.CommanderAttack != EnemyAttack.Coward)
                {   // Cannot be combative while SubNeutral
                    thisInst.lastEnemy = null;
                    Mind.CommanderAttack = EnemyAttack.Coward;
                }
                RGeneral.Scurry(thisInst, tank, Mind);
                if (Mind.Hurt)
                {   // If we were hit, then we fight back the attacker
                    RandomSetMindAttack(Mind, tank);
                }
            }
            else if (Mind.EvilCommander != EnemyHandling.Stationary && Mind.EvilCommander != EnemyHandling.Airplane)
            {
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
            }

            //CommanderMind is handled in each seperate class
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


        // AI SETUP
        public static void RandomizeBrain(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //Debug.Log("TACtical_AI: offset " + tank.boundsCentreWorldNoCheck);
            if (!tank.gameObject.GetComponent<EnemyMind>())
                tank.gameObject.AddComponent<EnemyMind>();
            thisInst.lastPlayer = null;

            thisInst.ResetToDefaultAIController();

            var toSet = tank.gameObject.GetComponent<EnemyMind>();
            toSet.HoldPos = tank.boundsCentreWorldNoCheck;
            toSet.Initiate();
            toSet.Range = 100;
            try
            {
                toSet.Range = (int)tank.Radar.Range + 50;
            }
            catch { }

            bool isMissionTech = RMission.SetupMissionAI(thisInst, tank, toSet);
            if (isMissionTech)
            {
                if (!(bool)tank)
                    return;
                FinalCleanup(thisInst, toSet, tank);
                return;
            }

            if (tank.Anchors.NumAnchored > 0)
                toSet.StartedAnchored = true;

            //add Smartness
            AutoSetIntelligence(toSet, tank);

            bool setEnemy = SetSmartAIStats(thisInst, tank, toSet);
            if (!setEnemy)
            {
                RandomSetMindAttack(toSet, tank);
            }
            FinalCleanup(thisInst, toSet, tank);
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
            //Determine driving method
            BlockSetEnemyHandling(tank, toSet);

            // CHECK before burning processing
            if (SpecialAISpawner.IsAttract)
            {
                return false;
            }
            else if (Singleton.Manager<ManGameMode>.inst.IsCurrent<ModeSumo>())
            {
                Debug.Log("TACtical_AI: Tech " + tank.name + " is a sumo brawler");
                toSet.CommanderSmarts = EnemySmarts.Default;
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.EvilCommander = EnemyHandling.Wheeled;
                toSet.CommanderAttack = EnemyAttack.Grudge;
                return true;
            }

            //Determine Attitude
            if (BM.IterateBlockComponents<ModuleWeapon>().Count() + BM.IterateBlockComponents<ModuleDrill>().Count() <= BM.IterateBlockComponents<ModuleTechController>().Count())
            {   // Unarmed - Runner
                toSet.CommanderMind = EnemyAttitude.SubNeutral;
                toSet.CommanderAttack = EnemyAttack.Coward;
            }
            else if (BM.blockCount > 250 && KickStart.MaxEnemyHQLimit > RBases.GetEnemyHQCount())
            {   // Boss
                toSet.InvertBullyPriority = true;
                toSet.CommanderMind = EnemyAttitude.Boss;
                toSet.CommanderAttack = EnemyAttack.Bully;
            }
            else if (BM.GetBlockWithID((uint)BlockTypes.HE_CannonBattleship_216).IsNotNull() || BM.GetBlockWithID((uint)BlockTypes.GSOBigBertha_845).IsNotNull())
            {   // Artillery
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Spyper;
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleItemHolder>().Count() > 0 || toSet.MainFaction == FactionTypesExt.GC || UnityEngine.Random.Range(0, 100) <= 17)
            {   // Miner
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        toSet.CommanderMind = EnemyAttitude.Miner;
                        //toSet.CommanderAttack = EnemyAttack.Pesterer;
                        toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank);
                        toSet.Range = 64;
                        break;
                    default:
                        if (BM.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                            toSet.CommanderMind = EnemyAttitude.Miner;
                        else
                            toSet.CommanderMind = EnemyAttitude.Default;
                        //toSet.CommanderAttack = EnemyAttack.Bully;
                        toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank);
                        toSet.Range = 64;
                        toSet.InvertBullyPriority = true;
                        break;
                }
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleWeapon>().Count() > 50)
            {   // Over-armed
                toSet.CommanderMind = EnemyAttitude.Homing;
                //toSet.CommanderAttack = EnemyAttack.Bully;
                toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank);
                fired = true;
            }
            else if (toSet.MainFaction == FactionTypesExt.VEN)
            {   // Ven
                toSet.CommanderMind = EnemyAttitude.Default; 
                //toSet.CommanderAttack = EnemyAttack.Circle;
                toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank);
                fired = true;
            }
            else if (toSet.MainFaction == FactionTypesExt.HE)
            {   // Assault
                toSet.CommanderMind = EnemyAttitude.Homing;
                //toSet.CommanderAttack = EnemyAttack.Grudge;
                toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank);
                fired = true;
            }
            if (BM.IterateBlocks().Count() >= KickStart.LethalTechSize)
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
            Debug.Log("TACtical_AI: Tech " + tank.name + " Has bias of" + biasDirection + " and a boost bias of" + boostBiasDirection);

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
                    toSet.SolarsAvail = flagG.HasFlag(ModuleEnergy.OutputConditionFlags.Anchored) && flagG.HasFlag(ModuleEnergy.OutputConditionFlags.DayTime);
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
            Debug.Log("TACtical_AI: Tech " + tank.name + "  Has block count " + blocs.Count() + "  | " + modBoostCount + " | " + modAGCount);


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
                toSet.CommanderMind = EnemyAttitude.SubNeutral;
            }
            else
            {
                int randomNum2 = UnityEngine.Random.Range(0, 0);
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
                    //toSet.CommanderMind = EnemyAttitude.Junker;
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
            toSet.CommanderAttack = RWeapSetup.GetAttackStrat(tank);
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
                toSet.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (toSet.TechMemor.IsNull())
                {
                    toSet.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                    toSet.TechMemor.Initiate();
                    Debug.Log("TACtical_AI: Tech " + tank.name + " Setup for DesignMemory (FinalCleanup)");
                }
                toSet.CommanderBolts = EnemyBolts.AtFullOnAggro;// allow base function
            }
            thisInst.TestForFlyingAIRequirement();

            if (toSet.CommanderSmarts == EnemySmarts.Default && toSet.CommanderMind != EnemyAttitude.Miner && toSet.EvilCommander == EnemyHandling.Wheeled)
            {
                thisInst.Hibernate = true;// enable the default AI
                Debug.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  Default enemy with Default everything");
                return;
            }


            if (toSet.CommanderAttack == EnemyAttack.Grudge)
                toSet.FindEnemy();

            // now handle base spawning
            if (SpecialAISpawner.IsAttract)
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
                            tank.TryToggleTechAnchor();
                        }
                        RBases.MakeMinersMineUnlimited(tank);
                        Debug.Log("TACtical_AI: Tech " + tank.name + " is is a base Tech");
                    }
                    else
                    {
                        toSet.CommanderMind = EnemyAttitude.Miner;
                        Debug.Log("TACtical_AI: Tech " + tank.name + " is is a harvester Tech");
                    }
                }
                else
                {
                    if (toSet.StartedAnchored)
                    {
                        /*
                        tank.FixupAnchors(true);
                        toSet.CommanderMind = EnemyAttitude.Default;
                        toSet.EvilCommander = EnemyHandling.Stationary;
                        toSet.CommanderBolts = EnemyBolts.AtFull;
                        if (!tank.IsAnchored && tank.Anchors.NumPossibleAnchors > 0)
                        {
                            tank.TryToggleTechAnchor();
                        }*/
                        RBases.SetupBaseAI(thisInst, tank, toSet);
                        RBases.MakeMinersMineUnlimited(tank);
                         Debug.Log("TACtical_AI: Tech " + tank.name + " is is a base Tech");
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
                /*
                if (toSet.EvilCommander == EnemyHandling.Starship && toSet.CommanderAttack == EnemyAttack.Circle)// Circle breaks hoverships... for now.
                {
                    toSet.CommanderAttack = EnemyAttack.Bully;
                    toSet.InvertBullyPriority = true;
                }*/
                if (toSet.CommanderMind == EnemyAttitude.Miner)
                {
                    thisInst.lastTechExtents = AIECore.Extremes(tank.blockBounds.extents);
                    if (toSet.CommanderAttack == EnemyAttack.Circle)// Circle breaks the harvester AI
                        toSet.CommanderAttack = EnemyAttack.Coward;
                    if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                        RawTechLoader.TrySpawnBase(tank, thisInst, BasePurpose.HarvestingNoHQ);
                    else
                        RawTechLoader.TrySpawnBase(tank, thisInst, BasePurpose.TechProduction);
                    Debug.Log("TACtical_AI: Tech " + tank.name + " is a base hosting tech!!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                }
                else if (toSet.CommanderMind == EnemyAttitude.Boss)
                {
                    thisInst.lastTechExtents = AIECore.Extremes(tank.blockBounds.extents);
                    RawTechLoader.TrySpawnBase(tank, thisInst, BasePurpose.Headquarters);
                    Debug.Log("TACtical_AI: Tech " + tank.name + " is a base boss with dangerous potential!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                }
                else if (toSet.CommanderMind == EnemyAttitude.Invader)
                {
                    thisInst.lastTechExtents = AIECore.Extremes(tank.blockBounds.extents);
                    RawTechLoader.TrySpawnBase(tank, thisInst, BasePurpose.AnyNonHQ);
                    Debug.Log("TACtical_AI: Tech " + tank.name + " is a base hosting tech!!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
                }
                else
                    Debug.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");

                if (RawTechLoader.ShouldDetonateBoltsNow(toSet) && tank.FirstUpdateAfterSpawn)
                {
                    RBolts.BlowBolts(tank, toSet);
                }
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
            Debug.Log("TACtical_AI: Tech " + tank.name + " Has bias of" + biasDirection + " and a boost bias of" + boostBiasDirection);

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
            //Debug.Log("TACtical_AI: Tech " + tank.name + "  Has block count " + blocs.Count() + "  | " + modBoostCount + " | " + modAGCount);


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
            if(tank.blockman.IterateBlocks().Count() < 3)
            {
                foreach (TankBlock lastBlock in tank.blockman.IterateBlocks())
                {
                    if (!lastBlock.damage.AboutToDie)
                        lastBlock.damage.SelfDestruct(2f);
                }
            }
            if (KickStart.isWaterModPresent && minion && mind.EvilCommander == EnemyHandling.Wheeled)
            {
                if (!tank.grounded && RBases.GetTeamBaseCount(tank.Team) > 0 && AIEPathing.AboveTheSea(tank.boundsCentreWorldNoCheck))
                {
                    Debug.Log("TACtical_AI: Recycling " + tank.name + " back to team " + tank.Team + " because it was stuck in the water");
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
        }
    }
}
