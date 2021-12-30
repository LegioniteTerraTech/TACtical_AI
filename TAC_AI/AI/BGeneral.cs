using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.AI
{
    public static class BGeneral
    {
        public static void ResetValues(AIECore.TankAIHelper thisInst)
        {
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

        /// <summary>
        /// Defend like default
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void AidDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI
            if (thisInst.lastEnemy != null)
            {
                thisInst.lastEnemy = GetTarget(thisInst, tank);
                //Fire even when retreating - the AI's life depends on this!
                thisInst.DANGER = true;
            }
            else
            {
                thisInst.DANGER = false;
                thisInst.lastEnemy = GetTarget(thisInst, tank);
            }
        }

        /// <summary>
        /// Hold fire until aiming at target cab-forwards or after some time
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void AimDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for turrets
            thisInst.DANGER = false;
            thisInst.lastEnemy = GetTarget(thisInst, tank);
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                thisInst.WeaponDelayClock++;
                if (thisInst.SideToThreat)
                {
                    if (Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) < 0.15f || Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) > -0.15f || thisInst.WeaponDelayClock >= 30)
                    {
                        thisInst.DANGER = true;
                        thisInst.WeaponDelayClock = 30;
                    }
                }
                else
                {
                    if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || thisInst.WeaponDelayClock >= 30)
                    {
                        thisInst.DANGER = true;
                        thisInst.WeaponDelayClock = 30;
                    }
                }
            }
            else
            {
                thisInst.WeaponDelayClock = 0;
                thisInst.DANGER = false;
            }
        }

        public static Visible GetTarget(AIECore.TankAIHelper thisInst, Tank tank)
        {
            Visible target = null;
            if ((bool)thisInst.lastPlayer)
            {
                target = thisInst.lastPlayer.tank.Weapons.GetManualTarget();
            }
            if (target == null)
                return tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            return target;
        }
        public static void SelfDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (thisInst.Obst == null)
            {
                AidDefend(thisInst, tank);
            }
        }

        /// <summary>
        /// Stay focused on first target if the unit is order to focus-fire
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void RTSCombat(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI
            if (thisInst.lastEnemy != null)
            {
                if (thisInst.RTSDestination != Vector3.zero)
                    thisInst.lastEnemy = GetTarget(thisInst, tank);
                // else we focus fire
                thisInst.DANGER = true;
            }
            else
            {
                thisInst.DANGER = false;
                if (thisInst.RTSDestination != Vector3.zero)
                    thisInst.lastEnemy = GetTarget(thisInst, tank);
            }
        }

    }
}
