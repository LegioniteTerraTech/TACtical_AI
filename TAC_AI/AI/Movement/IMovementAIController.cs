using System;
using System.Collections.Generic;
using System.Linq;
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
        float GetDrive { get; }// The Forwards drive of the Tech

        void Initiate(Tank tank, TankAIHelper helper, Enemy.EnemyMind mind = null);
        void UpdateEnemyMind(Enemy.EnemyMind mind);

        void DriveDirector(ref EControlCoreSet core);

        void DriveDirectorRTS(ref EControlCoreSet core);

        void DriveMaintainer(ref EControlCoreSet core);

        void OnMoveWorldOrigin(IntVector3 move);
        Vector3 GetDestination();

        void Recycle();
    }
}
