using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RCrashMissile
    {   // Temp changed to coward AI

        public static void AttackCrash(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            BGeneral.ResetValues(helper, ref direct);
            helper.AvoidStuff = true;
            helper.Attempt3DNavi = false;
            if (mind.CommanderMind == EnemyAttitude.Homing && helper.lastEnemyGet.IsNotNull())
            {
                if ((helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude > mind.MaxCombatRange)
                {
                    bool isMending = RGeneral.LollyGag(helper, tank, mind, ref direct);
                    if (isMending)
                        return;
                }
            }
            if (helper.lastEnemyGet == null)
            {
                RGeneral.LollyGag(helper, tank, mind, ref direct);
                return;
            }
            RGeneral.Engadge(helper, tank, mind);

            float enemyExt = helper.lastEnemyGet.GetCheapBounds();
            float dist = helper.GetDistanceFromTask(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck, enemyExt);
            float range = AIGlobals.MinCombatRangeDefault + helper.lastTechExtents;
            float spacer = helper.lastTechExtents + enemyExt;

            helper.AISetSettings.SideToThreat = false;
            helper.Retreat = true;
            direct.DriveDest = EDriveDest.FromLastDestination;
            /*
            Vector3 runPlane = (helper.lastEnemy.tank.boundsCentreWorldNoCheck - helper.tank.boundsCentreWorldNoCheck).normalized * 100;
            if (ManWorld.inst.GetTerrainHeight(runPlane, out float height))
                runPlane.y = height;
            */
            direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            helper.ThrottleState = AIThrottleState.ForceSpeed;
            helper.DriveVar = 1;
            if (dist < spacer + (range / 4))
            {
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                else
                {
                    helper.SettleDown();
                    helper.FullBoost = true;
                }
            }
            else if (dist < spacer + range)
            {
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                else
                    helper.SettleDown();
            }
        }
        /*
        public static void RamTillDeath(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            if (helper.lastEnemy.IsNotNull())
            {
                helper.BOOST = true;
                helper.FullMelee = true;
                helper.Attempt3DNavi = true;
                if (helper.ActionPause > 12 && helper.ActionPause < 20)
                {
                    direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                if (helper.ActionPause > 20)
                {
                    helper.BOOST = true;
                    direct.lastDestination = helper.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    helper.BOOST = true;
                    direct.lastDestination = tank.boundsCentreWorldNoCheck + (Vector3.up * 100);
                    helper.ActionPause += KickStart.AIClockPeriod / 5;
                }
            }
        }*/
    }
}
