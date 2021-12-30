using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    public interface IMovementAICore
    {
        bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank);

        void Initiate(Tank tank, IMovementAIController controller);

        bool DriveDirector();

        bool DriveDirectorRTS(); // FOR RTS CONTROL

        bool DriveDirectorEnemy(Enemy.EnemyMind mind);

        bool TryAdjustForCombat();

        bool TryAdjustForCombatEnemy(Enemy.EnemyMind mind);

        Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset);
    }
}
