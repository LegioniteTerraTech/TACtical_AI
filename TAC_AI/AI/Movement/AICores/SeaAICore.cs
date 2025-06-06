﻿using System;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using UnityEngine;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement.AICores
{
    /// <summary> Handles Sea AI Directors & Maintainers </summary>
    internal class SeaAICore : IMovementAICore
    {
        private AIControllerDefault controller;
        private Tank tank;
        public float GetDrive => lastDrive;
        private float lastDrive = 0;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerDefault)controller;
            this.controller.WaterPathing = WaterPathing.StayInWater;
            this.tank = tank;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GroundOffsetGeneralAir;
            //DebugTAC_AI.Info(KickStart.ModID + ": SeaAICore - Init");

            if (controller.Helper.Allied && controller.Helper.AutoAnchor)
            {
                if (tank.IsAnchored && !controller.Helper.PlayerAllowAutoAnchoring)
                    DebugTAC_AI.Log(KickStart.ModID + ": SeaAICore - Should NOT be active when anchored UNLESS we have autoAnchor! StaticAICore should be in control!");
            }
            else if (tank.IsAnchored)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SeaAICore - Should NOT be active when anchored UNLESS we have autoAnchor! StaticAICore should be in control!");
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
            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                    Target = AIEPathing.OffsetFromGroundH(Target, helper);
                else
                    Target = AIEPathing.OffsetToSea(Target, tank, helper);
                Target = AIEPathing.ModerateMaxAlt(Target, helper);
            }
            controller.PathPointSet = Target;

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
            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                    Target = AIEPathing.OffsetFromGroundH(Target, helper);
                else
                    Target = AIEPathing.OffsetToSea(Target, tank, helper);
                Target = AIEPathing.ModerateMaxAlt(Target, helper);
            }
            controller.PathPointSet = Target;

            // Planned pathing
            if (!helper.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
        }


        public bool PlanningPathing(Vector3 Target, EDrivePathing aim)
        {
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
                case EDrivePathing.PrecisePathIgnoreScenery:
                case EDrivePathing.PrecisePath:
                    pathSuccessMulti = AIGlobals.AIPathingSuccessRadPrecise;
                    break;
            }
            if (!AIEAutoPather.IsFarEnough(tank.boundsCentreWorldNoCheck, Target))
                return false;
            var helper = controller.Helper;
            controller.WaterPathing = WaterPathing.StayInWater;

            if (!controller.AutoPathfind)
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - Started pathfinding!");

            controller.TargetDestination = WorldPosition.FromScenePosition(Target);
            controller.SetAutoPathfinding(true);
            if (controller.PathPlanned.Count > 0)
            {
                helper.AutoSpacing = 0; // Drive DIRECTLY to target
                controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                if ((controller.PathPoint - tank.boundsCentreWorldNoCheck).WithinSquareXZ(tank.GetCheapBounds() * pathSuccessMulti))
                {
                    controller.PathPlanned.Dequeue(); // Next position!
                    DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - finished pathing to " + controller.PathPoint);
                    if (controller.PathPlanned.Count == 0)
                    {
                        DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - All Done!");
                        return false;
                    }
                    controller.PathPointSet = AIEPathing.SnapOffsetToSea(controller.PathPlanned.Peek().ScenePosition);
                }
                switch (aim)
                {
                    case EDrivePathing.Path:
                        controller.PathPointSet = helper.AvoidAssist(controller.PathPoint);
                        break;
                    case EDrivePathing.PrecisePathIgnoreScenery:
                        controller.PathPointSet = helper.AvoidAssistPrecise(controller.PathPoint, true, true);
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
                case EDrivePathing.PrecisePathIgnoreScenery:
                    Target = helper.AvoidAssistPrecise(Target, IgnoreDestructable: true);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = helper.AvoidAssistPrecise(Target);
                    break;
            }

            if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                Target = AIEPathing.OffsetFromGroundH(Target, helper);
            else
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
            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                if (helper.Attempt3DNavi && !(helper.FullMelee && helper.lastEnemyGet.IsNotNull()))
                    Target = AIEPathing.OffsetFromGroundH(Target, helper);
                else
                    Target = AIEPathing.OffsetToSea(Target, tank, helper);
                Target = AIEPathing.ModerateMaxAlt(Target, helper);
            }
            controller.PathPointSet = Target;

            // Planned pathing
            if (!helper.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
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
                case EDrivePathing.PrecisePathIgnoreScenery:
                    Target = helper.AvoidAssistPrecise(Target, IgnoreDestructable: true);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = helper.AvoidAssistPrecise(Target);
                    break;
            }
            Target = AIEPathing.OffsetToSea(Target, tank, helper);

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
            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                Target = AIEPathing.OffsetToSea(Target, tank, helper);
                Target = AIEPathing.ModerateMaxAlt(Target, helper);
            }
            controller.PathPointSet = Target;

            // Planned pathing
            if (!helper.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathingEnemy(mind, Target, core.DrivePathing);
        }



        public bool DriveMaintainer(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
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
                if (turnVal.x > 180)
                    turnVal.x = -((turnVal.x - 360) / 180);
                else
                    turnVal.x = -(turnVal.x / 180);
                if (turnVal.z > 180)
                    turnVal.z = -((turnVal.z - 360) / 180);
                else
                    turnVal.z = -(turnVal.z / 180);

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
                    if (!helper.FullMelee && Vector3.Dot(helper.Navi3DUp, tank.rootBlockTrans.up) < 0.6f)
                    {
                        //If overtilt then try get back upright again
                        turnVal.z = turnValUp.z;
                        if (turnVal.z > 180)
                            turnVal.z = -((turnVal.z - 360) / 180);
                        else
                            turnVal.z = -(turnVal.z / 180);
                        //DebugTAC_AI.Log(KickStart.ModID + ": Broadside overloaded with value " + Vector3.Dot(helper.Navi3DUp, tank.rootBlockTrans.up));
                    }
                    else
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Broadside Z-tilt active");
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

                //DebugTAC_AI.Log(KickStart.ModID + ": TurnVal AIM " + turnVal);
            }

            helper.Navi3DDirect = Vector3.zero;
            helper.Navi3DUp = Vector3.up;
            Vector3 TurnVal = Vector3.zero;
            if (helper.DoSteerCore)
            {
                float turnValF;
                if (helper.AdviseAwayCore)
                {   //Move from target
                    if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Broadside the enemy
                        TurnVal = turnVal.Clamp01Box();//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
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
                        TurnVal = turnVal.Clamp01Box();
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
                        TurnVal = turnVal.Clamp01Box();//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
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
                        TurnVal = turnVal.Clamp01Box();
                        VehicleUtils.TurnerHovership(tank.control, helper, -distDiff, ref core);
                    }
                    else
                    {
                        TurnVal.y = 0;
                    }
                }
                else
                {
                    if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Broadside the enemy
                        TurnVal = turnVal.Clamp01Box();
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
                        TurnVal = turnVal.Clamp01Box();
                        VehicleUtils.TurnerHovership(tank.control, helper, -distDiff, ref core);
                    }
                    else if (core.DriveDir == EDriveFacing.Forwards)
                    {
                        TurnVal = turnVal.Clamp01Box();
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
                        TurnVal = (turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.rootBlockTrans.forward), 0, 1)).Clamp01Box();
                        VehicleUtils.TurnerHovership(tank.control, helper, distDiff, ref core);
                    }
                }
            }
            else
                TurnVal = Vector3.zero;

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
                        if (core.DriveDir== EDriveFacing.Forwards || core.DriveDir== EDriveFacing.Backwards)
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
            Vector3 DriveVal = Vector3.zero;

            // PREVENT GROUND CRASHING
            if (EmergencyUp)
            {
                DriveVal = (tank.rootBlockTrans.InverseTransformVector(Vector3.up) * 2).Clamp01Box();

                if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
                {
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, driveVal * helper.lastTechExtents, new Color(0, 0, 1));
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, DriveVal * helper.lastTechExtents, new Color(1, 0, 0));
                }
                helper.ProcessControl(DriveVal, TurnVal, Vector3.zero, false, false);
                return true;
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
            DriveVal = final.Clamp01Box();

            if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
            {
                // DEBUG FOR DRIVE ERRORS
                if (!tank.IsAnchored)
                {
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, distDiff, new Color(0, 1, 1));
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, tank.rootBlockTrans.TransformVector(driveVal * helper.lastTechExtents * 2), new Color(0, 0, 1)); // blue
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 2, tank.rootBlockTrans.TransformVector(DriveVal * helper.lastTechExtents * 2), new Color(1, 0, 0));
                }
                else if (helper.AttackEnemy && helper.lastEnemyGet)
                {
                    if (ManBaseTeams.IsEnemy(tank.Team, helper.lastEnemyGet.tank.Team))
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, helper.lastEnemyGet.centrePosition - tank.trans.position, new Color(0, 1, 1));
                }
            }
            lastDrive = DriveVal.z;
            helper.ProcessControl(DriveVal, TurnVal, Vector3.zero, false, false);
            return true;
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
                if (between && helper.theResource?.tank)
                {
                    targPos = Between(targPos, helper.theResource.tank.boundsCentreWorldNoCheck);
                }
                helper.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((helper.lastCombatRange - helper.MinCombatRange) / 3f, -1, 1);
                if (helper.SideToThreat)
                {
                    core.DriveDir = EDriveFacing.Perpendicular;
                    if (helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        pos = targPos;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                    else if (driveDyna < 0)
                    {
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
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                }
                else
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    if (helper.FullMelee)
                    {
                        pos = targPos;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
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
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                }
            }
            else
                helper.IgnoreEnemyDistance();
            controller.PathPointSet = pos;
            return output;
        }

        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper helper = controller.Helper;
            bool output = false;
            if (!helper.Retreat && helper.lastEnemyGet.IsNotNull() && mind.CommanderMind != EnemyAttitude.OnRails)
            {
                output = true;
                Vector3 targPos = helper.InterceptTargetDriving(helper.lastEnemyGet);
                core.DriveDir = EDriveFacing.Forwards;
                helper.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((helper.lastCombatRange - mind.MinCombatRange) / 3f, -1, 1);

                if (mind.CommanderAttack == EAttackMode.Circle)
                {   // works fine for now
                    if (helper.SideToThreat)
                        core.DriveDir = EDriveFacing.Perpendicular;
                    else
                        core.DriveDir = EDriveFacing.Forwards;
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!;
                        pos = targPos;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        pos = helper.AvoidAssist(targPos);
                        //helper.MinimumRad = helper.lastTechExtents + helper.lastEnemy.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(targPos);
                        //helper.MinimumRad = helper.lastTechExtents + helper.lastEnemy.GetCheapBounds() + 2;
                    }
                    else
                    {
                        pos = helper.AvoidAssist(targPos);
                        //helper.MinimumRad = helper.lastTechExtents + helper.lastEnemy.GetCheapBounds() + 2;
                    }
                    //DebugTAC_AI.Log("DriveDyna is " + driveDyna);
                }
                else
                {   // Since the enemy also uses it's Operator in combat, this will have to listen to that
                    if (helper.IsDirectedMovingFromDest)
                    {
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = 0.5f;
                    }
                    else if (helper.IsDirectedMovingToDest && mind.LikelyMelee)
                    {
                        if (mind.LikelyMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = helper.AvoidAssist(targPos);
                            helper.AutoSpacing = 0.5f;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = helper.AvoidAssist(targPos);
                            helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                        }
                    }
                    else
                    {
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                    }
                }
            }
            else
                helper.IgnoreEnemyDistance();
            controller.PathPointSet = pos;
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
