using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI
{
    public static class BGeneral
    {
        public static void ResetValues(AIECore.TankAIHelper thisInst)
        {
            thisInst.AvoidStuff = true;
            thisInst.Yield = false;
            thisInst.PivotOnly = false;
            thisInst.FIRE_NOW = false;
            thisInst.BOOST = false;
            thisInst.forceBeam = false;
            thisInst.forceDrive = false;
            thisInst.featherBoost = false;

            thisInst.MoveFromObjective = false;
            thisInst.ProceedToObjective = false;
            thisInst.ProceedToBase = false;
            thisInst.ProceedToMine = false;
        }

        public static void AidDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI
            if (thisInst.lastEnemy != null)
            {
                thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
                //Fire even when retreating - the AI's life depends on this!
                thisInst.DANGER = true;
            }
            else
            {
                thisInst.DANGER = false;
                thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            }
        }

        public static void AimDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for turrets
            thisInst.DANGER = false;
            thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                thisInst.Urgency++;
                if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || thisInst.Urgency >= 30)
                {
                    thisInst.DANGER = true;
                    thisInst.Urgency = 30;
                }
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.DANGER = false;
            }
        }

        public static void SelfDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (thisInst.Obst == null)
            {
                AidDefend(thisInst, tank);
            }
        }

    }
}
