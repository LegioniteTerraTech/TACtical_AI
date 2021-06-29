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
            public const float Stallspeed = 68;

            //Data Gathering
            public float SlowestPropLerpSpeed = 1;
            public float AerofoilSluggishness = 1;
            public float RollStrength = 1;
            public int PerformUTurn = 0;    //set this to one to ignite the multi-stage process
            public Vector3 FlyingChillFactor = Vector3.one * 30; //The higher the values, the less stiff the controls will be

            //Error-Checking
            public float CorrectionThreshold = 5;
            public float AllowedErrorDist = 10;
            public bool Grounded = false;

            public static void Initiate(Tank tank, AIECore.TankAIHelper thisInst, Enemy.RCore.EnemyMind mind = null)
            {
                FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
                var pilot = tank.gameObject.AddComponent<PIDAssistance>();
                pilot.tank = tank;
                tank.AttachEvent.Subscribe(OnAttach);
                tank.DetachEvent.Subscribe(OnDetach);

                // SETUP
                pilot.CheckFlightBlocks();

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
                Vector3 biasDirection = Vector3.zero;
                Vector3 boostBiasDirection = Vector3.zero;
                foreach (ModuleBooster module in Engines)
                {
                    //Get teh slowest spooling one
                    List<FanJet> jets = module.transform.GetComponentsInChildren<FanJet>().ToList();
                    foreach (FanJet jet in jets)
                    {
                        if (lowestDelta <= 10)
                        {
                            biasDirection += tank.rootBlockTrans.InverseTransformDirection(jet.transform.TransformDirection(jet.EffectorForwards));
                            if (jet.spinDelta < lowestDelta)
                                lowestDelta = jet.spinDelta;
                        }
                    }
                    List<BoosterJet> boosts = module.transform.GetComponentsInChildren<BoosterJet>().ToList();
                    foreach (BoosterJet boost in boosts)
                    {
                        if (boost.ConsumesFuel)
                            guzzleLevel += boost.BurnRate;
                        //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                        boostBiasDirection += tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection)) * (float)boostGet.GetValue(boost);
                    }
                }

                if (lowestDelta > 10 && boostBiasDirection == Vector3.zero)
                {   //IT HAS NO VALID PROPS OR BOOSTERS!!!!
                    Debug.Log("TACtical AI: Tech " + tank.name + " DOES NOT HAVE ANY PROPS OR BOOSTERS TO FLY USING!!");
                    return;
                }
                if (lowestDelta > 10 && guzzleLevel > 0)
                {
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
                        if (lowestDelta <= 10)
                        {
                            if (foil.flapTurnSpeed < aerofoilSpeed)
                                aerofoilSpeed = foil.flapTurnSpeed;
                        }
                    }
                }
                AerofoilSluggishness = 45 / aerofoilSpeed;
            }

            public void UpdateStatus()
            {
                Engines = tank.blockman.IterateBlockComponents<ModuleBooster>().ToList();
                Brakes = tank.blockman.IterateBlockComponents<ModuleAirBrake>().ToList();
                Wings = tank.blockman.IterateBlockComponents<ModuleWing>().ToList();
            }
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
        {
            var pilot = tank.GetComponent<PIDAssistance>();
            if (pilot.IsNull())
                return;
            if (AIERepair.SystemsCheck(tank))
                pilot.Grounded = TestForMayday(tank.GetComponent<AIECore.TankAIHelper>(), tank, pilot);
        }

        public static void UpdateThrottle(PIDAssistance pilot)
        {
            if (pilot.NoProps)
            {
                if (pilot.FlyStyle == FlightType.Aircraft)
                {
                    if (pilot.MainThrottle > 0.1 && pilot.tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z < PIDAssistance.Stallspeed)
                        pilot.tank.GetComponent<AIECore.TankAIHelper>().BOOST = true;
                    else
                        pilot.tank.GetComponent<AIECore.TankAIHelper>().BOOST = false;
                }
                else if (pilot.FlyStyle == FlightType.Helicopter)
                {
                    if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.tank.blockBounds.extents) * 2))
                        pilot.tank.GetComponent<AIECore.TankAIHelper>().BOOST = true;
                    else
                        pilot.tank.GetComponent<AIECore.TankAIHelper>().BOOST = false;
                }
                else
                {
                    if (pilot.MainThrottle > 0.1 && pilot.tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z < PIDAssistance.Stallspeed)
                        pilot.tank.GetComponent<AIECore.TankAIHelper>().BOOST = true;
                    else if (pilot.MainThrottle > 0.1 && !AIEPathing.AboveHeightFromGround(pilot.tank.boundsCentreWorldNoCheck, AIECore.Extremes(pilot.tank.blockBounds.extents) * 2))
                        pilot.tank.GetComponent<AIECore.TankAIHelper>().BOOST = true;
                    else
                        pilot.tank.GetComponent<AIECore.TankAIHelper>().BOOST = false;
                }
            }
            else
            { 
                if (pilot.CurrentThrottle < pilot.MainThrottle + pilot.SlowestPropLerpSpeed)
                {
                    pilot.CurrentThrottle += pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else if (pilot.CurrentThrottle > pilot.MainThrottle - pilot.SlowestPropLerpSpeed)
                {
                    pilot.CurrentThrottle -= pilot.SlowestPropLerpSpeed * Time.deltaTime;
                }
                else
                {   //Snap
                    pilot.CurrentThrottle = pilot.MainThrottle;
                }
            }
        }

        //Navigation updater - set positions for the plane to fly to
        public static bool FlightMarshal(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
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

                if (!AIEPathing.AboveHeightFromGround(tank.boundsCentreWorldNoCheck + (tank.rbody.velocity * pilot.AerofoilSluggishness), 25))
                {
                    pilot.AirborneDest += Vector3.up * tank.rbody.velocity.magnitude;
                }
            }
            else if (thisInst.AIState == 2) //enemy
            {
                pilot.AirborneDest = Enemy.RPathfinding.AvoidAssistEnemy(tank, pilot.AirborneDest);
            }    
            return true;
        }

        //
        public static bool PilotTech(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot)
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
            if (pilot.PerformUTurn > 0)
            {   //The Immelmann Turn
                pilot.MainThrottle = 1;
                UpdateThrottle(pilot);
                if (pilot.tank.rootBlockTrans.InverseTransformVector(pilot.tank.rbody.velocity).z < PIDAssistance.Stallspeed - 4)
                {   //ABORT!!!
                    pilot.PerformUTurn = -1;
                }
                if (pilot.PerformUTurn == 1)
                {
                    AngleTowards(thisControl, thisInst, tank, pilot, Vector3.up * 100);
                    if (Vector3.Dot(tank.rootBlockTrans.forward, Vector3.up) > 0.75f)
                        pilot.PerformUTurn = 2;
                }
                else if (pilot.PerformUTurn == 2)
                {
                    AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                    if (Vector3.Dot((pilot.AirborneDest - tank.boundsCentreWorld).normalized, tank.rootBlockTrans.forward) > 0.75f)
                        pilot.PerformUTurn = 3;
                }
                else if (pilot.PerformUTurn == 3)
                {
                    AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);
                    if (Vector3.Dot(tank.rootBlockTrans.up, Vector3.up) > 0.75f)
                        pilot.PerformUTurn = 0;
                }
                return true;
            }
            else if (pilot.PerformUTurn == -1)
            {
                pilot.MainThrottle = 1;
                UpdateThrottle(pilot);
                Vector3 flatR = tank.rootBlockTrans.right;
                flatR.y = 0;
                AngleTowards(thisControl, thisInst, tank, pilot, flatR.normalized * 100);
                if (Vector3.Dot(tank.rootBlockTrans.forward, (pilot.AirborneDest - tank.boundsCentreWorld).normalized) > 0)
                    pilot.PerformUTurn = 0;
                return true;
            }
            UpdateThrottle(pilot);
            AngleTowards(thisControl, thisInst, tank, pilot, pilot.AirborneDest);


            return true;
        }
        public static Vector3 DetermineRoll(Tank tank, PIDAssistance pilot, Vector3 Navi3DDirect)
        {
            Vector3 turnValUp = Quaternion.LookRotation(tank.rootBlockTrans.forward, tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;

            Vector3 direct = Vector3.up;
            if (Navi3DDirect.y > 0.5f)
            { // We roll to aim at target
                direct = Vector3.Cross(tank.rootBlockTrans.forward, (tank.rootBlockTrans.right + (Vector3.up * pilot.RollStrength)).normalized).normalized;
            }
            else if (Navi3DDirect.y < -0.5f)
            { // We roll to aim at target
                direct = Vector3.Cross(tank.rootBlockTrans.forward, (tank.rootBlockTrans.right + (Vector3.down * pilot.RollStrength)).normalized).normalized;
            }
            else if (Navi3DDirect.z < -0.5 && pilot.PerformUTurn == 0)
            { // Preform the Immelmann turn, or better known as the "U-Turn"
                pilot.PerformUTurn = 1;
            }
            else if (pilot.PerformUTurn == 2)
            {
                direct = Vector3.down;
            }
            return direct;
        }

        public static bool AngleTowards(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, PIDAssistance pilot, Vector3 position)
        {
            //AI Steering Rotational
            FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
            TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(thisControl);

            thisInst.Navi3DDirect = (pilot.AirborneDest - tank.trans.position).normalized;

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

            //Turn our work in to process
            control3D.m_State.m_InputRotation = turnVal;

            // DRIVE
            Vector3 DriveVar = Vector3.forward * pilot.CurrentThrottle;

            //Turn our work in to processing
            control3D.m_State.m_InputRotation = turnVal;
            control3D.m_State.m_InputMovement = DriveVar;
            controlGet.SetValue(tank.control, control3D);
            return false;
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
