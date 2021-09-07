using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public static class RBolts
    {
        /*  EnemyBolts
        Default,        // Explode IMMEDEATELY
        AtFull,         // Explode when tech is fully-built (requires smrt or above to utilize nicely)
        AtFullOnAggro,  // Explode when tech is fully-built and enemy in range (requires smrt or above to utilize nicely)
        Countdown,      // Explode after # of ingame FixedUpdate ticks
        MissionTrigger, // Hold until triggered by mission event (basically no internal fire)
         */

        public static void ManageBolts(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            //if (tank.IsSleeping)
            //    return;
            if (AtWorldTechMax())
                return; // world is too stressed to handle more
            switch (mind.CommanderBolts)
            {
                case EnemyBolts.Default:        // Blow up like default - first enemy sighting on spacebar
                    if (thisInst.lastEnemy.IsNotNull() && thisInst.FIRE_NOW)
                        BlowBolts(tank, mind);
                    break;
                case EnemyBolts.MissionTrigger:  // do nothing
                    break;
                //DO NOT CALL THE TWO BELOW WITHOUT EnemyMemory!!!  THEY WILL ACT LIKE DEFAULT BUT WORSE!!!
                case EnemyBolts.AtFull:         // Blow up passively at full health (or we are an area town base)
                    if (AllyCostCount(tank) < KickStart.EnemyTeamTechLimit && !AIERepair.SystemsCheckBolts(tank, mind.TechMemor))
                        BlowBolts(tank, mind);
                    break;
                case EnemyBolts.AtFullOnAggro:  // Blow up if enemy is in range and on full health
                    if (thisInst.lastEnemy.IsNotNull() && AllyCostCount(tank) < KickStart.EnemyTeamTechLimit && !AIERepair.SystemsCheckBolts(tank, mind.TechMemor))
                        BlowBolts(tank, mind);
                    break;
                default:                        // Unimplemented
                    if (thisInst.lastEnemy.IsNotNull())
                        BlowBolts(tank, mind);
                    break;
            }
            if (mind.BoltsQueued > 0)
                mind.BoltsQueued--;
        }
        public static void BlowBolts(Tank tank, EnemyMind mind)
        {
            if (mind.TechMemor)
            {
                mind.TechMemor.ReserveSuperGrabs = -256;
            }
            mind.BoltsQueued = 2;
            tank.control.DetonateExplosiveBolt();
        }

        public static int AllyCostCount(Tank tank)
        {
            int AllyCount = 0;
            var allTechs = Singleton.Manager<ManTechs>.inst.CurrentTechs;
            int techCount = allTechs.Count();
            List<Tank> techs = allTechs.ToList();
            try
            {
                for (int stepper = 0; techCount > stepper; stepper++)
                {
                    Tank tech = techs.ElementAt(stepper);
                    if (tech.IsFriendly(tank.Team))
                    { 
                        AllyCount++;
                        if (!tech.IsAnchored)
                        {
                            AllyCount++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: AllyCostCount - Error on ally counting");
                Debug.Log(e);
            }
            return AllyCount;
        }
        public static bool AtWorldTechMax()
        {
            int Counter = 0;
            var allTechs = Singleton.Manager<ManTechs>.inst.CurrentTechs;
            int techCount = allTechs.Count();
            try
            {
                for (int stepper = 0; techCount > stepper; stepper++)
                {
                    Tank tech = allTechs.ElementAt(stepper);
                    if (tech.IsEnemy())
                        Counter++;
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: AtWorldTechMax - Error on The World");
                Debug.Log(e);
            }
            return Counter >= KickStart.MaxEnemyWorldCapacity;
        }
    }
}
