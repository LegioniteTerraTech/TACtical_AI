using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.AlliedOperations;
using TAC_AI.AI.Enemy;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RScavenger
    {
        internal const int reverseFromResourceTime = 35;
        internal const int reverseFromBaseTime = AIGlobals.ReverseDelay;
        public static void Scavenge(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            helper.IsMultiTech = false;
            helper.Attempt3DNavi = mind.EvilCommander == EnemyHandling.Starship || mind.EvilCommander == EnemyHandling.Airplane || mind.EvilCommander == EnemyHandling.Chopper;
            
            //float prevDist = helper.lastOperatorRange;
            float dist = helper.GetDistanceFromTask(helper.lastDestinationCore);
            bool needsToSlowDown = helper.IsOrbiting();
            bool hasMessaged = false;
            helper.AvoidStuff = true;

            BGeneral.ResetValues(helper, ref direct);

            if (mind.CommanderSmarts >= EnemySmarts.Mild && helper.lastEnemyGet != null)
            {   //RUN!!!!!!!!
                if (!helper.foundBase)
                {
                    helper.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck, mind.MaxCombatRange + AIGlobals.FindBaseScanRangeExtension, out helper.lastBasePos, out Tank theBase, tank.Team);
                    if (!helper.foundBase)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return;
                    }
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  There's no base nearby!  I AM LOST!!!");
                    helper.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (theBase == null)
                        return; // There's no base!
                    helper.lastBaseExtremes = theBase.GetCheapBounds();
                }
                else if (helper.theBase == null)
                {
                    helper.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck, mind.MaxCombatRange + AIGlobals.FindBaseScanRangeExtension, out helper.lastBasePos, out helper.theBase, tank.Team);
                    if (!helper.foundBase)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return;
                    }
                    helper.lastBaseExtremes = helper.theBase.GetCheapBounds();
                    helper.EstTopSped = 1;//slow down the clock to reduce lagg
                    return;
                }

                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = 1;

                if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived in safety of the base.");
                    helper.AvoidStuff = false;
                    helper.actionPause -= KickStart.AIClockPeriod / 5;
                }
                else if (helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  GET OUT OF THE WAY!  (dest base)");
                    helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Aaaah enemy!  Running back to base!");
                direct.DriveDest = EDriveDest.ToBase;
                return;
            }

            if (helper.CollectedTarget)
            {   // Unload all contents
                helper.CollectedTarget = false;
                if (helper.HeldBlock)
                {
                    helper.CollectedTarget = true;
                }
                else
                {
                    foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                    {
                        if (hold.BlockLoaded())
                        {
                            helper.CollectedTarget = true;
                            break;//Checking if tech is empty when unloading at base
                        }
                    }
                }
                if (!helper.CollectedTarget)
                    helper.actionPause = reverseFromResourceTime;
            }
            else
            {   // Gather materials
                helper.CollectedTarget = true;
                if (!helper.HeldBlock)
                {
                    helper.CollectedTarget = false;
                }
                /*
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    if (hold.BlockNotFullAndAvail())
                    {
                        helper.CollectedTarget = false;
                        break;//Checking if tech is full after destroying a node
                    }
                }
                */
                if (helper.CollectedTarget)
                    helper.actionPause = reverseFromBaseTime;
            }

            //DebugTAC_AI.Log(KickStart.ModID + ": Block is Present: " + helper.foundGoal);
            if (helper.CollectedTarget)
            {   // BRANCH - Return to base
                if (helper.ActionPause > 0)
                {   // BRANCH - Reverse from Resources
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from blocks...");
                    direct.Reverse(helper);
                    helper.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                helper.foundBase = AIECore.FetchClosestBlockReceiver(tank.rootBlockTrans.position, mind.MaxCombatRange + AIGlobals.FindBaseScanRangeExtension, out helper.lastBasePos, out helper.theBase, tank.Team);
                if (!helper.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                    helper.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (helper.theBase == null)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return; // There's no base! 
                    }
                    direct.SetLastDest(helper.theBase.boundsCentreWorld);
                    dist = (tank.boundsCentreWorldNoCheck - helper.lastDestinationCore).magnitude;
                }
                helper.lastBaseExtremes = helper.theBase.GetCheapBounds();
                direct.DriveToFacingTowards();

                float spacing = helper.lastBaseExtremes + AIGlobals.MaxBlockGrabRangeAlt + helper.lastTechExtents;

                if (dist < spacing)
                {
                    helper.theBase.GetComponent<TankAIHelper>().SlowForApproacher(helper);
                    if (helper.recentSpeed == 1)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        helper.AvoidStuff = false;
                        helper.DriveVar = -1;
                        //helper.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                        try
                        {
                            Visible blockHeld = helper.HeldBlock.visible;
                            helper.DropBlock((helper.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents))) - helper.HeldBlock.visible.centrePosition);
                            //blockHeld.centrePosition = helper.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents));
                        }
                        catch { }
                        helper.AvoidStuff = false;
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        helper.ThrottleState = AIThrottleState.Yield;
                        helper.SettleDown();
                    }
                }
                else if (dist < spacing + 8)
                {
                    helper.theBase.GetComponent<TankAIHelper>().SlowForApproacher(helper);
                    if (helper.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        helper.AvoidStuff = false;
                        helper.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                    }
                    else if (helper.recentSpeed < 8)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                        helper.AvoidStuff = false;
                        helper.ThrottleState = AIThrottleState.Yield;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                        helper.AvoidStuff = false;
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        helper.ThrottleState = AIThrottleState.Yield;
                        helper.SettleDown();
                    }
                }
                else if (dist < spacing + 12)
                {
                    helper.theBase.GetComponent<TankAIHelper>().SlowForApproacher(helper);
                    if (helper.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                        helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        //helper.ThrottleState = AIThrottleState.Yield;
                        helper.SettleDown();
                        if (needsToSlowDown)
                            helper.ThrottleState = AIThrottleState.Yield;
                    }
                }
                else if (helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                    helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (needsToSlowDown)
                    helper.ThrottleState = AIThrottleState.Yield;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                direct.DriveDest = EDriveDest.ToBase;
                helper.foundGoal = false;
            }
            else
            {   // BRANCH - Go look for blocks
                if (helper.ActionPause > 0)
                {   // BRANCH - Reverse from Base
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                    direct.Reverse(helper);
                    helper.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                if (!helper.foundGoal)
                {
                    helper.EstTopSped = 1;//slow down the clock to reduce lagg
                    helper.foundGoal = AIECore.FetchLooseBlocks(tank.rootBlockTrans.position, mind.MaxCombatRange, out helper.theResource);
                    if (!helper.foundGoal)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Found a block...");
                        direct.SetLastDest(helper.theResource.centrePosition);
                        direct.DriveDest = EDriveDest.ToBase;
                        BScrapper.StopByBase(helper, tank, dist, ref hasMessaged, ref direct);
                        return;
                    }
                }
                else if (helper.theResource != null)
                {
                    if (!helper.theResource.block || helper.theResource.block.IsAttached || helper.theResource.InBeam)
                    {
                        helper.theResource = null;
                        helper.DropBlock(Vector3.up);
                        helper.foundGoal = false;
                        DebugTAC_AI.Log(KickStart.ModID + ": Block was removed from targeting");
                        return;
                    }
                }
                direct.DriveToFacingTowards();

                float spacing = AIGlobals.MaxBlockGrabRange + helper.lastTechExtents;
                if (dist <= spacing && helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Grabbing block at " + helper.theResource.centrePosition);
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.Yield;
                    if (!helper.FullMelee)
                        helper.ThrottleState = AIThrottleState.PivotOnly;
                    helper.SettleDown();
                    helper.HoldBlock(helper.theResource);
                }
                else if (helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (dist <= spacing)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at loose block at " + helper.theResource.centrePosition);
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.Yield;
                    helper.HoldBlock(helper.theResource);
                    helper.SettleDown();
                }
                else if (needsToSlowDown)
                    helper.ThrottleState = AIThrottleState.Yield;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to scavenge at " + helper.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveDest = EDriveDest.ToMine;
                helper.foundBase = false;
            }
        }
    }
}
