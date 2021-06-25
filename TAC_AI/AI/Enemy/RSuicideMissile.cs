using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public class RSuicideMissile
    {
        public static void RamTillDeath(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (thisInst.lastEnemy.IsNotNull())
            {
                thisInst.BOOST = true;
                thisInst.FullMelee = true;
                thisInst.Attempt3DNavi = true;
                if (thisInst.ActionPause > 1 && thisInst.ActionPause < 7)
                {
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                if (thisInst.ActionPause == 1)
                {
                    thisInst.BOOST = true;
                    thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
                }
                else if (thisInst.ActionPause == 0)
                    thisInst.ActionPause = 20;
                else
                {
                    thisInst.BOOST = true;
                    thisInst.lastDestination = tank.boundsCentreWorldNoCheck + Vector3.up * (100);
                    thisInst.ActionPause--;
                }
            }
        }
    }
}
