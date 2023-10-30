using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BAstrotech 
    {
        //Same a escort code, because the BEscort code supports 3D!
        // we just need to re-define how far above ground we should be
        public static void MotivateSpace(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the ship (Escort) what to do movement-wise
            thisInst.lastPlayer = thisInst.GetPlayerTech();
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = true;

            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.AvoidStuff = true;

            if (thisInst.lastPlayer == null)
                return;
            direct.SetLastDest(thisInst.lastPlayer.tank.boundsCentreWorldNoCheck);
            float dist = thisInst.GetDistanceFromTask(thisInst.lastPlayer.tank.boundsCentreWorldNoCheck, thisInst.lastPlayer.GetCheapBounds());
            float range = thisInst.MaxObjectiveRange + thisInst.lastTechExtents;

            bool hasMessaged = false;

            float playerExt = thisInst.lastPlayer.GetCheapBounds();

            if ((bool)thisInst.lastEnemyGet && !thisInst.Retreat)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                return;
            }

            if (dist < thisInst.lastTechExtents + playerExt + 2)
            {
                thisInst.DelayedAnchorClock = 0;
                AIECore.AIMessage(tank, ref hasMessaged, "TACtical_AI:AI " + tank.name + ":  Giving the player some room...");
                direct.DriveAwayFacingTowards();
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;
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
            else if (dist < range + playerExt && dist > (range / 2) + playerExt)
            {
                // Time to go!
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Departing!");
                direct.DriveDest = EDriveDest.ToLastDestination;
                thisInst.anchorAttempts = 0; thisInst.DelayedAnchorClock = 0;
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
            else if (dist >= range + playerExt)
            {
                thisInst.DelayedAnchorClock = 0;
                direct.DriveDest = EDriveDest.ToLastDestination;
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1f;


                //DISTANCE WARNINGS
                if (dist > range * 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Oh Crafty they are too far!");
                    thisInst.Urgency += KickStart.AIClockPeriod / 2f;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1f;
                    //DebugTAC_AI.Log("TACtical_AI: AI drive " + tank.control.DriveControl);
                    if (thisInst.UrgencyOverload > 0)
                        thisInst.UrgencyOverload--;
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
                if (thisInst.Urgency > 30)
                {
                    //FARRR behind! BOOSTERS NOW!
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": I AM SUPER FAR BEHIND!");
                    thisInst.AvoidStuff = false;
                    thisInst.FullBoost = true; // WE ARE SOO FAR BEHIND
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (thisInst.Urgency > 15)
                {
                    //Behind and we must catch up
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Wait for meeeeeeeeeee!");
                    thisInst.AvoidStuff = false;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1;
                    thisInst.LightBoost = true;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (thisInst.Urgency > 5 && thisInst.recentSpeed < 6)
                {
                    //bloody tree moment
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": GET OUT OF THE WAY NUMBNUT!");
                    thisInst.AvoidStuff = false;
                    thisInst.FIRE_ALL = true;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 0.5f;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                //OBSTRUCTION MANAGEMENT
                bool goingTooSlow = !thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend);
                if (dist >= range + playerExt + 10 && goingTooSlow)
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                else if (goingTooSlow)
                {
                    // Moving a bit too slow for what we can do
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Trying to catch up!");
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
            else if (dist < (range / 4) + playerExt)
            {
                //Likely stationary
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
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
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  in resting state");
                thisInst.AvoidStuff = true;
                thisInst.SettleDown();
                thisInst.DriveVar = 0;
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

        /// <summary> UNFINISHED <param name="direct"></param>
        public static void SpaceDriver(TankAIHelper thisInst, Tank tank, Visible followThis, ref EControlOperatorSet direct)
        {
            //The Handler that tells the ship (Escort) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.Attempt3DNavi = true;

            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.AvoidStuff = true;

            if (followThis == null)
                return;
            float dist = thisInst.GetDistanceFromTask(followThis.tank.boundsCentreWorldNoCheck, followThis.GetCheapBounds());
            float range = (thisInst.MaxObjectiveRange * 3) + thisInst.lastTechExtents;
            // The range is tripled here due to flight conditions
            bool hasMessaged = false;

            float playerExt = followThis.GetCheapBounds();

            if ((bool)thisInst.lastEnemyGet && !thisInst.Retreat)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                return;
            }

            if (dist < thisInst.lastTechExtents + playerExt + 2)
            {
                thisInst.DelayedAnchorClock = 0;
                AIECore.AIMessage(tank, ref hasMessaged, "TACtical_AI:AI " + tank.name + ":  Giving the player some room...");
                direct.DriveAwayFacingTowards();
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1;
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
            else if (dist < range + playerExt && dist > (range * 0.75f) + playerExt)
            {
                // Time to go!
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Departing!");
                direct.DriveDest = EDriveDest.ToLastDestination;
                thisInst.anchorAttempts = 0; thisInst.DelayedAnchorClock = 0;
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
            else if (dist >= range + playerExt)
            {
                thisInst.DelayedAnchorClock = 0;
                direct.DriveDest = EDriveDest.ToLastDestination;
                thisInst.ForceSetDrive = true;
                thisInst.DriveVar = 1f;


                //DISTANCE WARNINGS
                if (dist > range * 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Oh Crafty they are too far!");
                    thisInst.Urgency += KickStart.AIClockPeriod / 2f;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1f;
                    //DebugTAC_AI.Log("TACtical_AI: AI drive " + tank.control.DriveControl);
                    if (thisInst.UrgencyOverload > 0)
                        thisInst.UrgencyOverload--;
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
                if (thisInst.Urgency > 30)
                {
                    //FARRR behind! BOOSTERS NOW!
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": I AM SUPER FAR BEHIND!");
                    thisInst.AvoidStuff = false;
                    thisInst.FullBoost = true; // WE ARE SOO FAR BEHIND
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (thisInst.Urgency > 15)
                {
                    //Behind and we must catch up
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Wait for meeeeeeeeeee!");
                    thisInst.AvoidStuff = false;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1;
                    thisInst.LightBoost = true;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (thisInst.Urgency > 5 && thisInst.recentSpeed < 6)
                {
                    //bloody tree moment
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": GET OUT OF THE WAY NUMBNUT!");
                    thisInst.AvoidStuff = false;
                    thisInst.FIRE_ALL = true;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 0.5f;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                //OBSTRUCTION MANAGEMENT
                bool goingTooSlow = !thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.EnemyAISpeedPanicDividend);
                if (dist >= range + playerExt + 10 && goingTooSlow)
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                else if (goingTooSlow)
                {
                    // Moving a bit too slow for what we can do
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Trying to catch up!");
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
            else if (dist < (range / 2) + playerExt)
            {
                //Likely stationary
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
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
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  in resting state");
                thisInst.AvoidStuff = true;
                thisInst.SettleDown();
                thisInst.DriveVar = 0;
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
