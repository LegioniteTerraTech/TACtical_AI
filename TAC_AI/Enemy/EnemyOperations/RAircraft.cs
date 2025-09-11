using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RAircraft
    {
        // ENEMY CONTROLLERS
        /*  
            Circle,     // Attack like the AC-130 Gunship, broadside while salvoing [BROKEN]
            Grudge,     // Chase and dogfight whatever hit this aircraft last
            Coward,     // Avoid danger
            Bully,      // Attack other aircraft over ground structures.  If inverted, prioritize ground structures over aircraft
            Pesterer,   // Switch to the next closest possible target after attacking one aircraft.  Do not try to dodge and prioritize attack
            Spyper,     // Take aim and fire at the best possible moment in our aiming 
        */
        public static void AttackWoosh(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            BGeneral.ResetValues(helper, ref direct);
            helper.Attempt3DNavi = false;
            helper.AvoidStuff = true;

            //Singleton.Manager<ManTechs>.inst.
            if (tank.rbody.IsNull())
            {   // remove aircraft AI from the world because it's outta player range
                tank.Recycle();
            }

            if (mind.CommanderMind == EnemyAttitude.Homing && helper.lastEnemyGet != null)
            {
                if ((helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude > mind.MaxCombatRange)
                {
                    bool isMending = RGeneral.LollyGag(helper, tank, mind, ref direct);
                    if (isMending)
                        return;
                }
            }
            if (helper.lastEnemyGet == null)
            {
                LollyGagAir(helper, tank, mind, ref direct);
                return;
            }
            RGeneral.Engadge(helper, tank, mind);

            float enemyExt = helper.lastEnemyGet.GetCheapBounds();
            float dist = helper.GetDistanceFromTask(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck, enemyExt);
            float range = AIGlobals.SpacingRangeAircraft;
            float spacing = helper.lastTechExtents + enemyExt;

            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = true;
                    direct.DriveDest = EDriveDest.FromLastDestination;
                    if (dist < spacing + (range / 4))
                    {
                        direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        helper.FullBoost = true;
                        if (tank.wheelGrounded)
                        {
                            if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                                helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                            else
                                helper.SettleDown();
                        }
                    }
                    else if (dist < spacing + range)
                    {
                        if (tank.wheelGrounded)
                        {
                            if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                                helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                            else
                                helper.SettleDown();
                        }
                    }
                    break;
                    /*
                case EnemyAttack.Circle:
                    helper.SideToThreat = true;
                    helper.Retreat = false;
                    if (tank.wheelGrounded)
                    {
                        if (!helper.IsTechMoving(helper.EstTopSped / AIGlobals.SpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            helper.SettleDown();
                    }
                    if (dist < spacing + 2)
                    {
                        direct.DriveDest = EDriveDest.FromLastDestination;
                        direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else if (mind.Range < spacing + range)
                    {
                        direct.DriveDest = EDriveDest.ToLastDestination;
                        direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        helper.BOOST = true;
                        direct.DriveDest = EDriveDest.ToLastDestination;
                        direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    break;
                case EnemyAttack.Spyper:
                    range = EnemyMind.SpacingRangeSpyper;

                    helper.SideToThreat = true;
                    helper.Retreat = false;
                    if (dist < spacing + (range / 2))
                    {
                        direct.DriveDest = EDriveDest.FromLastDestination;
                        direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (tank.wheelGrounded)
                        {
                            if (!helper.IsTechMoving(helper.EstTopSped / AIGlobals.SpeedPanicDividend))
                                helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                helper.SettleDown();
                        }
                    }
                    else if (dist < spacing + range)
                    {
                        direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else if (dist < spacing + (range * 2))
                    {
                        if (tank.wheelGrounded)
                        {
                            if (!helper.IsTechMoving(helper.EstTopSped / AIGlobals.SpeedPanicDividend))
                                helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                helper.SettleDown();
                        }
                        direct.DriveDest = EDriveDest.ToLastDestination;
                        direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        if (tank.wheelGrounded)
                        {
                            if (!helper.IsTechMoving(helper.EstTopSped / AIGlobals.SpeedPanicDividend))
                                helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                helper.SettleDown();
                        }
                        helper.BOOST = true;
                        direct.DriveDest = EDriveDest.ToLastDestination;
                        direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    break;*/
                default:    // Others

                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = false;
                    direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    if (dist < spacing + range)
                    {
                        direct.DriveDest = EDriveDest.FromLastDestination;
                        direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    }
                    else if (dist < spacing + (range * 2))
                    {
                        direct.DriveDest = EDriveDest.FromLastDestination;
                    }
                    else if (dist < spacing + (range * 3))
                    {
                        direct.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else
                    {
                        direct.DriveDest = EDriveDest.ToLastDestination;
                        helper.FullBoost = true;
                    }
                    if (tank.wheelGrounded)
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                    }
                    break;
            }
        }

        public static bool LollyGagAir(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct, bool holdGround = false)
        {
            bool isRegenerating = false;
            if (mind.Hurt)// && helper.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10)
            {
                var energy = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (energy.storageTotal > 500)
                    {
                        if (mind.SolarsAvail && tank.Anchors.NumPossibleAnchors > 0 && !tank.IsAnchored)
                        {
                            if (helper.CanAttemptAnchor)
                            {
                                helper.TryInsureAnchor();
                            }
                            else
                            {   //Try to find new spot
                                FlutterAround(helper, tank, mind, ref direct);
                            }
                        }
                        if (energy.storageTotal - 100 < (energy.storageTotal - energy.spareCapacity))
                        {
                            mind.Hurt = false;
                        }
                    }
                    else
                    {
                        //Cannot repair block damage or recharge shields!
                        mind.Hurt = false;
                    }
                    if (tank.IsAnchored && !mind.StartedAnchored)
                    {
                        isRegenerating = true;
                    }
                }
                if (mind.CommanderSmarts == EnemySmarts.Smrt)
                {
                    if (helper.PendingDamageCheck) //&& helper.AttemptedRepairs < 3)
                    {
                        bool venPower = false;
                        if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                        helper.PendingDamageCheck = RRepair.EnemyRepairStepper(helper, tank, mind, Super: venPower);
                        //helper.AttemptedRepairs++;
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
                if (mind.CommanderSmarts >= EnemySmarts.IntAIligent)
                {
                    if (helper.PendingDamageCheck) //&& helper.AttemptedRepairs < 4)
                    {
                        if ((energy.storageTotal - energy.spareCapacity) / energy.storageTotal > 0.5)
                        {
                            //flex yee building speeds on them players
                            helper.PendingDamageCheck = !RRepair.EnemyInstaRepair(tank, mind);
                            //helper.AttemptedRepairs++;
                        }
                        else
                        {
                            bool venPower = false;
                            if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                            helper.PendingDamageCheck = RRepair.EnemyRepairStepper(helper, tank, mind, Super: venPower);
                            //helper.AttemptedRepairs++;
                        }
                        //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
            }
            else
            {
               // helper.anchorAttempts = 0;
            }

            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is lollygagging   " + mind.CommanderMind.ToString());

            if (holdGround)
                direct.SetLastDest(mind.sceneStationaryPos);
            else
            {
                switch (mind.CommanderMind)
                {
                    case EnemyAttitude.Default: // do dumb stuff
                        FlutterAround(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Homing:  // Get nearest tech regardless of max combat range and attack them
                        RGeneral.HomingIdle(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Miner:   // mine resources
                        RMiner.MineYerOwnBusiness(helper, tank, mind, ref direct);
                        break;
                    //The case below I still have to think of a reason for them to do the things
                    case EnemyAttitude.Junker:  // Huddle up by blocks on the ground
                        FlutterAround(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.OnRails:
                        break;
                    case EnemyAttitude.NPCBaseHost:
                        break;
                    case EnemyAttitude.Boss:
                        break;
                    case EnemyAttitude.Invader:
                        break;
                    case EnemyAttitude.Guardian:
                        break;
                    case EnemyAttitude.PartTurret:
                        break;
                    case EnemyAttitude.PartStatic:
                        break;
                    case EnemyAttitude.PartMimic:
                        break;
                    default:
                        break;
                }
            }
            if (mind.EvilCommander == EnemyHandling.Naval)
                direct.SetLastDest(AIEPathing.OffsetToSea(helper.lastDestinationCore, tank, helper));
            else if (mind.EvilCommander == EnemyHandling.Starship)
                direct.SetLastDest(AIEPathing.OffsetFromGroundH(helper.lastDestinationCore, helper));
            else //Snap to ground
                direct.SetLastDest(AIEPathing.OffsetFromGround(helper.lastDestinationCore, helper, tank.blockBounds.size.y));
            return isRegenerating;
        }
        public static void FlutterAround(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            if (helper.ActionPause == 1)
            {
                if (mind.GetComponent<AIControllerAir>() && UnityEngine.Random.Range(1, 10) < 6)
                {
                    var pilot = mind.GetComponent<AIControllerAir>();
                    direct.SetLastDest(pilot.Tank.boundsCentreWorldNoCheck + (helper.SafeVelocity * Time.fixedDeltaTime * KickStart.AIClockPeriod) + pilot.Tank.rootBlockTrans.forward);
                }
                else
                    direct.SetLastDest(GetRANDPos(tank));
                helper.actionPause = 0;
            }
            else if (helper.ActionPause == 0)
                helper.actionPause = 30;
            direct.DriveDest = EDriveDest.ToLastDestination;
        }

        public static void EnemyDogfighting(TankAIHelper helper, Tank tank, EnemyMind mind)
        {   // Only accounts for forward weapons

            helper.AttackEnemy = false;
            helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);

            if (helper.lastEnemyGet != null)
            {
                Vector3 aimTo = (helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                helper.Urgency += KickStart.AIClockPeriod / 25f;
                Vector3 foreDirect = tank.rootBlockTrans.InverseTransformDirection(aimTo);
                //if (KickStart.isWeaponAimModPresent && mind.CommanderAttack == EnemyAttack.Circle && ((AIControllerAir) helper.MovementController).LargeAircraft)
                //{   // AC-130 broadside attack
                //    if (Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) < 0.25f || Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) > -0.25f || helper.Urgency >= 30)
                //    {
                //        helper.DANGER = true;
                //        //helper.Urgency = 50;
                //        helper.SettleDown();
                //    }
                //}
                //else
                //{   // Normal Dogfighting
                if ((foreDirect.z > 0.15f && foreDirect.x > -0.5f && foreDirect.x < 0.5f) || helper.Urgency >= 30)
                {
                    helper.AttackEnemy = true;
                    //helper.Urgency = 50;
                    helper.SettleDown();
                }
                //}
            }
            else
            {
                helper.Urgency = 0;
                helper.AttackEnemy = false;
            }
        }


        // Utilities
        public static Vector3 GetRANDPos(Tank tank)
        {
            float rangeRAND = 250;
            Vector3 final = tank.boundsCentreWorldNoCheck;

            final.x += UnityEngine.Random.Range(-rangeRAND, rangeRAND);
            final.y += UnityEngine.Random.Range(-rangeRAND, rangeRAND);
            final.z += UnityEngine.Random.Range(-rangeRAND, rangeRAND);

            return final;
        }
    }
}
