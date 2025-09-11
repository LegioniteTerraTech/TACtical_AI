using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;
using UnityEngine;
using TerraTechETCUtil;
#if DEBUG
using System.Text;
#endif

namespace TAC_AI.AI
{
    internal class AIControllerDefault : MonoBehaviour, IMovementAIController, IPathfindable
    {
        internal static FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);

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

        //Manuvering (Post-Pathfinding)
        /// <summary> The point where the AI is driving towards.  This is NOT the destination! </summary>
        public Vector3 PathPoint { get => PathPointMain.ScenePosition; }// Where land and spaceships coordinate movement
        /// <summary> The point where the AI is driving towards.  This is NOT the destination! </summary>
        private WorldPosition PathPointMain = WorldPosition.FromScenePosition(Vector3.zero);// Where land and spaceships coordinate movement
        public Vector3 PathPointSet
        {
            set
            {
                if (value.IsNaN() || float.IsInfinity(value.x) || float.IsInfinity(value.z))
                {
                    DebugTAC_AI.Assert("AIControllerDefault.PathPointSet - lastDestination was NaN or infinity.  Defaulting to own position");
                    PathPointMain = WorldPosition.FromScenePosition(Tank.boundsCentreWorldNoCheck);
                }
                /*
                if (value.IsNaN())
                    DebugTAC_AI.Exception("AIControllerDefault.PathPointSet - lastDestination was NaN!");
                if (float.IsInfinity(value.x) || float.IsInfinity(value.z))
                    DebugTAC_AI.Exception("AIControllerDefault.PathPointSet - lastDestination was Inf!");
                */
                PathPointMain = WorldPosition.FromScenePosition(value);
            }
        }// Where land and spaceships coordinate movement
        public float GetDrive => _AI.GetDrive;

        //Tech Drive Data Gathering
        public Vector3 BoostBias = Vector3.zero;// Center of thrust of all boosters, center of boost
        //public float BoosterThrustBias = 0.5f;

        //AI Pathfinding
        public bool AutoPathfind { get; set; } = false;
        public bool Do3DPathing => _helper.Attempt3DNavi;
        public AIEAutoPather Pathfinder { get; set; }
        public WaterPathing WaterPathing { get; set; }
        public float PathingPrecision { get; set; } = 10;
        public byte MaxPathDifficulty { get; set; } = AIEAutoPather.DefaultMaxDifficulty;
        public readonly Queue<WorldPosition> PathPlanned = new Queue<WorldPosition>();
        public WorldPosition TargetDestination;

        public Vector3 CurrentPosition()
        {
            return Tank.boundsCentreWorldNoCheck;
        }
        public Vector3 GetTargetDestination()
        {
            return TargetDestination.ScenePosition;
        }
        public bool IsRunningLowOnPathPoints()
        {
            return PathPlanned.Count < 3;
        }
#if DEBUG
        private static StringBuilder SB = new StringBuilder();
#endif
        public void OnPartialPathfinding(List<WorldPosition> pos)
        {
            if (DebugTAC_AI.NoLogPathing)
            {
                if (pos.Count == 0)
                    return;
                foreach (var item in pos)
                {
                    PathPlanned.Enqueue(item);
                }
            }
            else
            {
                if (pos.Count == 0)
                {
                    DebugTAC_AI.Log(Tank.name + ": OnPartialPathfinding - Path - NONE");
                    return;
                }
#if DEBUG
                int num = 0;
                SB.Clear();
                foreach (var item in pos)
                {
                    PathPlanned.Enqueue(item);
                    SB.Append(" > " + item.ScenePosition.ToString());
                    //AIGlobals.PopupNeutralInfo(num + " | " + item.GameWorldPosition.ToString(), item);
                    num++;
                }
                DebugTAC_AI.Log(Tank.name + ": OnPartialPathfinding - Path -" + SB.ToString());
                SB.Clear();
#endif
            }
        }
        public void OnFinishedPathfinding(List<WorldPosition> pos)
        {
            if (pos == null)
            {
                PathPlanned.Clear();
                return; // Clearing 
            }
            if (DebugTAC_AI.NoLogPathing)
            {
                if (pos.Count == 0)
                    return;
                foreach (var item in pos)
                {
                    PathPlanned.Enqueue(item);
                }
            }
            else
            {
                if (pos.Count == 0)
                {
                    DebugTAC_AI.Log(Tank.name + ": OnFinishedPathfinding - Finished AutoPathing with " + PathPlanned.Count + " waypoints to follow.");
                    DebugTAC_AI.Log(Tank.name + ": OnFinishedPathfinding - Path - NONE");
                    //throw new Exception("OnFinishedPathfinding expects at least one pathing WorldPosition in pos, but received none!");
                    return;
                }
#if DEBUG
                int num = 0;
                SB.Clear();
                foreach (var item in pos)
                {
                    PathPlanned.Enqueue(item);
                    SB.Append(" > " + item.ScenePosition.ToString());
                    //AIGlobals.PopupNeutralInfo(num + " | " + item.GameWorldPosition.ToString(), item);
                    num++;
                }
                DebugTAC_AI.Log(Tank.name + ": OnFinishedPathfinding - Finished AutoPathing with " + PathPlanned.Count + " waypoints to follow.");
                DebugTAC_AI.Log(Tank.name + ": OnFinishedPathfinding - Path -" + SB.ToString());
                SB.Clear();
#endif
            }
        }


