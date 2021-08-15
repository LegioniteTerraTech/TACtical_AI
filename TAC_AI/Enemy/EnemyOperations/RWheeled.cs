using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RWheeled
    {
        public static void TryAttack(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = false;
            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemy.IsNotNull())
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
                RGeneral.LollyGag(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents);
            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).magnitude - enemyExt;
            float range = EnemyMind.SpacingRange + AIECore.Extremes(tank.blockBounds.extents);
            thisInst.lastRange = dist;
            float spacer = thisInst.lastTechExtents + enemyExt;
            if (mind.MainFaction == FactionSubTypes.GC && mind.CommanderAttack != EnemyAttack.Coward)
                spacer = -32;// ram no matter what, or get close for snipers

            if (mind.CommanderAttack == EnemyAttack.Coward)
            {
                thisInst.SideToThreat = false;
                thisInst.Retreat = true;
                thisInst.MoveFromObjective = true;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
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
            }
            else if (mind.CommanderAttack == EnemyAttack.Circle)
            {
                thisInst.SideToThreat = true;
                thisInst.Retreat = false;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
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
            }
            else if (mind.CommanderAttack == EnemyAttack.Spyper)
            {
                thisInst.SideToThreat = true;
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
            }
            else
            {   // Others
                thisInst.SideToThreat = false;
                thisInst.Retreat = false;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                if (dist < spacer - 2)
                {   // too close?
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
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
            }
        }
    }
}
