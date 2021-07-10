using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAC_AI.AI.MovementAI;

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

        AIECore.TankAIHelper Helper
        {
            get;
        }

        Enemy.EnemyMind EnemyMind
        {
            get;
        }

        void Initiate(Tank tank, AIECore.TankAIHelper helper, Enemy.EnemyMind mind = null);

        void DriveDirector();

        void DriveMaintainer(TankControl tankControl);

        void Recycle();
    }
}
