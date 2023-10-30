using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Movement.AICores;


namespace TAC_AI.AI {
    
    public interface IMovementAIController
    {
        IMovementAICore AICore
        {
            get;
        }

        Tank Tank
        {
            get;
        }

        TankAIHelper Helper
        {
            get;
        }

        Enemy.EnemyMind EnemyMind
        {
            get;
        }

        Vector3 PathPoint { get; }// WHere the Tech is moving towards, not the target's exact location

        void Initiate(Tank tank, TankAIHelper helper, Enemy.EnemyMind mind = null);
        void UpdateEnemyMind(Enemy.EnemyMind mind);

        void DriveDirector(ref EControlCoreSet core);

        void DriveDirectorRTS(ref EControlCoreSet core);

        void DriveMaintainer(TankControl tankControl, ref EControlCoreSet core);

        void OnMoveWorldOrigin(IntVector3 move);
        Vector3 GetDestination();

        void Recycle();
    }
}
