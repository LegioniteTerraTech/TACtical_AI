using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAC_AI.AI.Movement;
using TAC_AI.Templates;
using TAC_AI.World;
using TerraTechETCUtil;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    /// <summary> RELIES ON EVERYTHING IN THE "AI" FOLDER TO FUNCTION PROPERLY!!!  
    ///            [excluding the Designators in said folder]
    /// </summary>
    public static class RCore
    {
        internal static FieldInfo charge = typeof(ModuleShieldGenerator).GetField("m_EnergyDeficit", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge2 = typeof(ModuleShieldGenerator).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge3 = typeof(ModuleShieldGenerator).GetField("m_Shield", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo generator = typeof(ModuleEnergy).GetField("m_OutputConditions", BindingFlags.NonPublic | BindingFlags.Instance);


        // ----------------------------  New Enemy  ---------------------------- 
        public static void GenerateEnemyAI(this TankAIHelper helper, Tank tank)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": offset " + tank.boundsCentreWorldNoCheck);
            DebugTAC_AI.BeginAICalculationTimer(tank);
            var newMind = tank.gameObject.GetComponent<EnemyMind>();
            if (!newMind)
            {
                newMind = tank.gameObject.AddComponent<EnemyMind>();
                newMind.Initiate();
            }
            helper.lastPlayer = null;

            //helper.RecalibrateMovementAIController();

            newMind.sceneStationaryPos = tank.boundsCentreWorldNoCheck;
            newMind.Refresh();

            bool isMissionTech = RMission.SetupBaseOrMissionAI(helper, tank, newMind);
            if (isMissionTech)
            {
                if (!(bool)tank)
                    return;
                FinalCleanup(helper, newMind, tank);
                if (ManNetwork.IsNetworked && ManNetwork.IsHost)
                {
                    NetworkHandler.TryBroadcastNewEnemyState(tank.netTech.netId.Value, newMind.CommanderSmarts);
                }
                return;
            }


            //add Smartness
            AutoSetIntelligence(newMind);

            //Determine driving method
            BlockSetEnemyHandling(tank, newMind);
            bool setEnemy = SetSmartAIStats(tank, newMind);
            if (!setEnemy)
            {
                RandomSetMindAttack(newMind, tank);
            }
            FinalCleanup(helper, newMind, tank);
            if (ManNetwork.IsNetworked && ManNetwork.IsHost)
            {
                NetworkHandler.TryBroadcastNewEnemyState(tank.netTech.netId.Value, newMind.CommanderSmarts);
            }
        }


        // ----------------------------  Setup  ---------------------------- 
        internal static void AutoSetIntelligence(this EnemyMind newMind)
        {
            //add Smartness
            int randomNum = UnityEngine.Random.Range(KickStart.LowerDifficulty, KickStart.UpperDifficulty);
            if (randomNum < 35)
                newMind.CommanderSmarts = EnemySmarts.Default;
            else if (randomNum < 60)
                newMind.CommanderSmarts = EnemySmarts.Mild;
            else if (randomNum < 80)
                newMind.CommanderSmarts = EnemySmarts.Meh;
            else if (randomNum < 92)
                newMind.CommanderSmarts = EnemySmarts.Smrt;
            else
                newMind.CommanderSmarts = EnemySmarts.IntAIligent;
            if (randomNum > 92)
            {
                newMind.AllowRepairsOnFly = true;
                newMind.InvertBullyPriority = true;
            }
        }
        private static bool SetSmartAIStats(Tank tank, EnemyMind newMind)
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
                DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is a sumo brawler");
                newMind.CommanderSmarts = EnemySmarts.Default;
                newMind.CommanderMind = EnemyAttitude.Homing;
                newMind.EvilCommander = EnemyHandling.Wheeled;
                newMind.CommanderAttack = EAttackMode.Chase;
                return true;
            }

            //Determine Attitude
            bool playerIsSmall = false;
            if (Singleton.playerTank)
                playerIsSmall = Singleton.playerTank.blockman.blockCount < BM.blockCount + 5;
            if (!playerIsSmall && BM.blockCount + BM.IterateBlockComponents<ModuleMeleeWeapon>().Count() <=
                BM.IterateBlockComponents<ModuleTechController>().Count())
            {   // Unarmed - Runner
                newMind.CommanderMind = EnemyAttitude.Default;
                newMind.CommanderAttack = EAttackMode.Safety;
            }
            else if (BM.blockCount > AIGlobals.BossTechSize && KickStart.MaxEnemyHQLimit > RLoadedBases.GetAllTeamsEnemyHQCount())
            {   // Boss
                newMind.InvertBullyPriority = true;
                newMind.CommanderMind = EnemyAttitude.Boss;
                newMind.CommanderAttack = EAttackMode.Strong;
            }
            else if (EWeapSetup.HasArtilleryWeapon(BM))
            {   // Artillery
                newMind.CommanderMind = EnemyAttitude.Homing;
                newMind.CommanderAttack = EAttackMode.Ranged;
                fired = true;
            }
            else if (IsHarvester(BM) || newMind.MainFaction == FactionSubTypes.GC)
            {   // Miner
                if (tank.IsPopulation && AIGlobals.EnemyBaseMakerChance >= UnityEngine.Random.Range(0, 100))
                {
                    newMind.CommanderMind = EnemyAttitude.NPCBaseHost;
                    newMind.CommanderAttack = RWeapSetup.GetAttackStrat(tank, newMind);
                    newMind.InvertBullyPriority = true;
                }
                else
                {
                    if (IsHarvester(BM))
                        newMind.CommanderMind = EnemyAttitude.Miner;
                    else
                        newMind.CommanderMind = EnemyAttitude.Junker;
                    newMind.CommanderAttack = RWeapSetup.GetAttackStrat(tank, newMind);
                }
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleWeapon>().Count() > AIGlobals.HomingWeaponCount)
            {   // Over-armed
                newMind.CommanderMind = EnemyAttitude.Homing;
                //toSet.CommanderAttack = EnemyAttack.Bully;
                newMind.CommanderAttack = RWeapSetup.GetAttackStrat(tank, newMind);
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleWeapon>().Count() >= AIGlobals.DefenderWeaponCount)
            {   // Can defend
                newMind.CommanderMind = EnemyAttitude.Guardian;
                //toSet.CommanderAttack = EnemyAttack.Bully;
                newMind.CommanderAttack = RWeapSetup.GetAttackStrat(tank, newMind);
                fired = true;
            }
            else if (newMind.MainFaction == FactionSubTypes.VEN)
            {   // Ven
                //toSet.CommanderMind = EnemyAttitude.Default; 
                newMind.CommanderAttack = EAttackMode.Circle;
                newMind.CommanderAttack = RWeapSetup.GetAttackStrat(tank, newMind);
                fired = true;
            }
            else if (newMind.MainFaction == FactionSubTypes.HE)
            {   // Assault
                newMind.CommanderMind = EnemyAttitude.Homing;
                //toSet.CommanderAttack = EnemyAttack.Grudge;
                newMind.CommanderAttack = RWeapSetup.GetAttackStrat(tank, newMind);
                fired = true;
            }
            if (BM.blockCount >= AIGlobals.LethalTechSize)
            {   // DEATH TO ALL
                if (!SpecialAISpawner.Eradicators.Contains(tank))
                    SpecialAISpawner.Eradicators.Add(tank);
            }
            return fired;
        }
        //private static List<ModuleBooster> engineGetCache = new List<ModuleBooster>();
        private static List<FanJet> jetGetCache = new List<FanJet>();
        private static List<BoosterJet> boosterGetCache = new List<BoosterJet>();
        internal static void BlockSetEnemyHandling(Tank tank, EnemyMind newMind, bool ForceAllBubblesUp = false)
        {
            RawTech RTT = RawTechLoader.GetEnemyBaseTypeFromNameFull(tank.name);
            var BM = tank.blockman;
            if (RTT != null)
            {
                if (RTT.purposes.Contains(BasePurpose.NotStationary))
                {
                    switch (RTT.terrain)
                    {
                        case BaseTerrain.Sea:
                            newMind.EvilCommander = EnemyHandling.Naval;
                            break;
                        case BaseTerrain.Air:
                            newMind.EvilCommander = EnemyHandling.Airplane;
                            break;
                        case BaseTerrain.Chopper:
                            newMind.EvilCommander = EnemyHandling.Chopper;
                            break;
                        case BaseTerrain.Space:
                            newMind.EvilCommander = EnemyHandling.Starship;
                            break;
                        default:
                            newMind.EvilCommander = EnemyHandling.Wheeled;
                            break;
                    }
                }
                else
                {
                    newMind.StartedAnchored = true;
                    newMind.EvilCommander = EnemyHandling.Stationary;
                    newMind.CommanderBolts = EnemyBolts.AtFull;
                }
                if (BM.IterateBlockComponents<ModuleMeleeWeapon>().Count() + (BM.IterateBlockComponents<ModuleWeaponTeslaCoil>().Count()
                    * 25) > BM.IterateBlockComponents<ModuleWeaponGun>().Count() || newMind.MainFaction == FactionSubTypes.GC)
                    newMind.LikelyMelee = true;
                if (RTT.purposes.Contains(BasePurpose.NANI) || RTT.blockCount >= RawTechUtil.FrameImpactingTechBlockCount)
                {   // DEATH TO ALL
                    if (!SpecialAISpawner.Eradicators.Contains(tank))
                        SpecialAISpawner.Eradicators.Add(tank);
                }
                return;
            }
            // We have to do it this way since modded blocks don't work well with the defaults
            bool canFloat = false;
            bool isFlying = false;
            bool isFlyingDirectionForwards = true;
            bool isOmniEngine = false;
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
                BlockDetails BD = new BlockDetails(bloc.BlockType);
                if (BD.DoesMovement)
                {
                    if (BD.HasBoosters || BD.HasFans)
                    {
                        var booster = bloc.GetComponent<ModuleBooster>();
                        if (booster)
                        {
                            modBoostCount++;
                            //Get the slowest spooling one
                            booster.transform.GetComponentsInChildren(jetGetCache);
                            foreach (FanJet jet in jetGetCache)
                            {
#if DEV
                                if (jet == null)
                                    throw new Exception("BlockDetails SAID there was a FanJet in this block but that was incorrect!" +
                                        " Somehow GetComponentsInChildren returned a NULL entry.  We should not be checking for this!");
#endif
                                if ((float)RawTechBase.spinDat.GetValue(jet) <= 10)
                                {
                                    biasDirection -= tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForward) * 
                                        (float)RawTechBase.thrustRate.GetValue(jet);
                                }
                            }
                            jetGetCache.Clear();
                            booster.transform.GetComponentsInChildren(boosterGetCache);
                            foreach (BoosterJet boost in boosterGetCache)
                            {
#if DEV
                                if (boost == null)
                                    throw new Exception("BlockDetails SAID there was a BoosterJet in this block but that was incorrect!" +
                                        " Somehow GetComponentsInChildren returned a NULL entry.  We should not be checking for this!");
#endif
                                //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                                boostBiasDirection -= tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalThrustDirection));
                            }
                            boosterGetCache.Clear();
                        }
                    }
                    if (BD.HasWings)
                    {
                        ModuleWing.Aerofoil[] foils = bloc.GetComponent<ModuleWing>().m_Aerofoils;
                        FoilCount += foils.Length;
                        foreach (ModuleWing.Aerofoil Afoil in foils)
                        {
                            if (Afoil.flapAngleRangeActual > 0 && Afoil.flapTurnSpeed > 0)
                                MovingFoilCount++;
                        }
                    }

                    if (BD.HasHovers)
                        modHoverCount++;
                    if (BD.IsGyro)
                        modGyroCount++;
                    if (BD.HasWheels)
                        modWheelCount++;
                    else if (BD.IsOmniDirectional)
                        isOmniEngine = true;
                    if (BD.FloatsOnWater)
                        canFloat = true;
                    if (BD.HasAntiGravity)
                        modAGCount++;
                }
                if (ForceAllBubblesUp && BD.IsBubble)
                {
                    var buubles = bloc.GetComponent<ModuleShieldGenerator>();
#if DEV
                    if (buubles == null)
                        throw new Exception("BlockDetails SAID there was a ModuleShieldGenerator in this block but that was incorrect!  We should not be checking for this!");
#endif
                    charge.SetValue(buubles, 0);
                    charge2.SetValue(buubles, 2);
                    BubbleShield shield = (BubbleShield)charge3.GetValue(buubles);
                    shield.SetTargetScale(buubles.m_Radius);
                }

                if (BD.IsGenerator)
                {
#if DEV
                    if (bloc.GetComponent<ModuleEnergy>() == null)
                        throw new Exception("BlockDetails SAID there was a ModuleEnergy in this block but that was incorrect!  We should not be checking for this!");
#endif
                    ModuleEnergy.OutputConditionFlags flagG = (ModuleEnergy.OutputConditionFlags)generator.GetValue(bloc.GetComponent<ModuleEnergy>());
                    if (flagG.HasFlag(ModuleEnergy.OutputConditionFlags.Anchored) && flagG.HasFlag(ModuleEnergy.OutputConditionFlags.DayTime))
                        newMind.SolarsAvail = true;
                }

                if (bloc.GetComponent<ModulePacemaker>())
                    tank.Holders.SetHeartbeatSpeed(TechHolders.HeartbeatSpeed.Fast);

                if (BD.IsCab)
                {
                    modControlCount++;
                }
                else if (BD.IsWeapon)
                {
                    if (bloc.GetComponent<ModuleWeaponGun>())
                        modGunCount++;
                    if (BD.IsMelee)
                        modDrillCount++;
                    if (bloc.GetComponent<ModuleWeaponTeslaCoil>())
                        modTeslaCount++;
                }
                try
                {
                    CheckAndHandleControlBlocks(newMind, bloc);
                }
                catch { }
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
            DebugTAC_AI.Info(KickStart.ModID + ": Tech " + tank.name + " Has bias of" + biasDirection + " and a boost bias of" + boostBiasDirection);

            DebugTAC_AI.Info(KickStart.ModID + ": Tech " + tank.name + "  Has block count " + BM.blockCount + "  | " + modBoostCount + " | " + modAGCount);


            if (tank.IsAnchored)
            {
                newMind.StartedAnchored = true;
                newMind.EvilCommander = EnemyHandling.Stationary;
                newMind.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (BM.blockCount == 1)
            {
                if (isOmniEngine)
                    newMind.EvilCommander = EnemyHandling.Starship;
                else if (isFlyingDirectionForwards && MovingFoilCount > 3)
                    newMind.EvilCommander = EnemyHandling.Airplane;
                else if (!isFlyingDirectionForwards)
                    newMind.EvilCommander = EnemyHandling.Chopper;
                else if (tank.IsAnchored)
                    newMind.EvilCommander = EnemyHandling.Stationary;
                else if (canFloat && modWheelCount == 0)
                    newMind.EvilCommander = EnemyHandling.Naval;
                else
                    newMind.EvilCommander = EnemyHandling.Wheeled;
            }
            else if (isOmniEngine && BM.blockCount == 1)
            {
                newMind.EvilCommander = EnemyHandling.Wheeled;
            }
            else if (MovingFoilCount > 4 && isFlying && isFlyingDirectionForwards)
            {
                if ((modHoverCount > 2 && modWheelCount > 2) || modAGCount > 0)
                {
                    newMind.EvilCommander = EnemyHandling.Starship;
                }
                else
                    newMind.EvilCommander = EnemyHandling.Airplane;
            }
            else if ((modGyroCount > 0 || modWheelCount < modBoostCount) && isFlying && !isFlyingDirectionForwards)
            {
                if ((modHoverCount > 2 && modWheelCount > 2) || modAGCount > 0)
                {
                    newMind.EvilCommander = EnemyHandling.Starship;
                }
                else
                    newMind.EvilCommander = EnemyHandling.Chopper;
            }
            else if (modBoostCount > 2 && modAGCount > 0)
            {
                newMind.EvilCommander = EnemyHandling.Starship;
            }
            else if (KickStart.isWaterModPresent && modGyroCount > 0 && modBoostCount > 0 && modWheelCount < 4 + FoilCount)
            {
                newMind.EvilCommander = EnemyHandling.Naval;
            }
            else if ((modBoostCount > 2 && modHoverCount > 2) || isOmniEngine)
            {
                newMind.EvilCommander = EnemyHandling.Starship;
            }
            else if (modGunCount < 1 && modDrillCount < 1 && modBoostCount > 0)
            {
                newMind.EvilCommander = EnemyHandling.SuicideMissile;
            }
            else
                newMind.EvilCommander = EnemyHandling.Wheeled;

            if (modDrillCount + (modTeslaCount * 25) > modGunCount|| newMind.MainFaction == FactionSubTypes.GC)
                newMind.LikelyMelee = true;

            if (BM.blockCount >= RawTechUtil.FrameImpactingTechBlockCount)
            {   // DEATH TO ALL
                if (!SpecialAISpawner.Eradicators.Contains(tank))
                    SpecialAISpawner.Eradicators.Add(tank);
            }
        }
        internal static void RandomSetMindAttack(EnemyMind newMind, Tank tank)
        {
            //add Attitude
            if (IsUnarmed(tank))
            {
                newMind.CommanderAttack = EAttackMode.Safety;
                newMind.CommanderMind = EnemyAttitude.Default;
                newMind.CommanderAlignment = EnemyStanding.SubNeutral;
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
                        newMind.CommanderMind = EnemyAttitude.Default;
                        break;
                    case 4:
                    case 5:
                        newMind.CommanderMind = EnemyAttitude.Junker;
                        break;
                    case 6:
                    case 7:
                        newMind.CommanderMind = EnemyAttitude.Homing;
                        break;
                    case 8:
                    case 9:
                        if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() > 0 && RawTechLoader.CanBeMiner(newMind))
                            newMind.CommanderMind = EnemyAttitude.Miner;
                        else
                            newMind.CommanderMind = EnemyAttitude.Default;
                        break;
                }
            }
            //add Attack
            newMind.CommanderAttack = RWeapSetup.GetAttackStrat(tank, newMind);
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


        // ----------------------------  Operations  ---------------------------- 
        internal static void ScarePlayer(EnemyMind mind, TankAIHelper helper, Tank tank)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": enemy AI active!");
            try
            {
                if (ManBaseTeams.IsEnemy(ManPlayer.inst.PlayerTeam, tank.Team))
                {
                    var targVis = helper.lastEnemyGet;
                    if (targVis?.tank)
                    {
                        Tank target = targVis.tank;
                        bool player = targVis.tank.PlayerFocused;
                        if (targVis.tank.Team == ManPlayer.inst.PlayerTeam)
                        {
                            if (KickStart.WarnOnEnemyLock && Mode<ModeMain>.inst != null)
                            {
                                switch (mind.EvilCommander)
                                {
                                    case EnemyHandling.Chopper:
                                    case EnemyHandling.Airplane:
                                        AIWiki.hintAirDanger.Show();
                                        break;
                                    case EnemyHandling.Starship:
                                        AIWiki.hintSpaceDanger.Show();
                                        break;
                                    case EnemyHandling.SuicideMissile:
                                        AIWiki.hintMissileDanger.Show();
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        bool weInDanger = false;
                        if (player && Singleton.playerTank)
                            weInDanger = true;
                        else if (ManWorldRTS.PlayerIsInRTS)
                        {
                            if (targVis.tank.Team == ManPlayer.inst.PlayerTeam)
                                weInDanger = true;
                        }
                        if (weInDanger)
                        {
                            if (Mode<ModeMain>.inst != null)
                                Mode<ModeMain>.inst.SetPlayerInDanger(true, true);
                            ManMusic.inst.SetDanger(ManMusic.DangerContext.Circumstance.Enemy, tank, targVis.tank);
                        }
                        else if (target != Singleton.playerTank && target.IsEnemy(tank.Team) && target.netTech != null &&
                            target.netTech.NetPlayer != null)
                        {
                            ManMusic.inst.SetDangerClient(ManMusic.DangerContext.Circumstance.Enemy, tank, target);
                        }
                    }
                }
            }
            catch (Exception e) { DebugTAC_AI.Log("error " + e); }
        }

        internal static void BeEvil(TankAIHelper helper, Tank tank)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": enemy AI active!");
            var Mind = helper.MovementController.EnemyMind;
            if (Mind.IsNull())
            {
                Mind = tank.GetComponent<EnemyMind>();
                DebugTAC_AI.Log(KickStart.ModID + ": Updating MovementController for " + tank.name);
                helper.MovementController.UpdateEnemyMind(Mind);
                //RandomizeBrain(helper, tank);
                //return;
            }
            RunEvilOperations(Mind, helper, tank);
            ScarePlayer(Mind, helper, tank);
        }
        internal static void BeEvilLight(TankAIHelper helper, Tank tank)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": enemy AI active!");
            var Mind = helper.MovementController.EnemyMind;
            if (Mind.IsNull())
            {
                Mind = tank.GetComponent<EnemyMind>();
                DebugTAC_AI.Log(KickStart.ModID + ": Updating MovementController for " + tank.name);
                helper.MovementController.UpdateEnemyMind(Mind);
                //RandomizeBrain(helper, tank);
                //return;
            }
            RunLightEvilOp(Mind, helper, tank);
            ScarePlayer(Mind, helper, tank);
        }
        private static void RunLightEvilOp(EnemyMind mind, TankAIHelper helper, Tank tank)
        {
            if (mind.StartedAnchored)
            {
                mind.EvilCommander = EnemyHandling.Stationary;// NO MOVE FOOL
                if (!tank.IsAnchored && !mind.Hurt)
                {
                    if (helper.CanAttemptAnchor)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Trying to anchor " + tank.name);
                        helper.AnchorIgnoreChecks();
                    }
                }
            }
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
            {
                if ((mind.AllowRepairsOnFly || mind.StartedAnchored || mind.BuildAssist) && helper.TechMemor)
                {
                    bool venPower = false;
                    if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                    RRepair.EnemyRepairStepper(helper, tank, mind, venPower);// longer while fighting
                }
            }
            // Attack handling
            switch (mind.CommanderAlignment)
            {
                case EnemyStanding.Enemy:
                    BeHostile(helper, tank);
                    break;
                case EnemyStanding.SubNeutral:
                    BeSubNeutral(helper, tank);
                    break;
                case EnemyStanding.Neutral:
                    BeNeutral(helper, tank);
                    break;
                case EnemyStanding.Friendly:
                    BeFriendly(helper, tank);
                    break;
            }
        }
        private static void RunEvilOperations(EnemyMind mind, TankAIHelper helper, Tank tank)
        {
            if (mind.StartedAnchored)
            {
                if (mind.EvilCommander != EnemyHandling.Stationary)
                    mind.EvilCommander = EnemyHandling.Stationary;// NO MOVE FOOL
                if (!tank.IsAnchored && !mind.Hurt)
                {
                    if (helper.CanAttemptAnchor)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Trying to anchor " + tank.name);
                        helper.AnchorIgnoreChecks();
                    }
                }
            }


            RBolts.ManageBolts(helper, tank, mind);
            TestShouldCommitDie(tank, mind);
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(tank.boundsCentreWorldNoCheck))
            {
                if ((mind.AllowRepairsOnFly || mind.StartedAnchored || mind.BuildAssist) && helper.TechMemor)
                {
                    bool venPower = false;
                    if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                    RRepair.EnemyRepairStepper(helper, tank, mind, venPower);// longer while fighting
                }
            }
            if (mind.AIControl.Provoked <= 0)
            {
                if (helper.lastEnemyGet && helper.lastEnemyGet.isActive)
                {
                    if (!mind.InMaxCombatRangeOfTarget())
                    {
                        mind.EndAggro();
                    }
                }
                else
                    mind.EndAggro();
                mind.AIControl.Provoked = 0;
            }
            else
                mind.AIControl.Provoked -= KickStart.AIClockPeriod;


            // Attack handling
            switch (mind.CommanderAlignment)
            {
                case EnemyStanding.Enemy:
                    BeHostile(helper, tank);
                    break;
                case EnemyStanding.SubNeutral:
                    BeSubNeutral(helper, tank);
                    break;
                case EnemyStanding.Neutral:
                    BeNeutral(helper, tank);
                    break;
                case EnemyStanding.Friendly:
                    BeFriendly(helper, tank);
                    break;
            }

            if (helper.RTSControlled)
                helper.RunRTSNaviEnemy(mind);
            else
                mind.EnemyOpsController.Execute();
            //CommanderMind is handled in each seperate class
            if (AIGlobals.IsBaseTeamDynamic(tank.Team))
            {
                EControlOperatorSet direct = helper.GetDirectedControl();
                if (ProccessIfRetreat(helper, tank, mind, ref direct))
                {
                    helper.SetDirectedControl(direct);
                    return;
                }
                helper.SetDirectedControl(direct);
            }
        }

        public static Vector3 GetTargetCoordinates(TankAIHelper helper, Visible target, EnemyMind mind)
        {
            if (mind.CommanderSmarts >= EnemySmarts.Smrt)   // Rough Target leading
            {
                return helper.RoughPredictTarget(target.tank);
            }
            else
                return target.tank.boundsCentreWorldNoCheck;
        }
        private static void CombatChecking(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            switch (mind.EvilCommander)
            {
                case EnemyHandling.Airplane:
                    EnemyOperations.RAircraft.EnemyDogfighting(helper, tank, mind);
                    break;
                case EnemyHandling.Stationary:
                    RGeneral.BaseAttack(helper, tank, mind);
                    break;
                default:
                    switch (mind.CommanderAttack)
                    {
                        case EAttackMode.Safety:
                            RGeneral.SelfDefense(helper, tank, mind);
                            break;
                        case EAttackMode.Ranged:
                            RGeneral.AimAttack(helper, tank, mind);
                            break;
                        case EAttackMode.Chase:
                            RGeneral.AidAttack(helper, tank, mind);
                            //RGeneral.HoldGrudge(helper, tank, Mind); - now handled within FindEnemy
                            break;
                        default:
                            RGeneral.AidAttack(helper, tank, mind);
                            break;
                    }
                    break;
            }
        }
        private static bool ProccessIfRetreat(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            if (helper.Retreat)
            {
                return GetRetreatLocation(helper, tank, mind, ref direct);
            }
            return false;
        }
        public static bool GetRetreatLocation(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            TeamBasePointer funds = RLoadedBases.GetTeamHQ(tank.Team);
            if (funds.IsValid())
            {
                BGeneral.ResetValues(helper, ref direct);
                if (funds.tank?.visible != null)
                    BGeneral.StopByPosition(helper, tank, funds.WorldPos.ScenePosition, 
                        funds.tank.visible.GetCheapBounds(), ref direct);
                else // It's not loaded, we space based on a fallback scale assumption
                    BGeneral.StopByPosition(helper, tank, funds.WorldPos.ScenePosition, 32, ref direct);
                return true;
            }
            else
            {
                if (helper.lastEnemy?.tank)
                {
                    if (mind.CanCallRetreat)
                    {
                        BGeneral.ResetValues(helper, ref direct);
                        Vector3 runVec = (tank.boundsCentreWorldNoCheck - helper.lastEnemy.tank.boundsCentreWorldNoCheck).normalized;
                        direct.SetLastDest(tank.boundsCentreWorldNoCheck + (runVec * 150));
                        direct.DriveToFacingTowards();
                        return true;
                    }
                }
                else
                {
                    NP_Presence_Automatic EP = ManEnemyWorld.GetTeam(tank.Team);
                    if (EP != null)
                    {
                        NP_BaseUnit EBU = UnloadedBases.RefreshTeamMainBaseIfAnyPossible(EP);
                        if (EBU != null)
                        {
                            BGeneral.ResetValues(helper, ref direct);
                            direct.DriveToFacingTowards();
                            direct.SetLastDest(EBU.PosScene);
                            // yes we will drive off-scene to retreat home
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static void BeHostile(TankAIHelper helper, Tank tank)
        {
            var Mind = helper.MovementController.EnemyMind;
            CombatChecking(helper, tank, Mind);
        }
        private static void BeSubNeutral(TankAIHelper helper, Tank tank)
        {
            var Mind = helper.MovementController.EnemyMind;
            if (helper.Provoked > 0)
                CombatChecking(helper, tank, Mind);
            else
                RGeneral.Monitor(helper, tank, Mind);
        }
        private static void BeNeutral(TankAIHelper helper, Tank tank)
        {
            var Mind = helper.MovementController.EnemyMind;
            if (Mind.Hurt && helper.PendingDamageCheck && Mind.AIControl.Provoked > 0)
            {   // If we were hit & lost blocks, then we fight back the attacker
                if (helper.lastEnemyGet?.tank)
                {
                    int teamAttacker = helper.lastEnemyGet.tank.Team;
                    if (AIGlobals.IsBaseTeamDynamic(teamAttacker) || teamAttacker == ManPlayer.inst.PlayerTeam)
                    {
                        var ETD = ManBaseTeams.InsureBaseTeam(tank.Team);
                        ETD.DegradeRelations(teamAttacker);
                        helper.EndPursuit();
                        ManEnemyWorld.UpdateTeam(tank.Team);
                        return;
                    }
                    /*
                    if (helper.lastEnemyGet.tank.Team == ManPlayer.inst.PlayerTeam)
                    {
                        ManEnemyWorld.ChangeTeam(tank.Team, AIGlobals.GetRandomEnemyBaseTeam());
                        RandomSetMindAttack(Mind, tank);
                        Mind.CommanderAlignment = EnemyStanding.Enemy;
                        return;
                    }
                    else if (AIGlobals.IsBaseTeam(helper.lastEnemyGet.tank.Team))
                    {
                        ManEnemyWorld.ChangeTeam(tank.Team, AIGlobals.GetRandomAllyBaseTeam());
                        RandomSetMindAttack(Mind, tank);
                        Mind.CommanderAlignment = EnemyStanding.Friendly;
                        return;
                    }
                    */
                }
            }
            //helper.lastEnemy = null;
            //helper.AttackEnemy = false;
        }
        private static void BeFriendly(TankAIHelper helper, Tank tank)
        {
            var Mind = helper.MovementController.EnemyMind;
            // Can't really damage an ally
            CombatChecking(helper, tank, Mind);
        }


        // ----------------------------  Checks  ---------------------------- 
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
        public static bool IsUnarmed(Tank tank)
        {
            return tank.blockman.IterateBlockComponents<ModuleTechController>().Count() >= 
                tank.blockman.IterateBlockComponents<ModuleWeapon>().Count() + 
                tank.blockman.IterateBlockComponents<ModuleMeleeWeapon>().Count();
        }
        private static void TestShouldCommitDie(Tank tank, EnemyMind mind)
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
                    DebugTAC_AI.Log(KickStart.ModID + ": Recycling " + tank.name + " back to team " + tank.Team + " because it was stuck in the water");
                    RLoadedBases.RecycleTechToTeam(tank);
                }
            }
        }
        private static void CheckAndHandleControlBlocks(EnemyMind mind, TankBlock block)
        {
            if (!KickStart.isControlBlocksPresent)
                return;
            else
                HandleUnsetControlBlocks(mind, block);
        }
        private static void HandleUnsetControlBlocks(EnemyMind mind, TankBlock block)
        {
            try
            {
                FieldInfo wasSet = typeof(Control_Block.ModuleBlockMover).GetField("Deserialized", BindingFlags.NonPublic | BindingFlags.Instance);
                Control_Block.ModuleBlockMover mover = block.GetComponent<Control_Block.ModuleBlockMover>();
                if (!(bool)mover)
                    return;
                if ((bool)wasSet.GetValue(mover))
                    return;
                DebugTAC_AI.Log(KickStart.ModID + ": Setting a Control Block...");

                mover.ProcessOperations.Clear();
                if (mover.IsPlanarVALUE)
                {
                    mover.ProcessOperations.Add(new Control_Block.InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = Control_Block.InputOperator.InputType.EnemyTechIsNear,
                        m_InputParam = mind.MaxCombatRange,
                        m_OperationType = Control_Block.InputOperator.OperationType.IfThen,
                        m_Strength = 0
                    });
                    mover.ProcessOperations.Add(new Control_Block.InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = Control_Block.InputOperator.InputType.AlwaysOn,
                        m_InputParam = mind.MaxCombatRange,
                        m_OperationType = Control_Block.InputOperator.OperationType.TargetPointPredictive,
                        m_Strength = 125
                    });
                    mover.ProcessOperations.Add(new Control_Block.InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = Control_Block.InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = Control_Block.InputOperator.OperationType.ElseThen,
                        m_Strength = 0
                    });
                    mover.ProcessOperations.Add(new Control_Block.InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = Control_Block.InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = Control_Block.InputOperator.OperationType.SetPos,
                        m_Strength = 0
                    });
                    mover.ProcessOperations.Add(new Control_Block.InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = Control_Block.InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = Control_Block.InputOperator.OperationType.EndIf,
                        m_Strength = 0
                    });
                }
                else
                {
                    mover.ProcessOperations.Add(new Control_Block.InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = Control_Block.InputOperator.InputType.EnemyTechIsNear,
                        m_InputParam = mind.MaxCombatRange,
                        m_OperationType = Control_Block.InputOperator.OperationType.ShiftPos,
                        m_Strength = 2
                    });
                    mover.ProcessOperations.Add(new Control_Block.InputOperator()
                    {
                        m_InputKey = KeyCode.Space,
                        m_InputType = Control_Block.InputOperator.InputType.AlwaysOn,
                        m_InputParam = 0,
                        m_OperationType = Control_Block.InputOperator.OperationType.ShiftPos,
                        m_Strength = -1
                    });
                }
            }
            catch { }
        }
        private static void CheckShouldMakeBase(TankAIHelper helper, EnemyMind newMind, Tank tank)
        {
            switch (newMind.CommanderMind)
            {
                case EnemyAttitude.NPCBaseHost:
                    if (!tank.name.Contains('Ω'))
                        tank.SetName(tank.name + " Ω");
                    newMind.CommanderMind = EnemyAttitude.NPCBaseHost;
                    if (!AIGlobals.IsBaseTeamDynamic(tank.Team))
                    {
                        if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() > 0)
                            RawTechLoader.TryStartBase(tank, helper, BasePurpose.HarvestingNoHQ);
                        else
                            RawTechLoader.TryStartBase(tank, helper, BasePurpose.TechProduction);
                    }
                    DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is a base hosting tech!!  " + newMind.EvilCommander.ToString() + " based " + newMind.CommanderAlignment.ToString() + " with attitude " + newMind.CommanderAttack.ToString() + " | Mind " + newMind.CommanderMind.ToString() + " | Smarts " + newMind.CommanderSmarts.ToString() + " inbound!");
                    break;
                case EnemyAttitude.Boss:
                    if (!tank.name.Contains('⦲'))
                        tank.SetName(tank.name + " ⦲");
                    if (!AIGlobals.IsBaseTeamDynamic(tank.Team))
                        RawTechLoader.TryStartBase(tank, helper, BasePurpose.Headquarters);
                    DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is a base boss with dangerous potential!  " + newMind.EvilCommander.ToString() + " based " + newMind.CommanderAlignment.ToString() + " with attitude " + newMind.CommanderAttack.ToString() + " | Mind " + newMind.CommanderMind.ToString() + " | Smarts " + newMind.CommanderSmarts.ToString() + " inbound!");
                    break;
                case EnemyAttitude.Invader:
                    RawTechLoader.TryStartBase(tank, helper, BasePurpose.AnyNonHQ);
                    DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is an invader looking to take over your world!  " + newMind.EvilCommander.ToString() + " based " + newMind.CommanderAlignment.ToString() + " with attitude " + newMind.CommanderAttack.ToString() + " | Mind " + newMind.CommanderMind.ToString() + " | Smarts " + newMind.CommanderSmarts.ToString() + " inbound!");
                    break;
                case EnemyAttitude.Miner:
                    if (newMind.CommanderAttack == EAttackMode.Circle)// Circle breaks the harvester AI in some attack cases
                    {
                        switch (newMind.EvilCommander)
                        {
                            case EnemyHandling.Naval:
                                newMind.CommanderAttack = EAttackMode.Ranged;
                                break;
                            case EnemyHandling.Starship:
                            case EnemyHandling.Airplane:
                            case EnemyHandling.Chopper:
                                newMind.CommanderAttack = EAttackMode.Random;
                                break;
                            default:
                                newMind.CommanderAttack = EAttackMode.Safety;
                                break;
                        }
                    }
                    DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is a harvester!  " + newMind.EvilCommander.ToString() + " based " + newMind.CommanderAlignment.ToString() + " with attitude " + newMind.CommanderAttack.ToString() + " | Mind " + newMind.CommanderMind.ToString() + " | Smarts " + newMind.CommanderSmarts.ToString() + " inbound!");
                    break;
                default:
                    DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is ready to roll!  " + newMind.EvilCommander.ToString() + " based " + newMind.CommanderAlignment.ToString() + " with attitude " + newMind.CommanderAttack.ToString() + " | Mind " + newMind.CommanderMind.ToString() + " | Smarts " + newMind.CommanderSmarts.ToString() + " inbound!");
                    break;
            }
        }
        internal static void SetFromScheme(EnemyMind newMind, Tank tank)
        {
            try
            {
                ControlSchemeCategory Schemer = tank.control.ActiveScheme.Category;
                switch (Schemer)
                {
                    case ControlSchemeCategory.Car:
                        newMind.EvilCommander = EnemyHandling.Wheeled;
                        break;
                    case ControlSchemeCategory.Aeroplane:
                        newMind.EvilCommander = EnemyHandling.Airplane;
                        break;
                    case ControlSchemeCategory.Helicopter:
                        newMind.EvilCommander = EnemyHandling.Chopper;
                        break;
                    case ControlSchemeCategory.AntiGrav:
                        newMind.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Rocket:
                        newMind.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Hovercraft:
                        newMind.EvilCommander = EnemyHandling.Starship;
                        break;
                    default:
                        string name = tank.control.ActiveScheme.CustomName;
                        if (name == "Ship" || name == "ship" || name == "Naval" || name == "naval" || name == "Boat" || name == "boat")
                        {
                            newMind.EvilCommander = EnemyHandling.Naval;
                        }
                        //Else we just default to Wheeled
                        break;
                }
            }
            catch { }//some population techs are devoid of schemes
        }


        // ----------------------------  Conclusion  ---------------------------- 
        private static void FinalCleanup(TankAIHelper helper, EnemyMind newMind, Tank tank)
        {
            if (tank.Anchors.NumAnchored > 0 || tank.GetComponent<RequestAnchored>())
            {
                newMind.StartedAnchored = true;
                newMind.EvilCommander = EnemyHandling.Stationary;
                helper.TryInsureAnchor();
                helper.AnchorIgnoreChecks();
            }
            if (newMind.CommanderSmarts > EnemySmarts.Meh)
            {
                helper.InsureTechMemor("FinalCleanup", true);
                newMind.CommanderBolts = EnemyBolts.AtFullOnAggro;// allow base function
            }

            bool isBaseMaker = newMind.CommanderMind == EnemyAttitude.NPCBaseHost || newMind.CommanderMind == EnemyAttitude.Boss;
            if (newMind.CommanderSmarts == EnemySmarts.Default && !isBaseMaker && newMind.EvilCommander == EnemyHandling.Wheeled
                && !ManBaseTeams.ShouldNotAttack(ManPlayer.inst.PlayerTeam, tank.Team))
            {
                helper.RunState = AIRunState.Default;// enable the default AI
                switch (newMind.CommanderMind)
                {
                    case EnemyAttitude.Miner:
                    case EnemyAttitude.Junker:
                        break;
                }
                DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is ready to roll!  Default enemy with Default everything");
                if (DebugRawTechSpawner.ShowDebugFeedBack)
                {
                    AIGlobals.PopupColored(newMind.EvilCommander.ToString(), tank.Team, 
                        WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up *
                        helper.lastTechExtents)));
                }
                //helper.RecalibrateMovementAIController();
                DebugTAC_AI.FinishAICalculationTimer(tank);
                return;
            }

            // now handle base spawning
            if (AIGlobals.IsAttract)
            {
                if (KickStart.SpecialAttractNum == AttractType.Harvester)
                {
                    newMind.CommanderSmarts = EnemySmarts.IntAIligent;
                    if (newMind.StartedAnchored)
                    {
                        tank.FixupAnchors(true);
                        newMind.CommanderMind = EnemyAttitude.Default;
                        newMind.EvilCommander = EnemyHandling.Stationary;
                        newMind.CommanderBolts = EnemyBolts.MissionTrigger;
                        if (!tank.IsAnchored && tank.Anchors.NumPossibleAnchors > 0)
                        {
                            helper.AnchorIgnoreChecks();
                        }
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is a base Tech");
                    }
                    else
                    {
                        newMind.CommanderMind = EnemyAttitude.Miner;
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is a harvester Tech");
                    }
                }
                else
                {
                    if (newMind.StartedAnchored)
                    {
                        RLoadedBases.SetupBaseAI(helper, tank, newMind);
                        if (ManBaseTeams.TryGetBaseTeamDynamicOnly(tank.Team, out var teamInst))
                            teamInst.AddBuildBucks(4000000);
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is is a base Tech");
                    }
                    if (newMind.EvilCommander != EnemyHandling.Wheeled)
                        newMind.CommanderAttack = EAttackMode.Chase;
                    if (newMind.CommanderAttack == EAttackMode.Safety)
                        newMind.CommanderAttack = EAttackMode.Circle;
                    if (newMind.CommanderAttack == EAttackMode.Ranged)
                        newMind.CommanderAttack = EAttackMode.Chase;
                    if (newMind.CommanderMind == EnemyAttitude.Miner)
                        newMind.CommanderMind = EnemyAttitude.Homing;
                }
            }
            else
            {
                CheckShouldMakeBase(helper, newMind, tank);

                int Team = tank.Team;
                if (ManBaseTeams.TryGetBaseTeamDynamicOnly(Team, out var ETD))
                {
                    newMind.CommanderAlignment = ETD.EnemyMindAlignment(ManPlayer.inst.PlayerTeam);
                }

                if (RawTechLoader.ShouldDetonateBoltsNow(newMind) && tank.FirstUpdateAfterSpawn)
                {
                    newMind.BlowBolts();
                }

                if (!KickStart.AllowEnemiesToMine && newMind.CommanderMind == EnemyAttitude.Miner)
                    newMind.CommanderMind = EnemyAttitude.Default;
            }
            if (DebugRawTechSpawner.ShowDebugFeedBack)
                AIGlobals.PopupColored(newMind.EvilCommander.ToString(), tank.Team, WorldPosition.FromScenePosition(tank.boundsCentreWorld + (Vector3.up * helper.lastTechExtents)));

            helper.SecondAvoidence = newMind.CommanderSmarts >= EnemySmarts.Smrt;
            helper.AISetSettings.AdvancedAI = newMind.CommanderSmarts >= EnemySmarts.Meh;
            if (newMind.CommanderMind == EnemyAttitude.Homing)
                helper.AISetSettings.ScanRange = AIGlobals.EnemyExtendActionRange;
            else
                helper.AISetSettings.ScanRange = AIGlobals.DefaultEnemyScanRange;

            switch (newMind.CommanderMind)
            {
                case EnemyAttitude.Homing:
                    newMind.MaxCombatRange = AIGlobals.EnemyExtendActionRange;
                    break;
                case EnemyAttitude.Miner:
                case EnemyAttitude.Junker:
                    newMind.MaxCombatRange = AIGlobals.PassiveMaxCombatRange;
                    break;
                case EnemyAttitude.NPCBaseHost:
                    newMind.MaxCombatRange = AIGlobals.BaseFounderMaxCombatRange;
                    break;
                case EnemyAttitude.Boss:
                    newMind.MaxCombatRange = AIGlobals.BossMaxCombatRange;
                    break;
                case EnemyAttitude.Invader:
                    newMind.MaxCombatRange = AIGlobals.InvaderMaxCombatRange;
                    break;
                default:
                    if (helper.AttackMode == EAttackMode.Ranged)
                        newMind.MaxCombatRange = AIGlobals.SpyperMaxCombatRange;
                    else
                        newMind.MaxCombatRange = AIGlobals.DefaultEnemyMaxCombatRange;

                    break;
            }
            if (helper.AttackMode == EAttackMode.Ranged)
                newMind.MinCombatRange = AIGlobals.MinCombatRangeSpyper;
            else if (helper.Attempt3DNavi)
                newMind.MinCombatRange = AIGlobals.SpacingRangeHoverer;
            else
                newMind.MinCombatRange = AIGlobals.MinCombatRangeDefault;

            //helper.RecalibrateMovementAIController();
            DebugTAC_AI.FinishAICalculationTimer(tank);
        }
    }
}
