using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public static class RRepair
    {
        //COMPLICATED MESS that re-attaches loose blocks for AI techs, does not apply to allied Techs FOR NOW.
        //  Most major operations are called from AIERepair.
        public static bool EnemyRepairLerp(Tank tank, EnemyMind mind, bool overrideChecker = false)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (mind.TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: EnemyRepairLerp called with no valid EnemyDesignMemory!!!");
                mind.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                mind.TechMemor.Initiate();
                mind.TechMemor.SaveTech();
                return false;
            }
            int savedBCount = mind.TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount && !overrideChecker)
            {
                if (!overrideChecker)
                    Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  INVALID SAVED TECH DESIGN MEMORY!!!");
                mind.TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                bool hardest = KickStart.EnemyBlockDropChance == 0;
                //Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Trying to repair");
                List<BlockTypes> typesMissing = AIERepair.GetMissingBlockTypes(mind.TechMemor, cBlocks);

                List<TankBlock> fBlocks = AIERepair.FindBlocksNearbyTank(tank, (mind.Range / 3), hardest);
                fBlocks = fBlocks.OrderBy((blok) => (blok.centreOfMassWorld - tank.boundsCentreWorld).sqrMagnitude).ToList();

                //int attachAttempts = fBlocks.Count();
                //Debug.Log("TACtical AI: EnemyRepairLerp - Found " + attachAttempts + " loose blocks to use");

                if (AIERepair.TryAttachExistingBlockFromList(tank, mind.TechMemor, fBlocks, hardest))
                    return true;

                if ((KickStart.EnemiesHaveCreativeInventory || mind.AllowInvBlocks || KickStart.AllowEnemiesToStartBases) && mind.CommanderSmarts >= EnemySmarts.Smrt)
                {
                    //Debug.Log("TACtical AI: EnemyRepairLerp - trying to fix from inventory);
                    if (AIERepair.TrySpawnAndAttachBlockFromList(tank, mind.TechMemor, typesMissing, false, true))
                        return true;
                }
            }
            return false;
        }
        public static bool EnemyInstaRepair(Tank tank, EnemyMind mind, int RepairAttempts = 0)
        {
            bool success = false;

            BlockManager.TableCache techCache = tank.blockman.GetTableCacheForPlacementCollection();
            List<TankBlock> cBlocks = techCache.blockTable.Cast<TankBlock>().ToList();
            if (RepairAttempts == 0)
                RepairAttempts = mind.TechMemor.ReturnContents().Count();

            while (RepairAttempts > 0)
            {
                if (!AIERepair.SystemsCheck(tank, mind.TechMemor))
                {
                    success = true;
                    break;
                }
                bool worked = EnemyRepairLerp(tank, mind, true);
                if (!worked)
                    break;
                RepairAttempts--;
            }
            return success;
        }
        public static bool EnemyRepairStepper(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind, int Delay = 25, bool Super = false)
        {
            if (!(bool)mind.TechMemor)
            {
                Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Tried to call EnemyRepairStepper when TechMemor is NULL");
                return false;
            }
            if (mind.TechMemor.ReserveSuperGrabs > 0)
            {
                while (mind.TechMemor.ReserveSuperGrabs > 0)
                {
                    mind.TechMemor.ReserveSuperGrabs--;
                    try
                    {
                        EnemyRepairLerp(tank, mind);
                        thisInst.AttemptedRepairs = 1;
                    }
                    catch { }
                }
                thisInst.PendingSystemsCheck = AIERepair.SystemsCheck(tank, mind.TechMemor);
            }
            else if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs == 0)
            {
                try
                {
                    EnemyRepairLerp(tank, mind);
                    thisInst.PendingSystemsCheck = AIERepair.SystemsCheck(tank, mind.TechMemor);
                    thisInst.AttemptedRepairs = 1;
                    if (mind.StartedAnchored && !(bool)thisInst.lastEnemy)   // Keep those anchors updating!
                        tank.Anchors.TryAnchorAll(true);
                }
                catch { }
            }
            else
            {
                if (thisInst.repairStepperClock == 1)
                {
                    thisInst.AttemptedRepairs = 0;
                    thisInst.repairStepperClock = 0;
                }
                else if (thisInst.repairStepperClock == 0)
                {
                    if (Templates.SpecialAISpawner.CreativeMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Backspace))
                    {
                        thisInst.repairStepperClock = 1;
                        thisInst.TechMemor.ReserveSuperGrabs = 9;
                    }
                    else if (tank.IsAnchored)
                    {
                        if (!Super)
                            thisInst.repairStepperClock = Delay / Mathf.Max(((int)mind.CommanderSmarts * 2) + 1, 1);
                        else
                            thisInst.repairStepperClock = (Delay / 4) / Mathf.Max(((int)mind.CommanderSmarts * 2) + 1, 1);
                    }
                    else
                    {
                        if (!Super)
                            thisInst.repairStepperClock = Delay / Mathf.Max((int)mind.CommanderSmarts+ 1, 1);
                        else
                            thisInst.repairStepperClock = (Delay / 4) / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                    }
                }
                else
                    thisInst.repairStepperClock--;
            }
            if (mind.TechMemor.ReserveSuperGrabs < 0)
                mind.TechMemor.ReserveSuperGrabs = 0;
            return thisInst.PendingSystemsCheck;
        }


        // EXPERIMENTAL - AI-Based new Tech building
        public static bool EnemyNewTechConstruction(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs == 0)
            {
                thisInst.PendingSystemsCheck = !EnemyInstaRepair(tank, mind, mind.TechMemor.ReturnContents().Count() + 10);
                thisInst.AttemptedRepairs = 1;
            }
            else
            {
                if (thisInst.repairStepperClock == 1)
                {
                    thisInst.AttemptedRepairs = 0;
                    thisInst.repairStepperClock = 0;
                }
                else if (thisInst.repairStepperClock == 0)
                    thisInst.repairStepperClock = 20;
                else
                    thisInst.repairStepperClock--;
            }
            return thisInst.PendingSystemsCheck;
        }
    }
}
