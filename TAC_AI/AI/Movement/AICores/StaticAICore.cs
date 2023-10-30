using System;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    internal class StaticAICore : IMovementAICore
    {
        private AIControllerStatic controller;
        private Tank tank;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerStatic)controller;
            this.tank = tank;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GroundOffsetGeneralAir;
            //DebugTAC_AI.Log("TACtical_AI: StaticAICore - Init");
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            throw new NotImplementedException(GetType().Name + " should not be calling AvoidAssist pathfinding");
        }

        public bool DriveDirector(ref EControlCoreSet core)
        {
            var help = controller.Helper;
            // DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " drive was called");
            try
            {
                help.PivotOnly = true;
                if (help.ApproachingTech)
                {
                    controller.HoldHeight = help.ApproachingTech.tank.boundsCentreWorldNoCheck.y;
                }
                else
                    controller.HoldHeight = ManWorld.inst.ProjectToGround(tank.boundsCentreWorldNoCheck, true).y + (help.lastTechExtents * 2);

                if (!TryAdjustForCombat(true, ref controller.AimTarget, ref core))
                {
                    controller.AimTarget = help.lastDestinationCore;
                }
            }
            catch (Exception e)
            {
                try
                {
                    DebugTAC_AI.Log("TACtical_AI: ERROR IN StaticAICore");
                    DebugTAC_AI.Log("TACtical_AI: Tank - " + tank.name);
                    DebugTAC_AI.Log("TACtical_AI: Helper - " + (bool)controller.Helper);
                    DebugTAC_AI.Log("TACtical_AI: AI Main Mode - " + tank.AI.GetAICategory().ToString());
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes tree))
                        DebugTAC_AI.Log("TACtical_AI: AI Tree Mode - " + tree.ToString());
                    DebugTAC_AI.Log("TACtical_AI: Last AI Tree Mode - " + help.lastAIType.ToString());
                    DebugTAC_AI.Log("TACtical_AI: Player - " + help.lastPlayer.tank.name);
                    if ((bool)help.lastEnemyGet)
                        DebugTAC_AI.Log("TACtical_AI: Target - " + help.lastEnemyGet.tank.name);
                    DebugTAC_AI.Log("TACtical_AI: " + e);
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Missing variable(s)");
                }
            }
            return true;
        }

        public bool DriveDirectorRTS(ref EControlCoreSet core)
        {
            DriveDirector(ref core);
            // DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " drive was called");DriveDirector()
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind, ref EControlCoreSet core)
        {
            var help = controller.Helper;
            if (mind.IsNull())
                return false;

            help.PivotOnly = true;
            if (help.ApproachingTech)
            {
                controller.HoldHeight = help.ApproachingTech.tank.boundsCentreWorldNoCheck.y;
            }
            else
                controller.HoldHeight = ManWorld.inst.ProjectToGround(tank.boundsCentreWorldNoCheck, true).y + (help.lastTechExtents * 2);

            if (!TryAdjustForCombatEnemy(mind, ref controller.AimTarget, ref core))
            {
                controller.AimTarget = tank.boundsCentreWorldNoCheck + (controller.IdleFacingDirect * 200).ToVector3XZ(0);
            }
            core.lastDestination = controller.PathPoint;
            return true;
        }

        public bool DriveMaintainer(TankControl thisControl, TankAIHelper thisInst, Tank tank, ref EControlCoreSet core)
        {
            // DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " normal drive was called");
            if (tank.Anchors.Fixed)
            {   // Static base
                return true;
            }
            else if (tank.IsSkyAnchored)
            {   //3D movement
                SkyMaintainer(thisControl, ref core);
            }
            else //Land movement
            {
                Vector3 TurnVal = Vector3.zero;


                Vector3 destDirect = controller.AimTarget - tank.boundsCentreWorldNoCheck;
                thisControl.DriveControl = 0;
                if (thisInst.DoSteerCore)
                {
                    VehicleUtils.Turner(thisControl, thisInst, destDirect, ref core);
                }
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, destDirect * thisInst.lastTechExtents, new Color(1, 0, 1));

                Vector3 InputLineVal = Vector3.zero;
                if (!thisInst.techIsApproaching)
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
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, InputLineVal * thisInst.lastTechExtents, new Color(0, 1, 1));
                }
                else
                {
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, InputLineVal, new Color(0, 1, 1));
                }

                Vector3 DriveVal = InputLineVal;
                thisControl.CollectMovementInput(DriveVal, TurnVal, Vector3.zero, false, false);
            }
            return true;
        }

        public void SkyMaintainer(TankControl thisControl, ref EControlCoreSet core)
        {
            TankAIHelper thisInst = controller.Helper;

            float driveMultiplier = 0;

            //AI Steering Rotational
            Vector3 distDiff = controller.PathPoint - tank.boundsCentreWorldNoCheck;
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
            forwardFlat = forwardFlat.normalized;
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

            thisInst.Navi3DDirect = Vector3.zero;
            thisInst.Navi3DUp = Vector3.up;
            Vector3 TurnVal = Vector3.zero;
            if (thisInst.DoSteerCore)
            {
                TurnVal = turnVal.Clamp01Box();
                if (thisInst.lastEnemyGet.IsNotNull())
                {
                    thisInst.Navi3DDirect = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    thisControl.m_Movement.FacePosition(tank, controller.AimTarget, 1);
                }
            }
            else
                TurnVal = Vector3.zero;

            //AI Drive Translational
            Vector3 driveVal;
            if (thisInst.techIsApproaching)
            {
                driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized;
            }
            else if (thisInst.lastEnemyGet.IsNotNull() && !thisInst.IsMultiTech && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
            {   //level alt with enemy
                controller.HoldHeight = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck.y + 4;
                driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized;
                if (tank.IsFriendly() && thisInst.lastPlayer.IsNotNull())
                {
                    if (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.MaxCombatRange / 3) < thisInst.tank.boundsCentreWorldNoCheck.y)
                        driveVal.y = -1;
                }
                else if (controller.HoldHeight + (thisInst.GroundOffsetHeight / 2) > thisInst.tank.boundsCentreWorldNoCheck.y)
                {
                    driveVal.y = Mathf.Clamp((controller.HoldHeight + (thisInst.GroundOffsetHeight / 3) - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
                }
                else
                {
                    driveVal.y = -1;
                }
            }
            else
            {
                float range = thisInst.lastOperatorRange;
                if (range < thisInst.MinimumRad - 1)
                {
                    driveMultiplier = 1f;
                    driveVal = tank.rootBlockTrans.InverseTransformVector(controller.PathPoint - tank.boundsCentreWorldNoCheck).normalized * 0.3f;
                }
                else if (range > thisInst.MinimumRad + 1)
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
            if (!thisInst.IsMultiTech && CloseToGroundWarning)
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

                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, driveVal * thisInst.lastTechExtents, new Color(0, 0, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, DriveVal * thisInst.lastTechExtents, new Color(1, 0, 0));

                thisControl.CollectMovementInput(DriveVal, TurnVal, Vector3.zero, false, false);
                return;
            }
            //thisInst.MinimumRad
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

            // DEBUG FOR DRIVE ERRORS
            if (tank.IsAnchored)
            {
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, distDiff, new Color(0, 1, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, driveVal * thisInst.lastTechExtents, new Color(0, 0, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, DriveVal * thisInst.lastTechExtents, new Color(1, 0, 0));
            }
            else if (thisInst.AttackEnemy && thisInst.lastEnemyGet)
            {
                if (thisInst.lastEnemyGet.tank.IsEnemy(tank.Team))
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, thisInst.lastEnemyGet.centrePosition - tank.trans.position, new Color(0, 1, 1));
            }
            thisControl.CollectMovementInput(DriveVal, TurnVal, Vector3.zero, false, false);
        }

        public bool TryAdjustForCombat(bool between, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper thisInst = controller.Helper;
            bool output = false;
            if (thisInst.lastEnemyGet.IsNotNull())
            {
                Vector3 targPos = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                output = true;
                thisInst.UpdateEnemyDistance(targPos);

                core.DriveDir = EDriveFacing.Forwards;
                pos = targPos;
                thisInst.MinimumRad = 0;
            }
            else
                thisInst.IgnoreEnemyDistance();
            return output;
        }
        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper thisInst = controller.Helper;
            bool output = false;
            if (thisInst.lastEnemyGet.IsNotNull() && mind.CommanderMind != Enemy.EnemyAttitude.OnRails)
            {
                output = true;
                thisInst.UpdateEnemyDistance(thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck);

                core.DriveDir = EDriveFacing.Forwards;
                pos = RCore.GetTargetCoordinates(thisInst, thisInst.lastEnemyGet, mind);
            }
            else
                thisInst.IgnoreEnemyDistance();
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
