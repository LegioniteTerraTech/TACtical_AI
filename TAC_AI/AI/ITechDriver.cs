using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAC_AI.AI.MovementAI;
using UnityEngine;

namespace TAC_AI.AI
{
    public interface ITechDriver
    {
        IMovementAI AI {
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
    }
}
