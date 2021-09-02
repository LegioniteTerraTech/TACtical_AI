using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy.EnemyOperations
{
    public static class RStation
    {
        public static void HoldPosition(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            //The Handler that tells the Tank (Escort) what to do movement-wise
            BGeneral.ResetValues(thisInst);


            thisInst.Attempt3DNavi = true;
            thisInst.Retreat = true;    //Prevent the auto-driveaaaa

            float dist = (mind.HoldPos - tank.boundsCentreWorldNoCheck).magnitude;
            thisInst.lastRange = dist;

            if (thisInst.lastEnemy == null)
            {
                RGeneral.LollyGag(thisInst, tank, mind);
                return;
            }
            RGeneral.Engadge(thisInst, tank, mind);

            if (dist > 6)
            {
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  HOLDING GROUND (or space)!!!");
                thisInst.ProceedToObjective = true;
                thisInst.Steer = true;
                thisInst.lastDestination = mind.HoldPos;
                if (Mathf.Abs(Vector3.Dot(mind.HoldPos - tank.boundsCentreWorldNoCheck, tank.rootBlockTrans.forward)) > 0.6f)
                {   //Move
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = -1;
                }
                else
                {   //Aim back
                    thisInst.PivotOnly = true;
                }
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
