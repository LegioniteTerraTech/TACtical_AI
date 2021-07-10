using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using TAC_AI.AI.Enemy;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
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
            // Debug.Log("TACtical_AI: Tech " + tank.name + " drive was called");
            if (this.controller.Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                if (this.controller.Helper.DediAI == AIType.MTSlave)
                {   // act like a trailer
                    this.controller.Helper.DriveDir = EDriveType.Neutral;
                    this.controller.Helper.Steer = false;
                    this.controller.Helper.lastDestination = this.controller.Helper.lastEnemy.transform.position;
                    this.controller.Helper.MinimumRad = 0;
                }
                else if (this.controller.Helper.lastEnemy != null && this.controller.Helper.DediAI == AIType.MTTurret)
                {
                    this.controller.Helper.Steer = true;
                    this.controller.Helper.lastDestination = this.controller.Helper.lastEnemy.transform.position;
                    this.controller.Helper.MinimumRad = 0;
                    //Vector3 aimTo = (this.controller.Helper.lastEnemy.transform.position - this.controller.Tank.transform.position).normalized;
                    //float driveDyna = Mathf.Abs(Mathf.Clamp((this.controller.Tank.rootBlockTrans.forward - aimTo).magnitude / 1.5f, -1, 1));
                    //thisControl.m_Movement.FacePosition(this.controller.Tank, this.controller.Helper.lastEnemy.transform.position, driveDyna);//Face the music
                }
                else if (this.controller.Helper.MTMimicHostAvail && this.controller.Helper.LastCloseAlly != null && this.controller.Helper.DediAI == AIType.MTMimic)
                {
                    this.controller.Helper.MinimumRad = 0.05f;
                    this.controller.Helper.lastDestination = AIEPathing.GetDriveApproxAir(this.controller.Helper.LastCloseAlly, this.controller.Helper);
                    if (!(this.controller.Helper.lastDestination - this.controller.Tank.boundsCentreWorld).Approximately(Vector3.zero, 0.05f))
                    {
                        if (Vector3.Dot(this.controller.Tank.rootBlockTrans.forward, (this.controller.Helper.lastDestination - this.controller.Tank.boundsCentreWorldNoCheck).normalized) >= 0)
                        {
                            //Debug.Log("TACtical_AI:AI " + this.controller.Tank.name + ": Forwards");
                            this.controller.Helper.Steer = true;
                            this.controller.Helper.DriveDir = EDriveType.Forwards;
                        }
                        else
                        {
                            this.controller.Helper.Steer = true;
                            this.controller.Helper.DriveDir = EDriveType.Backwards;
                        }
                    }
                    else
                    {
                        this.controller.Helper.PivotOnly = true;
                    }
                }
            }
            else if (this.controller.Helper.ProceedToBase)
            {
                if (this.controller.Helper.lastBasePos.IsNotNull())
                {
                    this.controller.Helper.Steer = true;
                    this.controller.Helper.DriveDir = EDriveType.Forwards;
                    this.controller.Helper.lastDestination = this.controller.Helper.AvoidAssistPrecise(this.controller.Helper.lastBasePos.position);
                    this.controller.Helper.MinimumRad = Mathf.Max(this.controller.Helper.lastTechExtents - 2, 0.5f);
                }
            }
            else if (this.controller.Helper.ProceedToMine)
            {
                if (this.controller.Helper.PivotOnly)
                {
                    this.controller.Helper.Steer = true;
                    this.controller.Helper.DriveDir = EDriveType.Forwards;
                    this.controller.Helper.lastDestination = this.controller.Helper.theResource.centrePosition;
                    this.controller.Helper.MinimumRad = 0;
                }
                else
                {
                    if (this.controller.Helper.FullMelee)
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.lastDestination = this.controller.Helper.AvoidAssistPrecise(this.controller.Helper.theResource.centrePosition);
                        this.controller.Helper.MinimumRad = 0;
                    }
                    else
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.lastDestination = this.controller.Helper.AvoidAssistPrecise(this.controller.Helper.theResource.centrePosition);
                        this.controller.Helper.MinimumRad = this.controller.Helper.lastTechExtents + 2;
                    }
                }
            }
            else if (this.controller.Helper.DediAI == AIType.Aegis)
            {
                this.controller.Helper.LastCloseAlly = AIEPathing.ClosestAlly(this.controller.Tank.boundsCentreWorldNoCheck, out float bestval);
                bool Combat = this.TryAdjustForCombat();
                if (!Combat)
                {
                    if (this.controller.Helper.MoveFromObjective && this.controller.Helper.LastCloseAlly.IsNotNull())
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.AdviseAway = true;
                        this.controller.Helper.lastDestination = this.controller.Helper.LastCloseAlly.transform.position;
                        this.controller.Helper.MinimumRad = 0.5f;
                    }
                    else if (this.controller.Helper.ProceedToObjective && this.controller.Helper.LastCloseAlly.IsNotNull())
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.lastDestination = this.controller.Helper.AvoidAssist(this.controller.Helper.LastCloseAlly.transform.position);
                        this.controller.Helper.MinimumRad = this.controller.Helper.lastTechExtents + AIECore.Extremes(this.controller.Helper.LastCloseAlly.blockBounds.extents) + 5;
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
                    if (this.controller.Helper.MoveFromObjective && this.controller.Helper.lastPlayer.IsNotNull())
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.AdviseAway = true;
                        this.controller.Helper.lastDestination = this.controller.Helper.lastPlayer.transform.position;
                        this.controller.Helper.MinimumRad = 0.5f;
                    }
                    else if (this.controller.Helper.ProceedToObjective && this.controller.Helper.lastPlayer.IsNotNull())
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.lastDestination = this.controller.Helper.AvoidAssist(this.controller.Helper.lastPlayer.transform.position);
                        this.controller.Helper.MinimumRad = this.controller.Helper.lastTechExtents + AIECore.Extremes(this.controller.Helper.lastPlayer.tank.blockBounds.extents) + 5;
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
            }
            if (this.controller.Helper.Attempt3DNavi && !(this.controller.Helper.FullMelee && this.controller.Helper.lastEnemy.IsNotNull()))
                this.controller.Helper.lastDestination = AIEPathing.OffsetFromGround(this.controller.Helper.lastDestination, this.controller.Helper);
            else if (this.controller.Helper.DediAI == AIType.Buccaneer)
                this.controller.Helper.lastDestination = AIEPathing.OffsetToSea(this.controller.Helper.lastDestination, this.controller.Helper);

            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind)
        {
            if (mind.IsNull())
                return false;
            if (this.controller.Helper.ProceedToBase)
            {
                if (this.controller.Helper.lastBasePos.IsNotNull())
                {
                    this.controller.Helper.Steer = true;
                    this.controller.Helper.DriveDir = EDriveType.Forwards;
                    this.controller.Helper.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, this.controller.Helper.lastBasePos.position, this.controller.Helper, mind);
                    this.controller.Helper.MinimumRad = Mathf.Max(this.controller.Helper.lastTechExtents - 2, 0.5f);
                }
            }
            else if (this.controller.Helper.ProceedToMine)
            {
                if (this.controller.Helper.PivotOnly)
                {
                    this.controller.Helper.Steer = true;
                    this.controller.Helper.DriveDir = EDriveType.Forwards;
                    this.controller.Helper.lastDestination = this.controller.Helper.theResource.centrePosition;
                    this.controller.Helper.MinimumRad = 0;
                }
                else
                {
                    if (mind.MainFaction == FactionSubTypes.GC)
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, this.controller.Helper.theResource.centrePosition, this.controller.Helper, mind);
                        this.controller.Helper.MinimumRad = 0;
                    }
                    else
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, this.controller.Helper.theResource.centrePosition, this.controller.Helper, mind);
                        this.controller.Helper.MinimumRad = this.controller.Helper.lastTechExtents + 2;
                    }
                }
            }
            else
            {
                bool Combat = this.TryAdjustForCombatEnemy(mind);
                if (!Combat)
                {
                    if (this.controller.Helper.MoveFromObjective)
                    {
                        this.controller.Helper.Steer = true;
                        if (this.controller.Helper.Retreat)
                            this.controller.Helper.DriveDir = EDriveType.Backwards;
                        else
                            this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.AdviseAway = true;
                        this.controller.Helper.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, this.controller.Helper.lastDestination, this.controller.Helper, mind);
                        this.controller.Helper.MinimumRad = 0.5f;
                    }
                    else if (this.controller.Helper.ProceedToObjective)
                    {
                        this.controller.Helper.Steer = true;
                        this.controller.Helper.DriveDir = EDriveType.Forwards;
                        this.controller.Helper.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(this.controller.Tank, this.controller.Helper.lastDestination, this.controller.Helper, mind);
                        this.controller.Helper.MinimumRad = this.controller.Helper.lastTechExtents + 8;
                    }
                }
            }
            if (mind.EvilCommander == Enemy.EnemyHandling.Naval)
                this.controller.Helper.lastDestination = AIEPathing.OffsetToSea(this.controller.Helper.lastDestination, this.controller.Helper);
            else if (mind.EvilCommander == Enemy.EnemyHandling.Starship)
                this.controller.Helper.lastDestination = AIEPathing.OffsetFromGround(this.controller.Helper.lastDestination, this.controller.Helper);
            else
                this.controller.Helper.lastDestination = AIEPathing.OffsetFromGround(this.controller.Helper.lastDestination, this.controller.Helper, this.controller.Tank.blockBounds.size.y);
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
                Vector3 destDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
                if (thisInst.Steer)
                {
                    if (thisInst.AdviseAway)
                    {   //Move from target
                        if (thisInst.DriveDir == EDriveType.Backwards)
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
                                thisControl.DriveControl = -0.3f;
                            }
                            else if (range > thisInst.MinimumRad + 1)
                            {
                                if (thisInst.DriveDir == EDriveType.Forwards)
                                    thisControl.DriveControl = 1f;
                                else
                                    thisControl.DriveControl = 0.6f;
                            }
                        }
                    }
                }

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
                    {
                        // works with forwards
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
                        if (thisInst.featherClock >= 25)
                        {
                            if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.75f)
                                thisControl.m_Movement.FireBoosters(tank);
                            thisInst.featherClock = 0;
                        }
                        thisInst.featherClock++;
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
                    if (thisInst.featherClock >= 25)
                    {
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.forward) > 0.75f)
                            thisControl.m_Movement.FireBoosters(tank);
                        thisInst.featherClock = 0;
                    }
                    thisInst.featherClock++;
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
                        Debug.Log("TACtical_AI: Broadside overloaded with value " + Vector3.Dot(thisInst.Navi3DUp, tank.rootBlockTrans.up));
                    }
                    else
                    {
                        Debug.Log("TACtical_AI: Broadside Z-tilt active");
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
                        thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);
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
                        thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);
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
                if (thisInst.lastEnemy.IsNotNull())
                {
                    float enemyOffsetH = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck.y;
                    driveVal = -tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized;
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
                    driveVal = -tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized;
                driveMultiplier = 1f;
            }
            else
            {
                if (thisInst.lastEnemy.IsNotNull())
                {   //level alt with enemy
                    float enemyOffsetH = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck.y;
                    driveVal = tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized;
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
                    driveVal = tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized;
                    int range = (int)(thisInst.lastDestination - tank.transform.position).magnitude;
                    if (range < thisInst.MinimumRad - 1)
                    {
                        driveVal = -tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized * 0.3f;
                    }
                    else if (range > thisInst.MinimumRad + 1)
                    {
                        if (thisInst.DriveDir == EDriveType.Forwards)
                            driveMultiplier = 1f;
                        else
                            driveMultiplier = 0.4f;
                    }
                }
            }
            if (driveVal.y >= -0.5f && driveVal.y < 0f)
                driveVal.y = 0; // prevent airships from slam-dunk
            else if (driveVal.y != -1)
            {
                driveVal.y += 0.5f;
            }

            if (thisInst.PivotOnly)
            {
                driveVal.x = 0;
                driveVal.z = 0;
            }
            else if (thisInst.Yield)
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
                if (thisInst.featherClock >= 25)
                {
                    if (Vector3.Dot(driveVal, tank.rootBlockTrans.forward) > 0.75f)
                        thisControl.m_Movement.FireBoosters(tank);
                    thisInst.featherClock = 0;
                }
                thisInst.featherClock++;
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
                {
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                }
                else
                {
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else
                    {
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;
                    }
                }
            }
            return output;
        }
    }
}
