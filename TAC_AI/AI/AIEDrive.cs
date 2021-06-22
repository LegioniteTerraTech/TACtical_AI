using UnityEngine;
using System.Reflection;

namespace RandomAdditions.AI
{
    public static class AIEDrive
    {
        public static void DriveDirector(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            thisControl.m_Movement.m_USE_AVOIDANCE = thisInst.AvoidStuff;
            thisInst.Steer = false;
            thisInst.DriveDir = EDriveType.Neutral;
            thisInst.AdviseAway = false;

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
                    thisInst.MinimumRad = 0.5f;
                    thisInst.lastDestination = AIEPathing.GetDriveApproxAir(thisInst.LastCloseAlly, thisInst);
                    if (!thisInst.lastDestination.Approximately(Vector3.zero, 0.5f))
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
                /*
                if (thisInst.recentSpeed < 10 || thisInst.DirectionalHandoffDelay >= 8)//OOp maybe hit something, allow reverse
                    thisInst.DirectionalHandoffDelay++;
                else
                    thisInst.DirectionalHandoffDelay = 0;
                thisInst.DirectionalHandoffDelay++;
                if (thisInst.DirectionalHandoffDelay <= 20 && thisInst.DirectionalHandoffDelay >= 14)
                {   //now go forwards a bit
                    thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
                    thisControl.DriveControl = -0.5f;
                }
                else if (thisInst.DirectionalHandoffDelay >= 8 && thisInst.DirectionalHandoffDelay < 14)
                {   //REVERSE!
                    thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos), 1, TankControl.DriveRestriction.ReverseOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
                    thisControl.DriveControl = -1;
                }
                else if (thisInst.DirectionalHandoffDelay > 20)
                    thisInst.DirectionalHandoffDelay = 0;
                else
                {
                */
                thisInst.Steer = true;
                thisInst.DriveDir = EDriveType.Forwards;
                thisInst.lastDestination = thisInst.AvoidAssistPrecise(thisInst.lastBasePos.position);
                thisInst.MinimumRad = Mathf.Max(thisInst.lastTechExtents - 2, 0.5f);
                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos.position), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
                //}
            }
            else if (thisInst.ProceedToMine)
            {
                if (thisInst.PivotOnly)
                {
                    thisInst.Steer = true;
                    thisInst.lastDestination = thisInst.lastResourcePos;
                    thisInst.MinimumRad = 0;
                    //thisControl.m_Movement.FacePosition(tank, thisInst.lastResourcePos, 1);//Face the music
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.AvoidAssistPrecise(thisInst.lastResourcePos);
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else
                    {
                        thisInst.Steer = true;
                        thisInst.lastDestination = thisInst.AvoidAssistPrecise(thisInst.lastResourcePos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + 2;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastResourcePos), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 5, 0.2f));
                    }
                }
            }
            else if (thisInst.DediAI == AIECore.DediAIType.Aegis)
            {
                bool Combat = TryHandleCombat(thisControl, thisInst, tank);
                if (!Combat)
                {
                    if (thisInst.MoveFromObjective && thisInst.lastPlayer.IsNotNull())
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.lastPlayer.transform.position;
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(tank.transform.position - tank.transform.InverseTransformPoint(thisInst.lastPlayer.transform.position)), 1, TankControl.DriveRestriction.ReverseOnly, thisInst.lastPlayer, 0.5f);
                    }
                    else if (thisInst.ProceedToObjective && thisInst.lastPlayer.IsNotNull())
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastPlayer.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastPlayer.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastPlayer, AIEnhancedCore.Extremes(tank.blockBounds.extents) + AIEnhancedCore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
            }
            else
            {
                bool Combat = TryHandleCombat(thisControl, thisInst, tank);
                if (!Combat)
                {
                    if (thisInst.MoveFromObjective && thisInst.lastPlayer.IsNotNull())
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.lastPlayer.transform.position;
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(tank.transform.position - tank.transform.InverseTransformPoint(thisInst.lastPlayer.transform.position)), 1, TankControl.DriveRestriction.ReverseOnly, thisInst.lastPlayer, 0.5f);
                    }
                    else if (thisInst.ProceedToObjective && thisInst.lastPlayer.IsNotNull())
                    {
                        thisInst.Steer = true;
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastPlayer.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastPlayer.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastPlayer, AIEnhancedCore.Extremes(tank.blockBounds.extents) + AIEnhancedCore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
            }
            if (thisInst.Attempt3DNavi)
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
            else if (thisInst.DediAI == AIECore.DediAIType.Buccaneer)
                thisInst.lastDestination = AIEPathing.OffsetToSea(thisInst.lastDestination, thisInst);
        }

        public static void DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
            if (thisInst.Attempt3DNavi)//3D movement
            {
                float driveMultiplier = 0;

                //AI Steering Rotational
                Vector3 distDiff = thisInst.lastDestination - tank.trans.position;
                Vector3 turnVal;
                if (thisInst.Navi3DDirect == Vector3.zero)
                {   //keep upright!
                    turnVal = tank.rootBlockTrans.InverseTransformDirection(Quaternion.LookRotation(Vector3.forward, Vector3.up).eulerAngles);
                }
                else
                {   //for special cases we want to angle at the enemy
                    Vector3 aim = Quaternion.LookRotation(thisInst.Navi3DDirect, Vector3.up).eulerAngles;
                    turnVal = tank.rootBlockTrans.InverseTransformDirection(Quaternion.LookRotation(Vector3.forward, Vector3.up).eulerAngles);
                    if (Mathf.Abs(aim.y) > 0.5)
                    {
                        aim.x = turnVal.x;
                        aim.z = turnVal.z;
                    }
                    turnVal = tank.rootBlockTrans.InverseTransformDirection(Quaternion.LookRotation(thisInst.Navi3DDirect, Vector3.up).eulerAngles);
                }
                turnVal.y = 0;

                if (thisInst.Steer)
                {
                    thisInst.Navi3DDirect = Vector3.zero;
                    if (thisInst.AdviseAway)
                    {   //Move from target
                        if (thisInst.DriveDir == EDriveType.Forwards)
                        {
                            control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, -tank.trans.forward), 0, 1);
                            AIEPathing.DriveTarget(tank, thisInst.lastDestination, thisControl);
                        }
                        else if (thisInst.DriveDir == EDriveType.Backwards)
                        {
                            control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.trans.forward), 0, 1);
                            AIEPathing.DriveHeading(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, thisControl);
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
                            control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.trans.forward), 0, 1);
                            if (Vector3.Dot(thisInst.lastDestination - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) > 0)
                                AIEPathing.DriveHeading(tank, Vector3.Cross(thisInst.lastDestination, Vector3.down), thisControl);
                            else
                                AIEPathing.DriveHeading(tank, Vector3.Cross(thisInst.lastDestination, Vector3.up), thisControl);
                        }
                        else if (thisInst.DriveDir == EDriveType.Backwards)
                        {
                            control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.trans.forward), 0, 1);
                            AIEPathing.DriveHeading(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, thisControl);
                            //thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);
                        }
                        else if (thisInst.DriveDir == EDriveType.Forwards)
                        {
                            thisInst.Navi3DDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
                            control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.trans.forward), 0, 1);
                        }
                        else
                        {   //Forwards follow but no pitch controls
                            control3D.m_State.m_InputRotation = turnVal * Mathf.Clamp(1 - Vector3.Dot(turnVal, tank.trans.forward), 0, 1);
                            AIEPathing.DriveTarget(tank, thisInst.lastDestination, thisControl);
                        }
                    }
                }
                else
                    control3D.m_State.m_InputRotation = Vector3.zero;

                //AI Drive Translational
                Vector3 driveVal;
                if (thisInst.AdviseAway)
                {   //Move from target
                    driveVal = -tank.transform.InverseTransformPoint(thisInst.lastDestination).normalized;
                    driveMultiplier = 1f;
                }
                else
                {
                    driveVal = tank.transform.InverseTransformPoint(thisInst.lastDestination) / 12;
                    driveVal.Normalize();
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
                if (driveVal.y >= -0.5f && driveVal.y < 0f)
                    driveVal.y = 0; // prevent airships from slam-dunk
                else if (driveVal.y != -1)
                {
                    driveVal.y += 0.5f;
                }

                if (thisInst.PivotOnly)
                {
                    driveMultiplier = 0;
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
                        if (thisInst.DriveDir == EDriveType.Backwards)
                        {
                            AIEPathing.DriveHeading(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, thisControl);
                            thisControl.DriveControl = 1f;
                        }
                        else
                        {
                            AIEPathing.DriveTarget(tank, thisInst.lastDestination, thisControl);
                            thisControl.DriveControl = -1f;
                        }
                    }
                    if (thisInst.DriveDir == EDriveType.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        int range = (int)(thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude;
                        if (range < thisInst.MinimumRad - 1)
                        {
                            AIEPathing.DriveHeading(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, thisControl);
                        }
                        else if (range > thisInst.MinimumRad + 1)
                        {
                            AIEPathing.DriveTarget(tank, thisInst.lastDestination, thisControl);
                        }
                        else  //ORBIT!
                        {
                            if (Vector3.Dot(thisInst.lastDestination - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.right) > 0)
                                AIEPathing.DriveHeading(tank, Vector3.Cross(thisInst.lastDestination, Vector3.down), thisControl);
                            else
                                AIEPathing.DriveHeading(tank, Vector3.Cross(thisInst.lastDestination, Vector3.up), thisControl);
                        }
                        //thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);//Face the music
                        thisControl.DriveControl = -1f;
                    }
                    else if (thisInst.DriveDir == EDriveType.Backwards)
                    {   //Drive to target driving backwards
                        AIEPathing.DriveHeading(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, thisControl);
                        //thisControl.m_Movement.FaceDirection(tank, thisInst.lastDestination - tank.boundsCentreWorldNoCheck, 1);//Face the music
                        thisControl.DriveControl = -1f;
                    }
                    else
                    {
                        AIEPathing.DriveTarget(tank, thisInst.lastDestination, thisControl);
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);//Face the music
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
                    //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos.position), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
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
        public static bool TryHandleCombat(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            bool output = false;
            if (thisInst.PursueThreat && !thisInst.Retreat && thisInst.lastEnemy.IsNotNull() && thisInst.RangeToChase > thisInst.lastRange)
            {
                output = true;
                thisInst.Steer = true;
                float driveDyna = Mathf.Clamp(((tank.boundsCentreWorldNoCheck - thisInst.lastEnemy.transform.position).magnitude - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
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
    }
}
