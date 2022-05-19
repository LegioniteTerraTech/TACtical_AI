using System;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    public class StaticAICore : IMovementAICore
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
        private AIControllerStatic controller;
        private Tank tank;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerStatic)controller;
            this.tank = tank;
            controller.Helper.GroundOffsetHeight = controller.Helper.lastTechExtents + AIGlobals.GeneralAirGroundOffset;
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            throw new NotImplementedException(GetType().Name + " should not be calling AvoidAssist pathfinding");
        }

        public bool DriveDirector()
        {
            var help = controller.Helper;
            // Debug.Log("TACtical_AI: Tech " + tank.name + " drive was called");
            try
            {
                help.PivotOnly = true;
                if (help.ApproachingTech)
                {
                    controller.HoldHeight = help.ApproachingTech.tank.boundsCentreWorldNoCheck.y;
                }
                else
                    controller.HoldHeight = ManWorld.inst.ProjectToGround(tank.boundsCentreWorldNoCheck, true).y + (help.lastTechExtents * 2);

                bool Combat = TryAdjustForCombat(true);
                if (!Combat)
                {
                    controller.AimTarget = tank.boundsCentreWorldNoCheck + (controller.IdleLookDirect * 200).ToVector3XZ(0);
                }
                help.lastDestination = controller.MovePosition;

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
                    if ((bool)help.lastEnemy)
                        DebugTAC_AI.Log("TACtical_AI: Target - " + help.lastEnemy.tank.name);
                    DebugTAC_AI.Log("TACtical_AI: " + e);
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Missing variable(s)");
                }
            }
            return true;
        }

        public bool DriveDirectorRTS()
        {
            var help = controller.Helper;
            DriveDirector();
            // Debug.Log("TACtical_AI: Tech " + tank.name + " drive was called");DriveDirector()
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind)
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


            bool Combat = TryAdjustForCombatEnemy(mind);
            if (!Combat)
            {
                controller.AimTarget = tank.boundsCentreWorldNoCheck + (controller.IdleLookDirect * 200).ToVector3XZ(0);
            }
            help.lastDestination = controller.MovePosition;
            return true;
        }

        public bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Debug.Log("TACtical_AI: Tech " + tank.name + " normal drive was called");
            if (tank.Anchors.Fixed)
            {   // Static base
                return true;
            }
            else if (tank.IsSkyAnchored)
            {   //3D movement
                SkyMaintainer(thisControl);
            }
            else //Land movement
            {
                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

                control3D.m_State.m_InputRotation = Vector3.zero;

                Vector3 InputLineVal = Vector3.zero;
                if (!thisInst.techIsApproaching)
                {
                    InputLineVal = tank.rootBlockTrans.InverseTransformVector(tank.boundsCentreWorldNoCheck - controller.MovePosition);
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

                control3D.m_State.m_InputMovement = InputLineVal;
                controlGet.SetValue(tank.control, control3D);
            }
            return true;
        }

        public void SkyMaintainer(TankControl thisControl)
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

            float driveMultiplier = 0;

            //AI Steering Rotational
            Vector3 distDiff = controller.SceneStayPos.ToVector3XZ(controller.HoldHeight) - tank.boundsCentreWorldNoCheck;
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
            forwardFlat = forwardFlat.normalized;
            turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;

            Vector3 turnValUp = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(forwardFlat.normalized), tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;
            if (thisInst.Navi3DUp == Vector3.up)
            {
                //Debug.Log("TACtical_AI: Forwards");
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
                    //Debug.Log("TACtical_AI: Broadside overloaded with value " + Vector3.Dot(thisInst.Navi3DUp, tank.rootBlockTrans.up));
                }
                else
                {
                    //Debug.Log("TACtical_AI: Broadside Z-tilt active");
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

                //Debug.Log("TACtical_AI: TurnVal AIM " + turnVal);

            thisInst.Navi3DDirect = Vector3.zero;
            thisInst.Navi3DUp = Vector3.up;
            if (thisInst.Steer)
            {
                control3D.m_State.m_InputRotation = turnVal.Clamp01Box();
                if (thisInst.lastEnemy.IsNotNull())
                {
                    thisInst.Navi3DDirect = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    thisControl.m_Movement.FacePosition(tank, controller.AimTarget, 1);
                }
            }
            else
                control3D.m_State.m_InputRotation = Vector3.zero;

            //AI Drive Translational
            Vector3 driveVal;
            if (thisInst.techIsApproaching)
            {
                driveVal = tank.rootBlockTrans.InverseTransformVector(controller.MovePosition - tank.boundsCentreWorldNoCheck).normalized;
            }
            else if (thisInst.lastEnemy.IsNotNull() && !thisInst.IsMultiTech && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
            {   //level alt with enemy
                controller.HoldHeight = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck.y + 4;
                driveVal = tank.rootBlockTrans.InverseTransformVector(controller.MovePosition - tank.boundsCentreWorldNoCheck).normalized;
                if (tank.IsFriendly() && thisInst.lastPlayer.IsNotNull())
                {
                    if (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.RangeToChase / 3) < thisInst.tank.boundsCentreWorldNoCheck.y)
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
                float range = thisInst.lastRange;
                if (range < thisInst.MinimumRad - 1)
                {
                    driveMultiplier = 1f;
                    driveVal = tank.rootBlockTrans.InverseTransformVector(controller.MovePosition - tank.boundsCentreWorldNoCheck).normalized * 0.3f;
                }
                else if (range > thisInst.MinimumRad + 1)
                {
                    driveVal = tank.rootBlockTrans.InverseTransformVector(controller.MovePosition - tank.boundsCentreWorldNoCheck).normalized;
                    if (thisInst.DriveDir == EDriveFacing.Forwards || thisInst.DriveDir == EDriveFacing.Backwards)
                        driveMultiplier = 1f;
                    else
                        driveMultiplier = 0.4f;
                }
                else
                    driveVal = tank.rootBlockTrans.InverseTransformVector(controller.MovePosition - tank.boundsCentreWorldNoCheck).normalized;
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
            Vector3 final = (driveVal * Mathf.Clamp(distDiff.magnitude / AIGlobals.StationaryMoveDampening, 0, 1) * driveMultiplier).Clamp01Box();

            if (thisInst.DriveDir != EDriveFacing.Neutral)
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

            control3D.m_State.m_InputMovement = final.Clamp01Box();

            // DEBUG FOR DRIVE ERRORS
            if (tank.IsAnchored)
            {
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, distDiff, new Color(0, 1, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, driveVal * thisInst.lastTechExtents, new Color(0, 0, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, control3D.m_State.m_InputMovement * thisInst.lastTechExtents, new Color(1, 0, 0));
            }
            else if (thisInst.AttackEnemy && thisInst.lastEnemy)
            {
                if (thisInst.lastEnemy.tank.IsEnemy(tank.Team))
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, thisInst.lastEnemy.centrePosition - tank.trans.position, new Color(0, 1, 1));
            }
            controlGet.SetValue(tank.control, control3D);
        }

        public bool TryAdjustForCombat(bool between)
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            bool output = false;
            if (thisInst.PursueThreat && (!thisInst.IsMovingAnyDest || !thisInst.Retreat) && thisInst.lastEnemy.IsNotNull())
            {
                Vector3 targPos = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                output = true;
                thisInst.Steer = true;
                thisInst.lastRangeEnemy = (targPos - tank.boundsCentreWorldNoCheck).magnitude;
                thisInst.lastRange = thisInst.lastRangeEnemy;

                thisInst.DriveDir = EDriveFacing.Forwards;
                controller.AimTarget = targPos;
                thisInst.MinimumRad = 0;
            }
            else
                thisInst.lastRangeEnemy = float.MaxValue;
            return output;
        }

        public bool TryAdjustForCombatEnemy(EnemyMind mind)
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            bool output = false;
            if (!thisInst.Retreat && thisInst.lastEnemy.IsNotNull() && mind.CommanderMind != Enemy.EnemyAttitude.OnRails)
            {
                output = true;
                thisInst.Steer = true;
                thisInst.lastRangeEnemy = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude;

                thisInst.DriveDir = EDriveFacing.Forwards;
                controller.AimTarget = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
            }
            else
                thisInst.lastRangeEnemy = float.MaxValue;
            return output;
        }


        private const float ignoreTurning = 0.875f;
        private const float MinThrottleToTurnFull = 0.75f;
        private const float MaxThrottleToTurnAccurate = 0.25f;
        public static bool StaticTurner(TankControl thisControl, AIECore.TankAIHelper helper, Vector3 destinationVec, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.normalized.ToVector2XZ(), helper.tank.rootBlockTrans.forward.ToVector2XZ());

            if (forwards > ignoreTurning && thisControl.DriveControl >= MinThrottleToTurnFull)
                return false;
            else
            {
                if (helper.DriveDir == EDriveFacing.Perpendicular)
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
                    if (thisControl.DriveControl <= MaxThrottleToTurnAccurate)
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
