using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RCrashMissile
    {   // Temp changed to coward AI

        public static void AttackCrash(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.AvoidStuff = true;
            thisInst.Attempt3DNavi = false;
            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemyGet.IsNotNull())
            {
                if ((thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude > mind.MaxCombatRange)
                {
                    bool isMending = RGeneral.LollyGag(thisInst, tank, mind, ref direct);
                    if (isMending)
                        return;
                }
            }
            if (thisInst.lastEnemyGet == null)
            {
                RGeneral.LollyGag(thisInst, tank, mind, ref direct);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = thisInst.lastEnemyGet.GetCheapBounds();
            float dist = thisInst.GetDistanceFromTask(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck, enemyExt);
            float range = AIGlobals.MinCombatRangeDefault + thisInst.lastTechExtents;
            float spacer = thisInst.lastTechExtents + enemyExt;

            thisInst.SideToThreat = false;
            thisInst.Retreat = true;
            direct.DriveDest = EDriveDest.FromLastDestination;
            /*
            Vector3 runPlane = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - thisInst.tank.boundsCentreWorldNoCheck).normalized * 100;
            if (ManWorld.inst.GetTerrainHeight(runPlane, out float height))
                runPlane.y = height;
            */
            direct.lastDestination = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck;
            thisInst.ForceSetDrive = true;
            thisInst.DriveVar = 1;
            if (dist < spacer + (range / 4))
            {
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                else
                {
                    thisInst.SettleDown();
                    thisInst.FullBoost = true;
                }
            }
            else if (dist < spacer + range)
            {
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
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
                    direct.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                if (thisInst.ActionPause > 20)
                {
                    thisInst.BOOST = true;
                    direct.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    thisInst.BOOST = true;
                    direct.lastDestination = tank.boundsCentreWorldNoCheck + (Vector3.up * 100);
                    thisInst.ActionPause += KickStart.AIClockPeriod / 5;
                }
            }
        }*/
    }
}
