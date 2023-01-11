using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI.AI.Movement.AICores
{
    public enum AISteerAim
    {
        IgnoreAll,
        OnlyImmedeate,
        Path,
        PrecisePath,
    }
    public interface IMovementAICore
    {
        /// <summary>
        /// DriveMaintainer is the most frequently updated out of the AI operations.  
        ///   Use this for matters that must be updated quickly and frequently.
        /// </summary>
        bool DriveMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank);

        void Initiate(Tank tank, IMovementAIController controller);

        /// <summary>
        /// DriveDirector is used for more expensive, less updated operations.
        ///   Pathfinding is also held here.
        /// </summary>
        bool DriveDirector();

        /// <summary>
        /// DriveDirectorRTS is used for RTS Control for AI or the player.  
        ///   Strictly follow a point in world space.
        /// </summary>
        bool DriveDirectorRTS(); // FOR RTS CONTROL

        bool DriveDirectorEnemy(Enemy.EnemyMind mind);

        bool TryAdjustForCombat(bool between); 

        bool TryAdjustForCombatEnemy(Enemy.EnemyMind mind);

        Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset);

    }
}
