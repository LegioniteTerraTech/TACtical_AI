using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RStarship
    {
        //Same as RWheeled but has multi-plane support
        /// <summary>
        /// Positions are handled by the AI Core
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        public static void AttackZoom(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.Attempt3DNavi = true;
            thisInst.AvoidStuff = true;

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemyGet?.tank)
            {
                if ((thisInst.lastEnemyGet.tank.boundsCentreWorld - tank.boundsCentreWorld).magnitude > mind.MaxCombatRange)
                {
                    bool isMending = RGeneral.LollyGag(thisInst, tank, mind, ref direct);
                    if (isMending)
                        return;
                }
            }
            else if (!thisInst.lastEnemyGet?.tank)
            {
                RGeneral.LollyGag(thisInst, tank, mind, ref direct);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = thisInst.lastEnemyGet.GetCheapBounds();
            float dist = thisInst.GetDistanceFromTask(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck, enemyExt);
            float range;
            float spacing = thisInst.lastTechExtents + enemyExt;

            thisInst.ForceSetDrive = true;
            thisInst.DriveVar = 1;
            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.AISetSettings.ObjectiveRange = spacing + range;
                    thisInst.AISetSettings.SideToThreat = false;
                    thisInst.Retreat = true;
                    direct.DriveAwayFacingTowards();
                    if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        thisInst.FullBoost = true;
                    }
                    else if (dist < spacing + (range*2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                    }
                    break;
                case EAttackMode.Circle:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.AISetSettings.ObjectiveRange = spacing + range;
                    //thisInst.SideToThreat = true;
                    thisInst.AISetSettings.SideToThreat = false;
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    thisInst.MinimumRad = range;
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                        thisInst.SettleDown();
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
                        thisInst.FullBoost = true;
                        direct.DriveToFacingPerp();
                    }
                    break;
                case EAttackMode.Ranged:// Spyper does not support melee
                    range = AIGlobals.SpacingRangeSpyperAir;
                    thisInst.AISetSettings.ObjectiveRange = spacing + range;
                    thisInst.AISetSettings.SideToThreat = false; // cannot strafe while firing shells it seems, will miss
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        direct.DriveAwayFacingTowards();
                        thisInst.ForceSetDrive = true;
                        thisInst.DriveVar = 1;
                    }
                    else if (dist < spacing + (range * 1.5f))
                    {
                        thisInst.PivotOnly = true;
                        /*
                        direct.DriveDest = EDriveDest.None;
                        direct.DriveDir = EDriveFacing.Forwards;*/
                        direct.DriveAwayFacingTowards();
                    }
                    else if (dist < spacing + (range * 2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        direct.DriveToFacingTowards();
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        thisInst.FullBoost = true;
                        direct.DriveToFacingTowards();
                    }
                    break;
                default:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.AISetSettings.ObjectiveRange = spacing + range;
                    thisInst.AISetSettings.SideToThreat = false;
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    if (!mind.LikelyMelee && dist < spacing + 2)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        direct.DriveAwayFacingTowards();

                    }
                    else if (!mind.LikelyMelee && dist < spacing + range)
                    {
                        thisInst.PivotOnly = true;
                        direct.DriveToFacingTowards();
                        
                    }
                    else if (dist < spacing + (range * 1.25f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        direct.DriveToFacingTowards();
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        thisInst.FullBoost = true;
                        direct.DriveToFacingTowards();
                    }
                    break;
            }
            mind.MinCombatRange = range;
        }
    }
}
