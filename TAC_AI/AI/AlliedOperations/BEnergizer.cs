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
        public static void MotivateCharge(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Energizer) what to do movement-wise
            helper.IsMultiTech = false;
            helper.Attempt3DNavi = (helper.DriverType == AIDriverType.Pilot || helper.DriverType == AIDriverType.Astronaut);

            float dist = helper.GetDistanceFromTask(helper.lastDestinationCore);
            bool hasMessaged = false;
            helper.AvoidStuff = true;

            BGeneral.ResetValues(helper, ref direct);

            // No running here - this is a combat case!

            TechEnergy.EnergyState state = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            if (helper.CollectedTarget)
            {
                if ((state.storageTotal - state.spareCapacity) / state.storageTotal < 0.2f)
                {
                    helper.CollectedTarget = false;
                    helper.actionPause = reverseFromResourceTime;
                }
            }
            else
            {
                if ((state.storageTotal - state.spareCapacity) / state.storageTotal > 0.9f)
                {
                    helper.CollectedTarget = true;
                    helper.actionPause = AIGlobals.ReverseDelay;
                }
            }

            if (!helper.CollectedTarget)
            {   // BRANCH - Not Charged: Recharge!
                if (helper.ActionPause > 0)
                {   // BRANCH - Reverse from Resources
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Reversing from resources...");
                    direct.Reverse(helper);
                    helper.actionPause -= KickStart.AIClockPeriod / 5;
                    return;
                }
                helper.foundBase = AIECore.FetchChargedChargers(tank, helper.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out helper.lastBasePos, out helper.theBase, tank.Team);
                if (!helper.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Searching for nearest charger!");
                    helper.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (helper.theBase == null)
                        return; // There's no base!
                    helper.lastBaseExtremes = helper.theBase.GetCheapBounds();
                }
                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = 1;

                if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 3)
                {
                    helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                    if (helper.recentSpeed == 1)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        helper.AvoidStuff = false; 
                        helper.ThrottleState = AIThrottleState.ForceSpeed;
                        helper.DriveVar = -1;
                        //helper.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arrived at nearest charger and recharging!");
                        helper.AvoidStuff = false;
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        helper.ThrottleState = AIThrottleState.Yield;
                        helper.SettleDown();
                    }
                }
                else if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 8)
                {
                    helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                    if (helper.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        helper.AvoidStuff = false;
                        helper.TryHandleObstruction(hasMessaged, dist, false, false, ref direct);
                    }
                    else if (helper.recentSpeed < 8)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Rattling off resources...");
                        helper.AvoidStuff = false;
                        helper.ThrottleState = AIThrottleState.Yield;
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Yielding base approach...");
                        helper.AvoidStuff = false;
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        helper.ThrottleState = AIThrottleState.Yield;
                        helper.SettleDown();
                    }
                }
                else if (dist < helper.lastBaseExtremes + helper.lastTechExtents + 12)
                {
                    helper.theBase.GetHelperInsured().SlowForApproacher(helper);
                    if (helper.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                        helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                        helper.actionPause -= KickStart.AIClockPeriod / 5;
                        //helper.ThrottleState = AIThrottleState.Yield;
                        helper.SettleDown();
                    }
                }
                else if (helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                    helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                direct.DriveDest = EDriveDest.ToBase;
                helper.foundGoal = false;
            }
            else
            {   // BRANCH - Charged: Find a chargeable target
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
                    helper.foundGoal = AIECore.FetchLowestChargeAlly(tank.boundsCentreWorldNoCheck, helper, out helper.theResource);
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Scanning for low batteries...");
                    if (!helper.foundGoal)
                    {
                        helper.foundBase = AIECore.FetchChargedChargers(tank, helper.JobSearchRange + AIGlobals.FindBaseScanRangeExtension, out helper.lastBasePos, out helper.theBase, tank.Team);
                        if (helper.theBase == null)
                            return; // There's no base!
                        helper.lastBaseExtremes = helper.theBase.GetCheapBounds();
                    }
                    direct.DriveDest = EDriveDest.ToBase;
                    return; // There's no resources left!
                }
                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = 1;

                if (dist < helper.lastTechExtents + 3 && helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Charging ally at " + helper.theResource.centrePosition);
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.Yield;
                    helper.SettleDown();
                }
                else if (helper.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Removing obstruction at " + tank.transform.position);
                    helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                }
                else if (dist < helper.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arriving at low battery position " + helper.theResource.centrePosition);
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.Yield;
                    helper.SettleDown();
                }
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Moving out to charge ally at " + helper.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                direct.DriveDest = EDriveDest.ToMine;
                helper.foundBase = false;
            }
        }
    }
}
