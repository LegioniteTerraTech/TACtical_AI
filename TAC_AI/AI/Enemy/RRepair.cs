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
            Debug.Log("TACtical_AI: AI " + tank.name + ":  Replaced block " + template.name);
            return tank.blockman.AddBlockToTech(canidate, template.cachedLocalPosition, template.cachedLocalRotation);
        }
        public static bool RepairLerp(Tank tank, RCore.EnemyMind mind, bool overrideChecker = false)
        {
            bool success = false;
            BlockManager.TableCache techCache = tank.blockman.GetTableCacheForPlacementCollection();
            List<TankBlock> cBlocks = techCache.blockTable.Cast<TankBlock>().ToList();
            int savedBCount = mind.TechMemor.SavedTech.Count;
            int cBCount = cBlocks.Count;
            if (savedBCount < cBCount && !overrideChecker)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  INVALID SAVED TECH DESIGN MEMORY!!!");
                mind.TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                List<TankBlock> rBlocks = mind.TechMemor.SavedTech;
                for (int step = 0; step < savedBCount; step++)
                {
                    //deduct present blocks
                    if (cBlocks.Contains(rBlocks.ElementAt(step)))
                        rBlocks.RemoveAt(step);
                }
                
                int sees = tank.Vision.VisibleCount;
                for (int step = 0; step < sees; step++)
                {
                    TankBlock foundBlock = tank.Vision.IterateVisibles().Current.block;
                    if (foundBlock.IsNull())
                    {
                        tank.Vision.IterateVisibles().MoveNext();
                        continue;
                    }
                    else
                    {
                        if (foundBlock.tank.IsNull() && foundBlock.visible.holderStack.myHolder.IsNull())
                        {
                            if (rBlocks.Contains(foundBlock))
                            {
                                TankBlock template = rBlocks.ElementAt(rBlocks.IndexOf(foundBlock));
                                bool attemptW = AttemptBlockAttach(tank, template, foundBlock);

                                if (!attemptW)
                                {
                                    // Are we a smart enemy?
                                    if (mind.CommanderSmarts >= EnemySmarts.Smrt)
                                    {
                                        // if we are smrt, run heavier operation
                                        List<TankBlock> posBlocks = rBlocks.FindAll(delegate (TankBlock cand) { return cand == foundBlock; });
                                        for (int step2 = 0; step2 < posBlocks.Count; step2++)
                                        {
                                            template = posBlocks.ElementAt(step);
                                            attemptW = AttemptBlockAttach(tank, template, foundBlock);
                                            if (attemptW)
                                            {
                                                Debug.Log("TACtical AI: RepairLerp - potential failiure point reached");
                                                FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                                                FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                                                soundSteal[(int)foundBlock.BlockConnectionAudioType].PlayOneShot();
                                                break;
                                            }
                                        }
                                    }
                                    // if not we fail
                                }
                                else
                                {
                                    Debug.Log("TACtical AI: RepairLerp - potential failiure point reached");
                                    FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                                    FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                                    soundSteal[(int)foundBlock.BlockConnectionAudioType].PlayOneShot();
                                }
                            }
                        }
                    }
                }
            }
            else
                return false;
            return success;
        }

        public static bool InstaRepair(Tank tank, RCore.EnemyMind mind, int RepairAttempts = 0)
        {
            bool success = false;

            BlockManager.TableCache techCache = tank.blockman.GetTableCacheForPlacementCollection();
            List<TankBlock> cBlocks = techCache.blockTable.Cast<TankBlock>().ToList();
            int savedBCount = mind.TechMemor.SavedTech.Count;
            int cBCount = cBlocks.Count;
            int rBCount = savedBCount - cBCount;
            if (RepairAttempts == 0)
                RepairAttempts = mind.TechMemor.SavedTech.Count();

            while (RepairAttempts > 0)
            {
                if (rBCount < 1)
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

        public static bool MobileRepairs(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (mind.PendingSystemsCheck && mind.AttemptedRepairs == 0)
            {
                mind.PendingSystemsCheck = !RepairLerp(tank, mind);
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
                    thisInst.ActionPause = 20 / (int)mind.CommanderSmarts;
                else
                    thisInst.ActionPause--;
            }
            return mind.PendingSystemsCheck;
        }

        public static void SetupForNewTechConstruction(RCore.EnemyMind mind, List<TankBlock> tankTemplate)
        {
            mind.TechMemor.SavedTech = tankTemplate;
        }
        public static bool NewTechConstruction(AIECore.TankAIHelper thisInst, Tank tank, RCore.EnemyMind mind)
        {
            if (mind.PendingSystemsCheck && mind.AttemptedRepairs == 0)
            {
                mind.PendingSystemsCheck = !InstaRepair(tank, mind, mind.TechMemor.SavedTech.Count());
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
