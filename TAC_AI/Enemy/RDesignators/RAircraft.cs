using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
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
        public static void TryFly(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
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

                float enemyExt = AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents);
                float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.tank.boundsCentreWorldNoCheck).magnitude - enemyExt;
                float range = thisInst.RangeToStopRush + AIECore.Extremes(tank.blockBounds.extents);
                thisInst.lastRange = dist;

            if (mind.CommanderAttack == EnemyAttack.Coward)
            {
                thisInst.SideToThreat = false;
                thisInst.Retreat = true;
                thisInst.MoveFromObjective = true;
                if (dist < thisInst.lastTechExtents + enemyExt + (range / 4))
                {
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    thisInst.BOOST = true;
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
            }
            else if (mind.CommanderAttack == EnemyAttack.Circle)
            {
                thisInst.SideToThreat = true;
                thisInst.Retreat = false;
                if (tank.wheelGrounded)
                {
                    if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(true, dist, true, true);
                    else
                        thisInst.SettleDown();
                }
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
                    thisInst.MoveFromObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    thisInst.PivotOnly = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 2))
                {
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
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
                    thisInst.MoveFromObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    thisInst.PivotOnly = true;
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 1.25f))
                {
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    if (tank.wheelGrounded)
                    {
                        if (!tank.AI.IsTankMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(true, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    thisInst.BOOST = true;
                    thisInst.ProceedToObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
            }
        }

        public static void EnemyDogfighting(AIECore.TankAIHelper thisInst, Tank tank)
        {   // Will have to account for the different types of flight methods available

            thisInst.DANGER = false;
            thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                thisInst.Urgency += KickStart.AIClockPeriod / 5;
                if (KickStart.isWeaponAimModPresent && thisInst.SideToThreat && thisInst.Pilot.LargeAircraft)
                {   // AC-130 broadside attack
                    if (Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) < 0.25f || Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) > -0.25f || thisInst.Urgency >= 30)
                    {
                        thisInst.DANGER = true;
                        //thisInst.Urgency = 50;
                        thisInst.SettleDown();
                    }
                }
                else
                {   // Normal Dogfighting
                    if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.25f || thisInst.Urgency >= 30)
                    {
                        thisInst.DANGER = true;
                        //thisInst.Urgency = 50;
                        thisInst.SettleDown();
                    }
                }
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.DANGER = false;
            }
        }
    }
}
