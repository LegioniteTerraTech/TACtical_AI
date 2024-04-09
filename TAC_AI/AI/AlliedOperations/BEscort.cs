using System;
using System.Collections.Generic;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    internal struct BEscort
    {
        public void Init(TankAIHelper thisInst)
        {
        }
        public void DeInit(TankAIHelper thisInst)
        {
        }
        public void MovementActions(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
        }

        public static void MotivateMove(TankAIHelper thisInst, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            thisInst.IsMultiTech = false;
            thisInst.foundGoal = false;
            thisInst.Attempt3DNavi = false;

            BGeneral.ResetValues(thisInst, ref direct);
            thisInst.AvoidStuff = true;

            thisInst.lastPlayer = thisInst.GetPlayerTech();
            if (thisInst.lastPlayer == null)
                return;
            /*
            Vector3 veloFlat;
            if ((bool)tank.rbody)   // So that drifting is minimized
            {
                veloFlat = tank.rbody.velocity;
                veloFlat.y = 0;
            }*/
            bool hasMessaged = false;

            float dist = thisInst.GetDistanceFromTask(thisInst.lastPlayer.tank.boundsCentreWorldNoCheck, thisInst.lastPlayer.GetCheapBounds());
            float range = thisInst.MaxObjectiveRange + thisInst.lastTechExtents;
            float playerExt = thisInst.lastPlayer.GetCheapBounds();

            if ((bool)thisInst.lastEnemyGet && !thisInst.Retreat)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                else
                {
                    thisInst.SettleDown();
                    thisInst.AvoidStuff = true;
                }
                return;
            }

            if (dist < thisInst.lastTechExtents + playerExt + 2)
            {
                thisInst.DelayedAnchorClock = 0;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving the player some room...");
                direct.DriveAwayFacingTowards();
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
                direct.DriveToFacingTowards();
                thisInst.anchorAttempts = 0; thisInst.DelayedAnchorClock = 0;
                if (thisInst.unanchorCountdown > 0)
                    thisInst.unanchorCountdown--;
                if (thisInst.AutoAnchor && thisInst.PlayerAllowAutoAnchoring && tank.Anchors.NumPossibleAnchors >= 1)
                {
                    if (tank.Anchors.NumIsAnchored > 0)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Time to pack up and move out!");
                        thisInst.unanchorCountdown = 15;
                        thisInst.UnAnchor();
                    }
                }
            }
            else if (dist >= range + playerExt)
            {
                thisInst.DelayedAnchorClock = 0;
                direct.DriveToFacingTowards();
                //thisInst.ForceSetDrive = true;
                //thisInst.DriveVar = 1f;


                //DISTANCE WARNINGS
                if (dist > range * 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Oh Crafty they are too far!");
                    thisInst.Urgency += KickStart.AIClockPeriod / 2f;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 1f;
                    thisInst.LightBoost = true;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI drive " + tank.control.DriveControl);
                    if (thisInst.UrgencyOverload > 0)
                        thisInst.UrgencyOverload -= KickStart.AIClockPeriod / 5f;
                }
                //OBSTRUCTION MANAGEMENT
                if (!thisInst.IsTechMoving(thisInst.EstTopSped / 5))
                {
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    return;
                }
                else if (!thisInst.IsTechMoving(thisInst.EstTopSped / 3))
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
                    thisInst.SettleDown();
                }
                /*
                if (thisInst.UrgencyOverload > 50)
                {
                    //Are we just randomly angry for too long? let's fix that
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                    thisInst.EstTopSped = 1;
                    thisInst.AvoidStuff = true;
                    thisInst.UrgencyOverload = 0;
                }*/
                //URGENCY REACTION
                if (thisInst.Urgency > 40)
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
                    //thisInst.AvoidStuff = false;
                    thisInst.FIRE_ALL = true;
                    thisInst.ForceSetDrive = true;
                    thisInst.DriveVar = 0.5f;
                    thisInst.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
            }
            else if (dist < (range / 2) + playerExt)
            {
                //Likely stationary
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
                thisInst.AvoidStuff = true;
                thisInst.SettleDown();
                if ((bool)thisInst.lastEnemyGet)
                {
                    thisInst.PivotOnly = true;
                }

                if (thisInst.DelayedAnchorClock < 15)
                    thisInst.DelayedAnchorClock++;
                if (thisInst.CanAutoAnchor)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && thisInst.anchorAttempts <= AIGlobals.AlliedAnchorAttempts)
                    {
                        AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
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
                if ((bool)thisInst.lastEnemyGet)
                {
                    thisInst.PivotOnly = true;
                }

                //thisInst.DriveVar = 0;
                if (thisInst.DelayedAnchorClock < 15)
                    thisInst.DelayedAnchorClock++;
                if (thisInst.CanAutoAnchor)
                {
                    if (tank.Anchors.NumIsAnchored == 0 && thisInst.anchorAttempts <= AIGlobals.AlliedAnchorAttempts)
                    {
                        AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
                        thisInst.TryAnchor();
                        thisInst.anchorAttempts++;
                    }
                }
            }
        }
    }
}
