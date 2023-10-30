using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI.Enemy;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    internal class EnemyOperationsController
    {
        private EnemyMind Mind;

        public EnemyOperationsController(EnemyMind Mind)
        {
            this.Mind = Mind;
        }

        public void Execute()
        {
            TankAIHelper thisInst = Mind.AIControl;
            Tank tank = thisInst.tank;

            EControlOperatorSet direct = thisInst.GetDirectedControl();

            switch (this.Mind.EvilCommander)
            {
                case EnemyHandling.Wheeled:
                    RWheeled.AttackVroom(thisInst, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Airplane:
                    RAircraft.AttackWoosh(thisInst, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Chopper:
                    RChopper.AttackShwa(thisInst, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Starship:
                    RStarship.AttackZoom(thisInst, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Naval:
                    RNaval.AttackWhish(thisInst, tank, Mind, ref direct);
                    break;
                case EnemyHandling.SuicideMissile:
                    // IDK, May make this obsolete and just use plane AI for this instead.
                    RCrashMissile.AttackCrash(thisInst, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Stationary:
                    RStation.AttackWham(thisInst, tank, Mind, ref direct);
                    break;
            }
            if (thisInst.Retreat)
            {
                RCore.GetRetreatLocation(thisInst, tank, Mind, ref direct);
            }
            thisInst.SetDirectedControl(direct);
        }
    }
}
