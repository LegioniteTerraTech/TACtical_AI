using UnityEngine;

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
                        thisInst.lastWeaponAction = AIWeaponState.Enemy;
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
                        thisInst.lastWeaponAction = AIWeaponState.Obsticle;
                    }
                    else
                    {
                        thisInst.lastWeaponAction = 0;
                    }
                }
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: WeaponDirector - Error on handling");
            }
        }

        public static void WeaponMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            thisInst.OverrideAim = 0;
            if (!tank.beam.IsActive)
            {
                if (thisInst.IsMultiTech)
                {   // sync to host tech
                    if (thisInst.lastCloseAlly.IsNotNull())
                    {
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            thisInst.OverrideAim = AIWeaponState.Mimic;
                            var targetTank = thisInst.lastEnemy.gameObject.GetComponent<Tank>();
                            thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, targetTank.GetCheapBounds());
                            if (thisInst.FIRE_NOW)
                                thisControl.m_Weapons.FireWeapons(tank);
                        }
                        else if (thisInst.lastCloseAlly.control.FireControl)
                        {
                            thisControl.m_Weapons.FireWeapons(tank);
                        }
                    }
                }
                else if (thisInst.lastWeaponAction == AIWeaponState.Obsticle)
                {
                    if (thisInst.Obst.IsNotNull())
                    {
                        try
                        {
                            //Debug.Log("TACtical_AI:Trying to shoot at " + thisInst.Obst.name);
                            thisInst.OverrideAim = AIWeaponState.Obsticle;
                            thisControl.m_Weapons.FireAtTarget(tank, thisInst.Obst.position + Vector3.up, 3f); 
                            if (thisInst.FIRE_NOW)
                                thisControl.m_Weapons.FireWeapons(tank);
                        }
                        catch
                        {
                            DebugTAC_AI.Log("TACtical_AI: WeaponDirector - Crash on targeting scenery");
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
                            DebugTAC_AI.Log("TACtical_AI: Obst HAS NO DAMAGEABLE");
                        }
                    }
                }
                else if (thisInst.lastWeaponAction == AIWeaponState.Enemy)
                {
                    if (thisInst.lastEnemy != null)
                    {
                        thisInst.OverrideAim = AIWeaponState.Enemy;
                        var targetTank = thisInst.lastEnemy.tank;
                        thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, targetTank.GetCheapBounds());
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
