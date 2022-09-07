using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.Templates;

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
            thisInst.FirePROPS = false;
            thisInst.ForceSetBeam = false;
            thisInst.ForceSetDrive = false;
            thisInst.FeatherBoost = false;

            thisInst.DriveDest = EDriveDest.None;
        }

        /// <summary>
        /// Defend like default
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static bool AidDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI
            if (thisInst.lastEnemy != null)
            {
                thisInst.lastEnemy = thisInst.GetTarget();
                //Fire even when retreating - the AI's life depends on this!
                thisInst.AttackEnemy = true;
                return false;
            }
            else
            {
                thisInst.AttackEnemy = false;
                thisInst.lastEnemy = thisInst.GetTarget();
                return thisInst.lastEnemy;
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
            thisInst.AttackEnemy = false;
            thisInst.lastEnemy = thisInst.GetTarget();
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                thisInst.WeaponDelayClock++;
                if (thisInst.Attempt3DNavi)
                {
                    if (thisInst.SideToThreat)
                    {
                        float dot = Vector3.Dot(tank.rootBlockTrans.right, aimTo);
                        if (dot > 0.45f || dot < -0.45f || thisInst.WeaponDelayClock >= 30)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 30;
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(tank.rootBlockTrans.forward, aimTo) > 0.45f || thisInst.WeaponDelayClock >= 30)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 30;
                        }
                    }
                }
                else
                {
                    if (thisInst.SideToThreat)
                    {
                        float dot = Vector2.Dot(tank.rootBlockTrans.right.ToVector2XZ(), aimTo.ToVector2XZ());
                        if (dot > 0.45f || dot < -0.45f || thisInst.WeaponDelayClock >= 30)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 30;
                        }
                    }
                    else
                    {
                        if (Vector2.Dot(tank.rootBlockTrans.forward.ToVector2XZ(), aimTo.ToVector2XZ()) > 0.45f || thisInst.WeaponDelayClock >= 30)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 30;
                        }
                    }
                }
            }
            else
            {
                thisInst.WeaponDelayClock = 0;
                thisInst.AttackEnemy = false;
            }
        }

        public static void SelfDefend(AIECore.TankAIHelper thisInst, Tank tank)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (thisInst.Obst == null)
            {
                if (AidDefend(thisInst, tank))
                {
                    AIECore.RequestFocusFireALL(tank, thisInst.lastEnemy, RequestSeverity.ThinkMcFly);
                }
                else
                    thisInst.AttackEnemy = false;
            }
            else
                thisInst.AttackEnemy = true;
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
            {   // focus fire like Grudge
                thisInst.AttackEnemy = true;
                if (!thisInst.lastEnemy.isActive)
                    thisInst.lastEnemy = thisInst.GetTarget();
            }
            else
            {
                thisInst.AttackEnemy = false;
                thisInst.lastEnemy = thisInst.GetTarget();
            }
        }

    }
}
