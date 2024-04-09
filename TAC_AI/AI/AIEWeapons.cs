using UnityEngine;
using TAC_AI.World;

namespace TAC_AI.AI
{
    internal static class AIEWeapons
    {
        public static void WeaponDirector(TankControl thisControl, TankAIHelper thisInst, Tank tank)
        {
            float FinalAim;

            try
            {
                if (!tank.beam.IsActive)
                {
                    if (thisInst.AttackEnemy && thisInst.lastEnemyGet.IsNotNull())
                    {
                        thisInst.WeaponState = AIWeaponState.Enemy;
                        if (tank.IsAnchored)
                        {
                            Vector3 aimTo = (thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                            float driveAngle = Vector3.Angle(aimTo, tank.rootBlockTrans.forward);
                            if (Mathf.Abs(driveAngle) >= AIGlobals.AnchorAimDampening)
                                FinalAim = 1;
                            else
                                FinalAim = Mathf.Abs(driveAngle / AIGlobals.AnchorAimDampening);
                            thisControl.m_Movement.FaceDirection(tank, aimTo, FinalAim);//Face the music
                        }
                    }
                    else if (thisInst.Obst.IsNotNull())
                    {
                        thisInst.WeaponState = AIWeaponState.Obsticle;
                    }
                    else
                    {
                        thisInst.WeaponState = 0;
                    }
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": WeaponDirector - Error on handling");
            }
        }

        public static void WeaponMaintainer(TankControl thisControl, TankAIHelper thisInst, Tank tank)
        {
            thisInst.ActiveAimState = 0;
            if (!tank.beam.IsActive)
            {
                if (!thisInst.FIRE_ALL && thisInst.AIAlign == AIAlignment.Player && 
                    !ManNetwork.IsNetworked && AIGlobals.PlayerClientFireCommand() &&
                    ManPlayerRTS.inst.LocalPlayerTechsControlled.Contains(thisInst))
                    thisInst.FIRE_ALL = true;

                if (thisInst.IsMultiTech)
                {   // sync to host tech
                    if (thisInst.lastCloseAlly.IsNotNull())
                    {
                        if (thisInst.lastEnemyGet.IsNotNull())
                        {
                            thisInst.ActiveAimState = AIWeaponState.Mimic;
                            var targetTank = thisInst.lastEnemyGet.tank;
                            if (targetTank)
                                thisControl.m_Weapons.AimAtTarget(tank, targetTank.boundsCentreWorldNoCheck, targetTank.GetCheapBounds());
                            else
                                thisControl.m_Weapons.AimAtTarget(tank, thisInst.lastEnemyGet.centrePosition, thisInst.lastEnemyGet.GetCheapBounds() + 1);
                            if (thisInst.FIRE_ALL)
                                thisControl.m_Weapons.FireWeapons(tank);
                        }
                        else if (thisInst.lastCloseAlly.control.FireControl)
                        {
                            thisControl.m_Weapons.FireWeapons(tank);
                        }
                    }
                }
                else if (thisInst.WeaponState == AIWeaponState.Obsticle)
                {
                    if (thisInst.Obst.IsNotNull())
                    {
                        try
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ":Trying to shoot at " + thisInst.Obst.name);
                            thisInst.ActiveAimState = AIWeaponState.Obsticle;
                            thisControl.m_Weapons.AimAtTarget(tank, thisInst.Obst.position + Vector3.up, 3f); 
                            if (thisInst.FIRE_ALL)
                                thisControl.m_Weapons.FireWeapons(tank);
                        }
                        catch
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": WeaponDirector - Crash on targeting scenery");
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
                            DebugTAC_AI.Log(KickStart.ModID + ": Obst HAS NO DAMAGEABLE");
                        }
                    }
                }
                else if (thisInst.WeaponState == AIWeaponState.Enemy)
                {
                    if (thisInst.lastEnemyGet != null)
                    {
                        thisInst.ActiveAimState = AIWeaponState.Enemy;
                        var targetTank = thisInst.lastEnemyGet.tank;
                        if (targetTank)
                            thisControl.m_Weapons.AimAtTarget(tank, targetTank.boundsCentreWorldNoCheck, targetTank.GetCheapBounds());
                        else
                            thisControl.m_Weapons.AimAtTarget(tank, thisInst.lastEnemyGet.centrePosition, thisInst.lastEnemyGet.GetCheapBounds() + 1);
                        if (thisInst.FIRE_ALL)
                            thisControl.m_Weapons.FireWeapons(tank);
                    }
                }
                else if (thisInst.FIRE_ALL)
                    thisControl.m_Weapons.FireWeapons(tank);
            }
        }

    }
}
