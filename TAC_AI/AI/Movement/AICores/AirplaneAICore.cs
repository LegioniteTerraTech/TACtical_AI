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
            pilot.Helper.GroundOffsetHeight = pilot.Helper.lastTechExtents + AIGlobals.AircraftGroundOffset;
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
            //Debug.Log("TACtical_AI: Tech " + tank.name + " plane drive was called");

            if (tank.beam.IsActive && thisInst.recentSpeed < 8)
            {   // BEAMING
                pilot.MainThrottle = 0;
                pilot.PerformUTurn = 0;
                this.pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat = flat.normalized;
                flat.y = 0.5f;
                //Debug.Log("TACtical_AI: Tech " + tank.name + " is in build beam");
                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 1000));
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                pilot.MainThrottle = 1;
                pilot.PerformUTurn = 0;
                this.pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat = flat.normalized;
                flat.y = 1f;
                //Debug.Log("TACtical_AI: Tech " + tank.name + " is grounded: " + tank.grounded + " | is ForcePitchUp: " + pilot.ForcePitchUp);
                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 1000));
            }
            else
            {
                if (pilot.TargetGrounded && (thisInst.lastEnemy || thisInst.theResource || thisInst.theBase)) // Divebombing mode
                {  // We previously disabled the ground offset terrain avoider and aim directly at the enemy
                    Vector3 deltaAim = pilot.deltaMovementClock;
                    float dist = (thisInst.lastDestination - (tank.boundsCentreWorldNoCheck + deltaAim)).SetY(0).magnitude;
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
                        AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 500));
                    }
                    else if (pilot.PerformDiveAttack == 1)
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Aiming at target!");
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
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
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
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (pilot.PerformDiveAttack == 2)
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  DIVEBOMBING!");
                        if (pilot.Helper.GetSpeed() < AIGlobals.AirStallSpeed + 16 || Heading.y > -0.25f)
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
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
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
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (dist > AIGlobals.GroundAttackStagingDist && Heading.z < 0)
                    {   // Launch teh attack run
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Turning back to face target at dist " + dist);
                        pilot.PerformDiveAttack = 1;
                    }
                    else
                    {
                        pilot.PerformUTurn = 0; // hold off on the U-Turn
                        if (Heading.z < 0.35f)
                        {   // Moving away from target
                            //Debug.Log("TACtical_AI: Tech " + tank.name + "  Gaining distance for attack run");
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            Vector3 AwayFlat = (tank.boundsCentreWorldNoCheck - pilot.AirborneDest).normalized;
                            AwayFlat.y = 0;
                            AwayFlat = AwayFlat.normalized;
                            AwayFlat.y = 0.1f;
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (AwayFlat.normalized * 1000));
                        }
                        else
                        {   // Moving to target
                            //Debug.Log("TACtical_AI: Tech " + tank.name + "  Closing in on target");
                            if (pilot.Helper.GetSpeed() < AIGlobals.AirStallSpeed + 16 || Heading.y > -0.25f)
                                pilot.AdvisedThrottle = 1;
                            else
                                pilot.AdvisedThrottle = 0;
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            pilot.UpdateThrottle(thisInst, thisControl);
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
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
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
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
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
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
                else if (thisInst.DriveDir == EDriveFacing.Perpendicular)
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
            pilot.Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + pilot.Helper.lastTechExtents;
            if (pilot.Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.AirborneDest = MultiTechUtils.HandleMultiTech(pilot.Helper, pilot.Tank);
                return true;
            }
            else if (pilot.Helper.DriveDest == EDriveDest.ToBase)
            {
                pilot.AdvisedThrottle = -1;
                pilot.LowerEngines = true;
                if (pilot.Helper.lastBasePos.IsNotNull())
                {
                    pilot.Helper.DriveDir = EDriveFacing.Forwards;
                    pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.lastBasePos.position);
                }
                // Orbit last position
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right.SetY(0).normalized * 129);
                }
                else
                {
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastDestination, pilot.AerofoilSluggishness + groundOffset);
            }
            else if (pilot.Helper.DriveDest == EDriveDest.ToMine)
            {
                pilot.AdvisedThrottle = -1;
                if (pilot.Helper.theResource.tank != null)
                {
                    pilot.LowerEngines = true;
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.tank.boundsCentreWorldNoCheck);
                            pilot.Helper.MinimumRad = 2;
                        }
                        else
                        {
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.tank.boundsCentreWorldNoCheck);
                            pilot.Helper.MinimumRad = pilot.Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    pilot.LowerEngines = false;
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.trans.position;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.trans.position);
                        }
                        else
                        {
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.AvoidAssistPrecise(pilot.Helper.theResource.centrePosition);
                        }
                    }
                }
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
                // Orbit last position
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += GetOrbitFlight();
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
                Combat = this.TryAdjustForCombat(true);
                if (!Combat)
                {
                    if (pilot.Helper.DriveDest == EDriveDest.FromLastDestination && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.LowerEngines = false;
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.transform.position;
                    }
                    else if (pilot.Helper.DriveDest == EDriveDest.ToLastDestination && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.LowerEngines = true;
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.AvoidAssist(pilot.Helper.theResource.tank.transform.position);
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
                pilot.AirborneDest = this.pilot.Helper.lastDestination; 
                // Orbit last position
                if ((pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += GetOrbitFlight();
                }
                else
                {
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
            }
            else
            {
                bool combat = this.TryAdjustForCombat(false);
                if (combat)
                {
                    pilot.LowerEngines = true;
                    pilot.AirborneDest = this.pilot.Helper.lastDestination;
                }
                else
                {
                    if (this.pilot.Helper.DriveDest == EDriveDest.ToLastDestination)
                    {   // Fly to target
                        pilot.LowerEngines = true;
                        if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 500);
                        }
                        else
                        {
                            pilot.AirborneDest = this.pilot.Helper.lastDestination;
                        }
                    }
                    else if (this.pilot.Helper.DriveDest == EDriveDest.FromLastDestination)
                    {   // Fly away from target
                        pilot.LowerEngines = false;
                        pilot.AirborneDest = ((this.pilot.Tank.trans.position - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.Tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {   // Orbit last position
                        if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.AirborneDest += GetOrbitFlight();
                        }
                        else
                        {
                            pilot.AirborneDest = this.pilot.Helper.lastDestination;
                        }
                    }
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            bool NoRamOrTargetNotInPath;
            if (pilot.Helper.FullMelee && pilot.Helper.AttackEnemy)
            {
                if (pilot.Helper.lastEnemy?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (!pilot.Helper.FullMelee)
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = AvoidAssist(pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + this.pilot.deltaMovementClock);

            if (pilot.Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

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
            pilot.Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + pilot.Helper.lastTechExtents;
            bool combat = false;
            if (pilot.Helper.RTSDestination == Vector3.zero)
                combat = TryAdjustForCombat(false);  // When set to chase then chase
            else
            {
                pilot.Helper.lastRangeEnemy = float.MaxValue;
                pilot.TargetGrounded = false;
            }

            if (combat)
            {
                pilot.LowerEngines = true;
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else
            {
                pilot.LowerEngines = true;
                pilot.Helper.lastDestination = pilot.Helper.RTSDestination;
                pilot.AirborneDest = pilot.Helper.lastDestination;
                if ((pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.AirborneDest += GetOrbitFlight();
                }
                else
                {
                    pilot.AirborneDest = pilot.Helper.RTSDestination;
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            bool NoRamOrTargetNotInPath;
            if (pilot.Helper.FullMelee && pilot.Helper.AttackEnemy)
            {
                if (pilot.Helper.lastEnemy?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (AvoidCrash)
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = AvoidAssist(pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + this.pilot.deltaMovementClock);

            if (pilot.Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

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
            pilot.Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + pilot.Helper.lastTechExtents;
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
                if (this.pilot.Helper.DriveDest == EDriveDest.ToLastDestination)
                {   // Fly to target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 500);
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

                    pilot.AirborneDest += GetOrbitFlight();
                }
                else if (this.pilot.Helper.DriveDest == EDriveDest.ToLastDestination)
                {   // Fly to target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 500);
                    }
                    else
                    {
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                }
                else if (this.pilot.Helper.DriveDest == EDriveDest.FromLastDestination)
                {   // Fly away from target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    pilot.AirborneDest = ((this.pilot.Tank.boundsCentreWorldNoCheck - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.Tank.boundsCentreWorldNoCheck;
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
            bool NoRamOrTargetNotInPath;
            if (mind.LikelyMelee && pilot.Helper.AttackEnemy)
            {
                if (pilot.Helper.lastEnemy?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (AvoidCrash)
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            pilot.AirborneDest = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.deltaMovementClock * pilot.AerofoilSluggishness), this.pilot.Helper, mind);
            
            if (pilot.Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);

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
            if (this.pilot.Helper.PursueThreat && !this.pilot.Helper.Retreat && this.pilot.Helper.lastEnemy.IsNotNull())
            {
                output = true;
                Vector3 targPos = AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                if (between && this.pilot.Helper.theResource?.tank)
                {
                    targPos = Between(targPos, this.pilot.Helper.theResource.tank.boundsCentreWorldNoCheck);
                }
                pilot.Helper.lastRangeEnemy = (targPos - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((pilot.Helper.lastRangeEnemy - this.pilot.Helper.IdealRangeCombat) / 3f, -1, 1);

                if (this.pilot.Helper.SideToThreat)
                {
                    if (this.pilot.Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.lastDestination = targPos;

                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.lastDestination = targPos;
                    }
                }
                else
                {
                    if (this.pilot.Helper.FullMelee)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        this.pilot.Helper.lastDestination = targPos;
                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else
                    {
                        this.pilot.Helper.lastDestination = AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }

                this.pilot.Helper.lastRange = (this.pilot.Helper.lastEnemy.tank.boundsCentreWorld - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);

                if (pilot.Helper.FullMelee)
                    pilot.AdvisedThrottle = 1;
                else
                    AirplaneUtils.AdviseThrottleTarget(pilot, this.pilot.Helper, this.pilot.Tank, this.pilot.Helper.lastEnemy);
            }
            else
            {
                pilot.Helper.lastRangeEnemy = float.MaxValue;
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
            if (!this.pilot.Helper.Retreat && this.pilot.Helper.lastEnemy.IsNotNull() && isCombatAttitude)
            {
                output = true;
                pilot.Helper.lastRangeEnemy = (pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((pilot.Helper.lastRangeEnemy - this.pilot.Helper.IdealRangeCombat) / 3f, -1, 1);
                
                if (this.pilot.Helper.SideToThreat)
                {
                    if (this.pilot.Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.lastDestination = AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Perpendicular;
                        this.pilot.Helper.lastDestination = AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }
                else
                {
                    if (this.pilot.Helper.FullMelee)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        this.pilot.Helper.lastDestination = AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy), AirplaneUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else
                    {
                        this.pilot.Helper.lastDestination = AirplaneUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }

                this.pilot.Helper.lastRange = (this.pilot.Helper.lastEnemy.tank.boundsCentreWorld - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (pilot.Helper.FullMelee)
                        pilot.AdvisedThrottle = 1;
                    else
                        AirplaneUtils.AdviseThrottleTarget(pilot, this.pilot.Helper, this.pilot.Tank, this.pilot.Helper.lastEnemy);
                }
                else
                    pilot.AdvisedThrottle = 1;  //if Ai not smrt enough just hold shift
            }
            else
            {
                pilot.Helper.lastRangeEnemy = float.MaxValue;
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
            AIECore.TankAIHelper thisInst = this.pilot.Helper;
            Tank tank = this.pilot.Tank;

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
            if (this.pilot.Tank.rootBlockTrans.up.y > 0)
                lFlat = -this.pilot.Tank.rootBlockTrans.right + (this.pilot.Tank.rootBlockTrans.forward * 2);
            else
                lFlat = this.pilot.Tank.rootBlockTrans.right + (this.pilot.Tank.rootBlockTrans.forward * 2);
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
