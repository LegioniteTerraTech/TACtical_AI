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
            TankAIHelper helper = Mind.AIControl;
            Tank tank = helper.tank;

            EControlOperatorSet direct = helper.GetDirectedControl();

            switch (this.Mind.EvilCommander)
            {
                case EnemyHandling.Wheeled:
                    RWheeled.AttackVroom(helper, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Airplane:
                    RAircraft.AttackWoosh(helper, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Chopper:
                    RChopper.AttackShwa(helper, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Starship:
                    RStarship.AttackZoom(helper, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Naval:
                    RNaval.AttackWhish(helper, tank, Mind, ref direct);
                    break;
                case EnemyHandling.SuicideMissile:
                    // IDK, May make this obsolete and just use plane AI for this instead.
                    RCrashMissile.AttackCrash(helper, tank, Mind, ref direct);
                    break;
                case EnemyHandling.Stationary:
                    RStation.AttackWham(helper, tank, Mind, ref direct);
                    break;
            }
            if (helper.Retreat)
            {
                RCore.GetRetreatLocation(helper, tank, Mind, ref direct);
            }
            helper.SetDirectedControl(direct);
        }
    }
}
