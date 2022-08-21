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

        public static void AngleTowardsUp(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AIControllerAir pilot, Vector3 positionToMoveTo, Vector3 positionToLookAt, bool ForceAccend = false)
        {
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
                isInControl = upVal > 0.35f;
            DeterminePitchRoll(tank, pilot, positionToMoveTo, positionToLookAt, thisInst, !isInControl, isInControl);
            Vector3 forwardFlat = thisInst.Navi3DDirect;
            forwardFlat.y = 0;
            if (ForceAccend || !isInControl)
            {
                turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(forwardFlat), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
            }
            else
            {
                turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
            }

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
            if (tank.rbody.velocity.y < -2)
                turnVal.y = 0;


            //Turn our work in to process
            control3D.m_State.m_InputRotation = turnVal.Clamp01Box();

            // DRIVE
            float xOffset = 0;
            if (thisInst.DriveDir == EDriveFacing.Perpendicular && thisInst.lastEnemy != null)
            {
                if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x > 0)
                    xOffset = 0.4f;
                else
                    xOffset = -0.4f;
            }

            Vector3 DriveVar = tank.rootBlockTrans.InverseTransformVector(-tank.rbody.velocity) / pilot.PropLerpValue;
            DriveVar.x = Mathf.Clamp(DriveVar.x + xOffset, -1, 1);
            DriveVar.z = Mathf.Clamp(DriveVar.z , -1, 1);
            DriveVar.y = 0;
            if (isInControl)
            {
                Vector3 nudge = tank.rootBlockTrans.InverseTransformPoint(positionToMoveTo) / thisInst.lastTechExtents;
                if (thisInst.PivotOnly)
                {
                    // Do nothing and let the inertia dampener kick in
                }
                else if (thisInst.IsMovingFromDest)
                {
                    DriveVar.x = -nudge.x;
                    DriveVar.z = -nudge.z;
                }
                else if (thisInst.IsMovingToDest)
                {
                    DriveVar.x = nudge.x;
                    DriveVar.z = nudge.z;
                }
            }
            //DriveVar = DriveVar.normalized;
            DriveVar.y = pilot.CurrentThrottle;

            // DEBUG FOR DRIVE ERRORS
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, positionToLookAt - tank.boundsCentreWorldNoCheck, new Color(0, 1, 1));
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, (tank.rootBlockTrans.TransformPoint(DriveVar) - tank.trans.position) * thisInst.lastTechExtents, new Color(0, 0, 1));
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, thisInst.Navi3DUp * pilot.Helper.lastTechExtents, new Color(1, 0, 0));


            //Turn our work in to processing
            //Debug.Log("TACtical_AI: Tech " + tank.name + " | steering " + turnVal + " | drive " + DriveVar);
            control3D.m_State.m_InputMovement = DriveVar.Clamp01Box();
            controlGet.SetValue(tank.control, control3D);
        }
        public static void DeterminePitchRoll(Tank tank, AIControllerAir pilot, Vector3 DestPosWorld, Vector3 LookPosWorld, AIECore.TankAIHelper thisInst, bool avoidCrash = false, bool PointAtTarget = false)
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

                // Rotors on some chopper designs were acting funky and cutting out due to pitch so I disabled pitching
                if (pilot.LowerEngines || avoidCrash)
                    fFlat.y = 0;
                else if (thisInst.IsMovingFromDest)
                    fFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z / (pitchDampening / pilot.SlowestPropLerpSpeed)) + 0.15f, -0.25f, 0.25f);
                else if (thisInst.IsMovingToDest)
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
            if (thisInst.DriveDir == EDriveFacing.Perpendicular)
            {   // orbit while firing
                if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x >= 0)
                    rFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x / (pitchDampening / pilot.SlowestPropLerpSpeed)) - 0.15f, -0.25f, 0.25f);
                else
                    rFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x / (pitchDampening / pilot.SlowestPropLerpSpeed)) + 0.15f, -0.25f, 0.25f);
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
            float final = ((targetHeight.y - tank.boundsCentreWorldNoCheck.y) / (pilot.PropLerpValue / 2)) + AIGlobals.ChopperOperatingExtraHeight;
            //Debug.Log("TACtical_AI: " + tank.name + " thrust = " + final + " | velocity " + tank.rbody.velocity);
            if (ForceUp)
            {
                final = 1;
            }
            else if (final <= -0.4f)
            {
                //Debug.Log("TACtical_AI: " + tank.name + " Too high!! Cutting engines!!");
                pilot.LowerEngines = true;
                final = -0.1f;
                if (tank.rbody.velocity.y < 0)
                {
                    final = Mathf.Pow(tank.rbody.velocity.y, 2) /12;
                }
            }
            else if (tank.rbody.velocity.y < 0 && final > -0.4f && final < 0)  // try ease fall
            {
                //Debug.Log("TACtical_AI: " + tank.name + " dampening fall");
                final = Mathf.Pow(tank.rbody.velocity.y, 2) / 10;
            }
            if (tank.rbody.velocity.y > 4 && final > 0 && final < 1.4f)     // try ease flight
            {
                //Debug.Log("TACtical_AI: " + tank.name + " dampening up speed");
                final = 0;
            }
            if (final < -0.1f)
                final = -0.1f;
            //Debug.Log("TACtical_AI: " + tank.name + " thrustFinal = " + final);

            if (final > 1.25f && pilot.BoostBias.y > 0.6f)
                thisInst.BOOST = true;
            else
                thisInst.BOOST = thisInst.lastOperatorRange > AIGlobals.GroundAttackStagingDist / 3;

            return Mathf.Clamp(final, -0.1f, 1);
        }
        public static void UpdateThrottleCopter(AIControllerAir pilot, AIECore.TankAIHelper thisInst, TankControl control)
        {
            control.BoostControlProps = false;
            if (pilot.NoProps)
            {
                if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck, pilot.Helper.GroundOffsetHeight) && !pilot.Tank.beam.IsActive)
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
                    control.BoostControlJets = thisInst.BOOST;
            }
            pilot.CurrentThrottle = Mathf.Clamp(pilot.MainThrottle, 0, 1);
        }
    }
}
