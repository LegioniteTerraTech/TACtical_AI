using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement.AICores;
using UnityEngine;

namespace TAC_AI.AI
{
    public class AIControllerDefault : MonoBehaviour, IMovementAIController
    {
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

        public void DriveDirector()
        {
            if (this.Helper == null)
            {
                string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                Debug.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirector WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            this.Helper.Steer = false;
            this.Helper.DriveDir = EDriveType.Forwards;
            if (this.Helper.AIState == 1)// Allied
            {
                if (this.AICore == null)
                {
                    string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                    Debug.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirector WITHOUT ANY SET AICore!!!");
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
                Debug.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT THE REQUIRED TankAIHelper MODULE!!!");
                return;
            }

            this.Helper.Steer = false;
            this.Helper.DriveDir = EDriveType.Forwards;
            if (this.Helper.AIState == 1)// Allied
            {
                if (this.AICore == null)
                {
                    string tankName = this.Tank.IsNotNull() ? this.Tank.name : "UNKNOWN_TANK";
                    Debug.Log("TACtical_AI: AI " + tankName + ":  FIRED DriveDirectorRTS WITHOUT ANY SET AICore!!!");
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
            Debug.Log("TACtical_AI: Added ground AI from " + Tank.name);
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
                //Debug.Log("TACtical_AI: Removed ground AI from " + Tank.name);
                DestroyImmediate(this);
            }
        }
    }
}
