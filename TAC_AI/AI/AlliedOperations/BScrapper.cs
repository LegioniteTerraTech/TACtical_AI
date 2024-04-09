using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BScrapper
    {
        internal const int reverseFromResourceTime = 35;
        internal const int reverseFromBaseTime = AIGlobals.ReverseDelay;
        public static void MotivateFind(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = (thisInst.DriverType == AIDriverType.Pilot || thisInst.DriverType == AIDriverType.Astronaut);
            float prevDist = thisInst.lastOperatorRange;
            float dist = thisInst.GetDistanceFromTask(thisInst.lastDestinationCore);
            bool needsToSlowDown = thisInst.IsOrbiting(thisInst.lastDestinationCore, dist - prevDist);
            bool hasMessaged = false;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst, ref direct);

            if (thisInst.AdvancedAI && thisInst.lastEnemyGet != null)
            {   //RUN!!!!!!!!
                if (!thisInst.foundBase)
                {
                    thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck,
                        thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, 
                        out Tank theBase, tank.Team);
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  There's no base nearby!  I AM LOST!!!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = theBase.GetCheapBounds();
                }
                else if (thisInst.theBase == null)
                {
                    thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck,
                        thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, 
                        out thisInst.theBase, tank.Team);
                    thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    return;
                }

                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived in safety of the base.");
                    thisInst.AvoidStuff = false;
                    thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  GET OUT OF THE WAY!  (dest base)");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Aaaah enemy!  Running back to base!");
                direct.DriveDest = EDriveDest.ToBase;
                return;
            }

            // VALIDATION CHECKS OF BLOCK HOLD FILL STATUS
            if (thisInst.CollectedTarget)
            {   // Unload all contents
                thisInst.CollectedTarget = false;
                if (thisInst.HeldBlock)
                {
                    thisInst.CollectedTarget = true;
                }
                else
                {
                    foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                    {
                        if (hold.BlockLoaded())
                        {
                            thisInst.CollectedTarget = true;
                            break;//Checking if tech is empty when unloading at base
                        }
                    }
                }
                if (!thisInst.CollectedTarget)
                    thisInst.actionPause = reverseFromResourceTime;
            }
            else
            {   // Gather materials
                thisInst.CollectedTarget = true;
                if (!thisInst.HeldBlock)
                    thisInst.CollectedTarget = false;
                /*
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    if (hold.BlockNotFullAndAvail())
                    {
                        thisInst.CollectedTarget = false;
                        break;//Checking if tech is full after destroying a node
                    }
                } */
                if (thisInst.CollectedTarget)
                    thisInst.actionPause = reverseFromBaseTime;
            }

            //DebugTAC_AI.Log(KickStart.ModID + ": Block is Present: " + thisInst.foundGoal);
            if (thisInst.CollectedTarget)
            {   // BRANCH - Return to base
                if (thisInst.ActionPause > 0)
                {   // BRANCH - Reverse from Resources
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from blocks...");
                    direct.Reverse(thisInst);
                    thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.rootBlockTrans.position, thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    direct.SetLastDest(thisInst.theBase.boundsCentreWorld);
                    dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestinationCore).magnitude;
                }
                thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                direct.DriveToFacingTowards();

                float spacing = thisInst.lastBaseExtremes + AIGlobals.MaxBlockGrabRangeAlt + thisInst.lastTechExtents;
                if (thisInst.DriverType == AIDriverType.Pilot)
                {
                    if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + AIGlobals.AircraftHailMaryRange)
                    {   // Final approach - turn off avoidence
                        thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                        thisInst.AvoidStuff = false;
                        if (thisInst.recentSpeed == 1)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.DriveVar = -1;
                            //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and dropping off payload..."); 
                            try
                            {
                                Visible blockHeld = thisInst.HeldBlock.visible;
                                thisInst.DropBlock((thisInst.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents))) - thisInst.HeldBlock.visible.centrePosition);
                                //blockHeld.centrePosition = thisInst.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents));
                            }
                            catch { }
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    }
                }
                else
                {
                    if (dist < spacing + 4)
                    {
                        thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                        if (thisInst.recentSpeed == 1)
                        {
                            direct.DriveAwayFacingTowards();
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.AvoidStuff = false;
                            thisInst.DriveVar = -1;
                            //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                            try
                            {
                                Visible blockHeld = thisInst.HeldBlock.visible;
                                thisInst.DropBlock((thisInst.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents))) - thisInst.HeldBlock.visible.centrePosition);
                                //blockHeld.centrePosition = thisInst.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents));
                            }
                            catch { }
                            direct.DriveToFacingTowards();
                            thisInst.AvoidStuff = false;
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < spacing + 12)
                    {
                        thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                        if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.AvoidStuff = false;
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                        }
                        else if (thisInst.recentSpeed < 8)
                        {
                            direct.DriveToFacingTowards();
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                            thisInst.AvoidStuff = false;
                            thisInst.Yield = true;
                        }
                        else
                        {
                            direct.DriveToFacingTowards();
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                            thisInst.AvoidStuff = false;
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < spacing + 18)
                    {
                        thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                        if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                        else
                        {
                            direct.DriveToFacingTowards();
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            //thisInst.Yield = true;
                            thisInst.SettleDown();
                            if (needsToSlowDown)
                                thisInst.Yield = true;
                        }
                    }
                    else if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    }
                    else if (needsToSlowDown)
                        thisInst.Yield = true;
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                direct.DriveDest = EDriveDest.ToBase;
                thisInst.foundGoal = false;
            }
            else
            {   // BRANCH - Go look for blocks
                if (thisInst.ActionPause > 0)
                {   // BRANCH - Reverse from Base
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                    direct.Reverse(thisInst);
                    thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                if (!thisInst.foundGoal)
                {
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FetchLooseBlocks(tank.rootBlockTrans.position, thisInst.JobSearchRange + AIGlobals.FindItemScanRangeExtension, out thisInst.theResource);
                    if (!thisInst.foundGoal)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for loose blocks...");
                        thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.rootBlockTrans.position, thisInst.JobSearchRange + 150, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Found a block...");
                        direct.SetLastDest(thisInst.theResource.centrePosition);
                        direct.DriveDest = EDriveDest.ToBase;
                        StopByBase(thisInst, tank, dist, ref hasMessaged, ref direct);
                        return;
                    }
                    direct.DriveDest = EDriveDest.ToBase;
                    StopByBase(thisInst, tank, dist, ref hasMessaged, ref direct);
                    return; // There's no resources left!
                }
                else if (thisInst.theResource != null)
                {
                    if (!thisInst.theResource.block || thisInst.theResource.block.IsAttached || thisInst.theResource.InBeam)
                    {
                        thisInst.theResource = null;
                        thisInst.DropBlock(Vector3.up);
                        thisInst.foundGoal = false;
                        DebugTAC_AI.Log(KickStart.ModID + ": Block was removed from targeting");
                        return;
                    }
                }
                direct.DriveToFacingTowards();

                float spacing = AIGlobals.MaxBlockGrabRange + thisInst.lastTechExtents;
                if (dist <= spacing && thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Grabbing block at " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    if (!thisInst.FullMelee)
                        thisInst.PivotOnly = true;
                    thisInst.SettleDown();
                    thisInst.HoldBlock(thisInst.theResource);
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (dist <= spacing)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at loose block at " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.HoldBlock(thisInst.theResource);
                    thisInst.SettleDown();
                }
                else if (needsToSlowDown)
                    thisInst.Yield = true;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to scavenge at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveDest = EDriveDest.ToMine;
                thisInst.foundBase = false;
            }
        }



        public static void StopByBase(TankAIHelper thisInst, Tank tank, float dist, ref bool hasMessaged, ref EControlOperatorSet direct)
        {
            if (thisInst.theBase == null)
                return; // There's no base!
            float girth = thisInst.lastBaseExtremes + thisInst.lastTechExtents;
            if (dist < girth + 3)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving room to base... |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                thisInst.AvoidStuff = false;
                direct.DriveDest = EDriveDest.FromLastDestination;
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
                thisInst.SettleDown();
            }
            else if (dist < girth + 7)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                thisInst.AvoidStuff = false;
                thisInst.Yield = true;
                thisInst.PivotOnly = true;
                thisInst.SettleDown();
            }
        }
    }
}
