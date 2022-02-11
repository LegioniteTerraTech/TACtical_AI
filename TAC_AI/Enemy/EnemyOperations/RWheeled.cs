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
                distToTarget = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).magnitude;

            float enemyExt = AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents);
            float dist = distToTarget - enemyExt;
            float range = EnemyMind.SpacingRange + AIECore.Extremes(tank.blockBounds.extents);
            thisInst.lastRange = dist;
            float spacer = thisInst.lastTechExtents + enemyExt;
            if (mind.MainFaction == FactionTypesExt.GC && mind.CommanderAttack != EnemyAttack.Coward)
                spacer = -32;// ram no matter what, or get close for snipers

            switch (mind.CommanderAttack)
            {
                case EnemyAttack.Coward:
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    thisInst.MoveFromObjective = true;
                    if ((bool)thisInst.lastEnemy)
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    else
                        RGeneral.Scurry(thisInst, tank, mind);
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 1;
                    if (dist < spacer + (range / 4))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.BOOST = true;
                        }
                    }
                    else if (dist < spacer + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    break;
                case EnemyAttack.Circle:
                    thisInst.SideToThreat = true;
                    thisInst.Retreat = false;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8) || 10 < thisInst.FrustrationMeter)
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                    else
                    {
                        thisInst.SettleDown();
                        if (dist < spacer + 2)
                        {
                            thisInst.MoveFromObjective = true;
                        }
                        else if (mind.Range < spacer + range)
                        {
                            thisInst.ProceedToObjective = true;
                        }
                        else
                        {
                            thisInst.BOOST = true;
                            thisInst.ProceedToObjective = true;
                        }
                    }
                    break;
                case EnemyAttack.Spyper:
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (dist < spacer + (range / 2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.MoveFromObjective = true;
                        }
                    }
                    else if (dist < thisInst.lastTechExtents + enemyExt + range)
                    {
                        thisInst.PivotOnly = true;
                    }
                    else if (dist < spacer + (range * 2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.ProceedToObjective = true;
                        }
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.BOOST = true;
                            thisInst.ProceedToObjective = true;
                        };
                    }
                    break;
                default:    // Others
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (dist < spacer - 2)
                    {   // too close?
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8) && mind.LikelyMelee)
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.MoveFromObjective = true;
                        }
                    }
                    else if (dist < spacer + range)
                    {   // 
                        thisInst.PivotOnly = true;
                        thisInst.ProceedToObjective = true;
                    }
                    else if (dist < spacer + (range * 1.25f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.ProceedToObjective = true;
                        }
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.BOOST = true;
                            thisInst.ProceedToObjective = true;
                        }
                    }
                    break;
            }
        }
    }
}
