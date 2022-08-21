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
    public static class RScavenger
    {

        public static void Scavenge(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = mind.EvilCommander == EnemyHandling.Starship || mind.EvilCommander == EnemyHandling.Airplane || mind.EvilCommander == EnemyHandling.Chopper;

            float dist = thisInst.GetDistanceFromTask(thisInst.lastDestination);
            bool hasMessaged = false;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst);

            if (mind.CommanderSmarts >= EnemySmarts.Mild && thisInst.lastEnemy != null)
            {   //RUN!!!!!!!!
                if (!thisInst.foundBase)
                {
                    thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck, mind.Range + AIGlobals.FindBaseExtension, out thisInst.lastBasePos, out Tank theBase, tank.Team);
                    if (!thisInst.foundBase)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return;
                    }
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, "TACtical_AI:AI " + tank.name + ":  There's no base nearby!  I AM LOST!!!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = theBase.GetCheapBounds();
                }
                else if (thisInst.theBase == null)
                {
                    thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.boundsCentreWorldNoCheck, mind.Range + AIGlobals.FindBaseExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                    if (!thisInst.foundBase)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return;
                    }
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
                thisInst.ActionPause = AIGlobals.ReverseDelay;
            }
            else
            {   // Gather materials
                thisInst.CollectedTarget = true;
                if (!thisInst.HeldBlock)
                {
                    thisInst.CollectedTarget = false;
                }
                /*
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    if (hold.BlockNotFullAndAvail())
                    {
                        thisInst.CollectedTarget = false;
                        break;//Checking if tech is full after destroying a node
                    }
                }
                */
            }

            //Debug.Log("TACtical_AI: Block is Present: " + thisInst.foundGoal);
            if (thisInst.CollectedTarget || thisInst.ActionPause > 10)
            {   // BRANCH - Return to base
                thisInst.foundBase = AIECore.FetchClosestBlockReceiver(tank.rootBlockTrans.position, mind.Range + AIGlobals.FindBaseExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Searching for nearest base!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return; // There's no base! 
                    }
                    thisInst.lastDestination = thisInst.theBase.boundsCentreWorld;
                    dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestination).magnitude;
                }
                thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                float spacing = thisInst.lastBaseExtremes + AIGlobals.MaxBlockGrabRangeAlt + thisInst.lastTechExtents;

                if (dist < spacing)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
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
                        try
                        {
                            Visible blockHeld = thisInst.HeldBlock.visible;
                            thisInst.DropBlock((thisInst.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents))) - thisInst.HeldBlock.visible.centrePosition);
                            //blockHeld.centrePosition = thisInst.lastBasePos.position + (Vector3.up * AIECore.ExtremesAbs(blockHeld.block.BlockCellBounds.extents));
                        }
                        catch { }
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < spacing + 8)
                {
                    thisInst.theBase.GetComponent<AIECore.TankAIHelper>().AllowApproach(thisInst);
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
                else if (dist < spacing + 12)
                {
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
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                thisInst.DriveDest = EDriveDest.ToBase;
                thisInst.foundGoal = false;
            }
            else if (thisInst.ActionPause > 0)
            {   // BRANCH - Reverse from Base
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = -1;
            }
            else
            {   // BRANCH - Go look for blocks
                if (!thisInst.foundGoal)
                {
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FetchLooseBlocks(tank.rootBlockTrans.position, mind.Range, out thisInst.theResource);
                    if (!thisInst.foundGoal)
                    {
                        mind.CommanderMind = EnemyAttitude.Default;
                        return;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Found a block...");
                        thisInst.lastDestination = thisInst.theResource.centrePosition;
                        thisInst.DriveDest = EDriveDest.ToBase;
                        BScrapper.StopByBase(thisInst, tank, dist, ref hasMessaged);
                        return;
                    }
                }
                else if (thisInst.theResource != null)
                {
                    if (!thisInst.theResource.block || thisInst.theResource.block.IsAttached || thisInst.theResource.InBeam)
                    {
                        thisInst.theResource = null;
                        thisInst.DropBlock(Vector3.up);
                        thisInst.foundGoal = false;
                        DebugTAC_AI.Log("TACtical_AI: Block was removed from targeting");
                        return;
                    }
                }
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

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
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                else if (dist <= spacing)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Arriving at loose block at " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.HoldBlock(thisInst.theResource);
                    thisInst.SettleDown();
                }
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Moving out to scavenge at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.DriveDest = EDriveDest.ToMine;
                thisInst.foundBase = false;
            }
        }


    }
}
