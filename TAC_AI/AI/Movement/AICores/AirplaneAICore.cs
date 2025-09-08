using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TerraTechETCUtil;


namespace TAC_AI.AI.Movement.AICores
{
    internal class AirplaneAICore : IMovementAICore
    {
        internal AIControllerAir pilot;
        internal TankAIHelper Helper => pilot.Helper;
        private float groundOffset => AIGlobals.GroundOffsetAircraft + Helper.lastTechExtents;
        public float GetDrive => pilot.CurrentThrottle;
        public bool BankOnly = false;           // Similar to LargeAircraft but for smaller aircraft
        public int PerformDiveAttack = 0;       // set this to one to launch dive bombing
        public int PerformUTurn = 0;            // set this to one to ignite the multi-stage process

        public virtual void Initiate(Tank tank, IMovementAIController pilotSet)
        {
            pilot = (AIControllerAir) pilotSet;
            pilot.FlyStyle = AIControllerAir.FlightType.Aircraft;
            Helper.GroundOffsetHeight = Helper.lastTechExtents + AIGlobals.GroundOffsetAircraft;
            float GravityForce = tank.rbody.mass * tank.GetGravityScale() * TankAIManager.GravMagnitude;
            float totalFwdThrust = pilot.FwdThrust + pilot.BoosterThrust * AIGlobals.BoosterThrustBias;
            BankOnly = totalFwdThrust < AIGlobals.ImmelmanTtWRThreshold * GravityForce;

            if (BankOnly)
            {
                DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + tank.name + " does not apply enough forwards thrust " +
                    totalFwdThrust + " vs " + (AIGlobals.ImmelmanTtWRThreshold * GravityForce) + " to perform an immelmann.");
            }
        }

