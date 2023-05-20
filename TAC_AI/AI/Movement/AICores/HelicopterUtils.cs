using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    internal class HelicopterUtils
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void AngleTowardsUp(TankControl thisControl, AIControllerAir pilot, 
            Vector3 positionToMoveTo, Vector3 positionToLookAt, ref EControlCoreSet core, bool ForceAccend = false)
        {
            AIECore.TankAIHelper thisInst = pilot.Helper;
            Tank tank = pilot.Tank;
            //AI Steering Rotational
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(thisControl);
            Vector3 turnVal;
            float upVal = tank.rootBlockTrans.up.y;
            //bool isMostlyInControl = upVal > 0.4f;
            bool isInControl;
            if (thisInst.FullMelee)
            {
                isInControl = upVal >= 0.1f;
            }
            else
                isInControl = upVal > 0.425f;
            DeterminePitchRoll(tank, pilot, positionToMoveTo, positionToLookAt, thisInst, !isInControl, isInControl, ref core);
            Vector3 fwdDelta;
            if (ForceAccend || !isInControl)
            {
                Vector3 forwardFlat = thisInst.Navi3DDirect;
                forwardFlat.y = 0;
                forwardFlat.Normalize();
                fwdDelta = tank.rootBlockTrans.InverseTransformDirection(forwardFlat);
                turnVal = Quaternion.LookRotation(fwdDelta, tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
            }
            else
            {
                fwdDelta = tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect);
                turnVal = Quaternion.LookRotation(fwdDelta, tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
            }
            bool needsTurnControl = fwdDelta.z < 0.65f;

            //Convert turnVal to runnable format

            float chillFactorMulti = thisInst.lastTechExtents;
            if (turnVal.x > 180)
                turnVal.x = Mathf.Clamp(-((turnVal.x - 360) / (pilot.FlyingChillFactor.x * chillFactorMulti)), -1, 1);
            else
                turnVal.x = Mathf.Clamp(-(turnVal.x / (pilot.FlyingChillFactor.x * chillFactorMulti)), -1, 1);

            if (turnVal.y > 180)
                turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / pilot.FlyingChillFactor.y), -1, 1);
            else
                turnVal.y = Mathf.Clamp(-(turnVal.y / pilot.FlyingChillFactor.y), -1, 1);

            if (turnVal.z > 180)
                turnVal.z = Mathf.Clamp(-((turnVal.z - 360) / (pilot.FlyingChillFactor.z * chillFactorMulti)), -1, 1);
            else
                turnVal.z = Mathf.Clamp(-(turnVal.z / (pilot.FlyingChillFactor.z * chillFactorMulti)), -1, 1);

            //Stop Wobble
            if (Mathf.Abs(turnVal.x) < 0.05f)
                turnVal.x = 0;
            if (Mathf.Abs(turnVal.y) < 0.05f)
                turnVal.y = 0;
            if (Mathf.Abs(turnVal.z) < 0.05f)
                turnVal.z = 0;

            // limit rotation speed


            // stop pitching if the main prop is trying to force us into the ground
            if (tank.rbody.velocity.y < -6)
                turnVal.y = 0;


            //Turn our work in to process
            control3D.m_State.m_InputRotation = turnVal.Clamp01Box();

            // -----------------------------------------------------------------------------------------------
            // -----------------------------------------------------------------------------------------------
            // -----------------------------------------------------------------------------------------------
            // DRIVE
            float xOffset = 0;
            if (core.DriveDir == EDriveFacing.Perpendicular && thisInst.lastEnemyGet != null)
            {
                if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x > 0)
                    xOffset = 0.4f;
                else
                    xOffset = -0.4f;
            }

            Vector3 DriveVar = tank.rootBlockTrans.InverseTransformVector(-tank.rbody.velocity) / pilot.PropLerpValue;
            float xFactor = fwdDelta.z - 0.65f;
            if (needsTurnControl)
                DriveVar.x = 0;
            else
                DriveVar.x = Mathf.Clamp(DriveVar.x + xOffset, -1, 1) * xFactor;
            DriveVar.z = Mathf.Clamp(DriveVar.z , -1, 1);
            DriveVar.y = 0;
            if (isInControl)
            {
                Vector3 nudge = tank.rootBlockTrans.InverseTransformPoint(positionToMoveTo) / thisInst.lastTechExtents;
                if (thisInst.PivotOnly)
                {
                    // Do nothing and let the inertia dampener kick in
                }
                else if (thisInst.IsDirectedMovingFromDest)
                {
                    if (!needsTurnControl)
                        DriveVar.x = -nudge.x * xFactor;
                    DriveVar.z = -nudge.z;
                }
                else if (thisInst.IsDirectedMovingToDest)
                {
                    if (!needsTurnControl)
                        DriveVar.x = nudge.x * xFactor;
                    DriveVar.z = nudge.z;
                }
            }
            //DriveVar = DriveVar.normalized;
            DriveVar.y = pilot.CurrentThrottle;

            // DEBUG FOR DRIVE ERRORS
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, positionToMoveTo - tank.boundsCentreWorldNoCheck, new Color(0, 1, 1));
            if (ForceAccend || !isInControl)
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, (tank.rootBlockTrans.TransformPoint(DriveVar) - tank.trans.position) * thisInst.lastTechExtents, new Color(1, 1, 0));
            else
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, (tank.rootBlockTrans.TransformPoint(DriveVar) - tank.trans.position) * thisInst.lastTechExtents, new Color(0, 0, 1));
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, thisInst.Navi3DUp * pilot.Helper.lastTechExtents, new Color(1, 0, 0));
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 3, thisInst.Navi3DDirect * pilot.Helper.lastTechExtents, new Color(1, 1, 1));


            //Turn our work in to processing
            //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " | steering " + turnVal + " | drive " + DriveVar);
            control3D.m_State.m_InputMovement = DriveVar.Clamp01Box();
            controlGet.SetValue(tank.control, control3D);
        }
        public static void DeterminePitchRoll(Tank tank, AIControllerAir pilot, Vector3 DestPosWorld, Vector3 LookPosWorld, 
            AIECore.TankAIHelper thisInst, bool avoidCrash, bool PointAtTarget, ref EControlCoreSet core)
        {
            float pitchDampening = 64 * thisInst.lastTechExtents;
            Vector3 Heading;
            if (PointAtTarget)
            {
                Heading = (LookPosWorld - tank.boundsCentreWorldNoCheck).normalized;
            }
            else
                Heading = (DestPosWorld - tank.boundsCentreWorldNoCheck).normalized;
            Vector3 fFlat = Heading;

            Vector3 directUp;
            Vector3 rFlat;

            // Pitch axis turning
            if (!PointAtTarget)
            {   // Try balance
                fFlat.y = 0;
                fFlat.Normalize();

                // Rotors on some chopper designs were acting funky and cutting out due to pitch so I disabled pitching
                if (pilot.LowerEngines || avoidCrash)
                    fFlat.y = 0;
                else if (thisInst.IsDirectedMovingFromDest)
                    fFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z / (pitchDampening / pilot.SlowestPropLerpSpeed)) + 0.15f, -0.25f, 0.25f);
                else if (thisInst.IsDirectedMovingToDest)
                    fFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z / (pitchDampening / pilot.SlowestPropLerpSpeed)) - 0.15f, -0.25f, 0.25f);
                else
                    fFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z / (pitchDampening / pilot.SlowestPropLerpSpeed), -0.25f, 0.25f);

            }
            // Because tilting forwards too hard causes the chopper to stall on some builds
            //fFlat.y = fFlat.y - (fFlat.y * pilot.CurrentThrottle);



            // Roll axis turning
            if (tank.rootBlockTrans.up.y > 0)
                rFlat = tank.rootBlockTrans.right;
            else
                rFlat = -tank.rootBlockTrans.right.SetY(0).normalized;
            if (core.DriveDir == EDriveFacing.Perpendicular)
            {   // orbit while firing
                float veloX = tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x;
                if (veloX >= 0)
                    rFlat.y = Mathf.Clamp((veloX / (pitchDampening / pilot.SlowestPropLerpSpeed)) - 0.15f, -0.25f, 0.25f);
                else
                    rFlat.y = Mathf.Clamp((veloX / (pitchDampening / pilot.SlowestPropLerpSpeed)) + 0.15f, -0.25f, 0.25f);
            }
            else
                rFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x / (pitchDampening / pilot.SlowestPropLerpSpeed), -0.25f, 0.25f);
            directUp = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;



            thisInst.Navi3DDirect = fFlat.normalized;
            thisInst.Navi3DUp = directUp;
        }
        public static float ModerateUpwardsThrust(Tank tank, AIECore.TankAIHelper thisInst, AIControllerAir pilot, Vector3 targetHeight, bool ForceUp = false)
        {
            pilot.LowerEngines = false;
            float final = ((targetHeight.y - tank.boundsCentreWorldNoCheck.y) / (pilot.PropLerpValue / 4)) + AIGlobals.ChopperOperatingExtraPower;
            //DebugTAC_AI.Log("TACtical_AI: " + tank.name + " thrust = " + final + " | velocity " + tank.rbody.velocity);
            if (ForceUp)
            {
                final = 1;
            }
            else if (final <= -0.4f)
            {
                //DebugTAC_AI.Log("TACtical_AI: " + tank.name + " Too high!! Cutting engines!!");
                pilot.LowerEngines = true;
                final = -0.1f;
                if (tank.rbody.velocity.y < 0)
                {
                    final = Mathf.Pow(tank.rbody.velocity.y, 2) /12;
                }
            }
            else if (tank.rbody.velocity.y < 0 && final > -0.4f && final < 0)  // try ease fall
            {
                //DebugTAC_AI.Log("TACtical_AI: " + tank.name + " dampening fall");
                final = Mathf.Pow(tank.rbody.velocity.y, 2) / 10;
            }
            if (tank.rbody.velocity.y > 4 && final > 0 && final < 1.4f)     // try ease flight
            {
                //DebugTAC_AI.Log("TACtical_AI: " + tank.name + " dampening up speed");
                final = 0;
            }
            if (final < -0.1f)
                final = -0.1f;
            //DebugTAC_AI.Log("TACtical_AI: " + tank.name + " thrustFinal = " + final);

            if (final > 1.25f && pilot.BoostBias.y > 0.65f)
                thisInst.FullBoost = true;
            else
                thisInst.FullBoost = thisInst.lastOperatorRange > AIGlobals.GroundAttackStagingDist / 3;

            return Mathf.Clamp(final, -0.1f, 1);
        }
        public static void UpdateThrottleCopter(TankControl control, AIControllerAir pilot)
        {
            AIECore.TankAIHelper thisInst = pilot.Helper;
            control.BoostControlProps = false;
            if (pilot.NoProps)
            {
                if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGroundTech(thisInst, thisInst.GroundOffsetHeight) && !pilot.Tank.beam.IsActive)
                    control.BoostControlJets = true;
                else
                    control.BoostControlJets = false;
            }
            else
            {
                if (pilot.ForcePitchUp)
                {
                    control.BoostControlProps = true;
                    control.BoostControlJets = false;
                }
                else
                    control.BoostControlJets = thisInst.FullBoost;
            }
            pilot.CurrentThrottle = Mathf.Clamp(pilot.MainThrottle, 0, 1);
        }
    }
}
