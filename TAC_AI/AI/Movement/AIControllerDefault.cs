using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement.AICores;
using UnityEngine;

namespace TAC_AI.AI
{
    public class AIControllerDefault : MonoBehaviour, IMovementAIController
    {
        internal static FieldInfo boostGet = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);

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

        //Manuvering (Post-Pathfinding)
        public Vector3 ProcessedDest = Vector3.zero;// Where land and spaceships coordinate movement

        //Tech Drive Data Gathering
        public Vector3 BoostBias = Vector3.zero;// Center of thrust of all boosters, center of boost
        public float BoosterThrustBias = 0.5f;

        public void DriveDirector()
        {
            if (this.Helper == null)
            {
                string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirector WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            this.Helper.Steer = false;
            this.Helper.DriveDir = EDriveFacing.Forwards;
            if (this.Helper.AIState == AIAlignment.Player)// Allied
            {
                if (this.AICore == null)
                {
                    string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirector WITHOUT ANY SET AICore!!!");
                    return;
                }
                this.AICore.DriveDirector();
            }
            else//ENEMY
            {
                this.AICore.DriveDirectorEnemy(this.EnemyMind);
            }
        }
        public void DriveDirectorRTS()
        {
            if (this.Helper == null)
            {
                string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                DebugTAC_AI.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            this.Helper.Steer = false;
            this.Helper.DriveDir = EDriveFacing.Forwards;
            if (this.Helper.AIState == AIAlignment.Player)// Allied
            {
                if (this.AICore == null)
                {
                    string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                    DebugTAC_AI.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT ANY SET AICore!!!");
                    return;
                }
                this.AICore.DriveDirectorRTS();
            }
            else//ENEMY
            {
                this.AICore.DriveDirectorEnemy(this.EnemyMind);
            }
        }

        public void DriveMaintainer(TankControl thisControl)
        {
            thisControl.m_Movement.m_USE_AVOIDANCE = this.Helper.AvoidStuff;
            this.AICore.DriveMaintainer(thisControl, this.Helper, this.Tank);
        }

        public void Initiate(Tank tank, AIECore.TankAIHelper helper, EnemyMind mind = null)
        {
            this.Tank = tank;
            this.Helper = helper;
            this.EnemyMind = mind;

            this.AICore = new VehicleAICore();
            this.AICore.Initiate(tank, this);

            tank.AttachEvent.Subscribe(OnAttach);
            tank.DetachEvent.Subscribe(OnDetach);
            CheckBoosters();
            DebugTAC_AI.Info("TACtical_AI: Added ground AI for " + Tank.name);
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

            foreach (ModuleBooster module in Tank.blockman.IterateBlockComponents<ModuleBooster>().ToList())
            {
                //Get the slowest spooling one
                List<FanJet> jets = module.transform.GetComponentsInChildren<FanJet>().ToList();
                foreach (FanJet jet in jets)
                {
                    if (jet.spinDelta <= 10)
                    {
                        biasDirection -= Tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForwards) * jet.force;
                        if (jet.spinDelta < lowestDelta)
                            lowestDelta = jet.spinDelta;
                    }
                    //Vector3 fanDirection = (Vector3) fanDir.GetValue(jet);
                    //if (fanDirection.x < -0.5)
                    if (Tank.rootBlockTrans.InverseTransformDirection(jet.EffectorForwards).z < -0.5)
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
                    //Vector3 jetDirection = (Vector3) boostDir.GetValue(boost);
                    //if (jetDirection.x < -0.5)
                    if (Tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection)).z < -0.5)
                    {
                        boosterThrust += force;
                    }

                    //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                    boostBiasDirection -= Tank.rootBlockTrans.InverseTransformDirection(boost.transform.TransformDirection(boost.LocalBoostDirection)) * force;
                }
            }

            //float totalThrust = (fanThrust + boosterThrust * this.BoosterThrustBias);
            BoostBias = boostBiasDirection.normalized;
        }
        public void TryBoost(TankControl TC, bool forwardsOnly = true)
        {
            if (Helper.Obst)
                return; // Prevent thrusting into trees
            if (BoostBias.z > 0.75f && forwardsOnly)
            {
                TC.BoostControlJets = true;
            }
            else
            {
                TC.BoostControlJets = true;
            }
        }


        public void OnMoveWorldOrigin(IntVector3 move)
        {

        }
        public Vector3 GetDestination()
        {
            return ProcessedDest;
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
                Tank.AttachEvent.Unsubscribe(OnAttach);
                Tank.DetachEvent.Unsubscribe(OnDetach);
                //DebugTAC_AI.Log("TACtical_AI: Removed ground AI from " + Tank.name);
                DestroyImmediate(this);
            }
        }
    }
}
