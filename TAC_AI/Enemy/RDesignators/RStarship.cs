using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy
{
    public class RStarship
    {
        //Same as RWheeled but has multi-plane support
        public static void TryAttack(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = true;

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemy.IsNotNull())
            {
                if ((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude > mind.Range)
                {
                    bool isMending = RGeneral.LollyGag(thisInst, tank, mind);
                    if (isMending)
                        return;
                }
            }
            else if (thisInst.lastEnemy == null)
            {
                RGeneral.LollyGag(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents);
            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).magnitude - enemyExt;
            float range = thisInst.RangeToStopRush + AIECore.Extremes(tank.blockBounds.extents);
            thisInst.lastRange = dist;

            thisInst.forceDrive = true;
            thisInst.DriveVar = 1;
            if (mind.CommanderAttack == EnemyAttack.Coward)
            {
                thisInst.SideToThreat = false;
                thisInst.Retreat = true;
                thisInst.MoveFromObjective = true;
                if (dist < thisInst.lastTechExtents + enemyExt + (range / 4))
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    thisInst.BOOST = true;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
            }
            else if (mind.CommanderAttack == EnemyAttack.Circle)
            {
                thisInst.SideToThreat = true;
                thisInst.Retreat = false;
                if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                    thisInst.TryHandleObstruction(true, dist, true, true);
                else
                    thisInst.SettleDown();
                if (dist < thisInst.lastTechExtents + enemyExt + 2)
                {
                    thisInst.MoveFromObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (mind.Range < thisInst.lastTechExtents + enemyExt + range)
                {
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    thisInst.BOOST = true;
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
            }
            else if (mind.CommanderAttack == EnemyAttack.Spyper)
            {
                thisInst.SideToThreat = true;
                thisInst.Retreat = false;
                if (dist < thisInst.lastTechExtents + enemyExt + (range / 2))
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.MoveFromObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    thisInst.PivotOnly = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 2))
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.BOOST = true;
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
            }
            else
            {
                thisInst.SideToThreat = false;
                thisInst.Retreat = false;
                if (dist < thisInst.lastTechExtents + enemyExt + 2)
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.MoveFromObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    thisInst.PivotOnly = true;
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 1.25f))
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    thisInst.BOOST = true;
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
            }
        }
    }
}
