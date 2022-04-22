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
        private float groundOffset => AIGlobals.AircraftGroundOffset + pilot.Helper.lastTechExtents;

        public virtual void Initiate(Tank tank, IMovementAIController pilot)
        {
            this.pilot = (AIControllerAir) pilot;
            this.pilot.FlyStyle = AIControllerAir.FlightType.Aircraft;
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
            if (pilot.Grounded) //|| thisInst.forceDrive)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, thisInst.lastTechExtents * 2))
                {
                    DriveMaintainerEmergLand(thisControl, thisInst, tank);
                    return false;
                }
                //WIP - Try fighting the controls to land safely

                return true;
            }
            //Debug.Log("TACtical_AI: Tech " + tank.name + " plane drive was called");

            if (tank.beam.IsActive)
            {   // BEAMING
                pilot.MainThrottle = 0;
                pilot.PerformUTurn = 0;
                this.pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat.Normalize();
                flat.y = 0.5f;
                AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 100));
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                pilot.MainThrottle = 1;
                pilot.PerformUTurn = 0;
                this.pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat.Normalize();
                flat.y = 0.5f;
                AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 100));
            }
            else
            {
                if (pilot.TargetGrounded && ((bool)thisInst.lastEnemy || (bool)thisInst.theResource || (bool)thisInst.theBase)) // Divebombing mode
                {  // We previously disabled the ground offset terrain avoider and aim directly at the enemy
                    Vector3 deltaAim = tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime;
                    float dist = (thisInst.lastDestination - (tank.boundsCentreWorldNoCheck + deltaAim)).magnitude;
                    Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(thisInst.lastDestination - tank.boundsCentreWorldNoCheck);
                    if (pilot.ForcePitchUp)
                        pilot.PerformDiveAttack = 0; // too low and we break off from the attack
                    if (dist < 32)
                    {   // target is in the air but grounded!?!?
                        pilot.PerformDiveAttack = 0; // abort

                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborting attack! Target too close!");
                        // AND PITCH UP NOW
                        pilot.MainThrottle = 1;
                        pilot.PerformUTurn = 0;
                        this.pilot.UpdateThrottle(thisInst, thisControl);
                        AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                    }
                    else if (pilot.PerformDiveAttack == 1)
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Aiming at target!");
                        if (Heading.x > 0.3f && Heading.x < -0.3f && Heading.z > 0)
                            pilot.PerformDiveAttack = 2; 
                        if (pilot.PerformUTurn > 0)
                        {   //The Immelmann Turn
                            AircraftUtils.UTurn(thisControl, thisInst, tank, pilot);
                            return true;
                        }
                        else if (pilot.PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
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
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (pilot.PerformDiveAttack == 2)
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  DIVEBOMBING!");
                        if (pilot.Helper.GetSpeed() < AIControllerAir.Stallspeed + 16 || Heading.y > -0.25f)
                            pilot.AdvisedThrottle = 1;
                        else
                            pilot.AdvisedThrottle = 0;
                        if (Heading.z < 0)
                            pilot.PerformDiveAttack = 0; // Passed by target
                        if (pilot.PerformUTurn > 0)
                        {   //The Immelmann Turn
                            AircraftUtils.UTurn(thisControl, thisInst, tank, pilot);
                            return true;
                        }
                        else if (pilot.PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
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
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (dist > AIControllerAir.GroundAttackStagingDist && Heading.z < 0)
                    {   // Launch teh attack run
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Turning back to face target at dist " + dist);
                        pilot.PerformDiveAttack = 1;
                    }
                    else
                    {
                        pilot.PerformUTurn = 0; // hold off on the U-Turn
                        if (Heading.z < 0.25f)
                        {   // Moving away from target
                            //Debug.Log("TACtical_AI: Tech " + tank.name + "  Gaining distance for attack run");
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            Vector3 AwayFlat = (tank.boundsCentreWorldNoCheck - pilot.AirborneDest).normalized;
                            AwayFlat.y = 0;
                            AwayFlat.Normalize();
                            AwayFlat.y = 0.25f;
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (AwayFlat.normalized * 300));
                        }
                        else
                        {   // Moving to target
                            //Debug.Log("TACtical_AI: Tech " + tank.name + "  Closing in on target");
                            if (pilot.Helper.GetSpeed() < AIControllerAir.Stallspeed + 16 || Heading.y > -0.25f)
                                pilot.AdvisedThrottle = 1;
                            else
                                pilot.AdvisedThrottle = 0;
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    return true;
                }

                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    AircraftUtils.UTurn(thisControl, thisInst, tank, pilot);
                    return true;
                }
                else if (pilot.PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
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
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
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
                    if (thisInst.DriveDir == EDriveType.Backwards)//EDriveType.Backwards
                    {   // Face back TOWARDS target
                        if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                        thisControl.DriveControl = 1f;
                    }
                    else if (thisInst.DriveDir == EDriveType.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                        //Debug.Log("Orbiting away");
                        thisControl.DriveControl = 1f;
                    }
                    else
                    {   // Face front TOWARDS target
                        if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                        thisControl.DriveControl = -1f;
                    }
                }
                else if (thisInst.DriveDir == EDriveType.Perpendicular)
                {   //Drive to target driving sideways, but obey distance
                    //int range = (int)(destDirect).magnitude;
                    float range = thisInst.lastRange;
                    if (range < thisInst.MinimumRad + 2)
                    {
                        if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                        //Debug.Log("Orbiting out " + thisInst.MinimumRad + " | " + destDirect);
                    }
                    else if (range > thisInst.MinimumRad + 22)
                    {
                        if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                        //Debug.Log("Orbiting in " + thisInst.MinimumRad);
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
                        //Debug.Log("Orbiting hold " + thisInst.MinimumRad);
                    }
                    thisControl.DriveControl = 1f;
                }
                else
                {
                    if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                        thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);//Face the music
                                                                                        //Debug.Log("TACtical_AI: AI " + tank.name + ":  driving to " + thisInst.lastDestination);
                    if (thisInst.MinimumRad > 0)
                    {
                        //if (thisInst.DriveDir == EDriveType.Perpendicular)
                        //    thisControl.DriveControl = 1f;
                        float range = thisInst.lastRange;
                        if (thisInst.DriveDir == EDriveType.Neutral)
                            thisControl.DriveControl = 0f;
                        else if (range < thisInst.MinimumRad - 1)
                        {
                            if (thisInst.DriveDir == EDriveType.Forwards)
                                thisControl.DriveControl = -1f;
                            else if (thisInst.DriveDir == EDriveType.Backwards)
                                thisControl.DriveControl = 1f;
                            else
                                thisControl.DriveControl = 0;

                        }
                        else if (range > thisInst.MinimumRad + 1)
                        {
                            if (thisInst.DriveDir == EDriveType.Forwards)
                                thisControl.DriveControl = 1f;
                            else if (thisInst.DriveDir == EDriveType.Backwards)
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
            if (thisInst.DriveDir == EDriveType.Neutral)
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
                if (thisInst.DriveDir == EDriveType.Backwards)
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
            else if (thisInst.forceDrive)
            {
                thisControl.DriveControl = thisInst.DriveVar;
                // Downed Aircraft can't boost as their engines are damaged
                if (thisInst.BOOST || thisInst.featherBoost)
                    thisControl.DriveControl = 1;
            }
            else if (thisInst.BOOST || thisInst.featherBoost)
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
            pilot.Helper.MinimumRad = 64;
            if (pilot.Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.AirborneDest = MultiTechUtils.HandleMultiTech(pilot.Helper, pilot.Tank);
                return true;
            }
            else if (pilot.Helper.ProceedToBase)
            {
                pilot.AdvisedThrottle = -1;
                if (pilot.Helper.lastBasePos.IsNotNull())
                {
                    pilot.Helper.DriveDir = EDriveType.Forwards;
                    pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.lastBasePos.position);
                }
                // Orbit last position
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right * 50);
                }
                else
                {
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastDestination, pilot.AerofoilSluggishness + groundOffset);
            }
            else if (pilot.Helper.ProceedToMine)
            {
                pilot.AdvisedThrottle = -1;
                if (pilot.Helper.theResource.tank != null)
                {
                    pilot.LowerEngines = true;
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.tank.boundsCentreWorldNoCheck);
                            pilot.Helper.MinimumRad = 2;
                        }
                        else
                        {
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.tank.boundsCentreWorldNoCheck);
                            pilot.Helper.MinimumRad = pilot.Helper.lastTechExtents + 2;
                        }
                    }
                    // Orbit last position
                    if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right * 50);
                    }
                    else
                    {
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                }
                else
                {
                    pilot.LowerEngines = false;
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.trans.position;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.trans.position);
                        }
                        else
                        {
                            pilot.Helper.DriveDir = EDriveType.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.centrePosition);
                        }
                    }
                }
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
                // Orbit last position
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right * 50);
                }
                else
                {
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastDestination, pilot.AerofoilSluggishness + groundOffset);
            }
            else if (pilot.Helper.DediAI == AIType.Aegis)
            {
                pilot.Helper.theResource = AIEPathing.ClosestUnanchoredAlly(pilot.Tank.boundsCentreWorldNoCheck, out _, pilot.Tank).visible;
                Combat = this.TryAdjustForCombat();
                if (!Combat)
                {
                    if (pilot.Helper.MoveFromObjective && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.transform.position;
                    }
                    else if (pilot.Helper.ProceedToObjective && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveType.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.AvoidAssist(pilot.Helper.theResource.tank.transform.position);
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
                pilot.AirborneDest = this.pilot.Helper.lastDestination; 
                // Orbit last position
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right * 50);
                }
                else
                {
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
            }
            else
            {
                bool combat = this.TryAdjustForCombat();
                if (combat)
                {
                    pilot.LowerEngines = true;
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
                else
                {
                    pilot.LowerEngines = false;
                    if (this.pilot.Helper.ProceedToObjective)
                    {   // Fly to target
                        if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 100);
                        }
                        else
                        {
                            pilot.AirborneDest = this.pilot.Helper.lastDestination;
                        }
                    }
                    else if (this.pilot.Helper.MoveFromObjective)
                    {   // Fly away from target
                        pilot.AirborneDest = ((this.pilot.Tank.trans.position - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.Tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {   // Orbit last position
                        if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right * 50);
                        }
                        else
                        {
                            pilot.AirborneDest = this.pilot.Helper.lastDestination;
                        }
                    }
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            if (!pilot.TargetGrounded && (unresponsiveAir || !pilot.Helper.FullMelee))
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = AvoidAssist(pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.deltaMovementClock * pilot.AerofoilSluggishness));

            if (pilot.Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AircraftUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

            Vector3 deltaAim = pilot.deltaMovementClock * pilot.AerofoilSluggishness;
            if (unresponsiveAir)
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + deltaAim + (Vector3.down * this.pilot.Helper.lastTechExtents), pilot.AerofoilSluggishness + groundOffset))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            else
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + deltaAim, pilot.AerofoilSluggishness + groundOffset))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
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
            bool combat = false;
            if (pilot.Helper.RTSDestination == Vector3.zero)
                combat = TryAdjustForCombat();  // When set to chase then chase
            if (combat)
            {
                pilot.LowerEngines = true;
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else
            {
                pilot.LowerEngines = false;
                pilot.AirborneDest = this.pilot.Helper.RTSDestination;
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right * 50);
                }
                else
                {
                    pilot.AirborneDest = this.pilot.Helper.RTSDestination;
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            if (!pilot.TargetGrounded && (unresponsiveAir || !pilot.Helper.FullMelee))
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = AvoidAssist(pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.deltaMovementClock * pilot.AerofoilSluggishness));

            if (pilot.Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AircraftUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

            Vector3 deltaAim = pilot.deltaMovementClock * pilot.AerofoilSluggishness;
            if (unresponsiveAir)
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + deltaAim + (Vector3.down * this.pilot.Helper.lastTechExtents), pilot.AerofoilSluggishness + groundOffset))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            else
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + deltaAim, pilot.AerofoilSluggishness + groundOffset))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
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
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck, pilot.Helper.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            this.pilot.Helper.Retreat = false;
            bool combat = this.TryAdjustForCombatEnemy(mind);
            if (combat)
            {
                pilot.LowerEngines = true;
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else if (!mind.AttackPlayer)
            {   // Fly straight, above ground in player visual distance
                if (this.pilot.Helper.ProceedToObjective)
                {   // Fly to target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 100);
                    }
                    else
                    {
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                }
                else
                    pilot.AirborneDest = AIEPathing.ForceOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock + pilot.Tank.rootBlockTrans.forward, pilot.Helper);
            }
            else
            {
                pilot.LowerEngines = false;
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    //Debug.Log("TACtical_AI: Tech " + this.pilot.Tank.name + " Arrived at destination");

                    Vector3 lFlat;
                    if (this.pilot.Tank.rootBlockTrans.up.y > 0)
                        lFlat = -this.pilot.Tank.rootBlockTrans.right + (this.pilot.Tank.rootBlockTrans.forward * 2);
                    else
                        lFlat = this.pilot.Tank.rootBlockTrans.right + (this.pilot.Tank.rootBlockTrans.forward * 2);
                    lFlat.y = 0.1f;
                    pilot.AirborneDest += (lFlat * 50);
                }
                else if (this.pilot.Helper.ProceedToObjective)
                {   // Fly to target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 100);
                    }
                    else
                    {
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                }
                else if (this.pilot.Helper.MoveFromObjective)
                {   // Fly away from target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    pilot.AirborneDest = ((this.pilot.Tank.trans.position - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.Tank.boundsCentreWorldNoCheck;
                }
                else
                {   // Orbit above player height to invoke trouble
                    this.pilot.Helper.lastPlayer = this.pilot.Helper.GetPlayerTech();
                    if (this.pilot.Helper.lastPlayer.IsNotNull())
                    {
                        pilot.AirborneDest.y = (this.pilot.Helper.lastPlayer.tank.boundsCentreWorldNoCheck + (Vector3.up * (this.pilot.Helper.GroundOffsetHeight / 5))).y;
                    }
                    else
                    {   // Fly forwards until target is found
                        pilot.AirborneDest = AIEPathing.ForceOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock + pilot.Tank.rootBlockTrans.forward, pilot.Helper);

                        /* - Old
                        //Fly off the screen
                        //Vector3 fFlat = this.pilot.Tank.rootBlockTrans.forward;
                        //fFlat.y = 0.25f;
                        //pilot.AirborneDest = (fFlat.normalized * 1000) + this.pilot.Tank.boundsCentreWorldNoCheck;
                        */
                    }
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            if (!pilot.TargetGrounded && (unresponsiveAir || !pilot.Helper.FullMelee))
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            pilot.AirborneDest = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.deltaMovementClock * pilot.AerofoilSluggishness), this.pilot.Helper, mind);
            
            if (pilot.Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AircraftUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);

            Vector3 deltaAim = pilot.deltaMovementClock * pilot.AerofoilSluggishness;
            if (unresponsiveAir)
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + deltaAim + (Vector3.down * this.pilot.Helper.lastTechExtents), pilot.AerofoilSluggishness + groundOffset))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            else
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + deltaAim, pilot.AerofoilSluggishness + groundOffset))
                {
                    if (pilot.Helper.FullMelee)
                    {   // If we can manever nicely and have the melee AI, then we can ram
                        //pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                    }
                    else
                    {
                        pilot.ForcePitchUp = true;
                        pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Tells the Player AI where to go (in lastDestination) to handle a moving target
        /// </summary>
        /// <returns>True if the AI can perform combat navigation</returns>
        public bool TryAdjustForCombat()
        {
            bool output = false;
            if (this.pilot.Helper.PursueThreat && !this.pilot.Helper.Retreat && this.pilot.Helper.lastEnemy.IsNotNull())
            {
                output = true;
                pilot.Helper.lastRangeCombat = (pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((pilot.Helper.lastRangeCombat - this.pilot.Helper.IdealRangeCombat) / 3f, -1, 1);
                if (this.pilot.Helper.SideToThreat)
                {
                    if (this.pilot.Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }
                else
                {
                    if (this.pilot.Helper.FullMelee)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else
                    {
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }

                this.pilot.Helper.lastRange = (this.pilot.Helper.lastEnemy.tank.boundsCentreWorld - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);

                if (pilot.Helper.FullMelee)
                    pilot.AdvisedThrottle = 1;
                else
                    AircraftUtils.AdviseThrottleTarget(pilot, this.pilot.Helper, this.pilot.Tank, this.pilot.Helper.lastEnemy);
            }
            else
                pilot.Helper.lastRangeCombat = float.MaxValue;
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
            if (!this.pilot.Helper.Retreat && this.pilot.Helper.lastEnemy.IsNotNull() && isCombatAttitude)
            {
                output = true;
                pilot.Helper.lastRangeCombat = (pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((pilot.Helper.lastRangeCombat - this.pilot.Helper.IdealRangeCombat) / 3f, -1, 1);
                if (this.pilot.Helper.SideToThreat)
                {
                    if (this.pilot.Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }
                else
                {
                    if (this.pilot.Helper.FullMelee)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else
                    {
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }
                //pilot.AdvisedThrottle = driveDyna;

                this.pilot.Helper.lastRange = (this.pilot.Helper.lastEnemy.tank.boundsCentreWorld - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (pilot.Helper.FullMelee)
                        pilot.AdvisedThrottle = 1;
                    else
                        AircraftUtils.AdviseThrottleTarget(pilot, this.pilot.Helper, this.pilot.Tank, this.pilot.Helper.lastEnemy);
                }
                else
                    pilot.AdvisedThrottle = 1;  //if Ai not smrt enough just hold shift
            }
            else
                pilot.Helper.lastRangeCombat = float.MaxValue;
            return output;
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
            AIECore.TankAIHelper thisInst = this.pilot.Helper;
            Tank tank = this.pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                if (thisInst.SecondAvoidence)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                    {
                        if (lastAuxVal < thisInst.lastTechExtents + lastCloseAlly2.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
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
                    Debug.Log("TACtical_AI: ALLY IS NULL");
                if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                {
                    IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                Debug.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
                //AIECore.TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }
    }
}
