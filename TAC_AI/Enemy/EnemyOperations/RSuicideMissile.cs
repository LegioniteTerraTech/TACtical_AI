using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RSuicideMissile
    {   // Temp changed to coward AI

        public static void RamTillDeath(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            BGeneral.ResetValues(thisInst);
            thisInst.AvoidStuff = true;
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

            thisInst.SideToThreat = false;
            thisInst.Retreat = true;
            thisInst.MoveFromObjective = true;
            /*
            Vector3 runPlane = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - thisInst.tank.boundsCentreWorldNoCheck).normalized * 100;
            if (ManWorld.inst.GetTerrainHeight(runPlane, out float height))
                runPlane.y = height;
            */
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
        /*
        public static void RamTillDeath(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (thisInst.lastEnemy.IsNotNull())
            {
                thisInst.BOOST = true;
                thisInst.FullMelee = true;
                thisInst.Attempt3DNavi = true;
                if (thisInst.ActionPause > 12 && thisInst.ActionPause < 20)
                {
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                if (thisInst.ActionPause > 20)
                {
                    thisInst.BOOST = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    thisInst.BOOST = true;
                    thisInst.lastDestination = tank.boundsCentreWorldNoCheck + (Vector3.up * 100);
                    thisInst.ActionPause += KickStart.AIClockPeriod / 5;
                }
            }
        }*/
    }
}
