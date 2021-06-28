using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy
{
    public static class RBolts
    {
        public static void ManageBolts(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (mind.CommanderBolts == EnemyBolts.Default)
            {
                tank.control.DetonateExplosiveBolt();
            }
            else if (mind.CommanderBolts == EnemyBolts.AtFullOnAggro)
            {
                //DO NOT CALL THIS WITHOUT EnemyMemory!!!
                if (!AIERepair.SystemsCheck(tank, mind.TechMemor) && thisInst.lastEnemy.IsNotNull())
                    tank.control.DetonateExplosiveBolt();
            }
        }
    }
}
