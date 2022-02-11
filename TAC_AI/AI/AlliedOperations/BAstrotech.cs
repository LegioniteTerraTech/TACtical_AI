using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    public static class BAstrotech 
    {
        //Same a escort code, because the BEscort code supports 3D!
        // we just need to re-define how far above ground we should be
        public static void MotivateSpace(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the ship (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst);
            thisInst.AvoidStuff = true;

            if (thisInst.lastPlayer == null)
                return;
            float dist = (tank.boundsCentreWorldNoCheck - thisInst.lastPlayer.tank.boundsCentreWorldNoCheck).magnitude - AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents);
            float range = (thisInst.RangeToStopRush * 2) + AIECore.Extremes(tank.blockBounds.extents);
            // The range is doubled here due to flight conditions
            bool hasMessaged = false;
            thisInst.lastRange = dist;

            float playerExt = AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents);

            if ((bool)thisInst.lastEnemy && !thisInst.Retreat)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / 4))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true);
                }
                return;
            }

            if (dist < thisInst.lastTechExtents + playerExt + 2)
            {
                thisInst.DelayedAnchorClock = 0;
                AIECore.AIMessage(tank, ref hasMessaged, "TACtical_AI:AI " + tank.name + ":  Giving the player some room...");
                thisInst.MoveFromObjective = true;
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;
                if (thisInst.unanchorCountdown > 0)
                    thisInst.unanchorCountdown--;
                if (thisInst.AutoAnchor && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        thisInst.unanchorCountdown = 15;
                        tank.TryToggleTechAnchor();
                    }
                }
            }
            else if (dist < range + playerExt && dist > (range / 2) + playerExt)
            {
                // Time to go!
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Departing!");
                thisInst.ProceedToObjective = true;
                thisInst.anchorAttempts = 0; thisInst.DelayedAnchorClock = 0;
                if (thisInst.unanchorCountdown > 0)
                    thisInst.unanchorCountdown--;
                if (thisInst.AutoAnchor && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        Debug.Log("TACtical_AI: AI " + tank.name + ": Time to pack up and move out!");
                        thisInst.unanchorCountdown = 15;
                        tank.TryToggleTechAnchor();
                    }
                }
            }
            else if (dist >= range + playerExt)
            {
                thisInst.DelayedAnchorClock = 0;
                thisInst.ProceedToObjective = true;
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1f;


                //DISTANCE WARNINGS
                if (dist > range * 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Oh Crafty they are too far!");
                    thisInst.Urgency += KickStart.AIClockPeriod / 2;
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 1f;
                    //Debug.Log("TACtical_AI: AI drive " + tank.control.DriveControl);
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
                    thisInst.BOOST = true; // WE ARE SOO FAR BEHIND
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5;
                }
                else if (thisInst.Urgency > 15)
                {
                    //Behind and we must catch up
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Wait for meeeeeeeeeee!");
                    thisInst.AvoidStuff = false;
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 1;
                    thisInst.featherBoost = true;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5;
                }
                else if (thisInst.Urgency > 5 && thisInst.recentSpeed < 6)
                {
                    //bloody tree moment
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": GET OUT OF THE WAY NUMBNUT!");
                    thisInst.AvoidStuff = false;
                    thisInst.FIRE_NOW = true;
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 0.5f;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5;
                }
                //OBSTRUCTION MANAGEMENT
                if (dist >= range + playerExt + 10 && !thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true);
                }
                else if (!thisInst.IsTechMoving(thisInst.EstTopSped / 8))
                {
                    // Moving a bit too slow for what we can do
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Trying to catch up!");
                    thisInst.Urgency += KickStart.AIClockPeriod / 5;
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 1;
                }
                else
                {
                    //Things are going smoothly
                    thisInst.AvoidStuff = true;
                    thisInst.SettleDown();
                }
                thisInst.lastMoveAction = 1;
            }
            else if (dist < (range / 4) + playerExt)
            {
                //Likely stationary
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
                thisInst.AvoidStuff = true;
                thisInst.lastMoveAction = 0;
                thisInst.SettleDown();
                if (thisInst.DelayedAnchorClock < 15)
                    thisInst.DelayedAnchorClock++;
                if (thisInst.AutoAnchor && tank.Anchors.NumPossibleAnchors >= 1 && thisInst.DelayedAnchorClock >= 15 && !thisInst.DANGER)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && thisInst.anchorAttempts <= 6)
                    {
                        Debug.Log("TACtical_AI: AI " + tank.name + ":  Setting camp!");
                        tank.TryToggleTechAnchor();
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
                thisInst.lastMoveAction = 0;
                thisInst.DriveVar = 0;
                if (thisInst.DelayedAnchorClock < 15)
                    thisInst.DelayedAnchorClock++;
                if (thisInst.AutoAnchor && tank.Anchors.NumPossibleAnchors >= 1 && thisInst.DelayedAnchorClock >= 15 && !thisInst.DANGER)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && thisInst.anchorAttempts <= 6)
                    {
                        Debug.Log("TACtical_AI: AI " + tank.name + ":  Setting camp!");
                        tank.TryToggleTechAnchor();
                        thisInst.anchorAttempts++;
                    }
                }
            }
        }
    }

}
