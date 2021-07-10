using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;

namespace TAC_AI.AI.MovementAI
{
    internal class AircraftUtils
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void UTurn(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AIControllerAir pilot)
        {
            //Debug.Log("TACtical_AI: Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
            pilot.MainThrottle = 1;
            pilot.UpdateThrottle(thisInst, thisControl);
            if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < AIControllerAir.Stallspeed)
            {   //ABORT!!!
                Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn with velocity " + tank.rootBlockTrans.InverseTransformVector(pilot.Tank.rbody.velocity).z);
                pilot.PerformUTurn = -1;
                pilot.ErrorsInUTurn++;
                if (pilot.ErrorsInUTurn > 3)
                    Debug.Log("TACtical_AI: Tech " + tank.name + " has failed to U-Turn/Immelmann over 3 times and will no longer try");
            }
            else if (Vector3.Dot(Vector3.down, tank.rbody.velocity.normalized) > 0.3f)
            {   //ABORT!!!
                Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                pilot.PerformUTurn = -1;
                pilot.ErrorsInUTurn++;
                if (pilot.ErrorsInUTurn > 3)
                    Debug.Log("TACtical_AI: Tech " + tank.name + " has failed to U-Turn/Immelmann over 3 times and will no longer try");
            }
            if (pilot.PerformUTurn == 1)
            {   // Accelerate
                AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + tank.rootBlockTrans.forward * 100);
                if (pilot.CurrentThrottle > 0.95)
                    pilot.PerformUTurn = 2;
            }
            else if (pilot.PerformUTurn == 2)
            {   // Pitch Up
                AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.75f)
                    pilot.PerformUTurn = 3;
            }
            else if (pilot.PerformUTurn == 3)
            {   // Aim back at target
                AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                if (Vector3.Dot((pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.2f)
                {
                    pilot.ErrorsInUTurn = 0;
                    pilot.PerformUTurn = 0;
                    if (pilot.PerformDiveAttack == 1)
                        pilot.PerformDiveAttack = 2;
                }
            }
        }
        public static Vector3 DetermineRoll(Tank tank, AIControllerAir pilot, Vector3 Navi3DDirect)
        {
            //Vector3 turnValUp = Quaternion.LookRotation(tank.rootBlockTrans.forward, tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;

            if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                return Vector3.up;
            Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(Navi3DDirect);

            Vector3 direct = Vector3.up;
            if (pilot.PerformUTurn == 3)
            {
                direct = Vector3.down;
            }
            else if ((pilot.PerformUTurn > 0 && !pilot.LargeAircraft) || pilot.ForcePitchUp)
            {
                // Stay upright
            }
            else if (Heading.z < -0.5 && pilot.PerformUTurn == 0 && AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
            {
                if (pilot.ErrorsInUTurn > 3)    // Aircraft failed Immelmann over 3 times in a row
                    pilot.PerformUTurn = -1;
                else if (pilot.LargeAircraft)   // Large aircraft cannot do the Immelmann
                    pilot.PerformUTurn = -1;
                else                            // Perform the Immelmann turn, or better known as the "U-Turn"
                    pilot.PerformUTurn = 1;
            }
            else if (pilot.LargeAircraft)
            {
                // Because we likely yaw slower, we should bank as much as possible
                if (Heading.x > 0f && Heading.z < 0.925f - (0.2f / pilot.RollStrength))
                { // We roll to aim at target
                  //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Right");
                    Vector3 rFlat;
                    if (tank.rootBlockTrans.up.y > 0)
                        rFlat = tank.rootBlockTrans.right;
                    else
                        rFlat = -tank.rootBlockTrans.right;
                    rFlat.y = -pilot.RollStrength;
                    direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                }
                else if (Heading.x < 0f && Heading.z < 0.925f - (0.2f / pilot.RollStrength))
                { // We roll to aim at target
                  //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Left");
                    Vector3 rFlat;
                    if (tank.rootBlockTrans.up.y > 0)
                        rFlat = tank.rootBlockTrans.right;
                    else
                        rFlat = -tank.rootBlockTrans.right;
                    rFlat.y = pilot.RollStrength;
                    direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                }
            }
            else
            {
                if (Heading.x > 0f && Heading.z < 0.85f - (0.2f / pilot.RollStrength))
                { // We roll to aim at target
                  //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Right");
                    Vector3 rFlat;
                    if (tank.rootBlockTrans.up.y > 0)
                        rFlat = tank.rootBlockTrans.right;
                    else
                        rFlat = -tank.rootBlockTrans.right;
                    rFlat.y = -pilot.RollStrength;
                    direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                }
                else if (Heading.x < 0f && Heading.z < 0.85f - (0.2f / pilot.RollStrength))
                { // We roll to aim at target
                  //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Left");
                    Vector3 rFlat;
                    if (tank.rootBlockTrans.up.y > 0)
                        rFlat = tank.rootBlockTrans.right;
                    else
                        rFlat = -tank.rootBlockTrans.right;
                    rFlat.y = pilot.RollStrength;
                    direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                }
            }
            return direct;
        }
        public static void AngleTowards(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AIControllerAir pilot, Vector3 position)
        {
            //AI Steering Rotational
            TankControl.ControlState control3D = (TankControl.ControlState) AircraftUtils.controlGet.GetValue(thisControl);

            thisInst.Navi3DDirect = (position - tank.boundsCentreWorldNoCheck).normalized;

            thisInst.Navi3DUp = DetermineRoll(tank, pilot, thisInst.Navi3DDirect);

            Vector3 turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;

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
            if (Mathf.Abs(turnVal.x) < 0.01f)
                turnVal.x = 0;
            if (Mathf.Abs(turnVal.y) < 0.01f)
                turnVal.y = 0;
            if (Mathf.Abs(turnVal.z) < 0.01f)
                turnVal.z = 0;


            //Turn our work in to process
            control3D.m_State.m_InputRotation = turnVal;

            // DRIVE
            Vector3 DriveVar = Vector3.forward * pilot.CurrentThrottle;

            //Turn our work in to processing
            //Debug.Log("TACtical_AI: Tech " + tank.name + " steering" + turnVal);
            control3D.m_State.m_InputMovement = DriveVar;
            if (pilot.SlowestPropLerpSpeed < 0.1f && pilot.PropBias.z > 0.75f && pilot.CurrentThrottle > 0.75f)
                thisControl.BoostControlProps = true;
            else
                thisControl.BoostControlProps = false;


            controlGet.SetValue(tank.control, control3D);
            return;
        }
        public static void AdviseThrottle(AIControllerAir pilot, AIECore.TankAIHelper thisInst, Tank tank, Vector3 target)
        {
            if (pilot.AdvisedThrottle == -1)
            {
                if (tank.rbody.IsNotNull())
                {
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z > AIControllerAir.Stallspeed)
                    {
                        float ExtAvoid = 32;
                        if (thisInst.lastPlayer.IsNotNull())
                            ExtAvoid = AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.size);
                        float Extremes = ExtAvoid + AIECore.Extremes(tank.blockBounds.size) + 5;
                        float throttleToSet = 1;
                        float foreTarg = tank.rootBlockTrans.InverseTransformPoint(target).z;

                        if (foreTarg > 0)
                            throttleToSet = (foreTarg - Extremes) / pilot.PropLerpValue;
                        pilot.AdvisedThrottle = Mathf.Clamp(throttleToSet, 0, 1);

                        if (!pilot.LowerEngines)
                        {   // Save fuel for chasing the enemy
                            if (pilot.NoProps)
                            {
                                if (!pilot.ForcePitchUp && foreTarg > Extremes && tank.rbody.velocity.y > -10 && Vector3.Dot((target - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                    thisInst.BOOST = true;
                                else
                                    thisInst.BOOST = false;
                            }
                            else
                            {
                                if (!pilot.ForcePitchUp && throttleToSet > 1.25f && tank.rbody.velocity.y > -10 && Vector3.Dot((target - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                    thisInst.BOOST = true;
                                else
                                    thisInst.BOOST = false;
                            }
                        }
                        else
                            thisInst.BOOST = false;
                        return;
                    }
                }
                pilot.AdvisedThrottle = 1;
            }
        }
        public static void AdviseThrottleTarget(AIControllerAir pilot, AIECore.TankAIHelper thisInst, Tank tank, Visible target)
        {
            if (pilot.AdvisedThrottle == -1)
            {
                if (tank.rbody.IsNotNull())
                {
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z > AIControllerAir.Stallspeed)
                    {
                        float throttleToSet = 1;
                        float foreTarg = tank.rootBlockTrans.InverseTransformPoint(target.tank.boundsCentreWorldNoCheck).z;
                        float Extremes = AIECore.Extremes(target.tank.blockBounds.size) + AIECore.Extremes(tank.blockBounds.size) + 5;
                        if (foreTarg > 0)
                            throttleToSet = (foreTarg - Extremes) / pilot.PropLerpValue;
                        //Debug.Log("TACtical_AI: throttle " + throttleToSet + " | position offset enemy " + foreTarg);
                        pilot.AdvisedThrottle = Mathf.Clamp(throttleToSet, 0, 1);

                        if (pilot.NoProps)
                        {
                            if (!pilot.ForcePitchUp && foreTarg > Extremes && tank.rbody.velocity.y > -10 && Vector3.Dot((target.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                thisInst.BOOST = true;
                            else
                                thisInst.BOOST = false;
                        }
                        else
                        {
                            if (!pilot.ForcePitchUp && throttleToSet > 1.25f && tank.rbody.velocity.y > -10 && Vector3.Dot((target.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                thisInst.BOOST = true;
                            else
                                thisInst.BOOST = false;
                        }
                        return;
                    }
                    //else
                    //Debug.Log("TACtical_AI: not fast enough, velocity" + tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z + " vs " + AIControllerAir.Stallspeed);
                }
                pilot.AdvisedThrottle = 1;
            }
            //Debug.Log("TACtical_AI: throttle is already " + pilot.AdvisedThrottle);
        }
        public static Vector3 ForeAiming(Visible target)
        {
            if (target.tank.rbody.IsNull())
                return target.tank.boundsCentreWorldNoCheck;
            else
                return target.tank.rbody.velocity + target.tank.boundsCentreWorldNoCheck;
        }
        public static Vector3 TryGetVelocityOffset(Tank tank, AIControllerAir pilot)
        {
            if (tank.rbody.IsNotNull())
                return tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness);
            return tank.boundsCentreWorldNoCheck;
        }
    }
}
