﻿using UnityEngine;

namespace TAC_AI.AI
{
    public static class AIEWeapons
    {
        public static void WeaponDirector(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            float FinalAim;

            try
            {
                if (!tank.beam.IsActive)
                {
                    if (thisInst.DANGER && thisInst.lastEnemy.IsNotNull())
                    {
                        thisInst.lastWeaponAction = 1;
                        if (tank.IsAnchored)
                        {
                            Vector3 aimTo = (thisInst.lastEnemy.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                            float driveAngle = Vector3.Angle(aimTo, tank.rootBlockTrans.forward);
                            if (Mathf.Abs(driveAngle) >= thisInst.AnchorAimDampening)
                                FinalAim = 1;
                            else
                                FinalAim = Mathf.Abs(driveAngle / thisInst.AnchorAimDampening);
                            thisControl.m_Movement.FaceDirection(tank, aimTo, FinalAim);//Face the music
                        }
                    }
                    else if (thisInst.Obst.IsNotNull())
                    {
                        thisInst.lastWeaponAction = 2;
                    }
                    else
                    {
                        thisInst.lastWeaponAction = 0;
                    }
                }
            }
            catch
            {
                Debug.Log("TACtical_AI: WeaponDirector - Error on handling");
            }
        }

        public static void WeaponMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            thisInst.OverrideAim = 0;
            if (!tank.beam.IsActive)
            {
                if (thisInst.IsMultiTech)
                {   // sync to host tech
                    if (thisInst.LastCloseAlly.IsNotNull())
                    {
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            thisInst.OverrideAim = 3;
                            var targetTank = thisInst.lastEnemy.gameObject.GetComponent<Tank>();
                            thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, AIECore.Extremes(targetTank.blockBounds.extents));
                            if (thisInst.FIRE_NOW)
                                thisControl.m_Weapons.FireWeapons(tank);
                        }
                        else if (thisInst.LastCloseAlly.control.FireControl)
                        {
                            thisControl.m_Weapons.FireWeapons(tank);
                        }
                    }
                }
                else if (thisInst.lastWeaponAction == 2)
                {
                    if (thisInst.Obst.IsNotNull())
                    {
                        try
                        {
                            //Debug.Log("TACtical_AI:Trying to shoot at " + thisInst.Obst.name);
                            thisInst.OverrideAim = 2;
                            thisControl.m_Weapons.FireAtTarget(tank, thisInst.Obst.position + Vector3.up, 3f); 
                            if (thisInst.FIRE_NOW)
                                thisControl.m_Weapons.FireWeapons(tank);
                        }
                        catch
                        {
                            Debug.Log("TACtical_AI: WeaponDirector - Crash on targeting scenery");
                        }
                        try
                        {
                            if (thisInst.Obst.GetComponent<Damageable>().Invulnerable)
                            {
                                thisInst.Obst = null;
                            }
                        }
                        catch
                        {
                            Debug.Log("TACtical_AI: Obst HAS NO DAMAGEABLE");
                        }
                    }
                }
                else if (thisInst.lastWeaponAction == 1)
                {
                    if (thisInst.lastEnemy != null)
                    {
                        thisInst.OverrideAim = 1;
                        var targetTank = thisInst.lastEnemy.tank;
                        thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, AIECore.Extremes(targetTank.blockBounds.extents));
                        if (thisInst.FIRE_NOW)
                            thisControl.m_Weapons.FireWeapons(tank);
                    }
                }
                else if (thisInst.FIRE_NOW)
                    thisControl.m_Weapons.FireWeapons(tank);
            }
        }
    }
}