        public void DriveDirector(ref EControlCoreSet core)
        {
            if (this.Helper == null)
            {
                string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirector WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            if (this.Helper.AIAlign == AIAlignment.Player)// Allied
            {
                if (this.AICore == null)
                {
                    string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Log(KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirector WITHOUT ANY SET AICore!!!");
                    return;
                }
                this.AICore.DriveDirector(ref core);
            }
            else//ENEMY
            {
                this.AICore.DriveDirectorEnemy(this.EnemyMind, ref core);
            }
        }
        public void DriveDirectorRTS(ref EControlCoreSet core)
        {
            if (this.Helper == null)
            {
                string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }


            if (this.Helper.AIAlign == AIAlignment.Player)// Allied
            {
                if (this.AICore == null)
                {
                    string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Log(KickStart.ModID + ": AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT ANY SET AICore!!!");
                    return;
                }
                this.AICore.DriveDirectorRTS(ref core);
            }
            else//ENEMY
            {
                this.AICore.DriveDirectorEnemy(this.EnemyMind, ref core);
            }
        }

        public void DriveMaintainer(ref EControlCoreSet core)
        {
            AICore.DriveMaintainer(Helper, Tank, ref core);
        }

        public void Initiate(Tank tank, TankAIHelper helper, EnemyMind mind = null)
        {
            this.Tank = tank;
            this.Helper = helper;
            this.EnemyMind = mind;

            if (mind.IsNull())
            {
                //if (helper.isAstrotechAvail && helper.DediAI == AIECore.DediAIType.Aviator)
                //    InitiateForVTOL(tank, this);
                switch (helper.DriverType)
                {
                    case AIDriverType.AutoSet:
                        //AICore = new VehicleAICore();
                        AICore = new LandAICore();
                        break;
                    case AIDriverType.Tank:
                        AICore = new LandAICore();
                        break;
                    case AIDriverType.Sailor:
                        AICore = new SeaAICore();
                        break;
                    case AIDriverType.Astronaut:
                        AICore = new SpaceAICore();
                        break;
                    case AIDriverType.Stationary: // Fallback when unanchored stationary
                        AICore = new SpaceAICore();
                        break;
                    default:
                        throw new Exception("Invalid control type for Non-NPT Vehicle " + helper.DriverType.ToString());
                }
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " has been assigned Vehicle AI with " + helper.DriverType.ToString() + ".");
            }
            else
            {
                switch (mind.EvilCommander)
                {
                    case EnemyHandling.Wheeled:
                        AICore = new LandAICore();
                        break;
                    case EnemyHandling.Starship:
                        AICore = new SpaceAICore();
                        break;
                    case EnemyHandling.Naval:
                        AICore = new SeaAICore();
                        break;
                    case EnemyHandling.SuicideMissile:
                        AICore = new SpaceAICore();
                        break;
                    case EnemyHandling.Stationary: // Fallback when unanchored stationary
                        AICore = new SpaceAICore();
                        break;
                    default:
                        throw new Exception("Invalid control type for NPT Vehicle " + mind.EvilCommander.ToString());
                }
                //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " has been assigned Non-Player Vehicle AI with " + mind.EvilCommander.ToString() + ".");
            }
            AICore.Initiate(tank, this);

            tank.AttachEvent.Subscribe(OnAttach);
            tank.DetachEvent.Subscribe(OnDetach);
            CheckBoosters();
            DebugTAC_AI.LogAISetup(KickStart.ModID + ": Added ground AI for " + Tank.name);
        }
        private void CheckBoosters()
        {
            float lowestDelta = 100;
            float guzzleLevel = 0;
            int consumeBoosters = 0;
            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            float fanThrust = 0.0f;
            float boosterThrust = 0.0f;

            foreach (ModuleBooster module in Tank.blockman.IterateBlockComponents<ModuleBooster>())
            {
                //Get the slowest spooling one
                foreach (FanJet jet in module.transform.GetComponentsInChildren<FanJet>())
                {
                    float thrust = (float)RawTechBase.thrustRate.GetValue(jet);
                    float spin = (float)RawTechBase.spinDat.GetValue(jet);
                    if (spin <= 10)
                    {
                        biasDirection -= Tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForward) * thrust;
                        if (spin < lowestDelta)
                            lowestDelta = spin;
                    }
                    //Vector3 fanDirection = (Vector3) fanDir.GetValue(jet);
                    //if (fanDirection.x < -0.5)
                    if (Tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForward).z < -0.5)
                    {
                        fanThrust += thrust;
                    }
                }
                foreach (BoosterJet boost in module.transform.GetComponentsInChildren<BoosterJet>())
                {
                    if (boost.ConsumesFuel)
                    {
                        consumeBoosters++;
                        guzzleLevel += boost.BurnRate;
                    }

                    float force = (float)boostGet.GetValue(boost);
                    //Vector3 jetDirection = (Vector3) boostDir.GetValue(boost);
                    //if (jetDirection.x < -0.5)
                    if (Tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalThrustDirection)).z < -0.5)
                    {
                        boosterThrust += force;
                    }

