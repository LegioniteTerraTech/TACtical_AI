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
    internal static class RStarship
    {
        //Same as RWheeled but has multi-plane support
        /// <summary>
        /// Positions are handled by the AI Core
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        public static void AttackZoom(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(helper, ref direct);
            helper.Attempt3DNavi = true;
            helper.AvoidStuff = true;

            if (mind.CommanderMind == EnemyAttitude.Homing && helper.lastEnemyGet?.tank)
            {
                if ((helper.lastEnemyGet.tank.boundsCentreWorld - tank.boundsCentreWorld).magnitude > mind.MaxCombatRange)
                {
                    bool isMending = RGeneral.LollyGag(helper, tank, mind, ref direct);
                    if (isMending)
                        return;
                }
            }
            else if (!helper.lastEnemyGet?.tank)
            {
                RGeneral.LollyGag(helper, tank, mind, ref direct);
                return;
            }
            RGeneral.Engadge(helper, tank, mind);

            float enemyExt = helper.lastEnemyGet.GetCheapBounds();
            float dist = helper.GetDistanceFromTask(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck, enemyExt);
            bool needsToSlowDown = helper.IsOrbiting();
            float range;
            float spacing = helper.lastTechExtents + enemyExt;

            helper.ThrottleState = AIThrottleState.ForceSpeed;
            helper.DriveVar = 1;
            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    range = AIGlobals.SpacingRangeHoverer;
                    helper.AISetSettings.ObjectiveRange = spacing + range;
                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = true;
                    direct.DriveAwayFacingTowards();
                    if (dist < spacing + range)
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                        helper.FullBoost = true;
                    }
                    else if (dist < spacing + (range*2))
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                    }
                    break;
                case EAttackMode.Circle:
                    range = AIGlobals.SpacingRangeHoverer;
                    helper.AISetSettings.ObjectiveRange = spacing + range;
                    //helper.SideToThreat = true;
                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                    helper.AutoSpacing = range;
                    if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                        helper.SettleDown();
                    // Melee makes the AI ignore the avoid, making the AI ram into the enemy
                    if (!mind.LikelyMelee && dist < spacing + 2)
                    {
                        direct.DriveAwayFacingPerp();
                    }
                    else if (mind.MaxCombatRange < spacing + range)
                    {
                        direct.DriveToFacingPerp();
                    }
                    else
                    {
                        helper.FullBoost = true;
                        direct.DriveToFacingPerp();
                    }
                    break;
                case EAttackMode.Ranged:// Spyper does not support melee
                    range = AIGlobals.SpacingRangeSpyperAir;
                    helper.AISetSettings.ObjectiveRange = spacing + range;
                    helper.AISetSettings.SideToThreat = false; // cannot strafe while firing shells it seems, will miss
                    helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                    if (needsToSlowDown)
                        helper.ThrottleState = AIThrottleState.Yield;
                    if (dist < spacing + range)
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                        direct.DriveAwayFacingTowards();
                        helper.ThrottleState = AIThrottleState.ForceSpeed;
                        helper.DriveVar = 1;
                    }
                    else if (dist < spacing + (range * 1.5f))
                    {
                        helper.ThrottleState = AIThrottleState.PivotOnly;
                        /*
                        direct.DriveDest = EDriveDest.None;
                        direct.DriveDir = EDriveFacing.Forwards;*/
                        direct.DriveAwayFacingTowards();
                    }
                    else if (dist < spacing + (range * 2))
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                        direct.DriveToFacingTowards();
                    }
                    else
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                        helper.FullBoost = true;
                        direct.DriveToFacingTowards();
                    }
                    break;
                default:
                    range = AIGlobals.SpacingRangeHoverer;
                    helper.AISetSettings.ObjectiveRange = spacing + range;
                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                    if (needsToSlowDown)
                        helper.ThrottleState = AIThrottleState.Yield;
                    if (!mind.LikelyMelee && dist < spacing + 2)
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                        direct.DriveAwayFacingTowards();

                    }
                    else if (!mind.LikelyMelee && dist < spacing + range)
                    {
                        helper.ThrottleState = AIThrottleState.PivotOnly;
                        direct.DriveToFacingTowards();
                        
                    }
                    else if (dist < spacing + (range * 1.25f))
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                        direct.DriveToFacingTowards();
                    }
                    else
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            helper.SettleDown();
                        helper.FullBoost = true;
                        direct.DriveToFacingTowards();
                    }
                    break;
            }
            mind.MinCombatRange = range;
        }
    }
}
