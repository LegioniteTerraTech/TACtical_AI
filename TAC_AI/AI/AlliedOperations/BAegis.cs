using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BAegis 
    {
        public static void MotivateProtect(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Aegis) what to do movement-wise
            helper.lastPlayer = helper.GetPlayerTech();
            helper.foundGoal = false;

            helper.IsMultiTech = false;
            helper.Attempt3DNavi = (helper.DriverType == AIDriverType.Pilot || helper.DriverType == AIDriverType.Astronaut);

            BGeneral.ResetValues(helper, ref direct);

            if (helper.theResource == null)
                return;
            if (helper.theResource.tank == null)
                return;
            if (helper.theResource.tank == tank)
            {
                DebugTAC_AI.Assert(true, KickStart.ModID + ": AI " + tank.name + ":  Aegis - error: trying to protect self");
                return;
            }

            direct.SetLastDest(helper.theResource.tank.boundsCentreWorldNoCheck);
            float dist = helper.GetDistanceFromTask(direct.lastDestination, helper.theResource.GetCheapBounds());
            float range = helper.MaxObjectiveRange + helper.lastTechExtents;
            bool hasMessaged = false;

            float AllyExt = helper.theResource.GetCheapBounds();

            if ((bool)helper.lastEnemyGet && !helper.Retreat && helper.lastOperatorRange <= helper.MaxCombatRange)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    helper.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                helper.ChaseThreat = true;
                return;
            }
            else
                helper.ChaseThreat = false;

            if (dist < helper.lastTechExtents + AllyExt + 2)
            {
                if (helper.AvoidStuff)
                {
                    helper.DelayedAnchorClock = 0;
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving " + helper.theResource.tank.name + " some room...");
                    direct.DriveAwayFacingTowards();
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = -1;
                    if (helper.unanchorCountdown > 0)
                        helper.unanchorCountdown--;
                    if (helper.AutoAnchor && helper.PlayerAllowAutoAnchoring && tank.Anchors.NumPossibleAnchors >= 1)
                    {
                        if (tank.Anchors.NumIsAnchored > 0)
                        {
                            helper.unanchorCountdown = 15;
                            helper.Unanchor();
                        }
                    }
                }
                else
                {   //Else we are holding off because someone is trying to dock.
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Waiting for other tech to finish their actions...");
                    helper.AvoidStuff = true;
                    helper.SettleDown();
                    if (helper.DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                        helper.DelayedAnchorClock++;
                    if (helper.CanAutoAnchor)
                    {
                        if (tank.Anchors.NumIsAnchored == 0 && helper.CanAttemptAnchor)
                        {
                            AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
                            helper.TryInsureAnchor();
                        }
                    }
                }
            }
            else if (dist < range + AllyExt && dist > (range * 0.75f) + AllyExt)
            {
                // Time to go!
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Departing!");
                direct.DriveDest = EDriveDest.ToLastDestination;
                helper.DelayedAnchorClock = 0;
                if (helper.unanchorCountdown > 0)
                    helper.unanchorCountdown--;
                if (helper.AutoAnchor && helper.PlayerAllowAutoAnchoring && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Time to pack up and move out!");
                        helper.unanchorCountdown = 15;
                        helper.Unanchor();
                    }
                }
            }
            else if (dist >= range + AllyExt)
            {
                helper.DelayedAnchorClock = 0;
                direct.DriveDest = EDriveDest.ToLastDestination;


                //DISTANCE WARNINGS
                if (dist > range * 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Oh Crafty they are too far!");
                    helper.Urgency += KickStart.AIClockPeriod / 2f;
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 1f;
                    helper.LightBoost = true;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI drive " + tank.control.DriveControl);
                    if (helper.UrgencyOverload > 0)
                        helper.UrgencyOverload -= KickStart.AIClockPeriod / 5f;
                }
                if (helper.UrgencyOverload > 50)
                {
                    //Are we just randomly angry for too long? let's fix that
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                    helper.EstTopSped = 1;
                    helper.AvoidStuff = true;
                    helper.UrgencyOverload = 0;
                }
                //URGENCY REACTION
                if (helper.Urgency > 20)
                {
                    //FARRR behind! BOOSTERS NOW!
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": I AM SUPER FAR BEHIND!");
                    helper.AvoidStuff = false;
                    helper.FullBoost = true; // WE ARE SOO FAR BEHIND
                    helper.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (helper.Urgency > 2)
                {
                    //Behind and we must catch up
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Wait for meeeeeeeeeee!");
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 1;
                    helper.LightBoost = true;
                    helper.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (helper.Urgency > 1 && helper.recentSpeed < 10)
                {
                    //bloody tree moment
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": GET OUT OF THE WAY NUMBNUT!");
                    helper.AvoidStuff = false;
                    helper.FIRE_ALL = true;
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 0.5f;
                    helper.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                //OBSTRUCTION MANAGEMENT
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    helper.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                else if (!helper.IsTechMovingAbs(helper.EstTopSped / 2))
                {
                    // Moving a bit too slow for what we can do
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Trying to catch up!");
                    helper.Urgency += KickStart.AIClockPeriod / 5f;
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 1;
                }
                else
                {
                    //Things are going smoothly
                    helper.AvoidStuff = true;
                    helper.SettleDown();
                }
            }
            else if (dist < (range / 2) + AllyExt)
            {
                //Likely stationary
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
                //direct.lastDestination = tank.transform.position;
                helper.AvoidStuff = true;
                helper.SettleDown();
                if (helper.DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                    helper.DelayedAnchorClock++;
                if (helper.CanAutoAnchor)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && helper.CanAttemptAnchor)
                    {
                        AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
                        helper.TryInsureAnchor();
                    }
                }
            }
            else
            {
                //Likely idle
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  in resting state");

                //direct.lastDestination = tank.transform.position;
                helper.AvoidStuff = true;
                helper.SettleDown();
                //helper.DriveVar = 0;
                if (helper.DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                    helper.DelayedAnchorClock++;
                if (helper.CanAutoAnchor)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && helper.CanAttemptAnchor)
                    {
                        AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
                        helper.TryInsureAnchor();
                    }
                }
            }
        }
    }
}
