using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement.AICores
{
    internal class VehicleUtils
    {
        private const float ignoreSteeringAboveAngle = 0.925f;
        private const float strictForwardsLowerSteeringAboveAngle = 0.775f;
        private const float forwardsLowerSteeringAboveAngle = 0.6f;
        private const float MinLookAngleToTurnFineSideways = 0.65f;
        private const float MaxThrottleToTurnFull = 0.75f;
        private const float MaxThrottleToTurnAccurate = 0.5f;
        /// <summary>
        /// Controls how hard the Tech should turn when pursuing a target vector
        /// </summary>
        public static bool Turner(TankControl thisControl, AIECore.TankAIHelper helper, Vector3 destinationVec, ref EControlCoreSet core, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.ToVector2XZ().normalized, helper.tank.rootBlockTrans.forward.ToVector2XZ().normalized);
            if (core.StrictTurning)
            {
                if (forwards > ignoreSteeringAboveAngle)
                {
                    return false;
                }
                else if (forwards > strictForwardsLowerSteeringAboveAngle)
                {
                    float strength = Mathf.Log10(1 + ((1 - forwards) * 9));
                    turnVal = Mathf.Clamp(strength, 0, 1);
                }
                else
                {
                    /* float sped = helper.recentSpeed / Mathf.Max(helper.EstTopSped, 14);
                    if (sped < 0.45f)
                    {
                        if (thisControl.DriveControl.Approximately(0, 0.05f))
                        thisControl.DriveControl = Mathf.Sign(thisControl.DriveControl) * 1;
                        return false;
                    }
                    else if (sped < 0.75f)
                        turnVal = sped;
                    else
                    {*/
                    float strength = Mathf.Log10(1 + Mathf.Max(0, forwards * 9));
                    thisControl.DriveControl = Mathf.Sign(thisControl.DriveControl) * Mathf.Clamp(Mathf.Max(Mathf.Abs(thisControl.DriveControl), strength), -1, 1);
                    //}
                }
                if (turnVal < 0 || turnVal > 1 || float.IsNaN(turnVal))
                    DebugTAC_AI.Exception("Invalid Turnval  NaN " + float.IsNaN(turnVal) + "  negative " + (turnVal < 0));
                return true;
            }
            else
            {
                if (forwards > ignoreSteeringAboveAngle && thisControl.DriveControl >= MaxThrottleToTurnFull)
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
                        else if (forwards > MinLookAngleToTurnFineSideways)
                        {
                            float strength = 1 - (forwards / 1.5f);
                            turnVal = Mathf.Clamp(strength, 0, 1);
                        }
                    }
                    else
                    {
                        if (thisControl.DriveControl <= MaxThrottleToTurnAccurate)
                        {
                            if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngle)
                            {
                                float strength = Mathf.Log10(4 + ((1 - forwards) * 6));
                                turnVal = Mathf.Clamp(strength, 0, 1);
                            }
                        }
                        else if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngle)
                        {
                            float strength = 1 - forwards;
                            turnVal = Mathf.Clamp(strength, 0, 1);
                        }
                    }
                    if (turnVal < 0 || turnVal > 1 || float.IsNaN(turnVal))
                        DebugTAC_AI.Exception("Invalid Turnval  NaN " + float.IsNaN(turnVal) + "  negative " + (turnVal < 0));
                    return true;
                }
            }
        }

        private const float ignoreSteeringAboveAngleAir = 0.95f;
        private const float forwardsLowerSteeringAboveAngleAir = 0.5f;
        private const float MinLookAngleToTurnFineSidewaysAir = 0.65f;
        /// <summary>
        /// Controls how hard the Tech should turn when pursuing a target vector
        /// </summary>
        public static bool TurnerHovership(TankControl thisControl, AIECore.TankAIHelper helper, Vector3 destinationVec, ref EControlCoreSet core, out float turnVal)
        {
            turnVal = 1;
            float forwards = Vector2.Dot(destinationVec.ToVector2XZ().normalized, helper.tank.rootBlockTrans.forward.ToVector2XZ().normalized);

            if (forwards > ignoreSteeringAboveAngleAir)
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
                    else if (forwards > MinLookAngleToTurnFineSidewaysAir)
                    {
                        float strength = 1 - (forwards / 1.5f);
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                else
                {
                    if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngleAir)
                    {
                        float strength = 1 - Mathf.Log10(1 + (forwards * 9));
                        turnVal = Mathf.Clamp(strength, 0, 1);
                    }
                }
                return true;
            }
        }


        public static bool GetPathingTargetRTS(AIControllerDefault controller, out Vector3 pos, ref EControlCoreSet core)
        {
            var help = controller.Helper;
            var tank = help.tank;
            pos = controller.PathPoint;
            try
            {
                help.MinimumRad = 0.5f;
                core.DriveDir = EDriveFacing.Forwards;
                if (help.IsMultiTech)
                {   //Override and disable most driving abilities
                    core.DrivePathing = EDrivePathing.IgnoreAll;

                    pos = MultiTechUtils.HandleMultiTech(help, tank, ref core);
                }
                else
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    bool Combat = false;
                    if (!help.IsGoingToRTSDest)
                        Combat = controller.AICore.TryAdjustForCombat(true, ref pos, ref core); //If we are set to chase then chase with proper AI
                    if (!Combat)
                    {
                        if (help.recentSpeed < 10 && controller.Helper.GetDistanceFromTask(help.RTSDestination) < 32)
                        {
                            if (ManPlayerRTS.HasMovementQueue(help))
                            {
                                help.ForceSetDrive = true;
                                help.DriveVar = 1;
                                core.DrivePathing = EDrivePathing.PrecisePath;
                                core.StrictTurning = true;

                                if (!help.IsGoingToRTSDest)
                                    pos = tank.boundsCentreWorldNoCheck;
                                else
                                    pos = help.RTSDestination;
                            }
                            else
                            {
                                help.PivotOnly = true;
                                core.Stop();

                                if (help.lastEnemyGet != null)
                                {
                                    pos = help.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                                }
                                else
                                {
                                    core.DrivePathing = EDrivePathing.PrecisePath;
                                    core.StrictTurning = true;

                                    if (!help.IsGoingToRTSDest)
                                        pos = tank.boundsCentreWorldNoCheck;
                                    else
                                        pos = help.RTSDestination;
                                }
                            }
                        }
                        else
                        {
                            core.DrivePathing = EDrivePathing.PrecisePath;
                            core.StrictTurning = true;
                            if (!help.IsGoingToRTSDest)
                                pos = tank.boundsCentreWorldNoCheck;
                            else
                                pos = help.RTSDestination;
                        }
                    }
                    else
                        core.DrivePathing = EDrivePathing.OnlyImmedeate;
                }
            }
            catch (Exception e)
            {
                try
                {
                    DebugTAC_AI.Log("TACtical_AI: ERROR IN VehicleAICore - GetPathingTargetRTS");
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
            core.lastDestination = pos;
            return true;
        }

        public static bool GetPathingTarget(AIControllerDefault controller, out Vector3 pos, ref EControlCoreSet core)
        {
            var help = controller.Helper;
            var tank = help.tank;
            pos = controller.PathPoint;
            try
            {
                if (help.IsMultiTech)
                {   //Override and disable most driving abilities
                    core.DrivePathing = EDrivePathing.IgnoreAll;
                    pos = MultiTechUtils.HandleMultiTech(help, tank, ref core);
                }
                else if (help.DriveDestDirected == EDriveDest.ToBase)
                {
                    if (help.lastBasePos.IsNotNull())
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DrivePathing = EDrivePathing.PrecisePath;
                        if (help.Yield)
                        {
                            help.MinimumRad = 0;
                            core.StrictTurning = true;
                        }
                        else
                        {
                            help.MinimumRad = Mathf.Max(help.lastTechExtents - 2, 0.5f);
                        }
                        pos = help.lastBasePos.position;
                    }
                    else
                        core.Stop();
                }
                else if (help.DriveDestDirected == EDriveDest.ToMine)
                {
                    if (help.theResource.tank != null)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        if (help.PivotOnly)
                        {
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                            core.StrictTurning = true;
                            help.MinimumRad = 0;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                            if (help.FullMelee)
                            {
                                help.MinimumRad = 0;
                            }
                            else
                            {
                                help.MinimumRad = help.lastTechExtents + 2;
                            }
                            if (help.Yield)
                                core.StrictTurning = true;
                        }
                        pos = help.theResource.tank.boundsCentreWorldNoCheck;
                    }
                    else
                    {
                        if (help.PivotOnly)
                        {
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                            core.DriveDir = EDriveFacing.Forwards;
                            help.MinimumRad = 0;

                            pos = help.theResource.trans.position;
                        }
                        else
                        {
                            if (help.FullMelee)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                                help.MinimumRad = 0;

                                pos = help.theResource.trans.position;
                            }
                            else
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                                help.MinimumRad = help.lastTechExtents + 2;

                                pos = help.theResource.centrePosition;
                            }
                        }
                    }
                }
                else if (help.DediAI == AIType.Aegis)
                {
                    core.DrivePathing = EDrivePathing.Path;
                    help.theResource = AIEPathing.ClosestUnanchoredAlly(AIEPathing.AllyList(controller.Tank),
                        controller.Tank.boundsCentreWorldNoCheck, Mathf.Pow(help.MaxCombatRange * 2, 2), out float bestval, tank).visible;
                    if (help.lastOperatorRange > help.MaxCombatRange || !controller.AICore.TryAdjustForCombat(true, ref pos, ref core))
                    {
                        if (help.theResource.IsNotNull())
                        {
                            if (help.IsDirectedMovingFromDest)
                            {
                                core.DrivePathing = EDrivePathing.PathInv;
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.FromLastDestination;
                                help.MinimumRad = 0.5f;

                                pos = help.theResource.tank.boundsCentreWorldNoCheck;
                            }
                            else if (help.IsDirectedMovingToDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.ToLastDestination;
                                help.MinimumRad = help.lastTechExtents + help.theResource.GetCheapBounds() + 5;

                                pos = help.theResource.tank.boundsCentreWorldNoCheck;
                            }
                            else
                            {
                                core.DrivePathing = EDrivePathing.IgnoreAll;
                                //DebugTAC_AI.Log("TACtical_AI: AI IDLE");
                                core.Stop();
                            }
                        }
                        else
                        {
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                            core.Stop();
                        }
                    }
                    else
                    {
                        core.DrivePathing = EDrivePathing.IgnoreAll;
                    }
                }
                else
                {
                    if (controller.AICore.TryAdjustForCombat(true, ref pos, ref core))
                    {
                        core.DrivePathing = EDrivePathing.OnlyImmedeate;
                    }
                    else
                    {
                        if (help.PivotOnly)
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                        else
                            core.DrivePathing = EDrivePathing.Path;
                        if (help.lastPlayer)
                        {
                            if (help.IsDirectedMovingFromDest)
                            {
                                core.DrivePathing = EDrivePathing.PathInv;
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.FromLastDestination;
                                help.MinimumRad = 0.5f;

                                pos = help.lastPlayer.tank.boundsCentreWorldNoCheck;
                            }
                            else if (help.IsDirectedMovingToDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                help.MinimumRad = help.lastTechExtents + help.lastPlayer.GetCheapBounds() + 5;

                                pos = help.lastPlayer.tank.boundsCentreWorldNoCheck;
                                if (help.Yield)
                                    core.StrictTurning = true;
                            }
                            else
                            {
                                core.DrivePathing = EDrivePathing.IgnoreAll;
                                help.PivotOnly = true;
                                core.Stop();
                                //DebugTAC_AI.Log("TACtical_AI: AI IDLE");
                            }
                        }
                        else
                        {
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                            core.Stop();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                try
                {
                    DebugTAC_AI.Log("TACtical_AI: ERROR IN VehicleAICore - GetPathingTarget");
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
            core.lastDestination = pos;
            return true;
        }
        
        public static bool GetPathingTargetEnemy(AIControllerDefault controller, out Vector3 pos, ref EControlCoreSet core)
        {
            var help = controller.Helper;
            pos = controller.PathPoint;
            EnemyMind mind = controller.EnemyMind;

            if (mind.IsNull())
                return false;
            if (help.DriveDestDirected == EDriveDest.ToBase)
            {
                if (help.lastBasePos.IsNotNull())
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    help.MinimumRad = Mathf.Max(help.lastTechExtents - 2, 0.5f);
                    core.DrivePathing = EDrivePathing.PrecisePath;

                    pos = help.lastBasePos.position;
                    if (help.Yield)
                        core.StrictTurning = true;
                }
                else
                    core.Stop();
            }
            else if (help.DriveDestDirected == EDriveDest.ToMine)
            {
                if (help.PivotOnly)
                {
                    core.DrivePathing = EDrivePathing.IgnoreAll;
                    core.DriveDir = EDriveFacing.Forwards;
                    help.MinimumRad = 0;

                    pos = help.theResource.trans.position;
                }
                else
                {
                    core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                    core.DriveDir = EDriveFacing.Forwards;
                    if (mind.LikelyMelee)
                    {
                        help.MinimumRad = 0;
                    }
                    else
                    {
                        help.MinimumRad = help.lastTechExtents + 2;
                    }
                    pos = help.theResource.trans.position;
                }
            }
            else
            {
                if (help.Retreat)
                {
                    pos = help.lastDestinationOp;
                    core.DrivePathing = EDrivePathing.Path;
                    core.DriveDest = EDriveDest.ToLastDestination;
                    help.MinimumRad = 0.5f;
                }
                else if (controller.AICore.TryAdjustForCombatEnemy(mind, ref pos, ref core))
                {
                    core.DrivePathing = EDrivePathing.OnlyImmedeate;
                }
                else
                {
                    if (help.PivotOnly)
                        core.DrivePathing = EDrivePathing.IgnoreAll;
                    else
                        core.DrivePathing = EDrivePathing.Path;
                    core.DriveDir = EDriveFacing.Forwards;
                    if (help.IsDirectedMovingFromDest)
                    {
                        core.DriveDest = EDriveDest.FromLastDestination;
                        help.MinimumRad = 0.5f;
                        core.DrivePathing = EDrivePathing.OnlyImmedeate;

                        pos = help.lastDestinationCore;
                    }
                    else if (help.IsDirectedMovingToDest)
                    {
                        core.DriveDest = EDriveDest.ToLastDestination;
                        if (mind.EvilCommander == EnemyHandling.Stationary)
                            help.MinimumRad = 0.5f;
                        else
                            help.MinimumRad = help.lastTechExtents + 8;

                        pos = help.lastDestinationCore;
                        if (help.Yield)
                            core.StrictTurning = true;
                    }
                }
            }
            core.lastDestination = pos;
            return true;
        }

    }
}
