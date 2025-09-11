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
    internal static class RWheeled
    {
        private static void MoveSideways(TankAIHelper helper, float dist, ref EControlOperatorSet direct)
        {   // Continuous circle
            helper.AISetSettings.SideToThreat = true;
            if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend)
                || 10 < helper.FrustrationMeter)
                helper.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
            else
            {
                helper.SettleDown();
                direct.DriveToFacingPerp();
                /*
                if (dist < spacer + 2)
                {
                    direct.DriveDest = EDriveDest.FromLastDestination;
                }
                else if (mind.Range < spacer + range)
                {
                    direct.DriveDest = EDriveDest.ToLastDestination;
                }
                else
                {
                    helper.BOOST = true;
                    direct.DriveDest = EDriveDest.ToLastDestination;
                }*/
            }
        }
        public static void AttackVroom(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            BGeneral.ResetValues(helper, ref direct);
            helper.Attempt3DNavi = false;
            helper.AvoidStuff = true;

            //DebugTAC_AI.Log("RWheeled.TryAttack - " + tank.name);

            float distToTarget = 0;
            if (mind.CommanderMind == EnemyAttitude.Homing && helper.lastEnemyGet.IsNotNull())
            {
                distToTarget = (tank.boundsCentreWorldNoCheck - helper.lastEnemyGet.tank.boundsCentreWorldNoCheck).magnitude;
                if (distToTarget > mind.MaxCombatRange)
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

            if (distToTarget == 0)
                distToTarget = helper.GetDistanceFromTask(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);

            float enemyExt = helper.lastEnemyGet.GetCheapBounds();
            float dist = distToTarget - enemyExt;
            float range;

            float spacer = helper.lastTechExtents + enemyExt;
            if (mind.MainFaction == FactionSubTypes.GC && mind.CommanderAttack != EAttackMode.Safety)
                spacer = -32;// ram no matter what, or get close for snipers

            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    range = AIGlobals.MinCombatRangeDefault;
                    helper.AISetSettings.ObjectiveRange = spacer + range;
                    if ((bool)helper.lastEnemyGet)
                        direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    else
                        RGeneral.Scurry(helper, tank, mind);
                    helper.AttackEnemy = true;
                    if (dist < spacer + range)
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                        {
                            helper.SettleDown();
                            helper.FullBoost = true;
                        }
                    }
                    else if (dist < spacer + (range * 2))
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                            helper.SettleDown();
                    }
                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = true;
                    direct.DriveAwayFacingAway();
                    break;
                case EAttackMode.Circle:
                    range = AIGlobals.MinCombatRangeDefault;
                    helper.AISetSettings.ObjectiveRange = spacer + range;
                    helper.AISetSettings.SideToThreat = true;
                    helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                    helper.AutoSpacing = range;
                    direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    if (helper.BlockedLineOfSight || KickStart.isTweakTechPresent || KickStart.isWeaponAimModPresent)
                    {   // Continuous circle
                        MoveSideways(helper, dist, ref direct);
                    }
                    else
                    {   // Stop every now and then to allow some shots
                        if (helper.ActionPause > 120)
                        {
                            if (!helper.IsTechMovingAbs(helper.EstTopSped / (AIGlobals.EnemyAISpeedPanicDividend * 2)))
                                //|| 10 < helper.FrustrationMeter)
                                helper.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                            else
                            {
                                helper.SettleDown();
                                direct.DriveToFacingPerp();
                            }
                        }
                        else
                        { // Stop moving and get some shots in
                            helper.AISetSettings.SideToThreat = false;
                            helper.SettleDown();
                            direct.DriveToFacingTowards();
                            if (mind.Hurt)
                                helper.actionPause = UnityEngine.Random.Range(160, 420);
                        }
                    }
                    break;
                case EAttackMode.Ranged:
                    range = AIGlobals.MinCombatRangeSpyper;
                    helper.AISetSettings.ObjectiveRange = spacer + range;
                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                    direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);

                    if (dist < spacer + (range * 0.65f))
                    {
                        direct.DriveAwayFacingTowards();
                        helper.ThrottleState = AIThrottleState.ForceSpeed;
                        helper.DriveVar = -1;
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                            helper.SettleDown();
                    }
                    else if (dist < spacer + range)
                    {
                        if (helper.BlockedLineOfSight)
                            MoveSideways(helper, dist, ref direct);
                        else
                        {
                            helper.SettleDown();
                            direct.DriveAwayFacingTowards();
                        }
                    }
                    else if (dist < spacer + (range * 1.25f))
                    {
                        if (helper.BlockedLineOfSight)
                            MoveSideways(helper, dist, ref direct);
                        else
                        {
                            helper.ThrottleState = AIThrottleState.PivotOnly;
                            direct.DriveToFacingTowards(); // point at the objective
                            helper.SettleDown();
                        }
                    }
                    else if (dist < spacer + (range * 1.5f))
                    {
                        helper.ThrottleState = AIThrottleState.ForceSpeed;
                        helper.DriveVar = 1;
                        direct.DriveToFacingTowards(); // point at the objective
                        helper.SettleDown();
                    }
                    else if (dist < spacer + (range * 1.75f))
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                        {
                            helper.SettleDown();
                            direct.DriveToFacingTowards();
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
                            direct.DriveDest = EDriveDest.ToLastDestination;
                        };
                    }
                    break;
                default:    // Others
                    range = AIGlobals.MinCombatRangeDefault;
                    helper.AISetSettings.ObjectiveRange = spacer + range;
                    helper.AISetSettings.SideToThreat = false;
                    helper.Retreat = RGeneral.CanRetreat(helper, tank, mind);
                    direct.SetLastDest(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    if (dist < spacer)
                    {   // too close?
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend) && !mind.LikelyMelee)
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                        {
                            helper.SettleDown();
                            if (mind.LikelyMelee)
                                direct.DriveToFacingTowards();
                            else
                                direct.DriveAwayFacingTowards();
                        }
                    }
                    else if (dist < spacer + range)
                    {   // 
                        if (helper.BlockedLineOfSight)
                            MoveSideways(helper, dist, ref direct);
                        else
                        {
                            helper.ThrottleState = AIThrottleState.PivotOnly;
                            direct.DriveDest = EDriveDest.ToLastDestination;
                        }
                    }
                    else if (dist < spacer + (range * 1.25f))
                    {
                        if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            helper.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                        {
                            helper.SettleDown();
                            direct.DriveToFacingTowards();
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
                            direct.DriveToFacingTowards();
                        }
                    }
                    break;
            }
            mind.MinCombatRange = range;
        }
    }
}
