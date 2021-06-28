using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    class RMission
    {
        public static bool TryHandleMissionAI(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            string name = tank.name;
            if (name == "Missile Defense")
            {
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EnemyAttack.Bully;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.Mild;
                return true;
            }

            if (name == "Wingnut")
            {
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EnemyAttack.Bully;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                return true;
            }


            // Racer
            if (name == "Runner")
            {   //WIP
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Wheeled;
                mind.CommanderAttack = EnemyAttack.Coward;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                return true;
            }

            if (name == "Enemy HQ")
            {   //Base where enemies spawn from
                mind.AllowInfBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EnemyAttack.Bully;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.AtFull;
                return true;
            }

            if (name == "Missile #2")
            {
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.SuicideMissile;
                mind.CommanderAttack = EnemyAttack.Grudge;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.MissionTrigger;
                return true;
            }

            return false;
        }
    }
}
