using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BAegis 
    {
        public static void MotivateProtect(AIECore.TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Aegis) what to do movement-wise
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.foundGoal = false;

            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = (thisInst.DriverType == AIDriverType.Pilot || thisInst.DriverType == AIDriverType.Astronaut);

            BGeneral.ResetValues(thisInst, ref direct);

            if (thisInst.theResource == null)
                return;
            if (thisInst.theResource.tank == null)
                return;
            if (thisInst.theResource.tank == tank)
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: AI " + tank.name + ":  Aegis - error: trying to protect self");
                return;
            }
            direct.lastDestination = thisInst.theResource.tank.boundsCentreWorldNoCheck;
            float dist = thisInst.GetDistanceFromTask(direct.lastDestination, thisInst.theResource.GetCheapBounds());
            float range = thisInst.MaxObjectiveRange + thisInst.lastTechExtents;
            bool hasMessaged = false;

            float AllyExt = thisInst.theResource.GetCheapBounds();

            if ((bool)thisInst.lastEnemyGet && !thisInst.Retreat && thisInst.lastOperatorRange <= thisInst.MaxCombatRange)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                thisInst.ChaseThreat = true;
                return;
            }
            else
                thisInst.ChaseThreat = false;

            if (dist < thisInst.lastTechExtents + AllyExt + 2)
            {
                if (thisInst.AvoidStuff)
                {
                    thisInst.DelayedAnchorClock = 0;
                    AIECore.AIMessage(tank, ref hasMessaged, "TACtical_AI:AI " + tank.name + ":  Giving " + thisInst.theResource.tank.name + " some room...");
                    direct.DriveAwayFacingTowards();
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = -1;
                    if (thisInst.unanchorCountdown > 0)
                        thisInst.unanchorCountdown--;
                    if (thisInst.AutoAnchor && thisInst.PlayerAllowAutoAnchoring && tank.Anchors.NumPossibleAnchors >= 1)
                    {
                        if (tank.Anchors.NumIsAnchored > 0)
                        {
                            thisInst.unanchorCountdown = 15;
                            thisInst.UnAnchor();
                        }
                    }
                }
                else
                {   //Else we are holding off because someone is trying to dock.
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Waiting for other tech to finish their actions...");
                    thisInst.AvoidStuff = true;
                    thisInst.SettleDown();
                    if (thisInst.DelayedAnchorClock < 15)
                        thisInst.DelayedAnchorClock++;
                    if (thisInst.CanAutoAnchor)
                    {
                        if (tank.Anchors.NumIsAnchored == 0 && thisInst.anchorAttempts <= AIGlobals.AlliedAnchorAttempts)
                        {
                            DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Setting camp!");
                            thisInst.TryAnchor();
                            thisInst.anchorAttempts++;
                        }
                    }
                }
            }
            else if (dist < range + AllyExt && dist > (range * 0.75f) + AllyExt)
            {
                // Time to go!
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Departing!");
                direct.DriveDest = EDriveDest.ToLastDestination;
                thisInst.anchorAttempts = 0; 
                thisInst.DelayedAnchorClock = 0;
                if (thisInst.unanchorCountdown > 0)
                    thisInst.unanchorCountdown--;
                if (thisInst.AutoAnchor && thisInst.PlayerAllowAutoAnchoring && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ": Time to pack up and move out!");
                        thisInst.unanchorCountdown = 15;
                        thisInst.UnAnchor();
                    }
                }
            }
            else if (dist >= range + AllyExt)
            {
                thisInst.DelayedAnchorClock = 0;
                direct.DriveDest = EDriveDest.ToLastDestination;


                //DISTANCE WARNINGS
                if (dist > range * 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Oh Crafty they are too far!");
                    thisInst.Urgency += KickStart.AIClockPeriod / 2f;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1f;
                    thisInst.LightBoost = true;
                    //DebugTAC_AI.Log("TACtical_AI: AI drive " + tank.control.DriveControl);
                    if (thisInst.UrgencyOverload > 0)
                        thisInst.UrgencyOverload -= KickStart.AIClockPeriod / 5f;
                }
                if (thisInst.UrgencyOverload > 50)
                {
                    //Are we just randomly angry for too long? let's fix that
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                    thisInst.EstTopSped = 1;
                    thisInst.AvoidStuff = true;
                    thisInst.UrgencyOverload = 0;
                }
                //URGENCY REACTION
                if (thisInst.Urgency > 20)
                {
                    //FARRR behind! BOOSTERS NOW!
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": I AM SUPER FAR BEHIND!");
                    thisInst.AvoidStuff = false;
                    thisInst.FullBoost = true; // WE ARE SOO FAR BEHIND
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (thisInst.Urgency > 2)
                {
                    //Behind and we must catch up
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Wait for meeeeeeeeeee!");
                    thisInst.AvoidStuff = false;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1;
                    thisInst.LightBoost = true;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (thisInst.Urgency > 1 && thisInst.recentSpeed < 10)
                {
                    //bloody tree moment
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": GET OUT OF THE WAY NUMBNUT!");
                    thisInst.AvoidStuff = false;
                    thisInst.FIRE_NOW = true;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 0.5f;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                //OBSTRUCTION MANAGEMENT
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                else if (!thisInst.IsTechMoving(thisInst.EstTopSped / 2))
                {
                    // Moving a bit too slow for what we can do
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Trying to catch up!");
                    thisInst.Urgency += KickStart.AIClockPeriod / 5f;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1;
                }
                else
                {
                    //Things are going smoothly
                    thisInst.AvoidStuff = true;
                    thisInst.SettleDown();
                }
            }
            else if (dist < (range / 2) + AllyExt)
            {
                //Likely stationary
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
                //direct.lastDestination = tank.transform.position;
                thisInst.AvoidStuff = true;
                thisInst.SettleDown();
                if (thisInst.DelayedAnchorClock < 15)
                    thisInst.DelayedAnchorClock++;
                if (thisInst.CanAutoAnchor)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && thisInst.anchorAttempts <= AIGlobals.AlliedAnchorAttempts)
                    {
                        DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Setting camp!");
                        thisInst.TryAnchor();
                        thisInst.anchorAttempts++;
                    }
                }
            }
            else
            {
                //Likely idle
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  in resting state");

                //direct.lastDestination = tank.transform.position;
                thisInst.AvoidStuff = true;
                thisInst.SettleDown();
                //thisInst.DriveVar = 0;
                if (thisInst.DelayedAnchorClock < 15)
                    thisInst.DelayedAnchorClock++;
                if (thisInst.CanAutoAnchor)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && thisInst.anchorAttempts <= AIGlobals.AlliedAnchorAttempts)
                    {
                        DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Setting camp!");
                        thisInst.TryAnchor();
                        thisInst.anchorAttempts++;
                    }
                }
            }
        }
    }
}
