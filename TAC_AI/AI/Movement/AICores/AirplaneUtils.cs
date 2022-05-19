using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI.AI.Movement.AICores
{
    internal class AirplaneUtils
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void UTurn(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AIControllerAir pilot)
        {
            //Debug.Log("TACtical_AI: Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
            pilot.MainThrottle = 1;
            pilot.UpdateThrottle(thisInst, thisControl);
            if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < AIGlobals.AirStallSpeed)
            {   //ABORT!!!
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn with velocity " + tank.rootBlockTrans.InverseTransformVector(pilot.Tank.rbody.velocity).z);
                pilot.PerformUTurn = -1;
                pilot.ErrorsInUTurn++;
                if (pilot.ErrorsInUTurn > 3)
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " has failed to U-Turn/Immelmann over 3 times and will no longer try");
            }
            else if (Vector3.Dot(Vector3.down, tank.rbody.velocity.normalized) > 0.6f)
            {   //ABORT!!!
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                pilot.PerformUTurn = -1;
                pilot.ErrorsInUTurn++;
                if (pilot.ErrorsInUTurn > 3)
                    DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " has failed to U-Turn/Immelmann over 3 times and will no longer try");
            }
            if (pilot.PerformUTurn == 1)
            {   // Accelerate
                //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " Executing U-Turn...");
               // DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck), "TACtical_AI: ASSERT - " + tank.name + " is UTurning above max allowed altitude");
                AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + tank.rootBlockTrans.forward * 100);
                if (pilot.CurrentThrottle > 0.95)
                    pilot.PerformUTurn = 2;
            }
            else if (pilot.PerformUTurn == 2)
            {   // Pitch Up
                //DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck), "TACtical_AI: ASSERT - " + tank.name + " is UTurning above max allowed altitude");
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
        public static Vector3 DetermineRoll(Tank tank, AIControllerAir pilot, Vector3 Navi3DDirect, bool forceUp)
        {
            //Vector3 turnValUp = Quaternion.LookRotation(tank.rootBlockTrans.forward, tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;


            if (forceUp)
                return Vector3.up;
            Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(Navi3DDirect);
            float fwdHeading = Heading.ToVector2XZ().normalized.y;
            bool PitchNotNeeded = Navi3DDirect.y > -0.6f && Navi3DDirect.y < 0.6f;

            Vector3 direct = Vector3.up;
            if (pilot.PerformUTurn == 3)
            {
                //Debug.Log("TACtical_AI: Tech " + tank.name + "  Stage 3 Immelmann");
                direct = Vector3.down;
            }
            else if (tank.rootBlockTrans.up.y < -0.4f)
            {   // handle invalid request to go upside down
                //Debug.Log("TACtical_AI: Tech " + tank.name + "  IS UPSIDE DOWN AND IS TRYING TO GET UPRIGHT");
                // Stay upright
            }
            else if ((pilot.PerformUTurn > 0 && !pilot.LargeAircraft && !pilot.BankOnly) || pilot.ForcePitchUp)
            {
                // Stay upright
            }
            else if (fwdHeading < -0.5f && PitchNotNeeded && pilot.PerformUTurn == 0 && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
            {
                //DebugTAC_AI.Log("directed is " + Navi3DDirect);
                if (pilot.ErrorsInUTurn > 3)    // Aircraft failed Immelmann over 3 times in a row
                    pilot.PerformUTurn = -1;
                else if (pilot.LargeAircraft || pilot.BankOnly)   // Large aircraft cannot do the Immelmann
                    pilot.PerformUTurn = -1;
                else                            // Perform the Immelmann turn, or better known as the "U-Turn"
                    pilot.PerformUTurn = 1;
            }
            else if (pilot.LargeAircraft || pilot.BankOnly)
            {
                // Because we likely yaw slower, we should bank as much as possible
                if (PitchNotNeeded && fwdHeading < 0.925f - (0.2f / pilot.RollStrength))
                {
                    if (Heading.x > 0f)
                    { // We roll to aim at target
                      //Debug.Log("TACtical_AI: (HVY) Tech " + tank.name + "  Roll turn Right");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, false);
                        rFlat.y = -pilot.RollStrength / 2;
                        direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                    }
                    else if (Heading.x < 0f)
                    { // We roll to aim at target
                      //Debug.Log("TACtical_AI: (HVY) Tech " + tank.name + "  Roll turn Left");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, false);
                        rFlat.y = pilot.RollStrength / 2;
                        direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                    }
                }
            }
            else
            {
                if (PitchNotNeeded && fwdHeading < 0.85f - (0.2f / pilot.RollStrength))
                {
                    if (Heading.x > 0f)
                    { // We roll to aim at target
                      //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Right");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, true);
                        rFlat.y = -pilot.RollStrength;
                        direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                    }
                    else if (Heading.x < 0f)
                    { // We roll to aim at target
                      //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Left");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, true);
                        rFlat.y = pilot.RollStrength;
                        direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                    }
                }
            }
            //Debug.Log("TACtical_AI: upwards direction " + tank.name + "  is " + direct.y);

            return direct; // IS IN WORLD SPACE
        }
        public static void AngleTowards(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AIControllerAir pilot, Vector3 position)
        {
            //AI Steering Rotational
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(thisControl);

            Transform root = tank.rootBlockTrans;

            bool EmergencyUp = false;
            if (pilot.LargeAircraft)
            {
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock, AIGlobals.AircraftGroundOffset))
                {
                    EmergencyUp = true;
                }
            }
            else if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock, thisInst.lastTechExtents + 2))
            {
                EmergencyUp = true;
            }
            Vector3 insureUpright = (position - tank.boundsCentreWorldNoCheck).normalized;
            if (root.forward.y < -AIGlobals.AircraftDangerDive || EmergencyUp)
            {   // CRASH LIKELY, PULL UP! 
                //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is trying to break from a crash-dive " + root.forward.y);
                insureUpright = new Vector3(0, 1.45f, 0) + root.forward.SetY(0).normalized;
            }
            else if (insureUpright.y < -AIGlobals.AircraftMaxDive)
            {   
                insureUpright = insureUpright.SetY(0).normalized;
                insureUpright.y = -AIGlobals.AircraftMaxDive;
            }
            else if (Vector3.Dot(insureUpright, root.forward) < 0 && !pilot.ForcePitchUp)
            {   // Level when turning far
                insureUpright = insureUpright.SetY(0).normalized;
                insureUpright.y = 0.1f;
            }
            thisInst.Navi3DDirect = insureUpright.normalized;
          
            thisInst.Navi3DUp = DetermineRoll(tank, pilot, thisInst.Navi3DDirect, EmergencyUp);

            // We must make the controls local to the cab to insure predictable performance
            Vector3 ForwardsLocal = root.InverseTransformDirection(thisInst.Navi3DDirect);
            Vector3 turnVal = Quaternion.LookRotation(ForwardsLocal, Vector3.up).eulerAngles;
            Vector3 UpLocal = root.InverseTransformDirection(thisInst.Navi3DUp);
            Vector3 turnValUp = Quaternion.LookRotation(Vector3.forward, UpLocal).eulerAngles;
            //Vector3 forwardFlat = tank.rootBlockTrans.forward;
            //forwardFlat.y = 0;

            //Debug.Log("TACtical_AI: Tech " + tank.name + " steering RAW" + turnVal);

            //Convert turnVal to runnable format
            // PITCH
            if (turnVal.x > 180)
                turnVal.x = Mathf.Clamp(-((turnVal.x - 360) / pilot.FlyingChillFactor.x), -1, 1);
            else
                turnVal.x = Mathf.Clamp(-(turnVal.x / pilot.FlyingChillFactor.x), -1, 1);
            // YAW
            if (turnVal.y > 180)
                turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / pilot.FlyingChillFactor.y), -1, 1);
            else
                turnVal.y = Mathf.Clamp(-(turnVal.y / pilot.FlyingChillFactor.y), -1, 1);
            // ROLL
            if (turnValUp.z > 180)
                turnValUp.z = Mathf.Clamp(-((turnValUp.z - 360) / pilot.FlyingChillFactor.z), -1, 1);
            else
                turnValUp.z = Mathf.Clamp(-(turnValUp.z / pilot.FlyingChillFactor.z), -1, 1);

            // Control oversteer since there's no proper control limiter for overyaw
            if (pilot.BankOnly)
            {
                turnVal.y = Mathf.Clamp(turnVal.y, -AIGlobals.AirMaxYawBankOnly, AIGlobals.AirMaxYawBankOnly);
            }
            else
            {
                turnVal.y = Mathf.Clamp(turnVal.y, -AIGlobals.AirMaxYaw, AIGlobals.AirMaxYaw);
            }

            //Stop Wobble
            if (Mathf.Abs(turnVal.x) < 0.01f)
                turnVal.x = 0;
            if (Mathf.Abs(turnVal.y) < 0.01f)
                turnVal.y = 0;
            if (Mathf.Abs(turnValUp.z) < 0.01f)
                turnValUp.z = 0;
            //thisInst.Navi3DDirect = (position - tank.boundsCentreWorldNoCheck).normalized;

            if (tank.rootBlockTrans.up.y < 0)
            {   // upside down due to a unfindable oversight in code - just override the bloody thing when it happens
                //Debug.Log("TACtical_AI: Tech " + tank.name + "  IS UPSIDE DOWN AND IS TRYING TO GET UPRIGHT");

                //Debug.Log("TACtical_AI: Tech " + tank.name + " steering" + turnVal);
                //turnVal.z = -Mathf.Clamp(turnVal.z * 10, -1, 1);
            }

            //Turn our work in to process
            turnVal.z = turnValUp.z;
            control3D.m_State.m_InputRotation = turnVal.Clamp01Box();

            // DRIVE
            Vector3 DriveVar = Vector3.forward * pilot.CurrentThrottle;

            //Turn our work in to processing
            //Debug.Log("TACtical_AI: Tech " + tank.name + " steering" + turnVal);
            control3D.m_State.m_InputMovement = DriveVar.Clamp01Box();
            //if (pilot.SlowestPropLerpSpeed < 0.1f && pilot.PropBias.z > 0.75f && pilot.CurrentThrottle > 0.75f)
            //    control3D.m_State.m_BoostProps = true;
            //else
            //    control3D.m_State.m_BoostProps = false;

            // Blue is the target destination, Red is up  

            // DEBUG FOR DRIVE ERRORS

            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, position - tank.boundsCentreWorldNoCheck, new Color(0, 1, 1));
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, thisInst.Navi3DDirect * pilot.Helper.lastTechExtents * 3, new Color(0, 0, 1));
            Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, thisInst.Navi3DUp * pilot.Helper.lastTechExtents * 3, new Color(1, 0, 0));


            controlGet.SetValue(tank.control, control3D);
            return;
        }
        public static void AdviseThrottle(AIControllerAir pilot, AIECore.TankAIHelper thisInst, Tank tank, Vector3 target)
        {
            if (pilot.AdvisedThrottle == -1)
            {
                if (tank.rbody.IsNotNull())
                {
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z > AIGlobals.AirStallSpeed)
                    {
                        float ExtAvoid = thisInst.MinimumRad;
                        if (thisInst.lastPlayer.IsNotNull())
                            ExtAvoid = thisInst.lastPlayer.GetCheapBounds();
                        float Extremes = ExtAvoid + thisInst.lastTechExtents + AIGlobals.ExtraSpace;
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
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z > AIGlobals.AirStallSpeed)
                    {
                        float throttleToSet = 1;
                        float foreTarg = tank.rootBlockTrans.InverseTransformPoint(target.tank.boundsCentreWorldNoCheck).z;
                        float Extremes = target.GetCheapBounds() + thisInst.lastTechExtents + 5;
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

        public static void PreventCollisionWithGround(AIControllerAir pilot, float groundOffset, bool unresponsiveAir)
        {
            Vector3 deltaAim = pilot.deltaMovementClock + pilot.Helper.tank.boundsCentreWorldNoCheck;
            float groundOffsetF = groundOffset; //pilot.AerofoilSluggishness
            if (unresponsiveAir)
            {
                if (!AIEPathing.AboveHeightFromGround(deltaAim, groundOffsetF + pilot.Helper.lastTechExtents))
                {
                    DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(deltaAim), "PreventCollisionWithGround called while height is too high");
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " -  deltaMovementClock = " + pilot.deltaMovementClock + " | slugishness = " + pilot.AerofoilSluggishness + " | deltaAim y " + deltaAim.y + " | vs " + groundOffsetF);
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " - GOING UP (HVY)");
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest.y = pilot.Helper.tank.boundsCentreWorldNoCheck.y;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - pilot.Helper.tank.boundsCentreWorldNoCheck).ToVector2XZ().magnitude * 4;
                }
            }
            else
            {
                if (!AIEPathing.AboveHeightFromGround(deltaAim, groundOffsetF))
                {
                    DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(deltaAim), "PreventCollisionWithGround called while height is too high");
                    pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " -  deltaMovementClock = " + pilot.deltaMovementClock.y + " | slugishness = " + pilot.AerofoilSluggishness + " | deltaAim y " + deltaAim.y + " | vs " + groundOffsetF + 
                    //    " | tech: " + pilot.Helper.tank.trans.position);
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " - GOING UP");
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest.y = pilot.Helper.tank.boundsCentreWorldNoCheck.y;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - pilot.Helper.tank.boundsCentreWorldNoCheck).ToVector2XZ().magnitude * 4;
                }
            }
        }


        public static Vector3 GetExactRightAlignedWorld(Tank tank, bool useLegacy)
        {
            if (useLegacy)
            {
                //return GetExactRightAlignedWorldLegacy(tank);
            }

            Vector3 right;
            if (tank.rootBlockTrans.forward.y <= -0.8f && tank.rootBlockTrans.forward.y >= 0.8f)
            {
                right = Vector3.Cross(Vector3.up, tank.rootBlockTrans.forward.SetY(0).normalized).SetY(0).normalized;
                DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 7, right * 24, new Color(1,1, 0, 1));
                return right;
            }
            else
            {
                return GetExactRightAlignedWorldLegacy(tank);
                /*
                if (tank.rootBlockTrans.up.y > 0)
                {
                    right = -Vector3.Cross(Vector3.up, tank.rootBlockTrans.forward.SetY(0).normalized).SetY(0).normalized;
                    DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 7, right * 24, new Color(1, 1, 0, 1));
                    return right;
                }
                else
                {
                    right = Vector3.Cross(Vector3.up, tank.rootBlockTrans.forward.SetY(0).normalized).SetY(0).normalized;
                    DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 7, right * 24, new Color(1, 1, 0, 1));
                    return right;
                }*/
            }

        }

        public static Vector3 GetExactRightAlignedWorldLegacy(Tank tank)
        {
            Vector3 rFlat;
            if (tank.rootBlockTrans.up.y > 0)
                rFlat = tank.rootBlockTrans.right;
            else
                rFlat = -tank.rootBlockTrans.right;
            rFlat.y = 0;
            rFlat.Normalize();
            DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 7, rFlat * 24, new Color(1, 1, 0, 1));
            return rFlat;
        }
    }
}
