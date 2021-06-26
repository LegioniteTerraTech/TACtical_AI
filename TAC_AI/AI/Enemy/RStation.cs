using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.AI.Enemy
{
    public class RStation
    {
        public static void HoldPosition(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst);

            thisInst.lastDestination = mind.HoldPos;
            thisInst.Attempt3DNavi = true;
            thisInst.Retreat = true;    //Prevent the auto-driveaaaa

            float dist = (tank.boundsCentreWorldNoCheck - mind.HoldPos).magnitude;
            thisInst.lastRange = dist;

            if (thisInst.lastEnemy == null)
            {
                RGeneral.LollyGag(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            if (dist > 10)
            {
                thisInst.ProceedToObjective = true;
                thisInst.Steer = true;
                thisInst.lastDestination = mind.HoldPos;
            }
            else
            {
                thisInst.ProceedToObjective = true;
                thisInst.Steer = true;
                thisInst.PivotOnly = true;
                thisInst.lastDestination = thisInst.lastEnemy.tank.boundsCentreWorldNoCheck;
            }
        }
    }
}
