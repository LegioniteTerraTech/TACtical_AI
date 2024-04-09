using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public static bool CanRetreat(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (!mind.CanDoRetreat)
                return false;
            if (!tank.IsAnchored && mind.Hurt)// && thisInst.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10)
            {
                if (mind.CommanderSmarts >= EnemySmarts.Meh && thisInst.CanStoreEnergy() &&
                    thisInst.GetEnergyPercent() < AIGlobals.BatteryRetreatPercent)
                {
                    if (mind.SolarsAvail && !Singleton.Manager<ManTimeOfDay>.inst.NightTime)
                        return true;
                    else if (AIECore.ChargedChargerExists(tank, mind.MaxCombatRange, tank.Team))
                        return true;
                }
                if (mind.CommanderSmarts == EnemySmarts.Smrt && 
                    thisInst.DamageThreshold < AIGlobals.RetreatBelowTechDamageThreshold)
                {
                    return true;
                }
            }
            return AIECore.RetreatingTeams.Contains(tank.Team);
        }

        internal static bool LollyGag(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct, bool holdGround = false)
        {
            bool isRegenerating = false;
            if (mind.Hurt)// && thisInst.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10)
            {
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (thisInst.CanStoreEnergy())
                    {
                        if (mind.SolarsAvail && !Singleton.Manager<ManTimeOfDay>.inst.NightTime && tank.Anchors.NumPossibleAnchors > 0 && !tank.IsAnchored)
                        {
                            if (thisInst.anchorAttempts < 6)
                            {
                                tank.Anchors.TryAnchorAll();
                                thisInst.anchorAttempts++;
                            }
                            else
                            {   //Try to find new spot
                                DefaultIdle(thisInst, tank, mind, ref direct);
                            }
                        }
                        else if (!holdGround && AIECore.FetchChargedChargers(tank, mind.MaxCombatRange, out Transform posTrans, out _, tank.Team))
                        {
                            direct.SetLastDest(posTrans.position);
                            return true;
                        }
                        if (thisInst.GetEnergyPercent() > 0.9f)
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
                    if (thisInst.PendingDamageCheck) //&& thisInst.AttemptedRepairs < 3)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
                if (mind.CommanderSmarts >= EnemySmarts.IntAIligent)
                {
                    if (thisInst.PendingDamageCheck) //&& thisInst.AttemptedRepairs < 4)
                    {
                        if (thisInst.GetEnergyPercent() > 0.5f)
                        {
                            //flex yee building speeds on them players
                            thisInst.PendingDamageCheck = !RRepair.EnemyInstaRepair(tank, mind);
                            //thisInst.AttemptedRepairs++;
                        }
                        else
                        {
                            bool venPower = false;
                            if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                            thisInst.PendingDamageCheck = RRepair.EnemyRepairStepper(thisInst, tank, mind, Super: venPower);
                            //thisInst.AttemptedRepairs++;
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
                thisInst.anchorAttempts = 0;
            }

            //DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " is lollygagging   " + mind.CommanderMind.ToString());

            if (holdGround)
                direct.SetLastDest(mind.sceneStationaryPos);
            else
            {
                switch (mind.CommanderMind)
                {
                    case EnemyAttitude.Default: // do dumb stuff
                        DefaultIdle(thisInst, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Homing:  // Get nearest tech regardless of max combat range and attack them
                        HomingIdle(thisInst, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Miner:   // mine resources
                        RMiner.MineYerOwnBusiness(thisInst, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.NPCBaseHost: // mine resources - will run off to do missions later
                        RMiner.MineYerOwnBusiness(thisInst, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.Boss:        // Tidy base - will run off to do missions later
                        RScavenger.Scavenge(thisInst, tank, mind, ref direct);
                        break;
                    //The case below I still have to think of a reason for them to do the things
                    case EnemyAttitude.Junker:  // Huddle up by blocks on the ground
                        RScavenger.Scavenge(thisInst, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.OnRails:
                        break;
                    case EnemyAttitude.Invader:
                        break;
                    case EnemyAttitude.Guardian:
                        RGuardian.MotivateDefend(thisInst, tank, mind, ref direct);
                        break;
                    case EnemyAttitude.PartTurret:
                        // Load, Aim,    FIIIIIRRRRRRRRRRRRRRRRRRRRRRRRRRRE!!!
                        BMultiTech.MimicDefend(thisInst, thisInst.tank);
                        BMultiTech.MTStatic(thisInst, thisInst.tank, ref direct);
                        //EMultiTech.FollowTurretBelow(helper, helper.tank, ref direct);
                        BMultiTech.BeamLockWithinBounds(thisInst, thisInst.tank); //lock rigidbody with closest non-MT Tech on build beam
                        break;
                    case EnemyAttitude.PartStatic:
                        // Defend and sit like good guard dog
                        BMultiTech.MimicDefend(thisInst, thisInst.tank);
                        BMultiTech.MTStatic(thisInst, thisInst.tank, ref direct);
                        BMultiTech.BeamLockWithinBounds(thisInst, thisInst.tank); //lock rigidbody with closest non-MT Tech on build beam
                        break;
                    case EnemyAttitude.PartMimic:
                        BMultiTech.MimicAllClosestAlly(thisInst, thisInst.tank, ref direct);
                        break;
                    default:
                        break;
                }
            }

            return isRegenerating;
        }
        internal static void Engadge(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (!mind.StartedAnchored && tank.IsAnchored)
            {
                thisInst.UnAnchor();
                thisInst.anchorAttempts = 0;
            }
        }


        // Handle being bored AIs
        internal static void DefaultIdle(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            if (thisInst.ActionPause == 1)
            {
                direct.SetLastDest(GetRANDPos(tank));
                thisInst.actionPause = 0;
            }
            else if (thisInst.ActionPause == 0)
                thisInst.actionPause = 60;
            if (thisInst.ActionPause > 15)
                direct.DriveDest = EDriveDest.ToLastDestination;
            else
                direct.DriveDest = EDriveDest.None;
        }
        internal static void HomingIdle(TankAIHelper thisInst, Tank tank, EnemyMind mind, ref EControlOperatorSet direct)
        {
            //Try find next target to assault
            try
            {
                var target = thisInst.FindEnemy(mind.InvertBullyPriority);
                if (target)
                    direct.SetLastDest(target.tank.boundsCentreWorldNoCheck);
                else
                    DefaultIdle(thisInst, tank, mind, ref direct);
                /*
                thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority, inRange: AIGlobals.EnemyExtendActionRange);
                if (thisInst.lastEnemyGet)
                    direct.lastDestination = thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck;
                else
                    DefaultIdle(thisInst, tank, mind, ref direct);
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
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        internal static void Scurry(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI - Sub-Neutral (Coward)
            //if (mind.CommanderAttack == EnemyAttack.Coward)
            //{
            thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            thisInst.AttackEnemy = false;
            //}
        }

        /// <summary>
        /// Only is used to keep track of enemies
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        internal static void Monitor(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            TeamBasePointer funds = RLoadedBases.GetTeamHQ(tank.Team);
            if (funds != null)
            {
                if ((funds.WorldPos.ScenePosition - tank.boundsCentreWorldNoCheck).sqrMagnitude > AIGlobals.MaximumNeutralMonitorSqr)
                    thisInst.lastEnemy = null;  // Stop following this far from base
                else
                    thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            }
            else
                thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            if (thisInst.lastEnemyGet)
                thisInst.SetPursuit(thisInst.lastEnemyGet);
            thisInst.AttackEnemy = false;
        }


        // HOSTILITIES
        /// <summary>
        /// Base attack
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        internal static void BaseAttack(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI
            var lastEnemyC = thisInst.lastEnemy;
            thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            //DebugTAC_AI.Log("Base " + tank.name + " has enemy: " + thisInst.lastEnemy.IsNotNull() + " prev " + lastEnemyC.IsNotNull());
            if (thisInst.lastEnemyGet != null)
            {
                thisInst.AttackEnemy = true;
            }
            else
                thisInst.AttackEnemy = false;
        }

        /// <summary>
        /// Attack like default
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        internal static void AidAttack(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI
            var lastEnemyC = thisInst.lastEnemy;
            thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            //DebugTAC_AI.Log("Tech " + tank.name + " has enemy: " + thisInst.lastEnemy.IsNotNull() + " prev " + lastEnemyC.IsNotNull()
            //    + " | Range " + thisInst.MaxCombatRange);
            if (thisInst.lastEnemyGet != null)
            {
                //Fire even when retreating - the AI's life depends on this!
                thisInst.AttackEnemy = true;
                if (thisInst.lastOperatorRange < AIGlobals.MaxRangeFireAll)
                {
                    thisInst.AttackEnemy = true;
                    return;
                }
            }
            thisInst.AttackEnemy = false;
        }

        /// <summary>
        /// Hold fire until aiming at target cab-forwards or after some time
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        internal static void AimAttack(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for turrets
            thisInst.AttackEnemy = false;
            var lastEnemyC = thisInst.lastEnemy;
            thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            //DebugTAC_AI.Log("Sniper " + tank.name + " has enemy: " + thisInst.lastEnemy.IsNotNull() + " prev " + lastEnemyC.IsNotNull());
            if (thisInst.lastEnemyGet != null)
            {
                Vector3 aimTo = (thisInst.lastEnemyGet.tank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).normalized;
                thisInst.WeaponDelayClock += KickStart.AIClockPeriod;
                if (thisInst.Attempt3DNavi)
                {
                    if (thisInst.SideToThreat)
                    {
                        float dot = Vector3.Dot(tank.rootBlockTrans.right, aimTo);
                        if (dot > 0.45f || dot < -0.45f || thisInst.WeaponDelayClock >= 150)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 150;
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(tank.rootBlockTrans.forward, aimTo) > 0.45f || thisInst.WeaponDelayClock >= 150)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 150;
                        }
                    }
                }
                else
                {
                    if (thisInst.SideToThreat)
                    {
                        float dot = Vector2.Dot(tank.rootBlockTrans.right.ToVector2XZ(), aimTo.ToVector2XZ());
                        if (dot > 0.45f || dot < -0.45f || thisInst.WeaponDelayClock >= 150)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 150;
                        }
                    }
                    else
                    {
                        if (Vector2.Dot(tank.rootBlockTrans.forward.ToVector2XZ(), aimTo.ToVector2XZ()) > 0.45f || thisInst.WeaponDelayClock >= 150)
                        {
                            thisInst.AttackEnemy = true;
                            thisInst.WeaponDelayClock = 150;
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


        /// <summary>
        /// Prioritize removal of obsticles over attacking enemy
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        internal static void SelfDefense(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (thisInst.Obst == null)
            {
                AidAttack(thisInst, tank, mind);
            }
            else
                thisInst.AttackEnemy = true;
        }


        /// <summary>
        /// Stay focused on first target if the unit is order to focus-fire
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        internal static void RTSCombat(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI
            if (thisInst.lastEnemyGet != null)
            {   // focus fire like Grudge
                thisInst.AttackEnemy = true;
                if (!thisInst.lastEnemyGet.isActive)
                    thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            }
            else
            {
                thisInst.AttackEnemy = false;
                thisInst.lastEnemy = thisInst.FindEnemy(mind.InvertBullyPriority);
            }
        }

        /// <summary>
        /// (OBSOLETE!!! Handled by FindEnemy) Find enemy and then chase the enemy until lost
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        /*
        internal static void HoldGrudge(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (thisInst.lastEnemy != null)
            {
                if (thisInst.lastEnemy.isActive)
                {
                    //Hold that grudge!
                    thisInst.DANGER = true;
                    return;
                }
            }
            thisInst.DANGER = false;
            thisInst.lastEnemySet = mind.FindEnemy();
        }*/
    }
}
