using UnityEngine;

namespace RandomAdditions.AI
{
    public static class AIEWeapons
    {
        public static void WeaponDirector(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            float FinalAim;

            if (!tank.beam.IsActive)
            {
                if (thisInst.DANGER && thisInst.lastEnemy.IsNotNull())
                {
                    thisInst.lastWeaponAction = 1;
                    if (tank.IsAnchored)
                    {
                        Vector3 aimTo = (thisInst.lastEnemy.rbody.position - tank.rbody.position).normalized;
                        float driveAngle = Vector3.Angle(aimTo, tank.transform.forward);
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
        public static void WeaponMaintainer(TankControl thisControl, AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (!tank.beam.IsActive)
            {
                if (thisInst.IsMultiTech)
                {   // sync to host tech
                    if (thisInst.LastCloseAlly.IsNotNull())
                    {
                        if (thisInst.lastEnemy.IsNotNull())
                        {
                            var targetTank = thisInst.lastEnemy.gameObject.GetComponent<Tank>();
                            thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, AIECore.Extremes(targetTank.blockBounds.extents));
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
                            thisControl.m_Weapons.FireAtTarget(tank, thisInst.Obst.centrePosition, 3f);
                        }
                        catch
                        {
                            Debug.Log("TACtical_AI: Crash on targeting scenery");
                        }
                        if (thisInst.Obst.damageable.Invulnerable)
                        {
                            thisInst.Obst = null;
                        }
                    }
                }
                else if (thisInst.lastWeaponAction == 1)
                {
                    if (thisInst.lastEnemy.IsNotNull())
                    {
                        var targetTank = thisInst.lastEnemy.gameObject.GetComponent<Tank>();
                        thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, AIECore.Extremes(targetTank.blockBounds.extents));
                    }
                }
                else if (thisInst.FIRE_NOW)
                    thisControl.m_Weapons.FireWeapons(tank);
            }
        }
    }
}
