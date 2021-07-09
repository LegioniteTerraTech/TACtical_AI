using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAC_AI.AI.Enemy;
using UnityEngine;

namespace TAC_AI.AI.MovementAI
{
    public class VehicleAI : IMovementAI
    {
        public Vector3 AvoidAssist(Vector3 targetIn, Vector3 predictionOffset)
        {
            throw new NotImplementedException();
        }

        public bool DriveDirector()
        {
            throw new NotImplementedException();
        }

        public bool DriveDirectorEnemy(RCore.EnemyMind mind)
        {
            throw new NotImplementedException();
        }

        public bool DriveTech(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            throw new NotImplementedException();
        }

        public void Initiate(Tank tank, ITechDriver pilot)
        {
            throw new NotImplementedException();
        }

        public bool TryAdjustForCombat()
        {
            throw new NotImplementedException();
        }

        public bool TryAdjustForCombatEnemy(RCore.EnemyMind mind)
        {
            throw new NotImplementedException();
        }
    }
}
