using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RNaval
    {
        //Same as RWheeled but has terrain avoidence
        public static void TryAttack(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = true;
            thisInst.AvoidStuff = true;

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

            float enemyExt = thisInst.lastEnemy.GetCheapBounds();
            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).magnitude - enemyExt;
            float range = AIGlobals.SpacingRange + thisInst.lastTechExtents;
            thisInst.lastRange = dist;
            float spacer = thisInst.lastTechExtents + enemyExt;
            if (mind.MainFaction == FactionTypesExt.GC && mind.CommanderAttack != EnemyAttack.Coward)
                spacer = -32;// ram no matter what, or get close for snipers

            thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
            if (mind.CommanderAttack == EnemyAttack.Coward)
            {
                thisInst.SideToThreat = false;
                thisInst.Retreat = true;
                thisInst.MoveFromObjective = true;
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
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                    thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                else
                {
                    thisInst.SettleDown();
                    if (dist < thisInst.lastTechExtents + enemyExt + 2)
                    {
                        thisInst.MoveFromObjective = true;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    else if (mind.Range < spacer + range)
                    {
                        thisInst.ProceedToObjective = true;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
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
                else if (dist < spacer + range)
                {
                    thisInst.PivotOnly = true;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 2))
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
            else
            {
                thisInst.SideToThreat = false;
                thisInst.Retreat = false;
                if (dist < spacer + 2)
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                    else
                    {
                        thisInst.MoveFromObjective = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < spacer + range)
                {
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
