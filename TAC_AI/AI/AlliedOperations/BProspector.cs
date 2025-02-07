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
        public static void MotivateMine(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            helper.IsMultiTech = false;
            helper.Attempt3DNavi = (helper.DriverType == AIDriverType.Pilot || helper.DriverType == AIDriverType.Astronaut);

            Vector3 veloFlat = helper.SafeVelocity;
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            //float prevDist = helper.lastOperatorRange;
            float dist = helper.GetDistanceFromTask(helper.lastDestinationCore);
            bool needsToSlowDown = helper.IsOrbiting();
            bool hasMessaged = false;
            helper.AvoidStuff = true;

            BGeneral.ResetValues(helper, ref direct);

            if (helper.AdvancedAI && helper.lastEnemyGet != null)
            {   // BRANCH - RUN!!!!!!!!
                BGeneral.GetBase(helper, tank, false, ref dist, ref hasMessaged, ref direct);
                if (!BGeneral.GetBase(helper, tank, false, ref dist, ref hasMessaged, ref direct))
                {
                    if (!BGeneral.GetBase(helper, tank, true, ref dist, ref hasMessaged, ref direct))
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  There's no base nearby!  I AM LOST!!!");
                }
                else if (helper.theBase == null)
                {
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


            // VALIDATION CHECKS OF TRACTOR BED FILL STATUS
            if (helper.CollectedTarget)
            {
                helper.CollectedTarget = false;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsEmpty && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        helper.CollectedTarget = true;
                        break;//Checking if tech is empty when unloading at base
                    }
                }
                if (!helper.CollectedTarget)
                    helper.actionPause = reverseFromResourceTime;
            }
            else
            {
                helper.CollectedTarget = true;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsFull && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        helper.CollectedTarget = false;
                        break;//Checking if tech is full after destroying a node
                    }
                }
                if (helper.CollectedTarget)
                    helper.actionPause = reverseFromBaseTime;
            }

            // To Base
            // Our Chunk-Carrying tractor pads are filled to the brim with Chunks
            if (helper.CollectedTarget)
            {   // BRANCH - Head back to base
                if (helper.ActionPause > 0)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from resources...");
                    direct.Reverse(helper);
                    helper.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                if (!BGeneral.GetBase(helper, tank, helper.AdvancedAI, ref dist, ref hasMessaged, ref direct))
                {   // No base!!!
                    return;
                }

                if (tank.blockman.IterateBlockComponents<ModuleItemHolder>().Count() == 0)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at a base and applying brakes. | Tech is at " + tank.boundsCentreWorldNoCheck);
                    BGeneral.StopByBase(helper, tank, helper.AdvancedAI, ref dist, ref hasMessaged, ref direct);
                    direct.DriveDest = EDriveDest.ToBase;
                    return;
                }
                if (helper.DriverType == AIDriverType.Pilot)
                {
                    if (helper.MovementController.AICore is HelicopterAICore)
                    {   // Float over target and unload
                        float distFlat = (tank.boundsCentreWorldNoCheck - helper.theBase.boundsCentreWorldNoCheck).ToVector2XZ().magnitude;
                        if (distFlat < helper.lastBaseExtremes)
                        {   // Final approach - turn off avoidence
                            helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                            helper.AvoidStuff = false;
                            if (helper.recentSpeed == 1)
                            {
                                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                                helper.DriveVar = -1;
                                //helper.TryHandleObstruction(hasMessaged, dist, false, false);
                            }
                            else
                            {
                                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and dropping off payload...");
                                helper.actionPause -= KickStart.AIClockPeriod / 5;
                                helper.ThrottleState = AIThrottleState.Yield;
                                helper.SettleDown();
                                helper.DropAllItemsInCollectors();
                            }
                        }
                        else if (helper.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                            helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                    }
                    else
                    {   // Fly aircraft
                        if (dist < helper.lastBaseExtremes + helper.lastTechExtents + AIGlobals.AircraftHailMaryRange)
                        {   // Final approach - turn off avoidence
                            helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                            helper.AvoidStuff = false;
                            if (helper.recentSpeed == 1)
                            {
                                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                                helper.DriveVar = -1;
                                //helper.TryHandleObstruction(hasMessaged, dist, false, false);
                            }
                            else
                            {
                                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and dropping off payload...");
                                helper.actionPause -= KickStart.AIClockPeriod / 5;
                                helper.ThrottleState = AIThrottleState.Yield;
                                helper.SettleDown();
                                helper.DropAllItemsInCollectors();
                            }
                        }
                        else if (helper.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                            helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                    }
                }
                else
                {
                    if (dist < helper.lastBaseExtremes + helper.lastTechExtents)
                    {   // Final approach - turn off avoidence
                        helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                        helper.AvoidStuff = false;
                        if (helper.recentSpeed == 1)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            direct.DriveAwayFacingTowards();
                            helper.DriveVar = -1;
                            //helper.TryHandleObstruction(hasMessaged, dist, false, false);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                            helper.actionPause -= KickStart.AIClockPeriod / 5;
                            direct.DriveToFacingTowards();
                            helper.ThrottleState = AIThrottleState.Yield;
                            helper.SettleDown();
                        }
                    }
                    else if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 4)
                    {   // almost at the the base receiver - fine-tune and yield if nesseary for approach
                        helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                        helper.AvoidStuff = false;
                        if (helper.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            helper.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                            helper.ThrottleState = AIThrottleState.FullSpeed;
                        }
                        else if (helper.recentSpeed < 6)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                            direct.DriveAwayFacingAway();
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                            helper.actionPause -= KickStart.AIClockPeriod / 5;
                            direct.DriveToFacingTowards();
                            helper.ThrottleState = AIThrottleState.Yield;
                            helper.SettleDown();
                        }
                    }
                    else if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 8)
                    {   // Near the base, but not quite at the receiver 
                        helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                        if (helper.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                            helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                            helper.actionPause -= KickStart.AIClockPeriod / 5;
                            direct.DriveToFacingTowards();
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
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                direct.DriveDest = EDriveDest.ToBase;
                direct.SetLastDest(helper.lastBasePos.position);
                helper.foundGoal = false;
            }
            else
            {   // BRANCH - Go look for resources
                if (helper.ActionPause > 0)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                    direct.Reverse(helper);
                    helper.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                if (!helper.foundGoal)
                {   
                    helper.EstTopSped = 1;//slow down the clock to reduce lagg
                    BGeneral.GetMineableScenery(helper, tank, helper.AdvancedAI, ref dist, ref hasMessaged, ref direct);
                    return; // There's no resources left!
                }
                else if (helper.theResource != null)
                {
                    if (!helper.theResource.isActive || helper.theResource.GetComponent<ResourceDispenser>().IsDeactivated ||
                        helper.theResource.gameObject.GetComponent<Damageable>().Invulnerable)
                    {
                        AIECore.Minables.Remove(helper.theResource);
                        helper.theResource = null;
                        helper.foundGoal = false;
                        return;
                    }
                    else if (!(helper.theResource.centrePosition - tank.boundsCentreWorldNoCheck).WithinBox(
                        helper.JobSearchRange + AIGlobals.FindItemScanRangeExtension))
                    {
                        helper.theResource = null;
                        helper.foundGoal = false;
                        return;
                    }
                }
                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = 1;

                if (dist < helper.lastTechExtents + 3 + helper.AutoSpacing && helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Mining resource at " + helper.theResource.centrePosition);
                    direct.DriveToFacingTowards();
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.Yield;
                    if (!helper.FullMelee)
                        helper.ThrottleState = AIThrottleState.PivotOnly;
                    helper.SettleDown();
                    helper.RemoveObstruction(48);
                }
                else if (helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (dist < helper.lastTechExtents + 12 + helper.AutoSpacing)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at resource at " + helper.theResource.centrePosition);
                    direct.DriveToFacingTowards();
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.Yield;
                    helper.SettleDown();
                    helper.RemoveObstruction(48);
                }
                else if (needsToSlowDown)
                    helper.ThrottleState = AIThrottleState.Yield;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to mine at " + helper.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveDest = EDriveDest.ToMine;
                helper.foundBase = false;
            }
        }

    }
}
