using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RChopper
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
        public static void AttackShwa(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.Attempt3DNavi = false;
            thisInst.AvoidStuff = true;

            if (mind.CommanderMind == EnemyAttitude.Homing && thisInst.lastEnemyGet != null)
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
            float prevDist = thisInst.lastOperatorRange;
            float dist = thisInst.GetDistanceFromTask(thisInst.lastDestinationCore);
            bool needsToSlowDown = thisInst.IsOrbiting(thisInst.lastDestinationCore, dist - prevDist, -1f);
            float range;
            float spacing = thisInst.lastTechExtents + enemyExt;

            switch (mind.CommanderAttack)
            {
                case EAttackMode.Safety:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.AISetSettings.ObjectiveRange = spacing + range;
                    thisInst.AISetSettings.SideToThreat = false;
                    thisInst.Retreat = true;
                    direct.DriveAwayFacingAway();
                    direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - (Vector3.down * 50));
                    if (dist < spacing + (range / 4))
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                    }
                    else if (dist < spacing + range)
                    {
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                    }
                    break;
                case EAttackMode.Circle:
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.AISetSettings.SideToThreat = true;
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                        thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                    else
                        thisInst.SettleDown();
                    if (dist < spacing + 2)
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        direct.DriveToFacingTowards();
                    }
                    else if (mind.MaxCombatRange < spacing + range)
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        direct.DriveToFacingPerp();
                    }
                    else
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        direct.DriveToFacingPerp();
                    }
                    break;
                case EAttackMode.Ranged:
                    if (mind.LikelyMelee)
                    {// Bomber
                        range = 8;
                        thisInst.AISetSettings.ObjectiveRange = spacing + range;
                        thisInst.AISetSettings.SideToThreat = false;
                        thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                        direct.DriveDest = EDriveDest.ToLastDestination;
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        if (dist < spacing + 2)
                        {
                            direct.DriveAwayFacingTowards();
                        }
                        else if (dist < spacing + range)
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                            else
                                thisInst.SettleDown();
                        }
                        else
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                            else
                                thisInst.SettleDown();
                        }
                    }
                    else
                    {
                        range = AIGlobals.SpacingRangeSpyperAir;
                        thisInst.AISetSettings.ObjectiveRange = spacing + range;
                        thisInst.AISetSettings.SideToThreat = false;
                        thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                        direct.DriveDest = EDriveDest.ToLastDestination;
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        if (dist < spacing + range)
                        {
                            direct.DriveAwayFacingTowards();
                        }
                        else if (dist < spacing + (range * 1.25f) || needsToSlowDown)
                        {
                            thisInst.PivotOnly = true;
                        }
                        else if (dist < spacing + (range * 1.75f))
                        {
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                            else
                                thisInst.SettleDown();
                        }
                        else
                        {
                            thisInst.LightBoost = true;
                            if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                                thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                            else
                                thisInst.SettleDown();
                        }
                    }

                    break;
                default:    // Others
                    range = AIGlobals.SpacingRangeHoverer;
                    thisInst.AISetSettings.ObjectiveRange = spacing + range;
                    thisInst.AISetSettings.SideToThreat = false;
                    thisInst.Retreat = RGeneral.CanRetreat(thisInst, tank, mind);
                    if (dist < spacing + 2)
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        direct.DriveAwayFacingTowards();
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                    }
                    else if (dist < spacing + range || needsToSlowDown)
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        thisInst.PivotOnly = true;
                        direct.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else if (dist < spacing + (range * 1.25f))
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        direct.DriveDest = EDriveDest.ToLastDestination;
                    }
                    else
                    {
                        direct.SetLastDest(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                        if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                            thisInst.TryHandleObstruction(!AIECore.Feedback, dist, true, true, ref direct);
                        else
                            thisInst.SettleDown();
                        thisInst.FullBoost = true;
                        direct.DriveDest = EDriveDest.ToLastDestination;
                    }
                    break;
            }
        }
    }
}
