using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI.AlliedOperations;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal static class RMiner
    {
        public static void MineYerOwnBusiness(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            int errorCode = 0;
            try
            {
                Vector3 veloFlat = Vector3.zero;
                if ((bool)tank.rbody)   // So that drifting is minimized
                {
                    veloFlat = helper.SafeVelocity;
                    veloFlat.y = 0;
                }
                float prevDist = helper.lastOperatorRange;
                float dist = helper.GetDistanceFromTask(helper.lastDestinationCore);
                bool needsToSlowDown = helper.IsOrbiting();
                bool hasMessaged = false;
                helper.AvoidStuff = true;
                errorCode = 1;

                BGeneral.ResetValues(helper, ref direct);

                // VALIDATION CHECKS OF TRACTOR BED FILL STATUS
                if (helper.CollectedTarget)
                {
                    errorCode = 2;
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
                        helper.actionPause = BProspector.reverseFromResourceTime;
                }
                else
                {
                    errorCode = 3;
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
                        helper.actionPause = BProspector.reverseFromBaseTime;
                }
                errorCode = 4;

                if (helper.CollectedTarget)
                {
                    errorCode = 5;
                    if (helper.ActionPause > 0)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from resources...");
                        direct.Reverse(helper);
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        return;
                    }
                    errorCode = 6;
                    if (!BGeneral.GetBase(helper, tank, true, ref dist, ref hasMessaged, ref direct))
                    {   // No base!!!
                        return;
                    }

                    errorCode = 8;
                    if (helper.MovementController is AIControllerAir)
                    {
                        if (helper.MovementController.AICore is Movement.AICores.HelicopterAICore)
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
                        if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 1)
                        {
                            if (helper.recentSpeed == 1)
                            {
                                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                                helper.AvoidStuff = false;
                                helper.DriveVar = -1;
                            }
                            else
                            {
                                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                                helper.AvoidStuff = false;
                                helper.actionPause -= KickStart.AIClockPeriod / 5;
                                helper.ThrottleState = AIThrottleState.Yield;
                                helper.SettleDown();
                            }
                        }
                        else if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 4)
                        {
                            if (helper.recentSpeed == 1)
                            {
                                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                                helper.AvoidStuff = false;
                                helper.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                            }
                            else if (helper.recentSpeed < 3)
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
                        else if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 8)
                        {
                            if (helper.recentSpeed < 2)
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
                    }
                    errorCode = 9;
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                    direct.DriveDest = EDriveDest.ToBase;
                    helper.foundGoal = false;
                }
                else
                {
                    errorCode = 101;
                    if (helper.ActionPause > 0)
                    {
                        errorCode = 102;
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                        direct.Reverse(helper);
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        return;
                    }
                    errorCode = 103;
                    if (!helper.foundGoal)
                    {
                        errorCode = 104;
                        helper.EstTopSped = 1;//slow down the clock to reduce lagg
                        BGeneral.StopByBase(helper, tank, true, ref dist, ref hasMessaged, ref direct);
                        return; // There's no resources left!
                    }
                    else if (helper.theResource != null)
                    {
                        errorCode = 107;
                        if (!helper.theResource.isActive ||
                            helper.theResource.GetComponent<ResourceDispenser>().IsDeactivated ||
                            helper.theResource.gameObject.GetComponent<Damageable>().Invulnerable)
                        {
                            errorCode = 108;
                            AIECore.Minables.Remove(helper.theResource);
                            helper.theResource = null;
                            helper.foundGoal = false;
                            return;
                        }
                        else if (!(helper.theResource.centrePosition - tank.boundsCentreWorldNoCheck).WithinBox(
                            helper.JobSearchRange + AIGlobals.FindItemScanRangeExtension))
                        {
                            errorCode = 10833;
                            helper.theResource = null;
                            helper.foundGoal = false;
                            return;
                        }
                    }

                    if (dist < helper.lastTechExtents + 3 && helper.recentSpeed < 3)
                    {
                        errorCode = 109;
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Mining resource at " + helper.theResource.trans.position);
                        helper.AvoidStuff = false;
                        helper.ThrottleState = AIThrottleState.Yield;
                        if (!mind.LikelyMelee)
                            helper.ThrottleState = AIThrottleState.PivotOnly;
                        else
                        {
                            helper.ThrottleState = AIThrottleState.ForceSpeed;
                            helper.DriveVar = 1;
                        }
                        helper.SettleDown();
                        helper.RemoveObstruction(48);
                    }
                    else if (helper.recentSpeed < 2.5f)
                    {
                        errorCode = 110;
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                        helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    }
                    else if (dist < helper.lastTechExtents + 12)
                    {
                        errorCode = 111;
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at resource at " + helper.theResource.trans.position);
                        helper.AvoidStuff = false;
                        helper.ThrottleState = AIThrottleState.Yield;
                        helper.SettleDown();
                        helper.RemoveObstruction();
                        helper.ThrottleState = AIThrottleState.ForceSpeed;
                        helper.DriveVar = 1;
                    }
                    else if (needsToSlowDown)
                        helper.ThrottleState = AIThrottleState.Yield;
                    errorCode = 112;
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to mine at " + helper.theResource.trans.position + "|| Current pos " + tank.boundsCentreWorldNoCheck);
                    direct.DriveDest = EDriveDest.ToMine;
                    helper.foundBase = false;
                }
            }
            catch (Exception)
            {
                DebugTAC_AI.Assert("MineYerOwnBusiness failed with error " + errorCode);
                throw new Exception("MineYerOwnBusiness failed with error " + errorCode);
            }
        }
    }
}
