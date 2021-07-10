using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RChopper
    {
        // ENEMY CONTROLLERS
        /*  
            Circle,     // Orbit while firing the target, and randomly switch directions every now and then
            Grudge,     // Chase whoever hit this Chopper last
            Coward,     // Avoid danger, and fly high
            Bully,      // Attack other aircraft over ground structures.  If inverted, prioritize ground structures over aircraft
            Pesterer,   // Randomly switch targets on 5 second intervals
            Spyper,     // OPPOSITE!!!  Bombs the enemy from high above instead! 
        */
        public static void TryFly(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = false;

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemy != null)
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

            try
            {
                float enemyExt = AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents);
                float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).magnitude - enemyExt;
                float range = thisInst.RangeToStopRush + AIECore.Extremes(tank.blockBounds.extents);
                thisInst.lastRange = dist;

                if (mind.CommanderAttack == EnemyAttack.Coward)
                {
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    thisInst.MoveFromObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - (Vector3.down * 50);
                    if (dist < thisInst.lastTechExtents + enemyExt + (range / 4))
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    else if (dist < thisInst.lastTechExtents + enemyExt + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                }
                else if (mind.CommanderAttack == EnemyAttack.Circle)
                {   // Orbit strafe while attacking
                    thisInst.SideToThreat = true;
                    thisInst.Retreat = false;
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                    if (dist < thisInst.lastTechExtents + enemyExt + 2)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.MoveFromObjective = true;
                    }
                    else if (mind.Range < thisInst.lastTechExtents + enemyExt + range)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.ProceedToObjective = true;
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.ProceedToObjective = true;
                    }
                }
                else if (mind.CommanderAttack == EnemyAttack.Spyper)
                {   // Bomber
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    thisInst.ProceedToObjective = true;
                    if (dist < thisInst.lastTechExtents + (enemyExt / 2))
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.MoveFromObjective = true;
                    }
                    else if (dist < thisInst.lastTechExtents + enemyExt + range)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
                else
                {
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    if (dist < thisInst.lastTechExtents + enemyExt + 2)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.MoveFromObjective = true;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    else if (dist < thisInst.lastTechExtents + enemyExt + range)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.PivotOnly = true;
                        thisInst.ProceedToObjective = true;
                    }
                    else if (dist < thisInst.lastTechExtents + enemyExt + (range * 1.25f))
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.ProceedToObjective = true;
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.BOOST = true;
                        thisInst.ProceedToObjective = true;
                    }
                }
            }
            catch { }
        }
    }
}
