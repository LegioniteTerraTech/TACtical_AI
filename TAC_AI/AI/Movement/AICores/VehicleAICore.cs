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
            try
            {
                if (help.IsMultiTech)
                {   //Override and disable most driving abilities
                    if (help.DediAI == AIType.MTSlave && help.lastEnemy != null)
                    {   // act like a trailer
                        help.DriveDir = EDriveType.Neutral;
                        help.Steer = false;
                        help.lastDestination = this.tank.boundsCentreWorldNoCheck;
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
                            help.lastDestination = help.theResource.trans.position;
                            help.MinimumRad = 0;
                        }
                        else
                        {
                            if (help.FullMelee)
                            {
                                help.Steer = true;
                                help.DriveDir = EDriveType.Forwards;
                                help.lastDestination = help.AvoidAssistPrecise(help.theResource.trans.position);
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
                    bool Combat = TryAdjustForCombat();
                    if (!Combat)
                    {
                        if (help.MoveFromObjective && (bool)help.lastPlayer)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.AdviseAway = true;
                            help.lastDestination = help.lastPlayer.transform.position;//help.AvoidAssistInv(help.lastPlayer.transform.position);
                            help.MinimumRad = 0.5f;
                        }
                        else if (help.ProceedToObjective && (bool)help.lastPlayer)
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
            }
            catch  (Exception e)
            {
                try
                {
                    Debug.Log("TACtical_AI: ERROR IN VehicleAICore");
                    Debug.Log("TACtical_AI: Tank - " + tank.name);
                    Debug.Log("TACtical_AI: Helper - " + (bool)this.controller.Helper);
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
                help.lastDestination = AIEPathing.OffsetFromGround(help.lastDestination, this.controller.Helper);
            else if (help.DediAI == AIType.Buccaneer)
                help.lastDestination = AIEPathing.OffsetToSea(help.lastDestination, tank, this.controller.Helper);

            help.lastDestination = AIEPathing.ModerateMaxAlt(help.lastDestination, help);

            return true;
        }

        public bool DriveDirectorRTS()
        {
            var help = this.controller.Helper;
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
                        help.lastDestination = tank.boundsCentreWorldNoCheck;
                        help.MinimumRad = 0;
                    }
                    else if (help.DediAI == AIType.MTTurret && help.lastEnemy != null)
                    {
                        help.PivotOnly = true;
                        if (help.lastEnemy != null)
                        {
                            help.Steer = true;
                            help.DriveDir = EDriveType.Forwards;
                            help.lastDestination = help.lastEnemy.tank.boundsCentreWorldNoCheck;
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
                        help.lastDestination = help.AvoidAssistPrecise(help.RTSDestination);
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
                                help.lastDestination = help.lastEnemy.tank.boundsCentreWorldNoCheck;
                            }
                            else
                                help.lastDestination = help.AvoidAssistPrecise(help.RTSDestination);
                        }
                        else
                            help.lastDestination = help.AvoidAssistPrecise(help.RTSDestination);

                    }
                }
            }
            catch (Exception e)
            {
                try
                {
                    Debug.Log("TACtical_AI: ERROR IN VehicleAICore (RTS)");
                    Debug.Log("TACtical_AI: Tank - " + tank.name);
                    Debug.Log("TACtical_AI: Helper - " + (bool)this.controller.Helper);
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
                help.lastDestination = AIEPathing.OffsetFromGround(help.lastDestination, this.controller.Helper);
            else if (help.DediAI == AIType.Buccaneer)
                help.lastDestination = AIEPathing.OffsetToSea(help.lastDestination, tank, this.controller.Helper);

            help.lastDestination = AIEPathing.ModerateMaxAlt(help.lastDestination, help);

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
                    help.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.lastBasePos.position, this.controller.Helper, mind);
                    help.MinimumRad = Mathf.Max(help.lastTechExtents - 2, 0.5f);
                }
            }
            else if (help.ProceedToMine)
            {
                if (help.PivotOnly)
                {
                    help.Steer = true;
                    help.DriveDir = EDriveType.Forwards;
                    help.lastDestination = help.theResource.trans.position;
                    help.MinimumRad = 0;
                }
                else
                {
                    if (mind.MainFaction == FactionTypesExt.GC)
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.theResource.trans.position, this.controller.Helper, mind);
                        help.MinimumRad = 0;
                    }
                    else
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;
                        help.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.theResource.trans.position, this.controller.Helper, mind);
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
                        if (help.Attempt3DNavi)
                        {
                            help.DriveDir = EDriveType.Forwards;
                        }
                        else
                        {
                            if (help.Retreat)
                            {
                                help.DriveDir = EDriveType.Backwards;
                            }
                            else
                                help.DriveDir = EDriveType.Forwards;
                        }
                        help.AdviseAway = true;
                        help.lastDestination = RPathfinding.AvoidAssistEnemyInv(this.controller.Tank, help.lastDestination, this.controller.Helper, mind);
                        help.MinimumRad = 0.5f;
                    }
                    else if (help.ProceedToObjective)
                    {
                        help.Steer = true;
                        help.DriveDir = EDriveType.Forwards;

                        help.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, help.lastDestination, this.controller.Helper, mind);
                        
                        if (mind.EvilCommander == EnemyHandling.Stationary)
                            help.MinimumRad = 0.5f;
                        else
                            help.MinimumRad = help.lastTechExtents + 8;
                    }
                }
            }
            if (mind.EvilCommander == EnemyHandling.Naval)
                help.lastDestination = AIEPathing.OffsetToSea(help.lastDestination, tank, this.controller.Helper);
            else if (mind.EvilCommander == EnemyHandling.Starship)
                help.lastDestination = AIEPathing.OffsetFromGround(help.lastDestination, this.controller.Helper);
            else
                help.lastDestination = AIEPathing.OffsetFromGround(help.lastDestination, this.controller.Helper, this.controller.Tank.blockBounds.size.y);
            if (mind.EvilCommander == EnemyHandling.Wheeled)
                help.lastDestination = AIEPathing.OffsetFromSea(help.lastDestination, tank, controller.Helper);

            help.lastDestination = AIEPathing.ModerateMaxAlt(help.lastDestination, help);

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
                Vector3 destDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
                // DEBUG FOR DRIVE ERRORS
