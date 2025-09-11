using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BBuccaneer {
        //Same as Airship but levels out with the sea and avoids terrain
        public static void MotivateBote(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the naval ship (Escort) what to do movement-wise
            helper.lastPlayer = helper.GetPlayerTech();
            helper.IsMultiTech = false;
            helper.Attempt3DNavi = true;

            if (!KickStart.isWaterModPresent)
            {
                //Fallback to normal escort if no watermod present
                helper.DediAI = AIType.Escort;
                return;
            }
            BGeneral.ResetValues(helper, ref direct);
            helper.AvoidStuff = true;

            if (helper.lastPlayer == null)
                return;
            bool hasMessaged = false;
            //direct.SetLastDest(helper.lastPlayer.tank.boundsCentreWorldNoCheck);
            // Disabling the above causes the AI to move as expected.
            if (helper.lastPlayer == tank.visible)
            {   // WE ARE FOLLOWING OURSELVES, just hold position!
                OnIdle(helper, tank, ref direct, ref hasMessaged);
                direct.STOP(helper);
                return;
            }
            float playerExt = helper.lastPlayer.GetCheapBounds();
            float dist = helper.GetDistanceFromTask(helper.lastPlayer.tank.boundsCentreWorldNoCheck, helper.lastPlayer.GetCheapBounds());
            float range = helper.MaxObjectiveRange + helper.lastTechExtents + playerExt;


            if ((bool)helper.lastEnemyGet && !helper.Retreat)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    helper.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                return;
            }

            if (dist < helper.lastTechExtents + playerExt + 2)
            {
                helper.DelayedAnchorClock = 0;
                hasMessaged = AIECore.AIMessage(tech: tank, ref hasMessaged, tank.name + ":  Giving the player some room...");
                direct.DriveDest =  EDriveDest.FromLastDestination;
                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = -1;
                //direct.lastDestination = helper.lastPlayer.centrePosition;
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
            else if (dist < range + playerExt && dist > (range * 0.75f))
            {
                // Time to go!
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Departing!");
                direct.DriveDest = EDriveDest.ToLastDestination;
                //direct.lastDestination = OffsetToSea(helper.lastPlayer.centrePosition, helper);
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
            else if (dist >= range + playerExt)
            {
                helper.DelayedAnchorClock = 0;
                direct.DriveDest = EDriveDest.ToLastDestination;
                //direct.lastDestination = OffsetToSea(helper.lastPlayer.centrePosition, helper);
                helper.ThrottleState = AIThrottleState.ForceSpeed;
                helper.DriveVar = 1f;


                //DISTANCE WARNINGS
                if (dist > range * 2)
                {
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Oh Crafty they are too far!");
                    helper.Urgency += KickStart.AIClockPeriod / 2f;
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 1f;
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI drive " + tank.control.DriveControl);
                    if (helper.UrgencyOverload > 0)
                        helper.UrgencyOverload--;
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
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Trying to catch up!");
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
            else if (dist < (range / 2))
            {
                //Likely stationary
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
                //direct.lastDestination = OffsetToSea(tank.transform.position, helper);
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
                OnIdle(helper, tank, ref direct, ref hasMessaged);
            }
        }
        private static void OnIdle(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct, ref bool hasMessaged)
        {
            hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  in resting state");
            //direct.lastDestination = OffsetToSea(tank.transform.position, helper);
            helper.AvoidStuff = true;
            helper.SettleDown();
            helper.DriveVar = 0;
            if (helper.DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                helper.DelayedAnchorClock++;
            if (helper.CanAutoAnchor && tank.Anchors.NumIsAnchored == 0)
            {
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
                helper.TryInsureAnchor();
            }
        }
    }
}
