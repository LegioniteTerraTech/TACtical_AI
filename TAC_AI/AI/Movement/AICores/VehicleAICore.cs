using System;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using UnityEngine;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement.AICores
{
    /// <summary> [OBSOLETE] Handles both Wheeled and Space AI Directors & Maintainers </summary>
    public class VehicleAICore : IMovementAICore
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
        private AIControllerDefault controller;
        private Tank tank;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerDefault) controller;
            this.controller.WaterPathing = WaterPathing.AvoidWater;
            this.tank = tank;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GroundOffsetGeneralAir;
            DebugTAC_AI.Log("TACtical_AI: VehicleAICore - Init");

            if (controller.Helper.AutoAnchor)
            {
                if (tank.IsAnchored && !controller.Helper.PlayerAllowAutoAnchoring)
                    DebugTAC_AI.Log("TACtical_AI: VehicleAICore - Should NOT be active when anchored UNLESS we have autoAnchor! StaticAICore should be in control!");
            }
            else if (tank.IsAnchored)
            {
                DebugTAC_AI.Log("TACtical_AI: VehicleAICore - Should NOT be active when anchored UNLESS we have autoAnchor! StaticAICore should be in control!");
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

            var help = controller.Helper;
            if (help.Attempt3DNavi && !(help.FullMelee && help.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, help);
            else if (help.DriverType == AIDriverType.Sailor)
                Target = AIEPathing.OffsetToSea(Target, tank, help);
            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, help);
            core.lastDestination = controller.PathPoint;

            // Planned pathing
            if (!help.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
        }



        public bool PlanningPathing(Vector3 Target, EDrivePathing aim)
        {
            if (!AIEAutoPather.IsFarEnough(tank.boundsCentreWorldNoCheck, Target))
                return false;
            var help = controller.Helper;
            switch (help.DriverType)
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
                help.MinimumRad = 0; // Drive DIRECTLY to target
                if (help.DriverType == AIDriverType.Sailor)
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
                    if (help.DriverType == AIDriverType.Sailor)
                        controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                    else
                        controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                }
                switch (aim)
                {
                    case EDrivePathing.Path:
                        controller.PathPointSet = help.AvoidAssist(controller.PathPoint);
                        break;
                    case EDrivePathing.PrecisePath:
                        controller.PathPointSet = help.AvoidAssistPrecise(controller.PathPoint);
                        break;
                }
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint + " of waypoints left " + controller.PathPlanned.Count);

                return true;
            }
            return false;
        }
        public bool ImmedeatePathing(Vector3 Target, EDrivePathing aim)
        {
            var help = controller.Helper;

            switch (aim)
            {
                case EDrivePathing.IgnoreAll:
                    controller.PathPointSet = Target;
                    return true;
                case EDrivePathing.OnlyImmedeate:
                    break;
                case EDrivePathing.Path:
                    Target = help.AvoidAssist(Target);
                    break;
                case EDrivePathing.PathInv:
                    Target = help.AvoidAssistInv(Target);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = help.AvoidAssistPrecise(Target);
                    break;
            }

            if (help.Attempt3DNavi && !(help.FullMelee && help.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, help);
            else if (help.DriverType == AIDriverType.Sailor)
                Target = AIEPathing.OffsetToSea(Target, tank, help);

            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, help);
            DebugTAC_AI.LogPathing(tank.name + ": ImmedeatePathing - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint);
            return true;
        }

        public bool DriveDirector(ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTarget(controller, out Vector3 Target, ref core))
                return false;

            var help = controller.Helper;
            if (help.Attempt3DNavi && !(help.FullMelee && help.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, help);
            else if (help.DriverType == AIDriverType.Sailor)
                Target = AIEPathing.OffsetToSea(Target, tank, help);
            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, help);

            // Planned pathing
            if (!help.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
        }


        public bool PlanningPathingEnemy(EnemyMind mind, Vector3 Target, EDrivePathing aim)
        {
            if (!AIEAutoPather.IsFarEnough(tank.boundsCentreWorldNoCheck, Target))
                return false;
            var help = controller.Helper;
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
                help.MinimumRad = 0; // Drive DIRECTLY to target
                if (help.DriverType == AIDriverType.Sailor)
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
                    if (help.DriverType == AIDriverType.Sailor)
                        controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                    else
                        controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                }
                switch (aim)
                {
                    case EDrivePathing.Path:
                        controller.PathPointSet = help.AvoidAssist(controller.PathPoint);
                        break;
                    case EDrivePathing.PrecisePath:
                        controller.PathPointSet = help.AvoidAssistPrecise(controller.PathPoint);
                        break;
                }
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathingEnemy - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint + " of waypoints left " + controller.PathPlanned.Count);
                return true;
            }
            return false;
        }
        public bool ImmedeatePathingEnemy(EnemyMind mind, Vector3 Target, EDrivePathing aim)
        {
            var help = controller.Helper;

            switch (aim)
            {
                case EDrivePathing.IgnoreAll:
                    controller.PathPointSet = Target;
                    return true;
                case EDrivePathing.OnlyImmedeate:
                    break;
                case EDrivePathing.Path:
                    Target = help.AvoidAssist(Target);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = help.AvoidAssistPrecise(Target);
                    break;
                case EDrivePathing.PathInv:
                    Target = help.AvoidAssistInv(Target);
                    break;
            }
            switch (mind.EvilCommander)
            {
                case EnemyHandling.Naval:
                    Target = AIEPathing.OffsetToSea(Target, tank, help);
                    break;
                case EnemyHandling.Starship:
                    Target = AIEPathing.OffsetFromGroundH(Target, help);
                    break;
                default:
                    Target = AIEPathing.OffsetFromGround(Target, help, tank.blockBounds.size.y);
                    if (mind.EvilCommander == EnemyHandling.Wheeled)
                        Target = AIEPathing.OffsetFromSea(Target, tank, help);
                    break;
            }

            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, help);
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

            var help = controller.Helper;
            switch (mind.EvilCommander)
            {
                case EnemyHandling.Naval:
                    Target = AIEPathing.OffsetToSea(Target, tank, help);
                    break;
                case EnemyHandling.Starship:
                    Target = AIEPathing.OffsetFromGroundH(Target, help);
                    break;
                default:
                    Target = AIEPathing.OffsetFromGround(Target, help, tank.blockBounds.size.y);
                    if (mind.EvilCommander == EnemyHandling.Wheeled)
                        Target = AIEPathing.OffsetFromSea(Target, tank, help);
                    break;
            }
            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, help);

            // Planned pathing
            if (!help.Attempt3DNavi && PlanningPathingEnemy(mind, Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathingEnemy(mind, Target, core.DrivePathing);
        }



        public bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, ref EControlCoreSet core)
        {
            // DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " normal drive was called");
            if (thisInst.Attempt3DNavi)
            {
                //3D movement
                this.SpaceMaintainer(thisControl, ref core);
            }
            else //Land movement
            {
                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

                control3D.m_State.m_InputRotation = Vector3.zero;
                control3D.m_State.m_InputMovement = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);
                Vector3 destDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;

                //DebugTAC_AI.Log("IS target player " + Singleton.playerTank == thisInst.lastEnemy + " | MinimumRad " + thisInst.MinimumRad + " | destination" + controller.PathPoint);
                thisControl.DriveControl = 0f;
                if (thisInst.DoSteerCore)
                {
                    if (core.DriveDest == EDriveDest.FromLastDestination)
                    {   //Move from target
                        if (core.DriveDir == EDriveFacing.Backwards)//EDriveType.Backwards
                        {   // Face back TOWARDS target
                            if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            thisControl.DriveControl = 1f;
                            //DebugTAC_AI.Log("Forwards looking away from target");
                        }
                        else if (core.DriveDir == EDriveFacing.Perpendicular)
                        {   // Still proceed away from target
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                            thisControl.DriveControl = 1f;
                            //DebugTAC_AI.Log("Sideways at target");
                        }
                        else
                        {   // Face front TOWARDS target
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                            thisControl.DriveControl = -1f;
                            //DebugTAC_AI.Log("Reverse looking at target");
                        }
                    }
                    else if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        //int range = (int)(destDirect).magnitude;
                        float range = thisInst.lastOperatorRange;
                        if (range < thisInst.MinimumRad + 2)
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            //DebugTAC_AI.Log("Orbiting out " + thisInst.MinimumRad + " | " + destDirect);
                        }
                        else if (range > thisInst.MinimumRad + 22)
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core, out float turnVal))
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
                            if (VehicleUtils.Turner(thisControl, thisInst, aimDirect, ref core, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, aimDirect, turnVal);
                            //DebugTAC_AI.Log("Orbiting hold " + thisInst.MinimumRad);
                        }
                        thisControl.DriveControl = 1f;
                    }
                    /*
                    else if (thisInst.DriveDir == EDriveType.Backwards) // Disabled for now as most designs are forwards-facing
                    {   //Drive to target driving backwards  
                        //if (thisInst.PivotOnly)
                        //    thisControl.m_Movement.FaceDirection(tank, destDirect, 1);//need max aiming strength for turning
                        //else
                        if (Turner(thisControl, thisInst, -destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);//Face the music
                        thisControl.DriveControl = -1f;
                    }*/
                    else
                    {
                        //if (thisInst.PivotOnly)
                        //    thisControl.m_Movement.FacePosition(tank, controller.PathPoint, 1);// need max aiming strength for turning
                        //else
                        if (VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);//Face the music
                        //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  driving to " + controller.PathPoint);
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
                {   // STOP
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
                    if (thisInst.FullBoost)
                    {
                        thisControl.DriveControl = 1;
                        if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                            controller.TryBoost(thisControl);
                    }
                    else if (thisInst.LightBoost)
                    {
                        if (thisInst.LightBoostFeatheringClock >= 25)
                        {
                            if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                                controller.TryBoost(thisControl);
                            thisInst.LightBoostFeatheringClock = 0;
                        }
                        thisInst.LightBoostFeatheringClock++;
                    }
                }
                else if (thisInst.FullBoost)
                {
                    thisControl.DriveControl = 1;
                    if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                        controller.TryBoost(thisControl);
                }
                else if (thisInst.LightBoost)
                {
                    if (thisInst.LightBoostFeatheringClock >= 25)
                    {
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.8f)
                            controller.TryBoost(thisControl);
                        thisInst.LightBoostFeatheringClock = 0;
                    }
                    thisInst.LightBoostFeatheringClock++;
                }

                if (thisInst.FirePROPS)
                {
                    thisControl.BoostControlProps = true;
                }

                // DEBUG FOR DRIVE ERRORS
                if (!tank.IsAnchored)
                {
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, tank.rootBlockTrans.forward * thisControl.DriveControl, new Color(0, 0, 1));
                    if (thisControl.BoostControlJets)
                        Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, tank.rootBlockTrans.TransformDirection(controller.BoostBias) * thisInst.lastTechExtents, new Color(1, 0, 0));
                }
                else if (thisInst.AttackEnemy && thisInst.lastEnemyGet)
                {
                    if (thisInst.lastEnemyGet.tank.IsEnemy(tank.Team))
                        Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, thisInst.lastEnemyGet.centrePosition - tank.trans.position, new Color(0, 1, 1));
                }
            }
            return true;
        }

        public void SpaceMaintainer(TankControl thisControl, ref EControlCoreSet core)
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

            float driveMultiplier = 0;

            //AI Steering Rotational
            Vector3 distDiff = controller.PathPoint - tank.boundsCentreWorldNoCheck;
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
            forwardFlat = forwardFlat.normalized;
            if (thisInst.Navi3DDirect == Vector3.zero)
            {   //keep upright!
                if (core.DriveDir == EDriveFacing.Backwards)
                    turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(-forwardFlat.normalized), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
                else
                    turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(forwardFlat.normalized), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
                
                //Convert turnVal to runnable format
                if (turnVal.x > 180)
                    turnVal.x = -((turnVal.x - 360) / 180);
                else
                    turnVal.x = -(turnVal.x / 180);
                if (turnVal.z > 180)
                    turnVal.z = -((turnVal.z - 360) / 180);
                else
                    turnVal.z = -(turnVal.z / 180);

                turnVal.y = 0;

                //DebugTAC_AI.Log("TACtical_AI: TurnVal UP " + turnVal);
            }
            else
            {   //for special cases we want to angle at the enemy
                if (core.DriveDir == EDriveFacing.Backwards)
                    turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(-thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
                else
                    turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;

                Vector3 turnValUp = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(forwardFlat.normalized), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
                if (thisInst.Navi3DUp == Vector3.up)
                {
                    //DebugTAC_AI.Log("TACtical_AI: Forwards");
                    if (!thisInst.FullMelee && Vector3.Dot(thisInst.Navi3DDirect, tank.rootBlockTrans.forward) < 0.6f)
                    {
                        //If overtilt then try get back upright again
                        turnVal.x = turnValUp.x;
                        if (turnVal.x > 180)
                            turnVal.x = -((turnVal.x - 360) / 180);
                        else
                            turnVal.x = -(turnVal.x / 180);
                    }
                    else
                    {
                        if (turnVal.x > 180)
                            turnVal.x = Mathf.Clamp(-((turnVal.x - 360) / 60), -1, 1);
                        else
                            turnVal.x = Mathf.Clamp(-(turnVal.x / 60), -1, 1);
                    }
                    turnVal.z = turnValUp.z;
                    if (turnVal.z > 180)
                        turnVal.z = -((turnVal.z - 360) / 180);
                    else
                        turnVal.z = -(turnVal.z / 180);
                }
                else
                {   //Using broadside tilting
                    if (!thisInst.FullMelee && Vector3.Dot(thisInst.Navi3DUp, tank.rootBlockTrans.up) < 0.6f)
                    {
                        //If overtilt then try get back upright again
                        turnVal.z = turnValUp.z;
                        if (turnVal.z > 180)
                            turnVal.z = -((turnVal.z - 360) / 180);
                        else
                            turnVal.z = -(turnVal.z / 180);
                        //DebugTAC_AI.Log("TACtical_AI: Broadside overloaded with value " + Vector3.Dot(thisInst.Navi3DUp, tank.rootBlockTrans.up));
                    }
                    else
                    {
                        //DebugTAC_AI.Log("TACtical_AI: Broadside Z-tilt active");
                        if (turnVal.z > 180)
                            turnVal.z = Mathf.Clamp(-((turnVal.z - 360) / 60), -1, 1);
                        else
                            turnVal.z = Mathf.Clamp(-(turnVal.z / 60), -1, 1);
                    }
                    turnVal.x = turnValUp.x;
                    if (turnVal.x > 180)
                        turnVal.x = Mathf.Clamp(-((turnVal.x - 360) / 60), -1, 1);
                    else
                        turnVal.x = Mathf.Clamp(-(turnVal.x / 60), -1, 1);
                }

                //Convert turnVal to runnable format
                if (turnVal.y > 180)
                    turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / 60), -1, 1);
                else
                    turnVal.y = Mathf.Clamp(-(turnVal.y / 60), -1, 1);

                //DebugTAC_AI.Log("TACtical_AI: TurnVal AIM " + turnVal);
            }

            thisInst.Navi3DDirect = Vector3.zero;
            thisInst.Navi3DUp = Vector3.up;
            if (thisInst.DoSteerCore)
            {
                float turnValF;
                if (thisInst.AdviseAwayCore)
                {   //Move from target
                    if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Broadside the enemy
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
                        if (thisInst.lastEnemyGet.IsNotNull())
                        {
                            thisInst.Navi3DDirect = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            if (VehicleUtils.TurnerHovership(tank.control, thisInst, distDiff, ref core, out turnValF))
                                thisControl.m_Movement.FaceDirection(tank, distDiff, turnValF);
                        }
                        // Disabled for now as most spaceships in the pop do not have broadsides.
                        /*
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            if (Vector3.Dot(thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                            {
                                thisInst.Navi3DDirect = Vector3.Cross(Vector3.up, (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                thisInst.Navi3DUp = Vector3.Cross((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, thisInst.Navi3DDirect).normalized;
                                //DebugTAC_AI.Log("TACtical_AI: Broadside Left A  up is " + thisInst.Navi3DUp);
                            }
                            else
                            {
                                thisInst.Navi3DDirect = Vector3.Cross((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, Vector3.up).normalized;
                                thisInst.Navi3DUp = Vector3.Cross(thisInst.Navi3DDirect, (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                //DebugTAC_AI.Log("TACtical_AI: Broadside Right A  up is " + thisInst.Navi3DUp);
                            }
                        }
                        else
                        {
                            //thisInst.Navi3DDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, controller.PathPoint, 1);
                        }*/
                    }
                    else if (core.DriveDir == EDriveFacing.Forwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
                        if (thisInst.lastEnemyGet.IsNotNull())
                        {
                            thisInst.Navi3DDirect = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            if (VehicleUtils.TurnerHovership(tank.control, thisInst, distDiff, ref core, out turnValF))
                                thisControl.m_Movement.FaceDirection(tank, distDiff, turnValF);
                        }
                    }
                    else if (core.DriveDir == EDriveFacing.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        if (VehicleUtils.TurnerHovership(tank.control, thisInst, -distDiff, ref core, out turnValF))
                            thisControl.m_Movement.FaceDirection(tank, -distDiff, turnValF);
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
                        if (thisInst.lastEnemyGet.IsNotNull())
                        {
                            if (Vector3.Dot(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                            {
                                thisInst.Navi3DDirect = Vector3.Cross(Vector3.up, (thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                thisInst.Navi3DUp = Vector3.Cross((thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, thisInst.Navi3DDirect).normalized;
                                //DebugTAC_AI.Log("TACtical_AI: Broadside Left  up is " + thisInst.Navi3DUp);
                            }
                            else
                            {
                                thisInst.Navi3DDirect = Vector3.Cross((thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, Vector3.up).normalized;
                                thisInst.Navi3DUp = Vector3.Cross(thisInst.Navi3DDirect, (thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                //DebugTAC_AI.Log("TACtical_AI: Broadside Right  up is " + thisInst.Navi3DUp);
                            }
                        }
                        else
                        {
                            //thisInst.Navi3DDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;
                            if (VehicleUtils.TurnerHovership(tank.control, thisInst, distDiff, ref core, out turnValF))
                                thisControl.m_Movement.FacePosition(tank, controller.PathPoint, turnValF);
                        }
                    }
                    else if (core.DriveDir == EDriveFacing.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        if (VehicleUtils.TurnerHovership(tank.control, thisInst, -distDiff, ref core, out turnValF))
                            thisControl.m_Movement.FaceDirection(tank, -distDiff, turnValF);
                    }
                    else if (core.DriveDir == EDriveFacing.Forwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                        if (thisInst.lastEnemyGet.IsNotNull())
                        {
                            thisInst.Navi3DDirect = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            //thisInst.Navi3DDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;
                            if (VehicleUtils.TurnerHovership(tank.control, thisInst, distDiff, ref core, out turnValF))
                                thisControl.m_Movement.FacePosition(tank, controller.PathPoint, turnValF);
                        }
                    }
                    else
                    {   //Forwards follow but no pitch controls
                        control3D.m_State.m_InputRotation = (turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.rootBlockTrans.forward), 0, 1)).Clamp01Box();
                        if (VehicleUtils.TurnerHovership(tank.control, thisInst, distDiff, ref core, out turnValF))
                            thisControl.m_Movement.FacePosition(tank, controller.PathPoint, turnValF);
                    }
                }
            }
            else
                control3D.m_State.m_InputRotation = Vector3.zero;

            //AI Drive Translational
            Vector3 driveVal;
            //DebugTAC_AI.Log("TACtical_AI: VehicleAICore for " + tank.name + " | " + thisInst.GetCoreControlString());
            if (thisInst.AdviseAwayCore)
            {   //Move from target
                if (thisInst.lastEnemyGet.IsNotNull() && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
                {
                    //DebugTAC_AI.Log("TACtical_AI: REVEREEE");
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(InvertHorizontalPlane(distDiff.normalized * 150)));
                    if (thisInst.AIAlign == AIAlignment.Player && thisInst.lastPlayer.IsNotNull())
                    {
                        // Keep below a certain height in relation to player so that they may command if need be
                        if (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.MaxCombatRange / 3) < thisInst.tank.boundsCentreWorldNoCheck.y)
                            driveVal.y = -1;
                    }
                    else
                    {
                        //DebugTAC_AI.Log("TACtical_AI: leveling");
                        float enemyOffsetH = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck.y + thisInst.lastEnemyGet.tank.GetCheapBounds() + thisInst.GroundOffsetHeight;
                        float leveler = Mathf.Clamp((enemyOffsetH - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
                        if (leveler > -0.25f)
                            driveVal.y = leveler;
                        else
                            driveVal.y = -1f;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log("TACtical_AI: REVEREEE2");
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(InvertHorizontalPlane(distDiff.normalized * 150)));
                }
                driveMultiplier = 1f;
            }
            else
            {
                if (thisInst.lastEnemyGet.IsNotNull() && !thisInst.IsMultiTech && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
                {   //level alt with enemy
                    //DebugTAC_AI.Log("TACtical_AI: FWD");
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(distDiff));
                    if (thisInst.AIAlign == AIAlignment.Player && thisInst.lastPlayer.IsNotNull())
                    {
                        // Keep below a certain height in relation to player so that they may command if need be
                        if (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.MaxCombatRange / 3) < thisInst.tank.boundsCentreWorldNoCheck.y)
                            driveVal.y = -1;
                    }
                    else
                    {
                        float enemyOffsetH = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck.y + thisInst.lastEnemyGet.tank.GetCheapBounds() + thisInst.GroundOffsetHeight;
                        float leveler = Mathf.Clamp((enemyOffsetH - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
                        if (leveler > -0.25f)
                            driveVal.y = leveler;
                        else
                            driveVal.y = -1f;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log("TACtical_AI: FWD2");
                    float range = thisInst.lastOperatorRange;
                    if (range < thisInst.MinimumRad - 1)
                    {
                        driveMultiplier = 1f;
                        driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformVector(InvertHorizontalPlane(distDiff)) * 0.3f);
                    }
                    else if (range > thisInst.MinimumRad + 1)
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
            if (!thisInst.IsMultiTech)
            {
                float height = thisInst.GetFrameHeight();
                if (height > tank.boundsCentreWorldNoCheck.y - thisInst.lastTechExtents)
                {
                    EmergencyUp = true;
                    CloseToGroundWarning = true;
                }
                else if (height > tank.boundsCentreWorldNoCheck.y - (thisInst.lastTechExtents * 2))
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

            if (thisInst.PivotOnly)
            {
                driveVal.x = 0;
                driveVal.z = 0;
            }

            if (thisInst.Yield)
            {
                // Supports all directions
                if (thisInst.recentSpeed > 20)
                    driveMultiplier = -0.3f;
                else
                    driveMultiplier = 0.3f;
            }
            else if (thisInst.FullBoost)
            {
                driveMultiplier = 1;
                if (thisInst.IsMultiTech || Vector3.Dot(driveVal, tank.rootBlockTrans.forward) > 0.75f)
                    thisControl.BoostControlJets = true;
            }
            else if (thisInst.LightBoost)
            {
                if (thisInst.ForceSetDrive)
                    driveMultiplier = thisInst.DriveVar;
                if (thisInst.LightBoostFeatheringClock >= 25)
                {
                    if (thisInst.IsMultiTech || Vector3.Dot(driveVal, tank.rootBlockTrans.forward) > 0.75f)
                        thisControl.BoostControlJets = true;
                    thisInst.LightBoostFeatheringClock = 0;
                }
                thisInst.LightBoostFeatheringClock++;
            }
            else if (thisInst.ForceSetDrive)
            {
                driveMultiplier = thisInst.DriveVar;
            }
            if (thisInst.FirePROPS)
            {
                thisControl.BoostControlProps = true;
            }

            // PREVENT GROUND CRASHING
            if (EmergencyUp)
            {
                control3D.m_State.m_InputMovement = (tank.rootBlockTrans.InverseTransformVector(Vector3.up) * 2).Clamp01Box();

                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, driveVal * thisInst.lastTechExtents, new Color(0, 0, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, control3D.m_State.m_InputMovement * thisInst.lastTechExtents, new Color(1, 0, 0));

                controlGet.SetValue(tank.control, control3D);
                return;
            }
            //thisInst.MinimumRad
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

            // DEBUG FOR DRIVE ERRORS
            if (!tank.IsAnchored)
            {
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, distDiff, new Color(0, 1, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, tank.rootBlockTrans.TransformVector(driveVal * thisInst.lastTechExtents * 2), new Color(0, 0, 1)); // blue
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, tank.rootBlockTrans.TransformVector(control3D.m_State.m_InputMovement * thisInst.lastTechExtents * 2), new Color(1, 0, 0));
            }
            else if (thisInst.AttackEnemy && thisInst.lastEnemyGet)
            {
                if (thisInst.lastEnemyGet.tank.IsEnemy(tank.Team))
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, thisInst.lastEnemyGet.centrePosition - tank.trans.position, new Color(0, 1, 1));
            }
            controlGet.SetValue(tank.control, control3D);
        }

        public bool TryAdjustForCombat(bool between, ref Vector3 pos, ref EControlCoreSet core)
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            bool output = false;
            if (thisInst.ChaseThreat && (!thisInst.IsDirectedMovingAnyDest || !thisInst.Retreat) && thisInst.lastEnemyGet.IsNotNull())
            {
                Vector3 targPos = thisInst.LeadTargetAiming(thisInst.lastEnemyGet);
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                thisInst.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((thisInst.lastCombatRange - thisInst.MinCombatRange) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    core.DriveDir = EDriveFacing.Perpendicular;
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = targPos;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 3;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        core.DriveDir = EDriveFacing.Backwards;
                        pos = thisInst.AvoidAssist(targPos);
                        //thisInst.MinimumRad = 0.5f;
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 3;
                    }
                    /*
                    else if (driveDyna < 0.5f)
                    {
                        thisInst.PivotOnly = true;
                        pos = thisInst.AvoidAssist(targPos);
                        //thisInst.MinimumRad = 0.5f;
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }*/
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 3;
                    }
                }
                else
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    if (thisInst.FullMelee)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = targPos;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 3;
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                }
                if (between && thisInst.theResource?.tank)
                {
                    pos = Between(pos, thisInst.theResource.tank.boundsCentreWorldNoCheck);
                }
            }
            else
                thisInst.IgnoreEnemyDistance();
            return output;
        }

        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            bool output = false;
            if (!thisInst.Retreat && thisInst.lastEnemyGet.IsNotNull() && mind.CommanderMind != EnemyAttitude.OnRails)
            {
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                thisInst.UpdateEnemyDistance(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                float driveDyna = Mathf.Clamp((thisInst.lastCombatRange - mind.MinCombatRange) / 3f, -1, 1);

                if (mind.CommanderAttack == EAttackMode.Circle)
                {   // works fine for now
                    if (thisInst.SideToThreat)
                        core.DriveDir = EDriveFacing.Perpendicular;
                    else
                        core.DriveDir = EDriveFacing.Forwards;
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!;
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = thisInst.AvoidAssist(RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind));
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = thisInst.AvoidAssist(RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind));
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = thisInst.AvoidAssist(RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind));
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    //DebugTAC_AI.Log("DriveDyna is " + driveDyna);
                }
                else
                {   // Since the enemy also uses it's Operator in combat, this will have to listen to that
                    if (thisInst.IsDirectedMovingFromDest)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = thisInst.AvoidAssistInv(RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind));
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.IsDirectedMovingToDest && mind.LikelyMelee)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        if (mind.LikelyMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = thisInst.AvoidAssist(RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind));
                            thisInst.MinimumRad = 0.5f;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = thisInst.AvoidAssist(RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind));
                            thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 5;
                        }
                    }
                    else
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        pos = thisInst.AvoidAssist(RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind));
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 5;
                    }
                }
            }
            else
                thisInst.IgnoreEnemyDistance();
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
                    return direction + Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(-controller.Helper.SafeVelocity) * DampeningStrength, direction);
                }
            }
            else
            {
                if (controller.Helper.AdvancedAI)
                {
                    return direction + Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(-controller.Helper.SafeVelocity) * DampeningStrength, direction);
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
