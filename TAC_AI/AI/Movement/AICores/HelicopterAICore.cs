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
    public class HelicopterAICore : IMovementAICore
    {
        private AIControllerAir pilot;
        private Tank tank;
        private float groundOffset => AIGlobals.ChopperGroundOffset + pilot.Helper.lastTechExtents;

        public void Initiate(Tank tank, IMovementAIController pilot)
        {
            this.tank = tank;
            this.pilot = (AIControllerAir) pilot;
            this.pilot.FlyStyle = AIControllerAir.FlightType.Helicopter;
            this.pilot.FlyingChillFactor = Vector3.one * 30;
            pilot.Helper.GroundOffsetHeight = pilot.Helper.lastTechExtents + AIGlobals.ChopperGroundOffset;
        }
        public bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                DebugTAC_AI.Log("TACtical_AI: " + tank.name + " is GROUNDED!!!");
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, thisInst.lastTechExtents * 2))
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
                HelicopterUtils.UpdateThrottleCopter(pilot, thisInst, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest, thisInst.lastDestination, true);
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                //Debug.Log("TACtical_AI: " + tank.name + " is taking off");
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, AIEPathing.OffsetFromGroundA(tank.boundsCentreWorldNoCheck, thisInst, 45), true);
                HelicopterUtils.UpdateThrottleCopter(pilot, thisInst, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest, thisInst.lastDestination, true);
            }
            else
            {   // Normal flight
                pilot.MainThrottle = HelicopterUtils.ModerateUpwardsThrust(tank, thisInst, pilot, pilot.AirborneDest);
                HelicopterUtils.UpdateThrottleCopter(pilot, thisInst, thisControl);
                HelicopterUtils.AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest, thisInst.lastDestination);
                /*
                if (thisInst.lastEnemy.IsNotNull())
                {
                    Debug.Log("TACtical_AI: " + tank.name + " is in combat at " + pilot.AirborneDest + " tank at " + thisInst.lastEnemy.tank.boundsCentreWorldNoCheck);
                }
                */
            }

            return true;
        }

        public bool DriveDirector()
        {
            pilot.ForcePitchUp = false;
            bool Combat;
            bool Precise = false;
            if (pilot.Helper.IsMultiTech)
            {   //Override and disable most driving abilities
                pilot.AirborneDest = MultiTechUtils.HandleMultiTech(pilot.Helper, tank);
                return true;
            }
            else if (pilot.Helper.DriveDest == EDriveDest.ToBase)
            {
                if (pilot.Helper.lastBasePos.IsNotNull())
                {
                    pilot.Helper.Steer = true;
                    pilot.Helper.DriveDir = EDriveFacing.Forwards;
                    pilot.Helper.MinimumRad = Mathf.Max(pilot.Helper.lastTechExtents - 2, 0.5f);
                    pilot.Helper.lastDestination = pilot.Helper.theBase.boundsCentreWorldNoCheck + (Vector3.up * pilot.Helper.lastBaseExtremes);
                    Precise = true;
                }
                pilot.AirborneDest = pilot.Helper.lastDestination;
            }
            else if (pilot.Helper.DriveDest == EDriveDest.ToMine)
            {
                if (pilot.Helper.theResource.tank != null)
                {
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.tank.boundsCentreWorldNoCheck;
                        pilot.Helper.MinimumRad = 0;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.theResource.tank.boundsCentreWorldNoCheck;
                            Precise = true;
                            pilot.Helper.MinimumRad = 0;
                        }
                        else
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.theResource.tank.boundsCentreWorldNoCheck;
                            Precise = true;
                            pilot.Helper.MinimumRad = pilot.Helper.lastTechExtents + 2;
                        }
                    }
                }
                else
                {
                    if (pilot.Helper.PivotOnly)
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.trans.position;
                        Precise = true;
                        pilot.Helper.MinimumRad = 0;
                    }
                    else
                    {
                        if (pilot.Helper.FullMelee)
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.theResource.trans.position;
                            pilot.Helper.MinimumRad = 0;
                        }
                        else
                        {
                            pilot.Helper.Steer = true;
                            pilot.Helper.DriveDir = EDriveFacing.Forwards;
                            pilot.Helper.lastDestination = pilot.Helper.theResource.centrePosition;
                            Precise = true;
                            pilot.Helper.MinimumRad = pilot.Helper.lastTechExtents + 2;
                        }
                    }
                }
                pilot.AirborneDest = pilot.Helper.lastDestination;
            }
            else if (pilot.Helper.DediAI == AIType.Aegis)
            {
                pilot.Helper.theResource = AIEPathing.ClosestUnanchoredAlly(tank.boundsCentreWorldNoCheck, out float bestval, tank).visible;
                Combat = TryAdjustForCombat(true);
                if (!Combat)
                {
                    if (pilot.Helper.DriveDest == EDriveDest.FromLastDestination && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.theResource.transform.position;
                    }
                    else if (pilot.Helper.DriveDest == EDriveDest.ToLastDestination && pilot.Helper.theResource.IsNotNull())
                    {
                        pilot.Helper.Steer = true;
                        pilot.Helper.DriveDir = EDriveFacing.Forwards;
                        pilot.Helper.lastDestination = pilot.Helper.AvoidAssist(pilot.Helper.theResource.tank.transform.position);
                    }
                    else
                    {
                        //Debug.Log("TACtical_AI: AI IDLE");
                    }
                }
                pilot.AirborneDest = pilot.Helper.lastDestination;
            }
            else
            {
                Combat = TryAdjustForCombat(false);
                if (Combat)
                {
                    pilot.AirborneDest = pilot.Helper.lastDestination;
                }
                else
                {
                    if (pilot.Helper.DriveDest == EDriveDest.ToLastDestination)
                    {   // Fly to target
                        pilot.AirborneDest = pilot.Helper.lastDestination;
                    }
                    else if (pilot.Helper.DriveDest == EDriveDest.FromLastDestination)
                    {   // Fly away from target
                        //pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(pilot.Helper.lastDestination, pilot.Helper, 44);
                        pilot.AirborneDest = pilot.Helper.lastDestination;
                    }
                    else
                    {
                        pilot.Helper.lastPlayer = pilot.Helper.GetPlayerTech();
                        if (pilot.Helper.lastPlayer.IsNotNull())
                        {
                            pilot.AirborneDest.y = pilot.Helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (pilot.Helper.GroundOffsetHeight / 5);
                        }
                        else
                        {   //stay
                            pilot.AirborneDest = pilot.Helper.lastDestination;
                        }
                    }
                }
            }

            pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, pilot.Helper, groundOffset);
            if (Precise)
                pilot.AirborneDest = pilot.Helper.AvoidAssistPrecise(pilot.AirborneDest);
            else
                pilot.AirborneDest = AvoidAssist(pilot.AirborneDest, pilot.Tank.boundsCentreWorldNoCheck + (pilot.deltaMovementClock * pilot.AerofoilSluggishness));
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);

            if (!AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock, groundOffset))
            {
                Debug.Log("TACtical_AI: Tech " + pilot.Tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            return true;
        }

        public bool DriveDirectorRTS()
        {
            pilot.ForcePitchUp = false;
            bool combat = false;
            if (pilot.Helper.RTSDestination == Vector3.zero)
                combat = TryAdjustForCombat(false); // When set to chase then chase
            if (combat)
            {
                pilot.AirborneDest = pilot.Helper.lastDestination;
            }
            else
            {
                pilot.Helper.DriveDest = EDriveDest.ToLastDestination;
                pilot.Helper.Steer = true;
                pilot.Helper.DriveDir = EDriveFacing.Forwards;
                pilot.AirborneDest = pilot.Helper.lastDestination;
                pilot.Helper.MinimumRad = Mathf.Max(pilot.Helper.lastTechExtents - 2, 0.5f);
            }

            pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, pilot.Helper, groundOffset);
            pilot.AirborneDest = AvoidAssist(pilot.AirborneDest, pilot.Tank.boundsCentreWorldNoCheck + (pilot.deltaMovementClock * pilot.AerofoilSluggishness));
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);

            if (!AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock, groundOffset))
            {
                Debug.Log("TACtical_AI: Tech " + pilot.Tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind)
        {
            pilot.ForcePitchUp = false;
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck, pilot.Helper.lastTechExtents * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            bool combat = TryAdjustForCombatEnemy(mind);
            if (combat)
            {
                pilot.AirborneDest = pilot.Helper.lastDestination;
            }
            else
            {
                if (pilot.Helper.DriveDest == EDriveDest.ToLastDestination)
                {   // Fly to target
                    pilot.AirborneDest = pilot.Helper.lastDestination;
                }
                else if (pilot.Helper.DriveDest == EDriveDest.FromLastDestination)
                {   // Fly away from target
                    pilot.AirborneDest = pilot.Helper.lastDestination;
                    //pilot.AirborneDest = ((pilot.tank.trans.position - pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + pilot.tank.boundsCentreWorldNoCheck;
                }
                else
                {
                    pilot.Helper.lastPlayer = pilot.Helper.GetPlayerTech();
                    if (pilot.Helper.lastPlayer.IsNotNull())
                    {
                        pilot.AirborneDest.y = pilot.Helper.lastPlayer.tank.boundsCentreWorldNoCheck.y + (pilot.Helper.GroundOffsetHeight / 5);
                    }
                    else
                    {   //Fly off the screen
                        //Debug.Log("TACtical_AI: Tech " + pilot.Tank.name + "  Leaving scene!");
                        Vector3 fFlat = pilot.Tank.rootBlockTrans.forward;
                        fFlat.y = 0;
                        pilot.AirborneDest = (fFlat.normalized * 1000) + pilot.Tank.boundsCentreWorldNoCheck;
                    }
                }
            }

            pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, pilot.Helper, groundOffset);
            pilot.AirborneDest = AIEPathing.ModerateMaxAlt(pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = RPathfinding.AvoidAssistEnemy(pilot.Tank, pilot.AirborneDest, pilot.Tank.boundsCentreWorldNoCheck + (pilot.deltaMovementClock * pilot.AerofoilSluggishness), pilot.Helper, mind);

            if (!AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck + pilot.deltaMovementClock, groundOffset))
            {
                Debug.Log("TACtical_AI: Tech " + pilot.Tank.name + "  Avoiding Ground!");
                pilot.ForcePitchUp = true;
            }
            return true;
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            AIECore.TankAIHelper thisInst = pilot.Helper;
            Tank tank = pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                if (thisInst.SecondAvoidence)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
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
                lastCloseAlly = AIEPathing.ClosestAllyPrecision(predictionOffset, out lastAllyDist, tank);
                if (lastCloseAlly == null)
                    DebugTAC_AI.Log("TACtical_AI: ALLY IS NULL");
                if (lastAllyDist < thisInst.lastTechExtents + lastCloseAlly.GetCheapBounds() + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                {
                    IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("TACtical_AI: Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                DebugTAC_AI.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
                //AIECore.TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }

        public bool TryAdjustForCombat(bool between)
        {
            AIECore.TankAIHelper thisInst = pilot.Helper;
            bool output = false;
            if (thisInst.PursueThreat && !thisInst.Retreat && thisInst.lastEnemy.IsNotNull())
            {
                output = true;
                thisInst.Steer = true;
                Vector3 targPos = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                if (between && thisInst.theResource?.tank)
                {
                    targPos = Between(targPos, thisInst.theResource.tank.boundsCentreWorldNoCheck);
                }
                thisInst.lastRangeEnemy = (targPos - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((thisInst.lastRangeEnemy - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.lastDestination = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(targPos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.lastDestination = targPos;
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + AIGlobals.SpacingRangeChopper;
                    }
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        thisInst.DriveDir = EDriveFacing.Forwards;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                                                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveFacing.Forwards;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveFacing.Forwards;
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
            else
                thisInst.lastRangeEnemy = float.MaxValue;
            return output;
        }

        public bool TryAdjustForCombatEnemy(EnemyMind mind)
        {
            AIECore.TankAIHelper thisInst = pilot.Helper;
            bool output = false;
            if (!thisInst.Retreat && thisInst.lastEnemy.IsNotNull() && mind.CommanderMind != Enemy.EnemyAttitude.OnRails)
            {
                output = true;
                thisInst.Steer = true;
                thisInst.lastRangeEnemy = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                float driveDyna = Mathf.Clamp((thisInst.lastRangeEnemy - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (mind.CommanderAttack == Enemy.EnemyAttack.Circle)
                {
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveFacing.Perpendicular;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 2;
                    }
                }
                else
                {
                    if (mind.CommanderMind == Enemy.EnemyAttitude.Miner)
                    {
                        thisInst.DriveDir = EDriveFacing.Forwards;
                        thisInst.lastDestination = Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind);
                        thisInst.MinimumRad = 0;//WHAAAAAAAAAAAM
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveFacing.Forwards;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, Enemy.RCore.GetTargetCoordinates(tank, thisInst.lastEnemy, mind), thisInst, mind);
                        thisInst.MinimumRad = thisInst.lastTechExtents + thisInst.lastEnemy.GetCheapBounds() + 5;
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveFacing.Forwards;
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
            else
                thisInst.lastRangeEnemy = float.MaxValue;
            return output;
        }
        public Vector3 Between(Vector3 Target, Vector3 other)
        {
            return (Target + other) / 2;
        }
    }
}
