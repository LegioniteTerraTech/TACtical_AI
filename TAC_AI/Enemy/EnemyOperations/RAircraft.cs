using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RAircraft
    {
        // ENEMY CONTROLLERS
        /*  
            Circle,     // Attack like the AC-130 Gunship, broadside while salvoing
            Grudge,     // Chase and dogfight whatever hit this aircraft last
            Coward,     // Avoid danger
            Bully,      // Attack other aircraft over ground structures.  If inverted, prioritize ground structures over aircraft
            Pesterer,   // Switch to the next closest possible target after attacking one aircraft.  Do not try to dodge and prioritize attack
            Spyper,     // Take aim and fire at the best possible moment in our aiming 
        */
        public static void TryFly(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = false;
            thisInst.AvoidStuff = true;

            //Singleton.Manager<ManTechs>.inst.
            if (tank.rbody.IsNull())
            {   // remove aircraft AI from the world because it's outta player range
                tank.Recycle();
            }

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemy != null)
            {
                if ((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude > mind.Range)
                {
                    bool isMending = RGeneral.LollyGag(thisInst, tank, mind);
                    if (isMending)
                        return;
                }
            }
            if (thisInst.lastEnemy == null)
            {
                LollyGagAir(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = thisInst.lastEnemy.GetCheapBounds();
            float dist = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude - enemyExt;
            float range = AIGlobals.SpacingRangeAircraft;
            float spacing = thisInst.lastTechExtents + enemyExt;
            thisInst.lastRange = dist;

            switch (mind.CommanderAttack)
            {
                case EnemyAttack.Coward:
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    thisInst.DriveDest = EDriveDest.FromLastDestination;
                    if (dist < spacing + (range / 4))
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.BOOST = true;
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                    }
                    else if (dist < spacing + range)
                    {
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                    }
                    break;
                    /*
                case EnemyAttack.Circle:
                    thisInst.SideToThreat = true;
                    thisInst.Retreat = false;
                    if (tank.wheelGrounded)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    if (dist < spacing + 2)
                    {
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else if (mind.Range < spacing + range)
                    {
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        thisInst.BOOST = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    break;
                case EnemyAttack.Spyper:
                    range = EnemyMind.SpacingRangeSpyper;

                    thisInst.SideToThreat = true;
                    thisInst.Retreat = false;
                    if (dist < spacing + (range / 2))
                    {
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                    }
                    else if (dist < spacing + range)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else if (dist < spacing + (range * 2))
                    {
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                        thisInst.BOOST = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    break;*/
                default:    // Others

                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (dist < spacing + range)
                    {
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                    }
                    else if (dist < spacing + (range * 2 ))
                    {
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                    }
                    else if (dist < spacing + (range * 3))
                    {
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                    }
                    else
                    {
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        if (tank.wheelGrounded)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                        thisInst.BOOST = true;
                    }
                    break;
            }
        }

        public static bool LollyGagAir(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind, bool holdGround = false)
        {
            bool isRegenerating = false;
            if (mind.Hurt)// && thisInst.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10)
            {
                var energy = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (energy.storageTotal > 500)
                    {
                        if (mind.SolarsAvail && tank.Anchors.NumPossibleAnchors > 0 && !tank.IsAnchored)
                        {
                            if (thisInst.anchorAttempts < 6)
                            {
                                thisInst.TryAnchor();
                                thisInst.anchorAttempts++;
                            }
                            else
                            {   //Try to find new spot
                                FlutterAround(thisInst, tank, mind);
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
                    if (thisInst.PendingDamageCheck) //&& thisInst.AttemptedRepairs < 3)
                    {
                        bool venPower = false;
                        if (mind.MainFaction == FactionTypesExt.VEN) venPower = true;
                        thisInst.PendingDamageCheck = RRepair.EnemyRepairStepper(thisInst, tank, mind, Super: venPower);
                        //thisInst.AttemptedRepairs++;
                        DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
                if (mind.CommanderSmarts >= EnemySmarts.IntAIligent)
                {
                    if (thisInst.PendingDamageCheck) //&& thisInst.AttemptedRepairs < 4)
                    {
                        if ((energy.storageTotal - energy.spareCapacity) / energy.storageTotal > 0.5)
                        {
                            //flex yee building speeds on them players
                            thisInst.PendingDamageCheck = !RRepair.EnemyInstaRepair(tank, mind);
                            //thisInst.AttemptedRepairs++;
                        }
                        else
                        {
                            bool venPower = false;
                            if (mind.MainFaction == FactionTypesExt.VEN) venPower = true;
                            thisInst.PendingDamageCheck = RRepair.EnemyRepairStepper(thisInst, tank, mind, Super: venPower);
                            //thisInst.AttemptedRepairs++;
                        }
                        //Debug.Log("TACtical_AI: Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
            }
            else
            {
                thisInst.anchorAttempts = 0;
            }

            //Debug.Log("TACtical_AI: Tech " + tank.name + " is lollygagging   " + mind.CommanderMind.ToString());

            if (holdGround)
                thisInst.lastDestination = mind.sceneStationaryPos;
            else
            {
                switch (mind.CommanderMind)
                {
                    case EnemyAttitude.Default: // do dumb stuff
                        FlutterAround(thisInst, tank, mind);
                        break;
                    case EnemyAttitude.Homing:  // Get nearest tech regardless of max combat range and attack them
                        RGeneral.HomingIdle(thisInst, tank, mind);
                        break;
                    case EnemyAttitude.Miner:   // mine resources
                        RMiner.MineYerOwnBusiness(thisInst, tank, mind);
                        break;
                    //The case below I still have to think of a reason for them to do the things
                    case EnemyAttitude.Junker:  // Huddle up by blocks on the ground
                        FlutterAround(thisInst, tank, mind);
                        break;
                }
            }
            if (mind.EvilCommander == EnemyHandling.Naval)
                thisInst.lastDestination = AIEPathing.OffsetToSea(thisInst.lastDestination, tank, thisInst);
            else if (mind.EvilCommander == EnemyHandling.Starship)
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
            else //Snap to ground
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst, tank.blockBounds.size.y);
            return isRegenerating;
        }
        public static void FlutterAround(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (thisInst.ActionPause == 1)
            {
                if (mind.GetComponent<AIControllerAir>() && UnityEngine.Random.Range(1, 10) < 6)
                {
                    var pilot = mind.GetComponent<AIControllerAir>();
                    thisInst.lastDestination = pilot.Tank.boundsCentreWorldNoCheck + (pilot.Tank.rbody.velocity * Time.deltaTime * KickStart.AIClockPeriod) + pilot.Tank.rootBlockTrans.forward;
                }
                thisInst.lastDestination = GetRANDPos(tank);
                thisInst.ActionPause = 0;
            }
            else if (thisInst.ActionPause == 0)
                thisInst.ActionPause = 30;
            thisInst.DriveDest = EDriveDest.ToLastDestination;
        }

        public static void EnemyDogfighting(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {   // Only accounts for forward weapons

            thisInst.AttackEnemy = false;
            thisInst.lastEnemy = mind.FindEnemyAir();

            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                thisInst.Urgency += KickStart.AIClockPeriod / 25;
                Vector3 foreDirect = tank.rootBlockTrans.InverseTransformDirection(aimTo);
                //if (KickStart.isWeaponAimModPresent && mind.CommanderAttack == EnemyAttack.Circle && ((AIControllerAir) thisInst.MovementController).LargeAircraft)
                //{   // AC-130 broadside attack
                //    if (Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) < 0.25f || Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) > -0.25f || thisInst.Urgency >= 30)
                //    {
                //        thisInst.DANGER = true;
                //        //thisInst.Urgency = 50;
                //        thisInst.SettleDown();
                //    }
                //}
                //else
                //{   // Normal Dogfighting
                if ((foreDirect.z > 0.15f && foreDirect.x > -0.5f && foreDirect.x < 0.5f) || thisInst.Urgency >= 30)
                {
                    thisInst.AttackEnemy = true;
                    //thisInst.Urgency = 50;
                    thisInst.SettleDown();
                }
                //}
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.AttackEnemy = false;
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
