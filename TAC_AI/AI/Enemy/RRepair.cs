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
        //COMPLICATED MESS that re-attaches loose blocks for AI techs, doe not apply to allied Techs FOR NOW.

        public static bool AttemptBlockAttach(Tank tank, TankBlock template, TankBlock canidate)
        {
            return tank.blockman.AddBlockToTech(canidate, template.cachedLocalPosition, template.cachedLocalRotation);
        }

        /// <summary>
        /// Returns true if the tech is damaged
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        /// <returns></returns>
        public static bool SystemsCheck(Tank tank, RCore.EnemyMind mind)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            int savedBCount = mind.TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            if (savedBCount != cBCount)
            {
                return true;
            }
            return false;
        }
        public static bool RepairLerp(Tank tank, RCore.EnemyMind mind, bool overrideChecker = false)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (mind.TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: RepairLerp called with no valid EnemyDesignMemory!!!");
            }
            int savedBCount = mind.TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount && !overrideChecker)
            {
                if (!overrideChecker)
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  INVALID SAVED TECH DESIGN MEMORY!!!");
                mind.TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to repair");
                List<TankBlock> rBlocks = mind.TechMemor.ReturnContents();
                for (int step = 0; step < cBCount; step++)
                {
                    //deduct present blocks
                    rBlocks.Remove(cBlocks.ElementAt(step));
                }
                Debug.Log("TACtical AI: RepairLerp - Blocks to repair = "+ rBlocks.Count);

                List<TankBlock> fBlocks = new List<TankBlock>();
                foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, (mind.Range / 3), new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
                {
                    if (foundBlock.block.IsNotNull())
                    {
                        if (!foundBlock.block.tank && foundBlock.holderStack == null && Singleton.Manager<ManPointer>.inst.DraggingItem != foundBlock)
                        {
                            //Debug.Log("TACtical AI: RepairLerp - block " + foundBlock.name + " has " + cBlocks.FindAll(delegate (TankBlock cand) { return cand.blockPoolID == foundBlock.block.blockPoolID; }).Count() + " matches");
                            fBlocks.Add(foundBlock.block);
                        }
                    }
                }
                fBlocks = fBlocks.OrderBy((blok) => (blok.centreOfMassWorld - tank.boundsCentreWorld).sqrMagnitude).ToList();
                int attachAttempts = fBlocks.Count();
                Debug.Log("TACtical AI: RepairLerp - Found " + attachAttempts + " loose blocks to use");
                for (int step = 0; step < attachAttempts; step++)
                {
                    TankBlock foundBlock = fBlocks[step];
                    bool attemptW = false;
                    int templateGet = rBlocks.IndexOf(foundBlock);
                    if (templateGet != -1)
                    {
                        TankBlock template = rBlocks.ElementAt(templateGet);
                        attemptW = AttemptBlockAttach(tank, template, foundBlock);
                    }

                    if (!attemptW)
                    {
                        // Are we a smart enemy?
                        if (mind.CommanderSmarts >= EnemySmarts.Smrt)
                        {
                            // if we are smrt, run heavier operation
                            List<TankBlock> posBlocks = mind.TechMemor.ReturnContents().FindAll(delegate (TankBlock cand) { return cand.BlockType == foundBlock.BlockType; });
                            //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                            for (int step2 = 0; step2 < posBlocks.Count; step2++)
                            {
                                TankBlock template = posBlocks.ElementAt(step2);
                                attemptW = AttemptBlockAttach(tank, template, foundBlock);
                                if (attemptW)
                                {
                                    Debug.Log("TACtical_AI: AI " + tank.name + ":  Attaching " + foundBlock.name);
                                    FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                                    FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                                    soundSteal[(int)foundBlock.BlockConnectionAudioType].PlayOneShot();
                                    return true;
                                }
                            }
                        }
                        else
                            Debug.Log("TACtical_AI: AI " + tank.name + ":  Could not attach " + foundBlock.name);
                        // if not we fail
                    }
                    else
                    {
                        Debug.Log("TACtical_AI: AI " + tank.name + ":  Attaching " + foundBlock.name);
                        FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                        FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                        soundSteal[(int)foundBlock.BlockConnectionAudioType].PlayOneShot();
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool InstaRepair(Tank tank, RCore.EnemyMind mind, int RepairAttempts = 0)
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
                if (!SystemsCheck(tank, mind))
                {
                    success = true;
                    break;
                }
                bool worked = RepairLerp(tank, mind, true);
                if (!worked)
                    break;
                RepairAttempts--;
            }
            return success;
        }

        public static bool RepairStepper(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind, int Delay = 35, bool Super = false)
        {
            if (mind.PendingSystemsCheck && mind.AttemptedRepairs == 0)
            {
                RepairLerp(tank, mind);
                mind.PendingSystemsCheck = SystemsCheck(tank, mind);
                mind.AttemptedRepairs = 1;
            }
            else
            {
                if (thisInst.ActionPause == 1)
                {
                    mind.AttemptedRepairs = 0;
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
            return mind.PendingSystemsCheck;
        }


        public static void SetupForNewTechConstruction(RCore.EnemyMind mind, List<TankBlock> tankTemplate)
        {
            mind.TechMemor.SaveTech(tankTemplate.FindAll(delegate (TankBlock cand) { return cand != null; }));
        }
        public static bool NewTechConstruction(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (mind.PendingSystemsCheck && mind.AttemptedRepairs == 0)
            {
                mind.PendingSystemsCheck = !InstaRepair(tank, mind, mind.TechMemor.ReturnContents().Count() + 10);
                mind.AttemptedRepairs = 1;
            }
            else
            {
                if (thisInst.ActionPause == 1)
                {
                    mind.AttemptedRepairs = 0;
                    thisInst.ActionPause = 0;
                }
                else if (thisInst.ActionPause == 0)
                    thisInst.ActionPause = 20;
                else
                    thisInst.ActionPause--;
            }
            return mind.PendingSystemsCheck;
        }
    }
}
