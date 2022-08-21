using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RStarship
    {
        //Same as RWheeled but has multi-plane support
        /// <summary>
        /// Positions are handled by the AI Core
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        public static void TryAttack(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = true;
            thisInst.AvoidStuff = true;

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemy?.tank)
            {
                if ((thisInst.lastEnemy.tank.boundsCentreWorld - tank.boundsCentreWorld).magnitude > mind.Range)
                {
                    bool isMending = RGeneral.LollyGag(thisInst, tank, mind);
                    if (isMending)
                        return;
                }
            }
            else if (!thisInst.lastEnemy?.tank)
            {
                RGeneral.LollyGag(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            float enemyExt = thisInst.lastEnemy.GetCheapBounds();
            float dist = thisInst.GetDistanceFromTask(thisInst.lastEnemy.tank.boundsCentreWorldNoCheck, enemyExt);
            float range;
            float spacing = thisInst.lastTechExtents + enemyExt;

            thisInst.ForceSetDrive = true;
            thisInst.DriveVar = 1;
            switch (mind.CommanderAttack)
            {
                case EnemyAttack.Coward:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    thisInst.DriveDest = EDriveDest.FromLastDestination;
                    if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.BOOST = true;
                    }
                    else if (dist < spacing + (range*2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    break;
                case EnemyAttack.Circle:
                    range = AIGlobals.SpacingRangeHoverer;
                    //thisInst.SideToThreat = true;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    thisInst.MinimumRad = range;
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                    else
                        thisInst.SettleDown();
                    // Melee makes the AI ignore the avoid, making the AI ram into the enemy
                    if (!mind.LikelyMelee && dist < spacing + 2)
                    {
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                    }
                    else if (mind.Range < spacing + range)
                    {
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else
                    {
                        thisInst.BOOST = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    break;
                case EnemyAttack.Spyper:// Spyper does not support melee
                    range = AIGlobals.SpacingRangeSpyper;
                    thisInst.SideToThreat = false; // cannot strafe while firing shells it seems
                    thisInst.Retreat = false;
                    if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                    }
                    else if (dist < spacing + (range * 1.5f))
                    {
                        thisInst.PivotOnly = true;
                        
                    }
                    else if (dist < spacing + (range * 2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.BOOST = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        
                    }
                    break;
                default:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    if (!mind.LikelyMelee && dist < spacing + 2)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                        
                    }
                    else if (!mind.LikelyMelee && dist < spacing + range)
                    {
                        thisInst.PivotOnly = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        
                    }
                    else if (dist < spacing + (range * 1.25f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.BOOST = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    break;
            }
        }
    }
}
