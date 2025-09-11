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
    internal static class RNaval
    {
        //Same as RWheeled but has terrain avoidence
        public static void AttackWhish(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(helper, ref direct);
            helper.Attempt3DNavi = true;
            helper.AvoidStuff = true;

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

            if (mind.MainFaction == FactionSubTypes.GC && mind.CommanderAttack != EAttackMode.Safety)
                spacer = -32;// ram no matter what, or get close for snipers

            direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
            if (mind.CommanderAttack == EAttackMode.Safety)
            {
                helper.AISetSettings.SideToThreat = false;
                helper.Retreat = true;
                direct.DriveAwayFacingAway();
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
            else if (mind.CommanderAttack == EAttackMode.Circle)
            {
                helper.AISetSettings.SideToThreat = true;
                helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                    helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                else
                {
                    helper.SettleDown();
                    if (dist < helper.lastTechExtents + enemyExt + 2)
                    {
                        direct.DriveAwayFacingPerp();
                        direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    }
                    else if (mind.MaxCombatRange < spacer + range)
                    {
                        direct.DriveToFacingPerp();
                        direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    }
                    else
                    {
                        direct.DriveToFacingPerp();
                        helper.FullBoost = true;
                    }
                }
            }
            else if (mind.CommanderAttack == EAttackMode.Ranged)
            {
                helper.AISetSettings.SideToThreat = true;
                helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                if (dist < spacer + (range / 2))
                {
                    if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        helper.SettleDown();
                        direct.DriveAwayFacingTowards();
                    }
                }
                else if (dist < spacer + range)
                {
                    helper.ThrottleState = AIThrottleState.PivotOnly;
                }
                else if (dist < helper.lastTechExtents + enemyExt + (range * 2))
                {
                    if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        helper.SettleDown();
                        direct.DriveToFacingPerp();
                    }
                }
                else
                {
                    if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        helper.SettleDown();
                        helper.FullBoost = true;
                        direct.DriveToFacingPerp();
                    }
                }
            }
            else
            {
                helper.AISetSettings.SideToThreat = false;
                helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                if (dist < spacer + 2)
                {
                    if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        direct.DriveAwayFacingTowards();
                        helper.SettleDown();
                    }
                }
                else if (dist < spacer + range)
                {
                    helper.ThrottleState = AIThrottleState.PivotOnly;
                    direct.DriveToFacingPerp();
                }
                else if (dist < spacer + (range * 1.25f))
                {
                    if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        helper.SettleDown();
                        direct.DriveToFacingPerp();
                    }
                }
                else
                {
                    if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                    {
                        helper.SettleDown();
                        helper.FullBoost = true;
                        direct.DriveToFacingPerp();
                    }
                }
            }
        }
    }
}
