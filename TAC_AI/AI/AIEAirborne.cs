using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;

namespace TAC_AI.AI
{
    public static class AIEAirborne
    {
        public enum FlightType
        {                   
            Aircraft,   // Horizontal flight
            Helicopter, // Vertical flight
            VTOL,       // Both Horizontal and vertical flight
        }
        public class AirAssistance : MonoBehaviour
        {   // PID Values - for airborne things that need to learn to stay in the air
            public Tank tank;
            public AIECore.TankAIHelper thisInst;

            public FlightType FlyStyle;
            public List<ModuleBooster> Engines;
            public List<ModuleAirBrake> Brakes;
            public List<ModuleWing> Wings;
            public bool NoProps = false;            // Do we have to rely on fuel only?
            public bool SkewedFlightCenter = false; // Are we going to struggle when turning?

            public Vector3 PropBias = Vector3.zero;
            public Vector3 BoostBias = Vector3.zero;

            //Manuvering
            public Vector3 AirborneDest = Vector3.zero;
            public float DestSuccessRad // When we have reached our airborne destination
            { 
                get { try { return thisInst.MinimumRad; } catch { return 10; } }
            }

            public float AdvisedThrottle = 0;                  // Forward for aircraft, Upwards for helicopters
            public float MainThrottle = 0;                  // Forward for aircraft, Upwards for helicopters
            public float CurrentThrottle = 0;               // 

            /// <summary> IN m/s !!!</summary>
            public const float Stallspeed = 25;

            //Data Gathering
            public float SlowestPropLerpSpeed = 1;
            public float PropLerpValue = 10;
            public float AerofoilSluggishness = 1;
            public float RollStrength = 1;
            public int PerformUTurn = 0;    //set this to one to ignite the multi-stage process
            public Vector3 FlyingChillFactor = Vector3.one * 30; //The higher the values, the less stiff the controls will be

            //Error-Checking
            public float CorrectionThreshold = 5;
            public float AllowedErrorDist = 10;
            public bool ForcePitchUp = false;
            public bool TakeOff = false;
            public bool Grounded = false;

