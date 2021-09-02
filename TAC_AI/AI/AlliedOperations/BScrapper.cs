using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BScrapper
    {
        const float FindBaseExtension = 500;
        public static void MotivateFind(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestination).magnitude;
            bool hasMessaged = false;
            thisInst.lastRange = dist;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst);

            if (thisInst.AdvancedAI && thisInst.lastEnemy != null)
            {   //RUN!!!!!!!!
                if (!thisInst.foundBase)
                {
                    thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck, tank.Radar.Range + FindBaseExtension, out thisInst.lastBasePos, out Tank theBase, tank.Team);
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, "TACtical_AI:AI " + tank.name + ":  There's no base nearby!  I AM LOST!!!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = AIECore.Extremes(theBase.blockBounds.extents);
                }
                else if (thisInst.theBase == null)
                {
                    thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck, tank.Radar.Range + FindBaseExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                    thisInst.lastBaseExtremes = AIECore.Extremes(thisInst.theBase.blockBounds.extents);
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    return;
                }

                thisInst.forceDrive = true;
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
                thisInst.ProceedToBase = true;
                return;
            }

            if (thisInst.areWeFull)
            {
                thisInst.areWeFull = false;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsEmpty && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        thisInst.areWeFull = true;
                        break;//Checking if tech is empty when unloading at base
                    }
                }
                thisInst.ActionPause = 20;
            }
            else
            {
                thisInst.areWeFull = true;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsFull && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        thisInst.areWeFull = false;
                        break;//Checking if tech is full after destroying a node
                    }
                }
            }

            if (thisInst.areWeFull || thisInst.ActionPause > 10)
            {
                thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.rootBlockTrans.position, tank.Radar.Range + FindBaseExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = AIECore.Extremes(thisInst.theBase.blockBounds.extents);
                }
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 3)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed == 1)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.DriveVar = -1;
                        //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arrived at base and unloading!");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else if (thisInst.recentSpeed < 8)
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                        thisInst.AvoidStuff = false;
                        thisInst.Yield = true;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach();
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
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                thisInst.ProceedToBase = true;
                thisInst.foundGoal = false;
            }
            else if (thisInst.ActionPause > 0)
            {
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                thisInst.forceDrive = true;
                thisInst.DriveVar = -1;
            }
            else
            {
                if (!thisInst.foundGoal)
                {
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FetchLooseBlocks(tank.rootBlockTrans.position, tank.Radar.Range, out thisInst.theResource);
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Scanning for loose blocks...");
                    if (!thisInst.foundGoal)
                    {
                        thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.rootBlockTrans.position, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = AIECore.Extremes(thisInst.theBase.blockBounds.extents);
                    }
                    thisInst.ProceedToBase = true;
                    return; // There's no resources left!
                }
                else if (thisInst.theResource != null)
                {
                    if (thisInst.theResource.block.IsAttached || thisInst.theResource.InBeam)
                    {
                        thisInst.theResource = null;
                        thisInst.foundGoal = false;
                        return;
                    }
                }
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastTechExtents + 3 && thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Grabbing block at " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    if (!thisInst.FullMelee)
                        thisInst.PivotOnly = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction();
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                else if (dist < thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at loose block at " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction();
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to scavenge at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.ProceedToMine = true;
                thisInst.foundBase = false;
            }
        }
    }
}
