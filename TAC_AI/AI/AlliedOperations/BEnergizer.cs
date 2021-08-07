using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BEnergizer
    {
        public static void MotivateCharge(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Energizer) what to do movement-wise
            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastDestination).magnitude;
            bool hasMessaged = false;
            thisInst.lastRange = dist;

            BGeneral.ResetValues(thisInst);


            EnergyRegulator.EnergyState state = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            if (thisInst.areWeFull)
            {
                thisInst.areWeFull = false;
                if ((state.spareCapacity - state.storageTotal) / state.storageTotal > 0.9f)
                    thisInst.areWeFull = true;

                thisInst.ActionPause = 20;
            }
            else
            {
                thisInst.areWeFull = true;
                if ((state.spareCapacity - state.storageTotal) / state.storageTotal < 0.2f)
                    thisInst.areWeFull = false;
            }

            if (thisInst.areWeFull || thisInst.ActionPause > 10)
            {
                thisInst.foundBase = AIECore.FetchChargedChargers(tank, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Searching for nearest charger!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = AIECore.Extremes(thisInst.theBase.blockBounds.extents);
                }
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 3)
                {
                    if (thisInst.recentSpeed == 1)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false; 
                        thisInst.forceDrive = true;
                        thisInst.DriveVar = -1;
                        //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arrived at nearest charger and recharging!");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                {
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
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
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  unjamming from base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                    }
                    else
                    {
                        hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arrived at base!");
                        thisInst.ActionPause -= KickStart.AIClockPeriod / 5;
                        //thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Removing obstruction on way to base...");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Heading back to base!");
                thisInst.ProceedToBase = true;
                thisInst.foundGoal = false;
            }
            else if (thisInst.ActionPause > 0)
            {
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Reversing from base...");
                thisInst.forceDrive = true;
                thisInst.DriveVar = -1;
            }
            else
            {
                if (!thisInst.foundGoal)
                {
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIECore.FetchLowestChargeAlly(tank.boundsCentreWorldNoCheck, thisInst, out thisInst.theResource);
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Scanning for low batteries...");
                    if (!thisInst.foundGoal)
                    {
                        thisInst.foundBase = AIECore.FetchChargedChargers(tank, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase, tank.Team);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = AIECore.Extremes(thisInst.theBase.blockBounds.extents);
                    }
                    thisInst.ProceedToBase = true;
                    return; // There's no resources left!
                }
                thisInst.forceDrive = true;
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
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                else if (dist < thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Arriving at low battery position " + thisInst.theResource.centrePosition);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                }
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Moving out to charge ally at " + thisInst.theResource.centrePosition + " |Tech is at " + tank.boundsCentreWorldNoCheck);
                thisInst.ProceedToMine = true;
                thisInst.foundBase = false;
            }
        }
    }
}
