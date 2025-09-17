using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;
using TerraTechETCUtil;
using UnityEngine;

namespace TAC_AI.AI
{
    internal class AIControllerAir : MonoBehaviour, IMovementAIController
    {
        internal static FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
        //internal static FieldInfo boostDir = typeof(BoosterJet).GetField("m_LocalBoostDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        //internal static FieldInfo fanDir = typeof(FanJet).GetField("m_LocalBoostDirection", BindingFlags.NonPublic | BindingFlags.Instance);

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
        private TankAIHelper _helper;
        public TankAIHelper Helper
        {
            get => _helper;
            internal set => _helper = value;
        }
        private IMovementAICore _AI;
        public IMovementAICore AICore => _AI;
        private Enemy.EnemyMind _mind;
        public Enemy.EnemyMind EnemyMind
        {
            get => _mind;
            internal set => _mind = value;
        }

        public FlightType FlyStyle;             // Dictates the way the AI should fly the Tech

        //Manuvering (Post-Pathfinding)
        /// <summary>
        /// Where we try to fly to
        /// </summary>
        public Vector3 PathPoint { get => PathPointSet; }// Aircraft-specific destination handling
        public Vector3 PathPointSet = Vector3.zero; // Aircraft-specific destination handling
        public float DestSuccessRad // When we have reached our airborne destination
        {
            get { try { return Helper.AutoSpacing; } catch { return 10; } }
        }
        public float GetDrive => _AI.GetDrive;


        // Forward for aircraft, Upwards for helicopters
        public float AdvisedThrottle = 0;               // Throttle to use when chasing or cruising
        public float MainThrottle = 0;                  // Ideal Throttle to chase after
        public float CurrentThrottle = 0;               // Throttle the craft knows it's going at


        // Systems Check
        public BlockManager.BlockIterator<ModuleBooster> Engines => Tank.blockman.IterateBlockComponents<ModuleBooster>();     // keep track of aircraft propultion
        public BlockManager.BlockIterator<ModuleAirBrake> Brakes => Tank.blockman.IterateBlockComponents<ModuleAirBrake>();     // keep tracck of airbrakes
        public BlockManager.BlockIterator<ModuleWing> Wings => Tank.blockman.IterateBlockComponents<ModuleWing>();          // keep track of the wings
        public bool NoProps = false;            // Do we have to rely on fuel only?
        public bool SkewedFlightCenter = false; // Are we going to struggle when turning?

        //Tech Flight Data Gathering
        public float lastDataGatherTime = 0;
        public Vector3 PropBias = Vector3.zero; // Center of thrust (RAW) of all forwards props
        public float FwdThrust = 0;
        public float UpThrust = 0;
        public Vector3 BoostBias = Vector3.zero;// Center of thrust of all boosters, center of boost
        public float BoosterThrust = 0;
        /// <summary>
        /// Thrust to Weight Ratio - Minimum % max thrust needed to overcome mass
        /// </summary>
        public float UpTtWRatio = 0;

        /// <summary>
        /// The lower this is, the slower it is
        /// </summary>
        public float SlowestPropLerpSpeed = 1;  // Slow action demand based on propeller responsiveness
        /// <summary>
        /// Increases as SlowestPropLerpSpeed decreases
        /// </summary>
        public float PropLerpValue = 10;        // aux value used for some engine calculations
        public float AerofoilSluggishness = 1;  // Slow action demand based on aerofoil responsiveness
        public float RollStrength = 1;          // How far to roll 90 degrees
        /// <summary>
        /// The higher the values, the less stiff the controls will be
        /// </summary>
        public Vector3 FlyingChillFactor = Vector3.one * 30;

        //Error-Checking
        public int ErrorsInTakeoff = 0;         // If this gets too high, then this tech isn't meant to fly
        public int ErrorsInUTurn = 0;           // If this gets too high, then this tech isn't meant to Immelmann
        public bool LargeAircraft = false;      // Restrict turning to 45 and no U-Turns
        //public float BoosterThrustBias = 0.5f;
        public bool ForcePitchUp = false;       // Emergency nose up
        public bool TakeOff = false;            // taking off from ground
        public bool Grounded = false;           // aircraft deemed too damaged to fly
        public bool TargetGrounded = false;     // Are we dealing with a target that is on the ground?
        public bool LowerEngines = false;       // Choppers: Too high! Too high!  Airplanes: Conserve booster fuel


