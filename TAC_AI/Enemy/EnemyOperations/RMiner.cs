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
        public static void MineYerOwnBusiness(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Prospector) what to do movement-wise

            Vector3 veloFlat = Vector3.zero;
            if ((bool)tank.rbody)   // So that drifting is minimized
            {
                veloFlat = tank.rbody.velocity;
                veloFlat.y = 0;
            }
            float prevDist = thisInst.lastOperatorRange;
            float dist = thisInst.GetDistanceFromTask(thisInst.lastDestinationCore);
            bool needsToSlowDown = thisInst.IsOrbiting(thisInst.lastDestinationCore, dist - prevDist);
            bool hasMessaged = false;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst, ref direct);

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
                    thisInst.actionPause = BProspector.reverseFromResourceTime;
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
                    thisInst.actionPause = BProspector.reverseFromBaseTime;
            }

            if (thisInst.CollectedTarget)
            {
                if (thisInst.ActionPause > 0)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from resources...");
                    direct.Reverse(thisInst);
                    thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.rootBlockTrans.position, mind.MaxCombatRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                }
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                if (thisInst.MovementController is AIControllerAir)
                {
                    if (thisInst.MovementController.AICore is Movement.AICores.HelicopterAICore)
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
                    if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 1)
                    {
                        if (thisInst.recentSpeed == 1)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.AvoidStuff = false;
                            thisInst.DriveVar = -1;
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                            thisInst.AvoidStuff = false;
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 4)
                    {
                        if (thisInst.recentSpeed == 1)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                            thisInst.AvoidStuff = false;
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                        }
                        else if (thisInst.recentSpeed < 3)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                            thisInst.AvoidStuff = false;
                            thisInst.Yield = true;
                        }
                        else
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                            thisInst.AvoidStuff = false;
                            thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                            thisInst.Yield = true;
                            thisInst.SettleDown();
                        }
                    }
                    else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                    {
                        if (thisInst.recentSpeed < 2)
                        {
                            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                            thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                        }
                        else
                        {
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
            {
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
                    thisInst.foundGoal = AIECore.FetchClosestResource(tank.rootBlockTrans.position, mind.MaxCombatRange
                        , thisInst.lastTechExtents * AIGlobals.WaterDepthTechHeightPercent, out thisInst.theResource);
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for resources...");
                    if (!thisInst.foundGoal)
                    {
                        thisInst.foundBase = AIECore.FetchClosestChunkReceiver(tank.rootBlockTrans.position, mind.MaxCombatRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    }
                    direct.DriveDest = EDriveDest.ToBase;
                    BProspector.StopByBase(thisInst, tank, dist, ref hasMessaged, ref direct);
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

                if (dist < thisInst.lastTechExtents + 3 && thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Mining resource at " + thisInst.theResource.trans.position);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    if (!mind.LikelyMelee)
                        thisInst.PivotOnly = true;
                    else
                    {
                        thisInst.ForceSetDrive = true;
                        thisInst.DriveVar = 1;
                    }
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction(48);
                }
                else if (thisInst.recentSpeed < 2.5f)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (dist < thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at resource at " + thisInst.theResource.trans.position);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction();
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1;
                }
                else if (needsToSlowDown)
                    thisInst.Yield = true;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to mine at " + thisInst.theResource.trans.position + "|| Current pos " + tank.boundsCentreWorldNoCheck);
                direct.DriveDest = EDriveDest.ToMine;
                thisInst.foundBase = false;
            }
        }
    }
}
