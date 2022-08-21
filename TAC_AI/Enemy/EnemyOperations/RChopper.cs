﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RChopper
    {
        // ENEMY CONTROLLERS
        /*  
            Circle,     // Orbit while firing the target, and randomly switch directions every now and then
            Grudge,     // Chase whoever hit this Chopper last
            Coward,     // Avoid danger, and fly high
            Bully,      // Attack other aircraft over ground structures.  If inverted, prioritize ground structures over aircraft
            Pesterer,   // Randomly switch targets on 5 second intervals
            Spyper,     // OPPOSITE!!!  Bombs the enemy from high above instead! 
        */
        public static void TryFly(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            BGeneral.ResetValues(thisInst);
            thisInst.Attempt3DNavi = false;
            thisInst.AvoidStuff = true;

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemy != null)
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

            float enemyExt = thisInst.lastEnemy.GetCheapBounds();
            float dist = thisInst.GetDistanceFromTask(thisInst.lastEnemy.tank.boundsCentreWorldNoCheck, enemyExt);
            float range;
            float spacing = thisInst.lastTechExtents + enemyExt;

            switch (mind.CommanderAttack)
            {
                case EnemyAttack.Coward:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = true;
                    thisInst.DriveDest = EDriveDest.FromLastDestination;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - (Vector3.down * 50);
                    if (dist < spacing + (range / 4))
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    else if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                    }
                    break;
                case EnemyAttack.Circle:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.SideToThreat = true;
                    thisInst.Retreat = false;
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                    else
                        thisInst.SettleDown();
                    if (dist < spacing + 2)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                    }
                    else if (mind.Range < spacing + range)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    break;
                case EnemyAttack.Spyper:
                    if (mind.LikelyMelee)
                    {// Bomber
                        range = 8;
                        thisInst.SideToThreat = false;
                        thisInst.Retreat = false;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (dist < spacing + 2)
                        {
                            thisInst.DriveDest = EDriveDest.FromLastDestination;
                        }
                        else if (dist < spacing + range)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                        else
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                    }
                    else
                    {
                        range = AIGlobals.SpacingRangeSpyper;
                        thisInst.SideToThreat = false;
                        thisInst.Retreat = false;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (dist < spacing + range)
                        {
                            thisInst.DriveDest = EDriveDest.FromLastDestination;
                        }
                        else if (dist < spacing + (range * 1.25f))
                        {
                            thisInst.PivotOnly = true;
                        }
                        else if (dist < spacing + (range * 1.75f))
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                        else
                        {
                            thisInst.FeatherBoost = true;
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                            else
                                thisInst.SettleDown();
                        }
                    }

                    break;
                default:    // Others
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.SideToThreat = false;
                    thisInst.Retreat = false;
                    if (dist < spacing + 2)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.DriveDest = EDriveDest.FromLastDestination;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                    }
                    else if (dist < spacing + range)
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        thisInst.PivotOnly = true;
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else if (dist < spacing + (range * 1.25f))
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true);
                        else
                            thisInst.SettleDown();
                        thisInst.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
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
