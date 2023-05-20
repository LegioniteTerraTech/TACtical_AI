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
        /// <summary>
        /// DriveMaintainer is the most frequently updated out of the AI operations.  
        ///   Use this for matters that must be updated quickly and frequently.
        /// </summary>
        bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank, ref EControlCoreSet core);

        void Initiate(Tank tank, IMovementAIController controller);

        /// <summary>
        /// DriveDirector is used for more expensive, less updated operations.
        ///   Pathfinding is also held here.
        /// </summary>
        bool DriveDirector(ref EControlCoreSet core);

        /// <summary>
        /// DriveDirectorRTS is used for RTS Control for AI or the player.  
        ///   Strictly follow a point in world space.
        /// </summary>
        bool DriveDirectorRTS(ref EControlCoreSet core); // FOR RTS CONTROL

        bool DriveDirectorEnemy(Enemy.EnemyMind mind, ref EControlCoreSet core);

        bool TryAdjustForCombat(bool between, ref Vector3 pos, ref EControlCoreSet core); 

        bool TryAdjustForCombatEnemy(Enemy.EnemyMind mind, ref Vector3 pos, ref EControlCoreSet core);

        Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset);

    }
}
