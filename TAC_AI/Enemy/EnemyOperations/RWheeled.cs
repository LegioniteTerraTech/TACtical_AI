using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RWheeled
    {
        public static void AttackVroom(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.Attempt3DNavi = false;
            thisInst.AvoidStuff = true;

            //DebugTAC_AI.Log("RWheeled.TryAttack - " + tank.name);

            float distToTarget = 0;
            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemyGet.IsNotNull())
            {
                distToTarget = (tank.boundsCentreWorldNoCheck - thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck).magnitude;
                if (distToTarget > mind.MaxCombatRange)
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

            if (distToTarget == 0)
                distToTarget = thisInst.GetDistanceFromTask(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);

            float enemyExt = thisInst.lastEnemyGet.GetCheapBounds();
            float dist = distToTarget - enemyExt;
            float range;

            float spacer = thisInst.lastTechExtents + enemyExt;
            if (mind.MainFaction == FactionTypesExt.GC && mind.CommanderAttack != EAttackMode.Safety)
                spacer = -32;// ram no matter what, or get close for snipers

            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    range = AIGlobals.MinCombatRangeDefault;
                    thisInst.AILimitSettings.ObjectiveRange = spacer + range;
                    if ((bool)thisInst.lastEnemyGet)
                        direct.lastDestination = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                    else
                        RGeneral.Scurry(thisInst, tank, mind);
                    if (dist < spacer + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.FullBoost = true;
                            thisInst.AttackEnemy = true;
                        }
                    }
                    else if (dist < spacer + (range * 2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                            thisInst.SettleDown();
                    }
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    direct.DriveAwayFacingAway();
                    break;
                case EAttackMode.Circle:
                    range = AIGlobals.MinCombatRangeDefault;
                    thisInst.AILimitSettings.ObjectiveRange = spacer + range;
                    thisInst.SideToThreat = true;
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    thisInst.MinimumRad = range;
                    direct.lastDestination = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                    if (KickStart.isTweakTechPresent || KickStart.isWeaponAimModPresent)
                    {   // Continuous circle
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend)
                            || 10 < thisInst.FrustrationMeter)
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                        {
                            thisInst.SettleDown();
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
                                thisInst.BOOST = true;
                                direct.DriveDest = EDriveDest.ToLastDestination;
                            }*/
                        }
                    }
                    else
                    {   // Stop every now and then to allow some shots
                        if (thisInst.ActionPause > 120)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                                //|| 10 < thisInst.FrustrationMeter)
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                            else
                            {
                                thisInst.SettleDown();
                                direct.DriveToFacingPerp();
                            }
                        }
                        else
                        { // Stop moving and get some shots in
                            thisInst.SideToThreat = false;
                            thisInst.SettleDown();
                            direct.DriveToFacingTowards();
                            if (mind.Hurt)
                                thisInst.ActionPause = UnityEngine.Random.Range(160, 420);
                        }
                    }
                    break;
                case EAttackMode.Ranged:
                    range = AIGlobals.MinCombatRangeSpyper;
                    thisInst.AILimitSettings.ObjectiveRange = spacer + range;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    direct.lastDestination = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                    
                    /*if (dist < spacer + (range * 0.65f))
                    {
                        direct.DriveAwayFacingTowards();
                        thisInst.ForceSetDrive = true;
                        thisInst.DriveVar = -1;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                            thisInst.SettleDown();
                    }
                    else */if (dist < spacer + range)
                    {
                        thisInst.SettleDown();
                        direct.DriveAwayFacingTowards();
                    }
                    else if (dist < spacer + (range * 1.25f))
                    {
                        thisInst.PivotOnly = true;
                        direct.DriveToFacingTowards(); // point at the objective
                        thisInst.SettleDown();
                    }
                    else if (dist < spacer + (range * 1.5f))
                    {
                        thisInst.PivotOnly = true;
                        thisInst.ForceSetDrive = true;
                        thisInst.DriveVar = 1;
                        direct.DriveToFacingTowards(); // point at the objective
                        thisInst.SettleDown();
                    }
                    else if (dist < spacer + (range * 1.75f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                        {
                            thisInst.SettleDown();
                            direct.DriveToFacingTowards();
                        }
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.FullBoost = true;
                            direct.DriveDest = EDriveDest.ToLastDestination;
                        };
                    }
                    break;
                default:    // Others
                    range = AIGlobals.MinCombatRangeDefault;
                    thisInst.AILimitSettings.ObjectiveRange = spacer + range;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    direct.lastDestination = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                    if (dist < spacer)
                    {   // too close?
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend) && !mind.LikelyMelee)
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                        {
                            thisInst.SettleDown();
                            if (mind.LikelyMelee)
                                direct.DriveToFacingTowards();
                            else
                                direct.DriveAwayFacingTowards();
                        }
                    }
                    else if (dist < spacer + range)
                    {   // 
                        thisInst.PivotOnly = true;
                        direct.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else if (dist < spacer + (range * 1.25f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, false, true, ref direct);
                        else
                        {
                            thisInst.SettleDown();
                            direct.DriveToFacingTowards();
                        }
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                        {
                            thisInst.SettleDown();
                            thisInst.FullBoost = true;
                            direct.DriveToFacingTowards();
                        }
                    }
                    break;
            }
            mind.MinCombatRange = range;
        }
    }
}
