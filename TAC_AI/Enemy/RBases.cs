using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public static class RBases
    {
        public static bool SetupBaseAI(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {   // iterate through EVERY BASE dammit
            string name = tank.name;
            bool DidFire = false;
            if (name == "INSTANTIATED_BASE")
            {   //It's a base spawned by this mod
                tank.Anchors.TryAnchorAll(true);
                DidFire = true;
            }
            switch (name)
            {
                case "error on load":
                    mind.StartedAnchored = true;
                    mind.AllowInfBlocks = true;
                    mind.AllowRepairsOnFly = true;
                    mind.EvilCommander = EnemyHandling.Stationary;
                    mind.CommanderMind = EnemyAttitude.Default;
                    mind.CommanderSmarts = EnemySmarts.IntAIligent;
                    mind.CommanderAttack = EnemyAttack.Spyper;
                    mind.CommanderBolts = EnemyBolts.AtFull;
                    break;
                case "GSO Seller Base":
                case "GSO Production":
                case "GSO Furlough Base":
                    mind.StartedAnchored = true;
                    mind.AllowInfBlocks = true;
                    mind.AllowRepairsOnFly = true;
                    mind.EvilCommander = EnemyHandling.Stationary;
                    mind.CommanderMind = EnemyAttitude.Default;
                    mind.CommanderSmarts = EnemySmarts.IntAIligent;
                    mind.CommanderAttack = EnemyAttack.Grudge;
                    mind.CommanderBolts = EnemyBolts.AtFull;
                    DidFire = true;
                    break;
                case "Enemy HQ":
                    mind.StartedAnchored = true;
                    mind.AllowInfBlocks = true;
                    mind.AllowRepairsOnFly = true;
                    mind.InvertBullyPriority = true;
                    mind.EvilCommander = EnemyHandling.Stationary;
                    mind.CommanderAttack = EnemyAttack.Bully;
                    mind.CommanderMind = EnemyAttitude.Homing;
                    mind.CommanderSmarts = EnemySmarts.IntAIligent;
                    mind.CommanderBolts = EnemyBolts.AtFull;
                    DidFire = true;
                    break;
            }

            return DidFire;
        }
    }
}
