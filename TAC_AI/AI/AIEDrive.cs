using UnityEngine;
using System.Reflection;

namespace TAC_AI.AI
{
    public static class AIEDrive
    {
        // Director for Land/Space Techs  (hand-offs to AIAirborne.FlightDirector for aircraft)
        public static void DriveDirector(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            thisInst.AdviseAway = false;
            if (thisInst.Pilot != null)
            {   // Handoff all operations to AIEAirborne
                bool fired = AIEAirborne.FlightDirector(thisInst, tank, thisInst.Pilot);
                if (fired)
                    return;
            }
            thisControl.m_Movement.m_USE_AVOIDANCE = thisInst.AvoidStuff;
            thisInst.Steer = false;
            thisInst.DriveDir = EDriveType.Neutral;

            if (thisInst.AIState == 1)// Allied
            {
                if (thisInst.IsMultiTech)
                {   //Override and disable most driving abilities
                    if (thisInst.lastEnemy != null && thisInst.DediAI == AIECore.DediAIType.MTTurret)
                    {
                        thisInst.Steer = true;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;
                        //Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                        //float driveDyna = Mathf.Abs(Mathf.Clamp((tank.rootBlockTrans.forward - aimTo).magnitude / 1.5f, -1, 1));
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                    else if (thisInst.MTMimicHostAvail && thisInst.LastCloseAlly != null && thisInst.DediAI == AIECore.DediAIType.MTMimic)
                    {
                        thisInst.MinimumRad = 0.05f;
                        thisInst.lastDestination = AIEPathing.GetDriveApproxAir(thisInst.LastCloseAlly, thisInst);
                        if (!(thisInst.lastDestination - tank.boundsCentreWorld).Approximately(Vector3.zero, 0.05f))
                        {
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (thisInst.lastDestination - tank.boundsCentreWorldNoCheck).normalized) >= 0)
                            {
                                //Debug.Log("TACtical_AI:AI " + tank.name + ": Forwards");
                                thisInst.Steer = true;
                                thisInst.DriveDir = EDriveType.Forwards;
                            }
                            else
                            {
                                thisInst.Steer = true;
                                thisInst.DriveDir = EDriveType.Backwards;
                            }
                        }
                        else
                        {
                            thisInst.PivotOnly = true;
                        }
                    }
                }
                else if (thisInst.ProceedToBase)
                {
                    if (thisInst.lastBasePos.IsNotNull())
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.AvoidAssistPrecise(thisInst.lastBasePos.position);
                        thisInst.MinimumRad = Mathf.Max(thisInst.lastTechExtents - 2, 0.5f);
                    }
                }
                else if (thisInst.ProceedToMine)
                {
                    if (thisInst.PivotOnly)
                    {
                        thisInst.Steer = true;
                        thisInst.lastDestination = thisInst.lastResourcePos;
                        thisInst.MinimumRad = 0;
                    }
                    else
                    {
                        if (thisInst.FullMelee)
                        {
                            thisInst.Steer = true;
                            thisInst.DriveDir = EDriveType.Forwards;
                            thisInst.lastDestination = thisInst.AvoidAssistPrecise(thisInst.lastResourcePos);
                            thisInst.MinimumRad = 0;
                        }
                        else
                        {
                            thisInst.Steer = true;
                            thisInst.lastDestination = thisInst.AvoidAssistPrecise(thisInst.lastResourcePos);
                            thisInst.MinimumRad = thisInst.lastTechExtents + 2;
                        }
                    }
                }
                else if (thisInst.DediAI == AIECore.DediAIType.Aegis)
                {
                    bool Combat = TryHandleCombat(thisInst, tank);
                    if (!Combat)
                    {
                        if (thisInst.MoveFromObjective && thisInst.lastPlayer.IsNotNull())
                        {
                            thisInst.Steer = true;
                            thisInst.DriveDir = EDriveType.Forwards;
                            thisInst.AdviseAway = true;
                            thisInst.lastDestination = thisInst.lastPlayer.transform.position;
                            thisInst.MinimumRad = 0.5f;
                        }
                        else if (thisInst.ProceedToObjective && thisInst.lastPlayer.IsNotNull())
                        {
                            thisInst.Steer = true;
                            thisInst.DriveDir = EDriveType.Forwards;
                            thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastPlayer.transform.position);
                            thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5;
                        }
                        else
                        {
                            //Debug.Log("TACtical_AI: AI IDLE");
                        }
                    }
                }
                else
                {
                    bool Combat = TryHandleCombat(thisInst, tank);
                    if (!Combat)
                    {
                        if (thisInst.MoveFromObjective && thisInst.lastPlayer.IsNotNull())
                        {
                            thisInst.Steer = true;
                            thisInst.DriveDir = EDriveType.Forwards;
                            thisInst.AdviseAway = true;
                            thisInst.lastDestination = thisInst.lastPlayer.transform.position;
                            thisInst.MinimumRad = 0.5f;
                        }
                        else if (thisInst.ProceedToObjective && thisInst.lastPlayer.IsNotNull())
                        {
                            thisInst.Steer = true;
                            thisInst.DriveDir = EDriveType.Forwards;
                            thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastPlayer.transform.position);
                            thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5;
                        }
                        else
                        {
                            //Debug.Log("TACtical_AI: AI IDLE");
                        }
                    }
                }
                if (thisInst.Attempt3DNavi && !(thisInst.FullMelee && thisInst.lastEnemy.IsNotNull()))
                    thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                else if (thisInst.DediAI == AIECore.DediAIType.Buccaneer)
                    thisInst.lastDestination = AIEPathing.OffsetToSea(thisInst.lastDestination, thisInst);
            }
            else//ENEMY
            {
                var mind = thisInst.GetComponent<Enemy.RCore.EnemyMind>();
                if (mind.IsNull())
                    return;
                bool Combat = TryCombatEnemy(thisInst, tank, mind);
                if (!Combat)
                {
                    if (thisInst.MoveFromObjective)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, thisInst.lastDestination);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.ProceedToObjective)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, thisInst.lastDestination);
                        thisInst.MinimumRad = thisInst.lastTechExtents + 8;
                    }
                }
                if (mind.EvilCommander == Enemy.EnemyHandling.Naval)
                    thisInst.lastDestination = AIEPathing.OffsetToSea(thisInst.lastDestination, thisInst);
                else if (mind.EvilCommander == Enemy.EnemyHandling.Starship)
                    thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                else
                    thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst, tank.blockBounds.size.y);
            }
        }


        // Maintainer for Land/Space Techs  (hand-offs to AIAirborne.FlightMaintainer for aircraft)
        public static void DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (thisInst.Pilot != null)
            {   // Handoff all operations to AIEAirborne
                bool fired = AIEAirborne.FlightMaintainer(thisControl, thisInst, tank, thisInst.Pilot);
                if (fired)
                    return;
            }

            FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
            if (thisInst.Attempt3DNavi)//3D movement
            {
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
                    thisControl.m_Movement.FireBoosters(tank);
                }
                else if (thisInst.featherBoost)
                {
                    if (thisInst.featherClock >= 25)
                    {
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
            else //Land movement
            {
                control3D.m_State.m_InputRotation = Vector3.zero;
                control3D.m_State.m_InputMovement = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);
                thisControl.DriveControl = 0;
                if (thisInst.Steer)
                {
                    if (thisInst.AdviseAway)
                    {   //Move from target
                        if (thisInst.DriveDir == EDriveType.Forwards)
                        {
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                            thisControl.DriveControl = -1f;
                        }
                        else
                        {
                            thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);
                            thisControl.DriveControl = 1f;
                        }
                    }
                    if (thisInst.DriveDir == EDriveType.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        int range = (int)(thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude;
                        if (range < thisInst.MinimumRad + 2)
                        {
                            thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);
                        }
                        else if (range > thisInst.MinimumRad + 22)
                        {
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
                        }
                        else  //ORBIT!
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ":  ORBITING!!!!");
                            if (Vector3.Dot(thisInst.lastDestination - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) < 0)
                                thisControl.m_Movement.FaceDirection(tank, Vector3.Cross(thisInst.lastDestination - tank.boundsCentreWorldNoCheck, Vector3.down), 1);
                            else
                                thisControl.m_Movement.FaceDirection(tank, Vector3.Cross(thisInst.lastDestination - tank.boundsCentreWorldNoCheck, Vector3.up), 1);
                        }
                        thisControl.DriveControl = 1f;
                    }
                    else if (thisInst.DriveDir == EDriveType.Backwards)
                    {   //Drive to target driving backwards
                        thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);//Face the music
                        thisControl.DriveControl = -1f;
                    }
                    else
                    {
                        thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);//Face the music
                        if (thisInst.MinimumRad > 0)
                        {
                            int range = (int)(thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude;
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

                if (thisInst.PivotOnly)
                {
                    thisControl.DriveControl = 0;
                }
                if (thisInst.Yield)
                {
                    //Only works with forwards
                    if (thisInst.recentSpeed > 15)
                        thisControl.DriveControl = -0.3f;
                    else
                        thisControl.DriveControl = 0.3f;
                }
                else if (thisInst.BOOST)
                {
                    thisControl.DriveControl = 1;
                    thisControl.m_Movement.FireBoosters(tank);
                }
                else if (thisInst.featherBoost)
                {
                    if (thisInst.featherClock >= 25)
                    {
                        thisControl.m_Movement.FireBoosters(tank);
                        thisInst.featherClock = 0;
                    }
                    thisInst.featherClock++;
                }
                else if (thisInst.forceDrive)
                {
                    thisControl.DriveControl = thisInst.DriveVar;
                }
                /*
                else
                {
                    if (thisInst.PursueThreat && thisInst.lastEnemy != null && thisInst.RangeToChase > thisInst.lastRange)
                    {
                        if (thisInst.FullMelee)
                            thisControl.DriveControl = 1;
                    }
                }
                */
            }
        }


        //Combat handler for DriveDirector
        public static bool TryHandleCombat(AIECore.TankAIHelper thisInst, Tank tank)
        {
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
        public static bool TryCombatEnemy(AIECore.TankAIHelper thisInst, Tank tank, Enemy.RCore.EnemyMind mind)
        {
            bool output = false;
            if (!thisInst.Retreat && thisInst.lastEnemy.IsNotNull())
            {
                output = true;
                thisInst.Steer = true;
                float driveDyna = Mathf.Clamp(((thisInst.lastEnemy.transform.position - tank.boundsCentreWorldNoCheck).magnitude - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (mind.CommanderAttack == Enemy.EnemyAttack.Circle)
                {
                    if (mind.MainFaction == FactionSubTypes.GC)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind));
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind));
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                }
                else
                {
                    if (mind.MainFaction == FactionSubTypes.GC)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind));
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind));
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

        /// <summary>
        /// For allied AI to determine combat readiness
        /// </summary>
        /// <param name="thisInst"></param>
        public static void DetermineCombat(AIECore.TankAIHelper thisInst)
        {
            bool DoNotEngage = false;
            if (thisInst.lastPlayer.IsNotNull())
            {
                if (thisInst.lastBasePos.IsNotNull())
                {
                    if (thisInst.IdealRangeCombat * 2 < (thisInst.lastBasePos.position - thisInst.tank.boundsCentreWorldNoCheck).magnitude && thisInst.DediAI == AIECore.DediAIType.Assault)
                        DoNotEngage = true;
                }
                if (thisInst.IdealRangeCombat < (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck - thisInst.tank.boundsCentreWorldNoCheck).magnitude && thisInst.DediAI != AIECore.DediAIType.Assault)
                    DoNotEngage = true;
                else if (thisInst.AdvancedAI)
                {
                    //WIP
                    if (thisInst.DamageThreshold > 30)
                    {
                        DoNotEngage = true;
                    }
                }
            }
            thisInst.Retreat = DoNotEngage;
        }
    }
}
