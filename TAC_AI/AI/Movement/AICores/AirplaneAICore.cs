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
    internal class AirplaneAICore : IMovementAICore
    {
        internal AIControllerAir pilot;
        internal TankAIHelper Helper => pilot.Helper;
        private float groundOffset => AIGlobals.GroundOffsetAircraft + Helper.lastTechExtents;

        public virtual void Initiate(Tank tank, IMovementAIController pilot)
        {
            this.pilot = (AIControllerAir) pilot;
            this.pilot.FlyStyle = AIControllerAir.FlightType.Aircraft;
            Helper.GroundOffsetHeight = Helper.lastTechExtents + AIGlobals.GroundOffsetAircraft;
        }

        /// <summary>
        /// Drives the Tech to the desired location (AIControllerAir.AirborneDest) in world space
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <returns></returns>
        public virtual bool DriveMaintainer(TankControl thisControl, TankAIHelper thisInst, Tank tank, ref EControlCoreSet core)
        {
            if (pilot.Grounded) //|| thisInst.ForceSetDrive)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, thisInst.lastTechExtents * 2))
                {
                    DriveMaintainerEmergLand(thisControl, thisInst, tank, ref core);
                    return false;
                }
                //WIP - Try fighting the controls to land safely

                return true;
            }
            //DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " plane drive was called

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
                if (pilot.TargetGrounded && (thisInst.lastEnemyGet || thisInst.theResource || thisInst.theBase)) // Divebombing mode
                {  // We previously disabled the ground offset terrain avoider and aim directly at the enemy
                    Vector3 posOffset = thisInst.lastDestinationCore - thisInst.DodgeSphereCenter;
                    float dist = posOffset.magnitude;
                    float dist2D = posOffset.SetY(0).magnitude;
                    Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(thisInst.lastDestinationCore - tank.boundsCentreWorldNoCheck);
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
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
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
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestinationCore);
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
                            AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
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
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestinationCore);
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
                            Vector3 AwayFlat = (tank.boundsCentreWorldNoCheck - pilot.PathPointSet).normalized;
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
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                            else    // Aim nose at target
                                AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestinationCore);
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
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
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
                    AirplaneUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.PathPointSet);
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
        public bool DriveMaintainerEmergLand(TankControl thisControl, TankAIHelper thisInst, Tank tank, ref EControlCoreSet core)
        {
            TankControl.ControlState control3D = (TankControl.ControlState)VehicleAICore.controlGet.GetValue(tank.control);

            control3D.m_State.m_InputRotation = Vector3.zero;
            control3D.m_State.m_InputMovement = Vector3.zero;
            VehicleAICore.controlGet.SetValue(tank.control, control3D);
            Vector3 destDirect = thisInst.lastDestinationOp - tank.boundsCentreWorldNoCheck;
            // DEBUG FOR DRIVE ERRORS
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));

            thisControl.DriveControl = 0f;
            if (thisInst.DoSteerCore)
            {
                if (thisInst.AdviseAwayCore)
                {   //Move from target
                    if (core.DriveDir == EDriveFacing.Backwards)//EDriveType.Backwards
                    {   // Face back TOWARDS target
                        VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core);
                        thisControl.DriveControl = 1f;
                    }
                    else if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core);
                        //DebugTAC_AI.Log("Orbiting away");
                        thisControl.DriveControl = 1f;
                    }
                    else
                    {   // Face front TOWARDS target
                        VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core);
                        thisControl.DriveControl = -1f;
                    }
                }
                else if (core.DriveDir == EDriveFacing.Perpendicular)
                {   //Drive to target driving sideways, but obey distance
                    //int range = (int)(destDirect).magnitude;
                    float range = thisInst.lastOperatorRange;
                    if (range < thisInst.MinimumRad + 2)
                    {
                        VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core);
                        //DebugTAC_AI.Log("Orbiting out " + thisInst.MinimumRad + " | " + destDirect);
                    }
                    else if (range > thisInst.MinimumRad + 22)
                    {
                        VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core);
                        //DebugTAC_AI.Log("Orbiting in " + thisInst.MinimumRad);
                    }
                    else  //ORBIT!
                    {
                        Vector3 aimDirect;
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.right) < 0)
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.down);
                        else
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.up);
                        VehicleUtils.Turner(thisControl, thisInst, aimDirect, ref core);
                        //DebugTAC_AI.Log("Orbiting hold " + thisInst.MinimumRad);
                    }
                    thisControl.DriveControl = 1f;
                }
                else
                {
                    VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core);//Face the music
                                                                                    //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  driving to " + thisInst.lastDestination);
                    if (thisInst.MinimumRad > 0)
                    {
                        //if (thisInst.DriveDir == EDriveType.Perpendicular)
                        //    thisControl.DriveControl = 1f;
                        float range = thisInst.lastOperatorRange;
                        if (core.DriveDir <= EDriveFacing.Neutral)
                            thisControl.DriveControl = 0f;
                        else if (range < thisInst.MinimumRad - 1)
                        {
                            if (core.DriveDir == EDriveFacing.Forwards)
                                thisControl.DriveControl = -1f;
                            else if (core.DriveDir == EDriveFacing.Backwards)
                                thisControl.DriveControl = 1f;
                            else
                                thisControl.DriveControl = 0;

                        }
                        else if (range > thisInst.MinimumRad + 1)
                        {
                            if (core.DriveDir == EDriveFacing.Forwards)
                                thisControl.DriveControl = 1f;
                            else if (core.DriveDir == EDriveFacing.Backwards)
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
            if (core.DriveDir == EDriveFacing.Stop)
            {
                thisControl.DriveControl = 0f;
                return true;
            }
            if (core.DriveDir == EDriveFacing.Neutral)
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
                if (core.DriveDir == EDriveFacing.Backwards)
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
                if (thisInst.FullBoost || thisInst.LightBoost)
                    thisControl.DriveControl = 1;
            }
            else if (thisInst.FullBoost || thisInst.LightBoost)
                thisControl.DriveControl = 1;
            return true;
        }

        /// <summary>
        /// Player automatic AI version (player following)
        /// Declares 3D points in WORLD space (AirborneDest) 
        /// </summary>
        /// <returns>Execution was successful</returns>
        public bool DriveDirector(ref EControlCoreSet core)
        {
            pilot.AdvisedThrottle = -1;
            Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;
            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.PathPointSet = MultiTechUtils.HandleMultiTech(Helper, pilot.Tank, ref core);
                return true;
            }
            else if (Helper.DriveDestDirected == EDriveDest.ToBase)
            {
                pilot.AdvisedThrottle = -1;
                pilot.LowerEngines = true;
                if (Helper.lastBasePos.IsNotNull())
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    //Helper.lastDestination 
                    pilot.PathPointSet = Helper.AvoidAssistPrecise(Helper.lastBasePos.position);
                }
                // Orbit last position
                if ((pilot.PathPointSet - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.PathPointSet += (-pilot.Tank.rootBlockTrans.right.SetY(0).normalized * 129);
                }
                else
                {
                    pilot.PathPointSet = Helper.lastDestinationOp;
                }
                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastDestinationOp, pilot.AerofoilSluggishness + groundOffset);
            }
            else if (Helper.DriveDestDirected == EDriveDest.ToMine)
            {
                pilot.AdvisedThrottle = -1;
                if (Helper.theResource.tank != null)
                {
                    pilot.LowerEngines = true;
                    if (Helper.PivotOnly)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.lastDestination = Helper.theResource.tank.boundsCentreWorldNoCheck;
                        pilot.PathPointSet = core.lastDestination;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            core.lastDestination = Helper.theResource.tank.boundsCentreWorldNoCheck;
                            pilot.PathPointSet = core.lastDestination;
                            Helper.MinimumRad = 2;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            core.lastDestination = Helper.theResource.tank.boundsCentreWorldNoCheck;
                            pilot.PathPointSet = Helper.AvoidAssistPrecise(core.lastDestination);
                            Helper.MinimumRad = Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    pilot.LowerEngines = false;
                    if (Helper.PivotOnly)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.lastDestination = Helper.theResource.trans.position;
                        pilot.PathPointSet = core.lastDestination;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            core.lastDestination = Helper.theResource.trans.position;
                            pilot.PathPointSet = Helper.AvoidAssistPrecise(core.lastDestination);
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            core.lastDestination = Helper.theResource.centrePosition;
                            pilot.PathPointSet = Helper.AvoidAssistPrecise(core.lastDestination);
                        }
                    }
                }
                // Orbit last position
                if ((pilot.PathPointSet - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.PathPointSet += GetOrbitFlight();
                }
                else
                {
                    pilot.PathPointSet = Helper.lastDestinationOp;
                }
                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastDestinationOp, pilot.AerofoilSluggishness + groundOffset);
            }
            else if (Helper.DediAI == AIType.Aegis)
            {
                Helper.theResource = AIEPathing.ClosestUnanchoredAlly(AIEPathing.AllyList(pilot.Tank), 
                    pilot.Tank.boundsCentreWorldNoCheck, Helper.MaxCombatRange * Helper.MaxCombatRange, out _, pilot.Tank).visible;
                TryAdjustForCombat(true, ref pilot.PathPointSet, ref core);
                if (Helper.lastCombatRange > Helper.MaxCombatRange)
                {
                    if (Helper.theResource.IsNotNull())
                    {
                        if (Helper.DriveDestDirected == EDriveDest.FromLastDestination)
                        {
                            pilot.LowerEngines = false;
                            core.DriveDir = EDriveFacing.Forwards;
                            core.lastDestination = Helper.theResource.tank.transform.position;
                            pilot.PathPointSet = core.lastDestination;
                        }
                        else if (Helper.DriveDestDirected == EDriveDest.ToLastDestination)
                        {
                            pilot.LowerEngines = true;
                            core.DriveDir = EDriveFacing.Forwards;
                            core.lastDestination = Helper.theResource.tank.transform.position;
                            pilot.PathPointSet = Helper.AvoidAssist(core.lastDestination);
                        }
                        else
                        {
                            //DebugTAC_AI.Log("TACtical_AI: AI IDLE");
                        }
                    }
                }
                // Orbit last position
                if ((Helper.lastDestinationOp - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.PathPointSet += GetOrbitFlight();
                }
                else
                {
                    pilot.PathPointSet = Helper.lastDestinationOp;
                }
            }
            else
            {
                if (TryAdjustForCombat(false, ref pilot.PathPointSet, ref core))
                {
                    pilot.LowerEngines = true;
                }
                else
                {
                    if (Helper.DriveDestDirected == EDriveDest.ToLastDestination)
                    {   // Fly to target
                        pilot.LowerEngines = true;
                        if ((Helper.lastDestinationOp - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.PathPointSet = Helper.lastDestinationOp + (pilot.Tank.rootBlockTrans.forward * 500);
                        }
                        else
                        {
                            pilot.PathPointSet = Helper.lastDestinationOp;
                        }
                    }
                    else if (Helper.DriveDestDirected == EDriveDest.FromLastDestination)
                    {   // Fly away from target
                        pilot.LowerEngines = false;
                        pilot.PathPointSet = ((pilot.Tank.trans.position - Helper.lastDestinationOp).normalized * (pilot.DestSuccessRad * 2)) + pilot.Tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {   // Orbit last position
                        if ((pilot.PathPointSet - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            pilot.PathPointSet += GetOrbitFlight();
                        }
                        else
                        {
                            pilot.PathPointSet = Helper.lastDestinationOp;
                        }
                    }
                }
            }
            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            bool NoRamOrTargetNotInPath;
            if (Helper.FullMelee && Helper.AttackEnemy)
            {
                if (Helper.lastEnemyGet?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (!Helper.FullMelee)
                pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, Helper);
            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, Helper);
            pilot.PathPointSet = AvoidAssist(pilot.PathPointSet, pilot.Helper.DodgeSphereCenter);

            if (Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, Helper, pilot.Tank, pilot.PathPointSet);

            if (AvoidCrash && !pilot.TargetGrounded)
                AirplaneUtils.PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            if (Helper.Yield)
                pilot.ForcePitchUp = true;
            return true;
        }


        /// <summary>
        /// Player click-based AI version (player RTS line following)
        /// Declares 3D points in WORLD space (AirborneDest) 
        /// </summary>
        /// <returns>Execution was successful</returns>
        public bool DriveDirectorRTS(ref EControlCoreSet core)
        {
            pilot.AdvisedThrottle = -1;
            Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;

            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.PathPointSet = MultiTechUtils.HandleMultiTech(Helper, pilot.Tank, ref core);
                return true;
            }

            pilot.LowerEngines = true;
            if (Helper.RTSDestination == TankAIHelper.RTSDisabled)
            {
                if (!TryAdjustForCombat(false, ref pilot.PathPointSet, ref core)) // When set to chase then chase
                {
                    if ((Helper.lastDestinationOp - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.PathPointSet += GetOrbitFlight();
                    }
                    else
                    {
                        pilot.PathPointSet = Helper.lastDestinationOp;
                    }
                }
            }
            else
            {
                Helper.IgnoreEnemyDistance();
                pilot.TargetGrounded = false;
                core.lastDestination = Helper.RTSDestination;
                pilot.PathPointSet = Helper.RTSDestination;
                if ((core.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    pilot.PathPointSet += GetOrbitFlight();
                }
                else
                {
                    pilot.PathPointSet = Helper.RTSDestination;
                }
            }

            bool unresponsiveAir = pilot.LargeAircraft || pilot.BankOnly;

            bool NoRamOrTargetNotInPath;
            if (Helper.FullMelee && Helper.AttackEnemy)
            {
                if (Helper.lastEnemyGet?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (AvoidCrash)
                pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, Helper);
            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, Helper);
            pilot.PathPointSet = AvoidAssist(pilot.PathPointSet, pilot.Helper.DodgeSphereCenter);

            if (Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, Helper, pilot.Tank, pilot.PathPointSet);

            if (AvoidCrash && !pilot.TargetGrounded)
                AirplaneUtils.PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            if (Helper.Yield)
                pilot.ForcePitchUp = true;
            return true;
        }

        /// <summary>
        /// Non-Player automatic AI version 
        /// Declares 3D points in WORLD space (AirborneDest) 
        /// </summary>
        /// <returns>Execution was successful</returns>
        public bool DriveDirectorEnemy(EnemyMind mind, ref EControlCoreSet core)
        {
            pilot.AdvisedThrottle = -1;
            pilot.ForcePitchUp = false;
            Helper.MinimumRad = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGroundTech(Helper, Helper.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            //Helper.Retreat = false;
            if (TryAdjustForCombatEnemy(mind, ref pilot.PathPointSet, ref core))
            {
                pilot.LowerEngines = true;
            }
            else if (!mind.AttackPlayer)
            {   // Fly straight, above ground in player visual distance
                if (Helper.DriveDestDirected == EDriveDest.ToLastDestination)
                {   // Fly to target
                    core.lastDestination = AIEPathing.OffsetFromGroundA(Helper.lastDestinationOp, Helper);
                    if ((core.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.PathPointSet = core.lastDestination + (pilot.Tank.rootBlockTrans.forward * 500);
                    }
                    else
                    {
                        pilot.PathPointSet = core.lastDestination;
                    }
                }
                else
                    pilot.PathPointSet = AIEPathing.SnapOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + pilot.Tank.rootBlockTrans.forward, Helper);
            }
            else
            {
                pilot.LowerEngines = false;
                if ((pilot.PathPointSet - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    //DebugTAC_AI.Log("TACtical_AI: Tech " + pilot.Tank.name + " Arrived at destination");

                    pilot.PathPointSet += GetOrbitFlight();
                }
                else if (Helper.DriveDestDirected == EDriveDest.ToLastDestination)
                {   // Fly to target
                    core.lastDestination = AIEPathing.OffsetFromGroundA(Helper.lastDestinationOp, Helper);
                    if ((core.lastDestination - pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.PathPointSet = core.lastDestination + (pilot.Tank.rootBlockTrans.forward * 500);
                    }
                    else
                    {
                        pilot.PathPointSet = core.lastDestination;
                    }
                }
                else if (Helper.DriveDestDirected == EDriveDest.FromLastDestination)
                {   // Fly away from target
                    pilot.PathPointSet = ((pilot.Tank.boundsCentreWorldNoCheck - AIEPathing.OffsetFromGroundA(Helper.lastDestinationOp, Helper))
                        .normalized * (pilot.DestSuccessRad * 2)) + pilot.Tank.boundsCentreWorldNoCheck;
                }
                else
                {   // Orbit above player height to invoke trouble
                    Helper.lastPlayer = Helper.GetPlayerTech();
                    if (Helper.lastPlayer.IsNotNull())
                    {
                        pilot.PathPointSet.y = (Helper.lastPlayer.tank.boundsCentreWorldNoCheck + (Vector3.up * (Helper.GroundOffsetHeight / 5))).y;
                    }
                    else
                    {   // Fly forwards until target is found
                        pilot.PathPointSet = AIEPathing.SnapOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + pilot.Tank.rootBlockTrans.forward, Helper);

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
                if (Helper.lastEnemyGet?.tank && pilot.Tank.rootBlockTrans.InverseTransformVector(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).z > 0.75f)
                    NoRamOrTargetNotInPath = false;
                else
                    NoRamOrTargetNotInPath = true;
            }
            else
                NoRamOrTargetNotInPath = true;
            bool AvoidCrash = unresponsiveAir || NoRamOrTargetNotInPath;

            if (AvoidCrash)
                pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, Helper);
            pilot.PathPointSet = Helper.AvoidAssistPrediction(pilot.PathPointSet, pilot.AerofoilSluggishness);
            
            if (Helper.FullMelee && !unresponsiveAir)
                pilot.AdvisedThrottle = 1;
            else
                AirplaneUtils.AdviseThrottle(pilot, Helper, pilot.Tank, pilot.PathPointSet);

            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, Helper);

            if (AvoidCrash && !pilot.TargetGrounded)
                AirplaneUtils.PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            if (Helper.Yield)
                pilot.ForcePitchUp = true;
            return true;
        }

        /// <summary>
        /// Tells the Player AI where to go (in lastDestination) to handle a moving target
        /// </summary>
        /// <returns>True if the AI can perform combat navigation</returns>
        public bool TryAdjustForCombat(bool between, ref Vector3 pos, ref EControlCoreSet core)
        {
            bool output = false;
            if (Helper.ChaseThreat && !Helper.Retreat && Helper.lastEnemyGet.IsNotNull())
            {
                output = true;
                Vector3 targPos = Helper.RoughPredictTarget(Helper.lastEnemyGet.tank);
                if (between && Helper.theResource?.tank)
                {
                    targPos = Between(targPos, Helper.theResource.tank.boundsCentreWorldNoCheck);
                }
                Helper.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((Helper.lastCombatRange - Helper.MinCombatRange) / 3f, -1, 1);

                if (Helper.SideToThreat)
                {
                    if (Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = targPos;

                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = targPos;
                    }
                }
                else
                {
                    if (Helper.FullMelee)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = targPos;
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = AvoidAssist(targPos, AirplaneUtils.TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else
                    {
                        pos = Helper.RoughPredictTarget(Helper.lastEnemyGet.tank);
                    }
                }

                Helper.UpdateEnemyDistance(Helper.lastEnemyGet.tank.boundsCentreWorld);

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);

                if (Helper.FullMelee)
                    pilot.AdvisedThrottle = 1;
                else
                    AirplaneUtils.AdviseThrottleTarget(pilot, Helper, pilot.Tank, Helper.lastEnemyGet);
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
        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            bool output = false;

            bool isCombatAttitude = mind.CommanderMind != EnemyAttitude.OnRails && mind.AttackAny;
            if (!Helper.Retreat && Helper.lastEnemyGet.IsNotNull() && isCombatAttitude)
            {
                output = true;
                Helper.UpdateEnemyDistance(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                float driveDyna = Mathf.Clamp((Helper.lastCombatRange - Helper.MinCombatRange) / 3f, -1, 1);
                
                if (Helper.SideToThreat)
                {
                    if (Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = Helper.RoughPredictTarget(Helper.lastEnemyGet.tank);

                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = Helper.AvoidAssistPrediction(Helper.RoughPredictTarget(Helper.lastEnemyGet.tank), pilot.AerofoilSluggishness);
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = Helper.AvoidAssistPrediction(Helper.RoughPredictTarget(Helper.lastEnemyGet.tank), pilot.AerofoilSluggishness);
                    }
                    else
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = Helper.RoughPredictTarget(Helper.lastEnemyGet.tank);
                    }
                }
                else
                {
                    if (Helper.FullMelee)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = Helper.RoughPredictTarget(Helper.lastEnemyGet.tank);
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = Helper.AvoidAssistPrediction(Helper.RoughPredictTarget(Helper.lastEnemyGet.tank), pilot.AerofoilSluggishness);
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveAwayFacingAway();
                        pos = Helper.AvoidAssistPrediction(Helper.RoughPredictTarget(Helper.lastEnemyGet.tank), pilot.AerofoilSluggishness);
                    }
                    else
                    {
                        pos = Helper.RoughPredictTarget(Helper.lastEnemyGet.tank);
                    }
                }

                Helper.UpdateEnemyDistance(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + groundOffset);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (Helper.FullMelee)
                        pilot.AdvisedThrottle = 1;
                    else
                        AirplaneUtils.AdviseThrottleTarget(pilot, Helper, pilot.Tank, Helper.lastEnemyGet);
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

        /// <summary>
        /// An airborne version of the Player AI pathfinding which handles obstructions
        /// </summary>
        /// <param name="targetIn"></param>
        /// <param name="predictionOffset"></param>
        /// <returns></returns>
        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            TankAIHelper thisInst = Helper;
            Tank tank = pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                predictionOffset /= Responsiveness;
                float moveSpace = (predictionOffset - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                if (thisInst.SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                    {
                        if (lastAuxVal < thisInst.lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                        {
                            IntVector3 ProccessedVal2 = thisInst.GetOtherDir(lastCloseAlly) + thisInst.GetOtherDir(lastCloseAlly2);
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, predictionOffset, out lastAllyDist, tank);
                if (lastCloseAlly == null)
                    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
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
                //TankAIManager.FetchAllAllies();
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
