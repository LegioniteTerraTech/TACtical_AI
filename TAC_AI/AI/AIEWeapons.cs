using UnityEngine;
using TAC_AI.World;

namespace TAC_AI.AI
{
    internal static class AIEWeapons
    {
        public static void WeaponDirector(TankControl thisControl, TankAIHelper helper, Tank tank)
        {
            float FinalAim;

            try
            {
                if (!tank.beam.IsActive)
                {
                    if (helper.lastEnemyGet?.tank)
                    {
                        if (AIGlobals.AllowWeaponsDisarm)
                        {
                            if (helper.DriverType == AIDriverType.Stationary || ManBaseTeams.IsEnemy(tank.Team, helper.lastEnemyGet.tank.Team))
                            {   // Stationary AI should NEVER lower guard - even against Sub-Neutrals
                                helper.WeaponState = AIWeaponState.Enemy;
                                helper.SuppressFiring(false);
                            }
                            else
                            {
                                helper.WeaponState = AIWeaponState.HoldFire;
                                helper.SuppressFiring(true);
                            }
                        }
                        else
                            helper.WeaponState = AIWeaponState.Enemy;
                        /*
                        if (helper.AttackEnemy)
                        {
                            if (tank.IsAnchored)
                            {
                                Vector3 aimTo = (helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                                float driveAngle = Vector3.Angle(aimTo, tank.rootBlockTrans.forward);
                                if (Mathf.Abs(driveAngle) >= AIGlobals.AnchorAimDampening)
                                    FinalAim = 1;
                                else
                                    FinalAim = Mathf.Abs(driveAngle / AIGlobals.AnchorAimDampening);
                                thisControl.m_Movement.FaceDirection(tank, aimTo, FinalAim);//Face the music
                            }
                        }
                        */
                    }
                    else if (helper.Obst.IsNotNull())
                    {
                        helper.WeaponState = AIWeaponState.Obsticle;
                        helper.SuppressFiring(false);
                    }
                    else
                    {
                        if (AIGlobals.AllowWeaponsDisarm)
                        {
                            if (tank.TechIsActivePlayer())
                                helper.WeaponState = AIWeaponState.Normal;
                            else
                                helper.WeaponState = AIWeaponState.HoldFire;
                        }
                        else
                            helper.WeaponState = AIWeaponState.Normal;
                        helper.SuppressFiring(false);
                    }
                }
                else
                {
                    helper.WeaponState = AIWeaponState.Normal;
                    helper.SuppressFiring(false);
                    /*
                    helper.WeaponState = AIWeaponState.HoldFire;
                    helper.SuppressFiring(true);
                    */
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": WeaponDirector - Error on handling");
            }
        }

        public static void WeaponMaintainer(TankAIHelper helper, Tank tank)
        {
            helper.ActiveAimState = AIWeaponState.Normal;
            if (!tank.beam.IsActive)
            {
                if (!helper.FIRE_ALL && helper.AIAlign == AIAlignment.Player && 
                    !ManNetwork.IsNetworked && AIGlobals.PlayerClientFireCommand() &&
                    ManWorldRTS.inst.LocalPlayerTechsControlled.Contains(helper))
                    helper.FIRE_ALL = true;

                if (helper.IsMultiTech)
                {   // sync to host tech
                    Tank closeAlly;
                    if ((bool)helper.theResource?.tank)
                        closeAlly = helper.theResource.tank;
                    else
                        closeAlly = helper.lastCloseAlly;
                    if (closeAlly)
                    {
                        Visible targetEnemy = closeAlly.GetHelperInsured().lastEnemyGet;
                        if (targetEnemy == null)
                            targetEnemy = helper.lastEnemyGet;
                        if (targetEnemy.IsNotNull())
                        {
                            helper.ActiveAimState = AIWeaponState.Mimic;
                            Tank targTank = targetEnemy.tank;
                            if (targTank != null)
                                helper.AimAndFireWeapons(targTank.boundsCentreWorldNoCheck, targTank.GetCheapBounds());
                            else
                                helper.AimAndFireWeapons(targetEnemy.centrePosition, targetEnemy.GetCheapBounds() + 1);
                            if (helper.FIRE_ALL)
                                helper.FireAllWeapons();
                        }
                        else if (closeAlly.control.FireControl)
                        {
                            helper.FireAllWeapons();
                        }
                    }
                }
                else
                {
                    switch (helper.WeaponState)
                    {
                        case AIWeaponState.Enemy:
                            if (helper.lastEnemyGet != null)
                            {
                                var targetTank = helper.lastEnemyGet.tank;
                                helper.ActiveAimState = AIWeaponState.Enemy;
                                if (targetTank)
                                    helper.AimAndFireWeapons(targetTank.boundsCentreWorldNoCheck, targetTank.GetCheapBounds());
                                else
                                    helper.AimAndFireWeapons(helper.lastEnemyGet.centrePosition, helper.lastEnemyGet.GetCheapBounds() + 1);
                                if (helper.FIRE_ALL)
                                    helper.FireAllWeapons();
                            }
                            break;
                        case AIWeaponState.HoldFire:
                            helper.ActiveAimState = AIWeaponState.HoldFire;
                            if (helper.lastEnemyGet != null)
                            {
                                var targetTank = helper.lastEnemyGet.tank;
                                Vector3 target;
                                if (targetTank)
                                    target = targetTank.boundsCentreWorldNoCheck;
                                else
                                    target = helper.lastEnemyGet.centrePosition;
                                helper.AimAndFireWeapons(target + (Vector3.down * 0.5f * (target - tank.boundsCentreWorldNoCheck).magnitude), 0);
                            }
                            break;
                        case AIWeaponState.Obsticle:
                            if (helper.Obst.IsNotNull())
                            {
                                try
                                {
                                    //DebugTAC_AI.Log(KickStart.ModID + ":Trying to shoot at " + helper.Obst.name);
                                    helper.ActiveAimState = AIWeaponState.Obsticle;
                                    helper.AimAndFireWeapons(helper.Obst.position + Vector3.up, 3f);
                                    if (helper.FIRE_ALL)
                                        helper.FireAllWeapons();
                                }
                                catch
                                {
                                    DebugTAC_AI.Log(KickStart.ModID + ": WeaponDirector - Crash on targeting scenery");
                                }
                                try
                                {
                                    if (helper.Obst.GetComponent<Damageable>().Invulnerable)
                                    {
                                        helper.Obst = null;
                                    }
                                }
                                catch
                                {
                                    DebugTAC_AI.Log(KickStart.ModID + ": Obst HAS NO DAMAGEABLE");
                                }
                            }
                            break;
                        case AIWeaponState.Normal:
                        case AIWeaponState.Mimic:
                        default:
                            if (helper.FIRE_ALL)
                                helper.FireAllWeapons();
                            break;
                    }
                } 
            }
        }

    }
}