            public static AirAssistance Initiate(Tank tank, AIECore.TankAIHelper thisInst, Enemy.RCore.EnemyMind mind = null)
            {
                FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
                var pilot = tank.gameObject.GetComponent<AirAssistance>();
                if (pilot.IsNull())
                    pilot = tank.gameObject.AddComponent<AirAssistance>();
                pilot.tank = tank;
                pilot.thisInst = thisInst;
                tank.AttachEvent.Subscribe(OnAttach);
                tank.DetachEvent.Subscribe(OnDetach);

                // SETUP
                pilot.CheckFlightBlocks();
                pilot.CurrentThrottle = 0;

                //Now determine type of craft
                if (mind.IsNull())
                {
                    //if (thisInst.isAstrotechAvail && thisInst.DediAI == AIECore.DediAIType.Aviator)
                    //    InitiateForVTOL(tank, pilot);
                    if (!pilot.NoProps)
                    {
                        if (Vector3.Dot(pilot.PropBias, Vector3.up) > 0.6f)
                        {   // Likely a helicopter
                            InitiateForChopper(tank, pilot);
                        }
                        else if (Vector3.Dot(pilot.PropBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            InitiateForVTOL(tank, pilot);
                        }
                        else
                        {   // Likely an airplane
                            InitiateForAirplane(tank, pilot);
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.6f)
                        {   // Likely a helicopter
                            InitiateForChopper(tank, pilot);
                        }
                        else if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            InitiateForVTOL(tank, pilot);
                        }
                        else
                        {   // Likely an airplane
                            InitiateForAirplane(tank, pilot);
                        }
                    }
                    Debug.Log("TACtical_AI: Tech " + tank.name + " has been assigned Allied aircraft AI with flight mentality " + pilot.FlyStyle.ToString() + " and flying chill of " + pilot.FlyingChillFactor);
                }
                else
                {
                    if (!pilot.NoProps)
                    {
                        if (Vector3.Dot(pilot.PropBias, Vector3.up) > 0.6f)
                        {   // Likely a helicopter
                            InitiateForChopper(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                        }
                        else if (Vector3.Dot(pilot.PropBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            InitiateForVTOL(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                        }
                        else
                        {   // Likely an airplane
                            InitiateForAirplane(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.6f)
                        {   // Likely a helicopter
                            InitiateForChopper(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                        }
                        else if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            InitiateForVTOL(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                        }
                        else
                        {   // Likely an airplane
                            InitiateForAirplane(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                        }
                    }
                    Debug.Log("TACtical_AI: Tech " + tank.name + " has been assigned aircraft AI with " + mind.EvilCommander.ToString() + " mentality " + pilot.FlyStyle.ToString() + " and flying chill of " + pilot.FlyingChillFactor);
                }
                return pilot;
            }
            public void Recycle()
            {
                if (this.IsNotNull())
                {
                    tank.AttachEvent.Unsubscribe(OnAttach);
                    tank.DetachEvent.Unsubscribe(OnDetach);
                    Debug.Log("TACtical_AI: Removed aircraft AI from " + tank.name);
                    DestroyImmediate(this);
                }
            }
            public void Reset()
            {

            }
            public void CheckFlightBlocks()
            {
                FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);

                // SETUP
                UpdateStatus();
                float lowestDelta = 100;
                float guzzleLevel = 0;
                int consumeBoosters = 0;
                Vector3 biasDirection = Vector3.zero;
                Vector3 boostBiasDirection = Vector3.zero;
                foreach (ModuleBooster module in Engines)
                {
                    //Get the slowest spooling one
                    List<FanJet> jets = module.transform.GetComponentsInChildren<FanJet>().ToList();
                    foreach (FanJet jet in jets)
                    {
                        if (jet.spinDelta <= 10)
                        {
                            biasDirection -= tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForwards);
                            if (jet.spinDelta < lowestDelta)
                                lowestDelta = jet.spinDelta;
                        }
                    }
                    List<BoosterJet> boosts = module.transform.GetComponentsInChildren<BoosterJet>().ToList();
                    foreach (BoosterJet boost in boosts)
                    {
                        if (boost.ConsumesFuel)
                        {
                            consumeBoosters++;
                            guzzleLevel += boost.BurnRate;
                        }
                        //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                        boostBiasDirection -= tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection)) * (float)boostGet.GetValue(boost);
                    }
                }

                if (lowestDelta > 10 && boostBiasDirection == Vector3.zero)
                {   //IT HAS NO VALID PROPS OR BOOSTERS!!!!
                    Debug.Log("TACtical AI: Tech " + tank.name + " DOES NOT HAVE ANY PROPS OR BOOSTERS TO FLY USING!!");
                    return;
                }
                if (lowestDelta > 10 && consumeBoosters > 0)
                {
                    Debug.Log("TACtical AI: Tech " + tank.name + " DOES NOT HAVE ANY PROPS TO FLY USING!!");
                    NoProps = true;
                }
                BoostBias = boostBiasDirection;

                boostBiasDirection.Normalize();
                biasDirection.Normalize();
                PropBias = biasDirection;
                if (Mathf.Abs(Vector3.Dot(biasDirection, Vector3.right)) > 0.2f)
                {   //CENTER OF THRUST MAY BE OFF!!!
                    SkewedFlightCenter = true;
                    Debug.Log("TACtical AI: Tech " + tank.name + " reported to have off-centered thrust of a factor of " + Mathf.Abs(Vector3.Dot(biasDirection.normalized, Vector3.right)) + ".  \nAs all props don't have uniform thrust backwards and forwards (in relation to the root cab), the AI may not be able to fly correctly!!!");
                }

                float aerofoilSpeed = 1;
                foreach (ModuleWing module in Wings)
                {
                    //Get teh slowest spooling one
                    List<ModuleWing.Aerofoil> foils = module.m_Aerofoils.ToList();
                    foreach (ModuleWing.Aerofoil foil in foils)
                    {
                        if (foil.flapTurnSpeed > 0.01)
                        {
                            if (foil.flapTurnSpeed < aerofoilSpeed)
                                aerofoilSpeed = foil.flapTurnSpeed;
                        }
                    }
                }
                SlowestPropLerpSpeed = lowestDelta;
                PropLerpValue = 10f / SlowestPropLerpSpeed;
                AerofoilSluggishness = 45 / aerofoilSpeed;
                FlyingChillFactor = Vector3.one * AerofoilSluggishness;
                RollStrength = Mathf.Clamp(aerofoilSpeed * 2, 0.5f, 2);
            }
            public void UpdateStatus()
            {
                Engines = tank.blockman.IterateBlockComponents<ModuleBooster>().ToList();
                Brakes = tank.blockman.IterateBlockComponents<ModuleAirBrake>().ToList();
                Wings = tank.blockman.IterateBlockComponents<ModuleWing>().ToList();
            }
        }

        //Navigation Director - set airborne positions for the plane to fly to based on lastDestination
        public static bool FlightDirector(AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot)
        {
            if (thisInst.Pilot == null)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  FIRED FlightDirector WITHOUT THE REQUIRED AirAssistance MODULE!!!");
                return false;
            }
            if (thisInst.AIState == 1)
            {
                pilot.ForcePitchUp = false;
                if (pilot.Grounded)
                {   //Become a ground vehicle for now
                    if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                    {
                        return false;
                    }
                    //Try fighting the controls to land safely

                    return true;
                }
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                if (pilot.FlyStyle == FlightType.Helicopter)
                {
                    bool combat = AIEDrive.TryHandleCombat(thisInst, tank);
                    if (combat)
                    {
                        pilot.AirborneDest = thisInst.lastDestination;
                    }
                    else
                    {
                        if (thisInst.ProceedToObjective)
                        {   // Fly to target
                            pilot.AirborneDest = thisInst.lastDestination;
                        }
                        else if (thisInst.MoveFromObjective)
                        {   // Fly away from target
                            thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                            pilot.AirborneDest = ((tank.trans.position - thisInst.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            thisInst.lastPlayer = thisInst.GetPlayerTech();
                            if (thisInst.lastPlayer.IsNotNull())
                            {
                                pilot.AirborneDest.y = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.GroundOffsetHeight / 5);
                            }
                            else
                            {   //stay
                                pilot.AirborneDest = thisInst.lastDestination;
                            }
                        }
                    }

                    pilot.AirborneDest = AIEPathing.OffsetFromGround(pilot.AirborneDest, thisInst, 50);
                    pilot.AirborneDest = AvoidAssistAir(tank, pilot.AirborneDest, tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));

                    if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * Time.deltaTime), 34))
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Avoiding Ground!");
                        pilot.ForcePitchUp = true;
                    }
                }
                else // AIrcraft or VTOL
                {
                    pilot.AdvisedThrottle = 0;
                    bool combat = TryHandleDogfighting(pilot, thisInst, tank);
                    if (combat)
                    {
                        pilot.AirborneDest = thisInst.lastDestination;
                    }
                    else
                    {
                        if (thisInst.ProceedToObjective)
                        {   // Fly to target
                            if ((thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                            {   //We are at target
                                pilot.AirborneDest = thisInst.lastDestination + (tank.rootBlockTrans.forward * 100);
                            }
                            else
                            {
                                pilot.AirborneDest = thisInst.lastDestination;
                            }
                        }
                        else if (thisInst.MoveFromObjective)
                        {   // Fly away from target
                            pilot.AirborneDest = ((tank.trans.position - thisInst.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {   // Orbit last position
                            if ((pilot.AirborneDest - tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                            {   //We are at target
                                pilot.AirborneDest = pilot.AirborneDest + (-tank.rootBlockTrans.right * 50);
                            }
                            else
                            {
                                pilot.AirborneDest = thisInst.lastDestination;
                            }
                        }
                    }

                    pilot.AirborneDest = AIEPathing.OffsetFromGround(pilot.AirborneDest, thisInst);
                    pilot.AirborneDest = AvoidAssistAir(tank, pilot.AirborneDest, tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    AdviseThrottle(pilot, thisInst, tank, pilot.AirborneDest);

                    if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime), 40))
                    {
                        pilot.ForcePitchUp = true;
                        pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).magnitude;
                    }
                }
            }
            else if (thisInst.AIState == 2) //enemy
            {
                var mind = tank.GetComponent<Enemy.RCore.EnemyMind>();
                if (pilot.FlyStyle == FlightType.Helicopter)
                {
                    pilot.ForcePitchUp = false;
                    if (pilot.Grounded)
                    {   //Become a ground vehicle for now
                        if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                        {
                            return false;
                        }
                        //Try fighting the controls to land safely

                        return true;
                    }
                    bool combat = AIEDrive.TryCombatEnemy(thisInst, tank, mind); 
                    if (combat)
                    {
                        pilot.AirborneDest = thisInst.lastDestination;
                    }
                    else
                    {
                        if (thisInst.ProceedToObjective)
                        {   // Fly to target
                            pilot.AirborneDest = thisInst.lastDestination;
                        }
                        else if (thisInst.MoveFromObjective)
                        {   // Fly away from target
                            thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                            pilot.AirborneDest = ((tank.trans.position - thisInst.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {
                            thisInst.lastPlayer = thisInst.GetPlayerTech();
                            if (thisInst.lastPlayer.IsNotNull())
                            {
                                pilot.AirborneDest.y = thisInst.lastPlayer.tank.boundsCentreWorldNoCheck.y + (thisInst.GroundOffsetHeight / 5);
                            }
                            else
                            {   //Fly off the screen
                                Debug.Log("TACtical_AI: Tech " + tank.name + "  Leaving scene!");
                                Vector3 fFlat = tank.rootBlockTrans.forward;
                                fFlat.y = 0;
                                pilot.AirborneDest = (fFlat.normalized * 1000) + tank.boundsCentreWorldNoCheck;
                            }
                        }
                    }

                    pilot.AirborneDest = AIEPathing.OffsetFromGround(pilot.AirborneDest, thisInst, 50);
                    pilot.AirborneDest = Enemy.RPathfinding.AvoidAssistEnemy(tank, pilot.AirborneDest, tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));

                    if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * Time.deltaTime), 34))
                    {
                        Debug.Log("TACtical_AI: Tech " + tank.name + "  Avoiding Ground!");
                        pilot.ForcePitchUp = true;
                    }
                }
                else
                {
                    TestForMayday(thisInst, tank, pilot);
                    pilot.ForcePitchUp = false;
                    if (pilot.Grounded)
                    {   //Become a ground vehicle for now
                        if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                        {
                            return false;
                        }
                        //Try fighting the controls to land safely

                        return true;
                    }
                    thisInst.Retreat = false;
                    bool combat = AIEDrive.TryCombatEnemy(thisInst, tank, mind);// Placeholder for now, will have dogfighting AI
                    if (combat)
                    {
                        pilot.AirborneDest = thisInst.lastDestination;
                    }
                    else
                    {
                        if ((pilot.AirborneDest - tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                        {   //We are at target
                            Debug.Log("TACtical_AI: Tech " + tank.name + " Arrived at destination");

                            Vector3 lFlat;
                            if (tank.rootBlockTrans.up.y > 0)
                                lFlat = -tank.rootBlockTrans.right + (tank.rootBlockTrans.forward * 2);
                            else
                                lFlat = tank.rootBlockTrans.right + (tank.rootBlockTrans.forward * 2);
                            lFlat.y = 0.1f;
                            pilot.AirborneDest =pilot.AirborneDest + (lFlat * 50);
                        }
                        else if (thisInst.ProceedToObjective)
                        {   // Fly to target
                            thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                            if ((thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                            {   //We are at target
                                pilot.AirborneDest = thisInst.lastDestination + (tank.rootBlockTrans.forward * 100);
                            }
                            else
                            {
                                pilot.AirborneDest = thisInst.lastDestination;
                            }
                        }
                        else if (thisInst.MoveFromObjective)
                        {   // Fly away from target
                            thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                            pilot.AirborneDest = ((tank.trans.position - thisInst.lastDestination).normalized * (pilot.DestSuccessRad * 2)) + tank.boundsCentreWorldNoCheck;
                        }
                        else
                        {   // Orbit above player height to invoke trouble
                            thisInst.lastPlayer = thisInst.GetPlayerTech();
                            if (thisInst.lastPlayer.IsNotNull())
                            {
                                pilot.AirborneDest.y = (thisInst.lastPlayer.tank.boundsCentreWorldNoCheck + (Vector3.up * (thisInst.GroundOffsetHeight / 5))).y;
                            }
                            else
                            {   //Fly off the screen
                                Vector3 fFlat = tank.rootBlockTrans.forward;
                                fFlat.y = 0.25f;
                                pilot.AirborneDest =(fFlat.normalized * 1000) + tank.boundsCentreWorldNoCheck;
                            }
                        }
                    }
                    pilot.AirborneDest = AIEPathing.OffsetFromGround(pilot.AirborneDest, thisInst);
                    pilot.AirborneDest = Enemy.RPathfinding.AvoidAssistEnemy(tank, pilot.AirborneDest, tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    AdviseThrottle(pilot, thisInst, tank, pilot.AirborneDest);

                    if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime), 75))
                    {
                        //Debug.Log("TACtical_AI: Tech " + tank.name + "  Avoiding Ground!");
                        pilot.ForcePitchUp = true;
                        pilot.AirborneDest.y += (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).magnitude;
                    }
                }
            }    
            return true;
        }

        //Flight Maintainer - handle the flight between airborne positions
        public static bool FlightMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot)
        {   //Universal handler
            if (thisInst.Pilot == null)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  FIRED FlightMaintainer WITHOUT THE REQUIRED AirAssistance MODULE!!!");
                return false;
            }
            switch (pilot.FlyStyle)
            {
                case FlightType.Aircraft:   // Throttle Horizontally
                    PilotTechAircraft(thisControl, thisInst, tank, pilot);
                    break;
                case FlightType.Helicopter: // Throttle Vertically
                    PilotTechChopper(thisControl, thisInst, tank, pilot);
                    break;
                case FlightType.VTOL:       // Combination of Helicopter and Aircraft based on altitude
                    PilotTechVTOL(thisControl, thisInst, tank, pilot);
                    break;
            }

            return true;
        }


        // COMBAT DIRECTOR - Applies to Aircraft only
        public static bool TryHandleDogfighting(AirAssistance pilot, AIECore.TankAIHelper thisInst, Tank tank)
        {
            bool output = false;
            if (thisInst.PursueThreat && !thisInst.Retreat && thisInst.lastEnemy.IsNotNull())
            {
                output = true;
                float driveDyna = Mathf.Clamp(((thisInst.lastEnemy.transform.position - tank.boundsCentreWorldNoCheck).magnitude - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = AvoidAssistAir(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = AvoidAssistAir(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);
                    }
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = AvoidAssistAir(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = AvoidAssistAir(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else
                    {
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);
                    }
                }
                AdviseThrottleTarget(pilot, tank, thisInst.lastEnemy);
            }
            return output;
        }
        public static bool TryDogfightingEnemy(AirAssistance pilot, AIECore.TankAIHelper thisInst, Tank tank, Enemy.RCore.EnemyMind mind)
        {
            bool output = false;
            if (thisInst.PursueThreat && !thisInst.Retreat && thisInst.lastEnemy.IsNotNull())
            {
                output = true;
                float driveDyna = Mathf.Clamp(((thisInst.lastEnemy.transform.position - tank.boundsCentreWorldNoCheck).magnitude - thisInst.IdealRangeCombat) / 3f, -1, 1);
                if (thisInst.SideToThreat)
                {
                    if (thisInst.FullMelee)
                    {   //orbit WHILE at enemy!
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);

                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else
                    {
                        thisInst.DriveDir = EDriveType.Perpendicular;
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);
                    }
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.DriveDir = EDriveType.Forwards;
                        thisInst.AdviseAway = true;
                        thisInst.lastDestination = Enemy.RPathfinding.AvoidAssistEnemy(tank, ForeAiming(thisInst.lastEnemy), tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));
                    }
                    else
                    {
                        thisInst.lastDestination = ForeAiming(thisInst.lastEnemy);
                    }
                }
                AdviseThrottleTarget(pilot, tank, thisInst.lastEnemy);
            }
            return output;
        }


