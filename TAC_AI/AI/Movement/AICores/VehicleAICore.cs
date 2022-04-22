﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using TAC_AI.AI.Enemy;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    /// <summary> Handles both Wheeled and Space AI Directors & Maintainers </summary>
    public class VehicleAICore : IMovementAICore
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
        private AIControllerDefault controller;
        private Tank tank;

        public void Initiate(Tank tank, IMovementAIController controller)
        {
            this.controller = (AIControllerDefault) controller;
            this.tank = tank;
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            throw new NotImplementedException();
        }

        public bool DriveDirector()
        {
            var help = controller.Helper;
            // Debug.Log("TACtical_AI: Tech " + tank.name + " drive was called");
            try
            {
                if (help.IsMultiTech)
                {   //Override and disable most driving abilities
                    controller.ProcessedDest = MultiTechUtils.HandleMultiTech(help, tank);
                }
                else if (help.ProceedToBase)
                {
                    if (help.lastBasePos.IsNotNull())
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.MinimumRad = Mathf.Max(help.lastTechExtents - 2, 0.5f);
                        controller.ProcessedDest = help.AvoidAssistPrecise(help.lastBasePos.position);
                    }
                }
                else if (help.ProceedToMine)
                {
                    if (help.theResource.tank != null)
                    {
                        if (help.PivotOnly)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            controller.ProcessedDest = help.theResource.tank.boundsCentreWorldNoCheck;
                            help.MinimumRad = 0;
                        }
                        else
                        {
                            if (help.FullMelee)
                            {
                                help.Steer = true;
                                help.DriveDir = EDriveType.Forwards;
                                controller.ProcessedDest = help.AvoidAssistPrecise(help.theResource.tank.boundsCentreWorldNoCheck);
                                help.MinimumRad = 0;
                            }
                            else
                            {
                                help.Steer = true;
                                help.DriveDir = EDriveType.Forwards;
                                controller.ProcessedDest = help.AvoidAssistPrecise(help.theResource.tank.boundsCentreWorldNoCheck);
                                help.MinimumRad = help.lastTechExtents + 2;
                            }
                        }
                    }
                    else
                    {
                        if (help.PivotOnly)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            controller.ProcessedDest = help.theResource.trans.position;
                            help.MinimumRad = 0;
                        }
                        else
                        {
                            if (help.FullMelee)
                            {
                                help.Steer = true;
                                help.DriveDir = EDriveType.Forwards;
                                controller.ProcessedDest = help.AvoidAssistPrecise(help.theResource.trans.position);
                                help.MinimumRad = 0;
                            }
                            else
                            {
                                help.Steer = true;
                                help.DriveDir = EDriveType.Forwards;
                                controller.ProcessedDest = help.AvoidAssistPrecise(help.theResource.centrePosition);
                                help.MinimumRad = help.lastTechExtents + 2;
                            }
                        }
                    }
                }
                else if (help.DediAI == AIType.Aegis)
                {
                    help.theResource = AIEPathing.ClosestUnanchoredAlly(controller.Tank.boundsCentreWorldNoCheck, out float bestval, tank).visible;
                    bool Combat = TryAdjustForCombat();
                    if (!Combat)
                    {
                        if (help.MoveFromObjective && help.theResource.IsNotNull())
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.AdviseAway = true;
                            controller.ProcessedDest = help.theResource.transform.position;
                            help.MinimumRad = 0.5f;
                        }
                        else if (help.ProceedToObjective && help.theResource.IsNotNull())
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            controller.ProcessedDest = help.AvoidAssist(help.theResource.tank.transform.position);
                            help.MinimumRad = help.lastTechExtents + help.theResource.GetCheapBounds() + 5;
                        }
                        else
                        {
                            //Debug.Log("TACtical_AI: AI IDLE");
                        }
                    }
                }
                else
                {
                    bool Combat = TryAdjustForCombat();
                    if (!Combat)
                    {
                        if (help.MoveFromObjective && (bool)help.lastPlayer)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.AdviseAway = true;
                            controller.ProcessedDest = help.lastPlayer.tank.boundsCentreWorldNoCheck;//help.AvoidAssistInv(help.lastPlayer.transform.position);
                            help.MinimumRad = 0.5f;
                        }
                        else if (help.ProceedToObjective && (bool)help.lastPlayer)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            controller.ProcessedDest = help.AvoidAssist(help.lastPlayer.tank.boundsCentreWorldNoCheck);
                            help.MinimumRad = help.lastTechExtents + help.lastPlayer.GetCheapBounds() + 5;
                        }
                        else
                        {
                            help.PivotOnly = true;
                            //Debug.Log("TACtical_AI: AI IDLE");
                        }
                    }
                }
            }
            catch  (Exception e)
            {
                try
                {
                    Debug.Log("TACtical_AI: ERROR IN VehicleAICore");
                    Debug.Log("TACtical_AI: Tank - " + tank.name);
                    Debug.Log("TACtical_AI: Helper - " + (bool)controller.Helper);
                    Debug.Log("TACtical_AI: AI Main Mode - " + tank.AI.GetAICategory().ToString());
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes tree))
                        Debug.Log("TACtical_AI: AI Tree Mode - " + tree.ToString());
                    Debug.Log("TACtical_AI: Last AI Tree Mode - " + help.lastAIType.ToString());
                    Debug.Log("TACtical_AI: Player - " + help.lastPlayer.tank.name);
                    if ((bool)help.lastEnemy)
                        Debug.Log("TACtical_AI: Target - " + help.lastEnemy.tank.name);
                    Debug.Log("TACtical_AI: " + e);
                }
                catch
                {
                    Debug.Log("TACtical_AI: Missing variable(s)");
                }
            }
            if (help.Attempt3DNavi && !(help.FullMelee && help.lastEnemy.IsNotNull()))
                controller.ProcessedDest = AIEPathing.OffsetFromGround(controller.ProcessedDest, controller.Helper);
            else if (help.DriverType == AIDriverType.Sailor)
                controller.ProcessedDest = AIEPathing.OffsetToSea(controller.ProcessedDest, tank, controller.Helper);

            controller.ProcessedDest = AIEPathing.ModerateMaxAlt(controller.ProcessedDest, help);

            return true;
        }

        public bool DriveDirectorRTS()
        {
            var help = controller.Helper;
            // Debug.Log("TACtical_AI: Tech " + tank.name + " drive was called");
            try
            {
                help.Steer = true;
                help.MinimumRad = 0.5f;
                help.DriveDir = EDriveType.Forwards;
                if (help.IsMultiTech && help.DediAI != AIType.MTMimic)
                {   //Override and disable most driving abilities
                    if (help.DediAI == AIType.MTSlave && help.lastEnemy != null)
                    {   // act like a trailer
                        help.DriveDir = EDriveType.Neutral;
                        help.Steer = false;
                        controller.ProcessedDest = tank.boundsCentreWorldNoCheck;
                        help.MinimumRad = 0;
                    }
                    else if (help.DediAI == AIType.MTTurret && help.lastEnemy != null)
                    {
                        help.PivotOnly = true;
                        if (help.lastEnemy != null)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            controller.ProcessedDest = help.lastEnemy.tank.boundsCentreWorldNoCheck;
                            help.MinimumRad = 0;
                        }
                        else
                        {
                            help.Steer = false;
                            help.DriveDir = EDriveType.Neutral;
                            help.MinimumRad = 2;
                        }
                    }
                    else
                        controller.ProcessedDest = help.AvoidAssistPrecise(help.RTSDestination);
                }
                else
                {
                    help.Steer = true;
                    help.DriveDir = EDriveType.Forwards;
                    bool Combat = false;
                    if (help.RTSDestination == Vector3.zero)
                        Combat = TryAdjustForCombat(); //If we are set to chase then chase with proper AI
                    if (!Combat)
                    {
                        if (help.recentSpeed < 10 && help.lastRange < 32)
                        {
                            help.PivotOnly = true;
                            if (help.lastEnemy != null)
                            {
                                controller.ProcessedDest = help.lastEnemy.tank.boundsCentreWorldNoCheck;
                            }
                            else
                                controller.ProcessedDest = help.AvoidAssistPrecise(help.RTSDestination);
                        }
                        else
                            controller.ProcessedDest = help.AvoidAssistPrecise(help.RTSDestination);

                    }
                }
            }
            catch (Exception e)
            {
                try
                {
                    Debug.Log("TACtical_AI: ERROR IN VehicleAICore (RTS)");
                    Debug.Log("TACtical_AI: Tank - " + tank.name);
                    Debug.Log("TACtical_AI: Helper - " + (bool)controller.Helper);
                    Debug.Log("TACtical_AI: AI Main Mode - " + tank.AI.GetAICategory().ToString());
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes tree))
                        Debug.Log("TACtical_AI: AI Tree Mode - " + tree.ToString());
                    Debug.Log("TACtical_AI: Last AI Tree Mode - " + help.lastAIType.ToString());
                    Debug.Log("TACtical_AI: Player - " + help.lastPlayer.tank.name);
                    if ((bool)help.lastEnemy)
                        Debug.Log("TACtical_AI: Target - " + help.lastEnemy.tank.name);
                    Debug.Log("TACtical_AI: " + e);
                }
                catch
                {
                    Debug.Log("TACtical_AI: Missing variable(s)");
                }
            }
            if (help.Attempt3DNavi && !(help.FullMelee && help.lastEnemy.IsNotNull()))
                controller.ProcessedDest = AIEPathing.OffsetFromGround(controller.ProcessedDest, controller.Helper);
            else if (help.DriverType == AIDriverType.Sailor)
                controller.ProcessedDest = AIEPathing.OffsetToSea(controller.ProcessedDest, tank, controller.Helper);

            controller.ProcessedDest = AIEPathing.ModerateMaxAlt(controller.ProcessedDest, help);

            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind)
        {
            var help = controller.Helper;
            if (mind.IsNull())
                return false;
            if (help.ProceedToBase)
            {
                if (help.lastBasePos.IsNotNull())
                {
                    help.Steer = true;
                    help.DriveDir = EDriveType.Forwards;
                    controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(controller.Tank, help.lastBasePos.position, controller.Helper, mind);
                    help.MinimumRad = Mathf.Max(help.lastTechExtents - 2, 0.5f);
                }
            }
            else if (help.ProceedToMine)
            {
                if (help.PivotOnly)
                {
                    help.Steer = true;
                    help.DriveDir = EDriveType.Forwards;
                    controller.ProcessedDest = help.theResource.trans.position;
                    help.MinimumRad = 0;
                }
                else
                {
                    if (mind.MainFaction == FactionTypesExt.GC)
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(controller.Tank, help.theResource.trans.position, controller.Helper, mind);
                        help.MinimumRad = 0;
                    }
                    else
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(controller.Tank, help.theResource.trans.position, controller.Helper, mind);
                        help.MinimumRad = help.lastTechExtents + 2;
                    }
                }
            }
            else
            {
                bool Combat = TryAdjustForCombatEnemy(mind);
                if (!Combat)
                {
                    if (help.MoveFromObjective)
                    {
                        help.Steer = true;
                        if (help.Attempt3DNavi)
                            help.DriveDir = EDriveType.Forwards;
                        else
                            help.DriveDir = EDriveType.Backwards;
                        help.AdviseAway = true;
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemyInv(controller.Tank, help.lastDestination, controller.Helper, mind);
                        help.MinimumRad = 0.5f;
                    }
                    else if (help.ProceedToObjective)
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;

                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(controller.Tank, help.lastDestination, controller.Helper, mind);

                        if (mind.EvilCommander == EnemyHandling.Stationary)
                            help.MinimumRad = 0.5f;
                        else
                            help.MinimumRad = help.lastTechExtents + 8;
                    }
                }
            }
            if (mind.EvilCommander == EnemyHandling.Naval)
                controller.ProcessedDest = AIEPathing.OffsetToSea(controller.ProcessedDest, tank, controller.Helper);
            else if (mind.EvilCommander == EnemyHandling.Starship)
                controller.ProcessedDest = AIEPathing.OffsetFromGround(controller.ProcessedDest, controller.Helper);
            else
                controller.ProcessedDest = AIEPathing.OffsetFromGround(controller.ProcessedDest, controller.Helper, controller.Tank.blockBounds.size.y);
            if (mind.EvilCommander == EnemyHandling.Wheeled)
                controller.ProcessedDest = AIEPathing.OffsetFromSea(controller.ProcessedDest, tank, controller.Helper);

            controller.ProcessedDest = AIEPathing.ModerateMaxAlt(controller.ProcessedDest, help);
            help.lastDestination = controller.ProcessedDest;
            return true;
        }

        public bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Debug.Log("TACtical_AI: Tech " + tank.name + " normal drive was called");
            if (thisInst.Attempt3DNavi)
            {
                //3D movement
                this.SpaceMaintainer(thisControl);
            }
            else //Land movement
            {
                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

                control3D.m_State.m_InputRotation = Vector3.zero;
                control3D.m_State.m_InputMovement = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);
                Vector3 destDirect = controller.ProcessedDest - tank.boundsCentreWorldNoCheck;

                //Debug.Log("IS target player " + Singleton.playerTank == thisInst.lastEnemy + " | MinimumRad " + thisInst.MinimumRad + " | destination" + controller.ProcessedDest);
                thisControl.DriveControl = 0f;
                if (thisInst.Steer)
                {
                    if (thisInst.AdviseAway)
                    {   //Move from target
                        if (thisInst.DriveDir == EDriveType.Backwards)//EDriveType.Backwards
                        {   // Face back TOWARDS target
                            if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            thisControl.DriveControl = 1f;
                        }
                        else if (thisInst.DriveDir == EDriveType.Perpendicular)
                        {   // Still proceed away from target
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                            thisControl.DriveControl = 1f;
                        }
                        else
                        {   // Face front TOWARDS target
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                            thisControl.DriveControl = -1f;
                        }
                    }
                    else if (thisInst.DriveDir == EDriveType.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        //int range = (int)(destDirect).magnitude;
                        float range = thisInst.lastRange;
                        if (range < thisInst.MinimumRad + 2)
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, -destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            //Debug.Log("Orbiting out " + thisInst.MinimumRad + " | " + destDirect);
                        }
                        else if (range > thisInst.MinimumRad + 22)
                        {
                            if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                            //Debug.Log("Orbiting in " + thisInst.MinimumRad);
                        }
                        else  //ORBIT!
                        {
                            Vector3 aimDirect;
                            if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.right) < 0)
                                aimDirect = Vector3.Cross(destDirect.normalized, Vector3.down);
                            else
                                aimDirect = Vector3.Cross(destDirect.normalized, Vector3.up);
                            if (VehicleUtils.Turner(thisControl, thisInst, aimDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, aimDirect, turnVal);
                            //Debug.Log("Orbiting hold " + thisInst.MinimumRad);
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
                        //    thisControl.m_Movement.FacePosition(tank, controller.ProcessedDest, 1);// need max aiming strength for turning
                        //else
                        if (VehicleUtils.Turner(thisControl, thisInst, destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);//Face the music
                        //Debug.Log("TACtical_AI: AI " + tank.name + ":  driving to " + controller.ProcessedDest);
                        if (thisInst.MinimumRad > 0)
                        {
                            //if (thisInst.DriveDir == EDriveType.Perpendicular)
                            //    thisControl.DriveControl = 1f;
                            float range = thisInst.lastRange;
                            if (thisInst.DriveDir == EDriveType.Neutral)
                                thisControl.DriveControl = 0f;
                            else if (range < thisInst.MinimumRad - 1)
                            {
                                if (thisInst.DriveDir == EDriveType.Forwards)
                                    thisControl.DriveControl = -1f;
                                else if (thisInst.DriveDir == EDriveType.Backwards)
                                    thisControl.DriveControl = 1f;
                                else
                                    thisControl.DriveControl = 0;

                            }
                            else if (range > thisInst.MinimumRad + 1)
                            {
                                if (thisInst.DriveDir == EDriveType.Forwards)
                                    thisControl.DriveControl = 1f;
                                else if (thisInst.DriveDir == EDriveType.Backwards)
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
                if (thisInst.DriveDir == EDriveType.Neutral)
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
                    if (thisInst.DriveDir == EDriveType.Backwards)
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
                else if (thisInst.forceDrive)
                {
                    thisControl.DriveControl = thisInst.DriveVar;
                    if (thisInst.BOOST)
                    {
                        thisControl.DriveControl = 1;
                        if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                            controller.TryBoost(thisControl);
                    }
                    else if (thisInst.featherBoost)
                    {
                        if (thisInst.featherBoostersClock >= 25)
                        {
                            if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                                controller.TryBoost(thisControl);
                            thisInst.featherBoostersClock = 0;
                        }
                        thisInst.featherBoostersClock++;
                    }
                }
                else if (thisInst.BOOST)
                {
                    thisControl.DriveControl = 1;
                    if (Vector3.Dot(destDirect.SetY(0).normalized, tank.rootBlockTrans.forward.SetY(0).normalized) > 0.8f)
                        controller.TryBoost(thisControl);
                }
                else if (thisInst.featherBoost)
                {
                    if (thisInst.featherBoostersClock >= 25)
                    {
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.8f)
                            controller.TryBoost(thisControl);
                        thisInst.featherBoostersClock = 0;
                    }
                    thisInst.featherBoostersClock++;
                }

                // DEBUG FOR DRIVE ERRORS
                if (!tank.IsAnchored)
                {
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, tank.rootBlockTrans.forward * thisControl.DriveControl, new Color(0, 0, 1));
                    if (thisControl.BoostControlJets)
                        Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, tank.rootBlockTrans.TransformDirection(controller.BoostBias) * thisInst.lastTechExtents, new Color(1, 0, 0));
                }
                else if (thisInst.DANGER && thisInst.lastEnemy)
                {
                    if (thisInst.lastEnemy.tank.IsEnemy(tank.Team))
                        Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, thisInst.lastEnemy.centrePosition - tank.trans.position, new Color(0, 1, 1));
                }
            }
            return true;
        }

        public void SpaceMaintainer(TankControl thisControl)
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

            float driveMultiplier = 0;

            //AI Steering Rotational
            Vector3 distDiff = controller.ProcessedDest - tank.boundsCentreWorldNoCheck;
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
            forwardFlat = forwardFlat.normalized;
            if (thisInst.Navi3DDirect == Vector3.zero)
            {   //keep upright!
                if (thisInst.DriveDir == EDriveType.Backwards)
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

                //Debug.Log("TACtical_AI: TurnVal UP " + turnVal);
            }
            else
            {   //for special cases we want to angle at the enemy
                if (thisInst.DriveDir == EDriveType.Backwards)
                    turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(-thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
                else
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
            }

            thisInst.Navi3DDirect = Vector3.zero;
            thisInst.Navi3DUp = Vector3.up;
            if (thisInst.Steer)
            {
                if (thisInst.AdviseAway)
                {   //Move from target
                    if (thisInst.DriveDir == EDriveType.Perpendicular)
                    {   //Broadside the enemy
                        control3D.m_State.m_InputRotation = turnVal;//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            thisInst.Navi3DDirect = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                            thisControl.m_Movement.FacePosition(tank, controller.ProcessedDest, 1);
                        // Disabled for now as most spaceships in the pop do not have broadsides.
                        /*
                        control3D.m_State.m_InputRotation = turnVal;
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            if (Vector3.Dot(thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                            {
                                thisInst.Navi3DDirect = Vector3.Cross(Vector3.up, (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                thisInst.Navi3DUp = Vector3.Cross((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, thisInst.Navi3DDirect).normalized;
                                //Debug.Log("TACtical_AI: Broadside Left A  up is " + thisInst.Navi3DUp);
                            }
                            else
                            {
                                thisInst.Navi3DDirect = Vector3.Cross((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, Vector3.up).normalized;
                                thisInst.Navi3DUp = Vector3.Cross(thisInst.Navi3DDirect, (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                //Debug.Log("TACtical_AI: Broadside Right A  up is " + thisInst.Navi3DUp);
                            }
                        }
                        else
                        {
                            //thisInst.Navi3DDirect = controller.ProcessedDest - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, controller.ProcessedDest, 1);
                        }*/
                    }
                    else if (thisInst.DriveDir == EDriveType.Forwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal;//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            thisInst.Navi3DDirect = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                            thisControl.m_Movement.FacePosition(tank, controller.ProcessedDest, 1);
                    }
                    else if (thisInst.DriveDir == EDriveType.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal;
                        thisControl.m_Movement.FaceDirection(tank, tank.boundsCentreWorldNoCheck - controller.ProcessedDest, 1);
                    }
                    else
                    {
                        control3D.m_State.m_InputRotation.y = 0;
                    }
                }
                else
                {
                    if (thisInst.DriveDir == EDriveType.Perpendicular)
                    {   //Broadside the enemy
                        control3D.m_State.m_InputRotation = turnVal;
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            if (Vector3.Dot(thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                            {
                                thisInst.Navi3DDirect = Vector3.Cross(Vector3.up, (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                thisInst.Navi3DUp = Vector3.Cross((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, thisInst.Navi3DDirect).normalized;
                                //Debug.Log("TACtical_AI: Broadside Left  up is " + thisInst.Navi3DUp);
                            }
                            else
                            {
                                thisInst.Navi3DDirect = Vector3.Cross((thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized, Vector3.up).normalized;
                                thisInst.Navi3DUp = Vector3.Cross(thisInst.Navi3DDirect, (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized).normalized;
                                //Debug.Log("TACtical_AI: Broadside Right  up is " + thisInst.Navi3DUp);
                            }
                        }
                        else
                        {
                            //thisInst.Navi3DDirect = controller.ProcessedDest - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, controller.ProcessedDest, 1);
                        }
                    }
                    else if (thisInst.DriveDir == EDriveType.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal;
                        thisControl.m_Movement.FaceDirection(tank, tank.boundsCentreWorldNoCheck - controller.ProcessedDest, 1);
                    }
                    else if (thisInst.DriveDir == EDriveType.Forwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal;
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            thisInst.Navi3DDirect = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            //thisInst.Navi3DDirect = controller.ProcessedDest - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, controller.ProcessedDest, 1);
                        }
                    }
                    else
                    {   //Forwards follow but no pitch controls
                        control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.rootBlockTrans.forward), 0, 1);
                        thisControl.m_Movement.FacePosition(tank, controller.ProcessedDest, 1);
                    }
                }
            }
            else
                control3D.m_State.m_InputRotation = Vector3.zero;

            //AI Drive Translational
            Vector3 driveVal;
            if (thisInst.AdviseAway)
            {   //Move from target
                if (thisInst.lastEnemy.IsNotNull() && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
                {
                    float enemyOffsetH = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck.y;
                    driveVal = InertiaTranslation(-tank.rootBlockTrans.InverseTransformPoint(controller.ProcessedDest).normalized);
                    if (tank.IsFriendly() && thisInst.lastPlayer.IsNotNull())
                    {
                        if (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.RangeToChase / 3) < thisInst.tank.boundsCentreWorldNoCheck.y)
                            driveVal.y = -1;
                    }
                    else if (enemyOffsetH + (thisInst.GroundOffsetHeight / 2) > thisInst.tank.boundsCentreWorldNoCheck.y)
                    {
                        //Debug.Log("TACtical_AI: leveling");
                        driveVal.y = Mathf.Clamp((enemyOffsetH + (thisInst.GroundOffsetHeight / 3) - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: Going DOWN");;
                        driveVal.y = -1;
                    }
                }
                else
                    driveVal = InertiaTranslation(-tank.rootBlockTrans.InverseTransformPoint(controller.ProcessedDest).normalized);
                driveMultiplier = 1f;
            }
            else
            {
                if (thisInst.lastEnemy.IsNotNull() && !thisInst.IsMultiTech && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
                {   //level alt with enemy
                    float enemyOffsetH = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck.y;
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformPoint(controller.ProcessedDest).normalized);
                    if (tank.IsFriendly() && thisInst.lastPlayer.IsNotNull())
                    {
                        if (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.RangeToChase / 3) < thisInst.tank.boundsCentreWorldNoCheck.y)
                            driveVal.y = -1;
                    }
                    else if (enemyOffsetH + (thisInst.GroundOffsetHeight / 2) > thisInst.tank.boundsCentreWorldNoCheck.y)
                    {
                        driveVal.y = Mathf.Clamp((enemyOffsetH + (thisInst.GroundOffsetHeight / 3) - tank.boundsCentreWorldNoCheck.y) / 10, -1, 1);
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
                        driveVal = InertiaTranslation(-tank.rootBlockTrans.InverseTransformPoint(controller.ProcessedDest).normalized * 0.3f);
                    }
                    else if (range > thisInst.MinimumRad + 1)
                    {
                        driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformPoint(controller.ProcessedDest).normalized);
                        if (thisInst.DriveDir == EDriveType.Forwards || thisInst.DriveDir == EDriveType.Backwards)
                            driveMultiplier = 1f;
                        else
                            driveMultiplier = 0.4f;
                    }
                    else
                        driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformPoint(controller.ProcessedDest).normalized);
                }
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

            if (thisInst.PivotOnly)
            {
                driveVal.x = 0;
                driveVal.z = 0;
            }

            if (thisInst.Yield)
            {
                // Supports all directions
                if (thisInst.recentSpeed > 15)
                    driveMultiplier = -0.3f;
                else
                    driveMultiplier = 0.3f;
            }
            else if (thisInst.BOOST)
            {
                driveMultiplier = 1;
                if (Vector3.Dot(driveVal, tank.rootBlockTrans.forward) > 0.75f)
                    thisControl.m_Movement.FireBoosters(tank);
            }
            else if (thisInst.featherBoost)
            {
                if (thisInst.forceDrive)
                    driveMultiplier = thisInst.DriveVar;
                if (thisInst.featherBoostersClock >= 25)
                {
                    if (Vector3.Dot(driveVal, tank.rootBlockTrans.forward) > 0.75f)
                        thisControl.m_Movement.FireBoosters(tank);
                    thisInst.featherBoostersClock = 0;
                }
                thisInst.featherBoostersClock++;
            }
            else if (thisInst.forceDrive)
            {
                driveMultiplier = thisInst.DriveVar;
            }

            // PREVENT GROUND CRASHING
            if (EmergencyUp)
            {
                control3D.m_State.m_InputMovement = tank.rootBlockTrans.InverseTransformVector(Vector3.up);

                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, driveVal * thisInst.lastTechExtents, new Color(0, 0, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, control3D.m_State.m_InputMovement * thisInst.lastTechExtents, new Color(1, 0, 0));

                controlGet.SetValue(tank.control, control3D);
                return;
            }
            //thisInst.MinimumRad
            // Prevent drifting
            Vector3 final = (driveVal * Mathf.Clamp(distDiff.magnitude / 5, 0, 1) * driveMultiplier).Clamp01Box();

            if (thisInst.DriveDir != EDriveType.Neutral)
            {
                if (final.y.Approximately(0, 0.2f))
                    final.y = 0;
                if (final.x.Approximately(0, 0.15f))
                    final.x = 0;
                if (final.z.Approximately(0, 0.15f))
                    final.z = 0;
            }
            control3D.m_State.m_InputMovement = final;

            // DEBUG FOR DRIVE ERRORS
            if (tank.IsAnchored)
            {
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, distDiff, new Color(0, 1, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 1, driveVal * thisInst.lastTechExtents, new Color(0, 0, 1));
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 2, control3D.m_State.m_InputMovement * thisInst.lastTechExtents, new Color(1, 0, 0));
            }
            else if (thisInst.DANGER && thisInst.lastEnemy)
            {
                if (thisInst.lastEnemy.tank.IsEnemy(tank.Team))
                    Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, thisInst.lastEnemy.centrePosition - tank.trans.position, new Color(0, 1, 1));
            }
            controlGet.SetValue(tank.control, control3D);
        }

        public bool TryAdjustForCombat()
        {
            AIECore.TankAIHelper thisInst = controller.Helper;
            bool output = false;
            if (thisInst.PursueThreat && ((!thisInst.ProceedToObjective && !thisInst.MoveFromObjective) || !thisInst.Retreat) && thisInst.lastEnemy.IsNotNull())
            {
                Vector3 enemyPosApprox = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                output = true;
                thisInst.Steer = true;
                thisInst.lastRangeCombat = (enemyPosApprox - tank.boundsCentreWorldNoCheck).magnitude;
                thisInst.lastRange = thisInst.lastRangeCombat;
                float driveDyna = Mathf.Clamp((thisInst.lastRangeCombat - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    thisInst.DriveDir = EDriveType.Perpendicular;
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        controller.ProcessedDest = enemyPosApprox;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        controller.ProcessedDest = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        controller.ProcessedDest = thisInst.AvoidAssist(enemyPosApprox);
                        //thisInst.MinimumRad = 0.5f;
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }
                    /*
                    else if (driveDyna < 0.5f)
                    {
                        thisInst.PivotOnly = true;
                        controller.ProcessedDest = thisInst.AvoidAssist(enemyPosApprox);
                        //thisInst.MinimumRad = 0.5f;
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }*/
                    else
                    {
                        controller.ProcessedDest = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                    }
                }
                else
                {
                    thisInst.DriveDir = EDriveType.Forwards;
                    if (thisInst.FullMelee)
                    {
                        controller.ProcessedDest = enemyPosApprox;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        controller.ProcessedDest = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        controller.ProcessedDest = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        controller.ProcessedDest = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 3;
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                }
            }
            else
                thisInst.lastRangeCombat = float.MaxValue;
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
                thisInst.lastRangeCombat = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna;
                if (thisInst.Attempt3DNavi)
                    driveDyna = Mathf.Clamp((thisInst.lastRangeCombat - AIGlobals.SpacingRangeHoverer) / 3f, -1, 1);
                else
                    driveDyna = Mathf.Clamp((thisInst.lastRangeCombat - AIGlobals.SpacingRange) / 3f, -1, 1);
                if (mind.CommanderAttack == EnemyAttack.Circle)
                {   // works fine for now
                    thisInst.DriveDir = EDriveType.Perpendicular;
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!;
                        controller.ProcessedDest = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else
                    {
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    //Debug.Log("DriveDyna is " + driveDyna);
                }
                else
                {   // Since the enemy also uses it's Operator in combat, this will have to listen to that
                    if (thisInst.MoveFromObjective)
                    {
                        thisInst.Steer = true;
                        /*
                        if (thisInst.Attempt3DNavi)
                            thisInst.DriveDir = EDriveType.Forwards;
                        else
                                thisInst.DriveDir = EDriveType.Backwards;
                        */
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemyInv(controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), controller.Helper, mind);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.ProceedToObjective && mind.LikelyMelee)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), controller.Helper, mind);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.ProceedToObjective)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), controller.Helper, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                    }
                    else
                    {
                        controller.ProcessedDest = RPathfinding.AvoidAssistEnemy(controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), controller.Helper, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                    }
                    /*
                    thisInst.DriveDir = EDriveType.Forwards;
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {
                        controller.ProcessedDest = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        controller.ProcessedDest = Enemy.RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        controller.ProcessedDest = Enemy.RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5; ;
                    }
                    else
                    {
                        controller.ProcessedDest = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5; ;
                    }
                    */
                }
            }
            else
                thisInst.lastRangeCombat = float.MaxValue;
            return output;
        }


        private const float throttleDampen = 0.5f;
        private const float DampeningStrength = 1.25f;
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
                    return direction + Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(-tank.rbody.velocity * Time.deltaTime) * DampeningStrength, direction);
                }
            }
            else
            {
                if (controller.Helper.AdvancedAI)
                {
                    return direction + Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(-tank.rbody.velocity * Time.deltaTime) * DampeningStrength, direction);
                }
            }
            return direction * throttleDampen;
        }
    }
}
