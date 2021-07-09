﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI.AI.MovementAI
{
    public interface IMovementAI
    {
        bool DriveTech(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank);

        void Initiate(Tank tank, ITechDriver pilot);

        bool DriveDirector();

        bool DriveDirectorEnemy(Enemy.RCore.EnemyMind mind);

        bool TryAdjustForCombat();

        bool TryAdjustForCombatEnemy(Enemy.RCore.EnemyMind mind);

        Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset);
    }
}
