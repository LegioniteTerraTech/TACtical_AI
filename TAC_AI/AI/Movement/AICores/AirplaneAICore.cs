using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;


namespace TAC_AI.AI.Movement.AICores
{
    public class AirplaneAICore : IMovementAICore
    {
        internal AIControllerAir pilot;
        internal AIECore.TankAIHelper Helper => pilot.Helper;
        private float groundOffset => AIGlobals.AircraftGroundOffset + Helper.lastTechExtents;

        public virtual void Initiate(Tank tank, IMovementAIController pilot)
        {
            this.pilot = (AIControllerAir) pilot;
            this.pilot.FlyStyle = AIControllerAir.FlightType.Aircraft;
            Helper.GroundOffsetHeight = Helper.lastTechExtents + AIGlobals.AircraftGroundOffset;
        }

        /// <summary>
        /// Drives the Tech to the desired location (AIControllerAir.AirborneDest) in world space
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <returns></returns>
        public virtual bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (pilot.Grounded) //|| thisInst.ForceSetDrive)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, thisInst.lastTechExtents * 2))
                {
                    DriveMaintainerEmergLand(thisControl, thisInst, tank);
                    return false;
                }
                //WIP - Try fighting the controls to land safely

                return true;
            }
            //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " plane drive was called");

            if (tank.beam.IsActive && thisInst.recentSpeed < 8)
            {   // BEAMING
                pilot.MainThrottle = 0;
                pilot.PerformUTurn = 0;
                pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat = flat.normalized;
                flat.y = 0.5f;
                //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is in build beam");
                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 1000));
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                pilot.MainThrottle = 1;
                pilot.PerformUTurn = 0;
                pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat = flat.normalized;
                flat.y = 1f;
                //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " is grounded: " + tank.grounded + " | is ForcePitchUp: " + pilot.ForcePitchUp);
                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 1000));
            }
            else
            {
                if (pilot.TargetGrounded && (thisInst.lastEnemy || thisInst.theResource || thisInst.theBase)) // Divebombing mode
                {  // We previously disabled the ground offset terrain avoider and aim directly at the enemy
                    Vector3 deltaAim = pilot.deltaMovementClock;
                    Vector3 posOffset = thisInst.lastDestination - (tank.boundsCentreWorldNoCheck + deltaAim);
                    float dist = posOffset.magnitude;
                    float dist2D = posOffset.SetY(0).magnitude;
                    Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(thisInst.lastDestination - tank.boundsCentreWorldNoCheck);
                    if (pilot.ForcePitchUp)
                        pilot.PerformDiveAttack = 0; // too low and we break off from the attack
                    if (dist < 32)
                    {   // target is in the air but grounded!?!?
                        pilot.PerformDiveAttack = 0; // abort

                        //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Aborting attack! Target too close!");
                        // AND PITCH UP NOW
                        pilot.MainThrottle = 1;
                        pilot.PerformUTurn = 0;
                        pilot.UpdateThrottle(thisInst, thisControl);
                        AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 500));
                    }
                    else if (pilot.PerformDiveAttack == 1)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Aiming at target!");
                        if (Heading.x > 0.3f && Heading.x < -0.3f && Heading.z > 0)
                            pilot.PerformDiveAttack = 2; 
                        if (pilot.PerformUTurn > 0)
                        {   //The Immelmann Turn
                            AirplaneUtils.UTurn(thisControl, thisInst, tank, pilot);
                            return true;
                        }
                        else if (pilot.PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(thisInst, thisControl);
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.ProcessedDest);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.ProcessedDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                            {
                                pilot.PerformUTurn = 0;
                                if (pilot.PerformDiveAttack == 1)
                                    pilot.PerformDiveAttack = 2;
                            }
                            return true;
                        }
                        else
                        {
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(thisInst, thisControl);
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.ProcessedDest);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (pilot.PerformDiveAttack == 2)
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  DIVEBOMBING!");
                        if (Helper.GetSpeed() < AIGlobals.AirStallSpeed + 16 || Heading.y > -0.25f)
                            pilot.AdvisedThrottle = 1;
                        else
                            pilot.AdvisedThrottle = 0;
                        if (Heading.z < 0)
                            pilot.PerformDiveAttack = 0; // Passed by target
                        if (pilot.PerformUTurn > 0)
                        {   //The Immelmann Turn
                            AirplaneUtils.UTurn(thisControl, thisInst, tank, pilot);
                            return true;
                        }
                        else if (pilot.PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(thisInst, thisControl);
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.ProcessedDest);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.ProcessedDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                            {
                                pilot.PerformUTurn = 0;
                                if (pilot.PerformDiveAttack == 1)
                                    pilot.PerformDiveAttack = 2;
                            }
                            return true;
                        }
                        else
                        {
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            pilot.UpdateThrottle(thisInst, thisControl);
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.ProcessedDest);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (dist2D > AIGlobals.GroundAttackStagingDist && Heading.z < 0)
                    {   // Launch teh attack run
                        //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Turning back to face target at dist " + dist);
                        pilot.PerformDiveAttack = 1;
                    }
                    else
                    {
                        pilot.PerformUTurn = 0; // hold off on the U-Turn
                        if (Heading.z < 0.35f)
                        {   // Moving away from target
                            //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Gaining distance for attack run");
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(thisInst, thisControl);
                            Vector3 AwayFlat = (tank.boundsCentreWorldNoCheck - pilot.ProcessedDest).normalized;
                            AwayFlat.y = 0;
                            AwayFlat = AwayFlat.normalized;
                            AwayFlat.y = 0.1f;
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (AwayFlat.normalized * 1000));
                        }
                        else
                        {   // Moving to target
                            //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + "  Closing in on target");
                            if (Helper.GetSpeed() < AIGlobals.AirStallSpeed + 16 || Heading.y > -0.25f)
                                pilot.AdvisedThrottle = 1;
                            else
                                pilot.AdvisedThrottle = 0;
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            pilot.UpdateThrottle(thisInst, thisControl);
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.ProcessedDest);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    return true;
                }

                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    AirplaneUtils.UTurn(thisControl, thisInst, tank, pilot);
                    return true;
                }
                else if (pilot.PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    pilot.UpdateThrottle(thisInst, thisControl);
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.ProcessedDest);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.ProcessedDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                    {
                        pilot.PerformUTurn = 0;
                        if (pilot.PerformDiveAttack == 1)
                            pilot.PerformDiveAttack = 2;
                    }
                    return true;
                }
                else
                {
                    pilot.MainThrottle = pilot.AdvisedThrottle;
                    pilot.UpdateThrottle(thisInst, thisControl);
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.ProcessedDest);
                }
            }

            return true;
        }

        /// <summary>
        /// A very limited version of the VehicleAICore DriveMaintainer for downed aircraft
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <returns></returns>
        public bool DriveMaintainerEmergLand(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            TankControl.ControlState control3D = (TankControl.ControlState)VehicleAICore.controlGet.GetValue(tank.control);

            control3D.m_State.m_InputRotation = Vector3.zero;
            control3D.m_State.m_InputMovement = Vector3.zero;
            VehicleAICore.controlGet.SetValue(tank.control, control3D);
            Vector3 destDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
            // DEBUG FOR DRIVE ERRORS
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));

            thisControl.DriveControl = 0f;
            if (thisInst.Steer)
            {
                if (thisInst.AdviseAway)
                {   //Move from target
                    if (thisInst.DriveDir == EDriveFacing.Backwards)//EDriveType.Backwards
                    {   // Face back TOWARDS target
                        if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                        thisControl.DriveControl = 1f;
                    }
                    else if (thisInst.DriveDir == EDriveFacing.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                        //DebugTAC_AI.Log("Orbiting away");
                        thisControl.DriveControl = 1f;
                    }
                    else
                    {   // Face front TOWARDS target
                        if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                        thisControl.DriveControl = -1f;
                    }
                }
                else if (thisInst.DriveDir == EDriveFacing.Perpendicular)
                {   //Drive to target driving sideways, but obey distance
                    //int range = (int)(destDirect).magnitude;
                    float range = thisInst.lastOperatorRange;
                    if (range < thisInst.MinimumRad + 2)
                    {
                        if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                        //DebugTAC_AI.Log("Orbiting out " + thisInst.MinimumRad + " | " + destDirect);
                    }
                    else if (range > thisInst.MinimumRad + 22)
                    {
                        if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                        //DebugTAC_AI.Log("Orbiting in " + thisInst.MinimumRad);
                    }
                    else  //ORBIT!
                    {
                        Vector3 aimDirect;
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.right) < 0)
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.down);
                        else
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.up);
                        if (VehicleUtils.Turner(thisControl, thisInst, aimDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, aimDirect, turnVal);
                        //DebugTAC_AI.Log("Orbiting hold " + thisInst.MinimumRad);
                    }
                    thisControl.DriveControl = 1f;
                }
                else
                {
                    if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                        thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);//Face the music
                                                                                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  driving to " + thisInst.lastDestination);
                    if (thisInst.MinimumRad > 0)
                    {
                        //if (thisInst.DriveDir == EDriveType.Perpendicular)
                        //    thisControl.DriveControl = 1f;
                        float range = thisInst.lastOperatorRange;
                        if (thisInst.DriveDir == EDriveFacing.Neutral)
                            thisControl.DriveControl = 0f;
                        else if (range < thisInst.MinimumRad - 1)
                        {
                            if (thisInst.DriveDir == EDriveFacing.Forwards)
                                thisControl.DriveControl = -1f;
                            else if (thisInst.DriveDir == EDriveFacing.Backwards)
                                thisControl.DriveControl = 1f;
                            else
                                thisControl.DriveControl = 0;

                        }
                        else if (range > thisInst.MinimumRad + 1)
                        {
                            if (thisInst.DriveDir == EDriveFacing.Forwards)
                                thisControl.DriveControl = 1f;
                            else if (thisInst.DriveDir == EDriveFacing.Backwards)
                                thisControl.DriveControl = -1f;
                            else
                                thisControl.DriveControl = 1f;
                        }
                    }
                }
            }
            else
                thisControl.DriveControl = 0;

            // Overrides to translational drive
            if (thisInst.DriveDir == EDriveFacing.Neutral)
            {   // become brakeless
                thisControl.DriveControl = 0.001f;
                return true;
            }

            // Operate normally
            if (thisInst.PivotOnly)
            {
                thisControl.DriveControl = 0;
            }
            else if (thisInst.Yield)
            {
                if (thisInst.DriveDir == EDriveFacing.Backwards)
                {
                    if (thisInst.recentSpeed > 10)
                        thisControl.DriveControl = 0.2f;
                    else
                        thisControl.DriveControl = -1f;
                }
                else
                {   // works with forwards
                    if (thisInst.recentSpeed > 10)
                        thisControl.DriveControl = -0.2f;
                    else
                        thisControl.DriveControl = 1f;
                }
            }
            else if (thisInst.ForceSetDrive)
            {
                thisControl.DriveControl = thisInst.DriveVar;
                // Downed Aircraft can't boost as their engines are damaged
                if (thisInst.BOOST || thisInst.FeatherBoost)
                    thisControl.DriveControl = 1;
            }
            else if (thisInst.BOOST || thisInst.FeatherBoost)
                thisControl.DriveControl = 1;
            return true;
        }

        /// <summary>
        /// Player automatic AI version (player following)
        /// Declares 3D points in WORLD space (AirborneDest) 
        /// </summary>
        /// <returns>Execution was successful</returns>
        public bool DriveDirector()
        {
            bool Combat;
            pilot.AdvisedThrottle = -1;
            Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;
            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.ProcessedDest = MultiTechUtils.HandleMultiTech(Helper, pilot.Tank);
                return true;
            }
            else if (Helper.DriveDest == EDriveDest.ToBase)
            {
                pilot.AdvisedThrottle = -1;
                pilot.LowerEngines = true;
                if (Helper.lastBasePos.IsNotNull())
                {
                    Helper.DriveDir = EDriveFacing.Forwards;
                    Helper.lastDestination = Helper.AvoidAssistPrecise(Helper.lastBasePos.position);
                }
                // Orbit last position
                if ((pilot.ProcessedDest - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.ProcessedDest += (-pilot.Tank.rootBlockTrans.right.SetY(0).normalized * 129);
                }
                else
                {
                    pilot.ProcessedDest = Helper.lastDestination;
                }
                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastDestination, pilot.AerofoilSluggishness + groundOffset);
            }
            else if (Helper.DriveDest == EDriveDest.ToMine)
            {
                pilot.AdvisedThrottle = -1;
                if (Helper.theResource.tank != null)
                {
                    pilot.LowerEngines = true;
                    if (Helper.PivotOnly)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = Helper.theResource.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            Helper.DriveDir = EDriveFacing.Forwards;
                            Helper.lastDestination = Helper.AvoidAssistPrecise(Helper.theResource.tank.boundsCentreWorldNoCheck);
                            Helper.MinimumRad = 2;
                        }
                        else
                        {
                            Helper.DriveDir = EDriveFacing.Forwards;
                            Helper.lastDestination = Helper.AvoidAssistPrecise(Helper.theResource.tank.boundsCentreWorldNoCheck);
                            Helper.MinimumRad = Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    pilot.LowerEngines = false;
                    if (Helper.PivotOnly)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = Helper.theResource.trans.position;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            Helper.DriveDir = EDriveFacing.Forwards;
                            Helper.lastDestination = Helper.AvoidAssistPrecise(Helper.theResource.trans.position);
                        }
                        else
                        {
                            Helper.DriveDir = EDriveFacing.Forwards;
                            Helper.lastDestination = Helper.AvoidAssistPrecise(Helper.theResource.centrePosition);
                        }
                    }
                }
                pilot.ProcessedDest = Helper.lastDestination;
                // Orbit last position
                if ((pilot.ProcessedDest - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.ProcessedDest += GetOrbitFlight();
                }
                else
                {
                    pilot.ProcessedDest = Helper.lastDestination;
                }
                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastDestination, pilot.AerofoilSluggishness + groundOffset);
            }
            else if (Helper.DediAI == AIType.Aegis)
            {
                Helper.theResource = AIEPathing.ClosestUnanchoredAlly(pilot.Tank.boundsCentreWorldNoCheck, out _, pilot.Tank).visible;
                Combat = TryAdjustForCombat(true);
                if (!Combat)
                {
                    if (Helper.DriveDest == EDriveDest.FromLastDestination && Helper.theResource.IsNotNull())
                    {
                        pilot.LowerEngines = false;
                        Helper.Steer = true;
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = Helper.theResource.transform.position;
                    }
                    else if (Helper.DriveDest == EDriveDest.ToLastDestination && Helper.theResource.IsNotNull())
                    {
                        pilot.LowerEngines = true;
                        Helper.Steer = true;
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = Helper.AvoidAssist(Helper.theResource.tank.transform.position);
                    }
                    else
                    {
                        //DebugTAC_AI.Log("TACtical_AI: AI IDLE");
                    }
                }
                pilot.ProcessedDest = Helper.lastDestination; 
                // Orbit last position
                if ((Helper.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.ProcessedDest += GetOrbitFlight();
                }
                else
                {
                    pilot.ProcessedDest = Helper.lastDestination;
                }
            }
            else
            {
                bool combat = TryAdjustForCombat(false);
                if (combat)
                {
                    pilot.LowerEngines = true;
                    pilot.ProcessedDest = Helper.lastDestination;
                }
                else
                {
                    if (Helper.DriveDest == EDriveDest.ToLastDestination)
                    {   // Fly to target
                        pilot.LowerEngines = true;
                        if ((Helper.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.ProcessedDest = Helper.lastDestination + (pilot.Tank.rootBlockTrans.forward * 500);
                        }
                        else
                        {
                            pilot.ProcessedDest = Helper.lastDestination;
                        }
                    }
                    else if (Helper.DriveDest == EDriveDest.FromLastDestination)
                    {   // Fly away from target
                        pilot.LowerEngines = false;
                        pilot.ProcessedDest = ((pilot.Tank.trans.position - Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + pilot.Tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {   // Orbit last position
                        if ((pilot.ProcessedDest - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.ProcessedDest += GetOrbitFlight();
                        }
                        else
                        {
                            pilot.ProcessedDest = Helper.lastDestination;
                        }
                    }
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            bool NoRamOrTargetNotInPath;
            if (Helper.FullMelee && Helper.AttackEnemy)
            {
                if (Helper.lastEnemy?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (!Helper.FullMelee)
                pilot.ProcessedDest = AIEPathing.OffsetFromGroundA(pilot.ProcessedDest, Helper);
            pilot.ProcessedDest = AIEPathing.ModerateMaxAlt(pilot.ProcessedDest, Helper);
            pilot.ProcessedDest = AvoidAssist(pilot.ProcessedDest, pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock);

            if (Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, Helper, pilot.Tank, pilot.ProcessedDest);

            if (AvoidCrash && !pilot.TargetGrounded)
                AirplaneUtils.PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            return true;
        }

        /// <summary>
        /// Player click-based AI version (player RTS line following)
        /// Declares 3D points in WORLD space (AirborneDest) 
        /// </summary>
        /// <returns>Execution was successful</returns>
        public bool DriveDirectorRTS()
        {
            pilot.AdvisedThrottle = -1;
            Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;

            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.ProcessedDest = MultiTechUtils.HandleMultiTech(Helper, pilot.Tank);
                return true;
            }
            bool combat = false;

            if (Helper.RTSDestination == Vector3.zero)
                combat = TryAdjustForCombat(false);  // When set to chase then chase
            else
            {
                Helper.IgnoreEnemyDistance();
                pilot.TargetGrounded = false;
            }

            if (combat)
            {
                pilot.LowerEngines = true;
                pilot.ProcessedDest = Helper.lastDestination;
            }
            else
            {
                pilot.LowerEngines = true;
                Helper.lastDestination = Helper.RTSDestination;
                pilot.ProcessedDest = Helper.lastDestination;
                if ((Helper.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.ProcessedDest += GetOrbitFlight();
                }
                else
                {
                    pilot.ProcessedDest = Helper.RTSDestination;
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            bool NoRamOrTargetNotInPath;
            if (Helper.FullMelee && Helper.AttackEnemy)
            {
                if (Helper.lastEnemy?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (AvoidCrash)
                pilot.ProcessedDest = AIEPathing.OffsetFromGroundA(pilot.ProcessedDest, Helper);
            pilot.ProcessedDest = AIEPathing.ModerateMaxAlt(pilot.ProcessedDest, Helper);
            pilot.ProcessedDest = AvoidAssist(pilot.ProcessedDest, pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock);

            if (Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, Helper, pilot.Tank, pilot.ProcessedDest);

            if (AvoidCrash && !pilot.TargetGrounded)
                AirplaneUtils.PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            return true;
        }

        /// <summary>
        /// Non-Player automatic AI version 
        /// Declares 3D points in WORLD space (AirborneDest) 
        /// </summary>
        /// <returns>Execution was successful</returns>
        public bool DriveDirectorEnemy(EnemyMind mind)
        {
            pilot.AdvisedThrottle = -1;
            pilot.ForcePitchUp = false;
            Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck, Helper.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            Helper.Retreat = false;
            bool combat = TryAdjustForCombatEnemy(mind);
            if (combat)
            {
                pilot.LowerEngines = true;
                pilot.ProcessedDest = Helper.lastDestination;
            }
            else if (!mind.AttackPlayer)
            {   // Fly straight, above ground in player visual distance
                if (Helper.DriveDest == EDriveDest.ToLastDestination)
                {   // Fly to target
                    Helper.lastDestination = AIEPathing.OffsetFromGroundA(Helper.lastDestination, Helper);
                    if ((Helper.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.ProcessedDest = Helper.lastDestination + (pilot.Tank.rootBlockTrans.forward * 500);
                    }
                    else
                    {
                        pilot.ProcessedDest = Helper.lastDestination;
                    }
                }
                else
                    pilot.ProcessedDest = AIEPathing.ForceOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock + pilot.Tank.rootBlockTrans.forward, Helper);
            }
            else
            {
                pilot.LowerEngines = false;
                if ((pilot.ProcessedDest - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    //DebugTAC_AI.Log("TACtical_AI: Tech " + pilot.Tank.name + " Arrived at destination");

                    pilot.ProcessedDest += GetOrbitFlight();
                }
                else if (Helper.DriveDest == EDriveDest.ToLastDestination)
                {   // Fly to target
                    Helper.lastDestination = AIEPathing.OffsetFromGroundA(Helper.lastDestination, Helper);
                    if ((Helper.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.ProcessedDest = Helper.lastDestination + (pilot.Tank.rootBlockTrans.forward * 500);
                    }
                    else
                    {
                        pilot.ProcessedDest = Helper.lastDestination;
                    }
                }
                else if (Helper.DriveDest == EDriveDest.FromLastDestination)
                {   // Fly away from target
                    Helper.lastDestination = AIEPathing.OffsetFromGroundA(Helper.lastDestination, Helper);
                    pilot.ProcessedDest = ((pilot.Tank.boundsCentreWorldNoCheck - Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + pilot.Tank.boundsCentreWorldNoCheck;
                }
                else
                {   // Orbit above player height to invoke trouble
                    Helper.lastPlayer = Helper.GetPlayerTech();
                    if (Helper.lastPlayer.IsNotNull())
                    {
                        pilot.ProcessedDest.y = (Helper.lastPlayer.tank.boundsCentreWorldNoCheck + (Vector3.up * (Helper.GroundOffsetHeight / 5))).y;
                    }
                    else
                    {   // Fly forwards until target is found
                        pilot.ProcessedDest = AIEPathing.ForceOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock + pilot.Tank.rootBlockTrans.forward, Helper);

                        /* - Old
                        //Fly off the screen
                        //Vector3 fFlat = pilot.Tank.rootBlockTrans.forward;
                        //fFlat.y = 0.25f;
                        //pilot.AirborneDest = (fFlat.normalized * 1000) + pilot.Tank.boundsCentreWorldNoCheck;
                        */
                    }
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;
            bool NoRamOrTargetNotInPath;
            if (mind.LikelyMelee && Helper.AttackEnemy)
            {
                if (Helper.lastEnemy?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (AvoidCrash)
                pilot.ProcessedDest = AIEPathing.OffsetFromGroundA(pilot.ProcessedDest, Helper);
            pilot.ProcessedDest = RPathfinding.AvoidAssistEnemy(pilot.Tank, pilot.ProcessedDest, pilot.Tank.boundsCentreWorldNoCheck + (pilot.deltaMovementClock * pilot.AerofoilSluggishness), Helper, mind);
            
            if (Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, Helper, pilot.Tank, pilot.ProcessedDest);

            pilot.ProcessedDest = AIEPathing.ModerateMaxAlt(pilot.ProcessedDest, Helper);

            if (AvoidCrash && !pilot.TargetGrounded)
                AirplaneUtils.PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            return true;
        }

        /// <summary>
        /// Tells the Player AI where to go (in lastDestination) to handle a moving target
        /// </summary>
        /// <returns>True if the AI can perform combat navigation</returns>
        public bool TryAdjustForCombat(bool between)
        {
            bool output = false;
            if (Helper.PursueThreat && !Helper.Retreat && Helper.lastEnemy.IsNotNull())
            {
                output = true;
                Vector3 targPos = AirplaneUtils.ForeAiming(Helper.lastEnemy);
                if (between && Helper.theResource?.tank)
                {
                    targPos = Between(targPos, Helper.theResource.tank.boundsCentreWorldNoCheck);
                }
                Helper.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((Helper.lastCombatRange - Helper.IdealRangeCombat) / 3f, -1, 1);

                if (Helper.SideToThreat)
                {
                    if (Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.lastDestination = targPos;

                    }
                    else if (driveDyna == 1)
                    {
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.lastDestination = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.AdviseAway = true;
                        Helper.lastDestination = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else
                    {
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.lastDestination = targPos;
                    }
                }
                else
                {
                    if (Helper.FullMelee)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = targPos;
                    }
                    else if (driveDyna == 1)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.AdviseAway = true;
                        Helper.lastDestination = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else
                    {
                        Helper.lastDestination = AirplaneUtils.ForeAiming(Helper.lastEnemy);
                    }
                }

                Helper.UpdateEnemyDistance(Helper.lastEnemy.tank.boundsCentreWorld);

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);

                if (Helper.FullMelee)
                    pilot.AdvisedThrottle = 1;
                else
                    AirplaneUtils.AdviseThrottleTarget(pilot, Helper, pilot.Tank, Helper.lastEnemy);
            }
            else
            {
                Helper.IgnoreEnemyDistance();
                pilot.TargetGrounded = false;
            }
            return output;
        }


        /// <summary>
        /// Tells the Non-Player AI where to go (in lastDestination) to handle a moving target
        /// </summary>
        /// <returns>True if the AI can perform combat navigation</returns>
        public bool TryAdjustForCombatEnemy(EnemyMind mind)
        {
            bool output = false;

            bool isCombatAttitude = mind.CommanderMind != EnemyAttitude.OnRails && mind.AttackAny;
            if (!Helper.Retreat && Helper.lastEnemy.IsNotNull() && isCombatAttitude)
            {
                output = true;
                Helper.UpdateEnemyDistance(Helper.lastEnemy.tank.boundsCentreWorldNoCheck);
                float driveDyna = Mathf.Clamp((Helper.lastCombatRange - Helper.IdealRangeCombat) / 3f, -1, 1);
                
                if (Helper.SideToThreat)
                {
                    if (Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.lastDestination = AirplaneUtils.ForeAiming(Helper.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.lastDestination = RPathfinding.AvoidAssistEnemy(pilot.Tank, AirplaneUtils.ForeAiming(Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot), Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.AdviseAway = true;
                        Helper.lastDestination = RPathfinding.AvoidAssistEnemy(pilot.Tank, AirplaneUtils.ForeAiming(Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot), Helper, mind);
                    }
                    else
                    {
                        Helper.DriveDir = EDriveFacing.Perpendicular;
                        Helper.lastDestination = AirplaneUtils.ForeAiming(Helper.lastEnemy);
                    }
                }
                else
                {
                    if (Helper.FullMelee)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = AirplaneUtils.ForeAiming(Helper.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.lastDestination = RPathfinding.AvoidAssistEnemy(pilot.Tank, AirplaneUtils.ForeAiming(Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot), Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        Helper.DriveDir = EDriveFacing.Forwards;
                        Helper.AdviseAway = true;
                        Helper.lastDestination = RPathfinding.AvoidAssistEnemy(pilot.Tank, AirplaneUtils.ForeAiming(Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot), Helper, mind);
                    }
                    else
                    {
                        Helper.lastDestination = AirplaneUtils.ForeAiming(Helper.lastEnemy);
                    }
                }

                Helper.UpdateEnemyDistance(Helper.lastEnemy.tank.boundsCentreWorldNoCheck);

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (Helper.FullMelee)
                        pilot.AdvisedThrottle = 1;
                    else
                        AirplaneUtils.AdviseThrottleTarget(pilot, Helper, pilot.Tank, Helper.lastEnemy);
                }
                else
                    pilot.AdvisedThrottle = 1;  //if Ai not smrt enough just hold shift
            }
            else
            {
                Helper.IgnoreEnemyDistance();
                pilot.TargetGrounded = false;
            }
            return output;
        }


        private float Responsiveness => (AIGlobals.AerofoilSluggishnessBaseValue * 2) / pilot.AerofoilSluggishness;
        private float MoveSpacing(Vector3 predictionOffset)
        {
            return (predictionOffset - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
        }

        /// <summary>
        /// An airborne version of the Player AI pathfinding which handles obstructions
        /// </summary>
        /// <param name="targetIn"></param>
        /// <param name="predictionOffset"></param>
        /// <returns></returns>
        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            AIECore.TankAIHelper thisInst = Helper;
            Tank tank = pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                predictionOffset /= Responsiveness;
                float moveSpace = MoveSpacing(predictionOffset);
                if (thisInst.SecondAvoidence)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace + moveSpace)
                    {
                        if (lastAuxVal < thisInst.lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.ExtraSpace + moveSpace)
                        {
                            IntVector3 ProccessedVal2 = thisInst.GetOtherDir(lastCloseAlly) + thisInst.GetOtherDir(lastCloseAlly2);
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(predictionOffset, out lastAllyDist, tank);
                if (lastCloseAlly == null)
                    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.ExtraSpace + moveSpace)
                {
                    IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
                //AIECore.TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }

        public Vector3 GetOrbitFlight()
        {
            Vector3 lFlat;
            if (pilot.Tank.rootBlockTrans.up.y > 0)
                lFlat = -pilot.Tank.rootBlockTrans.right + (pilot.Tank.rootBlockTrans.forward * 2);
            else
                lFlat = pilot.Tank.rootBlockTrans.right + (pilot.Tank.rootBlockTrans.forward * 2);
            lFlat.y = -0.1f;
            //DebugTAC_AI.Log("TACtical_AI: GetOrbitFlight");
            return lFlat * 126;
        }
        public Vector3 Between(Vector3 Target, Vector3 other)
        {
            return (Target + other) / 2;
        }
    }
}