#if DEBUG
                Templates.DebugRawTechSpawner.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));
#endif
                //Debug.Log("IS target player " + Singleton.playerTank == thisInst.lastEnemy + " | MinimumRad " + thisInst.MinimumRad + " | destination" + thisInst.lastDestination);
                thisControl.DriveControl = 0f;
                if (thisInst.Steer)
                {
                    if (thisInst.AdviseAway)
                    {   //Move from target
                        if (thisInst.DriveDir == EDriveType.Backwards)//EDriveType.Backwards
                        {   // Face back TOWARDS target
                            if (Turner(thisControl, thisInst, -destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            thisControl.DriveControl = 1f;
                        }
                        else if (thisInst.DriveDir == EDriveType.Perpendicular)
                        {   //Drive to target driving sideways, but obey distance
                            if (Turner(thisControl, thisInst, destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);
                            //Debug.Log("Orbiting away");

                            /*
                            int range = thisInst.LastRange;
                            if (range < thisInst.MinimumRad + 2)
                            {
                                if (Turner(thisControl, thisInst, -destDirect, out float turnVal))
                                    thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            }
                            else if (range > thisInst.MinimumRad + 22)
                            {
                                if (Turner(thisControl, thisInst, -destDirect, out float turnVal))
                                    thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            }
                            else  //ORBIT!
                            {
                                Vector3 aimDirect;
                                if (Vector3.Dot(destDirect, tank.rootBlockTrans.right) < 0)
                                    aimDirect = Vector3.Cross(destDirect.normalized, Vector3.down);
                                else
                                    aimDirect = Vector3.Cross(destDirect.normalized, Vector3.up);
                                if (Turner(thisControl, thisInst, aimDirect, out float turnVal))
                                        thisControl.m_Movement.FaceDirection(tank, aimDirect, turnVal);
                            }*/
                            thisControl.DriveControl = 1f;
                        }
                        else
                        {   // Face front TOWARDS target
                            if (Turner(thisControl, thisInst, destDirect, out float turnVal))
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
                            if (Turner(thisControl, thisInst, -destDirect, out float turnVal))
                                thisControl.m_Movement.FaceDirection(tank, -destDirect, turnVal);
                            //Debug.Log("Orbiting out " + thisInst.MinimumRad + " | " + destDirect);
                        }
                        else if (range > thisInst.MinimumRad + 22)
                        {
                            if (Turner(thisControl, thisInst, destDirect, out float turnVal))
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
                            if (Turner(thisControl, thisInst, aimDirect, out float turnVal))
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
                        //    thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);// need max aiming strength for turning
                        //else
                        if (Turner(thisControl, thisInst, destDirect, out float turnVal))
                            thisControl.m_Movement.FaceDirection(tank, destDirect, turnVal);//Face the music
                        //Debug.Log("TACtical_AI: AI " + tank.name + ":  driving to " + thisInst.lastDestination);
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
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
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
                            //thisInst.Navi3DDirect = thisInst.lastDestination - tank.boundsCentreWorldNoCheck;
                            thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);
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
                    driveVal = InertiaTranslation(-tank.rootBlockTrans.InverseTransformPoint(thisInst.lastDestination).normalized);
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
                    driveVal = InertiaTranslation(-tank.rootBlockTrans.InverseTransformPoint(thisInst.lastDestination).normalized);
                driveMultiplier = 1f;
            }
            else
            {
                if (thisInst.lastEnemy.IsNotNull() && !thisInst.IsMultiTech && AIEPathing.IsUnderMaxAltPlayer(tank.boundsCentreWorldNoCheck))
                {   //level alt with enemy
                    float enemyOffsetH = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck.y;
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformPoint(thisInst.lastDestination).normalized);
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
                    driveVal = InertiaTranslation(tank.rootBlockTrans.InverseTransformPoint(thisInst.lastDestination).normalized);
                    float range = thisInst.lastRange;
                    if (range < thisInst.MinimumRad - 1)
                    {
                        driveMultiplier = 1f;
                        driveVal = InertiaTranslation(-tank.rootBlockTrans.InverseTransformPoint(thisInst.lastDestination).normalized * 0.3f);
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
            if (!thisInst.IsMultiTech)
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

            control3D.m_State.m_InputMovement = (driveVal * Mathf.Clamp(distDiff.magnitude / thisInst.MinimumRad, 0, 1) * driveMultiplier).Clamp01Box();
            controlGet.SetValue(tank.control, control3D);
        }

        public bool TryAdjustForCombat()
        {
            AIECore.TankAIHelper thisInst = this.controller.Helper;
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
                        thisInst.lastDestination = enemyPosApprox;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.lastDestination = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(enemyPosApprox);
                        //thisInst.MinimumRad = 0.5f;
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                    /*
                    else if (driveDyna < 0.5f)
                    {
                        thisInst.PivotOnly = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(enemyPosApprox);
                        //thisInst.MinimumRad = 0.5f;
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }*/
                    else
                    {
                        thisInst.lastDestination = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
                    }
                }
                else
                {
                    thisInst.DriveDir = EDriveType.Forwards;
                    if (thisInst.FullMelee)
                    {
                        thisInst.lastDestination = enemyPosApprox;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.lastDestination = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.AvoidAssist(enemyPosApprox);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 3;
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
                thisInst.lastRangeCombat = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna;
                if (thisInst.Attempt3DNavi)
                    driveDyna = Mathf.Clamp((thisInst.lastRangeCombat - EnemyMind.SpacingRangeAir) / 3f, -1, 1);
                else
                    driveDyna = Mathf.Clamp((thisInst.lastRangeCombat - EnemyMind.SpacingRange) / 3f, -1, 1);
                if (mind.CommanderAttack == EnemyAttack.Circle)
                {   // works fine for now
                    thisInst.DriveDir = EDriveType.Perpendicular;
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!;
                        thisInst.lastDestination = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                    else
                    {
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        //thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 2;
                    }
                    Debug.Log("DriveDyna is " + driveDyna);
                }
                else
                {   // Since the enemy also uses it's Operator in combat, this will have to listen to that
                    if (thisInst.MoveFromObjective)
                    {
                        thisInst.Steer = true;
                        if (thisInst.Attempt3DNavi)
                            thisInst.DriveDir = EDriveType.Forwards;
                        else
                            thisInst.DriveDir = EDriveType.Backwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), this.controller.Helper, mind);
                        thisInst.MinimumRad = 0.5f;
                    }
                    else if (thisInst.ProceedToObjective && mind.LikelyMelee)
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
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                    }
                    else
                    {
                        thisInst.lastDestination = RPathfinding.AvoidAssistEnemy(this.controller.Tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), this.controller.Helper, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                    }
                    /*
                    thisInst.DriveDir = EDriveType.Forwards;
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {
                        thisInst.lastDestination = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5; ;
                    }
                    else
                    {
                        thisInst.lastDestination = RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIECore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5; ;
                    }
                    */
                }
            }
            return output;
        }


        //private const int ignoreTurning = 50;
        private const float ignoreTurning = 0.875f;
        private const float MinThrottleToTurnFull = 0.75f;
        private const float throttleDampen = 0.5f;
        private const float DampeningStrength = 1.75f;
        public bool Turner(TankControl thisControl, AIECore.TankAIHelper helper, Vector3 destinationVec, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.normalized.ToVector2XZ(), tank.rootBlockTrans.forward.ToVector2XZ());

            if (forwards > ignoreTurning && thisControl.DriveControl >= MinThrottleToTurnFull)
                return false;
            if (helper.DriveDir == EDriveType.Perpendicular)
            {
                if (!(bool)helper.LastCloseAlly)
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
                /*
                if (!(bool)helper.LastCloseAlly && forwards > 0.65f)
                {
                    float strength = 1 - forwards;
                    turnVal = Mathf.Clamp(strength, 0, 1);
                }*/
                if (!(bool)helper.LastCloseAlly && forwards > 0.7f)
                {
                    float strength = 1 - forwards;
                    turnVal = Mathf.Clamp(strength, 0, 1);
                }
            }
            return true;
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
                    return direction - Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity * Time.deltaTime) * DampeningStrength, direction);
                }
            }
            else
            {
                if (controller.Helper.AdvancedAI)
                {
                    return direction - Vector3.ProjectOnPlane(tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity * Time.deltaTime) * DampeningStrength, direction);
                }
            }
            return direction * throttleDampen;
        }
    }
}
