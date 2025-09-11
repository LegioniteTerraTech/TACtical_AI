using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement.AICores
{
    internal class HelicopterUtils
    {
        public static void AngleTowardsUp(AIControllerAir pilot, Vector3 positionToMoveTo, 
            Vector3 positionToLookAt, ref EControlCoreSet core, bool ForceAccend = false)
        {
            TankAIHelper helper = pilot.Helper;
            Tank tank = pilot.Tank;
            //AI Steering Rotational
            Vector3 turnVal;
            float upVal = tank.rootBlockTrans.up.y;
            //bool isMostlyInControl = upVal > 0.4f;
            bool isInControl;
            if (helper.FullMelee)
            {
                isInControl = upVal >= 0.375f;
            }
            else
                isInControl = upVal > 0.425f;
            DeterminePitchRoll(tank, pilot, positionToMoveTo, positionToLookAt, helper, !isInControl, isInControl, ref core);
            Vector3 fwdDelta;
            if (ForceAccend || !isInControl)
            {
                Vector3 forwardFlat = helper.Navi3DDirect;
                forwardFlat.y = -tank.rootBlockTrans.forward.y;
                forwardFlat.Normalize();
                helper.Navi3DDirect = forwardFlat;
                helper.Navi3DUp = Vector3.up;
                fwdDelta = tank.rootBlockTrans.InverseTransformDirection(forwardFlat);
                turnVal = AIGlobals.LookRot(fwdDelta, tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
            }
            else
            {
                fwdDelta = tank.rootBlockTrans.InverseTransformDirection(helper.Navi3DDirect);
                turnVal = AIGlobals.LookRot(fwdDelta, tank.rootBlockTrans.InverseTransformDirection(helper.Navi3DUp)).eulerAngles;
            }
            bool needsTurnControl = fwdDelta.z < 0.65f;

            //Convert turnVal to runnable format

            turnVal.x = Mathf.Clamp(-(AIGlobals.AngleUnsignedToSigned(turnVal.x) / pilot.FlyingChillFactor.x), -1, 1);

            turnVal.y = Mathf.Clamp(-(AIGlobals.AngleUnsignedToSigned(turnVal.y) / pilot.FlyingChillFactor.y), -1, 1);

            turnVal.z = Mathf.Clamp(-(AIGlobals.AngleUnsignedToSigned(turnVal.z) / pilot.FlyingChillFactor.z), -1, 1);

            //Stop Wobble
            if (Mathf.Abs(turnVal.x) < 0.05f)
                turnVal.x = 0;
            if (Mathf.Abs(turnVal.y) < 0.05f)
                turnVal.y = 0;
            if (Mathf.Abs(turnVal.z) < 0.05f)
                turnVal.z = 0;

            // limit rotation speed


            // stop pitching if the main prop is trying to force us into the ground
            if (helper.SafeVelocity.y < -6)
                turnVal.y = 0;


            //Turn our work in to process
            Vector3 TurnVal = turnVal.Clamp01Box();

            // -----------------------------------------------------------------------------------------------
            // -----------------------------------------------------------------------------------------------
            // -----------------------------------------------------------------------------------------------
            // DRIVE
            float xOffset = 0;
            if (core.DriveDir == EDriveFacing.Perpendicular && helper.lastEnemyGet != null)
            {
                if (helper.LocalSafeVelocity.x > 0)
                    xOffset = 0.4f;
                else
                    xOffset = -0.4f;
            }

            Vector3 DriveVar = -helper.LocalSafeVelocity / pilot.PropLerpValue;
            float xFactor = fwdDelta.z - 0.65f;
            if (needsTurnControl)
                DriveVar.x = 0;
            else
                DriveVar.x = Mathf.Clamp(DriveVar.x + xOffset, -1, 1) * xFactor;
            DriveVar.z = Mathf.Clamp(DriveVar.z , -1, 1);
            DriveVar.y = 0;
            if (isInControl)
            {
                Vector3 nudge = tank.rootBlockTrans.InverseTransformPoint(positionToMoveTo) / helper.lastTechExtents;
                if (helper.ThrottleState == AIThrottleState.PivotOnly)
                {
                    // Do nothing and let the inertia dampener kick in
                }
                else if (helper.IsDirectedMovingFromDest)
                {
                    if (!needsTurnControl)
                        DriveVar.x = -nudge.x * xFactor;
                    DriveVar.z = -nudge.z;
                }
                else if (helper.IsDirectedMovingToDest)
                {
                    if (!needsTurnControl)
                        DriveVar.x = nudge.x * xFactor;
                    DriveVar.z = nudge.z;
                }
            }
            //DriveVar = DriveVar.normalized;
            DriveVar.y = pilot.CurrentThrottle;

            //Turn our work in to processing
            Vector3 DriveVal = DriveVar.Clamp01Box();

            /*
            if (TurnVal.x != 0 && TurnVal.y != 0 && TurnVal.z != 0)
            {   // Controls saturated, for some reason when two turning inputs are maxed, the third stops doing anything
                //  We must ignore our WEAKEST input to keep control!
                if (Mathf.Abs(TurnVal.x) > 0.5f)
                    TurnVal.SetY(0);
                else
                {
                    int lowest = 0;
                    float most = 2;
                    for (int i = 0; i < 3; i++)
                    {
                        float val = Mathf.Abs(TurnVal[i]);
                        if (val < most)
                        {
                            lowest = i;
                            most = val;
                        }
                    }
                    switch (lowest)
                    {
                        case 0:
                            TurnVal.SetX(0);
                            break;
                        case 1:
                            TurnVal.SetY(0);
                            break;
                        case 2:
                            TurnVal.SetZ(0);
                            break;
                    }
                }
            }//*/
            

            if (AIGlobals.ShowDebugFeedBack)
            {
                // DEBUG FOR DRIVE ERRORS
                // Teal is target vector
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, positionToMoveTo - tank.boundsCentreWorldNoCheck, new Color(0, 1, 1));
                // The drive direction - blue means upright, Yellow means correcting
                if (ForceAccend || !isInControl)
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, (tank.rootBlockTrans.TransformPoint(DriveVar) - tank.trans.position).normalized * helper.lastTechExtents, new Color(1, 1, 0));
                else
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, (tank.rootBlockTrans.TransformPoint(DriveVar) - tank.trans.position).normalized * helper.lastTechExtents, new Color(0, 0, 1));
                // The angle facing (upright!) Red
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 2, helper.Navi3DUp * pilot.Helper.lastTechExtents, new Color(1, 0, 0));
                // The angle facing (forwards!) White
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 3, helper.Navi3DDirect * pilot.Helper.lastTechExtents, new Color(1, 1, 1));
            }

            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " | steering " + turnVal + " | drive " + DriveVar);
            if (helper.FixControlReversal(DriveVal.z))
                TurnVal = TurnVal.SetY(-turnVal.y);
            helper.ProcessControl(DriveVal, TurnVal, Vector3.zero, false, false);
        }
        private static void DeterminePitchRoll(Tank tank, AIControllerAir pilot, Vector3 DestPosWorld, Vector3 LookPosWorld, 
            TankAIHelper helper, bool avoidCrash, bool PointAtTarget, ref EControlCoreSet core)
        {
            float pitchDampening = Mathf.Lerp(16, 64, Mathf.InverseLerp(1, 64, helper.lastTechExtents));
            Vector3 Heading;
            if (PointAtTarget)
            {
                Heading = (LookPosWorld - tank.boundsCentreWorldNoCheck).normalized;
            }
            else
                Heading = (DestPosWorld - tank.boundsCentreWorldNoCheck).normalized;
            Vector3 fFlat = Heading;

            Vector3 tankForward = tank.rootBlockTrans.forward;
            float tankUp = tank.rootBlockTrans.up.y;
            Vector3 directUp;
            Vector3 rFlat;
            Vector3 veloLocal = helper.LocalSafeVelocity;
            bool inertiaDampen;
            switch (helper.ThrottleState)
            {
                case AIThrottleState.PivotOnly:
                    inertiaDampen = true;
                    break;
                case AIThrottleState.Yield:
                    if (veloLocal.z > AIGlobals.YieldSpeed)
                        inertiaDampen = true;
                    else if (veloLocal.z < -AIGlobals.YieldSpeed)
                        inertiaDampen = true;
                    else
                        inertiaDampen = false;
                    break;
                case AIThrottleState.FullSpeed:
                case AIThrottleState.ForceSpeed:
                    inertiaDampen = false;
                    break;
                default:
                    throw new NotImplementedException("Unknown ThrottleState " + helper.ThrottleState);
            }
            // Pitch axis turning
            if (!inertiaDampen && !PointAtTarget)
            {   // Try balance
                fFlat.y = 0;
                fFlat.Normalize();
                // Rotors on some chopper designs were acting funky and cutting out due to pitch so I disabled pitching
                if (pilot.LowerEngines || avoidCrash)
                    fFlat.y = 0;
                else if (Vector2.Dot(Heading.ToVector2XZ(), tankForward.ToVector2XZ()) < AIGlobals.ChopperAngleDoPitchPercent)
                {   // Pitch to recover control
                    inertiaDampen = true;
                }
                else
                {   // Pitch to speed up advance
                    if (helper.IsDirectedMovingFromDest)
                        fFlat.y = Mathf.Clamp((veloLocal.z / (pitchDampening / pilot.SlowestPropLerpSpeed))
                            + AIGlobals.ChopperAngleNudgePercent, -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
                    else if (helper.IsDirectedMovingToDest)
                        fFlat.y = Mathf.Clamp((veloLocal.z / (pitchDampening / pilot.SlowestPropLerpSpeed))
                            - AIGlobals.ChopperAngleNudgePercent, -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
                    else
                        fFlat.y = Mathf.Clamp(veloLocal.z / (pitchDampening / pilot.SlowestPropLerpSpeed),
                            -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
                }

            }
            if (inertiaDampen)
            {
                if (veloLocal.z > AIGlobals.ChopperSpeedCounterPitch)
                    fFlat.y = Mathf.Clamp((veloLocal.z / (pitchDampening / pilot.SlowestPropLerpSpeed))
                        + AIGlobals.ChopperAngleNudgePercent, -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
                else if (veloLocal.z < -AIGlobals.ChopperSpeedCounterPitch)
                    fFlat.y = Mathf.Clamp((veloLocal.z / (pitchDampening / pilot.SlowestPropLerpSpeed))
                        - AIGlobals.ChopperAngleNudgePercent, -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
                else
                    fFlat.y = Mathf.Clamp(veloLocal.z / (pitchDampening / pilot.SlowestPropLerpSpeed),
                        -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
            }
            // Because tilting forwards too hard causes the chopper to stall on some builds
            //fFlat.y = fFlat.y - (fFlat.y * pilot.CurrentThrottle);



            // Roll axis turning
            if (tankUp > 0)
                rFlat = tank.rootBlockTrans.right;
            else
                rFlat = -tank.rootBlockTrans.right.SetY(0).normalized;
            if (core.DriveDir == EDriveFacing.Perpendicular)
            {   // orbit while firing
                if (veloLocal.x >= 0)
                    rFlat.y = Mathf.Clamp((veloLocal.x / (pitchDampening / pilot.SlowestPropLerpSpeed)) - AIGlobals.ChopperAngleNudgePercent, 
                        -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
                else
                    rFlat.y = Mathf.Clamp((veloLocal.x / (pitchDampening / pilot.SlowestPropLerpSpeed)) + AIGlobals.ChopperAngleNudgePercent,
                        -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);
            }
            else
                rFlat.y = Mathf.Clamp(veloLocal.x / (pitchDampening / pilot.SlowestPropLerpSpeed), 
                    -AIGlobals.ChopperMaxDeltaAnglePercent, AIGlobals.ChopperMaxDeltaAnglePercent);

            directUp = Vector3.Cross(tankForward, rFlat.normalized);
            if (directUp.y < AIGlobals.ChopperMaxDeltaAnglePercent)
                directUp = Vector3.up;
            else
                directUp.Normalize();


            helper.Navi3DDirect = fFlat.normalized;
            helper.Navi3DUp = directUp;
        }
        internal class HelperGUI : MonoBehaviour
        {
            static HelperGUI helpGUI = null;
            private Rect Window = new Rect(0, 0, 280, 145);
            public static void Init()
            {
                if (helpGUI == null)
                    helpGUI = new GameObject().AddComponent<HelperGUI>();
            }
            public void OnGUI()
            {
                try
                {
                    Window = GUI.Window(2958715, Window, GUIWindow, "Settings");
                }
                catch { }
            }
            private void GUIWindow(int ID)
            {
                GUILayout.Label("ChopperThrottleAntiBounce: " + AIGlobals.ChopperDownAntiBounce.ToString("F"));
                AIGlobals.ChopperDownAntiBounce = Mathf.Round(GUILayout.HorizontalSlider(
                    AIGlobals.ChopperDownAntiBounce, 0.5f, 2f) * 20f) / 20f;
                GUILayout.Label("ChopperThrottleDamper: " + AIGlobals.ChopperThrottleDamper.ToString("F"));
                AIGlobals.ChopperThrottleDamper = Mathf.Round(GUILayout.HorizontalSlider(
                    AIGlobals.ChopperThrottleDamper, 1f, 5f) * 20f) / 20f;
                GUI.DragWindow();
            }
        }
        public static float ThrustLeveler = 1f;
        public static int Iterations = 0;
        public static float ModerateUpwardsThrust(Tank tank, TankAIHelper helper, AIControllerAir pilot, float targetHeight, bool ForceUp = false)
        {
            //HelperGUI.Init();
            pilot.LowerEngines = false;
            float final;
            //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " thrust = " + final + " | velocity " + helper.LocalSafeVelocity;
            if (ForceUp)
                final = 1;
            else if (tank.rbody)
            {   // height compensator
                // works bloody AMAZING!
                final = pilot.MainThrottle;
                float speedDownTime, deltaDists, beginStopDist;
                float deltaHeight = targetHeight - tank.boundsCentreWorldNoCheck.y;
                float throttleLerp = pilot.SlowestPropLerpSpeed * Time.deltaTime * 0.9f;
                float throttleCurTo0 = final / pilot.SlowestPropLerpSpeed;
                float accel = ((pilot.UpTtWRatio * final) - 1f) * TankAIManager.GravMagnitude;
                //float MaxThrottleAccelDelta = (((pilot.UpTtWRatio * (final + pilot.SlowestPropLerpSpeed)) - 1f) * TankAIManager.GravMagnitude) - accel;
                float velo = helper.SafeVelocity.y;
                float stopDist = 0;
                if (accel > 0 != velo > 0)
                    stopDist = velo * Mathf.Abs(velo) / Mathf.Abs(accel * 2);

                if (deltaHeight < 0)
                {   // Down
                    if (velo > 0.25f)
                    {   // Wrong way
                        final -= throttleLerp;
                    }
                    else
                    {
                        deltaDists = Mathf.Abs(stopDist / deltaHeight);
                        beginStopDist = Mathf.Clamp01(1.1f - deltaDists);
                        speedDownTime = Mathf.Abs(velo / accel) * AIGlobals.ChopperDownAntiBounce;
                        if (deltaDists > 1f)
                        {   // We might need to try again 
                            final += throttleLerp;
                            //DebugTAC_AI.Log("Down OVERSHOOT " + final.ToString("0.00"));
                        }
                        else if (speedDownTime < throttleCurTo0 + 0.5f)
                        {
                            final += throttleLerp * Mathf.Clamp01((throttleCurTo0 + AIGlobals.ChopperThrottleDamper) * 
                                beginStopDist / speedDownTime);
                            //DebugTAC_AI.Log("Down ReboundI " + final.ToString("0.00"));
                        }
                        else if (speedDownTime < throttleCurTo0 + AIGlobals.ChopperThrottleDamper)
                        {
                            final += throttleLerp * Mathf.Clamp01((throttleCurTo0 + AIGlobals.ChopperThrottleDamper) *
                                beginStopDist / speedDownTime);
                            //DebugTAC_AI.Log("Down Rebound " + final.ToString("0.00"));
                        }
                        else
                        {
                            final -= throttleLerp * beginStopDist;
                            //DebugTAC_AI.Log("Down " + final.ToString("0.00"));
                        }
                    }
                }
                else
                {   // Up
                    if (velo < -0.25f)
                    {   // Wrong way
                        final += throttleLerp;
                    }
                    else
                    {
                        deltaDists = Mathf.Abs(stopDist / deltaHeight);
                        beginStopDist = Mathf.Clamp01(1.1f - deltaDists);
                        speedDownTime = Mathf.Abs(velo / accel);
                        if (speedDownTime < throttleCurTo0)
                        {   // We might need to try again 
                            final -= throttleLerp;
                            //DebugTAC_AI.Log("Up OVERSHOOT " + final.ToString("0.00"));
                        }
                        else if (speedDownTime < throttleCurTo0 + 0.5f)
                        {
                            final -= throttleLerp * Mathf.Clamp01((throttleCurTo0 + AIGlobals.ChopperThrottleDamper) *
                                beginStopDist / speedDownTime);
                            //DebugTAC_AI.Log("Up ReboundI " + final.ToString("0.00"));
                        }
                        else if (speedDownTime < throttleCurTo0 + AIGlobals.ChopperThrottleDamper)
                        {
                            final -= throttleLerp * Mathf.Clamp01((throttleCurTo0 + AIGlobals.ChopperThrottleDamper) * 
                                beginStopDist / speedDownTime);
                            //DebugTAC_AI.Log("Up Rebound " + final.ToString("0.00"));
                        }
                        else
                        {
                            final += throttleLerp * beginStopDist;
                            //DebugTAC_AI.Log("Up " + final.ToString("0.00"));
                        }
                    }
                }


                /*
                float rampDownTimeThrottle = RampDownTime(pilot, pilot.MainThrottle);
                float throttle0toMaxTimeSec = ThrustLeveler / Math.Max(pilot.SlowestPropLerpSpeed, 0.001f);
                float deltaVelo = targetHeight - tank.boundsCentreWorldNoCheck.y - (helper.SafeVelocity.y * throttle0toMaxTimeSec);

                float timeToReachDeltaVelo;
                float deltaThrottle;
                for (int i = 0; i < Iterations; i++)
                {
                    if (deltaVelo < 0f)
                    {
                        deltaThrottle = -pilot.SlowestPropLerpSpeed * 0.9f;
                        float curAccel = ((pilot.UpTtWRatio * final) - 1f) * TankAIManager.GravMagnitude;
                        if (curAccel < 0f)
                            timeToReachDeltaVelo = deltaVelo / Mathf.Min(curAccel, -0.001f);
                        else
                            timeToReachDeltaVelo = 9001f;
                    }
                    else
                    {
                        deltaThrottle = pilot.SlowestPropLerpSpeed * 0.9f;
                        float curAccel = ((pilot.UpTtWRatio * final) - 1f) * TankAIManager.GravMagnitude;
                        if (curAccel > 0f)
                            timeToReachDeltaVelo = deltaVelo / Mathf.Max(curAccel, 0.001f);
                        else
                            timeToReachDeltaVelo = 9001f;
                    }

                    if (timeToReachDeltaVelo > throttle0toMaxTimeSec)
                        final += (deltaThrottle * Time.deltaTime);
                    else // timeToReachDeltaVelo <= predictTime
                        final += pilot.MainThrottle + (deltaThrottle * Time.deltaTime *
                            (timeToReachDeltaVelo / throttle0toMaxTimeSec));
                }
                */
            }
            else
                final = 0f;
            //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " thrustFinal = " + final);

            if (final > 1.25f && pilot.BoostBias.y > 0.65f)
                helper.FullBoost = true;
            /*
            else
                helper.FullBoost = helper.lastOperatorRange > AIGlobals.GroundAttackStagingDist / 3;
            */
            return Mathf.Clamp(final, -0.1f, 1);
        }
        public static float ModerateUpwardsThrust_LEGACY(Tank tank, TankAIHelper helper, AIControllerAir pilot, float targetHeight, bool ForceUp = false)
        {
            //HelperGUI.Init();
            pilot.LowerEngines = false;
            float dampScale = 4f / pilot.PropLerpValue;
            float mulVal = Mathf.Clamp01(8f / pilot.PropLerpValue);// 4 /
            float final = ((targetHeight - tank.boundsCentreWorldNoCheck.y) * mulVal) + pilot.UpTtWRatio;
            //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " thrust = " + final + " | velocity " + helper.LocalSafeVelocity;
            if (ForceUp)
                final = 1;
            else
            {
                if (final > 1.6f)
                { 
                }
                if (final > 1.2f)
                {
                    if (helper.SafeVelocity.y > 4f)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " dampening up speed");
                        final = Mathf.Max(final - Mathf.Pow(Mathf.Max(helper.SafeVelocity.y - 4f, 0), 2) * dampScale, 0);
                    }
                }
                else if (final > 0.8f)
                {
                    if (helper.SafeVelocity.y > 2f)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " dampening up speed");
                        final = Mathf.Max(final - Mathf.Pow(Mathf.Max(helper.SafeVelocity.y - 2f, 0), 2) * dampScale, 0);
                    }
                }
                else if (final > 0.4f)
                {
                    if (helper.SafeVelocity.y > 1f)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " dampening up speed");
                        final = Mathf.Max(final - Mathf.Pow(Mathf.Max(helper.SafeVelocity.y - 1f, 0), 2) * dampScale * 0.8f, 0);
                    }
                }
                else if (final >= 0f)
                {
                    if (helper.SafeVelocity.y > 0f)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " dampening up speed");
                        final = Mathf.Max(final - Mathf.Pow(helper.SafeVelocity.y, 2) * dampScale * 0.4f, 0);
                    }
                }
                else if (final > -0.1f)
                    final = -0.1f;
                else if (final > -0.4f)
                {
                    pilot.LowerEngines = true;
                    final = 0f;
                    if (helper.SafeVelocity.y < 0f)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " dampening fall");
                        final = Mathf.Pow(helper.SafeVelocity.y, 2) * dampScale * 0.4f;
                    }
                }
                else if (final > -0.8f)
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " Too high!! Cutting engines!!");
                    pilot.LowerEngines = true;
                    final = -0.1f;
                    if (helper.SafeVelocity.y < 0f)
                    {
                        final = Mathf.Pow(helper.SafeVelocity.y, 2) * dampScale * 0.8f;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " Too high!! Cutting engines!!");
                    pilot.LowerEngines = true;
                    final = -0.1f;
                    if (helper.SafeVelocity.y < 0f)
                    {
                        final = Mathf.Pow(helper.SafeVelocity.y, 2) * dampScale;
                    }
                }
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " thrustFinal = " + final);

            if (final > 1.25f && pilot.BoostBias.y > 0.65f)
                helper.FullBoost = true;
            else
                helper.FullBoost = helper.lastOperatorRange > AIGlobals.GroundAttackStagingDist / 3;

            return Mathf.Clamp(final, -0.1f, 1);
        }
        public static void UpdateThrottleCopter(AIControllerAir pilot)
        {
            TankAIHelper helper = pilot.Helper;
            if (pilot.NoProps)
            {
                if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGroundTech(helper, helper.GroundOffsetHeight) && !pilot.Tank.beam.IsActive)
                    helper.MaxBoost();
            }
            else
            {
                if (pilot.ForcePitchUp)
                    helper.MaxProps();
                else if (helper.FullBoost)
                    helper.MaxBoost();
            }
            pilot.CurrentThrottle = Mathf.Clamp(pilot.MainThrottle, 0, 1);
        }
    }
}
