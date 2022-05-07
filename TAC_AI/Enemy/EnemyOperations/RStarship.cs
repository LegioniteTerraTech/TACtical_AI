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
            float dist = (tank.boundsCentreWorld - thisInst.lastEnemy.tank.boundsCentreWorld).magnitude - enemyExt;
            float range;
            float spacing = thisInst.lastTechExtents + enemyExt;
            thisInst.lastRange = dist;

            thisInst.ForceSetDrive = true;
            thisInst.DriveVar = 1;
            switch (mind.CommanderAttack)
            {
                case EnemyAttack.Coward:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    thisInst.MoveFromObjective = true;
                    if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.BOOST = true;
                    }
                    else if (dist < spacing + (range*2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
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
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                    else
                        thisInst.SettleDown();
                    // Melee makes the AI ignore the avoid, making the AI ram into the enemy
                    if (!mind.LikelyMelee && dist < spacing + 2)
                    {
                        thisInst.MoveFromObjective = true;
                    }
                    else if (mind.Range < spacing + range)
                    {
                        thisInst.ProceedToObjective = true;
                    }
                    else
                    {
                        thisInst.BOOST = true;
                        thisInst.ProceedToObjective = true;
                    }
                    break;
                case EnemyAttack.Spyper:// Spyper does not support melee
                    range = AIGlobals.SpacingRangeSpyper;
                    thisInst.SideToThreat = false; // cannot strafe while firing shells it seems
                    thisInst.Retreat = false;
                    if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.MoveFromObjective = true;
                    }
                    else if (dist < spacing + (range * 1.5f))
                    {
                        thisInst.PivotOnly = true;
                        
                    }
                    else if (dist < spacing + (range * 2))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.ProceedToObjective = true;
                        
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.BOOST = true;
                        thisInst.ProceedToObjective = true;
                        
                    }
                    break;
                default:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    if (!mind.LikelyMelee && dist < spacing + 2)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.MoveFromObjective = true;
                        
                    }
                    else if (!mind.LikelyMelee && dist < spacing + range)
                    {
                        thisInst.PivotOnly = true;
                        thisInst.ProceedToObjective = true;
                        
                    }
                    else if (dist < spacing + (range * 1.25f))
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.ProceedToObjective = true;
                        
                    }
                    else
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.BOOST = true;
                        thisInst.ProceedToObjective = true;
                        
                    }
                    break;
            }
        }
    }
}
