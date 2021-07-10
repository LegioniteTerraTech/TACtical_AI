﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy.EnemyOperations;

namespace TAC_AI.AI.Enemy
{
    public static class RGeneral
    {
        static float RANDRange = 50;

        public static bool LollyGag(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind, bool holdGround = false)
        {
            bool isRegenerating = false;
            if (mind.Hurt)// && thisInst.lastDestination.Approximately(tank.boundsCentreWorldNoCheck, 10)
            {
                var energy = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                if (mind.CommanderSmarts >= EnemySmarts.Meh)
                {
                    if (energy.storageTotal > 500)
                    {
                        if (mind.SolarsAvail && tank.Anchors.NumPossibleAnchors > 0 && !tank.IsAnchored)
                        {
                            if (thisInst.anchorAttempts < 6)
                            {
                                tank.TryToggleTechAnchor();
                                thisInst.anchorAttempts++;
                            }
                            else
                            {   //Try to find new spot
                                DefaultIdle(thisInst, tank, mind);
                            }
                        }
                        if (energy.storageTotal - 100 < energy.currentAmount)
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
                    if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs < 3)
                    {
                        bool venPower = false;
                        if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                        thisInst.PendingSystemsCheck = RRepair.EnemyRepairStepper(thisInst, tank, mind, Super: venPower);
                        thisInst.AttemptedRepairs++;
                        Debug.Log("TACtical_AI: Tech " + tank.name + " is repairing");
                        return true;
                    }
                    else
                        mind.Hurt = false;
                }
                if (mind.CommanderSmarts >= EnemySmarts.IntAIligent)
                {
                    if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs < 4)
                    {
                        if (energy.currentAmount / energy.storageTotal > 0.5)
                        {
                            //flex yee building speeds on them players
                            thisInst.PendingSystemsCheck = !RRepair.EnemyInstaRepair(tank, mind);
                            thisInst.AttemptedRepairs++;
                        }
                        else
                        {
                            bool venPower = false;
                            if (mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                            thisInst.PendingSystemsCheck = RRepair.EnemyRepairStepper(thisInst, tank, mind, 6, Super: venPower);
                            thisInst.AttemptedRepairs++;
                        }
                        Debug.Log("TACtical_AI: Tech " + tank.name + " is repairing");
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

            //Debug.Log("TACtical_AI: Tech " + tank.name + " is lollygagging   " + mind.CommanderMind.ToString());

            if (holdGround)
                thisInst.lastDestination = mind.HoldPos;
            else
            {
                switch (mind.CommanderMind)
                {
                    case EnemyAttitude.Default: // do dumb stuff
                        DefaultIdle(thisInst, tank, mind);
                        break;
                    case EnemyAttitude.Homing:  // Get nearest tech regardless of max combat range and attack them
                        HomingIdle(thisInst, tank, mind);
                        break;
                    case EnemyAttitude.Miner:   // mine resources
                        RMiner.MineYerOwnBusiness(thisInst, tank, mind);
                        break;
                    //The case below I still have to think of a reason for them to do the things
                    case EnemyAttitude.Junker:  // Huddle up by blocks on the ground
                        DefaultIdle(thisInst, tank, mind);
                        break;
                }
            }
            if (mind.EvilCommander == EnemyHandling.Naval)
                thisInst.lastDestination = AIEPathing.OffsetToSea(thisInst.lastDestination, thisInst);
            else if (mind.EvilCommander == EnemyHandling.Starship)
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst);
            else //Snap to ground
                thisInst.lastDestination = AIEPathing.OffsetFromGround(thisInst.lastDestination, thisInst, tank.blockBounds.size.y);
            return isRegenerating;
        }
        public static void Engadge(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (!mind.StartedAnchored && tank.IsAnchored)
            {
                tank.TryToggleTechAnchor();
                thisInst.anchorAttempts = 0;
            }
        }


        // Handle being bored AIs
        public static void DefaultIdle(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (thisInst.ActionPause == 1)
            {
                thisInst.lastDestination = GetRANDPos(tank);
                thisInst.ActionPause = 0;
            }
            else if (thisInst.ActionPause == 0)
                thisInst.ActionPause = 30;
            else
                thisInst.ActionPause--;
        }
        public static void HomingIdle(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            //Try find next target to assault
            try
            {
                thisInst.lastEnemy = mind.FindEnemy(inRange: 500);
            }
            catch { }//No tanks available
        }
        public static Vector3 GetRANDPos(Tank tank)
        {
            Vector3 final = tank.boundsCentreWorldNoCheck;

            final.x += UnityEngine.Random.Range(-RANDRange, RANDRange);
            final.y += UnityEngine.Random.Range(-RANDRange, RANDRange);
            final.z += UnityEngine.Random.Range(-RANDRange, RANDRange);

            return final;
        }


        // HOSTILITIES
        /// <summary>
        /// Attack like default
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void AidAttack(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI
            thisInst.lastEnemy = mind.FindEnemy();
            if (thisInst.lastEnemy != null)
            {
                //Fire even when retreating - the AI's life depends on this!
                thisInst.DANGER = true;
            }
            else
                thisInst.DANGER = false;
        }

        /// <summary>
        /// Hold fire until aiming at target cab-forwards or after some time
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void AimAttack(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for turrets
            thisInst.DANGER = false;
            thisInst.lastEnemy = mind.FindEnemy();
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                thisInst.Urgency += KickStart.AIClockPeriod / 5;
                if (thisInst.SideToThreat)
                {
                    if (Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) < 0.15f || Mathf.Abs((tank.rootBlockTrans.right - aimTo).magnitude) > -0.15f || thisInst.Urgency >= 30)
                    {
                        thisInst.DANGER = true;
                        thisInst.Urgency = 30;
                    }
                }
                else
                {
                    if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || thisInst.Urgency >= 30)
                    {
                        thisInst.DANGER = true;
                        thisInst.Urgency = 30;
                    }
                }
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.DANGER = false;
            }
        }

        /// <summary>
        /// Prioritize removal of obsticles over attacking enemy
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void SelfDefense(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (thisInst.Obst == null)
            {
                AidAttack(thisInst, tank, mind);
            }
        }

        /// <summary>
        /// Find enemy and then chase the enemy until lost
        /// </summary>
        /// <param name="thisInst"></param>
        /// <param name="tank"></param>
        public static void HoldGrudge(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
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
            thisInst.lastEnemy = mind.FindEnemy();
        }
    }
}