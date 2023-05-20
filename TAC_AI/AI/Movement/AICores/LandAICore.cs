using System;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    /// <summary> Handles Space AI Directors & Maintainers </summary>
    public class LandAICore : IMovementAICore
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
        private AIControllerDefault controller;
        private Tank tank;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerDefault)controller;
            this.tank = tank;
            this.controller.WaterPathing = WaterPathing.AvoidWater;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GroundOffsetGeneralAir;
            //DebugTAC_AI.Info("TACtical_AI: LandAICore - Init");

            if (controller.Helper.Allied && controller.Helper.AutoAnchor)
            {
                if (tank.IsAnchored && !controller.Helper.PlayerAllowAutoAnchoring)
                    DebugTAC_AI.Log("TACtical_AI: LandAICore - Should NOT be active when anchored UNLESS we have autoAnchor!  StaticAICore should be in control!");
            }
            else if (tank.IsAnchored)
            {
                DebugTAC_AI.Log("TACtical_AI: LandAICore - Should NOT be active when anchored UNLESS we have autoAnchor!  StaticAICore should be in control!");
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
            var help = controller.Helper;
            if (!controller.AutoPathfind)
            {
                DebugTAC_AI.LogPathing(tank.name + ": PlanningPathing - Started pathfinding!");
                controller.WaterPathing = WaterPathing.AvoidWater;
                controller.SetAutoPathfinding(true);
            }
            //DebugTAC_AI.Log(tank.name + ": PlanningPathing - Trying to work");
            if (controller.PathPlanned.Count > 0)
            {
                help.MinimumRad = 0; // Drive DIRECTLY to target
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
                        controller.PathPointSet = help.AvoidAssist(controller.PathPoint, help.recentSpeed < help.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend);
                        break;
                    case EDrivePathing.PrecisePathIgnoreScenery:
                        controller.PathPointSet = help.AvoidAssistPrecise(controller.PathPoint, help.recentSpeed < help.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend, true);
                        break;
                    case EDrivePathing.PrecisePath:
                        controller.PathPointSet = help.AvoidAssistPrecise(controller.PathPoint, help.recentSpeed < help.EstTopSped / AIGlobals.PlayerAISpeedPanicDividend);
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

            var help = controller.Helper;
            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                Target = AIEPathing.OffsetFromGround(Target, help, tank.blockBounds.size.y);
                Target = AIEPathing.OffsetFromSea(Target, tank, help);
                Target = AIEPathing.ModerateMaxAlt(Target, help);
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
                case EDrivePathing.PrecisePathIgnoreScenery:
                    Target = help.AvoidAssistPrecise(Target, IgnoreDestructable: true);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = help.AvoidAssistPrecise(Target);
                    break;
            }

            Target = AIEPathing.OffsetFromSea(Target, tank, help);
            Target = AIEPathing.OffsetFromGround(Target, help, tank.blockBounds.size.y);

            controller.PathPointSet = AIEPathing.ModerateMaxAlt(Target, help);
            //DebugTAC_AI.LogPathing(tank.name + ": ImmedeatePathing - Current pos is " + tank.boundsCentreWorldNoCheck + " and target is " + controller.PathPoint);
            return true;
        }

        public bool DriveDirector(ref EControlCoreSet core)
        {
            if (!VehicleUtils.GetPathingTarget(controller, out Vector3 Target, ref core))
                return false;

            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                var help = controller.Helper;
                Target = AIEPathing.OffsetFromGround(Target, help, tank.blockBounds.size.y);
                Target = AIEPathing.OffsetFromSea(Target, tank, help);
                Target = AIEPathing.ModerateMaxAlt(Target, help);
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
                case EDrivePathing.PrecisePathIgnoreScenery:
                    Target = help.AvoidAssistPrecise(Target, IgnoreDestructable: true);
                    break;
                case EDrivePathing.PrecisePath:
                    Target = help.AvoidAssistPrecise(Target);
                    break;
                case EDrivePathing.PathInv:
                    Target = help.AvoidAssistInv(Target);
                    break;
            }
            Target = AIEPathing.OffsetFromSea(Target, tank, help);
            Target = AIEPathing.OffsetFromGround(Target, help, tank.blockBounds.size.y);

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
            if (core.DrivePathing != EDrivePathing.IgnoreAll)
            {
                Target = AIEPathing.OffsetFromGround(Target, help, tank.blockBounds.size.y);
                Target = AIEPathing.OffsetFromSea(Target, tank, help);
                Target = AIEPathing.ModerateMaxAlt(Target, help);
            }
            controller.PathPointSet = Target;

            // Planned pathing
            if (!help.Attempt3DNavi && PlanningPathing(Target, core.DrivePathing))
                return true;

            // Immedeate Pathing
            return ImmedeatePathingEnemy(mind, Target, core.DrivePathing);
        }



        public bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, ref EControlCoreSet core)
        {
            // DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " normal drive was called");
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

            control3D.m_State.m_InputRotation = Vector3.zero;
            control3D.m_State.m_InputMovement = Vector3.zero;
            controlGet.SetValue(tank.control, control3D);
            Vector3 destDirect = controller.PathPoint - tank.boundsCentreWorldNoCheck;

            //DebugTAC_AI.Log("IS target player " + Singleton.playerTank == thisInst.lastEnemy + " | MinimumRad " + thisInst.MinimumRad + " | destination" + controller.PathPoint);
            // DRIVE
            thisControl.DriveControl = 0f;

            float range = thisInst.lastOperatorRange;
            if (thisInst.lastEnemyGet)
                range = thisInst.lastCombatRange;

            switch (core.DriveDir)
            {
                case EDriveFacing.Stop:
                    thisControl.DriveControl = 0f;
                    return true;
                case EDriveFacing.Neutral:
                    thisControl.DriveControl = 0.001f;
                    return true;
                case EDriveFacing.Forwards:
                    if (core.DriveDest >= EDriveDest.ToLastDestination)
                    {
                        if (thisInst.MinimumRad > 0)
                        {
                            if (range < thisInst.MinimumRad - 1)
                                thisControl.DriveControl = -1f;
                            else if (range > thisInst.MinimumRad + 1)
                                thisControl.DriveControl = 1f;
                            else
                                thisControl.DriveControl = 0f;
                        }
                        else
                            thisControl.DriveControl = 1f;
                    }
                    else if (core.DriveDest == EDriveDest.FromLastDestination)
                    {
                        thisControl.DriveControl = -1f;
                    }
                    break;
                case EDriveFacing.Perpendicular:
                    if (core.DriveDest >= EDriveDest.ToLastDestination)
                    {
                        if (thisInst.MinimumRad > 0)
                        {
                            if (range > thisInst.MinimumRad + 1)
                                thisControl.DriveControl = 1f;
                            else
                                thisControl.DriveControl = 0f;
                        }
                        else
                            thisControl.DriveControl = 1f;
                    }
                    else if (core.DriveDest == EDriveDest.FromLastDestination)
                    {
                        thisControl.DriveControl = 1f;
                    }
                    break;
                case EDriveFacing.Backwards:
                    if (core.DriveDest >= EDriveDest.ToLastDestination)
                    {
                        if (thisInst.MinimumRad > 0)
                        {
                            if (range < thisInst.MinimumRad - 1)
                                thisControl.DriveControl = 1f;
                            else if (range > thisInst.MinimumRad + 1)
                                thisControl.DriveControl = -1f;
                            else
                                thisControl.DriveControl = 0f;
                        }
                        else
                            thisControl.DriveControl = -1f;
                    }
                    else if (core.DriveDest == EDriveDest.FromLastDestination)
                    {
                        thisControl.DriveControl = 1f;
                    }
                    break;
                default:
                    thisControl.DriveControl = 0f;
                    break;
            }

            // Additional drive controls
            if (thisInst.PivotOnly)
            {
                thisControl.DriveControl = 0;
            }
            else if (thisInst.Yield)
            {
                if (thisControl.DriveControl < 0)
                {
                    if (thisInst.recentSpeed < -10)
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
                    Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                    controller.TryBoost(thisControl, forwardLocal);
                }
                else if (thisInst.LightBoost)
                {
                    if (thisInst.LightBoostFeatheringClock >= 25)
                    {
                        Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                        controller.TryBoost(thisControl, forwardLocal);
                        thisInst.LightBoostFeatheringClock = 0;
                    }
                    thisInst.LightBoostFeatheringClock++;
                }
            }
            else if (thisInst.FullBoost)
            {
                Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                controller.TryBoost(thisControl, forwardLocal);
            }
            else if (thisInst.LightBoost)
            {
                if (thisInst.LightBoostFeatheringClock >= 25)
                {
                    Vector3 forwardLocal = tank.rootBlockTrans.InverseTransformDirection(destDirect).normalized;
                    controller.TryBoost(thisControl, forwardLocal);
                    thisInst.LightBoostFeatheringClock = 0;
                }
                thisInst.LightBoostFeatheringClock++;
            }

            if (thisInst.FirePROPS)
            {
                thisControl.BoostControlProps = true;
            }


            // STEERING
            if (thisInst.DoSteerCore)
            {
                float turnVal;
                switch (core.DriveDir)
                {
                    case EDriveFacing.Perpendicular:
                        if (core.DriveDest == EDriveDest.FromLastDestination)
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core, out turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            //DebugTAC_AI.Log("Sideways at target");
                        }
                        else
                        {
                            if (range < thisInst.MinimumRad + 2)
                            {
                                if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core, out turnVal))
                                    thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                                //DebugTAC_AI.Log("Orbiting out " + thisInst.MinimumRad + " | " + destDirect);
                            }
                            else if (range > thisInst.MinimumRad + 22)
                            {
                                if (VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core, out turnVal))
                                    thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                                //DebugTAC_AI.Log("Orbiting in " + thisInst.MinimumRad);
                            }
                            else  //ORBIT!
                            {
                                Vector3 aimDirect;
                                float angleControl = Mathf.InverseLerp(thisInst.MinimumRad + 2, thisInst.MinimumRad + 22, range) * 2 - 1;
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
                                if (VehicleUtils.Turner(thisControl, thisInst, aimDirect, ref core, out turnVal))
                                    thisControl.m_Movement.FaceDirection(tank, aimDirect, turnVal);
                                //DebugTAC_AI.Log("Orbiting hold " + thisInst.MinimumRad);
                            }
                        }
                        break;
                    case EDriveFacing.Backwards:
                        // Face back TOWARDS target
                        if (core.DriveDest == EDriveDest.FromLastDestination)
                        {   //Move from target
                            if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core, out turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            //DebugTAC_AI.Log("Forwards looking away from target");
                        }
                        else
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, ref core, out turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);//Face the music
                        }
                        break;
                    default:
                        // Face front TOWARDS target
                        if (core.DriveDest == EDriveDest.FromLastDestination)
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core, out turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                        }
                        else
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core, out turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);//Face the music
                                                                                                //DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  driving to " + controller.PathPoint); 
                        }
                        break;
                }
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
            return true;
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
                    targPos = Between(targPos, thisInst.theResource.tank.boundsCentreWorldNoCheck);
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
            if (thisInst.ChaseThreat && thisInst.lastEnemyGet.IsNotNull() && mind.CommanderMind != EnemyAttitude.OnRails)
            {
                output = true;
                Vector3 targPos = thisInst.LeadTargetAiming(thisInst.lastEnemyGet);
                core.DriveDir = EDriveFacing.Forwards;
                thisInst.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((thisInst.lastCombatRange - mind.MinCombatRange) / 3f, -1, 1);

                if (mind.CommanderAttack == EAttackMode.Circle)
                {   // works fine for now
                    if (thisInst.SideToThreat)
                        core.DriveDir = EDriveFacing.Perpendicular;
                    else
                        core.DriveDir = EDriveFacing.Forwards;
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!;
                        pos = targPos;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        pos = thisInst.AvoidAssist(targPos);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = thisInst.AvoidAssist(targPos);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else
                    {
                        pos = thisInst.AvoidAssist(targPos);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    //DebugTAC_AI.Log("DriveDyna is " + driveDyna);
                }
                else
                {   // Since the enemy also uses it's Operator in combat, this will have to listen to that
                    if (thisInst.IsDirectedMovingFromDest)
                    {
                        pos = thisInst.AvoidAssistInv(targPos);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.IsDirectedMovingToDest && mind.LikelyMelee)
                    {
                        if (mind.LikelyMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = thisInst.AvoidAssist(targPos);
                            thisInst.MinimumRad = 0.5f;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pos = thisInst.AvoidAssist(targPos);
                            thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 5;
                        }
                    }
                    else
                    {
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 5;
                    }
                }
            }
            else
                thisInst.IgnoreEnemyDistance();
            controller.PathPointSet = pos;
            return output;
        }


        public Vector3 Between(Vector3 Target, Vector3 other)
        {
            return (Target + other) / 2;
        }

    }
}
