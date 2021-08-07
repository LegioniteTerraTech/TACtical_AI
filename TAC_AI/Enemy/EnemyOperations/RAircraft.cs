using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
                RGeneral.LollyGag(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents);
            float dist = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude - enemyExt;
            float range = EnemyMind.SpacingRangeAir + AIECore.Extremes(tank.blockBounds.extents);
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
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    if (tank.wheelGrounded)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
            }
            else if (mind.CommanderAttack == EnemyAttack.Circle)
            {
                thisInst.SideToThreat = true;
                thisInst.Retreat = false;
                if (tank.wheelGrounded)
                {
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
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
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 2))
                {
                    if (tank.wheelGrounded)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
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
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
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
                thisInst.ProceedToObjective = true;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                if (dist < thisInst.lastTechExtents + enemyExt + 2)
                {
                    thisInst.MoveFromObjective = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    if (tank.wheelGrounded)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + range)
                {
                }
                else if (dist < thisInst.lastTechExtents + enemyExt + (range * 1.25f))
                {
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
                    if (tank.wheelGrounded)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    thisInst.BOOST = true;
                }
            }
        }

        public static void EnemyDogfighting(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {   // Only accounts for forward weapons

            thisInst.DANGER = false;
            thisInst.lastEnemy = mind.FindEnemyAir();

            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                thisInst.Urgency += KickStart.AIClockPeriod / 25;
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
                    if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.4f || thisInst.Urgency >= 30)
                    {
                        thisInst.DANGER = true;
                        //thisInst.Urgency = 50;
                        thisInst.SettleDown();
                    }
                //}
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.DANGER = false;
            }
        }
    }
}
