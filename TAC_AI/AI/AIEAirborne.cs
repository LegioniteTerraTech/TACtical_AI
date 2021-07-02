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
        public class PIDAssistance : MonoBehaviour
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
            public float DestSuccessRad = 0;                // When we have reached our airborne destination
            public float MainThrottle = 0;                  // Forward for aircraft, Upwards for helicopters
            public float CurrentThrottle = 0;               // 
            public Vector3 SteeringOrigin = Vector3.zero;

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

            public static PIDAssistance Initiate(Tank tank, AIECore.TankAIHelper thisInst, Enemy.RCore.EnemyMind mind = null)
            {
                FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
                var pilot = tank.gameObject.AddComponent<PIDAssistance>();
                pilot.tank = tank;
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
                }
                Debug.Log("TACtical_AI: Tech " + tank.name + " has been assigned aircraft AI with " + mind.EvilCommander.ToString() + " mentality " + pilot.FlyStyle.ToString() + " and flying chill of " + pilot.FlyingChillFactor);
                return pilot;
            }
            public void Recycle()
            {
                tank.AttachEvent.Unsubscribe(OnAttach);
                tank.DetachEvent.Unsubscribe(OnDetach);
                DestroyImmediate(this);
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
                PropLerpValue = 10f / SlowestPropLerpSpeed;
                AerofoilSluggishness = 45 / aerofoilSpeed;
                FlyingChillFactor = Vector3.one * (AerofoilSluggishness);
                RollStrength = Mathf.Clamp(aerofoilSpeed * 2, 1, 2);
            }
            public void UpdateStatus()
            {
                Engines = tank.blockman.IterateBlockComponents<ModuleBooster>().ToList();
                Brakes = tank.blockman.IterateBlockComponents<ModuleAirBrake>().ToList();
                Wings = tank.blockman.IterateBlockComponents<ModuleWing>().ToList();
            }
        }

        //Navigation updater - set airborne positions for the plane to fly to based on lastDestination
        public static bool FlightMarshal(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
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
            if (thisInst.AIState == 1)
            {
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                if (thisInst.ProceedToObjective)
                {   // Fly to target
                    if ((thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = AIEPathing.OffsetFromGround(thisInst.lastDestination + (tank.rootBlockTrans.forward * 100), thisInst);
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
                        pilot.AirborneDest = AIEPathing.OffsetFromGround(pilot.AirborneDest + (-tank.rootBlockTrans.right * 50), thisInst);
                    }
                }

                pilot.AirborneDest = AvoidAssistAir(tank, pilot.AirborneDest, tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness));

                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime), 40))
                {
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).magnitude;
                }
            }
            else if (thisInst.AIState == 2) //enemy
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
                    pilot.AirborneDest = AIEPathing.OffsetFromGround(pilot.AirborneDest + (lFlat * 50), thisInst);
                }
                else if (thisInst.ProceedToObjective)
                {   // Fly to target
                    thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
                    if ((thisInst.lastDestination - tank.boundsCentreWorldNoCheck).magnitude < pilot.DestSuccessRad)
                    {   //We are at target
                        pilot.AirborneDest = AIEPathing.OffsetFromGround(thisInst.lastDestination + (tank.rootBlockTrans.forward * 100), thisInst);
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
                        pilot.AirborneDest.y = AIEPathing.OffsetFromGround(thisInst.lastPlayer.tank.boundsCentreWorldNoCheck + (Vector3.up * (thisInst.GroundOffsetHeight / 5)), thisInst).y;
                    }
                    else 
                    {   //Fly off the screen
                        pilot.AirborneDest = AIEPathing.OffsetFromGround((Vector3.forward * 10000) + (Vector3.up * (thisInst.GroundOffsetHeight / 5)), thisInst);
                    }
                }

                pilot.AirborneDest = Enemy.RPathfinding.AvoidAssistEnemy(tank, pilot.AirborneDest);

                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness * Time.deltaTime), 40))
                {
                    //Debug.Log("TACtical_AI: Tech " + tank.name + "  Avoiding Ground!");
                    pilot.ForcePitchUp = true;
                    pilot.AirborneDest += Vector3.up * (pilot.AirborneDest - tank.boundsCentreWorldNoCheck).magnitude;
                }
            }    
            return true;
        }

        //Flight updater - handle the flight between airborne positions
        public static bool PilotTech(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
        {   //Universal handler
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


        // AIRCRAFT CONTROLERS
        public static bool PilotTechAircraft(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
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
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < PIDAssistance.Stallspeed - 4)
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
        public static Vector3 DetermineRoll(Tank tank, PIDAssistance pilot, Vector3 Navi3DDirect)
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
            else if (Heading.y > 0f && Heading.z < 0.5)
            { // We roll to aim at target
                Vector3 rFlat;
                if (tank.rootBlockTrans.up.y > 0)
                    rFlat = tank.rootBlockTrans.right;
                else
                    rFlat = -tank.rootBlockTrans.right;
                rFlat.y = -pilot.RollStrength;
                direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;
            }
            else if (Heading.y < 0f && Heading.z < 0.5)
            { // We roll to aim at target
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
        public static void AngleTowards(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot, Vector3 position)
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
            control3D.m_State.m_InputRotation = turnVal;
            //Debug.Log("TACtical_AI: Tech " + tank.name + " steering" + turnVal);
            control3D.m_State.m_InputMovement = DriveVar;
            controlGet.SetValue(tank.control, control3D);
            return;
        }


        // HELICOPTER CONTROLLERS
        // WIP.
        public static bool PilotTechChopper(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
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
                pilot.MainThrottle = ModerateUpwardsThrust(tank, pilot, AIEPathing.OffsetFromGround(tank.boundsCentreWorldNoCheck, thisInst, AIECore.Extremes(tank.blockBounds.extents) * 2));
                UpdateThrottle(pilot, thisControl);
                AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest, true);
            }
            else
            {
                pilot.MainThrottle = ModerateUpwardsThrust(tank, pilot, pilot.AirborneDest);
                UpdateThrottle(pilot, thisControl);
                AngleTowardsUp(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
            }

            return true;
        }
        public static void AngleTowardsUp(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot, Vector3 position, bool ForceAccend = false)
        {
            //AI Steering Rotational
            FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(thisControl);
            Vector3 turnVal;
            Vector3 forwardFlat = tank.rootBlockTrans.forward;
            forwardFlat.y = 0;
            if (ForceAccend)
            {
                turnVal = Quaternion.LookRotation(tank.rootBlockTrans.InverseTransformDirection(forwardFlat), tank.rootBlockTrans.InverseTransformDirection(Vector3.one)).eulerAngles;
            }
            else
            {
                DeterminePitchRoll(tank, pilot, position - tank.boundsCentreWorldNoCheck, thisInst);

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
            Vector3 DriveVar = (Vector3.up * pilot.CurrentThrottle) + (tank.rootBlockTrans.InverseTransformVector(position - tank.boundsCentreWorldNoCheck) / pilot.PropLerpValue);

            //Turn our work in to processing
            control3D.m_State.m_InputRotation = turnVal;
            //Debug.Log("TACtical_AI: Tech " + tank.name + " steering" + turnVal);
            control3D.m_State.m_InputMovement = DriveVar;
            controlGet.SetValue(tank.control, control3D);
            return;
        }
        public static void DeterminePitchRoll(Tank tank, PIDAssistance pilot, Vector3 DestinationVector, AIECore.TankAIHelper thisInst, bool PointAtTarget = false)
        {
            Vector3 Heading = (DestinationVector - tank.boundsCentreWorldNoCheck).normalized;
            Vector3 fFlat = tank.rootBlockTrans.InverseTransformDirection(Heading);
            fFlat.y = 0;
            if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck, AIECore.Extremes(tank.blockBounds.extents) * 2))
            {
                thisInst.Navi3DDirect = fFlat;
                thisInst.Navi3DUp = Vector3.up;
                return;
            }
            Vector3 direct;
            Vector3 rFlat;

            if (tank.rootBlockTrans.up.y > 0)
                rFlat = tank.rootBlockTrans.right;
            else
                rFlat = -tank.rootBlockTrans.right;
            rFlat.y = -Mathf.Clamp(tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).x / (10 / pilot.SlowestPropLerpSpeed), -0.75f, 0.75f);
            direct = Vector3.Cross(tank.rootBlockTrans.forward, rFlat.normalized).normalized;

            if (PointAtTarget)
                fFlat.y = tank.rootBlockTrans.InverseTransformDirection(Heading).z;
            else
                fFlat.y = -Mathf.Clamp(tank.rootBlockTrans.InverseTransformDirection(tank.rbody.velocity).z - tank.rootBlockTrans.InverseTransformDirection(Heading).z / (10 / pilot.SlowestPropLerpSpeed), -0.75f, 0.75f);


            thisInst.Navi3DDirect = fFlat;
            thisInst.Navi3DUp = direct;
        }
        public static float ModerateUpwardsThrust(Tank tank, PIDAssistance pilot, Vector3 targetHeight)
        {
            float final = ((targetHeight.y - tank.boundsCentreWorldNoCheck.y) / pilot.PropLerpValue) + (pilot.PropLerpValue / 2);
            if (tank.rbody.velocity.y > final && final < 2)
                final = pilot.CurrentThrottle - (0.1f * Time.deltaTime);
            else if (tank.rbody.velocity.y < -2 && final <= -4)
            {   // try ease fall
                final = -tank.rbody.velocity.y / pilot.PropLerpValue;
            }
            else if (tank.rbody.velocity.y < final && final > -4)
                final = pilot.CurrentThrottle + (0.1f * Time.deltaTime);
           
            return Mathf.Clamp(final, -1, 1);
        }


        // VTOL CONTROLLER
        public static bool PilotTechVTOL(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
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
                AngleTowardsUp(thisControl, thisInst, tank, pilot, tank.boundsCentreWorldNoCheck);
            }
            else
            {   //Fly like plane
                if (pilot.PerformUTurn > 0)
                {   //The Immelmann Turn
                    //Debug.Log("TACtical_AI: Tech " + tank.name + "  U-Turn level " + pilot.PerformUTurn + "  throttle " + pilot.CurrentThrottle);
                    pilot.MainThrottle = 1;
                    UpdateThrottle(pilot, thisControl);
                    if (tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z < PIDAssistance.Stallspeed - 4)
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
        public static bool TestForMayday(AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
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
        public static void OnAttach(TankBlock block, Tank tank)
        {
            var pilot = tank.GetComponent<PIDAssistance>();
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
                if (tank.GetComponent<PIDAssistance>().IsNotNull())
                    tank.GetComponent<PIDAssistance>().Recycle();
                return;
            }
            var pilot = tank.GetComponent<PIDAssistance>();
            if (pilot.IsNull())
                return;
            var mem = tank.GetComponent<AIERepair.DesignMemory>();
            if (mem.IsNull())
                return;
            if (AIERepair.SystemsCheck(tank))
                pilot.Grounded = TestForMayday(tank.GetComponent<AIECore.TankAIHelper>(), tank, pilot);
            */
        }
        public static void UpdateThrottle(PIDAssistance pilot, TankControl control)
        {
            if (pilot.NoProps)
            {
                if (pilot.FlyStyle == FlightType.Aircraft)
                {
                    if (pilot.MainThrottle > 0.1 && pilot.tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z < PIDAssistance.Stallspeed && !pilot.tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = false;
                }
                else if (pilot.FlyStyle == FlightType.Helicopter)
                {
                    if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.tank.blockBounds.extents) * 2) && !pilot.tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = false;
                }
                else
                {
                    if (pilot.MainThrottle > 0.1 && pilot.tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z < PIDAssistance.Stallspeed && !pilot.tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.tank.blockBounds.extents) * 2) && !pilot.tank.beam.IsActive)
                        control.BoostControlJets = true;
                    else
                        control.BoostControlJets = false;
                }

                // Still try to move wheels and other things
                if (pilot.CurrentThrottle + pilot.SlowestPropLerpSpeed * Time.deltaTime < pilot.MainThrottle)
                {
                    pilot.CurrentThrottle += pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (pilot.CurrentThrottle - pilot.SlowestPropLerpSpeed * Time.deltaTime > pilot.MainThrottle)
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
                if (pilot.CurrentThrottle + pilot.SlowestPropLerpSpeed * Time.deltaTime < pilot.MainThrottle)
                {
                    pilot.CurrentThrottle += pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (pilot.CurrentThrottle - pilot.SlowestPropLerpSpeed * Time.deltaTime > pilot.MainThrottle)
                {
                    pilot.CurrentThrottle -= pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    pilot.CurrentThrottle = pilot.MainThrottle;
                }
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
                        if (lastAllyDist < thisInst.lastTechExtents + AIECore.Extremes(lastCloseAlly.blockBounds.extents) + 12)
                        {
                            if (lastAuxVal < thisInst.lastTechExtents + AIECore.Extremes(lastCloseAlly.blockBounds.extents) + 12)
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
                    if (lastAllyDist < thisInst.lastTechExtents + AIECore.Extremes(lastCloseAlly.blockBounds.extents) + 12)
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
                Debug.Log("TACtical_AI: AvoidAssistAir IS NaN!!");
            return targetIn;
        }



        // ENEMY CONTROLLERS
        /*  
            Circle,     // Attack like the AC-130 Gunship, broadside while salvoing
            Grudge,     // Chase and dogfight whatever hit this aircraft last
            Coward,     // Avoid danger
            Bully,      // Attack other aircraft over ground structures.  If inverted, prioritize ground structures over aircraft
            Pesterer,   // Attack like the A-10, unload as much pain as possible in a single flyby
            Spyper,     // Take aim and fire at the best possible moment in our aiming 
        */



        // Initation
        public static void InitiateForAirplane(Tank tank, PIDAssistance pilot)
        {
            pilot.FlyStyle = FlightType.Aircraft;
        }
        public static void InitiateForChopper(Tank tank, PIDAssistance pilot)
        {
            pilot.FlyStyle = FlightType.Helicopter;
        }
        public static void InitiateForVTOL(Tank tank, PIDAssistance pilot)
        {
            pilot.FlyStyle = FlightType.VTOL;
        }
    }
}
