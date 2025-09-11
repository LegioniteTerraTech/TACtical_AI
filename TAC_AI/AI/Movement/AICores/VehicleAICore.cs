using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement.AICores
{
    /// <summary>
    /// [OBSOLETE] Handles both Wheeled and Space AI Directors and Maintainers
    /// </summary>
    internal class VehicleAICore : IMovementAICore
    {
        private static FieldInfo controlGet => VehicleUtils.controlGet;
        private AIControllerDefault controller;
        private Tank tank;
        public float GetDrive => 0;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerDefault) controller;
            this.controller.WaterPathing = WaterPathing.AvoidWater;
            this.tank = tank;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GroundOffsetGeneralAir;
            DebugTAC_AI.Log(KickStart.ModID + ": VehicleAICore - Init");

            if (controller.Helper.AutoAnchor)
            {
                if (tank.IsAnchored && !controller.Helper.PlayerAllowAutoAnchoring)
                    DebugTAC_AI.Log(KickStart.ModID + ": VehicleAICore - Should NOT be active when anchored UNLESS we have autoAnchor! StaticAICore should be in control!");
            }
            else if (tank.IsAnchored)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": VehicleAICore - Should NOT be active when anchored UNLESS we have autoAnchor! StaticAICore should be in control!");
            }
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            throw new NotImplementedException();
        }

        public bool DriveDirectorRTS(ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTargetRTS(controller, out Vector3 Target, ref core))
                return false;

            var helper = controller.Helper;
            if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, helper);
            else if (helper.DriverType == AIDriverType.Sailor)
                Target = AIEPathing.OffsetToSea(Target, tank, helper);
            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, helper);
            core.lastDestination = controller.PathPoint;

            // Planned pathing
            if (!helper.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
        }

        public bool DriveDirectorEnemyRTS(EnemyMind mind, ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTargetRTSEnemy(controller, out Vector3 Target, ref core))
                return false;

            var helper = controller.Helper;
            if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, helper);
            else if (helper.DriverType == AIDriverType.Sailor)
                Target = AIEPathing.OffsetToSea(Target, tank, helper);
            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, helper);
            core.lastDestination = controller.PathPoint;

            // Planned pathing
            if (!helper.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
        }


        public bool PlanningPathing(Vector3 Target, EDrivePathing aim)
        {
            if (!AIEAutoPather.IsFarEnough(tank.boundsCentreWorldNoCheck, Target))
                return false;
            var helper = controller.Helper;
            switch (helper.DriverType)
            {
                case AIDriverType.Null:
                    return false; // NULL
                case AIDriverType.AutoSet:
                    return false; // It's still thinking.  This should not be the case though...
                case AIDriverType.Tank:
                    controller.WaterPathing = WaterPathing.AvoidWater;
                    break;
                case AIDriverType.Sailor:
                    controller.WaterPathing = WaterPathing.StayInWater;
                    break;
                case AIDriverType.Pilot:
                case AIDriverType.Astronaut:
                    controller.WaterPathing = WaterPathing.AllowWater;
                    return false; // UNSUPPORTED for now
                case AIDriverType.Stationary:
                    return false; // Bases do not need planned pathing.
            }
            float pathSuccessMulti = 1;
            switch (aim)
            {
                case EDrivePathing.IgnoreAll:
                case EDrivePathing.OnlyImmedeate:
                    controller.SetAutoPathfinding(false);
                    return false;
                case EDrivePathing.Path:
                    pathSuccessMulti = AIGlobals.AIPathingSuccessRad;
                    break;
                case EDrivePathing.PrecisePath:
                    pathSuccessMulti = AIGlobals.AIPathingSuccessRadPrecise;
                    break;
            }
            if (!controller.AutoPathfind)
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - Started pathfinding!");

            controller.TargetDestination = WorldPosition.FromScenePosition(Target);
            controller.SetAutoPathfinding(true);
            if (controller.PathPlanned.Count > 0)
            {
                helper.AutoSpacing = 0; // Drive DIRECTLY to target
                if (helper.DriverType == AIDriverType.Sailor)
                    controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                else
                    controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                if ((controller.PathPoint - tank.boundsCentreWorldNoCheck).WithinSquareXZ(tank.GetCheapBounds() * pathSuccessMulti))
                {
                    controller.PathPlanned.Dequeue(); // Next position!
                    DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - finished pathing to " + controller.PathPoint);
                    if (controller.PathPlanned.Count == 0)
                    {
                        DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - All Done!");
                        return false;
                    }
                    if (helper.DriverType == AIDriverType.Sailor)
                        controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                    else
                        controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                }
                switch (aim)
                {
                    case EDrivePathing.Path:
                        controller.PathPointSet = helper.AvoidAssist(controller.PathPoint);
                        break;
                    case EDrivePathing.PrecisePath:
                        controller.PathPointSet = helper.AvoidAssistPrecise(controller.PathPoint);
                        break;
                }
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint + " of waypoints left " + controller.PathPlanned.Count);

                return true;
            }
            return false;
        }
        public bool ImmedeatePathing(Vector3 Target, EDrivePathing aim)
        {
            var helper = controller.Helper;

            switch (aim)
            {
                case EDrivePathing.IgnoreAll:
                    controller.PathPointSet = Target;
                    return true;
                case EDrivePathing.OnlyImmedeate:
                    break;
                case EDrivePathing.Path:
                    Target = helper.AvoidAssist(Target);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = helper.AvoidAssistPrecise(Target);
                    break;
            }

            if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, helper);
            else if (helper.DriverType == AIDriverType.Sailor)
                Target = AIEPathing.OffsetToSea(Target, tank, helper);

            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, helper);
            DebugTAC_AI.LogPathing(tank.name + ": ImmedeatePathing - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint);
            return true;
        }

        public bool DriveDirector(ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTarget(controller, out Vector3 Target, ref core))
                return false;

            var helper = controller.Helper;
            if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, helper);
            else if (helper.DriverType == AIDriverType.Sailor)
                Target = AIEPathing.OffsetToSea(Target, tank, helper);
            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, helper);

            // Planned pathing
            if (!helper.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
        }


        public bool PlanningPathingEnemy(EnemyMind mind, Vector3 Target, EDrivePathing aim)
        {
            if (!AIEAutoPather.IsFarEnough(tank.boundsCentreWorldNoCheck, Target))
                return false;
            var helper = controller.Helper;
            switch (mind.EvilCommander)
            {
                case EnemyHandling.Wheeled:
                    controller.WaterPathing = WaterPathing.AvoidWater;
                    break;
                case EnemyHandling.Naval:
                    controller.WaterPathing = WaterPathing.StayInWater;
                    break;
                case EnemyHandling.Chopper:
                case EnemyHandling.Airplane:
                case EnemyHandling.Starship:
                case EnemyHandling.SuicideMissile:
                    controller.WaterPathing = WaterPathing.AllowWater;
                    return false; // UNSUPPORTED for now
                case EnemyHandling.Stationary:
                    return false; // Bases do not need planned pathing.
                default:
                    break;
            }
            float pathSuccessMulti = 1;
            switch (aim)
            {
                case EDrivePathing.IgnoreAll:
                case EDrivePathing.OnlyImmedeate:
                    controller.SetAutoPathfinding(false);
                    return false;
                case EDrivePathing.Path:
                    pathSuccessMulti = AIGlobals.AIPathingSuccessRad;
                    break;
                case EDrivePathing.PrecisePath:
                    pathSuccessMulti = AIGlobals.AIPathingSuccessRadPrecise;
                    break;
            }
            if (!controller.AutoPathfind)
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathingEnemy - Started pathfinding!");

            controller.TargetDestination = WorldPosition.FromScenePosition(Target);
            controller.SetAutoPathfinding(true);
            if (controller.PathPlanned.Count > 0)
            {
                helper.AutoSpacing = 0; // Drive DIRECTLY to target
                if (helper.DriverType == AIDriverType.Sailor)
                    controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                else
                    controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                if ((controller.PathPoint - tank.boundsCentreWorldNoCheck).WithinSquareXZ(tank.GetCheapBounds() * pathSuccessMulti))
                {
                    controller.PathPlanned.Dequeue(); // Next position!
                    DebugTAC_AI.LogPathing(tank.name + ": PlanningPathingEnemy - finished pathing to " + controller.PathPoint);
                    if (controller.PathPlanned.Count == 0)
                    {
                        DebugTAC_AI.LogPathing(tank.name + ": PlanningPathingEnemy - All Done!");
                        return false;
                    }
                    if (helper.DriverType == AIDriverType.Sailor)
                        controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                    else
                        controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                }
                switch (aim)
                {
                    case EDrivePathing.Path:
                        controller.PathPointSet = helper.AvoidAssist(controller.PathPoint);
                        break;
                    case EDrivePathing.PrecisePath:
                        controller.PathPointSet = helper.AvoidAssistPrecise(controller.PathPoint);
                        break;
                }
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathingEnemy - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint + " of waypoints left " + controller.PathPlanned.Count);
                return true;
            }
            return false;
        }
        public bool ImmedeatePathingEnemy(EnemyMind mind, Vector3 Target, EDrivePathing aim)
        {
            var helper = controller.Helper;

            switch (aim)
            {
                case EDrivePathing.IgnoreAll:
                    controller.PathPointSet = Target;
                    return true;
                case EDrivePathing.OnlyImmedeate:
                    break;
                case EDrivePathing.Path:
                    Target = helper.AvoidAssist(Target);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = helper.AvoidAssistPrecise(Target);
                    break;
            }
            switch (mind.EvilCommander)
            {
                case EnemyHandling.Naval:
                    Target = AIEPathing.OffsetToSea(Target, tank, helper);
                    break;
                case EnemyHandling.Starship:
                    Target = AIEPathing.OffsetFromGroundH(Target, helper);
                    break;
                default:
                    Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);
                    if (mind.EvilCommander == EnemyHandling.Wheeled)
                        Target = AIEPathing.OffsetFromSea(Target, tank, helper);
                    break;
            }

            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, helper);
            //core.lastDestination = controller.ProcessedDest;
            DebugTAC_AI.LogPathing(tank.name + ": ImmedeatePathingEnemy - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint);
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind, ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTargetEnemy(controller, out Vector3 Target, ref core))
            {
                throw new NullReferenceException("DriveDirectorEnemy expects a valid EnemyMind but IT WAS NULL");
            }

            var helper = controller.Helper;
            switch (mind.EvilCommander)
            {
                case EnemyHandling.Naval:
                    Target = AIEPathing.OffsetToSea(Target, tank, helper);
                    break;
                case EnemyHandling.Starship:
                    Target = AIEPathing.OffsetFromGroundH(Target, helper);
                    break;
                default:
                    Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);
                    if (mind.EvilCommander == EnemyHandling.Wheeled)
                        Target = AIEPathing.OffsetFromSea(Target, tank, helper);
                    break;
            }
            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, helper);

            // Planned pathing
            if (!helper.Attempt3DNavi && PlanningPathingEnemy(mind, Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathingEnemy(mind, Target, core.DrivePathing);
        }



        public bool DriveMaintainer(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
            // DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " normal drive was called");
            if (helper.Attempt3DNavi)
            {
                //3D movement
                SpaceMaintainer(ref core);
            }
            else //Land movement
            {
                helper.UpdateVanillaAvoidence();
                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

                control3D.m_State.m_InputRotation = Vector3.zero;
                control3D.m_State.m_InputMovement = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);
                Vector3 destDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;

                //DebugTAC_AI.Log("IS target player " + Singleton.playerTank == helper.lastEnemy + " | MinimumRad " + helper.MinimumRad + " | destination" + controller.PathPoint);
                float DriveControl = 0;
                if (helper.DoSteerCore)
                {
                    if (core.DriveDest == EDriveDest.FromLastDestination)
                    {   //Move from target
                        if (core.DriveDir == EDriveFacing.Backwards)//EDriveType.Backwards
                        {   // Face back TOWARDS target
                            VehicleUtils.Turner(helper, -destDirect, 0, ref core);
                            DriveControl = 1f;
                            //DebugTAC_AI.Log("Forwards looking away from target");
                        }
                        else if (core.DriveDir == EDriveFacing.Perpendicular)
                        {   // Still proceed away from target
                            VehicleUtils.Turner(helper, destDirect, 0, ref core);
                            DriveControl = 1f;
                            //DebugTAC_AI.Log("Sideways at target");
                        }
                        else
                        {   // Face front TOWARDS target
                            VehicleUtils.Turner(helper, destDirect, 0, ref core);
                            DriveControl = -1f;
                            //DebugTAC_AI.Log("Reverse looking at target");
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
                        DriveControl = 1f;
                    }
                    /*
                    else if (helper.DriveDir == EDriveType.Backwards) // Disabled for now as most designs are forwards-facing
                    {   //Drive to target driving backwards  
                        //if (helper.PivotOnly)
                        //    thisControl.m_Movement.FaceDirection(tank, destDirect, 1);//need max aiming strength for turning
                        //else
                        if (Turner(thisControl, helper, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);//Face the music
                        DriveControl = -1f;
                    }*/
                    else
                    {
                        //if (helper.PivotOnly)
                        //    thisControl.m_Movement.FacePosition(tank, controller.PathPoint, 1);// need max aiming strength for turning
                        //else
                        VehicleUtils.Turner(helper, destDirect, 0, ref core);//Face the music
                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  driving to " + controller.PathPoint);
                        if (helper.AutoSpacing > 0)
                        {
                            //if (helper.DriveDir == EDriveType.Perpendicular)
                            //    DriveControl = 1f;
                            float range = helper.lastOperatorRange;
                            if (core.DriveDir <= EDriveFacing.Neutral)
                                DriveControl = 0f;
                            else if (range < helper.AutoSpacing - 1)
                            {
                                if (core.DriveDir == EDriveFacing.Forwards)
                                    DriveControl = -1f;
                                else if (core.DriveDir == EDriveFacing.Backwards)
                                    DriveControl = 1f;
                                else
                                    DriveControl = 0;

                            }
                            else if (range > helper.AutoSpacing + 1)
                            {
                                if (core.DriveDir == EDriveFacing.Forwards)
                                    DriveControl = 1f;
                                else if (core.DriveDir == EDriveFacing.Backwards)
                                    DriveControl = -1f;
                                else
                                    DriveControl = 1f;
                            }
                        }
                    }
                }
                else
                    DriveControl = 0;

                // Overrides to translational drive
                if (core.DriveDir == EDriveFacing.Stop)
                {   // STOP
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
                        DriveControl = 0;
                        break;
                    case AIThrottleState.Yield:
                        if (core.DriveDir == EDriveFacing.Backwards)
                        {
                            if (helper.recentSpeed > 10)
                                DriveControl = 0.2f;
                            else
                                DriveControl = -1f;
                        }
                        else
                        {   // works with forwards
                            if (helper.recentSpeed > 10)
                                DriveControl = -0.2f;
                            else
                                DriveControl = 1f;
                        }
                        break;
                    case AIThrottleState.FullSpeed:
                        if (helper.FullBoost)
                        {
                            DriveControl = 1;
                            if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                                controller.TryBoost();
                        }
                        else if (helper.LightBoost)
                        {
                            if (helper.LightBoostFeatheringClock >= 25)
                            {
                                if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.8f)
                                    controller.TryBoost();
                                helper.LightBoostFeatheringClock = 0;
                            }
                            helper.LightBoostFeatheringClock++;
                        }
                        break;
                    case AIThrottleState.ForceSpeed:
                        DriveControl = helper.DriveVar;
                        if (helper.FullBoost)
                        {
                            DriveControl = 1;
                            if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                                controller.TryBoost();
                        }
                        else if (helper.LightBoost)
                        {
                            if (helper.LightBoostFeatheringClock >= 25)
                            {
                                if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                                    controller.TryBoost();
                                helper.LightBoostFeatheringClock = 0;
                            }
                            helper.LightBoostFeatheringClock++;
                        }
                        break;
                    default:
                        break;
                }

                if (helper.FirePROPS)
                    helper.MaxProps();
                helper.DriveControl = DriveControl;

                if (AIGlobals.ShowDebugFeedBack)
                {
                    // DEBUG FOR DRIVE ERRORS
                    if (!tank.IsAnchored)
                    {
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));
                        if (DriveControl != 0)
                            DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, tank.rootBlockTrans.forward * (helper.lastTechExtents + (DriveControl * helper.lastTechExtents)), new Color(0, 0, 1));
                        if (helper.FullBoost || helper.LightBoost)
                            DebugExtUtilities.DrawDirIndicator(tank.gameObject, 2, tank.rootBlockTrans.TransformDirection(controller.BoostBias) * helper.lastTechExtents, new Color(1, 0, 0));
                    }
                    else if (helper.AttackEnemy && helper.lastEnemyGet)
                    {
                        if (ManBaseTeams.IsEnemy(tank.Team, helper.lastEnemyGet.tank.Team))
                            DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, helper.lastEnemyGet.centrePosition - tank.trans.position, new Color(0, 1, 1));
                    }
                }
            }
            return true;
        }

        public void SpaceMaintainer( ref EControlCoreSet core)
        {
            TankAIHelper helper = controller.Helper;
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

            float driveMultiplier = 0;

            //AI Steering Rotational
            Vector3 distDiff = controller.PathPoint - tank.boundsCentreWorldNoCheck;
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
            forwardFlat = forwardFlat.normalized;
            if (helper.Navi3DDirect == Vector3.zero)
            {   //keep upright!
                if (core.DriveDir == EDriveFacing.Backwards)
                    turnVal = AIGlobals.LookRot(tank.rootBlockTrans.InverseTransformDirection(-forwardFlat.normalized), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
                else
                    turnVal = AIGlobals.LookRot(tank.rootBlockTrans.InverseTransformDirection(forwardFlat.normalized), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;

                //Convert turnVal to runnable format
                turnVal.x = -AIGlobals.AngleUnsignedToSigned(turnVal.x) / 180f;

                turnVal.z = -AIGlobals.AngleUnsignedToSigned(turnVal.z) / 180f;

                turnVal.y = 0;

                //DebugTAC_AI.Log(KickStart.ModID + ": TurnVal UP " + turnVal);
            }
            else
            {   //for special cases we want to angle at the enemy
                if (core.DriveDir == EDriveFacing.Backwards)
                    turnVal = AIGlobals.LookRot(tank.rootBlockTrans.InverseTransformDirection(-helper.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(helper.Navi3DUp)).eulerAngles;
                else
                    turnVal = AIGlobals.LookRot(tank.rootBlockTrans.InverseTransformDirection(helper.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(helper.Navi3DUp)).eulerAngles;

                Vector3 turnValUp = AIGlobals.LookRot(tank.rootBlockTrans.InverseTransformDirection(forwardFlat.normalized), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
                if (helper.Navi3DUp == Vector3.up)
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": Forwards");
                    if (!helper.FullMelee && Vector3.Dot(helper.Navi3DDirect, tank.rootBlockTrans.forward) < 0.6f)
                    {
                        //If overtilt then try get back upright again
                        turnVal.x = turnValUp.x;
                        turnVal.x = -AIGlobals.AngleUnsignedToSigned(turnVal.x) / 180f;
                    }
                    else
                    {
                        turnVal.x = Mathf.Clamp(-AIGlobals.AngleUnsignedToSigned(turnVal.x) / 60f, -1, 1);
                    }
                    turnVal.z = turnValUp.z;
                    turnVal.z = -AIGlobals.AngleUnsignedToSigned(turnVal.z) / 180f;
                }
                else
                {   //Using broadside tilting
                    if (!helper.FullMelee && Vector3.Dot(helper.Navi3DUp, tank.rootBlockTrans.up) < 0.6f)
                    {
                        //If overtilt then try get back upright again
                        turnVal.z = turnValUp.z;
                        turnVal.z = -AIGlobals.AngleUnsignedToSigned(turnVal.z) / 180f;
                        //DebugTAC_AI.Log(KickStart.ModID + ": Broadside overloaded with value " + Vector3.Dot(helper.Navi3DUp, tank.rootBlockTrans.up));
                    }
                    else
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Broadside Z-tilt active");
                        turnVal.z = Mathf.Clamp(-AIGlobals.AngleUnsignedToSigned(turnVal.x) / 60f, -1, 1);
                    }
                    turnVal.x = turnValUp.x;
                    turnVal.x = Mathf.Clamp(-AIGlobals.AngleUnsignedToSigned(turnVal.x) / 60f, -1, 1);
                }

                //Convert turnVal to runnable format
                turnVal.y = Mathf.Clamp(-AIGlobals.AngleUnsignedToSigned(turnVal.y) / 60f, -1, 1);

                //DebugTAC_AI.Log(KickStart.ModID + ": TurnVal AIM " + turnVal);
            }

            helper.Navi3DDirect = Vector3.zero;
            helper.Navi3DUp = Vector3.up;
            if (helper.DoSteerCore)
            {
                float turnValF;
                if (helper.AdviseAwayCore)
                {   //Move from target
                    if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Broadside the enemy
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
                        if (helper.lastEnemyGet.IsNotNull())
                        {
                            helper.Navi3DDirect = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            VehicleUtils.TurnerHovership(tank.control, helper, distDiff, ref core);
                        }
                        // Disabled for now as most spaceships in the pop do not have broadsides.
                        /*
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        if (helper.lastEnemy.IsNotNull())
                        {
                            if (Vector3.Dot(helper.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                            {
                                helper.Navi3DDirect = Vector3.Cross(Vector3.up, (helper.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                helper.Navi3DUp = Vector3.Cross((helper.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, helper.Navi3DDirect).normalized;
                                //DebugTAC_AI.Log(KickStart.ModID + ": Broadside Left A  up is " + helper.Navi3DUp);
                            }
                            else
                            {
                                helper.Navi3DDirect = Vector3.Cross((helper.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, Vector3.up).normalized;
                                helper.Navi3DUp = Vector3.Cross(helper.Navi3DDirect, (helper.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                //DebugTAC_AI.Log(KickStart.ModID + ": Broadside Right A  up is " + helper.Navi3DUp);
                            }
                        }
                        else
                        {
                            //helper.Navi3DDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, controller.PathPoint, 1);
                        }*/
                    }
                    else if (core.DriveDir == EDriveFacing.Forwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
                        if (helper.lastEnemyGet.IsNotNull())
                        {
                            helper.Navi3DDirect = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            VehicleUtils.TurnerHovership(tank.control, helper, distDiff, ref core);
                        }
                    }
                    else if (core.DriveDir == EDriveFacing.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        VehicleUtils.TurnerHovership(tank.control, helper, -distDiff, ref core);
                    }
                    else
                    {
                        control3D.m_State.m_InputRotation.y = 0;
                    }
                }
                else
                {
                    if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Broadside the enemy
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        if (helper.lastEnemyGet.IsNotNull())
                        {
                            if (Vector3.Dot(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                            {
                                helper.Navi3DDirect = Vector3.Cross(Vector3.up, (helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                helper.Navi3DUp = Vector3.Cross((helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, helper.Navi3DDirect).normalized;
                                //DebugTAC_AI.Log(KickStart.ModID + ": Broadside Left  up is " + helper.Navi3DUp);
                            }
                            else
                            {
                                helper.Navi3DDirect = Vector3.Cross((helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, Vector3.up).normalized;
                                helper.Navi3DUp = Vector3.Cross(helper.Navi3DDirect, (helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                //DebugTAC_AI.Log(KickStart.ModID + ": Broadside Right  up is " + helper.Navi3DUp);
                            }
                        }
                        else
                        {
                            //helper.Navi3DDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;
                            VehicleUtils.TurnerHovership(tank.control, helper, distDiff, ref core);
                        }
                    }
                    else if (core.DriveDir == EDriveFacing.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        VehicleUtils.TurnerHovership(tank.control, helper, -distDiff, ref core);
                    }
                    else if (core.DriveDir == EDriveFacing.Forwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        if (helper.lastEnemyGet.IsNotNull())
                        {
                            helper.Navi3DDirect = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            //helper.Navi3DDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;
                            VehicleUtils.TurnerHovership(tank.control, helper, distDiff, ref core);
                        }
                    }
                    else
                    {   //Forwards follow but no pitch controls
                        control3D.m_State.m_InputRotation = (turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.rootBlockTrans.forward), 0, 1)).Clamp01Box();
                        VehicleUtils.TurnerHovership(tank.control, helper, distDiff, ref core);
                    }
                }
            }
            else
                control3D.m_State.m_InputRotation = Vector3.zero;

            //AI Drive Translational
            Vector3 driveVal;
            //DebugTAC_AI.Log(KickStart.ModID + ": VehicleAICore for " + tank.name + " | " + helper.GetCoreControlString());
            if (helper.AdviseAwayCore)
            {   //Move from target
                if (helper.lastEnemyGet.IsNotNull() && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck.y))
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": REVEREEE");
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(InvertHorizontalPlane(distDiff.normalized * 150)));
                    if (helper.AIAlign == AIAlignment.Player && helper.lastPlayer.IsNotNull())
                    {
                        // Keep below a certain height in relation to player so that they may command if need be
                        if (helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (helper.MaxCombatRange / 3) < helper.tank.boundsCentreWorldNoCheck.y)
                            driveVal.y = -1;
                    }
                    else
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": leveling");
                        float enemyOffsetH = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck.y + helper.lastEnemyGet.tank.GetCheapBounds() + helper.GroundOffsetHeight;
                        float leveler = Mathf.Clamp((enemyOffsetH - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
                        if (leveler > -0.25f)
                            driveVal.y = leveler;
                        else
                            driveVal.y = -1f;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": REVEREEE2");
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(InvertHorizontalPlane(distDiff.normalized * 150)));
                }
                driveMultiplier = 1f;
            }
            else
            {
                if (helper.lastEnemyGet.IsNotNull() && !helper.IsMultiTech && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck.y))
                {   //level alt with enemy
                    //DebugTAC_AI.Log(KickStart.ModID + ": FWD");
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(distDiff));
                    if (helper.AIAlign == AIAlignment.Player && helper.lastPlayer.IsNotNull())
                    {
                        // Keep below a certain height in relation to player so that they may command if need be
                        if (helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (helper.MaxCombatRange / 3) < helper.tank.boundsCentreWorldNoCheck.y)
                            driveVal.y = -1;
                    }
                    else
                    {
                        float enemyOffsetH = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck.y + helper.lastEnemyGet.tank.GetCheapBounds() + helper.GroundOffsetHeight;
                        float leveler = Mathf.Clamp((enemyOffsetH - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
                        if (leveler > -0.25f)
                            driveVal.y = leveler;
                        else
                            driveVal.y = -1f;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": FWD2");
                    float range = helper.lastOperatorRange;
                    if (range < helper.AutoSpacing - 1)
                    {
                        driveMultiplier = 1f;
                        driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(InvertHorizontalPlane(distDiff)) * 0.3f);
                    }
                    else if (range > helper.AutoSpacing + 1)
                    {
                        driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(distDiff));
                        if (core.DriveDir == EDriveFacing.Forwards || core.DriveDir == EDriveFacing.Backwards)
                            driveMultiplier = 1f;
                        else
                            driveMultiplier = 0.4f;
                    }
                    else
                        driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(distDiff));
                }
            }

            bool EmergencyUp = false;
            bool CloseToGroundWarning = false;
            // Multitechs do NOT use ground avoidence
            if (!helper.IsMultiTech)
            {
                float height = helper.GetFrameHeight();
                if (height > tank.boundsCentreWorldNoCheck.y - helper.lastTechExtents)
                {
                    EmergencyUp = true;
                    CloseToGroundWarning = true;
                }
                else if (height > tank.boundsCentreWorldNoCheck.y - (helper.lastTechExtents * 2))
                {
                    CloseToGroundWarning = true;
                }
            }

            if (CloseToGroundWarning)
            {
                if (driveVal.y >= -0.3f && driveVal.y < 0f)
                    driveVal.y = 0; // prevent airships from slam-dunk
                else if (driveVal.y != -1)
                {
                    driveVal.y += 0.5f;
                }
            }

            switch (helper.ThrottleState)
            {
                case AIThrottleState.PivotOnly:
                    driveVal.x = 0;
                    driveVal.z = 0;
                    break;
                case AIThrottleState.Yield:
                    // Supports all directions
                    if (helper.recentSpeed > 20)
                        driveMultiplier = -0.3f;
                    else
                        driveMultiplier = 0.3f;
                    break;
                case AIThrottleState.FullSpeed:
                    if (helper.FullBoost)
                    {
                        driveMultiplier = 1;
                        if (helper.IsMultiTech || Vector3.Dot(driveVal, tank.rootBlockTrans.forward) > 0.75f)
                            helper.MaxBoost();
                    }
                    else if (helper.LightBoost)
                    {
                        if (helper.LightBoostFeatheringClock >= 25)
                        {
                            if (helper.IsMultiTech || Vector3.Dot(driveVal, tank.rootBlockTrans.forward) > 0.75f)
                                helper.MaxBoost();
                            helper.LightBoostFeatheringClock = 0;
                        }
                        helper.LightBoostFeatheringClock++;
                    }
                    break;
                case AIThrottleState.ForceSpeed:
                    driveMultiplier = helper.DriveVar;
                    break;
                default:
                    break;
            }
            if (helper.FirePROPS)
                helper.MaxProps();

            // PREVENT GROUND CRASHING
            if (EmergencyUp)
            {
                control3D.m_State.m_InputMovement = (tank.rootBlockTrans.InverseTransformVector(Vector3.up) * 2).Clamp01Box();

                if (AIGlobals.ShowDebugFeedBack)
                {
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, driveVal * helper.lastTechExtents, new Color(0, 0, 1));
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, control3D.m_State.m_InputMovement * helper.lastTechExtents, new Color(1, 0, 0));
                }
                controlGet.SetValue(tank.control, control3D);
                return;
            }
            //helper.MinimumRad
            // Prevent drifting
            Vector3 final = (driveVal * Mathf.Clamp(distDiff.magnitude / 5, 0, 1) * driveMultiplier).Clamp01Box();
            final.x = final.x * AIGlobals.HovershipHorizontalDriveMulti;
            final.z = final.z * AIGlobals.HovershipHorizontalDriveMulti;
            if (final.y > 0)
                final.y = final.y * AIGlobals.HovershipUpDriveMulti;
            else
                final.y = final.y * AIGlobals.HovershipDownDriveMulti;

            if (core.DriveDir > EDriveFacing.Neutral)
            {
                if (final.y.Approximately(0, 0.4f))
                    final.y = 0;
                if (final.x.Approximately(0, 0.35f))
                    final.x = 0;
                if (final.z.Approximately(0, 0.35f))
                    final.z = 0;
            }
            control3D.m_State.m_InputMovement = final.Clamp01Box();

            if (AIGlobals.ShowDebugFeedBack)
            {
                // DEBUG FOR DRIVE ERRORS
                if (!tank.IsAnchored)
                {
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, distDiff, new Color(0, 1, 1));
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, tank.rootBlockTrans.TransformVector(driveVal * helper.lastTechExtents * 2), new Color(0, 0, 1)); // blue
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 2, tank.rootBlockTrans.TransformVector(control3D.m_State.m_InputMovement * helper.lastTechExtents * 2), new Color(1, 0, 0));
                }
                else if (helper.AttackEnemy && helper.lastEnemyGet)
                {
                    if (ManBaseTeams.IsEnemy(tank.Team, helper.lastEnemyGet.tank.Team))
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, helper.lastEnemyGet.centrePosition - tank.trans.position, new Color(0, 1, 1));
                }
            }
            controlGet.SetValue(tank.control, control3D);
        }

        public bool TryAdjustForCombat(bool between, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper helper = controller.Helper;
            bool output = false;
            if (helper.ChaseThreat && (!helper.IsDirectedMoving || !helper.Retreat) && helper.lastEnemyGet.IsNotNull())
            {
                Vector3 targPos = helper.InterceptTargetDriving(helper.lastEnemyGet);
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                helper.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((helper.lastCombatRange - helper.MinCombatRange) / 3f, -1, 1);
                if (helper.SideToThreat)
                {
                    core.DriveDir = EDriveFacing.Perpendicular;
                    if (helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = targPos;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        core.DriveDir = EDriveFacing.Backwards;
                        pos = helper.AvoidAssist(targPos);
                        //helper.MinimumRad = 0.5f;
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                    /*
                    else if (driveDyna < 0.5f)
                    {
                        helper.ThrottleState = AIThrottleState.PivotOnly;
                        pos = helper.AvoidAssist(targPos);
                        //helper.MinimumRad = 0.5f;
                        helper.MinimumRad = helper.lastTechExtents + helper.lastEnemy.GetCheapBounds() + 3;
                    }*/
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                }
                else
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    if (helper.FullMelee)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = targPos;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = 0.5f;
                    }
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                }
                if (between && helper.theResource?.tank)
                {
                    pos = Between(pos, helper.theResource.tank.boundsCentreWorldNoCheck);
                }
            }
            else
                helper.IgnoreEnemyDistance();
            return output;
        }

        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper helper = controller.Helper;
            bool output = false;
            if (!helper.Retreat && helper.lastEnemyGet.IsNotNull() && mind.CommanderMind != EnemyAttitude.OnRails)
            {
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                helper.UpdateEnemyDistance(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                float driveDyna = Mathf.Clamp((helper.lastCombatRange - mind.MinCombatRange) / 3f, -1, 1);

                if (mind.CommanderAttack == EAttackMode.Circle)
                {   // works fine for now
                    if (helper.SideToThreat)
                        core.DriveDir = EDriveFacing.Perpendicular;
                    else
                        core.DriveDir = EDriveFacing.Forwards;
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!;
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind);
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        //helper.MinimumRad = helper.lastTechExtents + helper.lastEnemy.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        //helper.MinimumRad = helper.lastTechExtents + helper.lastEnemy.GetCheapBounds() + 2;
                    }
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        //helper.MinimumRad = helper.lastTechExtents + helper.lastEnemy.GetCheapBounds() + 2;
                    }
                    //DebugTAC_AI.Log("DriveDyna is " + driveDyna);
                }
                else
                {   // Since the enemy also uses it's Operator in combat, this will have to listen to that
                    if (helper.IsDirectedMovingFromDest)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        helper.AutoSpacing = 0.5f;
                    }
                    else if (helper.IsDirectedMovingToDest && mind.LikelyMelee)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        if (mind.LikelyMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                            helper.AutoSpacing = 0.5f;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                            helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                        }
                    }
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                    }
                }
            }
            else
                helper.IgnoreEnemyDistance();
            return output;
        }


        public Vector3 Between(Vector3 Target, Vector3 other)
        {
            return (Target + other) / 2;
        }


        private const float throttleDampen = 0.5f;
        private const float DampeningStrength = 0.75f;
        public Vector3 InertiaTranslation(Vector3 direction)
        {
            Tank tank = controller.Tank;
            if (tank.rbody == null)
                return direction;

            EnemyMind mind = controller.EnemyMind;
            if (mind != null)
            {
                if (mind.CommanderSmarts >= EnemySmarts.Smrt)
                {
                    return direction + Vector3.ProjectOnPlane(-controller.Helper.LocalSafeVelocity * DampeningStrength, direction);
                }
            }
            else
            {
                if (controller.Helper.AdvancedAI)
                {
                    return direction + Vector3.ProjectOnPlane(-controller.Helper.LocalSafeVelocity * DampeningStrength, direction);
                }
            }
            return direction * throttleDampen;
        }
        public Vector3 InvertHorizontalPlane(Vector3 direction)
        {
            direction.x = -direction.x;
            direction.z = -direction.z;
            return direction;
        }
    }
}