                    //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                    boostBiasDirection -= Tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalThrustDirection)) * force;
                }
            }

            //float totalThrust = (fanThrust + boosterThrust * this.BoosterThrustBias);
            BoostBias = boostBiasDirection.normalized;
        }
        public void TryBoost(bool forwardsOnly = true)
        {
            if (Helper.Obst)
                return; // Prevent thrusting into trees
            if (forwardsOnly)
            {
                if (BoostBias.z > 0.75f)
                    Helper.MaxBoost();
            }
            else
            {
                Helper.MaxBoost();
            }
        }
        public void TryBoost(Vector3 headingLocalCab)
        {
            if (Helper.Obst)
                return; // Prevent thrusting into trees
            if (Vector3.Dot(BoostBias, headingLocalCab) > 0.75f)
                Helper.MaxBoost();
        }


        public void OnMoveWorldOrigin(IntVector3 move)
        {

        }
        public Vector3 GetDestination()
        {
            return Helper.lastDestinationCore;
        }

        public void UpdateEnemyMind(EnemyMind mind)
        {
            this.EnemyMind = mind;
        }

        public void OnAttach(TankBlock block, Tank tank)
        {
        }
        public void OnDetach(TankBlock block, Tank tank)
        {
        }

        public void Recycle()
        {
            this.AICore = null;
            if (this.IsNotNull())
            {
                this.SetAutoPathfinding(false);
                Tank.AttachEvent.Unsubscribe(OnAttach);
                Tank.DetachEvent.Unsubscribe(OnDetach);
                //DebugTAC_AI.Log(KickStart.ModID + ": Removed ground AI from " + Tank.name);
                DestroyImmediate(this);
            }
        }
    }
}
