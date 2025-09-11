using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;

namespace TAC_AI.AI.AlliedOperations
{
    internal static class BEscort
    {
        public static void MotivateMove(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            helper.IsMultiTech = false;
            helper.foundGoal = false;
            helper.Attempt3DNavi = false;

            BGeneral.ResetValues(helper, ref direct);
            helper.AvoidStuff = true;

            helper.lastPlayer = helper.GetPlayerTech();
            if (helper.lastPlayer == null)
                return;
            bool hasMessaged = false;
            if (helper.lastPlayer == tank.visible)
            {   // WE ARE FOLLOWING OURSELVES, just hold position!
                //DebugTAC_AI.Log("Player Tech: We are following ourselves!!!!");
                OnIdle(helper, tank, ref direct, ref hasMessaged);
                direct.STOP(helper);
                return;
            }
            /*
            Vector3 veloFlat;
            if ((bool)tank.rbody)   // So that drifting is minimized
            {
                veloFlat = tank.rbody.velocity;
                veloFlat.y = 0;
            }*/

            float range = helper.MaxObjectiveRange + helper.lastTechExtents;
            float playerExt = helper.lastPlayer.GetCheapBounds();
            float dist = helper.GetDistanceFromTask2D(helper.lastPlayer.tank.boundsCentreWorldNoCheck, helper.lastPlayer.GetCheapBounds());

            if ((bool)helper.lastEnemyGet && !helper.Retreat)
            {   // combat pilot will take care of the rest
                //OBSTRUCTION MANAGEMENT
                if (!helper.IsTechMovingAbs(helper.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend))
                {
                    helper.TryHandleObstruction(hasMessaged, dist, true, true, ref direct);
                }
                else
                {
                    helper.SettleDown();
                    helper.AvoidStuff = true;
                }
                return;
            }

            if (dist < helper.lastTechExtents + playerExt + 2)
            {
                helper.DelayedAnchorClock = 0;
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Giving the player some room...");
                direct.DriveAwayFacingTowards();
                if (helper.unanchorCountdown > 0)
                    helper.unanchorCountdown--;
                if (helper.CanAutoUnanchor)
                {
                    helper.unanchorCountdown = 15;
                    helper.Unanchor();
                }
            }
            else if (dist < range + playerExt && dist > (range * 0.75f) + playerExt)
            {
                // Time to go!
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Departing!");
                direct.DriveToFacingTowards();
                helper.DelayedAnchorClock = 0;
                if (helper.unanchorCountdown > 0)
                    helper.unanchorCountdown--;
                if (helper.CanAutoUnanchor)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Time to pack up and move out!");
                    helper.unanchorCountdown = 15;
                    helper.Unanchor();
                }
            }
            else if (dist >= range + playerExt)
            {
                helper.DelayedAnchorClock = 0;
                direct.DriveToFacingTowards();
                //helper.ThrottleState = AIThrottleState.ForceSpeed;
                //helper.DriveVar = 1f;


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
                //OBSTRUCTION MANAGEMENT
                if (!helper.IsTechMovingAbs(helper.EstTopSped / 5))
                {
                    helper.TryHandleObstruction(hasMessaged, dist, false, true, ref direct);
                    return;
                }
                else if (!helper.IsTechMovingAbs(helper.EstTopSped / 3))
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
                    helper.SettleDown();
                }
                /*
                if (helper.UrgencyOverload > 50)
                {
                    //Are we just randomly angry for too long? let's fix that
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Overloaded urgency!  ReCalcing top speed!");
                    helper.EstTopSped = 1;
                    helper.AvoidStuff = true;
                    helper.UrgencyOverload = 0;
                }*/
                //URGENCY REACTION
                if (helper.Urgency > 40)
                {
                    //FARRR behind! BOOSTERS NOW!
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": I AM SUPER FAR BEHIND!");
                    helper.AvoidStuff = false;
                    helper.FullBoost = true; // WE ARE SOO FAR BEHIND
                    helper.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (helper.Urgency > 15)
                {
                    //Behind and we must catch up
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": Wait for meeeeeeeeeee!");
                    helper.AvoidStuff = false;
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 1;
                    helper.LightBoost = true;
                    helper.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
                else if (helper.Urgency > 5 && helper.recentSpeed < 6)
                {
                    //bloody tree moment
                    hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ": GET OUT OF THE WAY NUMBNUT!");
                    //helper.AvoidStuff = false;
                    helper.FIRE_ALL = true;
                    helper.ThrottleState = AIThrottleState.ForceSpeed;
                    helper.DriveVar = 0.5f;
                    helper.UrgencyOverload += KickStart.AIClockPeriod / 5f;
                }
            }
            else if (dist < (range / 2) + playerExt)
            {
                //Likely stationary
                hasMessaged = AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Settling");
                helper.AvoidStuff = true;
                helper.SettleDown();
                if ((bool)helper.lastEnemyGet)
                {
                    helper.ThrottleState = AIThrottleState.PivotOnly;
                }

                if (helper.DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                    helper.DelayedAnchorClock++;
                if (helper.CanAutoAnchor)
                {
                    AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
                    helper.TryInsureAnchor();
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
            helper.AvoidStuff = true;
            helper.SettleDown();
            if ((bool)helper.lastEnemyGet)
            {
                helper.ThrottleState = AIThrottleState.PivotOnly;
            }

            //helper.DriveVar = 0;
            if (helper.DelayedAnchorClock < AIGlobals.BaseAnchorMinimumTimeDelay)
                helper.DelayedAnchorClock++;
            if (helper.CanAutoAnchor)
            {
                AIECore.AIMessage(tank, ref hasMessaged, tank.name + ":  Setting camp!");
                helper.TryInsureAnchor();
            }
        }
    }
}
