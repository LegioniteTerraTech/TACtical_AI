using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RWheeled
    {
        public static void TryAttack(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = false;
            thisInst.AvoidStuff = true;

            float distToTarget = 0;
            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemy.IsNotNull())
            {
                distToTarget = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).magnitude;
                if (distToTarget > mind.Range)
                {
                    bool isMending = RGeneral.LollyGag(thisInst, tank, mind);
                    if (isMending)
                        return;
                }
            }
            if (thisInst.lastEnemy == null)
            {
                RGeneral.LollyGag(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            if (distToTarget == 0)
                distToTarget = thisInst.GetDistanceFromTask(thisInst.lastEnemy.tank.boundsCentreWorldNoCheck);

            float enemyExt = thisInst.lastEnemy.GetCheapBounds();
            float dist = distToTarget - enemyExt;
            float range;

            float spacer = thisInst.lastTechExtents + enemyExt;
            if (mind.MainFaction == FactionTypesExt.GC && mind.CommanderAttack != EnemyAttack.Coward)
                spacer = -32;// ram no matter what, or get close for snipers

            switch (mind.CommanderAttack)
            {
                case EnemyAttack.Coward:
                    range = AIGlobals.SpacingRange;
                    if ((bool)thisInst.lastEnemy)
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    else
                        RGeneral.Scurry(thisInst, tank, mind);
                    if (dist < spacer + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.BOOST = true;
                        }
                    }
                    else if (dist < spacer + (range * 2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    thisInst.DriveDest = EDriveDest.FromLastDestination;
                    break;
                case EnemyAttack.Circle:
                    range = AIGlobals.SpacingRange;
                    thisInst.SideToThreat = true;
                    thisInst.Retreat = false;
                    thisInst.MinimumRad = range;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (KickStart.isTweakTechPresent || KickStart.isWeaponAimModPresent)
                    {   // Continuous circle
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6) || 10 < thisInst.FrustrationMeter)
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.DriveDest = EDriveDest.ToLastDestination;
                            /*
                            if (dist < spacer + 2)
                            {
                                thisInst.DriveDest = EDriveDest.FromLastDestination;
                            }
                            else if (mind.Range < spacer + range)
                            {
                                thisInst.DriveDest = EDriveDest.ToLastDestination;
                            }
                            else
                            {
                                thisInst.BOOST = true;
                                thisInst.DriveDest = EDriveDest.ToLastDestination;
                            }*/
                        }
                    }
                    else
                    {   // Stop every now and then to allow some shots
                        if (thisInst.ActionPause > 120)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6) || 10 < thisInst.FrustrationMeter)
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                            {
                                thisInst.SettleDown();
                                thisInst.DriveDest = EDriveDest.ToLastDestination;
                            }
                        }
                        if (thisInst.ActionPause > 0)
                        {   // Stop moving and get some shots in
                            thisInst.SideToThreat = false;
                        }
                        else if (mind.Hurt)
                        {
                            thisInst.ActionPause = UnityEngine.Random.Range(160, 420);
                        }
                    }
                    break;
                case EnemyAttack.Spyper:
                    range = AIGlobals.SpacingRangeSpyper;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (dist < spacer + (range * 0.65f))
                    {
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                        thisInst.ForceSetDrive = true;
                        thisInst.DriveVar = -1;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();

                    }
                    else if (dist < spacer + range)
                    {
                        thisInst.SettleDown();
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                    }
                    else if (dist < spacer + (range * 1.25f))
                    {
                        thisInst.PivotOnly = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination; // point at the objective
                        thisInst.SettleDown();
                    }
                    else if (dist < spacer + (range * 1.5f))
                    {
                        thisInst.PivotOnly = true;
                        thisInst.ForceSetDrive = true;
                        thisInst.DriveVar = 1;
                        thisInst.DriveDest = EDriveDest.ToLastDestination; // point at the objective
                        thisInst.SettleDown();
                    }
                    else if (dist < spacer + (range * 1.75f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.DriveDest = EDriveDest.ToLastDestination;
                        }
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.BOOST = true;
                            thisInst.DriveDest = EDriveDest.ToLastDestination;
                        };
                    }
                    break;
                default:    // Others
                    range = AIGlobals.SpacingRange;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (dist < spacer)
                    {   // too close?
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6) && !mind.LikelyMelee)
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            if (mind.LikelyMelee)
                                thisInst.DriveDest = EDriveDest.ToLastDestination;
                            else
                                thisInst.DriveDest = EDriveDest.FromLastDestination;
                        }
                    }
                    else if (dist < spacer + range)
                    {   // 
                        thisInst.PivotOnly = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else if (dist < spacer + (range * 1.25f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.DriveDest = EDriveDest.ToLastDestination;
                        }
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 6))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.BOOST = true;
                            thisInst.DriveDest = EDriveDest.ToLastDestination;
                        }
                    }
                    break;
            }
        }
    }
}
