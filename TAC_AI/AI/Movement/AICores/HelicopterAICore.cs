using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TerraTechETCUtil;

namespace TAC_AI.AI.Movement.AICores
{
    internal class HelicopterAICore : IMovementAICore
    {
        private AIControllerAir pilot;
        internal TankAIHelper Helper => pilot.Helper;
        private Tank tank;
        private float groundOffset => Helper.GroundOffsetHeight;
        private float groundOffsetEmerg => AIGlobals.GroundOffsetCrashWarnChopperDelta + Helper.GroundOffsetHeight;
        public float GetDrive => pilot.CurrentThrottle;

        public void Initiate(Tank tank, IMovementAIController pilotSet)
        {
            this.tank = tank;
            pilot = (AIControllerAir) pilotSet;
            pilot.FlyStyle = AIControllerAir.FlightType.Helicopter;

            //pilot.FlyingChillFactor = Vector3.one * 30;
            pilot.FlyingChillFactor.x = AIGlobals.ChopperXZChillFactorMulti * pilot.PropLerpValue;
            pilot.FlyingChillFactor.z = AIGlobals.ChopperXZChillFactorMulti * pilot.PropLerpValue;
            if (pilot.LargeAircraft)
                pilot.FlyingChillFactor.y = 2.5f;    // need accuraccy for large chopper bombing runs
            else
                pilot.FlyingChillFactor.y = AIGlobals.ChopperYChillFactorMulti * pilot.PropLerpValue;

            if (tank.rbody && pilot.UpTtWRatio < 1f)
            {
                float GravityForce = tank.rbody.mass * tank.GetGravityScale() * TankAIManager.GravMagnitude;
                DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + tank.name + " does not apply enough upwards thrust " +
                    pilot.UpThrust + " vs " + GravityForce + " to be a helicopter.");
            }
            float Height = tank.blockman.blockCentreBounds.size.y;
            Helper.GroundOffsetHeight = Height + AIGlobals.GroundOffsetChopper
                + (AIGlobals.GroundOffsetChopperExtra * Mathf.Clamp01(Height / 64));
        }
        public bool DriveMaintainer(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is GROUNDED!!!");
                if (!AIEPathing.AboveHeightFromGroundTech(helper, helper.lastTechExtents))
                {
                    DriveMaintainerEmergLand(helper, tank, ref core);
                    return false;
                }
                //WIP - Try fighting the controls to land safely
                if (helper.SafeVelocity.y > 0.1f)
                {
                    pilot.ErrorsInTakeoff = 0;
                    pilot.Grounded = false;
                }
                else
                {
                    pilot.ForcePitchUp = true;
                    pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, helper, pilot, 500000, true);
                    HelicopterUtils.UpdateThrottleCopter(pilot);
                    HelicopterUtils.AngleTowardsUp(pilot, pilot.PathPointSet, helper.lastDestinationCore, ref core, true);
                    return true;
                }
            }

            if (tank.beam.IsActive)
            {   // BEAMING
                pilot.MainThrottle = 0;
                pilot.AdvisedThrottle = 0;
                pilot.CurrentThrottle = 0;
                HelicopterUtils.UpdateThrottleCopter(pilot);
                HelicopterUtils.AngleTowardsUp(pilot, pilot.PathPointSet, helper.lastDestinationCore, ref core, true);
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is taking off");
                Vector3 pos = tank.boundsCentreWorldNoCheck;
                AIEPathMapper.GetAltitudeLoadedOnly(pos, out float height);
                float targetHeight;
                if (AIEPathing.IsUnderMaxAltPlayer(pilot.PathPointSet.y))
                    targetHeight = Mathf.Max(pilot.PathPointSet.y, height);
                else
                    targetHeight = height;
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, helper, pilot, targetHeight, true);
                HelicopterUtils.UpdateThrottleCopter(pilot);
                HelicopterUtils.AngleTowardsUp(pilot, pilot.PathPointSet, helper.lastDestinationCore, ref core, true);
                if (AIGlobals.ShowDebugFeedBack)
                {   // DEBUG FOR DRIVE ERRORS
                    DebugExtUtilities.DrawDirIndicator(pos.SetY(height), pos.SetY(targetHeight), new Color(1, 0, 1));
                }
            }
            else
            {   // Normal flight
                Vector3 pos = tank.boundsCentreWorldNoCheck;
                AIEPathMapper.GetAltitudeLoadedOnly(pos, out float height);
                float targetHeight;
                if (AIEPathing.IsUnderMaxAltPlayer(pilot.PathPointSet.y))
                    targetHeight = Mathf.Max(pilot.PathPointSet.y, height);
                else
                    targetHeight = height;
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, helper, pilot, targetHeight);
                HelicopterUtils.UpdateThrottleCopter(pilot);
                HelicopterUtils.AngleTowardsUp(pilot, pilot.PathPointSet, helper.lastDestinationCore, ref core);
                if (AIGlobals.ShowDebugFeedBack)
                {   // DEBUG FOR DRIVE ERRORS
                    DebugExtUtilities.DrawDirIndicator(pos.SetY(height), pos.SetY(targetHeight), new Color(1, 0, 1));
                }
                /*
                if (helper.lastIsNotNull())
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is in combat at " + pilot.AirborneDest + " tank at " + helper.lastEnemy.tank.boundsCentreWorldNoCheck);
                }
                */
            }

            return true;
        }

        public void WatchStability()
        {
            float upVal = tank.rootBlockTrans.up.y;
            if (!AIEPathing.AboveHeightFromGround(Helper.DodgeSphereCenter, groundOffsetEmerg) ||
                upVal < AIGlobals.ChopperMaxAnglePercent)
                pilot.ForcePitchUp = true;
            else if (upVal > 0.275f)
            {
                pilot.ForcePitchUp = false;
                pilot.ErrorsInTakeoff = 0;
            }
            /*
            if (pilot.ForcePitchUp && Helper.SafeVelocity.y < 0.1f)
            {
                // DebugTAC_AI.Log(KickStart.ModID + ": Tech " + pilot.Tank.name + "  Avoiding Ground!");
                pilot.ErrorsInTakeoff += KickStart.AIDodgeCheapness;
                if (pilot.ErrorsInTakeoff > AIGlobals.MaxTakeoffFailiures)
                {
                    if (!pilot.Grounded)
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + pilot.Tank.name + "  HAS BEEN DEEMED INCAPABLE OF FLIGHT!");
                    pilot.Grounded = true;
                }
            }
            */
        }

        /// <summary>
        /// A very limited version of the VehicleAICore DriveMaintainer for downed aircraft
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /// <returns></returns>
        public bool DriveMaintainerEmergLand(TankAIHelper helper, Tank tank, ref EControlCoreSet core)
        {
            TankControl.ControlState control3D = (TankControl.ControlState)VehicleUtils.controlGet.GetValue(tank.control);

            control3D.m_State.m_InputRotation = Vector3.zero;
            control3D.m_State.m_InputMovement = Vector3.zero;
            VehicleUtils.controlGet.SetValue(tank.control, control3D);
            Vector3 destDirect = helper.lastDestinationOp - tank.boundsCentreWorldNoCheck;
            // DEBUG FOR DRIVE ERRORS
            if (AIGlobals.ShowDebugFeedBack)
                DebugExtUtilities.DrawDirIndicator(tank.gameObject, 0, destDirect, new Color(0, 1, 1));

            helper.DriveControl = 0f;
            if (helper.DoSteerCore)
            {
                if (helper.AdviseAwayCore)
                {   //Move from target
                    if (core.DriveDir == EDriveFacing.Backwards)//EDriveType.Backwards
                    {   // Face back TOWARDS target
                        VehicleUtils.Turner(helper, -destDirect, 0, ref core);
                        helper.DriveControl = 1f;
                    }
                    else if (core.DriveDir == EDriveFacing.Perpendicular)
                    {   //Drive to target driving sideways, but obey distance
                        VehicleUtils.Turner(helper, -destDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting away");
                        helper.DriveControl = 1f;
                    }
                    else
                    {   // Face front TOWARDS target
                        VehicleUtils.Turner(helper, destDirect, 0, ref core);
                        helper.DriveControl = -1f;
                    }
                }
                else if (core.DriveDir == EDriveFacing.Perpendicular)
                {   //Drive to target driving sideways, but obey distance
                    //int range = (int)(destDirect).magnitude;
                    float range = helper.lastOperatorRange;
                    if (range < helper.AutoSpacing + 2)
                    {
                        VehicleUtils.Turner(helper, -destDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting out " + helper.MinimumRad + " | " + destDirect);
                    }
                    else if (range > helper.AutoSpacing + 22)
                    {
                        VehicleUtils.Turner(helper, destDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting in " + helper.MinimumRad);
                    }
                    else  //ORBIT!
                    {
                        Vector3 aimDirect;
                        if (Vector3.Dot(destDirect.normalized, tank.rootBlockTrans.right) < 0)
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.down);
                        else
                            aimDirect = Vector3.Cross(destDirect.normalized, Vector3.up);
                        VehicleUtils.Turner(helper, aimDirect, 0, ref core);
                        //DebugTAC_AI.Log("Orbiting hold " + helper.MinimumRad);
                    }
                    helper.DriveControl = 1f;
                }
                else
                {
                    VehicleUtils.Turner(helper, destDirect, 0, ref core);//Face the music
                                                                                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  driving to " + helper.lastDestination);
                    if (helper.AutoSpacing > 0)
                    {
                        //if (helper.DriveDir == EDriveType.Perpendicular)
                        //    helper.DriveControl = 1f;
                        float range = helper.lastOperatorRange;
                        if (core.DriveDir <= EDriveFacing.Neutral)
                            helper.DriveControl = 0f;
                        else if (range < helper.AutoSpacing - 1)
                        {
                            if (core.DriveDir == EDriveFacing.Forwards)
                                helper.DriveControl = -1f;
                            else if (core.DriveDir == EDriveFacing.Backwards)
                                helper.DriveControl = 1f;
                            else
                                helper.DriveControl = 0;

                        }
                        else if (range > helper.AutoSpacing + 1)
                        {
                            if (core.DriveDir == EDriveFacing.Forwards)
                                helper.DriveControl = 1f;
                            else if (core.DriveDir == EDriveFacing.Backwards)
                                helper.DriveControl = -1f;
                            else
                                helper.DriveControl = 1f;
                        }
                    }
                }
            }
            else
                helper.DriveControl = 0;

            // Overrides to translational drive
            if (core.DriveDir == EDriveFacing.Stop)
            {
                helper.DriveControl = 0f;
                return true;
            }
            if (core.DriveDir == EDriveFacing.Neutral)
            {   // become brakeless
                helper.DriveControl = 0.001f;
                return true;
            }

            // Operate normally
            switch (helper.ThrottleState)
            {
                case AIThrottleState.PivotOnly:
                    helper.DriveControl = 0;
                    break;
                case AIThrottleState.Yield:
                    if (core.DriveDir == EDriveFacing.Backwards)
                    {
                        if (helper.recentSpeed > 10)
                            helper.DriveControl = 0.2f;
                        else
                            helper.DriveControl = -1f;
                    }
                    else
                    {   // works with forwards
                        if (helper.recentSpeed > 10)
                            helper.DriveControl = -0.2f;
                        else
                            helper.DriveControl = 1f;
                    }
                    break;
                case AIThrottleState.FullSpeed:
                    if (helper.FullBoost || helper.LightBoost)
                        helper.DriveControl = 1;
                    break;
                case AIThrottleState.ForceSpeed:
                    helper.DriveControl = helper.DriveVar;
                    // Downed Aircraft can't boost as their engines are damaged
                    if (helper.FullBoost || helper.LightBoost)
                        helper.DriveControl = 1;
                    break;
                default:
                    break;
            }
            return true;
        }
        public bool DriveDirector(ref EControlCoreSet core)
        {
            bool Precise = false;
            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.PathPointSet = MultiTechUtils.HandleMultiTech(pilot.Helper, tank, ref core);
                return true;
            }
            else if (Helper.DriveDestDirected == EDriveDest.ToBase)
            {
                if (Helper.lastBasePos.IsNotNull())
                {
                    core.DriveDir = EDriveFacing.Forwards;
                    Helper.AutoSpacing = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
                    pilot.PathPointSet = Helper.theBase.boundsCentreWorldNoCheck + (Vector3.up * Helper.lastBaseExtremes);
                    Precise = true;
                }
            }
            else if (Helper.DriveDestDirected == EDriveDest.ToMine)
            {
                if (Helper.theResource.tank != null)
                {
                    if (Helper.ThrottleState == AIThrottleState.PivotOnly)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pilot.PathPointSet = Helper.theResource.tank.boundsCentreWorldNoCheck;
                        Helper.AutoSpacing = 0;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.tank.boundsCentreWorldNoCheck;
                            Precise = true;
                            Helper.AutoSpacing = 0;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.tank.boundsCentreWorldNoCheck;
                            Precise = true;
                            Helper.AutoSpacing = Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    if (Helper.ThrottleState == AIThrottleState.PivotOnly)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pilot.PathPointSet = Helper.theResource.trans.position;
                        Precise = true;
                        Helper.AutoSpacing = 0;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.trans.position;
                            Helper.AutoSpacing = 0;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.centrePosition;
                            Precise = true;
                            Helper.AutoSpacing = Helper.lastTechExtents + 2;
                        }
                    }
                }
            }
            else if (Helper.DediAI == AIType.Aegis)
            {
                Helper.theResource = AIEPathing.ClosestUnanchoredAllyAegis(TankAIManager.GetTeamTanks(pilot.Tank.Team),
                    pilot.Tank.boundsCentreWorldNoCheck, Helper.MaxCombatRange * Helper.MaxCombatRange, out _, pilot.Helper).visible;
                TryAdjustForCombat(true, ref pilot.PathPointSet, ref core);
                if (Helper.lastCombatRange > Helper.MaxCombatRange)
                {
                    if (Helper.theResource.IsNotNull())
                    {
                        Helper.theResource.tank.GetHelperInsured().MultiTechsAffiliated.Add(Helper.tank);
                        if (Helper.DriveDestDirected == EDriveDest.FromLastDestination)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.transform.position;
                        }
                        else if (Helper.DriveDestDirected == EDriveDest.ToLastDestination)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.AvoidAssist(Helper.theResource.tank.transform.position);
                        }
                        else
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AI IDLE");
                        }
                    }
                }
            }
            else
            {
                if (!TryAdjustForCombat(false, ref pilot.PathPointSet, ref core))
                {
                    if (Helper.DriveDestDirected == EDriveDest.ToLastDestination)
                    {   // Fly to target
                        pilot.PathPointSet = Helper.lastDestinationOp;
                    }
                    else if (Helper.DriveDestDirected == EDriveDest.FromLastDestination)
                    {   // Fly away from target
                        //pilot.pilot.ProcessedDest = AIEPathing.OffsetFromGroundA(Helper.lastDestination, pilot.Helper, 44);
                        pilot.PathPointSet = Helper.lastDestinationOp;
                    }
                    else
                    {
                        Helper.lastPlayer = Helper.GetPlayerTech();
                        if (Helper.lastPlayer.IsNotNull())
                        {
                            pilot.PathPointSet.y = Helper.lastPlayer.tank.boundsCentreWorldNoCheck.y;
                        }
                        else
                        {   //stay
                            pilot.PathPointSet = Helper.lastDestinationOp;
                        }
                    }
                }
            }

            pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, pilot.Helper, groundOffset);
            if (Precise)
                pilot.PathPointSet = Helper.AvoidAssistPrecise(pilot.PathPointSet);
            else
                pilot.PathPointSet = Helper. AvoidAssistPrediction(pilot.PathPointSet, pilot.AerofoilSluggishness);
            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, pilot.Helper);

            WatchStability();

            core.lastDestination = pilot.PathPointSet;
            return true;
        }

        public bool DriveDirectorRTS(ref EControlCoreSet core)
        {
            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.PathPointSet = MultiTechUtils.HandleMultiTech(pilot.Helper, tank, ref core);
                return true;
            }
            if (!Helper.IsGoingToPositionalRTSDest)
            {
                if (!TryAdjustForCombat(false, ref pilot.PathPointSet, ref core)) // When set to chase then chase
                {
                    core.DriveDest = EDriveDest.ToLastDestination;
                    core.DriveDir = EDriveFacing.Forwards;
                    pilot.PathPointSet = Helper.RTSDestination;
                    Helper.AutoSpacing = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
                    /*// Our target is too far.  We will just fly there without any correction
                    if (Helper.lastEnemyGet?.tank != null)
                    {
                        Helper.UpdateEnemyDistance(Helper.lastEnemyGet.tank.boundsCentreWorld);
                        core.DriveDest = EDriveDest.ToLastDestination;
                        core.DriveDir = EDriveFacing.Forwards;
                        pilot.PathPointSet = Helper.lastEnemyGet.tank.boundsCentreWorld;
                        Helper.AutoSpacing = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
                    }*/
                }
            }
            else
            {
                core.DriveDest = EDriveDest.ToLastDestination;
                core.DriveDir = EDriveFacing.Forwards;
                pilot.PathPointSet = Helper.RTSDestination;
                Helper.AutoSpacing = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
            }

            pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, pilot.Helper, groundOffset);
            pilot.PathPointSet = Helper.AvoidAssistPrediction(pilot.PathPointSet, pilot.AerofoilSluggishness);
            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, pilot.Helper);

            WatchStability();

            core.lastDestination = pilot.PathPointSet;
            return true;
        }

        public bool DriveDirectorEnemyRTS(EnemyMind mind, ref EControlCoreSet core)
        {
            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.PathPointSet = MultiTechUtils.HandleMultiTech(pilot.Helper, tank, ref core);
                return true;
            }
            if (!Helper.IsGoingToPositionalRTSDest)
            {
                if (!TryAdjustForCombatEnemy(mind, ref pilot.PathPointSet, ref core)) // When set to chase then chase
                {
                    core.DriveDest = EDriveDest.ToLastDestination;
                    core.DriveDir = EDriveFacing.Forwards;
                    pilot.PathPointSet = Helper.RTSDestination;
                    Helper.AutoSpacing = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
                    /*// Our target is too far.  We will just fly there without any correction
                    if (Helper.lastEnemyGet?.tank != null)
                    {
                        Helper.UpdateEnemyDistance(Helper.lastEnemyGet.tank.boundsCentreWorld);
                        core.DriveDest = EDriveDest.ToLastDestination;
                        core.DriveDir = EDriveFacing.Forwards;
                        pilot.PathPointSet = Helper.lastEnemyGet.tank.boundsCentreWorld;
                        Helper.AutoSpacing = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
                    }*/
                }
            }
            else
            {
                core.DriveDest = EDriveDest.ToLastDestination;
                core.DriveDir = EDriveFacing.Forwards;
                pilot.PathPointSet = Helper.RTSDestination;
                Helper.AutoSpacing = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
            }

            pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, pilot.Helper, groundOffset);
            pilot.PathPointSet = Helper.AvoidAssistPrediction(pilot.PathPointSet, pilot.AerofoilSluggishness);
            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, pilot.Helper);

            WatchStability();

            core.lastDestination = pilot.PathPointSet;
            return true;
        }
        public bool DriveDirectorEnemy(EnemyMind mind, ref EControlCoreSet core)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGroundTech(pilot.Helper, Helper.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            if (!TryAdjustForCombatEnemy(mind, ref pilot.PathPointSet, ref core))
            {
                if (Helper.DriveDestDirected == EDriveDest.ToLastDestination)
                {   // Fly to target
                    pilot.PathPointSet = Helper.lastDestinationOp;
                }
                else if (Helper.DriveDestDirected == EDriveDest.FromLastDestination)
                {   // Fly away from target
                    pilot.PathPointSet = Helper.lastDestinationOp;
                    //pilot.AirborneDest = ((pilot.tank.trans.position - Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + pilot.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    Helper.lastPlayer = Helper.GetPlayerTech();
                    if (Helper.lastPlayer.IsNotNull())
                    {
                        pilot.PathPointSet.y = Helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (Helper.GroundOffsetHeight / 5);
                    }
                    else
                    {   //Fly off the screen
                        //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + pilot.Tank.name + "  Leaving scene!");
                        Vector3 fFlat = pilot.Tank.rootBlockTrans.forward;
                        fFlat.y = 0;
                        pilot.PathPointSet = (fFlat.normalized * 1000) + pilot.Tank.boundsCentreWorldNoCheck;
                    }
                }
            }

            pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, pilot.Helper, groundOffset);
            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, pilot.Helper);
            pilot.PathPointSet = Helper.AvoidAssistPrediction(pilot.PathPointSet, pilot.AerofoilSluggishness);

            WatchStability();

            core.lastDestination = pilot.PathPointSet;
            return true;
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            TankAIHelper helper = pilot.Helper;
            Tank tank = pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                if (helper.SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, predictionOffset, out Tank lastCloseAlly2, 
                        out lastAllyDist, out float lastAuxVal, helper);
                    float predictOffset = (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude;
                    if (lastCloseAlly && lastAllyDist < helper.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + predictOffset)
                    {
                        if (lastCloseAlly2 && lastAuxVal < helper.lastTechExtents + lastCloseAlly2.GetCheapBounds() + 12 + predictOffset)
                        {
                            IntVector3 ProccessedVal2 = helper.GetOtherDir(lastCloseAlly) + helper.GetOtherDir(lastCloseAlly2);
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = helper.GetOtherDir(lastCloseAlly);
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, predictionOffset, out lastAllyDist, pilot.Helper);
                if (lastCloseAlly == null)
                    DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                if (lastAllyDist < helper.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                {
                    IntVector3 ProccessedVal = helper.GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AvoidAssistAir IS NaN!!");
                //TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }


        public bool TryAdjustForCombat(bool between, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper helper = pilot.Helper;
            bool output = false;
            if (helper.ChaseThreat && !helper.Retreat && helper.lastEnemyGet.IsNotNull())
            {
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                Vector3 targPos = helper.InterceptTargetDriving(helper.lastEnemyGet);
                if (between && helper.theResource?.tank)
                {
                    targPos = Between(targPos, helper.theResource.tank.boundsCentreWorldNoCheck);
                }
                helper.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((helper.lastCombatRange - helper.MinCombatRange) / 3f, -1, 1);
                if (helper.SideToThreat)
                {
                    if (helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = helper.lastEnemyGet.transform.position;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(targPos);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                    else
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = targPos;
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                }
                else
                {
                    if (helper.FullMelee)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = helper.lastEnemyGet.transform.position;
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, helper.AvoidAssist(helper.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, helper.lastEnemy, Mathf.Max(helper.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = helper.AvoidAssist(helper.lastEnemyGet.transform.position);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, helper.AvoidAssist(helper.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, helper.lastEnemy, helper.lastTechExtents + AIEnhancedCore.Extremes(helper.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(helper.lastEnemyGet.transform.position);
                        helper.AutoSpacing = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, helper.AvoidAssist(helper.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, helper.lastEnemy, helper.lastTechExtents + AIEnhancedCore.Extremes(helper.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        pos = helper.lastEnemyGet.transform.position;
                        helper.AutoSpacing = 0;
                        //thisControl.m_Movement.FacePosition(tank, helper.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                }
            }
            else
                helper.IgnoreEnemyDistance();
            return output;
        }
        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper helper = pilot.Helper;
            bool output = false;
            if (!helper.Retreat && helper.lastEnemyGet.IsNotNull() && mind.CommanderMind != EnemyAttitude.OnRails)
            {
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                helper.UpdateEnemyDistance(helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                float driveDyna = Mathf.Clamp((helper.lastCombatRange - helper.MinCombatRange) / 3f, -1, 1);
                if (mind.CommanderAttack == EAttackMode.Circle)
                {
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind);
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 2;
                    }
                    else
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind);
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 2;
                    }
                }
                else
                {
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind);
                        helper.AutoSpacing = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        helper.AutoSpacing = helper.lastTechExtents + helper.lastEnemyGet.GetCheapBounds() + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = helper.AvoidAssist(RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind));
                        helper.AutoSpacing = 0.5f;
                    }
                    else
                    {
                        pos = RCore.GetTargetCoordinates(helper, helper.lastEnemyGet, mind);
                        helper.AutoSpacing = 0;
                    }
                }
            }
            else
                helper.IgnoreEnemyDistance();
            return output;
        }


        public Vector3 Between(Vector3 Target, Vector3 other)
        {
            return (Target + other) / 2;
        }
    }
}
