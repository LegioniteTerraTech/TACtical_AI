using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;

namespace TAC_AI.AI.Movement.AICores
{
    internal class HelicopterAICore : IMovementAICore
    {
        private AIControllerAir pilot;
        internal TankAIHelper Helper => pilot.Helper;
        private Tank tank;
        private float groundOffset => Helper.GroundOffsetHeight;
        private float groundOffsetEmerg => AIGlobals.GroundOffsetCrashWarnChopper + Helper.lastTechExtents;

        public void Initiate(Tank tank, IMovementAIController pilot)
        {
            this.tank = tank;
            this.pilot = (AIControllerAir) pilot;
            this.pilot.FlyStyle = AIControllerAir.FlightType.Helicopter;
            this.pilot.FlyingChillFactor = Vector3.one * 30;
            Helper.GroundOffsetHeight = Helper.lastTechExtents + AIGlobals.GroundOffsetChopper;
        }
        public bool DriveMaintainer(TankControl thisControl, TankAIHelper thisInst, Tank tank, ref EControlCoreSet core)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is GROUNDED!!!");
                if (!AIEPathing.AboveHeightFromGroundTech(thisInst, thisInst.lastTechExtents * 2))
                {
                    return false;
                }
                //WIP - Try fighting the controls to land safely

                return true;
            }

