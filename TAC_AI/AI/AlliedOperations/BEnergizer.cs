using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BEnergizer
    {
        internal const int reverseFromResourceTime = 35;
        public static void MotivateCharge(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Energizer) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = (thisInst.DriverType == AIDriverType.Pilot || thisInst.DriverType == AIDriverType.Astronaut);

            float dist = thisInst.GetDistanceFromTask(thisInst.lastDestinationCore);
            bool hasMessaged = false;
            thisInst.AvoidStuff = true;

            BGeneral.ResetValues(thisInst, ref direct);

            // No running here - this is a combat case!

            TechEnergy.EnergyState state = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            if (thisInst.CollectedTarget)
            {
                if ((state.storageTotal - state.spareCapacity) / state.storageTotal < 0.2f)
                {
                    thisInst.CollectedTarget = false;
                    thisInst.actionPause = reverseFromResourceTime;
                }
            }
            else
            {
                if ((state.storageTotal - state.spareCapacity) / state.storageTotal > 0.9f)
                {
                    thisInst.CollectedTarget = true;
                    thisInst.actionPause = AIGlobals.ReverseDelay;
                }
            }

            if (!thisInst.CollectedTarget)
            {   // BRANCH - Not Charged: Recharge!
                if (thisInst.ActionPause > 0)
                {   // BRANCH - Reverse from Resources
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from resources...");
                    direct.Reverse(thisInst);
                    thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                thisInst.foundBase = AIECore.FetchChargedChargers(tank, thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Searching for nearest charger!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                }
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 3)
                {
                    thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                    if (thisInst.recentSpeed == 1)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false; 
                        thisInst.ForceSetDrive = true;
                        thisInst.DriveVar = -1;
                        //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arrived at nearest charger and recharging!");
                        thisInst.AvoidStuff = false;
                        thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                {
                    thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                    }
                    else if (thisInst.recentSpeed < 8)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                        thisInst.AvoidStuff = false;
                        thisInst.Yield = true;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                        thisInst.AvoidStuff = false;
                        thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    thisInst.theBase.GetHelperInsured().SlowForApproacher(thisInst);
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                        thisInst.actionPause -= KickStart.AIClockPeriod / 5;
                        //thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                direct.DriveDest = EDriveDest.ToBase;
                thisInst.foundGoal = false;
            }
            else
            {   // BRANCH - Charged: Find a chargeable target
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
                    thisInst.foundGoal = AIECore.FetchLowestChargeAlly(tank.boundsCentreWorldNoCheck, thisInst, out thisInst.theResource);
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Scanning for low batteries...");
                    if (!thisInst.foundGoal)
                    {
                        thisInst.foundBase = AIECore.FetchChargedChargers(tank, thisInst.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = thisInst.theBase.GetCheapBounds();
                    }
                    direct.DriveDest = EDriveDest.ToBase;
                    return; // There's no resources left!
                }
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastTechExtents + 3 && thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Charging ally at " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (dist < thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arriving at low battery position " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                }
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Moving out to charge ally at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveDest = EDriveDest.ToMine;
                thisInst.foundBase = false;
            }
        }
    }
}
