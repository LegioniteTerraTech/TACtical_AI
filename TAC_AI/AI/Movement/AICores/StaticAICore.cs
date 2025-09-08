using System;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using UnityEngine;
using TerraTechETCUtil;
using static HarmonyLib.Code;

namespace TAC_AI.AI.Movement.AICores
{
    internal class StaticAICore : IMovementAICore
    {
        private AIControllerStatic controller;
        private Tank tank;
        public float GetDrive => 0;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerStatic)controller;
            this.tank = tank;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GroundOffsetGeneralAir;
            //DebugTAC_AI.Log(KickStart.ModID + ": StaticAICore - Init");
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            throw new NotImplementedException(GetType().Name + " should not be calling AvoidAssist pathfinding");
        }

        public bool DriveDirector(ref EControlCoreSet core)
        {
            var helper = controller.Helper;
            // DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " drive was called");
            try
            {
                helper.ThrottleState = AIThrottleState.PivotOnly;
                if (helper.ApproachingTech)
                {
                    controller.HoldHeight = helper.ApproachingTech.tank.boundsCentreWorldNoCheck.y;
                }
                else
                    controller.HoldHeight = ManWorld.inst.ProjectToGround(tank.boundsCentreWorldNoCheck, true).y + (helper.lastTechExtents * 2);

                if (!TryAdjustForCombat(true, ref controller.AimTarget, ref core))
                {
                    controller.AimTarget = helper.lastDestinationCore;
                }
            }
            catch (Exception e)
            {
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ERROR IN StaticAICore");
                    DebugTAC_AI.Log(KickStart.ModID + ": Tank - " + tank.name);
                    DebugTAC_AI.Log(KickStart.ModID + ": Helper - " + (bool)controller.Helper);
                    DebugTAC_AI.Log(KickStart.ModID + ": AI Main Mode - " + tank.AI.GetAICategory().ToString());
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes tree))
                        DebugTAC_AI.Log(KickStart.ModID + ": AI Tree Mode - " + tree.ToString());
                    DebugTAC_AI.Log(KickStart.ModID + ": Last AI Tree Mode - " + helper.lastAIType.ToString());
                    DebugTAC_AI.Log(KickStart.ModID + ": Player - " + helper.lastPlayer.tank.name);
                    if ((bool)helper.lastEnemyGet)
                        DebugTAC_AI.Log(KickStart.ModID + ": Target - " + helper.lastEnemyGet.tank.name);
                    DebugTAC_AI.Log(KickStart.ModID + ": " + e);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Missing variable(s)");
                }
            }
            return true;
        }

        public bool DriveDirectorRTS(ref EControlCoreSet core)
        {
            DriveDirector(ref core);
            // DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " drive was called");DriveDirector()
            return true;
        }
        public bool DriveDirectorEnemyRTS(EnemyMind mind, ref EControlCoreSet core)
        {
            DriveDirectorEnemy(mind, ref core);
            // DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " drive was called");DriveDirector()
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind, ref EControlCoreSet core)
        {
            var helper = controller.Helper;
            if (mind.IsNull())
                return false;

            helper.ThrottleState = AIThrottleState.PivotOnly;
            if (helper.ApproachingTech)
            {
                controller.HoldHeight = helper.ApproachingTech.tank.boundsCentreWorldNoCheck.y;
            }
            else
                controller.HoldHeight = ManWorld.inst.ProjectToGround(tank.boundsCentreWorldNoCheck, true).y + (helper.lastTechExtents * 2);

            if (!TryAdjustForCombatEnemy(mind, ref controller.AimTarget, ref core))
            {
                controller.AimTarget = tank.boundsCentreWorldNoCheck + (controller.IdleFacingDirect * 200).ToVector3XZ(0);
            }
            core.lastDestination = controller.PathPoint;
            return true;
        }

        public bool DriveMaintainer(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
            // DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " normal drive was called");
            /*
            if (tank.Anchors.NumAnchored)
            {   // Static base
                return true;
            }
            else // */
            if (tank.IsSkyAnchored)
            {   //3D movement
                SkyMaintainer(ref core);
            }
            else //Land movement
            {
                Vector3 TurnVal = Vector3.zero;


                Vector3 destDirect = controller.AimTarget - tank.boundsCentreWorldNoCheck;
                helper.DriveControl = 0;
                if (helper.DoSteerCore)
                {
                    VehicleUtils.Turner(helper, destDirect, 0, ref core);
                }
                if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, destDirect * helper.lastTechExtents, new Color(1, 0, 1));

                Vector3 InputLineVal = Vector3.zero;
                if (!helper.techIsApproaching)
                {
                    InputLineVal = tank.rootBlockTrans.InverseTransformVector(tank.boundsCentreWorldNoCheck - controller.PathPoint);
                    InputLineVal /= AIGlobals.StationaryMoveDampening;
                }

                if (tank.control.AnyThrottleInAxes(Vector3.one))
                {
                    if (tank.control.GetThrottle(0, out float throttleX))
                    {   // X 
                        InputLineVal.x = throttleX;
                    }
                    if (tank.control.GetThrottle(1, out float throttleY))
                    {   // Y
                        InputLineVal.y = throttleY;
                    }
                    if (tank.control.GetThrottle(2, out float throttleZ))
                    {   // X
                        InputLineVal.z = throttleZ;
                    }
                    if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, InputLineVal * helper.lastTechExtents, new Color(0, 1, 1));
                }
                else
                {
                    if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, InputLineVal, new Color(0, 1, 1));
                }

                Vector3 DriveVal = InputLineVal;
                helper.ProcessControl(DriveVal, TurnVal, Vector3.zero, false, false);
            }
            return true;
        }

        public void SkyMaintainer(ref EControlCoreSet core)
        {
            TankAIHelper helper = controller.Helper;

            float driveMultiplier = 0;

            //AI Steering Rotational
            Vector3 distDiff = controller.PathPoint - tank.boundsCentreWorldNoCheck;
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
            forwardFlat = forwardFlat.normalized;
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

            helper.Navi3DDirect = Vector3.zero;
            helper.Navi3DUp = Vector3.up;
            Vector3 TurnVal = Vector3.zero;
            if (helper.DoSteerCore)
            {
                TurnVal = turnVal.Clamp01Box();
                if (helper.lastEnemyGet.IsNotNull())
                {
                    helper.Navi3DDirect = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    helper.SteerControl(controller.AimTarget, 1);
                }
            }
            else
                TurnVal = Vector3.zero;

            //AI Drive Translational
            Vector3 driveVal;
            if (helper.techIsApproaching)
            {
                driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized;
            }
            else if (helper.lastEnemyGet.IsNotNull() && !helper.IsMultiTech && 
                AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck.y))
            {   //level alt with enemy
                controller.HoldHeight = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck.y + 4;
                driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized;
                if (tank.IsFriendly() && helper.lastPlayer.IsNotNull())
                {
                    if (helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (helper.MaxCombatRange / 3) < helper.tank.boundsCentreWorldNoCheck.y)
                        driveVal.y = -1;
                }
                else if (controller.HoldHeight + (helper.GroundOffsetHeight / 2) > helper.tank.boundsCentreWorldNoCheck.y)
                {
                    driveVal.y = Mathf.Clamp((controller.HoldHeight + (helper.GroundOffsetHeight / 3) - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
                }
                else
                {
                    driveVal.y = -1;
                }
            }
            else
            {
                float range = helper.lastOperatorRange;
                if (range < helper.AutoSpacing - 1)
                {
                    driveMultiplier = 1f;
                    driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized * 0.3f;
                }
                else if (range > helper.AutoSpacing + 1)
                {
                    driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized;
                    if (core.DriveDir == EDriveFacing.Forwards || core.DriveDir == EDriveFacing.Backwards)
                        driveMultiplier = 1f;
                    else
                        driveMultiplier = 0.4f;
                }
                else
                    driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized;
            }

            bool EmergencyUp = false;
            bool CloseToGroundWarning = false;
            if (ManWorld.inst.GetTerrainHeight(tank.boundsCentreWorldNoCheck, out float height))
            {
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
            if (!helper.IsMultiTech && CloseToGroundWarning)
            {
                if (driveVal.y >= -0.5f && driveVal.y < 0f)
                    driveVal.y = 0; // prevent airships from slam-dunk
                else if (driveVal.y != -1)
                {
                    driveVal.y += 0.5f;
                }
            }
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
                if (helper.FixControlReversal(DriveVal.z))
                    TurnVal = TurnVal.SetY(-TurnVal.y);
                helper.ProcessControl(DriveVal, TurnVal, Vector3.zero, false, false);
                return;
            }
            //helper.MinimumRad
            // Prevent drifting
            Vector3 final = (driveVal * Mathf.Clamp(distDiff.magnitude / AIGlobals.StationaryMoveDampening, 0, 1) * driveMultiplier).Clamp01Box();

            if (core.DriveDir > EDriveFacing.Neutral)
            {
                if (final.y.Approximately(0, 0.2f))
                    final.y = 0;
                if (final.x.Approximately(0, 0.15f))
                    final.x = 0;
                if (final.z.Approximately(0, 0.15f))
                    final.z = 0;
            }

            if (tank.control.GetThrottle(0, out float throttleX))
            {   // X 
                final.x = throttleX;
            }
            if (tank.control.GetThrottle(1, out float throttleY))
            {   // Y
                final.y = throttleY;
            }
            if (tank.control.GetThrottle(2, out float throttleZ))
            {   // X
                final.z = throttleZ;
            }

            DriveVal = final.Clamp01Box();

            if (Templates.DebugRawTechSpawner.ShowDebugFeedBack)
            {
                // DEBUG FOR DRIVE ERRORS
                if (tank.IsAnchored)
                {
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, distDiff, new Color(0, 1, 1));
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 1, driveVal * helper.lastTechExtents, new Color(0, 0, 1));
                    DebugExtUtilities.DrawDirIndicator(tank.gameObject, 2, DriveVal * helper.lastTechExtents, new Color(1, 0, 0));
                }
                else if (helper.AttackEnemy && helper.lastEnemyGet)
                {
                    if (ManBaseTeams.IsEnemy(tank.Team, helper.lastEnemyGet.tank.Team))
                        DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, helper.lastEnemyGet.centrePosition - tank.trans.position, new Color(0, 1, 1));
                }
            }
            if (helper.FixControlReversal(DriveVal.z))
                TurnVal = TurnVal.SetY(-TurnVal.y); 
            helper.ProcessControl(DriveVal, TurnVal, Vector3.zero, false, false);
        }

        public bool TryAdjustForCombat(bool between, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper helper = controller.Helper;
            bool output = false;
            if (helper.lastEnemyGet.IsNotNull())
            {
                Vector3 targPos = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                output = true;
                helper.UpdateEnemyDistance(targPos);

                core.DriveDir = EDriveFacing.Forwards;
                pos = targPos;
                helper.AutoSpacing = 0;
            }
            else
                helper.IgnoreEnemyDistance();
            return output;
        }
        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper helper = controller.Helper;
            bool output = false;
            if (helper.lastEnemyGet.IsNotNull() && mind.CommanderMind != Enemy.EnemyAttitude.OnRails)
            {
                output = true;
                helper.UpdateEnemyDistance(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);

                core.DriveDir = EDriveFacing.Forwards;
                pos = RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind);
            }
            else
                helper.IgnoreEnemyDistance();
            return output;
        }



        private const float ignoreTurning = 0.875f;
        private const float MinThrottleToTurnFull = 0.75f;
        private const float MaxThrottleToTurnAccurate = 0.25f;
        public static bool StaticTurner(TankControl thisControl, TankAIHelper helper, Vector3 destinationVec, ref EControlCoreSet core, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.normalized.ToVector2XZ(), helper.tank.rootBlockTrans.forward.ToVector2XZ());

            if (forwards > ignoreTurning && thisControl.CurState.m_InputMovement.z >= MinThrottleToTurnFull)
                return false;
            else
            {
                if (core.DriveDir == EDriveFacing.Perpendicular)
                {
                    if (!(bool)helper.lastCloseAlly)
                    {
                        float strength = 1 - forwards;
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                    else if (forwards > 0.65f)
                    {
                        float strength = 1 - (forwards / 1.5f);
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                else
                {
                    if (thisControl.CurState.m_InputMovement.z <= MaxThrottleToTurnAccurate)
                    {
                        if (!(bool)helper.lastCloseAlly && forwards > 0.7f)
                        {
                            float strength = 1 - Mathf.Log10(1 + (forwards * 9));
                            turnVal = Mathf.Clamp(strength, 0, 1);
                        }
                    }
                    else if (!(bool)helper.lastCloseAlly && forwards > 0.7f)
                    {
                        float strength = 1 - forwards;
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                return true;
            }
        }
    }
}
