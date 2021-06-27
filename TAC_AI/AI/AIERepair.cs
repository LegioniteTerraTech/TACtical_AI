using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI
{
    public class AIERepair
    {
        /// <summary>
        /// Auto-repair handler for both enemy and allied AIs
        /// </summary>
        public class DesignMemory : MonoBehaviour
        {   // Save the design on load!
            private Tank Tank;
            public List<TankBlock> SavedTech { get; private set; }

            public void Initiate()
            {
                Tank = gameObject.GetComponent<Tank>();
            }
            public void Remove()
            {
                DestroyImmediate(this);
            }
            public List<TankBlock> ReturnContents()
            {
                if (SavedTech.Count() == 0)
                {
                    Debug.Log("TACtical_AI: INVALID TECH DATA STORED FOR TANK " + Tank.name);
                }
                return new List<TankBlock>(SavedTech);
            }
            public void SaveTech()
            {
                List<TankBlock> ToSave = Tank.blockman.IterateBlocks().ToList();
                if (ToSave.Count() == 0)
                {
                    Debug.Log("TACtical_AI: INVALID TECH DATA SAVED FOR TANK " + Tank.name);
                }
                SavedTech = new List<TankBlock>(ToSave);
            }
            public void SaveTech(List<TankBlock> overwrite)
            {
                SavedTech = new List<TankBlock>(overwrite.FindAll(delegate (TankBlock cand) { return cand != null; }));
            }
        }

        //COMPLICATED MESS that re-attaches loose blocks for AI techs, does not apply to allied Techs FOR NOW.

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
        public static bool SystemsCheck(Tank tank, DesignMemory TechMemor)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            if (savedBCount != cBCount)
            {
                return true;
            }
            return false;
        }
        public static bool RepairLerp(Tank tank, DesignMemory TechMemor, AIECore.TankAIHelper thisInst, bool overrideChecker = false)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: RepairLerp called with no valid EnemyDesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
                TechMemor.SaveTech();
                return false;
            }
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount && !overrideChecker)
            {
                if (!overrideChecker)
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  INVALID SAVED TECH DESIGN MEMORY!!!");
                TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to repair");
                List<TankBlock> rBlocks = TechMemor.ReturnContents();
                for (int step = 0; step < cBCount; step++)
                {
                    //deduct present blocks
                    rBlocks.Remove(cBlocks.ElementAt(step));
                }
                Debug.Log("TACtical AI: RepairLerp - Blocks to repair = " + rBlocks.Count);

                List<TankBlock> fBlocks = new List<TankBlock>();
                foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, (thisInst.RangeToChase / 4), new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
                {
                    if (foundBlock.block.IsNotNull())
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
                        if (thisInst.AdvancedAI)
                        {
                            // if we are smrt, run heavier operation
                            List<TankBlock> posBlocks = TechMemor.ReturnContents().FindAll(delegate (TankBlock cand) { return cand.BlockType == foundBlock.BlockType; });
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

        public static bool InstaRepair(Tank tank, DesignMemory TechMemor, int RepairAttempts = 0)
        {
            bool success = false;

            BlockManager.TableCache techCache = tank.blockman.GetTableCacheForPlacementCollection();
            List<TankBlock> cBlocks = techCache.blockTable.Cast<TankBlock>().ToList();
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            int rBCount = savedBCount - cBCount;
            if (RepairAttempts == 0)
                RepairAttempts = TechMemor.ReturnContents().Count();

            while (RepairAttempts > 0)
            {
                if (!SystemsCheck(tank, TechMemor))
                {
                    success = true;
                    break;
                }
                bool worked = RepairLerp(tank, TechMemor, tank.GetComponent<AIECore.TankAIHelper>(), true);
                if (!worked)
                    break;
                RepairAttempts--;
            }
            return success;
        }

        public static bool RepairStepper(AIECore.TankAIHelper thisInst, Tank tank, DesignMemory TechMemor, int Delay = 20, bool Super = false)
        {
            if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs == 0)
            {
                RepairLerp(tank, TechMemor, thisInst);
                thisInst.PendingSystemsCheck = SystemsCheck(tank, TechMemor);
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
                        thisInst.ActionPause = Delay;
                    else
                        thisInst.ActionPause = Delay / 6;

                }
                else
                    thisInst.ActionPause--;
            }
            return thisInst.PendingSystemsCheck;
        }


        public static void SetupForNewTechConstruction(DesignMemory TechMemor, List<TankBlock> tankTemplate)
        {
            TechMemor.SaveTech(tankTemplate.FindAll(delegate (TankBlock cand) { return cand != null; }));
        }
        public static bool NewTechConstruction(AIECore.TankAIHelper thisInst, Tank tank, DesignMemory TechMemor)
        {
            if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs == 0)
            {
                thisInst.PendingSystemsCheck = !InstaRepair(tank, TechMemor, TechMemor.ReturnContents().Count() + 10);
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
