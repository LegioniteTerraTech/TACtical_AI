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
    public class AirplaneAICore : IMovementAICore
    {
        internal AIControllerAir pilot;

        public virtual void Initiate(Tank tank, IMovementAIController pilot)
        {
            this.pilot = (AIControllerAir) pilot;
            this.pilot.FlyStyle = AIControllerAir.FlightType.Aircraft;
        }
        public virtual bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (pilot.Grounded || thisInst.forceDrive)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                {
                    return false;
                }
                //WIP - Try fighting the controls to land safely

                return true;
            }
            //Debug.Log("TACtical_AI: Tech " + tank.name + " plane drive was called");

            if (tank.beam.IsActive)
            {   // BEAMING
                pilot.MainThrottle = 0;
                pilot.PerformUTurn = 0;
                this.pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat.Normalize();
                flat.y = 0.5f;
                AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 100));
            }
            else if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                pilot.MainThrottle = 1;
                pilot.PerformUTurn = 0;
                this.pilot.UpdateThrottle(thisInst, thisControl);
                Vector3 flat = tank.rootBlockTrans.forward;
                flat.y = 0;
                flat.Normalize();
                flat.y = 0.5f;
                AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (flat * 100));
            }
            else
            {
                if (pilot.TargetGrounded && (bool)thisInst.lastEnemy) // Divebombing mode
                {  // We previously disabled the ground offset terrain avoider and aim directly at the enemy
                    float dist = (thisInst.lastDestination - (tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime))).magnitude;
                    Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(thisInst.lastDestination - tank.boundsCentreWorldNoCheck);
                    if (pilot.ForcePitchUp)
                        pilot.PerformDiveAttack = 0; // too low and we break off from the attack
                    if (dist < 32)
                    {   // target is in the air but grounded!?!?
                        pilot.PerformDiveAttack = 0; // abort

                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborting attack! Target too close!");
                        // AND PITCH UP NOW
                        pilot.MainThrottle = 1;
                        pilot.PerformUTurn = 0;
                        this.pilot.UpdateThrottle(thisInst, thisControl);
                        AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                    }
                    else if (pilot.PerformDiveAttack == 1)
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Aiming at target!");
                        if (Heading.x > 0.3f && Heading.x < -0.3f && Heading.z > 0)
                            pilot.PerformDiveAttack = 2; 
                        if (pilot.PerformUTurn > 0)
                        {   //The Immelmann Turn
                            AircraftUtils.UTurn(thisControl, thisInst, tank, pilot);
                            return true;
                        }
                        else if (pilot.PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                            {
                                pilot.PerformUTurn = 0;
                                if (pilot.PerformDiveAttack == 1)
                                    pilot.PerformDiveAttack = 2;
                            }
                            return true;
                        }
                        else
                        {
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (pilot.PerformDiveAttack == 2)
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  DIVEBOMBING!");
                        if (tank.GetForwardSpeed() < AIControllerAir.Stallspeed + 16 || Heading.y > -0.25f)
                            pilot.AdvisedThrottle = 1;
                        else
                            pilot.AdvisedThrottle = 0;
                        if (Heading.z < 0)
                            pilot.PerformDiveAttack = 0; // Passed by target
                        if (pilot.PerformUTurn > 0)
                        {   //The Immelmann Turn
                            AircraftUtils.UTurn(thisControl, thisInst, tank, pilot);
                            return true;
                        }
                        else if (pilot.PerformUTurn == -1)
                        {
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                            if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                            {
                                pilot.PerformUTurn = 0;
                                if (pilot.PerformDiveAttack == 1)
                                    pilot.PerformDiveAttack = 2;
                            }
                            return true;
                        }
                        else
                        {
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    else if (dist > AIControllerAir.GroundAttackStagingDist && Heading.z < 0)
                    {   // Launch teh attack run
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Turning back to face target at dist " + dist);
                        pilot.PerformDiveAttack = 1;
                    }
                    else
                    {
                        pilot.PerformUTurn = 0; // hold off on the U-Turn
                        if (Heading.z < 0.25f)
                        {   // Moving away from target
                            //Debug.Log("TACtical_AI: Tech " + tank.name + "  Gaining distance for attack run");
                            pilot.MainThrottle = 1;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            Vector3 AwayFlat = -(pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized;
                            AwayFlat.y = 0;
                            AwayFlat.Normalize();
                            AwayFlat.y = 0.25f;
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (AwayFlat.normalized * 300));
                        }
                        else
                        {   // Moving to target
                            //Debug.Log("TACtical_AI: Tech " + tank.name + "  Closing in on target");
                            if (tank.GetForwardSpeed() < AIControllerAir.Stallspeed + 16 || Heading.y > -0.25f)
                                pilot.AdvisedThrottle = 1;
                            else
                                pilot.AdvisedThrottle = 0;
                            pilot.MainThrottle = pilot.AdvisedThrottle;
                            this.pilot.UpdateThrottle(thisInst, thisControl);
                            AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, thisInst.lastDestination);
                        }
                    }
                    return true;
                }

                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    AircraftUtils.UTurn(thisControl, thisInst, tank, pilot);
                    return true;
                }
                else if (pilot.PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                    {
                        pilot.PerformUTurn = 0;
                        if (pilot.PerformDiveAttack == 1)
                            pilot.PerformDiveAttack = 2;
                    }
                    return true;
                }
                else
                {
                    pilot.MainThrottle = pilot.AdvisedThrottle;
                    this.pilot.UpdateThrottle(thisInst, thisControl);
                    AircraftUtils.AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                }
            }

            return true;
        }

        public bool DriveDirector()
        {
            pilot.AdvisedThrottle = -1;
            bool combat = this.TryAdjustForCombat();
            if (combat)
            {
                pilot.LowerEngines = true;
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else
            {
                pilot.LowerEngines = false;
                if (this.pilot.Helper.ProceedToObjective)
                {   // Fly to target
                    if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 100);
                    }
                    else
                    {
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                }
                else if (this.pilot.Helper.MoveFromObjective)
                {   // Fly away from target
                    pilot.AirborneDest = ((this.pilot.Tank.trans.position - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.Tank.boundsCentreWorldNoCheck;
                }
                else
                {   // Orbit last position
                    if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest += (-this.pilot.Tank.rootBlockTrans.right * 50);
                    }
                    else
                    {
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                }
            }

            if (!pilot.TargetGrounded)
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            AIEPathing.ModerateMaxAlt(ref pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = AvoidAssist(pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness));
            AircraftUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

            if (pilot.LargeAircraft || pilot.BankOnly)
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + ((this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime) * 5) - (Vector3.down * AIECore.Extremes(this.pilot.Tank.blockBounds.size)), pilot.AerofoilSluggishness + 25))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            else
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime), pilot.AerofoilSluggishness + 25))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            return true;
        }

        public bool DriveDirectorEnemy(EnemyMind mind)
        {
            pilot.AdvisedThrottle = -1;
            pilot.ForcePitchUp = false;
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck, AIECore.Extremes(this.pilot.Tank.blockBounds.extents) * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            this.pilot.Helper.Retreat = false;
            bool combat = this.TryAdjustForCombatEnemy(mind);
            if (combat)
            {
                pilot.LowerEngines = true;
                pilot.AirborneDest = this.pilot.Helper.lastDestination;
            }
            else if (mind.CommanderMind == EnemyAttitude.SubNeutral)
            {   // Fly straight, above ground in player visual distance
                pilot.AirborneDest = AIEPathing.ForceOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * Time.deltaTime * KickStart.AIClockPeriod) + pilot.Tank.rootBlockTrans.forward, pilot.Helper);
            }
            else
            {
                pilot.LowerEngines = false;
                if ((pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                {   //We are at target
                    //Debug.Log("TACtical_AI: Tech " + this.pilot.Tank.name + " Arrived at destination");

                    Vector3 lFlat;
                    if (this.pilot.Tank.rootBlockTrans.up.y > 0)
                        lFlat = -this.pilot.Tank.rootBlockTrans.right + (this.pilot.Tank.rootBlockTrans.forward * 2);
                    else
                        lFlat = this.pilot.Tank.rootBlockTrans.right + (this.pilot.Tank.rootBlockTrans.forward * 2);
                    lFlat.y = 0.1f;
                    pilot.AirborneDest += (lFlat * 50);
                }
                else if (this.pilot.Helper.ProceedToObjective)
                {   // Fly to target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    if ((this.pilot.Helper.lastDestination - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = this.pilot.Helper.lastDestination + (this.pilot.Tank.rootBlockTrans.forward * 100);
                    }
                    else
                    {
                        pilot.AirborneDest = this.pilot.Helper.lastDestination;
                    }
                }
                else if (this.pilot.Helper.MoveFromObjective)
                {   // Fly away from target
                    this.pilot.Helper.lastDestination = AIEPathing.OffsetFromGroundA(this.pilot.Helper.lastDestination, this.pilot.Helper);
                    pilot.AirborneDest = ((this.pilot.Tank.trans.position - this.pilot.Helper.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + this.pilot.Tank.boundsCentreWorldNoCheck;
                }
                else
                {   // Orbit above player height to invoke trouble
                    this.pilot.Helper.lastPlayer = this.pilot.Helper.GetPlayerTech();
                    if (this.pilot.Helper.lastPlayer.IsNotNull())
                    {
                        pilot.AirborneDest.y = (this.pilot.Helper.lastPlayer.tank.boundsCentreWorldNoCheck + (Vector3.up * (this.pilot.Helper.GroundOffsetHeight / 5))).y;
                    }
                    else
                    {   // Fly forwards until target is found
                        pilot.AirborneDest = AIEPathing.ForceOffsetFromGroundA(pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * Time.deltaTime * KickStart.AIClockPeriod) + pilot.Tank.rootBlockTrans.forward, pilot.Helper);

                        /* - Old
                        //Fly off the screen
                        //Vector3 fFlat = this.pilot.Tank.rootBlockTrans.forward;
                        //fFlat.y = 0.25f;
                        //pilot.AirborneDest = (fFlat.normalized * 1000) + this.pilot.Tank.boundsCentreWorldNoCheck;
                        */
                    }
                }
            }

            if (!pilot.TargetGrounded)
                pilot.AirborneDest = AIEPathing.OffsetFromGroundA(pilot.AirborneDest, this.pilot.Helper);
            AIEPathing.ModerateMaxAlt(ref pilot.AirborneDest, pilot.Helper);
            pilot.AirborneDest = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, pilot.AirborneDest, this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness), this.pilot.Helper, mind);
            AircraftUtils.AdviseThrottle(pilot, this.pilot.Helper, this.pilot.Tank, pilot.AirborneDest);

            if (pilot.LargeAircraft || pilot.BankOnly)
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + ((this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime) * 2) - (Vector3.down * AIECore.Extremes(this.pilot.Tank.blockBounds.size)), pilot.AerofoilSluggishness + 25))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            else
            {
                if (!AIEPathing.AboveHeightFromGround(this.pilot.Tank.boundsCentreWorldNoCheck + (this.pilot.Tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime), pilot.AerofoilSluggishness + 25))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            return true;
        }

        public bool TryAdjustForCombat()
        {
            bool output = false;
            if (this.pilot.Helper.PursueThreat && !this.pilot.Helper.Retreat && this.pilot.Helper.lastEnemy.IsNotNull())
            {
                output = true;
                float driveDyna = Mathf.Clamp(((this.pilot.Helper.lastEnemy.transform.position - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude - this.pilot.Helper.IdealRangeCombat) / 3f, -1, 1);
                if (this.pilot.Helper.SideToThreat)
                {
                    if (this.pilot.Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }
                else
                {
                    if (this.pilot.Helper.FullMelee)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = this.AvoidAssist(AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot));
                    }
                    else
                    {
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }

                this.pilot.Helper.lastRange = (this.pilot.Helper.lastEnemy.tank.boundsCentreWorld - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + 25);
                AircraftUtils.AdviseThrottleTarget(pilot, this.pilot.Helper, this.pilot.Tank, this.pilot.Helper.lastEnemy);
            }
            return output;
        }
        public bool TryAdjustForCombatEnemy(EnemyMind mind)
        {
            bool output = false;

            bool isCombatAttitude = mind.CommanderMind != EnemyAttitude.OnRails && mind.CommanderMind != EnemyAttitude.SubNeutral;
            if (!this.pilot.Helper.Retreat && this.pilot.Helper.lastEnemy.IsNotNull() && isCombatAttitude)
            {
                output = true;
                float driveDyna = Mathf.Clamp(((this.pilot.Helper.lastEnemy.transform.position - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude - this.pilot.Helper.IdealRangeCombat) / 3f, -1, 1);
                if (this.pilot.Helper.SideToThreat)
                {
                    if (this.pilot.Helper.FullMelee)
                    {   //orbit WHILE at enemy!
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Perpendicular;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }
                else
                {
                    if (this.pilot.Helper.FullMelee)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else if (driveDyna < 0)
                    {
                        this.pilot.Helper.DriveDir = EDriveType.Forwards;
                        this.pilot.Helper.AdviseAway = true;
                        this.pilot.Helper.lastDestination = RPathfinding.AvoidAssistEnemy(this.pilot.Tank, AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy), AircraftUtils.TryGetVelocityOffset(this.pilot.Tank, pilot), this.pilot.Helper, mind);
                    }
                    else
                    {
                        this.pilot.Helper.lastDestination = AircraftUtils.ForeAiming(this.pilot.Helper.lastEnemy);
                    }
                }
                //pilot.AdvisedThrottle = driveDyna;

                this.pilot.Helper.lastRange = (this.pilot.Helper.lastEnemy.tank.boundsCentreWorld - this.pilot.Tank.boundsCentreWorldNoCheck).magnitude;

                pilot.TargetGrounded = !AIEPathing.AboveHeightFromGround(this.pilot.Helper.lastEnemy.tank.boundsCentreWorldNoCheck, pilot.AerofoilSluggishness + 25);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                    AircraftUtils.AdviseThrottleTarget(pilot, this.pilot.Helper, this.pilot.Tank, this.pilot.Helper.lastEnemy);
                else
                    pilot.AdvisedThrottle = 1;  //if Ai not smrt enough just hold shift
            }
            return output;
        }

        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            AIECore.TankAIHelper thisInst = this.pilot.Helper;
            Tank tank = this.pilot.Tank;

            try
            {
                Tank lastCloseAlly;
                float lastAllyDist;
                if (thisInst.SecondAvoidence)// MORE processing power
                {
                    lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal, tank);
                    if (lastAllyDist < thisInst.lastTechExtents + AIECore.Extremes(lastCloseAlly.blockBounds.extents) + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                    {
                        if (lastAuxVal < thisInst.lastTechExtents + AIECore.Extremes(lastCloseAlly.blockBounds.extents) + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
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
                    Debug.Log("TACtical_AI: ALLY IS NULL");
                if (lastAllyDist < thisInst.lastTechExtents + AIECore.Extremes(lastCloseAlly.blockBounds.extents) + 12 + (predictionOffset - tank.boundsCentreWorldNoCheck).magnitude)
                {
                    IntVector3 ProccessedVal = thisInst.GetOtherDir(lastCloseAlly);
                    return (targetIn + ProccessedVal) / 2;
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Crash on AvoidAssistAir " + e);
                return targetIn;
            }
            if (targetIn.IsNaN())
            {
                Debug.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
                AIECore.TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }
    }
}
