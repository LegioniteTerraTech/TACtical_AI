using System;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using TerraTechETCUtil;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    /// <summary> Handles Space AI Directors & Maintainers </summary>
    internal class LandAICore : IMovementAICore
    {
        private AIControllerDefault controller;
        private Tank tank;
        public float GetDrive => lastDrive;
        private float lastDrive = 0;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerDefault)controller;
            this.tank = tank;
            this.controller.WaterPathing = WaterPathing.AvoidWater;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GroundOffsetGeneralAir;
            //DebugTAC_AI.Info(KickStart.ModID + ": LandAICore - Init");

            if (controller.Helper.Allied && controller.Helper.AutoAnchor)
            {
                if (tank.IsAnchored && !controller.Helper.PlayerAllowAutoAnchoring)
                    DebugTAC_AI.Log(KickStart.ModID + ": LandAICore - Should NOT be active when anchored UNLESS we have autoAnchor [" +
                        controller.Helper.AutoAnchor.ToString() + "]!  StaticAICore should be in control!");
            }
            else if (tank.IsAnchored)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": LandAICore - Should NOT be active when anchored UNLESS we have autoAnchor [NPT]!  StaticAICore should be in control!");
            }
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            throw new NotImplementedException();
        }

        public bool PlanningPathing(Vector3 Target, EDrivePathing aim)
        {
            float pathSuccessMulti = 1;

            controller.TargetDestination = WorldPosition.FromScenePosition(Target);
            switch (aim)
            {
                case EDrivePathing.IgnoreAll:
                case EDrivePathing.OnlyImmedeate:
                    //DebugTAC_AI.Log(tank.name + ": PlanningPathing - NotThisFrame");
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
            {
                //DebugTAC_AI.Log(tank.name + ": PlanningPathing - Not far enough");
                return false;
            }
            var helper = controller.Helper;
            if (!controller.AutoPathfind)
            {
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - Started pathfinding!");
                controller.WaterPathing = WaterPathing.AvoidWater;
                controller.SetAutoPathfinding(true);
            }
            //DebugTAC_AI.Log(tank.name + ": PlanningPathing - Trying to work");
            if (controller.PathPlanned.Count > 0)
            {
                helper.AutoSpacing = 0; // Drive DIRECTLY to target
                controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                if ((controller.PathPoint - tank.boundsCentreWorldNoCheck).WithinSquareXZ((tank.GetCheapBounds() * pathSuccessMulti) + 4))
                {
                    controller.PathPlanned.Dequeue(); // Next position!
                    DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - finished pathing to " + controller.PathPoint);
                    if (controller.PathPlanned.Count == 0)
                    {
                        DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - All Done!");
                        return false;
                    }
                    controller.PathPointSet = AIEPathing.SnapOffsetFromGroundA(controller.PathPlanned.Peek().ScenePosition, 1);
                }
                switch (aim)
                {
                    case EDrivePathing.Path:
                        controller.PathPointSet = helper.AvoidAssist(controller.PathPoint, helper.recentSpeed < helper.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend);
                        break;
                    case EDrivePathing.PrecisePathIgnoreScenery:
                        controller.PathPointSet = helper.AvoidAssistPrecise(controller.PathPoint, helper.recentSpeed < helper.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend, true);
                        break;
                    case EDrivePathing.PrecisePath:
                        controller.PathPointSet = helper.AvoidAssistPrecise(controller.PathPoint, helper.recentSpeed < helper.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend);
                        break;
                }

                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint + " of waypoints left " + controller.PathPlanned.Count);

                return true;
            }
            return false;
        }

        public bool DriveDirectorRTS(ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTargetRTS(controller, out Vector3 Target, ref core))
                return false;

            var helper = controller.Helper;
            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);
                Target = AIEPathing.OffsetFromSea(Target, tank, helper);
                Target = AIEPathing.ModerateMaxAlt(Target, helper);
            }
            controller.PathPointSet = Target;

            // Planned pathing
            if (PlanningPathing(Target, core.DrivePathing))
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
                Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);
                Target = AIEPathing.OffsetFromSea(Target, tank, helper);
                Target = AIEPathing.ModerateMaxAlt(Target, helper);
            }
            controller.PathPointSet = Target;

            // Planned pathing
            if (PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathing(Target, core.DrivePathing);
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
                    break;
                case EDrivePathing.PrecisePathIgnoreScenery:
                    Target = helper.AvoidAssistPrecise(Target, IgnoreDestructable: true);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = helper.AvoidAssistPrecise(Target);
                    break;
            }

            Target = AIEPathing.OffsetFromSea(Target, tank, helper);
            Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);

            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, helper);
            //DebugTAC_AI.LogPathing(tank.name + ": ImmedeatePathing - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint);
            return true;
        }

        public bool DriveDirector(ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTarget(controller, out Vector3 Target, ref core))
                return false;

            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                var helper = controller.Helper;
                Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);
                Target = AIEPathing.OffsetFromSea(Target, tank, helper);
                Target = AIEPathing.ModerateMaxAlt(Target, helper);
            }
            controller.PathPointSet = Target;

            // Planned pathing
            if (PlanningPathing(Target, core.DrivePathing))
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
            Target = AIEPathing.OffsetFromSea(Target, tank, helper);
            Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);

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
                Target = AIEPathing.OffsetFromGround(Target, helper, tank.blockBounds.size.y);
                Target = AIEPathing.OffsetFromSea(Target, tank, helper);
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
            helper.UpdateVanillaAvoidence();
            // DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " normal drive was called");
            Vector3 destDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;

            //DebugTAC_AI.Log("IS target player " + Singleton.playerTank == helper.lastEnemy + " | MinimumRad " + helper.MinimumRad + " | destination" + controller.PathPoint);
            // DRIVE
            float DriveControl = 0f;

            float range = helper.lastOperatorRange;
            if (helper.lastEnemyGet)
                range = helper.lastCombatRange;

            switch (core.DriveDir)
            {
                case EDriveFacing.Stop:
                    helper.DriveControl = 0;
                    return true;
                case EDriveFacing.Neutral:
                    helper.DriveControl = 0.001f;
                    return true;
                case EDriveFacing.Forwards:
                    if (core.DriveDest >= EDriveDest.ToLastDestination)
                    {
                        if (helper.AutoSpacing > 0)
                        {
                            if (range < helper.AutoSpacing - 1)
                                DriveControl = -1f;
                            else if (range > helper.AutoSpacing + 1)
                                DriveControl = 1f;
                            else
                                DriveControl = 0f;
                        }
                        else
                            DriveControl = 1f;
                    }
                    else if (core.DriveDest == EDriveDest.FromLastDestination)
                    {
                        DriveControl = -1f;
                    }
                    break;
                case EDriveFacing.Perpendicular:
                    if (core.DriveDest >= EDriveDest.ToLastDestination)
                    {
                        if (helper.AutoSpacing > 0)
                        {
                            if (range > helper.AutoSpacing + 1)
                                DriveControl = 1f;
                            else
                                DriveControl = 0f;
                        }
                        else
                            DriveControl = 1f;
                    }
                    else if (core.DriveDest == EDriveDest.FromLastDestination)
                    {
                        DriveControl = 1f;
                    }
                    break;
                case EDriveFacing.Backwards:
                    if (core.DriveDest >= EDriveDest.ToLastDestination)
                    {
                        if (helper.AutoSpacing > 0)
                        {
                            if (range < helper.AutoSpacing - 1)
                                DriveControl = 1f;
                            else if (range > helper.AutoSpacing + 1)
                                DriveControl = -1f;
                            else
                                DriveControl = 0f;
                        }
                        else
                            DriveControl = -1f;
                    }
                    else if (core.DriveDest == EDriveDest.FromLastDestination)
                    {
                        DriveControl = 1f;
                    }
                    break;
                default:
                    DriveControl = 0f;
                    break;
            }

            // Additional drive controls
            switch (helper.ThrottleState)
            {
                case AIThrottleState.PivotOnly:
                    DriveControl = 0;
                    break;
                case AIThrottleState.Yield:
                    if (DriveControl < 0)
                    {
                        if (helper.recentSpeed < -AIGlobals.YieldSpeed)
                            DriveControl = 0.2f;
                        else
                            DriveControl = -1f;
                    }
                    else
                    {   // works with forwards
                        if (helper.recentSpeed > AIGlobals.YieldSpeed)
                            DriveControl = -0.2f;
                        else
                            DriveControl = 1f;
                    }
                    break;
                case AIThrottleState.FullSpeed:
                    if (helper.FullBoost)
                    {
                        Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                        controller.TryBoost(forwardLocal);
                    }
                    else if (helper.LightBoost)
                    {
                        if (helper.LightBoostFeatheringClock >= 25)
                        {
                            Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                            controller.TryBoost(forwardLocal);
                            helper.LightBoostFeatheringClock = 0;
                        }
                        helper.LightBoostFeatheringClock++;
                    }
                    break;
                case AIThrottleState.ForceSpeed:
                    DriveControl = helper.DriveVar;
                    if (helper.FullBoost)
                    {
                        Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                        controller.TryBoost(forwardLocal);
                    }
                    else if (helper.LightBoost)
                    {
                        if (helper.LightBoostFeatheringClock >= 25)
                        {
                            Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                            controller.TryBoost(forwardLocal);
                            helper.LightBoostFeatheringClock = 0;
                        }
                        helper.LightBoostFeatheringClock++;
                    }
                    break;
                default:
                    break;
            }
            // STEERING
            if (helper.DoSteerCore)
            {
                float turnVal;
                switch (core.DriveDir)
                {
                    case EDriveFacing.Perpendicular:
                        if (core.DriveDest == EDriveDest.FromLastDestination)
                        {
                            VehicleUtils.Turner(helper, -destDirect, DriveControl, ref core);
                            //DebugTAC_AI.Log("Sideways at target");
                        }
                        else
                        {
                            if (range < helper.AutoSpacing + 2)
                            {
                                VehicleUtils.Turner(helper, -destDirect, DriveControl, ref core);
                                //DebugTAC_AI.Log("Orbiting out " + helper.MinimumRad + " | " + destDirect);
                            }
                            else if (range > helper.AutoSpacing + 22)
                            {
                                VehicleUtils.Turner(helper, destDirect, DriveControl, ref core);
                                //DebugTAC_AI.Log("Orbiting in " + helper.MinimumRad);
                            }
                            else  //ORBIT!
                            {
                                Vector3 aimDirect;
                                float angleControl = Mathf.InverseLerp(helper.AutoSpacing + 2, helper.AutoSpacing + 22, range) * 2 - 1;
                                if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.right) < 0)
                                {
                                    Quaternion tweakAngle = Quaternion.AngleAxis(90 * angleControl, Vector3.down);
                                    aimDirect = Vector3.Cross(tweakAngle * destDirect.normalized, Vector3.down);
                                }
                                else
                                {
                                    Quaternion tweakAngle = Quaternion.AngleAxis(90 * angleControl, Vector3.up);
                                    aimDirect = Vector3.Cross(tweakAngle * destDirect.normalized, Vector3.up);
                                }
                                VehicleUtils.Turner(helper, aimDirect, DriveControl, ref core);
                                //DebugTAC_AI.Log("Orbiting hold " + helper.MinimumRad);
                            }
                        }
                        break;
                    case EDriveFacing.Backwards:
                        // Face back TOWARDS target
                        if (core.DriveDest == EDriveDest.FromLastDestination)
                        {   //Move from target
                            VehicleUtils.Turner(helper, -destDirect, DriveControl, ref core);
                            //DebugTAC_AI.Log("Forwards looking away from target");
                        }
                        else
                        {
                            VehicleUtils.Turner(helper, -destDirect, DriveControl, ref core);
                        }
                        break;
                    default:
                        // Face front TOWARDS target
                        if (core.DriveDest == EDriveDest.FromLastDestination)
                        {
                            VehicleUtils.Turner(helper, destDirect, DriveControl, ref core);
                        }
                        else
                        {
                            VehicleUtils.Turner(helper, destDirect, DriveControl, ref core);
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  driving to " + controller.PathPoint); 
                        }
                        break;
                }
            }


            // DEBUG FOR DRIVE ERRORS
            if (DebugRawTechSpawner.ShowDebugFeedBack)
            {
                if (!tank.IsAnchored)
                {
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));
                    if (DriveControl != 0)
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, tank.rootBlockTrans.forward * (helper.lastTechExtents + (DriveControl * helper.lastTechExtents)), new Color(0, 0, 1));
                    if (helper.FullBoost || helper.LightBoost)
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 2, tank.rootBlockTrans.TransformDirection(controller.BoostBias) * (helper.lastTechExtents * 2), new Color(1, 0, 0));
                }
                else if (helper.AttackEnemy && helper.lastEnemyGet)
                {
                    if (ManBaseTeams.IsEnemy(tank.Team, helper.lastEnemyGet.tank.Team))
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, helper.lastEnemyGet.centrePosition - tank.trans.position, new Color(1, 0, 0));
                }
            }
            helper.ProcessControl(new Vector3(0, 0, DriveControl), Vector3.zero, Vector3.zero, helper.FirePROPS, false);

            lastDrive = DriveControl;
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
                helper.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((helper.lastCombatRange - helper.MinCombatRange) / 3f, -1, 1);
                if (helper.SideToThreat || (helper.BlockedLineOfSight && helper.AdvancedAI))
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
                        core.DriveToFacingTowards();
                        pos = targPos;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveToFacingTowards();
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveAwayFacingTowards();
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = 0.5f;
                    }
                    else
                    {
                        core.DriveToFacingTowards();
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 3;
                    }
                }
                if (between && helper.theResource?.tank)
                {
                    pos = Between(targPos, helper.theResource.tank.boundsCentreWorldNoCheck);
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
            if (helper.ChaseThreat && helper.lastEnemyGet.IsNotNull() && mind.CommanderMind != EnemyAttitude.OnRails)
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
                        core.DriveAwayFacingTowards();
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = 0;//0.5f;
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

    }
}
