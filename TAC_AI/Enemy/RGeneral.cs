using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy.EnemyOperations;
using TAC_AI.Enemy.EnemyOperations;
using TAC_AI.AI.AlliedOperations;

namespace TAC_AI.AI.Enemy
{
    public static class RGeneral
    {
        public const float RANDRange = 125;
        public static bool CanRetreat(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            if (!mind.CanDoRetreat)
                return false;
            if (!tank.IsAnchored && mind.Hurt)// && helper.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10)
            {
                if (mind.CommanderSmarts >= EnemySmarts.Meh && helper.CanStoreEnergy() &&
                    helper.GetEnergyPercent() < AIGlobals.BatteryRetreatPercent)
                {
                    if (mind.SolarsAvail && !Singleton.Manager<ManTimeOfDay>.inst.NightTime)
                        return true;
                    else if (AIECore.ChargedChargerExists(tank, mind.MaxCombatRange, tank.Team))
                        return true;
                }
                if (mind.CommanderSmarts == EnemySmarts.Smrt && 
                    helper.DamageThreshold < AIGlobals.RetreatBelowTechDamageThreshold)
                {
                    return true;
                }
            }
            return AIECore.RetreatingTeams.Contains(tank.Team);
        }

        internal static bool LollyGag(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct, bool holdGround = false)
        {
            bool isRegenerating = false;
            if (mind.Hurt)// && helper.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10)
            {
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (helper.CanStoreEnergy())
                    {
                        if (mind.SolarsAvail && !Singleton.Manager<ManTimeOfDay>.inst.NightTime && tank.Anchors.NumPossibleAnchors > 0 && !tank.IsAnchored)
                        {
                            if (helper.CanAttemptAnchor)
                            {
                                helper.TryInsureAnchor();
                            }
                            else
                            {   //Try to find new spot
                                DefaultIdle(helper, tank, mind, ref direct);
                            }
                        }
                        else if (!holdGround && AIECore.FetchChargedChargers(tank, mind.MaxCombatRange, out IAIFollowable posTrans, out _, tank.Team))
                        {
                            direct.SetLastDest(posTrans.position);
                            return true;
                        }
                        if (helper.GetEnergyPercent() > 0.9f)
                        {
                            mind.Hurt = false;
                        }
                    }
                    else
                    {
                        //Cannot repair block damage or recharge shields!
                        mind.Hurt = false;
                    }
                    if (tank.IsAnchored && !mind.StartedAnchored)
                    {
                        isRegenerating = true;
                    }
                }
                if (mind.CommanderSmarts == EnemySmarts.Smrt)
                {
                    if (helper.PendingDamageCheck) //&& helper.AttemptedRepairs < 3)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
                if (mind.CommanderSmarts >= EnemySmarts.IntAIligent)
                {
                    if (helper.PendingDamageCheck) //&& helper.AttemptedRepairs < 4)
                    {
                        if (helper.GetEnergyPercent() > 0.5f)
                        {
                            //flex yee building speeds on them players
                            helper.PendingDamageCheck = !RRepair.EnemyInstaRepair(tank, mind);
                            //helper.AttemptedRepairs++;
                        }
                        else
                        {
                            bool venPower = false;
                            if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                            helper.PendingDamageCheck = RRepair.EnemyRepairStepper(helper, tank, mind, Super: venPower);
                            //helper.AttemptedRepairs++;
                        }
                        //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
            }
            else
            {
                //helper.anchorAttempts = 0;
            }

            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is lollygagging   " + mind.CommanderMind.ToString());

            if (holdGround)
                direct.SetLastDest(mind.sceneStationaryPos);
            else
            {
                switch (mind.CommanderMind)
                {
                    case EnemyAttitude.Default: // do dumb stuff
                        DefaultIdle(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Homing:  // Get nearest tech regardless of max combat range and attack them
                        HomingIdle(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Miner:   // mine resources
                        RMiner.MineYerOwnBusiness(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.NPCBaseHost: // mine resources - will run off to do missions later
                        RMiner.MineYerOwnBusiness(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Boss:        // Tidy base - will run off to do missions later
                        RScavenger.Scavenge(helper, tank, mind, ref direct);
                        break;
                    //The case below I still have to think of a reason for them to do the things
                    case EnemyAttitude.Junker:  // Huddle up by blocks on the ground
                        RScavenger.Scavenge(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.OnRails:
                        break;
                    case EnemyAttitude.Invader:
                        break;
                    case EnemyAttitude.Guardian:
                        RGuardian.MotivateDefend(helper, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.PartTurret:
                        // Load, Aim,    FIIIIIRRRRRRRRRRRRRRRRRRRRRRRRRRRE!!!
                        BMultiTech.MimicDefend(helper, tank);
                        BMultiTech.MTStatic(helper, tank, ref direct);
                        //EMultiTech.FollowTurretBelow(helper, helper.tank, ref direct);
                        BMultiTech.BeamLockWithinBounds(helper, tank); //lock rigidbody with closest non-MT Tech on build beam
                        break;
                    case EnemyAttitude.PartStatic:
                        // Defend and sit like good guard dog
                        BMultiTech.MimicDefend(helper, tank);
                        BMultiTech.MTStatic(helper, tank, ref direct);
                        BMultiTech.BeamLockWithinBounds(helper, tank); //lock rigidbody with closest non-MT Tech on build beam
                        break;
                    case EnemyAttitude.PartMimic:
                        BMultiTech.MimicAllClosestAlly(helper, tank, ref direct);
                        break;
                    default:
                        break;
                }
            }

            return isRegenerating;
        }
        internal static void Engadge(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            if (!mind.StartedAnchored && tank.IsAnchored)
            {
                helper.Unanchor();
            }
        }


        // Handle being bored AIs
        internal static void DefaultIdle(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            if (helper.ActionPause == 1)
            {
                direct.SetLastDest(GetRANDPos(tank));
                helper.actionPause = 0;
            }
            else if (helper.ActionPause == 0)
                helper.actionPause = 60;
            if (helper.ActionPause > 15)
                direct.DriveDest = EDriveDest.ToLastDestination;
            else
                direct.DriveDest = EDriveDest.None;
        }
        internal static void HomingIdle(TankAIHelper helper, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //Try find next target to assault
            try
            {
                var target = helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
                if (target)
                    direct.SetLastDest(target.tank.boundsCentreWorldNoCheck);
                else
                    DefaultIdle(helper, tank, mind, ref direct);
                /*
                helper.lastEnemy = helper.FindEnemy(mind.InvertBullyPriority, inRange: AIGlobals.EnemyExtendActionRange);
                if (helper.lastEnemyGet)
                    direct.lastDestination = helper.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                else
                    DefaultIdle(helper, tank, mind, ref direct);
                */
            }
            catch { }//No tanks available
        }
        internal static Vector3 GetRANDPos(Tank tank)
        {
            Vector3 final = tank.boundsCentreWorldNoCheck;

            final.x += UnityEngine.Random.Range(-RANDRange, RANDRange);
            final.y += UnityEngine.Random.Range(-RANDRange, RANDRange);
            final.z += UnityEngine.Random.Range(-RANDRange, RANDRange);

            return final;
        }

        /// <summary>
        /// Only is used to keep track of enemies
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        internal static void Scurry(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI - Sub-Neutral (Coward)
            //if (mind.CommanderAttack == EnemyAttack.Coward)
            //{
            helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
            helper.AttackEnemy = helper.Provoked > 0;
            //}
        }

        /// <summary>
        /// Only is used to keep track of enemies
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        internal static void Monitor(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            TeamBasePointer funds = RLoadedBases.GetTeamHQ(tank.Team);
            if (funds != null)
            {
                if ((funds.WorldPos.ScenePosition - tank.boundsCentreWorldNoCheck).sqrMagnitude > AIGlobals.MaximumNeutralMonitorSqr)
                    helper.lastEnemy = null;  // Stop following this far from base
                else
                    helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
            }
            else  // Don't stalk cause that's rude
                helper.lastEnemy = null;  // Stop following this far from base
            helper.AttackEnemy = false;
            if (helper.lastEnemyGet)
            {
                //helper.SetPursuit(helper.lastEnemyGet);
                if (ManBaseTeams.IsEnemy(tank.Team, helper.lastEnemyGet.tank.Team))
                    helper.AttackEnemy = true;
            }
        }


        // HOSTILITIES
        /// <summary>
        /// Base attack
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        internal static void BaseAttack(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI
            var lastEnemyC = helper.lastEnemy;
            helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
            //DebugTAC_AI.Log("Base " + tank.name + " has enemy: " + helper.lastEnemy.IsNotNull() + " prev " + lastEnemyC.IsNotNull());
            
            if (helper.lastEnemyGet != null)
            {
                helper.AttackEnemy = true;
            }
            else
                helper.AttackEnemy = false;
            
        }

        /// <summary>
        /// Attack like default
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        internal static void AidAttack(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI
            var lastEnemyC = helper.lastEnemy;
            helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
            //DebugTAC_AI.Log("Tech " + tank.name + " has enemy: " + helper.lastEnemy.IsNotNull() + " prev " + lastEnemyC.IsNotNull()
            //    + " | Range " + helper.MaxCombatRange);
            helper.AttackEnemy = false;
            if (helper.lastEnemyGet != null)
            {
                //Fire even when retreating - the AI's life depends on this!
                helper.AttackEnemy = true;
                /*
                if (helper.lastCombatRange < AIGlobals.MaxRangeFireAll)
                {
                    helper.FIRE_ALL = true;
                    return;
                }*/
            }
        }

        /// <summary>
        /// Hold fire until aiming at target cab-forwards or after some time
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        internal static void AimAttack(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for turrets
            var lastEnemyC = helper.lastEnemy;
            helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
            //DebugTAC_AI.Log("Sniper " + tank.name + " has enemy: " + helper.lastEnemy.IsNotNull() + " prev " + lastEnemyC.IsNotNull());
            if (helper.lastEnemyGet != null)
            {
                Vector3 aimTo = (helper.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                helper.WeaponDelayClock += KickStart.AIClockPeriod;
                if (helper.Attempt3DNavi)
                {
                    if (helper.SideToThreat)
                    {
                        float dot = Vector3.Dot(tank.rootBlockTrans.right, aimTo);
                        if (dot > 0.45f || dot < -0.45f || helper.WeaponDelayClock >= 150)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 150;
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(tank.rootBlockTrans.forward, aimTo) > 0.45f || helper.WeaponDelayClock >= 150)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 150;
                        }
                    }
                }
                else
                {
                    if (helper.SideToThreat)
                    {
                        float dot = Vector2.Dot(tank.rootBlockTrans.right.ToVector2XZ(), aimTo.ToVector2XZ());
                        if (dot > 0.45f || dot < -0.45f || helper.WeaponDelayClock >= 150)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 150;
                        }
                    }
                    else
                    {
                        if (Vector2.Dot(tank.rootBlockTrans.forward.ToVector2XZ(), aimTo.ToVector2XZ()) > 0.45f || helper.WeaponDelayClock >= 150)
                        {
                            helper.AttackEnemy = true;
                            helper.WeaponDelayClock = 150;
                        }
                    }
                }
            }
            else
            {
                helper.WeaponDelayClock = 0;
                helper.AttackEnemy = false;
            }
        }


        /// <summary>
        /// Prioritize removal of obsticles over attacking enemy
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        internal static void SelfDefense(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (helper.Obst == null)
            {
                AidAttack(helper, tank, mind);
            }
            else
                helper.AttackEnemy = true;
        }


        /// <summary>
        /// Stay focused on first target if the unit is order to focus-fire
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        internal static void RTSCombat(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI
            if (helper.lastEnemyGet != null)
            {   // focus fire like Grudge
                helper.AttackEnemy = true;
                if (!helper.lastEnemyGet.isActive)
                    helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
            }
            else
            {
                helper.AttackEnemy = false;
                helper.TryRefreshEnemyEnemy(mind.InvertBullyPriority);
            }
        }

        /// <summary>
        /// (OBSOLETE!!! Handled by FindEnemy) Find enemy and then chase the enemy until lost
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="tank"></param>
        /*
        internal static void HoldGrudge(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            if (helper.lastEnemy != null)
            {
                if (helper.lastEnemy.isActive)
                {
                    //Hold that grudge!
                    helper.DANGER = true;
                    return;
                }
            }
            helper.DANGER = false;
            helper.lastEnemySet = mind.FindEnemy();
        }*/
    }
}
