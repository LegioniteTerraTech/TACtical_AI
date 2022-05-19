using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TAC_AI.AI.Movement.AICores;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BProspector
    {
        public static void MotivateMine(AIECore.TankAIHelper thisInst, Tank tank)
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
            float dist = (tank.boundsCentreWorldNoCheck + veloFlat - thisInst.lastDestination).magnitude;
            bool hasMessaged = false;
            thisInst.lastRange = dist;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst);

            if (thisInst.AdvancedAI && thisInst.lastEnemy != null)
            {   // BRANCH - RUN!!!!!!!!
                if (!thisInst.foundBase)
                {
                    thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.boundsCentreWorldNoCheck, tank.Radar.Range + AIGlobals.FindBaseExtension, out thisInst.lastBasePos, out Tank theBase, tank.Team);
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, "TACtical_AI:AI " + tank.name + ":  There's no base nearby!  I AM LOST!!!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = theBase.GetCheapBounds();
                }
                else if (thisInst.theBase == null)
                {
                    thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.boundsCentreWorldNoCheck, tank.Radar.Range + AIGlobals.FindBaseExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
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
                    thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  GET OUT OF THE WAY!  (dest base)");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Aaaah enemy!  Running back to base!");
                thisInst.DriveDest = EDriveDest.ToBase;
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
                thisInst.ActionPause = AIGlobals.ReverseDelay;
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
            }

            // Our Chunk-Carrying tractor pads are filled to the brim with Chunks
            if (thisInst.CollectedTarget || thisInst.ActionPause > 10)
            {   // BRANCH - Head back to base
                thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.rootBlockTrans.position, tank.Radar.Range + AIGlobals.FindBaseExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    thisInst.lastDestination = thisInst.theBase.boundsCentreWorld;
                    dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestination).magnitude;
                }
                /*
                else if (thisInst.lastBasePos.IsNull())
                {
                    thisInst.foundBase = AIEnhancedCore.FetchClosestHarvestReceiver(tank.rootBlockTrans.position, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase);
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
                    StopByBase(thisInst, tank, dist, ref hasMessaged);
                    thisInst.DriveDest = EDriveDest.ToBase;
                    return;
                }
                if (thisInst.DriverType == AIDriverType.Pilot)
                {
                    if (thisInst.MovementController.AICore is HelicopterAICore)
                    {   // Float over target and unload
                        float distFlat = (tank.boundsCentreWorldNoCheck - thisInst.theBase.boundsCentreWorldNoCheck).ToVector2XZ().magnitude;
                        if (distFlat < thisInst.lastBaseExtremes)
                        {   // Final approach - turn off avoidence
                            thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
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
                                thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                                thisInst.Yield = true;
                                thisInst.SettleDown();
                                thisInst.DropAllItemsInCollectors();
                            }
                        }
                        else if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                        }
                    }
                    else
                    {   // Fly aircraft
                        if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + AIGlobals.AircraftHailMaryRange)
                        {   // Final approach - turn off avoidence
                            thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
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
                                thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                                thisInst.Yield = true;
                                thisInst.SettleDown();
                                thisInst.DropAllItemsInCollectors();
                            }
                        }
                        else if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                        }
                    }
                }
                else
                {
                    if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents)
                    {   // Final approach - turn off avoidence
                        thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
                        thisInst.AvoidStuff = false;
                        if (thisInst.recentSpeed == 1)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.DriveVar = -1;
                            //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                            thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 4)
                    {   // almost at the the base receiver - fine-tune and yield if nesseary for approach
                        thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
                        thisInst.AvoidStuff = false;
                        if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                        }
                        else if (thisInst.recentSpeed < 8)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                            thisInst.Yield = true;
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                            thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                    {   // Near the base, but not quite at the receiver 
                        thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
                        if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                            thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                            //thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                    }
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                thisInst.DriveDest = EDriveDest.ToBase;
                thisInst.foundGoal = false;
            }
            else if (thisInst.ActionPause > 0)
            {
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
            }
            else
            {   // BRANCH - Go look for resources
                if (!thisInst.foundGoal)
                {   
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FetchClosestResource(tank.rootBlockTrans.position, tank.Radar.Range + AIGlobals.FindItemExtension, out thisInst.theResource);
                    if (!thisInst.foundGoal)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for resources...");
                        thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.rootBlockTrans.position, tank.Radar.Range + AIGlobals.FindBaseExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Found a Resource Node...");
                        thisInst.lastDestination = thisInst.theResource.centrePosition;
                        thisInst.DriveDest = EDriveDest.ToBase;
                        StopByBase(thisInst, tank, dist, ref hasMessaged);
                        return;
                    }
                    thisInst.DriveDest = EDriveDest.ToBase;
                    StopByBase(thisInst, tank, dist, ref hasMessaged);
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
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                else if (dist < thisInst.lastTechExtents + 12 + thisInst.MinimumRad)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at resource at " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction(48);
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to mine at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.DriveDest = EDriveDest.ToMine;
                thisInst.foundBase = false;
            }
        }

        public static void StopByBase(AIECore.TankAIHelper thisInst, Tank tank, float dist, ref bool hasMessaged)
        {
            if (thisInst.theBase == null)
                return; // There's no base!
            float girth = thisInst.lastBaseExtremes + thisInst.lastTechExtents;
            if (dist < girth + 3)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving room to base... |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
                thisInst.AvoidStuff = false;
                thisInst.AdviseAway = true;
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
                thisInst.SettleDown();
            }
            else if (dist < girth + 7)
            {   // We are at the base, stop moving and hold pos
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
                thisInst.AvoidStuff = false;
                thisInst.Yield = true;
                thisInst.PivotOnly = true;
                thisInst.SettleDown();
            }
        }
    }
}
