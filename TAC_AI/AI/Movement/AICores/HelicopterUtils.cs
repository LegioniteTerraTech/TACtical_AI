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

        public static void AngleTowardsUp(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AIControllerAir pilot, Vector3 position, bool ForceAccend = false)
        {
            //AI Steering Rotational
            TankControl.ControlState control3D = (TankControl.ControlState) HelicopterUtils.controlGet.GetValue(thisControl);
            Vector3 turnVal;
            DeterminePitchRoll(tank, pilot, position - tank.rbody.velocity, thisInst);
            Vector3 forwardFlat = thisInst.Navi3DDirect;
            forwardFlat.y = 0;
            if (ForceAccend)
            {
                turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(forwardFlat), tank.rootBlockTrans.InverseTransformDirection(Vector3.one)).eulerAngles;
            }
            else
            {
                turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
            }

            //Convert turnVal to runnable format
            if (turnVal.x > 180)
                turnVal.x = Mathf.Clamp(-((turnVal.x - 360) / pilot.FlyingChillFactor.x), -1, 1);
            else
                turnVal.x = Mathf.Clamp(-(turnVal.x / pilot.FlyingChillFactor.x), -1, 1);

            if (turnVal.y > 180)
                turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / pilot.FlyingChillFactor.y), -1, 1);
            else
                turnVal.y = Mathf.Clamp(-(turnVal.y / pilot.FlyingChillFactor.y), -1, 1);

            if (turnVal.z > 180)
                turnVal.z = Mathf.Clamp(-((turnVal.z - 360) / pilot.FlyingChillFactor.z), -1, 1);
            else
                turnVal.z = Mathf.Clamp(-(turnVal.z / pilot.FlyingChillFactor.z), -1, 1);

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
            control3D.m_State.m_InputRotation = turnVal;

            // DRIVE
            float xOffset = 0;
            if (thisInst.DriveDir == EDriveType.Perpendicular && thisInst.lastEnemy != null)
            {
                if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).x > 0)
                    xOffset = 0.4f;
                else
                    xOffset = -0.4f;
            }
            Vector3 DriveVar = tank.rootBlockTrans.InverseTransformVector(-tank.rbody.velocity) / pilot.PropLerpValue;
            DriveVar.x = Mathf.Clamp(DriveVar.x + xOffset, -1, 1);
            DriveVar.z = Mathf.Clamp(DriveVar.z, -1, 1);
            DriveVar.y = 0;
            if (thisInst.PivotOnly) 
            {
                // Do nothing and let the inertia dampener kick in
            }
            else if (thisInst.MoveFromObjective)
                DriveVar.z = -1;
            else if (thisInst.ProceedToObjective)
                DriveVar.z = 1;
            //DriveVar = DriveVar.normalized;
            DriveVar.y = pilot.CurrentThrottle;

            //Turn our work in to processing
            //Debug.Log("TACtical_AI: Tech " + tank.name + " | steering " + turnVal + " | drive " + DriveVar);
            control3D.m_State.m_InputMovement = DriveVar.Clamp01Box();
            controlGet.SetValue(tank.control, control3D);
            return;
        }
        public static void DeterminePitchRoll(Tank tank, AIControllerAir pilot, Vector3 DestinationVector, AIECore.TankAIHelper thisInst, bool PointAtTarget = false)
        {
            Vector3 Heading = (DestinationVector - tank.boundsCentreWorldNoCheck).normalized;
            Vector3 fFlat = Heading;
            fFlat.y = 0;

            Vector3 directUp;
            Vector3 rFlat;

            // X-axis turning
            if (tank.rootBlockTrans.up.y > 0)
                rFlat = tank.rootBlockTrans.right;
            else
                rFlat = -tank.rootBlockTrans.right;
            if (thisInst.DriveDir == EDriveType.Perpendicular)
            {   // orbit while firing
                if (tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).x >= 0)
                    rFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).x / (10 / pilot.SlowestPropLerpSpeed)) - 0.5f, -0.75f, 0.75f);
                else
                    rFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).x / (10 / pilot.SlowestPropLerpSpeed)) + 0.5f, -0.75f, 0.75f);
            }
            else
                rFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).x / (10 / pilot.SlowestPropLerpSpeed), -0.75f, 0.75f);
            directUp = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;


            // Other axis turning
            if (PointAtTarget)
                fFlat.y = Heading.y;
            else
            {
                fFlat.y = 0;
                /*
                // Rotors on some chopper designs were acting funky and cutting out due to pitch so I disabled pitching
                if (pilot.LowerEngines)
                    fFlat.y = 0;
                else if (thisInst.MoveFromObjective || thisInst.AdviseAway)
                    fFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).z / (10 / pilot.SlowestPropLerpSpeed)) + 0.1f, -0.35f, 0.35f);
                else if (thisInst.ProceedToObjective)
                    fFlat.y = Mathf.Clamp((tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).z / (10 / pilot.SlowestPropLerpSpeed)) - 0.1f, -0.35f, 0.35f);
                else
                    fFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).z / (10 / pilot.SlowestPropLerpSpeed), -0.35f, 0.35f);
                */
            }
            // Because tilting forwards too hard causes the chopper to stall on some builds
            //fFlat.y = fFlat.y - (fFlat.y * pilot.CurrentThrottle);

            thisInst.Navi3DDirect = fFlat.normalized;
            thisInst.Navi3DUp = directUp;
        }
        public static float ModerateUpwardsThrust(Tank tank, AIECore.TankAIHelper thisInst, AIControllerAir pilot, Vector3 targetHeight, bool ForceUp = false)
        {
            pilot.LowerEngines = false;
            float final = ((targetHeight.y - tank.boundsCentreWorldNoCheck.y) / (pilot.PropLerpValue / 2)) + 0.5f;
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
                    final = Mathf.Abs(-Mathf.Pow(tank.rbody.velocity.y, 2)) / 10;
                }
            }
            else if (tank.rbody.velocity.y < 0 && final > -0.4f && final < 0)  // try ease fall
            {
                //Debug.Log("TACtical_AI: " + tank.name + " dampening fall");
                final = Mathf.Abs(-Mathf.Pow(tank.rbody.velocity.y, 2)) / 10;
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
                thisInst.BOOST = thisInst.lastRange > AIControllerAir.GroundAttackStagingDist / 3;

            return Mathf.Clamp(final, -0.1f, 1);
        }
        public static void UpdateThrottleCopter(AIControllerAir pilot, AIECore.TankAIHelper thisInst, TankControl control)
        {
            control.BoostControlProps = false;
            if (pilot.NoProps)
            {
                if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.Tank.blockBounds.extents) * 2) && !pilot.Tank.beam.IsActive)
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