        /// <summary>
        /// Drives the Tech to the desired location (AIControllerAir.AirborneDest) in world space
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /// <returns></returns>
        public virtual bool DriveMaintainer(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
            if (pilot.Grounded) //|| helper.ForceSetDrive)
            {   //Become a ground vehicle for now
                DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": " + tank.name + " is GROUNDED!!!");
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, helper.lastTechExtents * 2))
                {
                    DriveMaintainerEmergLand(helper, tank, ref core);
                    return false;
                }
                //WIP - Try fighting the controls to land safely

                return true;
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " plane drive was called

            if (tank.beam.IsActive && helper.recentSpeed < 8)
            {   // BEAMING
                pilot.MainThrottle = 0;
                PerformUTurn = 0;
                pilot.UpdateThrottle(helper);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat = flat.normalized;
                flat.y = 0.5f;
                DebugTAC_AI.LogSpecific(tank,KickStart.ModID + ": Tech " + tank.name + " is in build beam");
                AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 1000));
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                pilot.MainThrottle = 1;
                PerformUTurn = 0;
                pilot.UpdateThrottle(helper);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat = flat.normalized;
                flat.y = 1f;
                DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + " is grounded: " + tank.grounded + " | is ForcePitchUp: " + pilot.ForcePitchUp);
                AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 1000));
            }
            else
            {
                if (pilot.TargetGrounded && (helper.lastEnemyGet || helper.theResource || helper.theBase)) // Divebombing mode
                {  // We previously disabled the ground offset terrain avoider and aim directly at the enemy
                    Vector3 posOffset = helper.lastDestinationCore - helper.DodgeSphereCenter;
                    float dist = posOffset.magnitude;
                    float dist2D = posOffset.SetY(0).magnitude;
                    Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(helper.lastDestinationCore - tank.boundsCentreWorldNoCheck);
                    if (pilot.ForcePitchUp)
                        PerformDiveAttack = 0; // too low and we break off from the attack
                    if (dist < 32)
                    {   // target is in the air but grounded!?!?
                        PerformDiveAttack = 0; // abort

                        DebugTAC_AI.LogSpecific(tank,KickStart.ModID + ": Tech " + tank.name + "  Aborting attack! Target too close! UTurn[" +
                            PerformUTurn + "], DiveAttack[" + PerformDiveAttack + "]");
                        // AND PITCH UP NOW
                        pilot.MainThrottle = 1;
                        PerformUTurn = 0;
                        pilot.UpdateThrottle(helper);
                        AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 500));
                    }
                    else if (PerformDiveAttack == 1)
                    {
                        DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + "  Aiming at target! UTurn[" +
                            PerformUTurn + "], DiveAttack[" + PerformDiveAttack + "]");
                        if (Heading.x > 0.3f && Heading.x < -0.3f && Heading.z > 0)
                            PerformDiveAttack = 2; 
                        if (PerformUTurn > 0)
                        {   //The Immelmann Turn
                            UTurn(helper, tank, pilot);
                            return true;
                        }
                        else if (PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(helper);
                            AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
                            {
                                PerformUTurn = 0;
                                if (PerformDiveAttack == 1)
                                    PerformDiveAttack = 2;
                            }
                            return true;
                        }
                        else
                        {
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(helper);
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                            else    // Aim nose at target
                                AngleTowards(helper, tank, pilot, helper.lastDestinationCore);
                        }
                    }
                    else if (PerformDiveAttack == 2)
                    {
                        DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + "  DIVEBOMBING! UTurn[" +
                            PerformUTurn + "], DiveAttack[" + PerformDiveAttack + "]");
                        if (Helper.GetSpeed() < AIGlobals.AirStallSpeed + 16 || Heading.y > -0.25f)
                            pilot.AdvisedThrottle = 1;
                        else
                            pilot.AdvisedThrottle = 0;
                        if (Heading.z < 0)
                            PerformDiveAttack = 0; // Passed by target
                        if (PerformUTurn > 0)
                        {   //The Immelmann Turn
                            UTurn(helper, tank, pilot);
                            return true;
                        }
                        else if (PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(helper);
                            AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
                            {
                                PerformUTurn = 0;
                                if (PerformDiveAttack == 1)
                                    PerformDiveAttack = 2;
                            }
                            return true;
                        }
                        else
                        {
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            pilot.UpdateThrottle(helper);
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                            else    // Aim nose at target
                                AngleTowards(helper, tank, pilot, helper.lastDestinationCore);
                        }
                    }
                    else if (dist2D > AIGlobals.GroundAttackStagingDist && Heading.z < 0)
                    {   // Launch teh attack run
                        DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + "  Turning back to face target at dist " + dist);
                        PerformDiveAttack = 1;
                    }
                    else
                    {
                        PerformUTurn = 0; // hold off on the U-Turn
                        if (Heading.z < 0.35f)
                        {   // Moving away from target
                            DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + "  Gaining distance for attack run");
                            pilot.MainThrottle = 1;
                            pilot.UpdateThrottle(helper);
                            Vector3 AwayFlat = (tank.boundsCentreWorldNoCheck - pilot.PathPointSet).normalized;
                            AwayFlat.y = 0;
                            AwayFlat = AwayFlat.normalized;
                            AwayFlat.y = 0.2f;
                            AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck + (AwayFlat.normalized * 1000));
                        }
                        else
                        {   // Moving to target
                            DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + "  Closing in on target");
                            if (Helper.GetSpeed() < AIGlobals.AirStallSpeed + 16 || Heading.y > -0.25f)
                                pilot.AdvisedThrottle = 1;
                            else
                                pilot.AdvisedThrottle = 0;
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            pilot.UpdateThrottle(helper);
                            if (pilot.LargeAircraft)    //Aim vaguely at target
                                AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                            else    // Aim nose at target
                                AngleTowards(helper, tank, pilot, helper.lastDestinationCore);
                        }
                    }
                    return true;
                }

                if (PerformUTurn > 0)
                {   //The Immelmann Turn
                    UTurn(helper, tank, pilot);
                    return true;
                }
                else if (PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    pilot.UpdateThrottle(helper);
                    AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized) > 0)
                    {
                        PerformUTurn = 0;
                        if (PerformDiveAttack == 1)
                            PerformDiveAttack = 2;
                    }
                    return true;
                }
                else
                {
                    pilot.MainThrottle = pilot.AdvisedThrottle;
                    pilot.UpdateThrottle(helper);
                    AngleTowards(helper, tank, pilot, pilot.PathPointSet);
                }
            }

            return true;
        }

        /// <summary>
        /// A very limited version of the VehicleAICore DriveMaintainer for downed aircraft
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /// <returns></returns>
        public bool DriveMaintainerEmergLand(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
            TankControl.ControlState control3D = (TankControl.ControlState)VehicleUtils.controlGet.GetValue(tank.control);

            control3D.m_State.m_InputRotation = Vector3.zero;
            control3D.m_State.m_InputMovement = Vector3.zero;
            VehicleUtils.controlGet.SetValue(tank.control, control3D);
            Vector3 destDirect = helper.lastDestinationOp - tank.boundsCentreWorldNoCheck;
            // DEBUG FOR DRIVE ERRORS
            if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));

            helper.DriveControl = 0f;
            if (helper.DoSteerCore)
            {
                if (helper.AdviseAwayCore)
                {   //Move from target
                    if (core.DriveDir == EDriveFacing.Backwards)//EDriveType.Backwards
                    {   // Face back TOWARDS target
                        VehicleUtils.Turner(helper, -destDirect, 0, ref core);
                        helper.DriveControl = 1f;
                    }
                    else if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        VehicleUtils.Turner(helper, -destDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting away");
                        helper.DriveControl = 1f;
                    }
                    else
                    {   // Face front TOWARDS target
                        VehicleUtils.Turner(helper, destDirect, 0, ref core);
                        helper.DriveControl = -1f;
                    }
                }
                else if (core.DriveDir == EDriveFacing.Perpendicular)
                {   //Drive to target driving sideways, but obey distance
                    //int range = (int)(destDirect).magnitude;
                    float range = helper.lastOperatorRange;
                    if (range < helper.AutoSpacing + 2)
                    {
                        VehicleUtils.Turner(helper, -destDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting out " + helper.MinimumRad + " | " + destDirect);
                    }
                    else if (range > helper.AutoSpacing + 22)
                    {
                        VehicleUtils.Turner(helper, destDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting in " + helper.MinimumRad);
                    }
                    else  //ORBIT!
                    {
                        Vector3 aimDirect;
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.right) < 0)
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.down);
                        else
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.up);
                        VehicleUtils.Turner(helper, aimDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting hold " + helper.MinimumRad);
                    }
                    helper.DriveControl = 1f;
                }
                else
                {
                    VehicleUtils.Turner(helper, destDirect, 0, ref core);//Face the music
                                                                                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  driving to " + helper.lastDestination);
                    if (helper.AutoSpacing > 0)
                    {
                        //if (helper.DriveDir == EDriveType.Perpendicular)
                        //    helper.DriveControl = 1f;
                        float range = helper.lastOperatorRange;
                        if (core.DriveDir <= EDriveFacing.Neutral)
                            helper.DriveControl = 0f;
                        else if (range < helper.AutoSpacing - 1)
                        {
                            if (core.DriveDir == EDriveFacing.Forwards)
                                helper.DriveControl = -1f;
                            else if (core.DriveDir == EDriveFacing.Backwards)
                                helper.DriveControl = 1f;
                            else
                                helper.DriveControl = 0;

                        }
                        else if (range > helper.AutoSpacing + 1)
                        {
                            if (core.DriveDir == EDriveFacing.Forwards)
                                helper.DriveControl = 1f;
                            else if (core.DriveDir == EDriveFacing.Backwards)
                                helper.DriveControl = -1f;
                            else
                                helper.DriveControl = 1f;
                        }
                    }
                }
            }
            else
                helper.DriveControl = 0;

            // Overrides to translational drive
            if (core.DriveDir == EDriveFacing.Stop)
            {
                helper.DriveControl = 0f;
                return true;
            }
            if (core.DriveDir == EDriveFacing.Neutral)
            {   // become brakeless
                helper.DriveControl = 0.001f;
                return true;
            }

            // Operate normally
            switch (helper.ThrottleState)
            {
                case AIThrottleState.PivotOnly:
                    helper.DriveControl = 0;
                    break;
                case AIThrottleState.Yield:
                    if (core.DriveDir == EDriveFacing.Backwards)
                    {
                        if (helper.recentSpeed > 10)
                            helper.DriveControl = 0.2f;
                        else
                            helper.DriveControl = -1f;
                    }
                    else
                    {   // works with forwards
                        if (helper.recentSpeed > 10)
                            helper.DriveControl = -0.2f;
                        else
                            helper.DriveControl = 1f;
                    }
                    break;
                case AIThrottleState.FullSpeed:
                    if (helper.FullBoost || helper.LightBoost)
                        helper.DriveControl = 1;
                    break;
                case AIThrottleState.ForceSpeed:
                    helper.DriveControl = helper.DriveVar;
                    // Downed Aircraft can't boost as their engines are damaged
                    if (helper.FullBoost || helper.LightBoost)
                        helper.DriveControl = 1;
                    break;
                default:
                    break;
            }
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
            Helper.AutoSpacing = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;
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
                    if (Helper.ThrottleState == AIThrottleState.PivotOnly)
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
                            Helper.AutoSpacing = 2;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            core.lastDestination = Helper.theResource.tank.boundsCentreWorldNoCheck;
                            pilot.PathPointSet = Helper.AvoidAssistPrecise(core.lastDestination);
                            Helper.AutoSpacing = Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    pilot.LowerEngines = false;
                    if (Helper.ThrottleState == AIThrottleState.PivotOnly)
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
            else if (Helper.DediAI == AIType.Aegis || (pilot.EnemyMind && pilot.EnemyMind.CommanderMind == EnemyAttitude.Guardian))
            {
                Helper.theResource = AIEPathing.ClosestUnanchoredAllyAegis(TankAIManager.GetTeamTanks(pilot.Tank.Team), 
                    pilot.Tank.boundsCentreWorldNoCheck, Helper.MaxCombatRange * Helper.MaxCombatRange, out _,
                    pilot.Helper).visible;
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
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI IDLE");
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
            bool unresponsiveAir = pilot.LargeAircraft || BankOnly;

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
                AdviseThrottle(pilot, Helper, pilot.Tank, pilot.PathPointSet);

            if (AvoidCrash && !pilot.TargetGrounded)
                PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            if (Helper.ThrottleState == AIThrottleState.Yield)
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
            Helper.AutoSpacing = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;

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

            bool unresponsiveAir = pilot.LargeAircraft || BankOnly;

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
                AdviseThrottle(pilot, Helper, pilot.Tank, pilot.PathPointSet);

            if (AvoidCrash && !pilot.TargetGrounded)
                PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            if (Helper.ThrottleState == AIThrottleState.Yield)
                pilot.ForcePitchUp = true;
            return true;
        }

        public bool DriveDirectorEnemyRTS(EnemyMind mind, ref EControlCoreSet core)
        {
            pilot.AdvisedThrottle = -1;
            Helper.AutoSpacing = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;

            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.PathPointSet = MultiTechUtils.HandleMultiTech(Helper, pilot.Tank, ref core);
                return true;
            }

            pilot.LowerEngines = true;
            if (Helper.RTSDestination == TankAIHelper.RTSDisabled)
            {
                if (!TryAdjustForCombatEnemy(mind, ref pilot.PathPointSet, ref core)) // When set to chase then chase
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

            bool unresponsiveAir = pilot.LargeAircraft || BankOnly;

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
                AdviseThrottle(pilot, Helper, pilot.Tank, pilot.PathPointSet);

            if (AvoidCrash && !pilot.TargetGrounded)
                PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            if (Helper.ThrottleState == AIThrottleState.Yield)
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
            Helper.AutoSpacing = AIGlobals.AircraftDestSuccessRadius + Helper.lastTechExtents;
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
                    //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + pilot.Tank.name + " Arrived at destination");

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
            bool unresponsiveAir = pilot.LargeAircraft || BankOnly;
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
                AdviseThrottle(pilot, Helper, pilot.Tank, pilot.PathPointSet);

            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, Helper);

            if (AvoidCrash && !pilot.TargetGrounded)
                PreventCollisionWithGround(pilot, groundOffset, unresponsiveAir);
            if (Helper.ThrottleState == AIThrottleState.Yield)
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
                        pos = AvoidAssist(targPos, TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = AvoidAssist(targPos, TryGetVelocityOffset(pilot.Tank, pilot));
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
                        pos = AvoidAssist(targPos, TryGetVelocityOffset(pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = AvoidAssist(targPos, TryGetVelocityOffset(pilot.Tank, pilot));
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
                    AdviseThrottleTarget(pilot, Helper, pilot.Tank, Helper.lastEnemyGet);
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

            bool isCombatAttitude = mind.CommanderMind != EnemyAttitude.OnRails;
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
                        AdviseThrottleTarget(pilot, Helper, pilot.Tank, Helper.lastEnemyGet);
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
            TankAIHelper helper = Helper;
            Tank tank = pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                predictionOffset /= Responsiveness;
                float moveSpace = (predictionOffset - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                if (helper.SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, predictionOffset, 
                        out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, pilot.Helper);
                    if (lastCloseAlly && lastAllyDist < helper.lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                    {
                        if (lastCloseAlly2 && lastAuxVal < helper.lastTechExtents + lastCloseAlly2.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                        {
                            IntVector3 ProccessedVal2 = helper.GetOtherDir(lastCloseAlly) + helper.GetOtherDir(lastCloseAlly2);
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = helper.GetOtherDir(lastCloseAlly);
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, predictionOffset, out lastAllyDist, pilot.Helper);
                if (lastCloseAlly == null)
                {
                    // DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                    return targetIn;
                }
                if (lastAllyDist < helper.lastTechExtents + lastCloseAlly.GetCheapBounds() + AIGlobals.PathfindingExtraSpace + moveSpace)
                {
                    IntVector3 ProccessedVal = helper.GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AvoidAssistAir IS NaN!!");
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
            //DebugTAC_AI.Log(KickStart.ModID + ": GetOrbitFlight");
            return lFlat * 126;
        }
        public Vector3 Between(Vector3 Target, Vector3 other)
        {
            return (Target + other) / 2;
        }


        // Utilities
        private const float UprightBankNudgeMultiplierFighter = 0.5f;
        private const float UprightBankNudgeMultiplierSlow = 0.75f;

        public void UTurn(TankAIHelper helper, Tank tank, AIControllerAir pilot)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
            pilot.MainThrottle = 1;
            pilot.UpdateThrottle(helper);
            if (helper.LocalSafeVelocity.z < AIGlobals.AirStallSpeed)
            {   //ABORT!!!
                DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + "  Aborted U-Turn with velocity " + helper.LocalSafeVelocity.z);
                PerformUTurn = -1;
                pilot.ErrorsInUTurn++;
                if (pilot.ErrorsInUTurn > 3)
                    DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " has failed to U-Turn/Immelmann over 3 times and will no longer try");
            }
            else if (Vector3.Dot(Vector3.down, helper.SafeVelocity.normalized) > 0.6f)
            {   //ABORT!!!
                DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                PerformUTurn = -1;
                pilot.ErrorsInUTurn++;
                if (pilot.ErrorsInUTurn > 3)
                    DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + " has failed to U-Turn/Immelmann over 3 times and will no longer try");
            }
            if (PerformUTurn == 1)
            {   // Accelerate
                DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + " Executing U-Turn[1]...");
                // DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck), KickStart.ModID + ": ASSERT - " + tank.name + " is UTurning above max allowed altitude");
                AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck +
                    (tank.rootBlockTrans.forward.SetY(0).normalized.SetY(0.4f) * 300));
                if (pilot.CurrentThrottle > 0.95)
                    PerformUTurn = 2;
            }
            else if (PerformUTurn == 2)
            {   // Pitch Up
                DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + " Executing U-Turn[2]...");
                //DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck), KickStart.ModID + ": ASSERT - " + tank.name + " is UTurning above max allowed altitude");
                AngleTowards(helper, tank, pilot, tank.boundsCentreWorldNoCheck + (tank.rootBlockTrans.forward.SetY(1.75f).normalized * 100));
                if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.65f)
                    PerformUTurn = 3;
            }
            else if (PerformUTurn == 3)
            {   // Aim back at target
                DebugTAC_AI.LogSpecific(tank, KickStart.ModID + ": Tech " + tank.name + " Executing U-Turn[3]...");
                AngleTowards(helper, tank, pilot, pilot.PathPointSet.SetY(tank.boundsCentreWorldNoCheck.y));
                if (Vector3.Dot((pilot.PathPointSet - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.1f)
                {
                    pilot.ErrorsInUTurn = 0;
                    PerformUTurn = 0;
                    if (PerformDiveAttack == 1)
                        PerformDiveAttack = 2;
                }
            }
        }
        public Vector3 DetermineRollUpright(Tank tank, AIControllerAir pilot, Vector3 Navi3DDirect, bool forceUp, out float nudgeTargPosUp)
        {
            //Vector3 turnValUp = AIGlobals.LookRot(tank.rootBlockTrans.forward, tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
            nudgeTargPosUp = 0;

            if (forceUp)
                return Vector3.up;
            Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(Navi3DDirect);
            float fwdHeading = Heading.ToVector2XZ().normalized.y;
            bool targetLevelElevation = Navi3DDirect.y > -0.6f && Navi3DDirect.y < 0.6f;

            Vector3 upright = Vector3.up;
            if (PerformUTurn == 3)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  Stage 3 Immelmann");
                upright = Vector3.down;
            }
            else if (tank.rootBlockTrans.up.y < -0.4f)
            {   // handle invalid request to go upside down
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  IS UPSIDE DOWN AND IS TRYING TO GET UPRIGHT");
                // Stay upright
            }
            else if ((PerformUTurn > 0 && !pilot.LargeAircraft && !BankOnly) || pilot.ForcePitchUp)
            {
                // Stay upright
            }
            else if (fwdHeading < -0.325f && Heading.y < 0.6f && targetLevelElevation && PerformUTurn == 0 &&
                AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck.y))
            {   // Check we are not facing target, not pointing up, target is level elevation,
                //  are within interactive height limit, and we aren't already doing UTurn
                //DebugTAC_AI.Log("directed is " + Navi3DDirect);
                if (pilot.ErrorsInUTurn > 3)    // Aircraft failed Immelmann over 3 times in a row
                    PerformUTurn = -1;
                else if (pilot.LargeAircraft || BankOnly)   // Large aircraft cannot do the Immelmann
                    PerformUTurn = -1;
                else                            // Perform the Immelmann turn, or better known as the "U-Turn"
                    PerformUTurn = 1;
            }
            else if (pilot.LargeAircraft || BankOnly)
            {
                // Because we likely yaw slower, we should bank as much as possible
                if (targetLevelElevation && fwdHeading < 0.925f - (0.2f / pilot.RollStrength))
                {
                    if (Heading.x > 0f)
                    { // We roll to aim at target
                      //DebugTAC_AI.Log(KickStart.ModID + ": (HVY) Tech " + tank.name + "  Roll turn Right");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, false);
                        rFlat.y = -pilot.RollStrength / 2;
                        upright = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                        nudgeTargPosUp = UprightBankNudgeMultiplierSlow;
                    }
                    else if (Heading.x < 0f)
                    { // We roll to aim at target
                      //DebugTAC_AI.Log(KickStart.ModID + ": (HVY) Tech " + tank.name + "  Roll turn Left");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, false);
                        rFlat.y = pilot.RollStrength / 2;
                        upright = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                        nudgeTargPosUp = UprightBankNudgeMultiplierSlow;
                    }
                }
            }
            else
            {
                if (targetLevelElevation && fwdHeading < 0.85f - (0.2f / pilot.RollStrength))
                {
                    if (Heading.x > 0f)
                    { // We roll to aim at target
                      //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  Roll turn Right");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, true);
                        rFlat.y = -pilot.RollStrength;
                        upright = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                        nudgeTargPosUp = UprightBankNudgeMultiplierFighter;
                    }
                    else if (Heading.x < 0f)
                    { // We roll to aim at target
                      //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  Roll turn Left");
                        Vector3 rFlat = GetExactRightAlignedWorld(tank, true);
                        rFlat.y = pilot.RollStrength;
                        upright = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
                        nudgeTargPosUp = UprightBankNudgeMultiplierFighter;
                    }
                }
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": upwards direction " + tank.name + "  is " + direct.y);

            return upright; // IS IN WORLD SPACE
        }
        public void AngleTowards(TankAIHelper helper, Tank tank,
            AIControllerAir pilot, Vector3 destPos, bool EmergencyUp = false)
        {
            //AI Steering Rotational
            Transform root = tank.rootBlockTrans;

            if (pilot.LargeAircraft)
            {
                if (!AIEPathing.AboveHeightFromGround(pilot.Helper.DodgeSphereCenter, AIGlobals.GroundOffsetAircraft))
                {
                    EmergencyUp = true;
                }
            }
            else if (!AIEPathing.AboveHeightFromGround(pilot.Helper.DodgeSphereCenter, helper.lastTechExtents + 4))
            {
                EmergencyUp = true;
            }
            Vector3 noseDirect = (destPos - tank.boundsCentreWorldNoCheck).normalized;
            if (EmergencyUp)// || root.forward.y < -AIGlobals.AircraftDangerDive)
            {   // CRASH LIKELY, PULL UP! 
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is trying to break from a crash-dive " + root.forward.y);
                noseDirect = new Vector3(0, 1.45f, 0) + root.forward.SetY(0).normalized;
            }
            else if (noseDirect.y < -AIGlobals.AircraftMaxDive)
            {
                noseDirect = noseDirect.SetY(-AIGlobals.AircraftMaxDive).normalized;
            }
            else if (Vector3.Dot(noseDirect, root.forward) < 0 && !pilot.ForcePitchUp && PerformUTurn == 0)
            {
                // Try deal with turns well exceeding 90 degrees
                Vector3 clamped = root.InverseTransformVector(noseDirect);
                if (clamped.z < 0)
                {
                    clamped.y = 0;
                    clamped.z = 0;
                }
                noseDirect = root.TransformVector(clamped);
                // Level when turning far
                noseDirect = noseDirect.SetY(0).normalized;
                noseDirect.y = 0.1f;
            }
            helper.Navi3DDirect = noseDirect.normalized;

            helper.Navi3DUp = DetermineRollUpright(tank, pilot, helper.Navi3DDirect, EmergencyUp, out float upNudge);
            if (helper.Navi3DDirect.y > -0.35f)
            {
                helper.Navi3DDirect.y += upNudge;
                helper.Navi3DDirect = helper.Navi3DDirect.normalized;
            }

            // We must make the controls local to the cab to insure predictable performance
            Vector3 ForwardsLocal = root.InverseTransformDirection(helper.Navi3DDirect);
            Vector3 turnVal = AIGlobals.LookRot(ForwardsLocal, Vector3.up).eulerAngles;
            Vector3 UpLocal = root.InverseTransformDirection(helper.Navi3DUp);
            Vector3 turnValUp = AIGlobals.LookRot(Vector3.forward, UpLocal).eulerAngles;
            //Vector3 forwardFlat = tank.rootBlockTrans.forward;
            //forwardFlat.y = 0;

            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " steering RAW" + turnVal);

            //Convert turnVal to runnable format
            // PITCH
            turnVal.x = Mathf.Clamp(-(AIGlobals.AngleUnsignedToSigned(turnVal.x) / pilot.FlyingChillFactor.x), -1, 1);
            // YAW
            turnVal.y = Mathf.Clamp(-(AIGlobals.AngleUnsignedToSigned(turnVal.y) / pilot.FlyingChillFactor.y), -1, 1);
            // ROLL
            turnValUp.z = Mathf.Clamp(-(AIGlobals.AngleUnsignedToSigned(turnValUp.z) / pilot.FlyingChillFactor.z), -1, 1);

            // Control oversteer since there's no proper control limiter for overyaw
            if (BankOnly)
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

            //Lock yaw AND limit roll when pitch operation is OUTSTANDING
            if (Mathf.Abs(turnVal.x) > 0.9f && EmergencyUp)
            {
                turnVal.y = 0;
                turnValUp.z = Mathf.Clamp(turnValUp.z, -0.25f, 0.25f);
            }


            //helper.Navi3DDirect = (position - tank.boundsCentreWorldNoCheck).normalized;

            if (tank.rootBlockTrans.up.y < 0)
            {   // upside down due to a unfindable oversight in code - just override the bloody thing when it happens
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + "  IS UPSIDE DOWN AND IS TRYING TO GET UPRIGHT");

                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " steering" + turnVal);
                //turnVal.z = -Mathf.Clamp(turnVal.z * 10, -1, 1);
            }

            //Turn our work in to process
            turnVal.z = turnValUp.z;
            Vector3 TurnVal = turnVal.Clamp01Box();

            // DRIVE
            Vector3 DriveVar = Vector3.forward * pilot.CurrentThrottle;

            //Turn our work in to processing
            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " steering" + turnVal);
            Vector3 DriveVal = DriveVar.Clamp01Box();

            /*
            if (Mathf.Abs(TurnVal.x) + Mathf.Abs(TurnVal.y) + Mathf.Abs(TurnVal.z) > 2f)
            {   // Controls saturated, for some reason when two turning inputs are maxed, the third stops doing anything
                //  We must ignore our WEAKEST input to keep control!
                int lowest = 0;
                float most = 2;
                for (int i = 0; i < 3; i++)
                {
                    float val = TurnVal[i];
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
            */

            //if (pilot.SlowestPropLerpSpeed < 0.1f && pilot.PropBias.z > 0.75f && pilot.CurrentThrottle > 0.75f)
            //    control3D.m_State.m_BoostProps = true;
            //else
            //    control3D.m_State.m_BoostProps = false;

            // Blue is the target destination, Red is up  

            // DEBUG FOR DRIVE ERRORS

            if (DebugRawTechSpawner.ShowDebugFeedBack)
            {
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, destPos - tank.boundsCentreWorldNoCheck, new Color(0, 1, 1)); //TEAL
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, helper.Navi3DDirect * pilot.Helper.lastTechExtents * 3, new Color(0, 0, 1));//BLUE
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 2, helper.Navi3DUp * pilot.Helper.lastTechExtents * 3, new Color(1, 0, 0));//RED
            }
            // We never drive backwards, so we do not need to correct that
            helper.ProcessControl(DriveVal, TurnVal, Vector3.zero, false, false);
            return;
        }
        public void AdviseThrottle(AIControllerAir pilot, TankAIHelper helper, Tank tank, Vector3 target)
        {
            if (pilot.AdvisedThrottle == -1)
            {
                if (tank.rbody.IsNotNull())
                {
                    if (helper.LocalSafeVelocity.z > AIGlobals.AirStallSpeed)
                    {
                        float ExtAvoid = helper.AutoSpacing;
                        if (helper.lastPlayer.IsNotNull())
                            ExtAvoid = helper.lastPlayer.GetCheapBounds();
                        float Extremes = ExtAvoid + helper.lastTechExtents + AIGlobals.PathfindingExtraSpace;
                        float throttleToSet = 1;
                        float foreTarg = tank.rootBlockTrans.InverseTransformPoint(target).z;

                        if (foreTarg > 0)
                            throttleToSet = (foreTarg - Extremes) / pilot.PropLerpValue;
                        pilot.AdvisedThrottle = Mathf.Clamp(throttleToSet, 0, 1);

                        if (!pilot.LowerEngines)
                        {   // Save fuel for chasing the enemy
                            if (pilot.NoProps)
                            {
                                if (!pilot.ForcePitchUp && foreTarg > Extremes && helper.SafeVelocity.y > -10 && Vector3.Dot((target - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                    helper.FullBoost = true;
                                else
                                    helper.FullBoost = false;
                            }
                            else
                            {
                                if (!pilot.ForcePitchUp && throttleToSet > 1.25f && helper.SafeVelocity.y > -10 && Vector3.Dot((target - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                    helper.FullBoost = true;
                                else
                                    helper.FullBoost = false;
                            }
                        }
                        else
                            helper.FullBoost = false;
                        return;
                    }
                }
                pilot.AdvisedThrottle = 1;
            }
        }
        public void AdviseThrottleTarget(AIControllerAir pilot, TankAIHelper helper, Tank tank, Visible target)
        {
            if (pilot.AdvisedThrottle == -1)
            {
                if (tank.rbody.IsNotNull())
                {
                    if (helper.LocalSafeVelocity.z > AIGlobals.AirStallSpeed)
                    {
                        float throttleToSet = 1;
                        float foreTarg = tank.rootBlockTrans.InverseTransformPoint(target.tank.boundsCentreWorldNoCheck).z;
                        float Extremes = target.GetCheapBounds() + helper.lastTechExtents + 5;
                        if (foreTarg > 0)
                            throttleToSet = (foreTarg - Extremes) / pilot.PropLerpValue;
                        //DebugTAC_AI.Log(KickStart.ModID + ": throttle " + throttleToSet + " | position offset enemy " + foreTarg);
                        pilot.AdvisedThrottle = Mathf.Clamp(throttleToSet, 0, 1);

                        if (pilot.NoProps)
                        {
                            if (!pilot.ForcePitchUp && foreTarg > Extremes && helper.SafeVelocity.y > -10 && Vector3.Dot((target.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                helper.FullBoost = true;
                            else
                                helper.FullBoost = false;
                        }
                        else
                        {
                            if (!pilot.ForcePitchUp && throttleToSet > 1.25f && helper.LocalSafeVelocity.y > -10 && Vector3.Dot((target.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6)
                                helper.FullBoost = true;
                            else
                                helper.FullBoost = false;
                        }
                        return;
                    }
                    //else
                    //DebugTAC_AI.Log(KickStart.ModID + ": not fast enough, velocity" +  helper.LocalSafeVelocity.z + " vs " + AIControllerAir.Stallspeed);
                }
                pilot.AdvisedThrottle = 1;
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": throttle is already " + pilot.AdvisedThrottle);
        }

        public Vector3 TryGetVelocityOffset(Tank tank, AIControllerAir pilot)
        {
            if (tank.rbody.IsNotNull())
                return tank.boundsCentreWorldNoCheck + (pilot.Helper.SafeVelocity * pilot.AerofoilSluggishness);
            return tank.boundsCentreWorldNoCheck;
        }

        public void PreventCollisionWithGround(AIControllerAir pilot, float groundOffset, bool unresponsiveAir)
        {
            float groundOffsetF = groundOffset; //pilot.AerofoilSluggishness
            if (unresponsiveAir)
            {
                if (!AIEPathing.AboveHeightFromGround(pilot.Helper.DodgeSphereCenter, groundOffsetF + pilot.Helper.lastTechExtents))
                {
                    //DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(deltaAim), "PreventCollisionWithGround called while height is too high");
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " -  deltaMovementClock = " + pilot.deltaMovementClock + " | slugishness = " + pilot.AerofoilSluggishness + " | deltaAim y " + deltaAim.y + " | vs " + groundOffsetF);
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " - GOING UP (HVY)");
                    pilot.ForcePitchUp = true;
                    pilot.PathPointSet.y = pilot.Helper.tank.boundsCentreWorldNoCheck.y;
                    pilot.PathPointSet += Vector3.up * (pilot.PathPointSet - pilot.Helper.tank.boundsCentreWorldNoCheck).ToVector2XZ().magnitude * 4;
                }
            }
            else
            {
                if (!AIEPathing.AboveHeightFromGround(pilot.Helper.DodgeSphereCenter, groundOffsetF))
                {
                    //DebugTAC_AI.Assert(!AIEPathing.IsUnderMaxAltPlayer(deltaAim), "PreventCollisionWithGround called while height is too high");
                    pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, pilot.Helper);
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " -  deltaMovementClock = " + pilot.deltaMovementClock.y + " | slugishness = " + pilot.AerofoilSluggishness + " | deltaAim y " + deltaAim.y + " | vs " + groundOffsetF + 
                    //    " | tech: " + pilot.Helper.tank.trans.position);
                    //DebugTAC_AI.Log(pilot.Helper.tank.name + " - GOING UP");
                    pilot.ForcePitchUp = true;
                    pilot.PathPointSet.y = pilot.Helper.tank.boundsCentreWorldNoCheck.y;
                    pilot.PathPointSet += Vector3.up * (pilot.PathPointSet - pilot.Helper.tank.boundsCentreWorldNoCheck).ToVector2XZ().magnitude * 4;
                }
            }
        }


        public Vector3 GetExactRightAlignedWorld(Tank tank, bool useLegacy)
        {
            if (useLegacy)
            {
                //return GetExactRightAlignedWorldLegacy(tank);
            }

            Vector3 right;
            if (tank.rootBlockTrans.forward.y >= -0.8f && tank.rootBlockTrans.forward.y <= 0.8f)
            {
                right = Vector3.Cross(Vector3.up, tank.rootBlockTrans.forward.SetY(0).normalized).SetY(0).normalized;
                if (DebugRawTechSpawner.ShowDebugFeedBack)
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 7, right * 24, new Color(1, 1, 0, 1));
                return right;
            }
            else
            {
                return GetExactRightAlignedWorldLegacy(tank);
                /*
                if (tank.rootBlockTrans.up.y > 0)
                {
                    right = -Vector3.Cross(Vector3.up, tank.rootBlockTrans.forward.SetY(0).normalized).SetY(0).normalized;
                    if (DebugRawTechSpawner.ShowDebugFeedBack)
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 7, right * 24, new Color(1, 1, 0, 1));
                    return right;
                }
                else
                {
                    right = Vector3.Cross(Vector3.up, tank.rootBlockTrans.forward.SetY(0).normalized).SetY(0).normalized;
                    if (DebugRawTechSpawner.ShowDebugFeedBack)
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 7, right * 24, new Color(1, 1, 0, 1));
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
            if (DebugRawTechSpawner.ShowDebugFeedBack)
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 7, rFlat * 24, new Color(1, 1, 0, 1));
            return rFlat;
        }
    }
}
