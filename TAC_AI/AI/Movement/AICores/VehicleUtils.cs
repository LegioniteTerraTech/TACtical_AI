using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement.AICores
{
    internal static class VehicleUtils
    {
        internal static FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);

        private const float maxSteeringStopDriveBelowAngle = 0.875f;
        private const float ignoreSteeringAboveAngle = 0.925f;
        private const float strictForwardsLowerSteeringAboveAngle = 0.775f;
        private const float forwardsLowerSteeringAboveAngle = 0.6f;
        private const float MinLookAngleToTurnFineSideways = 0.65f;
        private const float MaxThrottleToTurnFull = 0.75f;
        private const float MaxThrottleToTurnAccurate = 0.5f;
        /// <summary>
        /// Controls how hard the Tech should turn when pursuing a target vector
        /// </summary>
        public static bool Turner(TankAIHelper helper, Vector3 destVec, float drive, ref EControlCoreSet core)
        {
            float turnVal;
            float driveVal;
            float forwards = Vector2.Dot(destVec.ToVector2XZ().normalized, helper.tank.rootBlockTrans.forward.ToVector2XZ().normalized);

            switch (core.TurningStrictness)
            {
                case ESteeringStrength.Strict:
                    if (forwards > ignoreSteeringAboveAngle)
                    {
                        return false;
                    }
                    else if (forwards > strictForwardsLowerSteeringAboveAngle)
                    {
                        driveVal = Mathf.Log10(1 + ((1 - forwards) * 9));
                        turnVal = Mathf.Clamp(driveVal, 0, 1);
                    }
                    else
                    {
                        turnVal = 1;
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
                        driveVal = Mathf.Log10(1 + Mathf.Max(0, forwards * 9));
                        helper.DriveControl = Mathf.Sign(drive) * Mathf.Clamp(Mathf.Max(Mathf.Abs(drive), driveVal), -1, 1);
                        //}
                    }
                    if (turnVal < 0 || turnVal > 1 || float.IsNaN(turnVal))
                        DebugTAC_AI.Exception("Invalid Turnval  NaN " + float.IsNaN(turnVal) + "  negative " + (turnVal < 0));
                    if (helper.FixControlReversal(drive))
                        helper.SteerControl(new Vector3(-destVec.x, destVec.y, -destVec.z), turnVal);
                    else
                        helper.SteerControl(destVec, turnVal);
                    return true;
                case ESteeringStrength.MaxSteering:
                    turnVal = 1;
                    if (maxSteeringStopDriveBelowAngle > forwards)
                    {
                        helper.DriveControl = 0;
                    }
                    else
                    {
                        driveVal = Mathf.Log10(1 + Mathf.Max(0, forwards * 9));
                        helper.DriveControl = Mathf.Lerp(0, Mathf.Sign(drive) * Mathf.Clamp(Mathf.Max(Mathf.Abs(drive), driveVal), -1, 1),
                            (forwards - maxSteeringStopDriveBelowAngle) * (1f / (1f - maxSteeringStopDriveBelowAngle)));
                    }
                    if (helper.FixControlReversal(drive))
                        helper.SteerControl(new Vector3(-destVec.x, destVec.y, -destVec.z), turnVal);
                    else
                        helper.SteerControl(destVec, turnVal);
                    return true;
                default:
                    if (forwards > ignoreSteeringAboveAngle && drive >= MaxThrottleToTurnFull)
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
                            else
                                turnVal = 1;
                        }
                        else
                        {
                            if (drive <= MaxThrottleToTurnAccurate)
                            {
                                if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngle)
                                {
                                    float strength = Mathf.Log10(4 + ((1 - forwards) * 6));
                                    turnVal = Mathf.Clamp(strength, 0, 1);
                                }
                                else
                                    turnVal = 1;
                            }
                            else if (!(bool)helper.lastCloseAlly && forwards > forwardsLowerSteeringAboveAngle)
                            {
                                float strength = 1 - forwards;
                                turnVal = Mathf.Clamp(strength, 0, 1);
                            }
                            else
                                turnVal = 1;
                        }
                        if (turnVal < 0 || turnVal > 1 || float.IsNaN(turnVal))
                            DebugTAC_AI.Exception("Invalid Turnval  NaN " + float.IsNaN(turnVal) + "  negative " + (turnVal < 0));
                    }
                    if (helper.FixControlReversal(drive))
                        helper.SteerControl(new Vector3(-destVec.x, destVec.y, -destVec.z), turnVal);
                    else
                        helper.SteerControl(destVec, turnVal);
                    return true;
            }
        }

        private const float ignoreSteeringAboveAngleAir = 0.95f;
        private const float forwardsLowerSteeringAboveAngleAir = 0.5f;
        private const float MinLookAngleToTurnFineSidewaysAir = 0.65f;
        /// <summary>
        /// Controls how hard the Tech should turn when pursuing a target vector
        /// </summary>
        public static void TurnerHovership(TankControl thisControl, TankAIHelper helper, Vector3 destVec, ref EControlCoreSet core)
        {
            Transform rootBlock = helper.tank.rootBlockTrans;
            float turnVal = 1;
            float forwards = Vector2.Dot(destVec.ToVector2XZ().normalized, rootBlock.forward.ToVector2XZ().normalized);

            if (forwards <= ignoreSteeringAboveAngleAir)
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
                if (helper.FixControlReversal(rootBlock.InverseTransformVector(destVec).z))
                    thisControl.m_Movement.FaceDirection(helper.tank, new Vector3(-destVec.x, destVec.y, -destVec.z), turnVal);
                else
                    thisControl.m_Movement.FaceDirection(helper.tank, destVec, turnVal);
            }
        }


        public static void ModerateThrust3D(TankAIHelper helper, ref Vector3 driveVal, Vector3 TtWRatios, Vector3 tankPos, Vector3 localSpaceTargPos, float propLerpSpeed)
        {
            ModerateThrustAxis(helper, ref driveVal.x, TtWRatios.x, tankPos.x, localSpaceTargPos.x, propLerpSpeed);
            ModerateThrustAxis(helper, ref driveVal.y, TtWRatios.y, tankPos.y, localSpaceTargPos.y, propLerpSpeed);
            ModerateThrustAxis(helper, ref driveVal.z, TtWRatios.z, tankPos.z, localSpaceTargPos.z, propLerpSpeed);
        }
        public static void ModerateThrustAxis(TankAIHelper helper, ref float driveVal, float TtWRatio, float tankPos, float localSpaceTargPos, float propLerpSpeed)
        {
            float deltaVelo = localSpaceTargPos - tankPos - helper.SafeVelocity.y;

            float timeCurToReach;

            float deltaThrottle;
            if (deltaVelo < 0f)
            {
                deltaThrottle = -propLerpSpeed * 0.9f;
                float curAccel = ((TtWRatio * driveVal) - 1f) * TankAIManager.GravMagnitude;
                if (curAccel < 0f)
                    timeCurToReach = deltaVelo / Mathf.Min(curAccel, -0.001f);
                else
                    timeCurToReach = 9001f;
            }
            else
            {
                deltaThrottle = propLerpSpeed * 0.9f;
                float curAccel = ((TtWRatio * driveVal) - 1f) * TankAIManager.GravMagnitude;
                if (curAccel > 0f)
                    timeCurToReach = deltaVelo / Mathf.Max(curAccel, 0.001f);
                else
                    timeCurToReach = 9001f;
            }

            if (timeCurToReach > 1f)
                driveVal += deltaThrottle;
            else // throttleShiftDelay >= timeCurToReach
                driveVal += deltaThrottle * timeCurToReach;
        }


        public static bool GetPathingTargetRTS(AIControllerDefault controller, out Vector3 pos, ref EControlCoreSet core)
        {
            var helper = controller.Helper;
            var tank = helper.tank;
            pos = controller.PathPoint;
            try
            {
                helper.AutoSpacing = 0.5f;
                core.DriveDir = EDriveFacing.Forwards;
                if (helper.IsMultiTech)
                {   //Override and disable most driving abilities
                    core.DrivePathing = EDrivePathing.IgnoreAll;

                    pos = MultiTechUtils.HandleMultiTech(helper, tank, ref core);
                }
                else
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    bool Combat = false;
                    if (!helper.IsGoingToPositionalRTSDest)
                        Combat = controller.AICore.TryAdjustForCombat(true, ref pos, ref core); //If we are set to chase then chase with proper AI
                    if (Combat)
                        core.DrivePathing = EDrivePathing.OnlyImmedeate;
                    else
                    {
                        if (helper.recentSpeed < 10 && controller.Helper.GetDistanceFromTask(helper.RTSDestination) < 32)
                        {
                            if (ManNetwork.IsNetworked || ManWorldRTS.HasMovementQueue(helper))
                            {
                                helper.ThrottleState = AIThrottleState.ForceSpeed;
                                helper.DriveVar = 1;
                                core.DrivePathing = EDrivePathing.PrecisePath;
                                core.TurningStrictness = ESteeringStrength.Strict;

                                if (!helper.IsGoingToPositionalRTSDest)
                                    pos = tank.boundsCentreWorldNoCheck;
                                else
                                    pos = helper.RTSDestination;
                            }
                            else
                            {
                                helper.ThrottleState = AIThrottleState.PivotOnly;
                                core.Stop();

                                if (helper.lastEnemyGet != null)
                                {
                                    pos = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                                }
                                else
                                {
                                    core.DrivePathing = EDrivePathing.PrecisePath;
                                    core.TurningStrictness = ESteeringStrength.Strict;

                                    if (!helper.IsGoingToPositionalRTSDest)
                                        pos = tank.boundsCentreWorldNoCheck;
                                    else
                                        pos = helper.RTSDestination;
                                }
                            }
                        }
                        else
                        {
                            core.DrivePathing = EDrivePathing.PrecisePath;
                            core.TurningStrictness = ESteeringStrength.Strict;
                            if (!helper.IsGoingToPositionalRTSDest)
                            {
                                pos = tank.boundsCentreWorldNoCheck;
                                core.Stop();
                            }
                            else
                                pos = helper.RTSDestination;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ERROR IN VehicleAICore - GetPathingTargetRTS");
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
            core.lastDestination = pos;
            return true;
        }

        public static bool GetPathingTargetRTSEnemy(AIControllerDefault controller, out Vector3 pos, ref EControlCoreSet core)
        {
            var helper = controller.Helper;
            var tank = helper.tank;
            pos = controller.PathPoint;
            EnemyMind mind = controller.EnemyMind;
            try
            {
                helper.AutoSpacing = 0.5f;
                core.DriveDir = EDriveFacing.Forwards;
                if (helper.IsMultiTech)
                {   //Override and disable most driving abilities
                    core.DrivePathing = EDrivePathing.IgnoreAll;

                    pos = MultiTechUtils.HandleMultiTech(helper, tank, ref core);
                }
                else
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    bool Combat = false;
                    if (!helper.IsGoingToPositionalRTSDest)
                        Combat = controller.AICore.TryAdjustForCombatEnemy(mind, ref pos, ref core); //If we are set to chase then chase with proper AI
                    if (Combat)
                        core.DrivePathing = EDrivePathing.OnlyImmedeate;
                    else
                    {
                        if (helper.recentSpeed < 10 && controller.Helper.GetDistanceFromTask(helper.RTSDestination) < 32)
                        {
                            if (ManNetwork.IsNetworked || ManWorldRTS.HasMovementQueue(helper))
                            {
                                helper.ThrottleState = AIThrottleState.ForceSpeed;
                                helper.DriveVar = 1;
                                core.DrivePathing = EDrivePathing.PrecisePath;
                                core.TurningStrictness = ESteeringStrength.Strict;

                                if (!helper.IsGoingToPositionalRTSDest)
                                    pos = tank.boundsCentreWorldNoCheck;
                                else
                                    pos = helper.RTSDestination;
                            }
                            else
                            {
                                helper.ThrottleState = AIThrottleState.PivotOnly;
                                core.Stop();

                                if (helper.lastEnemyGet != null)
                                {
                                    pos = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                                }
                                else
                                {
                                    core.DrivePathing = EDrivePathing.PrecisePath;
                                    core.TurningStrictness = ESteeringStrength.Strict;

                                    if (!helper.IsGoingToPositionalRTSDest)
                                        return GetPathingTargetEnemy(controller, out pos, ref core);
                                    else
                                        pos = helper.RTSDestination;
                                }
                            }
                        }
                        else
                        {
                            core.DrivePathing = EDrivePathing.PrecisePath;
                            core.TurningStrictness = ESteeringStrength.Strict;
                            if (!helper.IsGoingToPositionalRTSDest)
                                return GetPathingTargetEnemy(controller, out pos, ref core);
                            else
                                pos = helper.RTSDestination;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ERROR IN VehicleAICore - GetPathingTargetRTS");
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
            core.lastDestination = controller.GetDestination();
            return true;
        }

        public static bool GetPathingTarget(AIControllerDefault controller, out Vector3 pos, ref EControlCoreSet core)
        {
            var helper = controller.Helper;
            var tank = helper.tank;
            pos = controller.PathPoint;
            try
            {
                if (helper.IsMultiTech)
                {   //Override and disable most driving abilities
                    core.DrivePathing = EDrivePathing.IgnoreAll;
                    pos = MultiTechUtils.HandleMultiTech(helper, tank, ref core);
                }
                else if (helper.DriveDestDirected == EDriveDest.Override)
                {
                    core.DrivePathing = EDrivePathing.IgnoreAll;
                    core.DriveDir = EDriveFacing.Forwards;
                    core.DriveDest = EDriveDest.Override;
                }
                else if (helper.DriveDestDirected == EDriveDest.ToBase)
                {
                    if (helper.lastBasePos.IsNotNull())
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DrivePathing = EDrivePathing.PrecisePath;
                        if (helper.ThrottleState == AIThrottleState.PivotOnly)
                        {
                            helper.AutoSpacing = 0;
                            core.TurningStrictness = ESteeringStrength.Strict;
                        }
                        else
                        {
                            helper.AutoSpacing = Mathf.Max(helper.lastTechExtents - 2, 0.5f);
                        }
                        pos = helper.lastBasePos.position;
                    }
                    else
                    {
                        core.Stop();
                        DebugTAC_AI.LogDevOnly("lastBasePos is null when " + helper.name + " was told to go to the base");
                    }
                }
                else if (helper.DriveDestDirected == EDriveDest.ToMine)
                {
                    if (helper.theResource == null)
                    {
                        core.Stop();
                        DebugTAC_AI.LogDevOnly("theResource is null when " + helper.name + " was told to go to the mines");
                    }
                    else
                    {
                        if (helper.theResource.tank != null)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            if (helper.ThrottleState == AIThrottleState.PivotOnly)
                            {
                                core.DrivePathing = EDrivePathing.IgnoreAll;
                                core.TurningStrictness = ESteeringStrength.Strict;
                                helper.AutoSpacing = 0;
                            }
                            else
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                                if (helper.FullMelee)
                                {
                                    helper.AutoSpacing = 0;
                                }
                                else
                                {
                                    helper.AutoSpacing = helper.lastTechExtents + 2;
                                }
                                if (helper.ThrottleState == AIThrottleState.Yield)
                                    core.TurningStrictness = ESteeringStrength.Strict;
                            }
                            pos = helper.theResource.tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            if (helper.ThrottleState == AIThrottleState.PivotOnly)
                            {
                                core.DrivePathing = EDrivePathing.IgnoreAll;
                                core.DriveDir = EDriveFacing.Forwards;
                                helper.AutoSpacing = 0;

                                pos = helper.theResource.trans.position;
                            }
                            else
                            {
                                if (helper.FullMelee)
                                {
                                    core.DriveDir = EDriveFacing.Forwards;
                                    core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                                    helper.AutoSpacing = 0;

                                    pos = helper.theResource.trans.position;
                                }
                                else
                                {
                                    core.DriveDir = EDriveFacing.Forwards;
                                    core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                                    helper.AutoSpacing = helper.lastTechExtents + 2;

                                    pos = helper.theResource.trans.position;
                                }
                            }
                        }
                    }
                }
                else if (helper.DediAI == AIType.Aegis)
                {
                    if (helper.ThrottleState == AIThrottleState.PivotOnly)
                        core.DrivePathing = EDrivePathing.IgnoreAll;
                    else
                        core.DrivePathing = EDrivePathing.Path;
                    helper.theResource = AIEPathing.ClosestUnanchoredAllyAegis(TankAIManager.GetTeamTanks(controller.Tank.Team),
                        controller.Tank.boundsCentreWorldNoCheck, Mathf.Pow(helper.MaxCombatRange * 2, 2), out float bestval, helper)?.visible;
                    if (helper.lastOperatorRange > helper.MaxCombatRange || !controller.AICore.TryAdjustForCombat(true, ref pos, ref core))
                    {
                        if (helper.theResource.IsNotNull())
                        {
                            if (helper.IsDirectedMovingFromDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.FromLastDestination;
                                helper.AutoSpacing = 0;//0.5f;

                                pos = helper.theResource.tank.boundsCentreWorldNoCheck;
                            }
                            else if (helper.IsDirectedMovingToDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.ToLastDestination;
                                helper.AutoSpacing = helper.lastTechExtents + helper.theResource.GetCheapBounds() + 5;

                                pos = helper.theResource.tank.boundsCentreWorldNoCheck;
                            }
                            else
                            {
                                core.DrivePathing = EDrivePathing.IgnoreAll;
                                //DebugTAC_AI.Log(KickStart.ModID + ": AI IDLE");
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
                        if (helper.ThrottleState == AIThrottleState.PivotOnly)
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                        else
                            core.DrivePathing = EDrivePathing.Path;
                        if (helper.lastPlayer)
                        {
                            if (helper.IsDirectedMovingFromDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.FromLastDestination;
                                helper.AutoSpacing = 0.01f;//0.5f;

                                pos = helper.lastPlayer.tank.boundsCentreWorldNoCheck;
                            }
                            else if (helper.IsDirectedMovingToDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                helper.AutoSpacing = helper.lastTechExtents + helper.lastPlayer.GetCheapBounds() + 5;

                                pos = helper.lastPlayer.tank.boundsCentreWorldNoCheck;
                                if (helper.ThrottleState == AIThrottleState.Yield)
                                    core.TurningStrictness = ESteeringStrength.Strict;
                            }
                            else
                            {
                                core.DrivePathing = EDrivePathing.IgnoreAll;
                                helper.ThrottleState = AIThrottleState.PivotOnly;
                                core.Stop();
                                //DebugTAC_AI.Log(KickStart.ModID + ": AI IDLE");
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
                    DebugTAC_AI.Log(KickStart.ModID + ": ERROR IN VehicleAICore - GetPathingTarget");
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
            core.lastDestination = controller.GetTargetDestination();
            return true;
        }
        
        public static bool GetPathingTargetEnemy(AIControllerDefault controller, out Vector3 pos, ref EControlCoreSet core)
        {
            int errorCode = 0;
            var helper = controller.Helper;
            try
            {
                pos = controller.PathPoint;
                EnemyMind mind = controller.EnemyMind;

                if (mind.IsNull())
                    return false;
                if (helper.DriveDestDirected == EDriveDest.Override)
                {
                    errorCode = 100;
                    core.DrivePathing = EDrivePathing.IgnoreAll;
                    core.DriveDir = EDriveFacing.Forwards;
                    core.DriveDest = EDriveDest.Override;
                }
                else if (helper.DriveDestDirected == EDriveDest.ToBase)
                {
                    errorCode = 200;
                    if (helper.lastBasePos.IsNotNull())
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        helper.AutoSpacing = Mathf.Max(helper.lastTechExtents - 2, 0.5f);
                        core.DrivePathing = EDrivePathing.PrecisePath;

                        pos = helper.lastBasePos.position;
                        if (helper.ThrottleState == AIThrottleState.Yield)
                            core.TurningStrictness = ESteeringStrength.Strict;
                    }
                    else
                        core.Stop();
                }
                else if (helper.DriveDestDirected == EDriveDest.ToMine)
                {
                    errorCode = 300;
                    if (helper.theResource == null)
                    {
                        core.Stop();
                        DebugTAC_AI.Log("theResource is null when " + helper.name + " was told to go to the mines");

                    }
                    else
                    {
                        if (helper.ThrottleState == AIThrottleState.PivotOnly)
                        {
                            errorCode = 301;
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                            core.DriveDir = EDriveFacing.Forwards;
                            helper.AutoSpacing = 0;

                            pos = helper.theResource.trans.position;
                        }
                        else
                        {
                            errorCode = 302;
                            core.DrivePathing = EDrivePathing.PrecisePathIgnoreScenery;
                            core.DriveDir = EDriveFacing.Forwards;
                            if (mind.LikelyMelee)
                            {
                                helper.AutoSpacing = 0;
                            }
                            else
                            {
                                helper.AutoSpacing = helper.lastTechExtents + 2;
                            }
                            pos = helper.theResource.trans.position;
                        }
                    }
                }
                else if (mind.CommanderMind == EnemyAttitude.Guardian)
                {
                    if (helper.ThrottleState == AIThrottleState.PivotOnly)
                        core.DrivePathing = EDrivePathing.IgnoreAll;
                    else
                        core.DrivePathing = EDrivePathing.Path;
                    helper.theResource = AIEPathing.ClosestUnanchoredAllyAegis(TankAIManager.GetTeamTanks(controller.Tank.Team),
                        controller.Tank.boundsCentreWorldNoCheck, Mathf.Pow(helper.MaxCombatRange * 2, 2),
                        out float bestval, helper)?.visible;
                    if (helper.lastOperatorRange > helper.MaxCombatRange || !controller.AICore.TryAdjustForCombat(true, ref pos, ref core))
                    {
                        if (helper.theResource.IsNotNull())
                        {
                            if (helper.IsDirectedMovingFromDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.FromLastDestination;
                                helper.AutoSpacing = 0;//0.5f;

                                pos = helper.theResource.tank.boundsCentreWorldNoCheck;
                            }
                            else if (helper.IsDirectedMovingToDest)
                            {
                                core.DriveDir = EDriveFacing.Forwards;
                                core.DriveDest = EDriveDest.ToLastDestination;
                                helper.AutoSpacing = helper.lastTechExtents + helper.theResource.GetCheapBounds() + 5;

                                pos = helper.theResource.tank.boundsCentreWorldNoCheck;
                            }
                            else
                            {
                                core.DrivePathing = EDrivePathing.IgnoreAll;
                                //DebugTAC_AI.Log(KickStart.ModID + ": AI IDLE");
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
                    errorCode = 400;
                    if (helper.Retreat)
                    {
                        pos = helper.lastDestinationOp;
                        core.DrivePathing = EDrivePathing.Path;
                        core.DriveDest = EDriveDest.ToLastDestination;
                        helper.AutoSpacing = 0.5f;
                    }
                    else if (controller.AICore.TryAdjustForCombatEnemy(mind, ref pos, ref core))
                    {
                        core.DrivePathing = EDrivePathing.OnlyImmedeate;
                    }
                    else
                    {
                        if (helper.ThrottleState == AIThrottleState.PivotOnly)
                            core.DrivePathing = EDrivePathing.IgnoreAll;
                        else
                            core.DrivePathing = EDrivePathing.Path;
                        core.DriveDir = EDriveFacing.Forwards;
                        if (helper.IsDirectedMovingFromDest)
                        {
                            core.DriveDest = EDriveDest.FromLastDestination;
                            helper.AutoSpacing = 0.01f;
                            //help.MinimumRad = 0.5f;
                            //core.DrivePathing = EDrivePathing.OnlyImmedeate;

                            pos = helper.lastDestinationCore;
                        }
                        else if (helper.IsDirectedMovingToDest)
                        {
                            core.DriveDest = EDriveDest.ToLastDestination;
                            if (mind.EvilCommander == EnemyHandling.Stationary)
                                helper.AutoSpacing = 0.5f;
                            else
                                helper.AutoSpacing = helper.lastTechExtents + 8;

                            pos = helper.lastDestinationCore;
                            if (helper.ThrottleState == AIThrottleState.Yield)
                                core.TurningStrictness = ESteeringStrength.Strict;
                        }
                    }
                }
                core.lastDestination = pos;
                return true;
            }
            catch (NullReferenceException)
            {
                DebugTAC_AI.Assert("GetPathingTargetEnemy - ERROR " + errorCode);
                helper.theResource = null;
                throw;
            }
        }

    }
}
