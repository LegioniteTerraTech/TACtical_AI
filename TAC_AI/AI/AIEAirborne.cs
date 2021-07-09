using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;
using TAC_AI.AI.MovementAI;

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
        public class AirAssistance : MonoBehaviour, ITechDriver
        {
            // ITechDriver interface
            private Tank _tank;
            public Tank Tank {
                get => _tank;
                internal set => _tank = value;
            }
            private AIECore.TankAIHelper _helper;
            public AIECore.TankAIHelper Helper
            {
                get => _helper;
                internal set => _helper = value;
            }
            private IMovementAI _AI;
            public IMovementAI AI {
                get => _AI;
                internal set => _AI = value;
            }

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
                get { try { return Helper.MinimumRad; } catch { return 10; } }
            }

            public float AdvisedThrottle = 0;               // Forward for aircraft, Upwards for helicopters
            public float MainThrottle = 0;                  // Forward for aircraft, Upwards for helicopters
            public float CurrentThrottle = 0;               // 

            /// <summary> IN m/s !!!</summary>
            public const float Stallspeed = 25;
            public const float GroundAttackStagingDist = 250;

            //Data Gathering
            public float SlowestPropLerpSpeed = 1;
            public float PropLerpValue = 10;
            public float AerofoilSluggishness = 1;
            public float RollStrength = 1;
            public int PerformDiveAttack = 0;       //set this to one to launch dive bombing
            public int PerformUTurn = 0;            //set this to one to ignite the multi-stage process
            public Vector3 FlyingChillFactor = Vector3.one * 30; //The higher the values, the less stiff the controls will be

            //Error-Checking
            public int ErrorsInTakeoff = 0;         // If this gets too high, then this tech isn't meant to fly
            public int ErrorsInUTurn = 0;           // If this gets too high, then this tech isn't meant to Immelmann
            public bool DestroyOnTerrain = false;   // Should the aircraft disintegrate on collision with terrain?
            public bool LargeAircraft = false;      // Restrict turning to 45 and no U-Turns
            public bool ForcePitchUp = false;
            public bool TakeOff = false;
            public bool Grounded = false;
            public bool TargetGrounded = false;     // Are we dealing with a target that is on the ground?
            public bool LowerEngines = false;       // Choppers: Too high! Too high!  Airplanes: conserve booster fuel

            public static AIEAirborne.AirAssistance Initiate(Tank tank, AIECore.TankAIHelper thisInst, Enemy.RCore.EnemyMind mind = null)
            {
                FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
                var pilot = tank.gameObject.GetComponent<AIEAirborne.AirAssistance>();
                if (pilot.IsNull())
                    pilot = tank.gameObject.AddComponent<AIEAirborne.AirAssistance>();
                pilot.Tank = tank;
                pilot.Helper = thisInst;
                tank.AttachEvent.Subscribe(OnAttach);
                tank.DetachEvent.Subscribe(OnDetach);

                // SETUP
                pilot.CheckAllFlightBlocks();
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
                            pilot.AI = null;
                            pilot.AI = new HelicopterAI();
                            pilot.AI.Initiate(tank, pilot);
                        }
                        else if (Vector3.Dot(pilot.PropBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            pilot.AI = null;
                            pilot.AI = new VtolAI();
                            pilot.AI.Initiate(tank, pilot);
                        }
                        else
                        {   // Likely an airplane
                            pilot.AI = null;
                            pilot.AI = new AirplaneAI();
                            pilot.AI.Initiate(tank, pilot);
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.6f)
                        {   // Likely a helicopter
                            pilot.AI = null;
                            pilot.AI = new HelicopterAI();
                            pilot.AI.Initiate(tank, pilot);
                        }
                        else if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            pilot.AI = null;
                            pilot.AI = new VtolAI();
                            pilot.AI.Initiate(tank, pilot);
                        }
                        else
                        {   // Likely an airplane
                            pilot.AI = null;
                            pilot.AI = new AirplaneAI();
                            pilot.AI.Initiate(tank, pilot);
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
                            pilot.AI = null;
                            pilot.AI = new HelicopterAI();
                            pilot.AI.Initiate(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                        }
                        else if (Vector3.Dot(pilot.PropBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            pilot.AI = null;
                            pilot.AI = new VtolAI();
                            pilot.AI.Initiate(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                        }
                        else
                        {   // Likely an airplane
                            pilot.AI = null;
                            pilot.AI = new AirplaneAI();
                            pilot.AI.Initiate(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.6f)
                        {   // Likely a helicopter
                            pilot.AI = null;
                            pilot.AI = new HelicopterAI();
                            pilot.AI.Initiate(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                        }
                        else if (Vector3.Dot(pilot.BoostBias, Vector3.up) > 0.3f)
                        {   // Likely a VTOL
                            pilot.AI = null;
                            pilot.AI = new VtolAI();
                            pilot.AI.Initiate(tank, pilot);
                            mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                        }
                        else
                        {   // Likely an airplane
                            pilot.AI = null;
                            pilot.AI = new AirplaneAI();
                            pilot.AI.Initiate(tank, pilot);
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
                    Tank.AttachEvent.Unsubscribe(OnAttach);
                    Tank.DetachEvent.Unsubscribe(OnDetach);
                    Debug.Log("TACtical_AI: Removed aircraft AI from " + Tank.name);
                    DestroyImmediate(this);
                }
            }
            public void Reset()
            {

            }
            public void CheckEngines()
            {
                FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);

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
                            biasDirection -= Tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForwards);
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
                        boostBiasDirection -= Tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection)) * (float)boostGet.GetValue(boost);
                    }
                }

                if (lowestDelta > 10 && boostBiasDirection == Vector3.zero)
                {   //IT HAS NO VALID PROPS OR BOOSTERS!!!!
                    Debug.Log("TACtical AI: Tech " + Tank.name + " DOES NOT HAVE ANY PROPS OR BOOSTERS TO FLY USING!!");
                    return;
                }
                if (lowestDelta > 10 && consumeBoosters > 0)
                {
                    Debug.Log("TACtical AI: Tech " + Tank.name + " DOES NOT HAVE ANY PROPS TO FLY USING!!");
                    NoProps = true;
                }
                BoostBias = boostBiasDirection;

                boostBiasDirection.Normalize();
                biasDirection.Normalize();
                PropBias = biasDirection;
                if (Mathf.Abs(Vector3.Dot(biasDirection, Vector3.right)) > 0.2f)
                {   //CENTER OF THRUST MAY BE OFF!!!
                    SkewedFlightCenter = true;
                    Debug.Log("TACtical AI: Tech " + Tank.name + " reported to have off-centered thrust of a factor of " + Mathf.Abs(Vector3.Dot(biasDirection.normalized, Vector3.right)) + ".  \nAs all props don't have uniform thrust backwards and forwards (in relation to the root cab), the AI may not be able to fly correctly!!!");
                }
                SlowestPropLerpSpeed = lowestDelta;
                PropLerpValue = 10f / SlowestPropLerpSpeed;
            }
            public void CheckWings()
            {
                float aerofoilSpeed = 100;
                foreach (ModuleWing module in Wings)
                {
                    //Get teh slowest spooling one
                    List<ModuleWing.Aerofoil> foils = module.m_Aerofoils.ToList();
                    foreach (ModuleWing.Aerofoil foil in foils)
                    {
                        if (foil.flapTurnSpeed > 0.01)
                        {
                            if (foil.flapTurnSpeed < aerofoilSpeed)
                            {
                                aerofoilSpeed = foil.flapTurnSpeed;
                            }
                        }
                    }
                }
                if (AIECore.Extremes(Tank.blockBounds.size) > 18)
                    LargeAircraft = true;
                else
                    LargeAircraft = false;

                AerofoilSluggishness = 30 / aerofoilSpeed;
                if (FlyStyle == FlightType.Helicopter)
                    FlyingChillFactor = Vector3.one * 30;
                else
                {
                    FlyingChillFactor = Vector3.one * AerofoilSluggishness;
                    if (LargeAircraft)
                        FlyingChillFactor.y = 5;    // need accuraccy for large aircraft bombing runs
                    else
                        FlyingChillFactor.y = 10;  // Yaw isn't normally too strong on aircraft so we give it a boost.
                }

                RollStrength = Mathf.Clamp(aerofoilSpeed * 2, 0.5f, 2);
            }
            public void CheckAllFlightBlocks()
            {
                // SETUP
                UpdateStatus();
                CheckEngines();
                CheckWings();
            }
            public void UpdateStatus()
            {
                Engines = Tank.blockman.IterateBlockComponents<ModuleBooster>().ToList();
                Brakes = Tank.blockman.IterateBlockComponents<ModuleAirBrake>().ToList();
                Wings = Tank.blockman.IterateBlockComponents<ModuleWing>().ToList();
            }
        }

        //Navigation Director - set airborne positions for the plane to fly to based on lastDestination
        public static bool FlightDirector(AIECore.TankAIHelper thisInst, Tank tank, AIEAirborne.AirAssistance pilot)
        {
            if (thisInst.Pilot == null)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  FIRED FlightDirector WITHOUT THE REQUIRED AIEAirborne.AirAssistance MODULE!!!");
                return false;
            }
            TestForMayday(thisInst, tank, pilot);

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
                thisInst.lastDestination = AIEPathing.OffsetFromGroundA(thisInst.lastDestination, thisInst);
                return pilot.AI.DriveDirector();
            }
            else if (thisInst.AIState == 2) //enemy
            {
                Enemy.RCore.EnemyMind mind = tank.GetComponent<Enemy.RCore.EnemyMind>();
                if (!pilot.TargetGrounded)
                    thisInst.lastDestination = AIEPathing.OffsetFromGroundA(thisInst.lastDestination, thisInst);

                return pilot.AI.DriveDirectorEnemy(mind);
            }
            return true;
        }

        //Flight Maintainer - handle the flight between airborne positions
        public static bool FlightMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, AIEAirborne.AirAssistance pilot)
        {   //Universal handler
            if (thisInst.Pilot == null)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  FIRED FlightMaintainer WITHOUT THE REQUIRED AIEAirborne.AirAssistance MODULE!!!");
                return false;
            }
            thisControl.BoostControlJets = thisInst.BOOST;

            pilot.AI.DriveTech(thisControl, thisInst, tank);
            return false;
        }


        // Action Updaters
        /// <summary>
        /// Returns true if the craft is likely never going to recover
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <param name="pilot"></param>
        public static bool TestForMayday(AIECore.TankAIHelper thisInst, Tank tank, AIEAirborne.AirAssistance pilot)
        {
            if (thisInst.PendingSystemsCheck)
            {
                pilot.UpdateStatus();
                bool damaged = false;

                if (pilot.Engines.Count() < 1)
                    damaged = true;
                int wingCount = 0;
                if (pilot.FlyStyle == FlightType.Helicopter)
                {
                    pilot.CheckEngines();
                    if (pilot.NoProps)
                    {
                        if (pilot.BoostBias.y <= 0.6f)
                            damaged = true;
                    }
                    else
                    {
                        if (pilot.PropBias.y <= 0.6f)
                            damaged = true;
                    }
                }
                else
                {
                    foreach (ModuleWing wing in pilot.Wings)
                    {
                        wingCount += wing.m_Aerofoils.Length;
                    }
                    if (wingCount < 5)
                        damaged = true;
                }

                if (AIERepair.CanRepairNow(tank))
                {
                    return false;
                }
                if (damaged)
                    Debug.Log("TACtical_AI: " + tank.name + " has been deemed incapable of flight!");

                return damaged;
            }
            return false;
        }
        public static void OnAttach(TankBlock block, Tank tank)
        {
            var pilot = tank.GetComponent<AIEAirborne.AirAssistance>();
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
                if (tank.GetComponent<AIEAirborne.AirAssistance>().IsNotNull())
                    tank.GetComponent<AIEAirborne.AirAssistance>().Recycle();
                return;
            }
            var pilot = tank.GetComponent<AIEAirborne.AirAssistance>();
            if (pilot.IsNull())
                return;
            var mem = tank.GetComponent<AIERepair.DesignMemory>();
            if (mem.IsNull())
                return;
            if (AIERepair.SystemsCheck(tank))
                pilot.Grounded = TestForMayday(tank.GetComponent<AIECore.TankAIHelper>(), tank, pilot);
            */
        }
        public static void UpdateThrottle(AIECore.TankAIHelper thisInst, AIEAirborne.AirAssistance pilot, TankControl control)
        {
            if (pilot.NoProps)
            {
                if (pilot.FlyStyle == FlightType.Aircraft)
                {
                    if (pilot.MainThrottle > 0.1 && pilot.Tank.rootBlockTrans.InverseTransformVector(pilot.Tank.rbody.velocity).z < AIEAirborne.AirAssistance.Stallspeed + 5 && !pilot.Tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = thisInst.BOOST;
                }
                else // VTOL
                {
                    if (pilot.MainThrottle > 0.1 && pilot.Tank.rootBlockTrans.InverseTransformVector(pilot.Tank.rbody.velocity).z < AIEAirborne.AirAssistance.Stallspeed + 5 && !pilot.Tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.Tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.Tank.blockBounds.extents) * 2) && !pilot.Tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = thisInst.BOOST;
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
    }
}