        public void Initiate(Tank tank, TankAIHelper helper, Enemy.EnemyMind mind = null)
        {
            Tank = tank;
            Helper = helper;
            EnemyMind = mind;

            tank.AttachEvent.Subscribe(OnAttach);
            tank.DetachEvent.Subscribe(OnDetach);

            CurrentThrottle = 0;
            ErrorsInTakeoff = 0;
            ErrorsInUTurn = 0;
            TakeOff = true;
            Grounded = false;
            TargetGrounded = false;

            // SETUP
            CheckAllFlightBlocks();
            CurrentThrottle = 0;


            //Now determine type of craft
            DebugTAC_AI.Info(KickStart.ModID + ": (2) Tech " + Tank.name + " PropBias " + PropBias + ", BoostBias " + BoostBias);
            if (mind.IsNull())
            {
                //if (helper.isAstrotechAvail && helper.DediAI == AIECore.DediAIType.Aviator)
                //    InitiateForVTOL(tank, this);
                if (!NoProps)
                {
                    if (PropBias.y > 0.6f)
                    {   // Likely a helicopter
                        _AI = new HelicopterAICore();
                        AICore.Initiate(tank, this);
                    }
                    else if (PropBias.y > 0.3f)
                    {   // Likely a VTOL
                        _AI = new VtolAICore();
                        AICore.Initiate(tank, this);
                    }
                    else
                    {   // Likely an airplane
                        _AI = new AirplaneAICore();
                        AICore.Initiate(tank, this);
                    }
                }
                else
                {
                    if (BoostBias.y > 0.6f)
                    {   // Likely a helicopter
                        _AI = new HelicopterAICore();
                        AICore.Initiate(tank, this);
                    }
                    else if (BoostBias.y > 0.3f)
                    {   // Likely a VTOL
                        _AI = new VtolAICore();
                        AICore.Initiate(tank, this);
                    }
                    else
                    {   // Likely an airplane
                        _AI = new AirplaneAICore();
                        AICore.Initiate(tank, this);
                    }
                }
                DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + tank.name + " has been assigned Non-NPT aircraft AI with flight mentality " + FlyStyle.ToString() + ", Roll intensity of " + RollStrength + ", Prop lerp of " + PropLerpValue + " and flying chill of " + FlyingChillFactor);
            }
            else
            {
                if (!NoProps)
                {
                    if (PropBias.y > 0.6f)
                    {   // Likely a helicopter
                        _AI = new HelicopterAICore();
                        AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                    }
                    else if (PropBias.y > 0.3f)
                    {   // Likely a VTOL
                        _AI = new VtolAICore();
                        AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                    else
                    {   // Likely an airplane
                        _AI = new AirplaneAICore();
                        AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                }
                else
                {
                    if (BoostBias.y > 0.6f)
                    {   // Likely a helicopter
                        _AI = new HelicopterAICore();
                        AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Chopper;
                    }
                    else if (BoostBias.y > 0.3f)
                    {   // Likely a VTOL
                        _AI = new VtolAICore();
                        AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                    else
                    {   // Likely an airplane
                        _AI = new AirplaneAICore();
                        AICore.Initiate(tank, this);
                        mind.EvilCommander = Enemy.EnemyHandling.Airplane;
                    }
                }
                DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + tank.name + " has been assigned Non-Player aircraft AI with " + mind.EvilCommander.ToString() + " mentality " + FlyStyle.ToString() + ", Roll intensity of " + RollStrength + ", Prop lerp of " + PropLerpValue + " and flying chill of " + FlyingChillFactor);
            }
        }
        public void UpdateEnemyMind(Enemy.EnemyMind mind)
        {
            this.EnemyMind = mind;
        }
        public void Recycle()
        {
            _AI = null;
            if (this.IsNotNull())
            {
                Tank.AttachEvent.Unsubscribe(OnAttach);
                Tank.DetachEvent.Unsubscribe(OnDetach);
                //DebugTAC_AI.Log(KickStart.ModID + ": Removed aircraft AI from " + Tank.name);
                DestroyImmediate(this);
            }
        }
        public void Reset()
        {

        }
        private void CheckEngines(bool firstCheck = false)
        {
            if (!firstCheck && Time.time < lastDataGatherTime)
                return;
            lastDataGatherTime = Time.time + 1f;
            float lowestDelta = 100;
            float guzzleLevel = 0;
            int consumeBoosters = 0;
            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            FwdThrust = 0f;
            UpThrust = 0f;
            float boosterThrust = 0f;

            foreach (ModuleBooster module in Engines)
            {
                //Get the slowest spooling one
                foreach (FanJet jet in module.transform.GetComponentsInChildren<FanJet>(true))
                {
                    float thrust = (float)RawTechBase.thrustRate.GetValue(jet);
                    float thrustRev = (float)RawTechBase.fanThrustRateRev.GetValue(jet);
                    Vector3 localFwd = jet.LocalThrustDirection;
                    Vector3 rawVec = localFwd * thrust;
                    if (localFwd.z < 0)
                        FwdThrust += Mathf.Max(localFwd.z * thrustRev, 0);
                    else
                        FwdThrust += Mathf.Max(localFwd.z * thrust, 0);
                    if (localFwd.y < 0)
                        UpThrust += Mathf.Max(localFwd.y * thrustRev, 0);
                    else
                        UpThrust += Mathf.Max(localFwd.y * thrust, 0);
                    biasDirection += new Vector3(Mathf.Abs(rawVec.x), Mathf.Abs(rawVec.y), Mathf.Abs(rawVec.z));
                    float spin = (float)RawTechBase.spinDat.GetValue(jet);
                    if (spin < lowestDelta)
                        lowestDelta = spin;
                }
                foreach (BoosterJet boost in module.transform.GetComponentsInChildren<BoosterJet>(true))
                {
                    Vector3 localFwd = -boost.LocalThrustDirection; // Booster force vector is negative
                    float force = (float)boostGet.GetValue(boost);
                    if (boost.ConsumesFuel)
                    {
                        consumeBoosters++;
                        guzzleLevel += boost.BurnRate;

                        if (localFwd.z > 0) // Booster force vector is negative
                            boosterThrust += Mathf.Max(localFwd.z * force, 0);
                        //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                        boostBiasDirection += localFwd * force;
                    }
                    else
                    {
                        Vector3 rawVec = localFwd * force;
                        if (localFwd.z > 0)
                            FwdThrust += Mathf.Max(rawVec.z, 0);
                        if (localFwd.y > 0)
                            UpThrust += Mathf.Max(rawVec.y, 0);

                        // Steering hovers do not count for biasDirection
                        //biasDirection += new Vector3(Mathf.Abs(rawVec.x), Mathf.Abs(rawVec.y), Mathf.Abs(rawVec.z));
                    }
                }
            }

            // this assumes IDEAL, which isn't always the case.  We have to compensate later on!
            float GravityForce = Tank.rbody.mass * Tank.GetGravityScale() * TankAIManager.GravMagnitude;
            UpTtWRatio = UpThrust / GravityForce;


            if (FwdThrust == 0 && UpThrust == 0)
            {
                NoProps = true;
                if (boostBiasDirection == Vector3.zero)
                {   //IT HAS NO VALID PROPS OR BOOSTERS!!!!
                    if (firstCheck)
                        DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + Tank.name + " DOES NOT HAVE ANY PROPS OR BOOSTERS TO FLY USING!!");
                }
                else if (firstCheck)
                    DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + Tank.name + " DOES NOT HAVE ANY PROPS TO FLY USING!!");
            }
            BoostBias = boostBiasDirection.normalized;

