using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TAC_AI.AI.Movement.AICores;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BProspector
    {
        internal const int reverseFromResourceTime = 35;
        internal const int reverseFromBaseTime = AIGlobals.ReverseDelay;
        public static void MotivateMine(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = (thisInst.DriverType == AIDriverType.Pilot || thisInst.DriverType == AIDriverType.Astronaut);

            Vector3 veloFlat = Vector3.zero;
            if ((bool)tank.rbody)   // So that drifting is minimized
            {
                veloFlat = tank.rbody.velocity;
                veloFlat.y = 0;
            }
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            float prevDist = thisInst.lastOperatorRange;
            float dist = thisInst.GetDistanceFromTask(thisInst.lastDestinationCore);
            bool needsToSlowDown = thisInst.IsOrbiting(thisInst.lastDestinationCore, dist - prevDist);
            bool hasMessaged = false;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst, ref direct);

            if (thisInst.AdvancedAI && thisInst.lastEnemyGet != null)
            {   // BRANCH - RUN!!!!!!!!
                if (!thisInst.foundBase)
                {
                    thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.boundsCentreWorldNoCheck, thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out Tank theBase, tank.Team);
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  There's no base nearby!  I AM LOST!!!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = theBase.GetCheapBounds();
                }
                else if (thisInst.theBase == null)
                {
                    thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.boundsCentreWorldNoCheck, thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
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


            // VALIDATION CHECKS OF TRACTOR BED FILL STATUS
            if (thisInst.CollectedTarget)
            {
                thisInst.CollectedTarget = false;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsEmpty && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        thisInst.CollectedTarget = true;
                        break;//Checking if tech is empty when unloading at base
                    }
                }
                if (!thisInst.CollectedTarget)
                    thisInst.actionPause = reverseFromResourceTime;
            }
            else
            {
                thisInst.CollectedTarget = true;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsFull && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        thisInst.CollectedTarget = false;
                        break;//Checking if tech is full after destroying a node
                    }
                }
                if (thisInst.CollectedTarget)
                    thisInst.actionPause = reverseFromBaseTime;
            }

            // To Base
            // Our Chunk-Carrying tractor pads are filled to the brim with Chunks
            if (thisInst.CollectedTarget)
            {   // BRANCH - Head back to base
                if (thisInst.ActionPause > 0)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from resources...");
                    direct.Reverse(thisInst);
                    thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.rootBlockTrans.position, thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                    {
                        direct.STOP(thisInst);
                        return; // There's no base!
                    }
                    thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    direct.SetLastDest(thisInst.theBase.boundsCentreWorld);
                    dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestinationCore).magnitude;
                }
                /*
                else if (thisInst.lastBasePos.IsNull())
                {
                    thisInst.foundBase = AIEnhancedCore.FetchClosestHarvestReceiver(tank.rootBlockTrans.position, thisInst.DetectionRange + 150, out thisInst.lastBasePos, out thisInst.theBase);
                    thisInst.lastBaseExtremes = AIEnhancedCore.Extremes(thisInst.theBase.blockBounds.extents); 
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    return;
                }
                */
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() == 0)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. | Tech is at " + tank.boundsCentreWorldNoCheck);
                    StopByBase(thisInst, tank, dist, ref hasMessaged, ref direct);
                    direct.DriveDest = EDriveDest.ToBase;
                    return;
                }
                if (thisInst.DriverType == AIDriverType.Pilot)
                {
                    if (thisInst.MovementController.AICore is HelicopterAICore)
                    {   // Float over target and unload
                        float distFlat = (tank.boundsCentreWorldNoCheck - thisInst.theBase.boundsCentreWorldNoCheck).ToVector2XZ().magnitude;
                        if (distFlat < thisInst.lastBaseExtremes)
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
                                thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                                thisInst.Yield = true;
                                thisInst.SettleDown();
                                thisInst.DropAllItemsInCollectors();
                            }
                        }
                        else if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                    }
                    else
                    {   // Fly aircraft
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
                                thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                                thisInst.Yield = true;
                                thisInst.SettleDown();
                                thisInst.DropAllItemsInCollectors();
                            }
                        }
                        else if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                    }
                }
                else
                {
                    if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents)
                    {   // Final approach - turn off avoidence
                        thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                        thisInst.AvoidStuff = false;
                        if (thisInst.recentSpeed == 1)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            direct.DriveAwayFacingTowards();
                            thisInst.DriveVar = -1;
                            //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            direct.DriveToFacingTowards();
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 4)
                    {   // almost at the the base receiver - fine-tune and yield if nesseary for approach
                        thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                        thisInst.AvoidStuff = false;
                        if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                            thisInst.Yield = false;
                        }
                        else if (thisInst.recentSpeed < 6)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                            direct.DriveAwayFacingAway();
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            direct.DriveToFacingTowards();
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                    {   // Near the base, but not quite at the receiver 
                        thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                        if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            direct.DriveToFacingTowards();
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
                direct.SetLastDest(thisInst.lastBasePos.position);
                thisInst.foundGoal = false;
            }
            else
            {   // BRANCH - Go look for resources
                if (thisInst.ActionPause > 0)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                    direct.Reverse(thisInst);
                    thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                if (!thisInst.foundGoal)
                {   
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FetchClosestResource(tank.rootBlockTrans.position, 
                        thisInst.JobSearchRange + AIGlobals.FindItemScanRangeExtension, thisInst.lastTechExtents * AIGlobals.WaterDepthTechHeightPercent
                        , out thisInst.theResource);
                    if (!thisInst.foundGoal)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for resources...");
                        thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.rootBlockTrans.position, thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Found a Resource Node...");
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
                    if (!thisInst.theResource.isActive || thisInst.theResource.GetComponent<ResourceDispenser>().IsDeactivated || thisInst.theResource.gameObject.GetComponent<Damageable>().Invulnerable)
                    {
                        AIECore.Minables.Remove(thisInst.theResource);
                        thisInst.theResource = null;
                        thisInst.foundGoal = false;
                        return;
                    }
                }
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastTechExtents + 3 + thisInst.MinimumRad && thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Mining resource at " + thisInst.theResource.centrePosition);
                    direct.DriveToFacingTowards();
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    if (!thisInst.FullMelee)
                        thisInst.PivotOnly = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction(48);
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (dist < thisInst.lastTechExtents + 12 + thisInst.MinimumRad)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at resource at " + thisInst.theResource.centrePosition);
                    direct.DriveToFacingTowards();
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction(48);
                }
                else if (needsToSlowDown)
                    thisInst.Yield = true;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to mine at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
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
                direct.DriveAwayFacingTowards();
                thisInst.AvoidStuff = false;
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
                thisInst.SettleDown();
            }
            else if (dist < girth + 7)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                direct.DriveToFacingTowards();
                thisInst.AvoidStuff = false;
                thisInst.Yield = true;
                thisInst.PivotOnly = true;
                thisInst.SettleDown();
            }
        }
    }
}
