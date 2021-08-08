using System;
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
            var help = this.controller.Helper;
            // Debug.Log("TACtical_AI: Tech " + tank.name + " drive was called");
            if (help.IsMultiTech)
            {   //Override and disable most driving abilities
                if (help.DediAI == AIType.MTSlave)
                {   // act like a trailer
                    help.DriveDir = EDriveType.Neutral;
                    help.Steer = false;
                    help.lastDestination = help.lastEnemy.transform.position;
                    help.MinimumRad = 0;
                }
                else if (help.DediAI == AIType.MTTurret && help.lastEnemy != null)
                {
                    help.Steer = true;
                    help.PivotOnly = true;
                    help.lastDestination = help.lastEnemy.transform.position;
                    help.MinimumRad = 0;
                }
                else if (help.DediAI == AIType.MTMimic && help.MTMimicHostAvail)
                {
                    if (help.LastCloseAlly != null)
                    {
                        try
                        {
                            help.lastDestination = AIEPathing.GetDriveApproxAir(help.LastCloseAlly, this.controller.Helper, out bool IsMoving);
                            if (IsMoving)//!(help.lastDestination - this.controller.Tank.boundsCentreWorld).Approximately(Vector3.zero, 0.75f)
                            {
                                //Debug.Log("TACtical_AI: MTMimic - AI " + this.controller.Tank.name + ": In range of " + help.LastCloseAlly.name + " and idle.");
                                help.MinimumRad = 0.1f;
                                if (Vector3.Dot(this.controller.Tank.rootBlockTrans.forward, (help.lastDestination - this.controller.Tank.boundsCentreWorldNoCheck).normalized) >= 0)
                                {
                                    //Debug.Log("TACtical_AI:AI " + this.controller.Tank.name + ": Forwards");
                                    help.Steer = true;
                                    help.DriveDir = EDriveType.Forwards;
                                }
                                else
                                {
                                    help.Steer = true;
                                    help.DriveDir = EDriveType.Backwards;
                                }
                            }
                            else
                            {
                                //Debug.Log("TACtical_AI: MTMimic - AI " + this.controller.Tank.name + ": In range of " + help.LastCloseAlly.name + " and idle.");
                                help.MinimumRad = 0f;
                                help.lastDestination = tank.boundsCentreWorldNoCheck;
                                help.forceDrive = true;
                                help.DriveVar = 0;
                                help.Steer = false;
                                help.PivotOnly = true;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: MTMimic - AI " + this.controller.Tank.name + ": Out of range of any possible target");
                        help.MinimumRad = 0f;
                        help.lastDestination = tank.boundsCentreWorldNoCheck;
                        help.forceDrive = true;
                        help.DriveVar = 0;
                        help.Steer = false;
                    }
                }
            }
            else if (help.ProceedToBase)
            {
                if (help.lastBasePos.IsNotNull())
                {
                    help.Steer = true;
                    help.DriveDir = EDriveType.Forwards;
                    help.lastDestination = help.AvoidAssistPrecise(help.lastBasePos.position);
                    help.MinimumRad = Mathf.Max(help.lastTechExtents - 2, 0.5f);
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
                        help.lastDestination = help.theResource.tank.boundsCentreWorldNoCheck;
                        help.MinimumRad = 0;
                    }
                    else
                    {
                        if (help.FullMelee)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.lastDestination = help.AvoidAssistPrecise(help.theResource.tank.boundsCentreWorldNoCheck);
                            help.MinimumRad = 0;
                        }
                        else
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.lastDestination = help.AvoidAssistPrecise(help.theResource.tank.boundsCentreWorldNoCheck);
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
                        help.lastDestination = help.theResource.centrePosition;
                        help.MinimumRad = 0;
                    }
                    else
                    {
                        if (help.FullMelee)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.lastDestination = help.AvoidAssistPrecise(help.theResource.centrePosition);
                            help.MinimumRad = 0;
                        }
                        else
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.lastDestination = help.AvoidAssistPrecise(help.theResource.centrePosition);
                            help.MinimumRad = help.lastTechExtents + 2;
                        }
                    }
                }
            }
            else if (help.DediAI == AIType.Aegis)
            {
                help.theResource = AIEPathing.ClosestUnanchoredAlly(this.controller.Tank.boundsCentreWorldNoCheck, out float bestval, tank).visible;
                bool Combat = this.TryAdjustForCombat();
                if (!Combat)
                {
                    if (help.MoveFromObjective && help.theResource.IsNotNull())
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.AdviseAway = true;
                        help.lastDestination = help.theResource.transform.position;
                        help.MinimumRad = 0.5f;
                    }
                    else if (help.ProceedToObjective && help.theResource.IsNotNull())
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.lastDestination = help.AvoidAssist(help.theResource.tank.transform.position);
                        help.MinimumRad = help.lastTechExtents + AIECore.Extremes(help.theResource.tank.blockBounds.extents) + 5;
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
            }
            else
            {
                bool Combat = this.TryAdjustForCombat();
                if (!Combat)
                {
                    if (help.MoveFromObjective && help.lastPlayer.IsNotNull())
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.AdviseAway = true;
                        help.lastDestination = help.lastPlayer.transform.position;
                        help.MinimumRad = 0.5f;
                    }
                    else if (help.ProceedToObjective && help.lastPlayer.IsNotNull())
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.lastDestination = help.AvoidAssist(help.lastPlayer.transform.position);
                        help.MinimumRad = help.lastTechExtents + AIECore.Extremes(help.lastPlayer.tank.blockBounds.extents) + 5;
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
            }
            if (help.Attempt3DNavi && !(help.FullMelee && help.lastEnemy.IsNotNull()))
                help.lastDestination = AIEPathing.OffsetFromGround(help.lastDestination, this.controller.Helper);
            else if (help.DediAI == AIType.Buccaneer)
                help.lastDestination = AIEPathing.OffsetToSea(help.lastDestination, tank, this.controller.Helper);

            AIEPathing.ModerateMaxAlt(ref help.lastDestination, help);

            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind)
        {
            var help = this.controller.Helper;
            if (mind.IsNull())
                return false;
            if (help.ProceedToBase)
            {
                if (help.lastBasePos.IsNotNull())
                {
                    help.Steer = true;
                    help.DriveDir = EDriveType.Forwards;
                    help.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.lastBasePos.position, this.controller.Helper, mind);
                    help.MinimumRad = Mathf.Max(help.lastTechExtents - 2, 0.5f);
                }
            }
            else if (help.ProceedToMine)
            {
                if (help.PivotOnly)
                {
                    help.Steer = true;
                    help.DriveDir = EDriveType.Forwards;
                    help.lastDestination = help.theResource.centrePosition;
                    help.MinimumRad = 0;
                }
                else
                {
                    if (mind.MainFaction == FactionSubTypes.GC)
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.theResource.centrePosition, this.controller.Helper, mind);
                        help.MinimumRad = 0;
                    }
                    else
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.theResource.centrePosition, this.controller.Helper, mind);
                        help.MinimumRad = help.lastTechExtents + 2;
                    }
                }
            }
            else
            {
                bool Combat = this.TryAdjustForCombatEnemy(mind);
                if (!Combat)
                {
                    if (help.MoveFromObjective)
                    {
                        help.Steer = true;
                        if (help.Retreat)
                            help.DriveDir = EDriveType.Backwards;
                        else
                            help.DriveDir = EDriveType.Forwards;
                        help.AdviseAway = true;
                        help.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.lastDestination, this.controller.Helper, mind);
                        help.MinimumRad = 0.5f;
                    }
                    else if (help.ProceedToObjective)
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.lastDestination, this.controller.Helper, mind);
                        help.MinimumRad = help.lastTechExtents + 8;
                    }
                }
            }
            if (mind.EvilCommander == Enemy.EnemyHandling.Naval)
                help.lastDestination = AIEPathing.OffsetToSea(help.lastDestination, tank, this.controller.Helper);
            else if (mind.EvilCommander == Enemy.EnemyHandling.Starship)
                help.lastDestination = AIEPathing.OffsetFromGround(help.lastDestination, this.controller.Helper);
            else
                help.lastDestination = AIEPathing.OffsetFromGround(help.lastDestination, this.controller.Helper, this.controller.Tank.blockBounds.size.y);
            if (mind.EvilCommander == EnemyHandling.Wheeled)
                help.lastDestination = AIEPathing.OffsetFromSea(help.lastDestination, tank, controller.Helper);

            AIEPathing.ModerateMaxAlt(ref help.lastDestination, help);

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
                thisControl.DriveControl = 0;
                Vector3 destDirect = tank.boundsCentreWorldNoCheck - thisInst.lastDestination;
                if (thisInst.Steer)
                {
                    if (thisInst.AdviseAway)
                    {   //Move from target
                        if (thisInst.DriveDir == EDriveType.Backwards)//EDriveType.Backwards
                        {
                            thisControl.m_Movement.FaceDirection(tank, destDirect, 1);
                            thisControl.DriveControl = 1f;
                        }
                        else
                        {
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                            thisControl.DriveControl = -1f;
                        }
                    }
                    if (thisInst.DriveDir == EDriveType.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        int range = (int)(destDirect).magnitude;
                        if (range < thisInst.MinimumRad + 2)
                        {
                            thisControl.m_Movement.FaceDirection(tank, destDirect, 1);
                        }
                        else if (range > thisInst.MinimumRad + 22)
                        {
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                        }
                        else  //ORBIT!
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ":  ORBITING!!!!");
                            if (Vector3.Dot(thisInst.lastDestination - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                                thisControl.m_Movement.FaceDirection(tank, Vector3.Cross(destDirect, Vector3.down), 1);
                            else
                                thisControl.m_Movement.FaceDirection(tank, Vector3.Cross(destDirect, Vector3.up), 1);
                        }
                        thisControl.DriveControl = 1f;
                    }
                    else if (thisInst.DriveDir == EDriveType.Backwards)
                    {   //Drive to target driving backwards
                        thisControl.m_Movement.FaceDirection(tank, destDirect, 1);//Face the music
                        thisControl.DriveControl = -1f;
                    }
                    else
                    {
                        thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);//Face the music
                        //Debug.Log("TACtical_AI: AI " + tank.name + ":  driving to " + thisInst.lastDestination);
                        if (thisInst.MinimumRad > 0)
                        {
                            int range = (int)(destDirect).magnitude;
                            if (range < thisInst.MinimumRad - 1)
                            {
                                if (thisInst.DriveDir == EDriveType.Forwards)
                                    thisControl.DriveControl = -0.3f;
                                else if (thisInst.DriveDir == EDriveType.Backwards)
                                    thisControl.DriveControl = 0.3f;
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
                                    thisControl.DriveControl = 0.6f;
                            }
                        }
                        else
                            thisControl.DriveControl = 0.6f;
                    }
                }

                // Overrides to translational drive
                if (thisInst.DriveDir == EDriveType.Neutral)
                {   // become brakeless
                    thisControl.DriveControl = 0.001f;
                    return true;
                }

                if (thisInst.PivotOnly)
                {
                    thisControl.DriveControl = 0;
                }
                if (thisInst.Yield)
                {
                    if (thisInst.DriveDir == EDriveType.Backwards)
                    {
                        if (thisInst.recentSpeed > 10)
                            thisControl.DriveControl = 0.2f;
                        else
                            thisControl.DriveControl = -0.5f;
                    }
                    else
                    {   // works with forwards
                        if (thisInst.recentSpeed > 10)
                            thisControl.DriveControl = -0.2f;
                        else
                            thisControl.DriveControl = 0.5f;
                    }
                }
                else if (thisInst.forceDrive)
                {
                    thisControl.DriveControl = thisInst.DriveVar;
                    if (thisInst.BOOST)
                    {
                        thisControl.DriveControl = 1;
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.75f)
                            thisControl.m_Movement.FireBoosters(tank);
                    }
                    else if (thisInst.featherBoost)
                    {
                        if (thisInst.featherBoostersClock >= 25)
                        {
                            if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.75f)
                                thisControl.m_Movement.FireBoosters(tank);
                            thisInst.featherBoostersClock = 0;
                        }
                        thisInst.featherBoostersClock++;
                    }
                }
                else if (thisInst.BOOST)
                {
                    thisControl.DriveControl = 1;
                    if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.75f)
                        thisControl.m_Movement.FireBoosters(tank);
                }
                else if (thisInst.featherBoost)
                {
                    if (thisInst.featherBoostersClock >= 25)
                    {
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.75f)
                            thisControl.m_Movement.FireBoosters(tank);
                        thisInst.featherBoostersClock = 0;
                    }
                    thisInst.featherBoostersClock++;
                }
            }
            return true;
        }

        public void SpaceMaintainer(TankControl thisControl)
        {
            Tank tank = this.tank;
            AIECore.TankAIHelper thisInst = this.controller.Helper;
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);

            float driveMultiplier = 0;

            //AI Steering Rotational
            Vector3 distDiff = thisInst.lastDestination - tank.trans.position;
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
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
                            //thisInst.Navi3DDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                        }
                    }
                    else if (thisInst.DriveDir == EDriveType.Forwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal;//* Mathf.Clamp(1 - Vector3.Dot(turnFVal, tank.trans.forward), 0, 1)
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            thisInst.Navi3DDirect = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck;
                        }
                        else
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                    }
                    else if (thisInst.DriveDir == EDriveType.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal;
                        thisControl.m_Movement.FaceDirection(tank, tank.boundsCentreWorldNoCheck - thisInst.lastDestination, 1);
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
                            //thisInst.Navi3DDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                        }
                    }
                    else if (thisInst.DriveDir == EDriveType.Backwards)
                    {
                        control3D.m_State.m_InputRotation = turnVal;
                        thisControl.m_Movement.FaceDirection(tank, tank.boundsCentreWorldNoCheck - thisInst.lastDestination, 1);
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
                            //thisInst.Navi3DDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                        }
                    }
                    else
                    {   //Forwards follow but no pitch controls
                        control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.trans.forward), 0, 1);
                        thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
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
                    driveVal = InertiaTranslation(-tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized);
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
                    driveVal = InertiaTranslation(-tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized);
                driveMultiplier = 1f;
            }
            else
            {
                if (thisInst.lastEnemy.IsNotNull() && thisInst.DediAI != AIType.MTMimic && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
                {   //level alt with enemy
                    float enemyOffsetH = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck.y;
                    driveVal = InertiaTranslation(tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized);
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
                    driveVal = InertiaTranslation(tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized);
                    int range = (int)(thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude;
                    if (range < thisInst.MinimumRad - 1)
                    {
                        driveMultiplier = 1f;
                        driveVal = InertiaTranslation(-tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized * 0.3f);
                    }
                    else if (range > thisInst.MinimumRad + 1)
                    {
                        if (thisInst.DriveDir == EDriveType.Forwards || thisInst.DriveDir == EDriveType.Backwards)
                            driveMultiplier = 1f;
                        else
                            driveMultiplier = 0.4f;
                    }
                }
            }
            if (thisInst.DediAI != AIType.MTMimic)
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


            control3D.m_State.m_InputMovement = driveVal * Mathf.Clamp(distDiff.magnitude / thisInst.MinimumRad, 0, 1) * driveMultiplier;
            controlGet.SetValue(tank.control, control3D);
        }

        public bool TryAdjustForCombat()
        {
            AIECore.TankAIHelper thisInst = this.controller.Helper;
            bool output = false;
            if (thisInst.PursueThreat && !thisInst.Retreat && thisInst.lastEnemy.IsNotNull())
            {
                output = true;
                thisInst.Steer = true;
                float driveDyna = Mathf.Clamp(((thisInst.lastEnemy.transform.position - tank.boundsCentreWorldNoCheck).magnitude - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                }
            }
            return output;
        }

        public bool TryAdjustForCombatEnemy(EnemyMind mind)
        {
            AIECore.TankAIHelper thisInst = this.controller.Helper;
            bool output = false;
            if (!thisInst.Retreat && thisInst.lastEnemy.IsNotNull() && mind.CommanderMind != Enemy.EnemyAttitude.OnRails)
            {   
                output = true;
                thisInst.Steer = true;
                float driveDyna = Mathf.Clamp(((thisInst.lastEnemy.transform.position - tank.boundsCentreWorldNoCheck).magnitude - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (mind.CommanderAttack == Enemy.EnemyAttack.Circle)
                {   // works fine for now
                    thisInst.DriveDir = EDriveType.Perpendicular;
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                    else
                    {
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                }
                else
                {   // Since the enemy also uses it's Operator in combat, this will have to listen to that
                    if (thisInst.MoveFromObjective)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), this.controller.Helper, mind);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.ProceedToObjective && mind.MainFaction == FactionSubTypes.GC)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), this.controller.Helper, mind);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.ProceedToObjective)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), this.controller.Helper, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + 8;
                    }
                    else
                    {
                        thisInst.lastDestination = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                    }
                    /*
                    thisInst.DriveDir = EDriveType.Forwards;
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5; ;
                    }
                    else
                    {
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5; ;
                    }
                    */
                }
            }
            return output;
        }


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
                    return direction - Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity * Time.deltaTime), direction);
                }
            }
            else
            {
                if (controller.Helper.AdvancedAI)
                {
                    return direction - Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity * Time.deltaTime), direction);
                }
            }
            return direction;
        }
    }
}
