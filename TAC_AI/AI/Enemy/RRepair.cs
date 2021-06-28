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

        public static bool EnemyRepairLerp(Tank tank, RCore.EnemyMind mind, bool overrideChecker = false)
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
            Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount && !overrideChecker)
            {
                if (!overrideChecker)
                    Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  INVALID SAVED TECH DESIGN MEMORY!!!");
                mind.TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                //Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Trying to repair");
                List<BlockMemory> rBlocks = mind.TechMemor.ReturnContents();

                List<TankBlock> fBlocks = new List<TankBlock>();
                foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, (mind.Range / 3), new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
                {
                    if (foundBlock.block.IsNotNull() && foundBlock.GetComponent<WorldSpaceObject>().IsEnabled)
                    {
                        if (!foundBlock.block.tank && foundBlock.holderStack == null && Singleton.Manager<ManPointer>.inst.DraggingItem != foundBlock)
                        {
                            if (foundBlock.block.PreExplodePulse)
                                continue; //explode? no thanks
                            //Debug.Log("TACtical AI: RepairLerp - block " + foundBlock.name + " has " + cBlocks.FindAll(delegate (TankBlock cand) { return cand.blockPoolID == foundBlock.block.blockPoolID; }).Count() + " matches");
                            fBlocks.Add(foundBlock.block);
                        }
                    }
                }
                fBlocks = fBlocks.OrderBy((blok) => (blok.centreOfMassWorld - tank.boundsCentreWorld).sqrMagnitude).ToList();
                int attachAttempts = fBlocks.Count();
                //Debug.Log("TACtical AI: EnemyRepairLerp - Found " + attachAttempts + " loose blocks to use");
                for (int step = 0; step < attachAttempts; step++)
                {
                    TankBlock foundBlock = fBlocks[step];
                    bool attemptW = false;
                    // if we are smrt, run heavier operation
                    List<BlockMemory> posBlocks = mind.TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.blockType == foundBlock.BlockType; });
                    //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                    for (int step2 = 0; step2 < posBlocks.Count; step2++)
                    {
                        BlockMemory template = posBlocks.ElementAt(step2);
                        attemptW = AIERepair.AttemptBlockAttach(tank, template, foundBlock, mind.TechMemor);
                        if (attemptW)
                        {
                            return true;
                        }
                    }
                    //else
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Could not attach " + foundBlock.name);
                    // if not we fail
                }
                if ((KickStart.EnemiesHaveCreativeInventory || mind.AllowInfBlocks) && mind.CommanderSmarts >= EnemySmarts.Smrt)
                {
                    List<BlockTypes> typesToRepair = new List<BlockTypes>();
                    int toFilter = mind.TechMemor.ReturnContents().Count();
                    for (int step = 0; step < toFilter; step++)
                    {
                        typesToRepair.Add(mind.TechMemor.ReturnContents().ElementAt(step).blockType);
                    }
                    typesToRepair = typesToRepair.Distinct().ToList();
                    attachAttempts = typesToRepair.Count();
                    //Debug.Log("TACtical AI: EnemyRepairLerp - Attempting to repair from inventory, fetched " + attachAttempts + " differing blocktype(s)");
                    //foreach (BlockTypes type in typesToRepair)
                    //{
                        //Debug.Log("TACtical AI: EnemyRepairLerp - " + type.ToString());
                    //}
                    for (int step = 0; step < attachAttempts; step++)
                    {
                        TankBlock foundBlock = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Block, (int)typesToRepair.ElementAt(step)), tank.boundsCentreWorldNoCheck + (Vector3.up * AIECore.Extremes(tank.blockBounds.extents)), Quaternion.identity, true).block;
                        bool attemptW = false;
                        //Debug.Log("TACtical AI: EnemyRepairLerp - pulled block " + foundBlock.name);

                        List<BlockMemory> posBlocks = mind.TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.blockType == typesToRepair.ElementAt(step); });
                        //Debug.Log("TACtical AI: RepairLerp - potential spots " + posBlocks.Count + " for block " + foundBlock);
                        for (int step2 = 0; step2 < posBlocks.Count; step2++)
                        {
                            BlockMemory template = posBlocks[step2];
                            attemptW = AIERepair.AttemptBlockAttach(tank, template, foundBlock, mind.TechMemor);
                            if (attemptW)
                            {
                                return true;
                            }
                            //else
                                //Debug.Log("TACtical AI: EnemyRepairLerp - Attach attempt failed");
                        }
                        foundBlock.trans.Recycle();
                        //Debug.Log("TACtical AI: EnemyRepairLerp - returned block");
                    }
                }
            }
            return false;
        }

        public static bool EnemyInstaRepair(Tank tank, RCore.EnemyMind mind, int RepairAttempts = 0)
        {
            bool success = false;

            BlockManager.TableCache techCache = tank.blockman.GetTableCacheForPlacementCollection();
            List<TankBlock> cBlocks = techCache.blockTable.Cast<TankBlock>().ToList();
            int savedBCount = mind.TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            int rBCount = savedBCount - cBCount;
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

        public static bool EnemyRepairStepper(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind, int Delay = 35, bool Super = false)
        {
            if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs == 0)
            {
                EnemyRepairLerp(tank, mind);
                thisInst.PendingSystemsCheck = AIERepair.SystemsCheck(tank, mind.TechMemor);
                thisInst.AttemptedRepairs = 1;
            }
            else
            {
                if (thisInst.ActionPause == 1)
                {
                    thisInst.AttemptedRepairs = 0;
                    thisInst.ActionPause = 0;
                }
                else if (thisInst.ActionPause == 0)
                {
                    if (!Super)
                        thisInst.ActionPause = Delay / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                    else
                        thisInst.ActionPause = (Delay / 6) / Mathf.Max((int)mind.CommanderSmarts + 1, 1);

                }
                else
                    thisInst.ActionPause--;
            }
            return thisInst.PendingSystemsCheck;
        }


        public static void SetupForNewEnemyTechConstruction(RCore.EnemyMind mind, List<TankBlock> tankTemplate, IntVector3 GridCenter)
        {
            mind.TechMemor.SaveTech(tankTemplate.FindAll(delegate (TankBlock cand) { return cand != null; }), GridCenter);
        }
        public static bool EnemyNewTechConstruction(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs == 0)
            {
                thisInst.PendingSystemsCheck = !EnemyInstaRepair(tank, mind, mind.TechMemor.ReturnContents().Count() + 10);
                thisInst.AttemptedRepairs = 1;
            }
            else
            {
                if (thisInst.ActionPause == 1)
                {
                    thisInst.AttemptedRepairs = 0;
                    thisInst.ActionPause = 0;
                }
                else if (thisInst.ActionPause == 0)
                    thisInst.ActionPause = 20;
                else
                    thisInst.ActionPause--;
            }
            return thisInst.PendingSystemsCheck;
        }
    }
}