            if (tank.beam.IsActive)
            {   // BEAMING
                pilot.MainThrottle = 0;
                pilot.AdvisedThrottle = 0;
                pilot.CurrentThrottle = 0;
                HelicopterUtils.UpdateThrottleCopter(thisControl, pilot);
                HelicopterUtils.AngleTowardsUp(thisControl, pilot, pilot.PathPointSet, thisInst.lastDestinationCore, ref core, true);
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is taking off");
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, AIEPathing.OffsetFromGroundA(tank.boundsCentreWorldNoCheck, thisInst, groundOffset + 5), true);
                HelicopterUtils.UpdateThrottleCopter(thisControl, pilot);
                HelicopterUtils.AngleTowardsUp(thisControl, pilot, pilot.PathPointSet, thisInst.lastDestinationCore, ref core, true);
            }
            else
            {   // Normal flight
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, pilot.PathPointSet);
                HelicopterUtils.UpdateThrottleCopter(thisControl, pilot);
                HelicopterUtils.AngleTowardsUp(thisControl, pilot, pilot.PathPointSet, thisInst.lastDestinationCore, ref core);
                /*
                if (thisInst.lastIsNotNull())
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is in combat at " + pilot.AirborneDest + " tank at " + thisInst.lastEnemy.tank.boundsCentreWorldNoCheck);
                }
                */
            }

            return true;
        }

        public bool DriveDirector(ref EControlCoreSet core)
        {
            pilot.ForcePitchUp = false;
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
                    Helper.MinimumRad = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
                    pilot.PathPointSet = Helper.theBase.boundsCentreWorldNoCheck + (Vector3.up * Helper.lastBaseExtremes);
                    Precise = true;
                }
            }
            else if (Helper.DriveDestDirected == EDriveDest.ToMine)
            {
                if (Helper.theResource.tank != null)
                {
                    if (Helper.PivotOnly)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pilot.PathPointSet = Helper.theResource.tank.boundsCentreWorldNoCheck;
                        Helper.MinimumRad = 0;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.tank.boundsCentreWorldNoCheck;
                            Precise = true;
                            Helper.MinimumRad = 0;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.tank.boundsCentreWorldNoCheck;
                            Precise = true;
                            Helper.MinimumRad = Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    if (Helper.PivotOnly)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pilot.PathPointSet = Helper.theResource.trans.position;
                        Precise = true;
                        Helper.MinimumRad = 0;
                    }
                    else
                    {
                        if (Helper.FullMelee)
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.trans.position;
                            Helper.MinimumRad = 0;
                        }
                        else
                        {
                            core.DriveDir = EDriveFacing.Forwards;
                            pilot.PathPointSet = Helper.theResource.centrePosition;
                            Precise = true;
                            Helper.MinimumRad = Helper.lastTechExtents + 2;
                        }
                    }
                }
            }
            else if (Helper.DediAI == AIType.Aegis)
            {
                Helper.theResource = AIEPathing.ClosestUnanchoredAlly(AIEPathing.AllyList(pilot.Tank),
                    pilot.Tank.boundsCentreWorldNoCheck, Helper.MaxCombatRange * Helper.MaxCombatRange, out _, pilot.Tank).visible;
                TryAdjustForCombat(true, ref pilot.PathPointSet, ref core);
                if (Helper.lastCombatRange > Helper.MaxCombatRange)
                {
                    if (Helper.theResource.IsNotNull())
                    {
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
                            pilot.PathPointSet.y = Helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (Helper.GroundOffsetHeight / 5);
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

            if (!AIEPathing.AboveHeightFromGround(Helper.DodgeSphereCenter, groundOffsetEmerg))
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + pilot.Tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            core.lastDestination = pilot.PathPointSet;
            return true;
        }

        public bool DriveDirectorRTS(ref EControlCoreSet core)
        {
            pilot.ForcePitchUp = false;
            if (Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.PathPointSet = MultiTechUtils.HandleMultiTech(pilot.Helper, tank, ref core);
                return true;
            }
            if (Helper.RTSDestination == TankAIHelper.RTSDisabled)
            {
                if (!TryAdjustForCombat(false, ref pilot.PathPointSet, ref core)) // When set to chase then chase
                {
                    core.DriveDest = EDriveDest.ToLastDestination;
                    core.DriveDir = EDriveFacing.Forwards;
                    pilot.PathPointSet = Helper.RTSDestination;
                    Helper.MinimumRad = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
                }
            }
            else
            {
                core.DriveDest = EDriveDest.ToLastDestination;
                core.DriveDir = EDriveFacing.Forwards;
                pilot.PathPointSet = Helper.RTSDestination;
                Helper.MinimumRad = Mathf.Max(Helper.lastTechExtents - 2, 0.5f);
            }

            pilot.PathPointSet = AIEPathing.OffsetFromGroundA(pilot.PathPointSet, pilot.Helper, groundOffset);
            pilot.PathPointSet = Helper.AvoidAssistPrediction(pilot.PathPointSet, pilot.AerofoilSluggishness);
            pilot.PathPointSet = AIEPathing.ModerateMaxAlt(pilot.PathPointSet, pilot.Helper);

            if (!AIEPathing.AboveHeightFromGround(Helper.DodgeSphereCenter, groundOffsetEmerg))
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + pilot.Tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            core.lastDestination = pilot.PathPointSet;
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind, ref EControlCoreSet core)
        {
            pilot.ForcePitchUp = false;
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

            if (!AIEPathing.AboveHeightFromGround(Helper.DodgeSphereCenter, groundOffsetEmerg))
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + pilot.Tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            core.lastDestination = pilot.PathPointSet;
            return true;
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            TankAIHelper thisInst = pilot.Helper;
            Tank tank = pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                HashSet<Tank> AlliesAlt = AIEPathing.AllyList(tank);
                if (thisInst.SecondAvoidence && AlliesAlt.Count > 1)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(AlliesAlt, predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    float predictOffset = (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude;
                    if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + predictOffset)
                    {
                        if (lastAuxVal < thisInst.lastTechExtents + lastCloseAlly2.GetCheapBounds() + 12 + predictOffset)
                        {
                            IntVector3 ProccessedVal2 = thisInst.GetOtherDir(lastCloseAlly) + thisInst.GetOtherDir(lastCloseAlly2);
                            return (targetIn + ProccessedVal2) / 3;
                        }
                        IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                        return (targetIn + ProccessedVal) / 2;
                    }

                }
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(AlliesAlt, predictionOffset, out lastAllyDist, tank);
                if (lastCloseAlly == null)
                    DebugTAC_AI.Log(KickStart.ModID + ": ALLY IS NULL");
                if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                {
                    IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
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
            TankAIHelper thisInst = pilot.Helper;
            bool output = false;
            if (thisInst.ChaseThreat && !thisInst.Retreat && thisInst.lastEnemyGet.IsNotNull())
            {
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                Vector3 targPos = thisInst.InterceptTargetDriving(thisInst.lastEnemyGet);
                if (between && thisInst.theResource?.tank)
                {
                    targPos = Between(targPos, thisInst.theResource.tank.boundsCentreWorldNoCheck);
                }
                thisInst.UpdateEnemyDistance(targPos);
                float driveDyna = Mathf.Clamp((thisInst.lastCombatRange - thisInst.MinCombatRange) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = thisInst.lastEnemyGet.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                    else
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = targPos;
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = thisInst.lastEnemyGet.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = thisInst.AvoidAssist(thisInst.lastEnemyGet.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemyGet.GetCheapBounds() + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = thisInst.AvoidAssist(thisInst.lastEnemyGet.transform.position);
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        pos = thisInst.lastEnemyGet.transform.position;
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                }
            }
            else
                thisInst.IgnoreEnemyDistance();
            return output;
        }
        public bool TryAdjustForCombatEnemy(EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core)
        {
            TankAIHelper Helper = pilot.Helper;
            bool output = false;
            if (!Helper.Retreat && Helper.lastEnemyGet.IsNotNull() && mind.CommanderMind != EnemyAttitude.OnRails)
            {
                output = true;
                core.DriveDir = EDriveFacing.Forwards;
                Helper.UpdateEnemyDistance(Helper.lastEnemyGet.tank.boundsCentreWorldNoCheck);
                float driveDyna = Mathf.Clamp((Helper.lastCombatRange - Helper.MinCombatRange) / 3f, -1, 1);
                if (mind.CommanderAttack == EAttackMode.Circle)
                {
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind);
                        Helper.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = Helper.AvoidAssist(RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind));
                        Helper.MinimumRad = Helper.lastTechExtents + Helper.lastEnemyGet.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = Helper.AvoidAssist(RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind));
                        Helper.MinimumRad = Helper.lastTechExtents + Helper.lastEnemyGet.GetCheapBounds() + 2;
                    }
                    else
                    {
                        core.DriveDir = EDriveFacing.Perpendicular;
                        pos = RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind);
                        Helper.MinimumRad = Helper.lastTechExtents + Helper.lastEnemyGet.GetCheapBounds() + 2;
                    }
                }
                else
                {
                    if (mind.CommanderMind == EnemyAttitude.Miner)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind);
                        Helper.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        pos = Helper.AvoidAssist(RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind));
                        Helper.MinimumRad = Helper.lastTechExtents + Helper.lastEnemyGet.GetCheapBounds() + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        core.DriveDir = EDriveFacing.Forwards;
                        core.DriveDest = EDriveDest.FromLastDestination;
                        pos = Helper.AvoidAssistInv(RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind));
                        Helper.MinimumRad = 0.5f;
                    }
                    else
                    {
                        pos = RCore.GetTargetCoordinates(Helper, Helper.lastEnemyGet, mind);
                        Helper.MinimumRad = 0;
                    }
                }
            }
            else
                Helper.IgnoreEnemyDistance();
            return output;
        }


        public Vector3 Between(Vector3 Target, Vector3 other)
        {
            return (Target + other) / 2;
        }
    }
}