        // AIRCRAFT CONTROLLERS
        public static bool PilotTechAircraft(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                pilot.MainThrottle = 1;
                pilot.PerformUTurn = 0;
                UpdateThrottle(pilot, thisControl);
                AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + ((tank.rootBlockTrans.forward + Vector3.up) * 100));
            }
            else
            {
                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    //Debug.Log("TACtical_AI: Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
                    pilot.MainThrottle = 1;
                    UpdateThrottle(pilot, thisControl);
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < AirAssistance.Stallspeed - 4)
                    {   //ABORT!!!
                        Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn with velocity " + tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z);
                        pilot.PerformUTurn = -1;
                    }
                    else if (Vector3.Dot(Vector3.down, tank.rbody.velocity.normalized) > 0.3f)
                    {   //ABORT!!!
                        Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                        pilot.PerformUTurn = -1;
                    }
                    if (pilot.PerformUTurn == 1)
                    {
                        AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + tank.rootBlockTrans.forward * 100);
                        if (pilot.CurrentThrottle > 0.95)
                            pilot.PerformUTurn = 2;
                    }
                    else if (pilot.PerformUTurn == 2)
                    {
                        AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                        if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.75f)
                            pilot.PerformUTurn = 3;
                    }
                    else if (pilot.PerformUTurn == 3)
                    {
                        AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                        if (Vector3.Dot((pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6f)
                            pilot.PerformUTurn = 0;
                    }
                    return true;
                }
                else if (pilot.PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    UpdateThrottle(pilot, thisControl);
                    AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                        pilot.PerformUTurn = 0;
                    return true;
                }
                else
                {
                    pilot.MainThrottle = pilot.AdvisedThrottle;
                    UpdateThrottle(pilot, thisControl);
                    AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                }
            }

            return true;
        }
        public static Vector3 DetermineRoll(Tank tank, AirAssistance pilot, Vector3 Navi3DDirect)
        {
            //Vector3 turnValUp = Quaternion.LookRotation(tank.rootBlockTrans.forward, tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;

            if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                return Vector3.up;
            Vector3 Heading = tank.rootBlockTrans.InverseTransformDirection(Navi3DDirect);

            Vector3 direct = Vector3.up;
            if (pilot.PerformUTurn == 3)
            {
                direct = Vector3.down;
            }
            else if (pilot.PerformUTurn > 0 || pilot.ForcePitchUp)
            {
            }
            else if (Heading.z < -0.25 && pilot.PerformUTurn == 0 && AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
            { // Perform the Immelmann turn, or better known as the "U-Turn"
                pilot.PerformUTurn = 1;
            }
            else if (Heading.x > 0f && Heading.z < 0.85f - (0.2f / pilot.RollStrength))
            { // We roll to aim at target
                //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Right");
                Vector3 rFlat;
                if (tank.rootBlockTrans.up.y > 0)
                    rFlat = tank.rootBlockTrans.right;
                else
                    rFlat = -tank.rootBlockTrans.right;
                rFlat.y = -pilot.RollStrength;
                direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
            }
            else if (Heading.x < 0f && Heading.z < 0.85f - (0.2f / pilot.RollStrength))
            { // We roll to aim at target
                //Debug.Log("TACtical_AI: Tech " + tank.name + "  Roll turn Left");
                Vector3 rFlat;
                if (tank.rootBlockTrans.up.y > 0)
                    rFlat = tank.rootBlockTrans.right;
                else
                    rFlat = -tank.rootBlockTrans.right;
                rFlat.y = pilot.RollStrength;
                direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
            }
            return direct;
        }
        public static void AngleTowards(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot, Vector3 position)
        {
            //AI Steering Rotational
            FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(thisControl);

            thisInst.Navi3DDirect = (position - tank.boundsCentreWorldNoCheck).normalized;

            thisInst.Navi3DUp = DetermineRoll(tank, pilot, thisInst.Navi3DDirect);

            Vector3 turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;

            //Convert turnVal to runnable format
            if (turnVal.x > 180)
                turnVal.x = Mathf.Clamp(-((turnVal.x - 360) / pilot.FlyingChillFactor.x), -1, 1);
            else
                turnVal.x = Mathf.Clamp(-(turnVal.x / pilot.FlyingChillFactor.x), -1, 1);

            if (turnVal.y > 180)
                turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / pilot.FlyingChillFactor.y), -1, 1);
            else
                turnVal.y = Mathf.Clamp(-(turnVal.y / pilot.FlyingChillFactor.y), -1, 1);

            if (turnVal.z > 180)
                turnVal.z = Mathf.Clamp(-((turnVal.z - 360) / pilot.FlyingChillFactor.z), -1, 1);
            else
                turnVal.z = Mathf.Clamp(-(turnVal.z / pilot.FlyingChillFactor.z), -1, 1);

            //Stop Wobble
            if (Mathf.Abs(turnVal.x) < 0.05f)
                turnVal.x = 0;
            if (Mathf.Abs(turnVal.y) < 0.05f)
                turnVal.y = 0;
            if (Mathf.Abs(turnVal.z) < 0.05f)
                turnVal.z = 0;


            //Turn our work in to process
            control3D.m_State.m_InputRotation = turnVal;

            // DRIVE
            Vector3 DriveVar = Vector3.forward * pilot.CurrentThrottle;

            //Turn our work in to processing
            //Debug.Log("TACtical_AI: Tech " + tank.name + " steering" + turnVal);
            control3D.m_State.m_InputMovement = DriveVar;
            controlGet.SetValue(tank.control, control3D);
            return;
        }
        public static void AdviseThrottle(AirAssistance pilot, AIECore.TankAIHelper thisInst, Tank tank, Vector3 target)
        {
            if (pilot.AdvisedThrottle == 0)
            {
                if (tank.rbody.IsNotNull())
                {
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z > AirAssistance.Stallspeed + 5)
                    {
                        //float Extremes = (AIECore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + AIECore.Extremes(tank.blockBounds.extents)) / 2;
                        pilot.AdvisedThrottle = Mathf.Clamp((target - tank.boundsCentreWorldNoCheck).magnitude / pilot.PropLerpValue, 0, 1);
                        return;
                    }
                }
                pilot.AdvisedThrottle = 1;
            }
        }
        public static void AdviseThrottleTarget(AirAssistance pilot, Tank tank, Visible target)
        {
            if (pilot.AdvisedThrottle == 0)
            {
                if (tank.rbody.IsNotNull())
                {
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z > AirAssistance.Stallspeed + 5)
                    {
                        //float Extremes = (AIECore.Extremes(target.tank.blockBounds.extents) + AIECore.Extremes(tank.blockBounds.extents)) / 2;
                        pilot.AdvisedThrottle = Mathf.Clamp((target.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).magnitude / pilot.PropLerpValue, 0, 1);
                        return;
                    }
                }
                pilot.AdvisedThrottle = 1;
            }
        }


        // HELICOPTER CONTROLLERS
        public static bool PilotTechChopper(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff
                //Debug.Log("TACtical_AI: " + tank.name + " is taking off");
                pilot.MainThrottle = ModerateUpwardsThrust(tank, pilot, AIEPathing.OffsetFromGround(tank.boundsCentreWorldNoCheck, thisInst, 45), true);
                UpdateThrottleCopter(pilot, thisControl);
                AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest, true);
            }
            else
            {
                pilot.MainThrottle = ModerateUpwardsThrust(tank, pilot, pilot.AirborneDest);
                UpdateThrottleCopter(pilot, thisControl);
                AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                /*
                if (thisInst.lastEnemy.IsNotNull())
                {
                    Debug.Log("TACtical_AI: " + tank.name + " is in combat at " + pilot.AirborneDest + " tank at " + thisInst.lastEnemy.tank.boundsCentreWorldNoCheck);
                }
                */
            }

            return true;
        }
        public static void AngleTowardsUp(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot, Vector3 position, bool ForceAccend = false)
        {
            //AI Steering Rotational
            FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(thisControl);
            Vector3 turnVal;
            DeterminePitchRoll(tank, pilot, position, thisInst);
            Vector3 forwardFlat = thisInst.Navi3DDirect;
            forwardFlat.y = 0;
            if (ForceAccend)
            {
                turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(forwardFlat), tank.rootBlockTrans.InverseTransformDirection(Vector3.one)).eulerAngles;
            }
            else
            {
                turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DDirect), tank.rootBlockTrans.InverseTransformDirection(thisInst.Navi3DUp)).eulerAngles;
            }

            //Convert turnVal to runnable format
            if (turnVal.x > 180)
                turnVal.x = Mathf.Clamp(-((turnVal.x - 360) / pilot.FlyingChillFactor.x), -1, 1);
            else
                turnVal.x = Mathf.Clamp(-(turnVal.x / pilot.FlyingChillFactor.x), -1, 1);

            if (turnVal.y > 180)
                turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / pilot.FlyingChillFactor.y), -1, 1);
            else
                turnVal.y = Mathf.Clamp(-(turnVal.y / pilot.FlyingChillFactor.y), -1, 1);

            if (turnVal.z > 180)
                turnVal.z = Mathf.Clamp(-((turnVal.z - 360) / pilot.FlyingChillFactor.z), -1, 1);
            else
                turnVal.z = Mathf.Clamp(-(turnVal.z / pilot.FlyingChillFactor.z), -1, 1);

            //Stop Wobble
            if (Mathf.Abs(turnVal.x) < 0.05f)
                turnVal.x = 0;
            if (Mathf.Abs(turnVal.y) < 0.05f)
                turnVal.y = 0;
            if (Mathf.Abs(turnVal.z) < 0.05f)
                turnVal.z = 0;


            //Turn our work in to process
            control3D.m_State.m_InputRotation = turnVal;

            // DRIVE
            Vector3 DriveVar = tank.rootBlockTrans.InverseTransformVector(-tank.rbody.velocity) / pilot.PropLerpValue;
            DriveVar.x = Mathf.Clamp(DriveVar.x, -1, 1);
            DriveVar.z = Mathf.Clamp(DriveVar.z, -1, 1);
            DriveVar.y = 0;
            if (thisInst.MoveFromObjective)
                DriveVar.z = -1;
            else if (thisInst.ProceedToObjective)
                DriveVar.z = 1;
            DriveVar = DriveVar.normalized;
            DriveVar.y = pilot.CurrentThrottle;

            //Turn our work in to processing
            //Debug.Log("TACtical_AI: Tech " + tank.name + " | steering " + turnVal + " | drive " + DriveVar);
            control3D.m_State.m_InputMovement = DriveVar;
            controlGet.SetValue(tank.control, control3D);
            return;
        }
        public static void DeterminePitchRoll(Tank tank, AirAssistance pilot, Vector3 DestinationVector, AIECore.TankAIHelper thisInst, bool PointAtTarget = false)
        {
            Vector3 Heading = (DestinationVector - tank.boundsCentreWorldNoCheck).normalized;
            Vector3 fFlat = Heading;
            fFlat.y = 0;
            /*
            if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
            {
                thisInst.Navi3DDirect = fFlat;
                thisInst.Navi3DUp = Vector3.up;
                return;
            }
            */
            Vector3 direct;
            Vector3 rFlat;

            // X-axis turning
            if (tank.rootBlockTrans.up.y > 0)
                rFlat = tank.rootBlockTrans.right;
            else
                rFlat = -tank.rootBlockTrans.right;
            rFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).x / (10 / pilot.SlowestPropLerpSpeed), -0.75f, 0.75f);
            direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;


            // Other axis turning
            if (PointAtTarget)
                fFlat.y = Heading.y;
            else
            {
                if (thisInst.MoveFromObjective || thisInst.AdviseAway)
                    fFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity + Heading).z / (10 / pilot.SlowestPropLerpSpeed), -0.75f, 0.75f);
                else if (thisInst.ProceedToObjective)
                    fFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity - Heading).z / (10 / pilot.SlowestPropLerpSpeed), -0.75f, 0.75f);
                else
                    fFlat.y = Mathf.Clamp(tank.rootBlockTrans.InverseTransformPoint(tank.rbody.velocity).z / (10 / pilot.SlowestPropLerpSpeed), -0.75f, 0.75f);
            }


            thisInst.Navi3DDirect = fFlat.normalized;
            thisInst.Navi3DUp = direct;
        }
        public static float ModerateUpwardsThrust(Tank tank, AirAssistance pilot, Vector3 targetHeight, bool ForceUp = false)
        {
            float final = ((targetHeight.y - tank.boundsCentreWorldNoCheck.y) / (pilot.PropLerpValue / 2)) + 0.5f;
            //Debug.Log("TACtical_AI: " + tank.name + " thrust = " + final + " | velocity " + tank.rbody.velocity);
            if (ForceUp)
            {
                final = 1;
            }
            else if (tank.rbody.velocity.y < 0 && final > -4 && final < 0)  // try ease fall
            {
                //Debug.Log("TACtical_AI: " + tank.name + " dampening fall");
                final = Mathf.Abs(-Mathf.Pow(tank.rbody.velocity.y, 2));
            }
            if (tank.rbody.velocity.y > 4 && final > 0 && final < 1.4f)     // try ease flight
            {
                //Debug.Log("TACtical_AI: " + tank.name + " dampening up speed");
                final = 0;
            }
            if (final < 0)
                final = 0;
            //Debug.Log("TACtical_AI: " + tank.name + " thrustFinal = " + final);

            return Mathf.Clamp(final, 0, 1);
        }
        public static void UpdateThrottleCopter(AirAssistance pilot, TankControl control)
        {
            if (pilot.NoProps)
            {
                if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.tank.blockBounds.extents) * 2) && !pilot.tank.beam.IsActive)
                    control.BoostControlJets = true;
                else
                    control.BoostControlJets = false;
            }
            pilot.CurrentThrottle = Mathf.Clamp(pilot.MainThrottle, 0, 1);
        }


        // VTOL CONTROLLER
        public static bool PilotTechVTOL(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot)
        {
            if (pilot.Grounded)
            {   //Become a ground vehicle for now
                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                {
                    return false;
                }
                //Try fighting the controls to land safely

                return true;
            }
            if (tank.grounded || pilot.ForcePitchUp)
            {   // Try and takeoff like helicopter
                pilot.MainThrottle = ModerateUpwardsThrust(tank, pilot, AIEPathing.OffsetFromGround(tank.boundsCentreWorldNoCheck, thisInst, AIECore.Extremes(tank.blockBounds.extents) * 2));
                UpdateThrottle(pilot, thisControl);
                AngleTowardsUp(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck, true);
            }
            else
            {   //Fly like plane
                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    //Debug.Log("TACtical_AI: Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
                    pilot.MainThrottle = 1;
                    UpdateThrottle(pilot, thisControl);
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < AirAssistance.Stallspeed - 4)
                    {   //ABORT!!!
                        Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn with velocity " + tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z);
                        pilot.PerformUTurn = -1;
                    }
                    else if (Vector3.Dot(Vector3.down, tank.rbody.velocity.normalized) > 0.4f)
                    {   //ABORT!!!
                        Debug.Log("TACtical_AI: Tech " + tank.name + "  Aborted U-Turn as too much movement to the ground");
                        pilot.PerformUTurn = -1;
                    }
                    if (pilot.PerformUTurn == 1)
                    {
                        AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + tank.rootBlockTrans.forward * 100);
                        if (pilot.CurrentThrottle > 0.95)
                            pilot.PerformUTurn = 2;
                    }
                    else if (pilot.PerformUTurn == 2)
                    {
                        AngleTowards(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck + (Vector3.up * 100));
                        if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.75f)
                            pilot.PerformUTurn = 3;
                    }
                    else if (pilot.PerformUTurn == 3)
                    {
                        AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                        if (Vector3.Dot((pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized, tank.rootBlockTrans.forward) > 0.6f)
                            pilot.PerformUTurn = 0;
                    }
                    return true;
                }
                else if (pilot.PerformUTurn == -1)
                {
                    pilot.MainThrottle = 1;
                    UpdateThrottle(pilot, thisControl);
                    AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).normalized) > 0)
                        pilot.PerformUTurn = 0;
                    return true;
                }
                else
                {
                    UpdateThrottle(pilot, thisControl);
                    AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                }
            }

            return true;
        }



        // Action Updaters
        /// <summary>
        /// Returns true if the craft is likely never going to recover
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <param name="pilot"></param>
        public static bool TestForMayday(AIECore.TankAIHelper thisInst, Tank tank, AirAssistance pilot)
        {
            if (thisInst.PendingSystemsCheck)
            {
                pilot.UpdateStatus();
                bool damaged = false;

                if (pilot.Engines.Count() < 1)
                    damaged = true;
                int wingCount = 0;
                foreach (ModuleWing wing in pilot.Wings)
                {
                    wingCount += wing.m_Aerofoils.Length;
                }
                if (wingCount < 5)
                    damaged = true;

                if (AIERepair.CanRepairNow(tank))
                {
                    return false;
                }
                return damaged;
            }
            return false;
        }
        public static void OnAttach(TankBlock block, Tank tank)
        {
            var pilot = tank.GetComponent<AirAssistance>();
            if (pilot.IsNull())
                return;
            if (AIERepair.SystemsCheck(tank))
                pilot.Grounded = TestForMayday(tank.GetComponent<AIECore.TankAIHelper>(), tank, pilot);
        }
        public static void OnDetach(TankBlock block, Tank tank)
        {   //Disabled for now as some irrelievent warning that doesn't have a label is spamming the logs
            /*
            if (tank.blockman.GetRootBlock().IsNull())
            {
                if (tank.GetComponent<AirAssistance>().IsNotNull())
                    tank.GetComponent<AirAssistance>().Recycle();
                return;
            }
            var pilot = tank.GetComponent<AirAssistance>();
            if (pilot.IsNull())
                return;
            var mem = tank.GetComponent<AIERepair.DesignMemory>();
            if (mem.IsNull())
                return;
            if (AIERepair.SystemsCheck(tank))
                pilot.Grounded = TestForMayday(tank.GetComponent<AIECore.TankAIHelper>(), tank, pilot);
            */
        }
        public static void UpdateThrottle(AirAssistance pilot, TankControl control)
        {
            if (pilot.NoProps)
            {
                if (pilot.FlyStyle == FlightType.Aircraft)
                {
                    if (pilot.MainThrottle > 0.1 && pilot.tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z < AirAssistance.Stallspeed + 5 && !pilot.tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = false;
                }
                else // VTOL
                {
                    if (pilot.MainThrottle > 0.1 && pilot.tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z < AirAssistance.Stallspeed + 5 && !pilot.tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.tank.blockBounds.extents) * 2) && !pilot.tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = false;
                }

                // Still try to move wheels and other things
                if (pilot.CurrentThrottle + (pilot.SlowestPropLerpSpeed * Time.deltaTime) < pilot.MainThrottle)
                {
                    pilot.CurrentThrottle += pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (pilot.CurrentThrottle - (pilot.SlowestPropLerpSpeed * Time.deltaTime) > pilot.MainThrottle)
                {
                    pilot.CurrentThrottle -= pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    pilot.CurrentThrottle = pilot.MainThrottle;
                }
            }
            else
            {
                if (pilot.CurrentThrottle + (pilot.SlowestPropLerpSpeed * Time.deltaTime) < pilot.MainThrottle)
                {
                    pilot.CurrentThrottle += pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (pilot.CurrentThrottle - (pilot.SlowestPropLerpSpeed * Time.deltaTime) > pilot.MainThrottle)
                {
                    pilot.CurrentThrottle -= pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    pilot.CurrentThrottle = pilot.MainThrottle;
                }
                pilot.CurrentThrottle = Mathf.Clamp(pilot.CurrentThrottle, -1, 1);
            }
        }
        public static Vector3 AvoidAssistAir(Tank tank, Vector3 targetIn, Vector3 predictionOffset)
        {
            //The method to determine if we should avoid an ally nearby while navigating to the target
            var thisInst = tank.GetComponent<AIECore.TankAIHelper>();

            if (thisInst.AvoidStuff)
            {
                try
                {
                    Tank lastCloseAlly;
                    float lastAllyDist;
                    if (thisInst.SecondAvoidence)// MORE processing power
                    {
                        lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(predictionOffset, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal);
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
                    lastCloseAlly = AIEPathing.ClosestAllyPrecision(predictionOffset, out lastAllyDist);
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
                    Debug.Log("TACtical_AI: Crash on Avoid " + e);
                    return targetIn;
                }
            }
            if (targetIn.IsNaN())
            {
                Debug.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
                AIECore.TankAIManager.FetchAllAllies();
            }
            return targetIn;
        }
        public static Vector3 ForeAiming(Visible target)
        {
            if (target.tank.rbody.IsNull())
                return target.tank.boundsCentreWorldNoCheck;
            else
                return target.tank.rbody.velocity + target.tank.boundsCentreWorldNoCheck;
        }



        // Initation
        public static void InitiateForAirplane(Tank tank, AirAssistance pilot)
        {
            pilot.FlyStyle = FlightType.Aircraft;
        }
        public static void InitiateForChopper(Tank tank, AirAssistance pilot)
        {
            pilot.FlyStyle = FlightType.Helicopter;
        }
        public static void InitiateForVTOL(Tank tank, AirAssistance pilot)
        {
            pilot.FlyStyle = FlightType.VTOL;
        }
    }
}
