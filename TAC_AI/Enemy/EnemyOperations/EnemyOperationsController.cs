using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI.Enemy;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public class EnemyOperationsController
    {
        private EnemyMind Mind;

        public EnemyOperationsController(EnemyMind Mind)
        {
            this.Mind = Mind;
        }

        public void Execute()
        {
            AIECore.TankAIHelper thisInst = Mind.AIControl;
            Tank tank = thisInst.tank;

            switch (this.Mind.EvilCommander)
            {
                case EnemyHandling.Wheeled:
                    RWheeled.TryAttack(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Airplane:
                    RAircraft.TryFly(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Chopper:
                    RChopper.TryFly(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Starship:
                    RStarship.TryAttack(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Naval:
                    RNaval.TryAttack(thisInst, tank, Mind);
                    break;
                case EnemyHandling.SuicideMissile:
                    // IDK, May make this obsolete and just use plane AI for this instead.
                    RSuicideMissile.RamTillDeath(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Stationary:
                    RGeneral.AimAttack(thisInst, tank, Mind);
                    RStation.HoldPosition(thisInst, tank, Mind);
                    break;
            }
        }
    }
}