            biasDirection.Normalize();
            PropBias = biasDirection;
            if (firstCheck)
                DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + Tank.name + " PropBias " + PropBias + ", BoostBias " + BoostBias);
            if (Mathf.Abs(Vector3.Dot(PropBias, Vector3.right)) > 0.2f)
            {   //CENTER OF THRUST MAY BE OFF!!!
                SkewedFlightCenter = true;
                if (firstCheck)
                    DebugTAC_AI.LogAISetup(KickStart.ModID + ": Tech " + Tank.name + " reported to have off-centered thrust of a factor of " + Mathf.Abs(Vector3.Dot(biasDirection.normalized, Vector3.right)) + ".  \nAs all props don't have uniform thrust backwards and forwards (in relation to the root cab), the AI may not be able to fly correctly!!!");
            }
            else
                SkewedFlightCenter = false;
            SlowestPropLerpSpeed = lowestDelta;
            PropLerpValue = AIGlobals.PropLerpStrictness / SlowestPropLerpSpeed;
        }
        /// <summary>
        /// MUST come AFTER CheckEngines()!
        /// </summary>
        private void CheckWings()
        {
            float aerofoilSpeed = 100;
            foreach (ModuleWing module in Wings)
            {
                //Get teh slowest spooling one
                foreach (ModuleWing.Aerofoil foil in module.m_Aerofoils)
                {
                    if (foil.flapTurnSpeed > 0.01f)
                    {
                        if (foil.flapTurnSpeed < aerofoilSpeed)
                        {
                            aerofoilSpeed = foil.flapTurnSpeed;
                        }
                    }
                }
            }
            if (Helper.lastTechExtents >= AIGlobals.LargeAircraftSize)
            {
                DebugTAC_AI.LogAISetup("CheckWings(): LARGE AIRCRAFT " + Helper.lastTechExtents + " ramming: " + Helper.FullMelee);
                LargeAircraft = true;
            }
            else
            {
                DebugTAC_AI.LogAISetup("CheckWings(): Normal aircraft " + Helper.lastTechExtents + " ramming: " + Helper.FullMelee);
                LargeAircraft = false;
            }

            AerofoilSluggishness = AIGlobals.AerofoilSluggishnessBaseValue / aerofoilSpeed;
            if (FlyStyle == FlightType.Helicopter)
            {
                //FlyingChillFactor is calculated and set in HelicopterAICore.Initiate()
            }
            else
            {
                if (LargeAircraft)
                {
                    FlyingChillFactor = Vector3.one * AerofoilSluggishness * AIGlobals.LargeAircraftChillFactorMulti;
                    FlyingChillFactor.y = 5;    // need accuraccy for large aircraft bombing runs
                }
                else
                {
                    FlyingChillFactor = Vector3.one * AerofoilSluggishness * AIGlobals.AircraftChillFactorMulti;
                    FlyingChillFactor.y = 0.75f;  // Yaw isn't normally too strong on aircraft so we give it a boost.
                }
            }

            RollStrength = Mathf.Clamp(aerofoilSpeed * 2, 0.5f, 2);
        }
        private void CheckAllFlightBlocks()
        {
            // SETUP
            CheckEngines(true);
            CheckWings();
        }

        //Navigation Director - set airborne positions for the plane to fly to based on lastDestination
        public void DriveDirector(ref EControlCoreSet core)
        {
            TankAIHelper helper = Helper;
            Tank tank = Tank;

            if (helper == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  FIRED FlightDirector WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            this.TestForMayday(helper, tank);

            if (helper.AIAlign == AIAlignment.Player)
            {
                this.ForcePitchUp = false;
                if (this.Grounded)
                {   //Become a ground vehicle for now
                    if (!AIEPathing.AboveHeightFromGroundTech(helper, helper.lastTechExtents * 2))
                    {
                        return;
                    }
                    //Try fighting the controls to land safely
                    return;
                }
                if (!this.TargetGrounded)
                    PathPointSet = AIEPathing.OffsetFromGroundA(helper.lastDestinationCore, helper);
                this.AICore.DriveDirector(ref core);
            }
            else if (helper.AIAlign == AIAlignment.NonPlayer) //enemy
            {
                if (!this.TargetGrounded)
                    PathPointSet = AIEPathing.OffsetFromGroundA(helper.lastDestinationCore, helper);

                this.AICore.DriveDirectorEnemy(EnemyMind, ref core);
            }
            return;
        }
        public void DriveDirectorRTS(ref EControlCoreSet core)
        {
            TankAIHelper helper = Helper;
            Tank tank = Tank;

            if (helper == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  FIRED FlightDirectorRTS WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            TestForMayday(helper, tank);

            if (helper.AIAlign == AIAlignment.Player)
            {
                this.ForcePitchUp = false;
                if (this.Grounded)
                {   //Become a ground vehicle for now
                    if (!AIEPathing.AboveHeightFromGroundTech(helper, helper.lastTechExtents * 2))
                    {
                        return;
                    }
                    //Try fighting the controls to land safely
                    return;
                }
                core.lastDestination = AIEPathing.OffsetFromGroundA(helper.RTSDestination, helper);
                this.AICore.DriveDirectorRTS(ref core);
            }
            else if (helper.AIAlign == AIAlignment.NonPlayer) //enemy
            {
                //if (!this.TargetGrounded)
                //    core.lastDestination = AIEPathing.OffsetFromGroundA(helper.lastDestination, helper);

                this.AICore.DriveDirectorEnemy(EnemyMind, ref core);
            }
            return;
        }

        //Flight Maintainer - handle the flight between airborne positions
        public void DriveMaintainer(ref EControlCoreSet core)
        {
            //Universal handler
            TankAIHelper helper = this.Helper;
            Tank tank = this.Tank;

            if (helper == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  FIRED FlightMaintainer WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }
            if (Tank.beam.IsActive)
            {
                KillAllControl(Helper);
                return;
            }

            if (helper.FullBoost)
                helper.MaxBoost();

            this.AICore.DriveMaintainer(helper, tank, ref core);
            /*
            if (!AIEPathing.IsUnderMaxAltPlayer(PathPoint.y))
                DebugTAC_AI.Log(KickStart.ModID + ":!!! - Tech " + tank.name + " has PathPoint [" + PathPoint.y + "] above max alt player [" + 
                    (AIGlobals.AirWanderMaxHeight + Singleton.playerPos.y) + "]");
            */
            return;
        }


        public void OnMoveWorldOrigin(IntVector3 move)
        {
            PathPointSet += move;
        }
        public Vector3 GetDestination()
        {
            return Helper.lastDestinationCore;
        }

        // Action Updaters
        /// <summary>
        /// Returns true if the craft is likely never going to recover
        /// </summary>
        /// <param name="thisControl"></param>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /// <param name="pilot"></param>
        private bool TestForMayday(TankAIHelper helper, Tank tank)
        {
            if (helper.PendingDamageCheck)
            {
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

                if (!AIERepair.CanRepairNow(tank))
                {
                    //if (!Grounded)
                    //    DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " has been damaged too badly with no parts to repair with");
                    return false;
                }
                if (damaged && !Grounded)
                    DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is missing too many parts and has been deemed incapable of flight!");

                return damaged;
            }
            return false;
        }
        private void OnAttach(TankBlock block, Tank tank)
        {
            if (AIERepair.SystemsCheck(tank))
                Grounded = TestForMayday(tank.GetHelperInsured(), tank);
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
                this.Grounded = TestForMayday(tank.GetHelperInsured(), tank, this);
            */
        }
        public void UpdateThrottle(TankAIHelper helper)
        {
            bool boostJets = false;
            bool boostProps = false;
            if (NoProps)
            {
                if (FlyStyle == FlightType.Aircraft)
                {
                    if (MainThrottle > 0.1f && Helper.LocalSafeVelocity.z < AIGlobals.AirStallSpeed + 5 && !Tank.beam.IsActive)
                        boostJets = true;
                    else
                        boostJets = helper.FullBoost;
                }
                else // VTOL
                {
                    if (MainThrottle > 0.1f && Helper.LocalSafeVelocity.z < AIGlobals.AirStallSpeed + 5 && !Tank.beam.IsActive)
                        boostJets = true;
                    else if (MainThrottle > 0.1f && !AIEPathing.AboveHeightFromGroundTech(helper, Helper.lastTechExtents * 2) && !Tank.beam.IsActive)
                        boostJets = true;
                    else
                        boostJets = helper.FullBoost;
                }

                // Still try to move wheels and other things
                if (CurrentThrottle + (SlowestPropLerpSpeed * Time.deltaTime) < MainThrottle)
                {
                    CurrentThrottle += SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (CurrentThrottle - (SlowestPropLerpSpeed * Time.deltaTime) > MainThrottle)
                {
                    CurrentThrottle -= SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    CurrentThrottle = MainThrottle;
                }
            }
            else
            {
                if (CurrentThrottle + (SlowestPropLerpSpeed * Time.deltaTime) < MainThrottle)
                {
                    CurrentThrottle += SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (CurrentThrottle - (SlowestPropLerpSpeed * Time.deltaTime) > MainThrottle)
                {
                    CurrentThrottle -= SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    CurrentThrottle = MainThrottle;
                }
                if (FlyStyle == FlightType.Aircraft)
                {   // Some aircraft stall when pitching up - this should help avoid that
                    if (CurrentThrottle > 1f)
                    {
                        boostProps = true;
                    }
                    else
                    {
                        boostProps = false;
                    }
                }
            }
            CurrentThrottle = Mathf.Clamp(CurrentThrottle, -1, 1);
            helper.ProcessControl(Vector3.zero, Vector3.zero, Vector3.zero, boostProps, boostJets);
        }
        public void KillAllControl(TankAIHelper helper)
        {
            helper.ProcessControl(Vector3.zero, Vector3.zero, Vector3.zero, false, false);
        }

    }
}
