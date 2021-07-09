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

        public static void ManageBolts(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            //if (tank.IsSleeping)
            //    return;
            switch (mind.CommanderBolts)
            {
                case EnemyBolts.Default:        // Blow up like default - first enemy sighting
                    if (thisInst.lastEnemy.IsNotNull())
                        tank.control.DetonateExplosiveBolt();
                    break;
                case EnemyBolts.MissionTrigger:  // do nothing
                    break;
                //DO NOT CALL THE TWO BELOW WITHOUT EnemyMemory!!!  THEY WILL ACT LIKE DEFAULT BUT WORSE!!!
                case EnemyBolts.AtFull:         // Blow up passively at full health (or we are an area town base)
                    if (AllyCount(tank) < KickStart.MaxEnemySplitLimit && !AIERepair.SystemsCheckBolts(tank, mind.TechMemor))
                        tank.control.DetonateExplosiveBolt();
                    break;
                case EnemyBolts.AtFullOnAggro:  // Blow up if enemy is in range and on full health
                    if (thisInst.lastEnemy.IsNotNull() && AllyCount(tank) < KickStart.MaxEnemySplitLimit && !AIERepair.SystemsCheckBolts(tank, mind.TechMemor))
                        tank.control.DetonateExplosiveBolt();
                    break;
                default:                        // Unimplemented
                    if (thisInst.lastEnemy.IsNotNull())
                        tank.control.DetonateExplosiveBolt();
                    break;
            }
        }

        public static int AllyCount(Tank tank)
        {
            int AllyCount = 0;
            var allTechs = Singleton.Manager<ManTechs>.inst.CurrentTechs;
            int techCount = allTechs.Count();
            List<Tank> techs = allTechs.ToList();
            try
            {
                for (int stepper = 0; techCount > stepper; stepper++)
                {
                    if (techs.ElementAt(stepper).IsFriendly(tank.Team))
                    { 
                        AllyCount++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log("TACtical_AI: Error on ally counting");
                Debug.Log(e);
            }
            return AllyCount;
        }
    }
}
