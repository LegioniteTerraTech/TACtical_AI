using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;

namespace TAC_AI.AI
{
    class AIControllerAir : MonoBehaviour, IMovementAIController
    {
        internal static FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo boostDir = typeof(BoosterJet).GetField("m_LocalBoostDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo fanDir = typeof(FanJet).GetField("m_LocalBoostDirection", BindingFlags.NonPublic | BindingFlags.Instance);

        public enum FlightType
        {
            Aircraft,   // Horizontal flight
            Helicopter, // Vertical flight
            VTOL,       // Both Horizontal and vertical flight
        }

        private Tank _tank;
        public Tank Tank
        {
            get => _tank;
            internal set => _tank = value;
        }
        private AIECore.TankAIHelper _helper;
        public AIECore.TankAIHelper Helper
        {
            get => _helper;
            internal set => _helper = value;
        }
        private IMovementAICore _AI;
        public IMovementAICore AICore
        {
            get => _AI;
            internal set => _AI = value;
        }
        private Enemy.EnemyMind _mind;
        public Enemy.EnemyMind EnemyMind
        {
            get => _mind;
            internal set => _mind = value;
        }

        public FlightType FlyStyle;             // Dictates the way the AI should fly the Tech
        public List<ModuleBooster> Engines;     // keep track of aircraft propultion
        public List<ModuleAirBrake> Brakes;     // keep tracck of airbrakes
        public List<ModuleWing> Wings;          // keep track of the wings
        public bool NoProps = false;            // Do we have to rely on fuel only?
        public bool SkewedFlightCenter = false; // Are we going to struggle when turning?

        public Vector3 PropBias = Vector3.zero; // Center of thrust (RAW) of all forwards props
        public Vector3 BoostBias = Vector3.zero;// Center of thrust of all boosters, center of boost

        //Manuvering
        public Vector3 AirborneDest = Vector3.zero; // Aircraft-specific destination handling
        public float DestSuccessRad // When we have reached our airborne destination
        {
            get { try { return Helper.MinimumRad; } catch { return 10; } }
        }

        // Forward for aircraft, Upwards for helicopters
        public float AdvisedThrottle = 0;               // Throttle to use when chasing or cruising
        public float MainThrottle = 0;                  // Ideal Throttle to chase after
        public float CurrentThrottle = 0;               // Throttle the craft knows it's going at

        /// <summary> IN m/s !!!</summary>
        public const float Stallspeed = 25;             // The speed of which most wings begin to stall at
        public const float GroundAttackStagingDist = 250;   // Distance to fly (in meters!) before turning back

        //Data Gathering
        public float SlowestPropLerpSpeed = 1;  // Slow action demand based on propeller responsiveness
        public float PropLerpValue = 10;        // aux value used for some engine calculations
        public float AerofoilSluggishness = 1;  // Slow action demand based on aerofoil responsiveness
        public float RollStrength = 1;          // How far to roll 90 degrees
        public int PerformDiveAttack = 0;       // set this to one to launch dive bombing
        public int PerformUTurn = 0;            // set this to one to ignite the multi-stage process
        public Vector3 FlyingChillFactor = Vector3.one * 30; //The higher the values, the less stiff the controls will be

        //Error-Checking
        public int ErrorsInTakeoff = 0;         // If this gets too high, then this tech isn't meant to fly
        public int ErrorsInUTurn = 0;           // If this gets too high, then this tech isn't meant to Immelmann
        public bool DestroyOnTerrain = false;   // Should the aircraft disintegrate on collision with terrain?
        public bool LargeAircraft = false;      // Restrict turning to 45 and no U-Turns
        public bool BankOnly = false;
        public float BoosterThrustBias = 0.5f;
        public float NoStallThreshold = 1.5f;
        public bool ForcePitchUp = false;       // Emergency nose up
        public bool TakeOff = false;            // taking off from ground
        public bool Grounded = false;           // aircraft deemed too damaged to fly
        public bool TargetGrounded = false;     // Are we dealing with a target that is on the ground?
        public bool LowerEngines = false;       // Choppers: Too high! Too high!  Airplanes: Conserve booster fuel

        public void Initiate(Tank tank, AIECore.TankAIHelper thisInst, Enemy.EnemyMind mind = null)
        {
            this.Tank = tank;
            this.Helper = thisInst;
            this.EnemyMind = mind;

            tank.AttachEvent.Subscribe(OnAttach);
            tank.DetachEvent.Subscribe(OnDetach);

            // SETUP
            this.CheckAllFlightBlocks();
            this.CurrentThrottle = 0;

            //Now determine type of craft
            if (mind.IsNull())
            {
                //if (thisInst.isAstrotechAvail && thisInst.DediAI == AIECore.DediAIType.Aviator)
                //    InitiateForVTOL(tank, this);
                if (!this.NoProps)
                {
                    if (Vector3.Dot(this.PropBias, Vector3.up) > 0.6f)
                    {   // Likely a helicopter
                        this.AICore = new HelicopterAICore();
                        this.AICore.Initiate(tank, this);
                    }
                    else if (Vector3.Dot(this.PropBias, Vector3.up) > 0.3f)
                    {   // Likely a VTOL
                        this.AICore = new VtolAICore();
                        this.AICore.Initiate(tank, this);
                    }
                    else
                    {   // Likely an airplane
                        this.AICore = new AirplaneAICore();
                        this.AICore.Initiate(tank, this);
                    }
                }
                else
                {
                    if (Vector3.Dot(this.BoostBias, Vector3.up) > 0.6f)
                    {   // Likely a helicopter
                        this.AICore = new HelicopterAICore();
                        this.AICore.Initiate(tank, this);
                    }
                    else if (Vector3.Dot(this.BoostBias, Vector3.up) > 0.3f)
                    {   // Likely a VTOL
                        this.AICore = new VtolAICore();
                        this.AICore.Initiate(tank, this);
                    }
                    else
                    {   // Likely an airplane
                        this.AICore = new AirplaneAICore();
                        this.AICore.Initiate(tank, this);
                    }
                }
                Debug.Log("TACtical_AI: Tech " + tank.name + " has been assigned Allied aircraft AI with flight mentality " + this.FlyStyle.ToString() + " and flying chill of " + this.FlyingChillFactor);
            }
            else
            {
                if (!this.NoProps)
                {
                    if (Vector3.Dot(this.PropBias, Vector3.up) > 0.6f)
                    {   // Likely a helicopter
                        this.AICore = new HelicopterAICore();
                        this.AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                    }
                    else if (Vector3.Dot(this.PropBias, Vector3.up) > 0.3f)
                    {   // Likely a VTOL
                        this.AICore = new VtolAICore();
                        this.AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                    else
                    {   // Likely an airplane
                        this.AICore = new AirplaneAICore();
                        this.AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                }
                else
                {
                    if (Vector3.Dot(this.BoostBias, Vector3.up) > 0.6f)
                    {   // Likely a helicopter
                        this.AICore = new HelicopterAICore();
                        this.AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                    }
                    else if (Vector3.Dot(this.BoostBias, Vector3.up) > 0.3f)
                    {   // Likely a VTOL
                        this.AICore = new VtolAICore();
                        this.AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                    else
                    {   // Likely an airplane
                        this.AICore = new AirplaneAICore();
                        this.AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                }
                Debug.Log("TACtical_AI: Tech " + tank.name + " has been assigned aircraft AI with " + mind.EvilCommander.ToString() + " mentality " + this.FlyStyle.ToString() + " and flying chill of " + this.FlyingChillFactor);
            }
        }
        public void UpdateEnemyMind(Enemy.EnemyMind mind)
        {
            this.EnemyMind = mind;
        }
        public void Recycle()
        {
            this.AICore = null;
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
        private void CheckEngines()
        {
            float lowestDelta = 100;
            float guzzleLevel = 0;
            int consumeBoosters = 0;
            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            float fanThrust = 0.0f;
            float boosterThrust = 0.0f;

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
                    Vector3 fanDirection = (Vector3) fanDir.GetValue(jet);
                    if (fanDirection.y < -0.5)
                    {
                        fanThrust += jet.force;
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

                    float force = (float)boostGet.GetValue(boost);
                    Vector3 jetDirection = (Vector3) boostDir.GetValue(boost);
                    if (jetDirection.y < -0.5)
                    {
                        boosterThrust += force;
                    }

                    //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                    boostBiasDirection -= Tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection)) * force;
                }
            }

            float totalThrust = (fanThrust + boosterThrust * this.BoosterThrustBias);
            this.BankOnly = totalThrust * totalThrust < (this.NoStallThreshold * this.Tank.rbody.mass * Physics.gravity).sqrMagnitude;

            if (lowestDelta > 10 && boostBiasDirection == Vector3.zero)
            {   //IT HAS NO VALID PROPS OR BOOSTERS!!!!
                //Debug.Log("TACtical AI: Tech " + Tank.name + " DOES NOT HAVE ANY PROPS OR BOOSTERS TO FLY USING!!");
                return;
            }
            if (lowestDelta > 10 && consumeBoosters > 0)
            {
                //Debug.Log("TACtical AI: Tech " + Tank.name + " DOES NOT HAVE ANY PROPS TO FLY USING!!");
                NoProps = true;
            }
            BoostBias = boostBiasDirection;

            boostBiasDirection.Normalize();
            biasDirection.Normalize();
            PropBias = biasDirection;
            if (Mathf.Abs(Vector3.Dot(biasDirection, Vector3.right)) > 0.2f)
            {   //CENTER OF THRUST MAY BE OFF!!!
                SkewedFlightCenter = true;
                //Debug.Log("TACtical AI: Tech " + Tank.name + " reported to have off-centered thrust of a factor of " + Mathf.Abs(Vector3.Dot(biasDirection.normalized, Vector3.right)) + ".  \nAs all props don't have uniform thrust backwards and forwards (in relation to the root cab), the AI may not be able to fly correctly!!!");
            }
            else
                SkewedFlightCenter = false;
            SlowestPropLerpSpeed = lowestDelta;
            PropLerpValue = 10f / SlowestPropLerpSpeed;
        }
        private void CheckWings()
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
        private void CheckAllFlightBlocks()
        {
            // SETUP
            UpdateStatus();
            CheckEngines();
            CheckWings();
        }
        private void UpdateStatus()
        {
            Engines = Tank.blockman.IterateBlockComponents<ModuleBooster>().ToList();
            Brakes = Tank.blockman.IterateBlockComponents<ModuleAirBrake>().ToList();
            Wings = Tank.blockman.IterateBlockComponents<ModuleWing>().ToList();
        }

        //Navigation Director - set airborne positions for the plane to fly to based on lastDestination
        public void DriveDirector()
        {
            AIECore.TankAIHelper thisInst = this.Helper;
            Tank tank = this.Tank;

            if (thisInst == null)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  FIRED FlightDirector WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            this.TestForMayday(thisInst, tank);

            if (thisInst.AIState == 1)
            {
                this.ForcePitchUp = false;
                if (this.Grounded)
                {   //Become a ground vehicle for now
                    if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
                    {
                        return;
                    }
                    //Try fighting the controls to land safely
                    return;
                }
                thisInst.lastDestination = AIEPathing.OffsetFromGroundA(thisInst.lastDestination, thisInst);
                this.AICore.DriveDirector();
            }
            else if (thisInst.AIState == 2) //enemy
            {
                Enemy.EnemyMind mind = tank.GetComponent<Enemy.EnemyMind>();
                if (!this.TargetGrounded)
                    thisInst.lastDestination = AIEPathing.OffsetFromGroundA(thisInst.lastDestination, thisInst);

                this.AICore.DriveDirectorEnemy(mind);
            }
            return;
        }

        //Flight Maintainer - handle the flight between airborne positions
        public void DriveMaintainer(TankControl thisControl)
        {
            //Universal handler
            AIECore.TankAIHelper thisInst = this.Helper;
            Tank tank = this.Tank;

            if (thisInst == null)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  FIRED FlightMaintainer WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }
            thisControl.BoostControlJets = thisInst.BOOST;

            this.AICore.DriveMaintainer(thisControl, thisInst, tank);
            return;
        }

        // Action Updaters
        /// <summary>
        /// Returns true if the craft is likely never going to recover
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <param name="pilot"></param>
        private bool TestForMayday(AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (thisInst.PendingSystemsCheck)
            {
                this.UpdateStatus();
                bool damaged = false;

                if (this.Engines.Count() < 1)
                    damaged = true;
                int wingCount = 0;
                if (this.FlyStyle == FlightType.Helicopter)
                {
                    this.CheckEngines();
                    if (this.NoProps)
                    {
                        if (this.BoostBias.y <= 0.6f)
                            damaged = true;
                    }
                    else
                    {
                        if (this.PropBias.y <= 0.6f)
                            damaged = true;
                    }
                }
                else
                {
                    foreach (ModuleWing wing in this.Wings)
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
        private void OnAttach(TankBlock block, Tank tank)
        {
            if (AIERepair.SystemsCheck(tank))
                this.Grounded = this.TestForMayday(tank.GetComponent<AIECore.TankAIHelper>(), tank);
        }
        private void OnDetach(TankBlock block, Tank tank)
        {   //Disabled for now as some irrelievent warning that doesn't have a label is spamming the logs
            /*
            if (tank.blockman.GetRootBlock().IsNull())
            {
                if (tank.GetComponent<AIEAirborne.AirAssistance>().IsNotNull())
                    tank.GetComponent<AIEAirborne.AirAssistance>().Recycle();
                return;
            }
            var pilot = tank.GetComponent<AIEAirborne.AirAssistance>();
            if (this.IsNull())
                return;
            var mem = tank.GetComponent<AIERepair.DesignMemory>();
            if (mem.IsNull())
                return;
            if (AIERepair.SystemsCheck(tank))
                this.Grounded = TestForMayday(tank.GetComponent<AIECore.TankAIHelper>(), tank, this);
            */
        }
        public void UpdateThrottle(AIECore.TankAIHelper thisInst, TankControl control)
        {
            if (this.NoProps)
            {
                if (this.FlyStyle == FlightType.Aircraft)
                {
                    if (this.MainThrottle > 0.1 && this.Tank.rootBlockTrans.InverseTransformVector(this.Tank.rbody.velocity).z < AIControllerAir.Stallspeed + 5 && !this.Tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = thisInst.BOOST;
                }
                else // VTOL
                {
                    if (this.MainThrottle > 0.1 && this.Tank.rootBlockTrans.InverseTransformVector(this.Tank.rbody.velocity).z < AIControllerAir.Stallspeed + 5 && !this.Tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else if (this.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(this.Tank.boundsCentreWorldNoCheck, AIECore.Extremes(this.Tank.blockBounds.extents) * 2) && !this.Tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = thisInst.BOOST;
                }

                // Still try to move wheels and other things
                if (this.CurrentThrottle + (this.SlowestPropLerpSpeed * Time.deltaTime) < this.MainThrottle)
                {
                    this.CurrentThrottle += this.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (this.CurrentThrottle - (this.SlowestPropLerpSpeed * Time.deltaTime) > this.MainThrottle)
                {
                    this.CurrentThrottle -= this.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    this.CurrentThrottle = this.MainThrottle;
                }
            }
            else
            {
                if (this.CurrentThrottle + (this.SlowestPropLerpSpeed * Time.deltaTime) < this.MainThrottle)
                {
                    this.CurrentThrottle += this.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (this.CurrentThrottle - (this.SlowestPropLerpSpeed * Time.deltaTime) > this.MainThrottle)
                {
                    this.CurrentThrottle -= this.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    this.CurrentThrottle = this.MainThrottle;
                }
                this.CurrentThrottle = Mathf.Clamp(this.CurrentThrottle, -1, 1);
                if (this.FlyStyle == FlightType.Aircraft && this.PropBias.z > 0.8f && CurrentThrottle > 0.8f)
                {   // Some aircraft stall when pitching up - this should help avoid that
                    control.BoostControlProps = true;
                }
                else
                {
                    control.BoostControlProps = false;
                }
            }
        }
    }
}
