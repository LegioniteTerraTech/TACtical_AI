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

            public List<BlockMemory> SavedTech = new List<BlockMemory>();
            //public List<TankBlock> SavedTech { get; private set; }
            private IntVector3 GridCentre = IntVector3.zero;

            public void Initiate()
            {
                Tank = gameObject.GetComponent<Tank>();
                gameObject.GetComponent<AIECore.TankAIHelper>().TechMemor = this;
            }
            public void Remove()
            {
                gameObject.GetComponent<AIECore.TankAIHelper>().TechMemor = null;
                DestroyImmediate(this);
            }
            public IntVector3 GetCentre()
            {
                return GridCentre;
            }
            public List<BlockMemory> ReturnContents()
            {
                if (SavedTech.Count() == 0)
                {
                    Debug.Log("TACtical_AI: INVALID TECH DATA STORED FOR TANK " + Tank.name);
                }
                return new List<BlockMemory>(SavedTech);
            }
            public void SaveTech()
            {
                List<TankBlock> ToSave = Tank.blockman.IterateBlocks().ToList();
                SavedTech.Clear();
                foreach (TankBlock bloc in ToSave)
                {
                    BlockMemory mem = new BlockMemory();
                    mem.blockType = bloc.BlockType;
                    mem.CachePos = bloc.cachedLocalPosition;
                    mem.CacheRot = bloc.cachedLocalRotation;
                    SavedTech.Add(mem);
                }
                if (ToSave.Count() == 0)
                {
                    Debug.Log("TACtical_AI: INVALID TECH DATA SAVED FOR TANK " + Tank.name);
                }
                Debug.Log("TACtical_AI:  DesignMemory - Saved " + Tank.name);
                //build AROUND the cab pls
                SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Tank.CentralBlock.cachedLocalPosition).sqrMagnitude).ToList();
                IntVector3 blockCenter = IntVector3.zero;

                FieldInfo centerGet = typeof(BlockManager).GetField("m_BlockTableCentre", BindingFlags.NonPublic | BindingFlags.Instance);
                GridCentre = (IntVector3)centerGet.GetValue(Tank.blockman);
            }
            public void SaveTech(List<TankBlock> overwrite, IntVector3 GridCenter)
            {
                SavedTech.Clear();
                foreach (TankBlock bloc in overwrite)
                {
                    BlockMemory mem = new BlockMemory();
                    mem.blockType = bloc.BlockType;
                    mem.CachePos = bloc.cachedLocalPosition;
                    mem.CacheRot = bloc.cachedLocalRotation;
                    SavedTech.Add(mem);
                }
                Debug.Log("TACtical_AI:  DesignMemory - Overwrote " + Tank.name);
                SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
            }
        }

        //COMPLICATED MESS that re-attaches loose blocks for AI techs, does not apply to allied Techs FOR NOW.

        public static bool AttemptBlockAttach(Tank tank, BlockMemory template, TankBlock canidate, DesignMemory TechMemor)
        {
            bool success;
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            success = tank.blockman.AddBlockToTech(canidate, template.CachePos, template.CacheRot);
            if (success)
            {
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Attaching " + canidate.name);
                FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                soundSteal[(int)canidate.BlockConnectionAudioType].PlayOneShot();
            }
            return success;
        }

        /// <summary>
        /// Returns true if the tech is damaged
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        /// <returns></returns>
        public static bool SystemsCheck(Tank tank, DesignMemory TechMemor)
        {
            if (TechMemor.ReturnContents().Count != tank.blockman.IterateBlocks().Count())
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Returns true if the tech is damaged and DesignMemory is present
        /// </summary>
        /// <param name="tank"></param>
        /// <returns></returns>
        public static bool SystemsCheck(Tank tank)
        {
            var TechMemor = tank.GetComponent<DesignMemory>();
            if (TechMemor.IsNull())
                return false;
            if (TechMemor.ReturnContents().Count != tank.blockman.IterateBlocks().Count())
            {
                return true;
            }
            return false;
        }

        /// <summary>
        ///  Returns true if the tech can repair
        /// </summary>
        /// <param name="tank"></param>
        /// <returns></returns>
        public static bool CanRepairNow(Tank tank)
        {
            var TechMemor = tank.gameObject.GetComponent<DesignMemory>();
            if (TechMemor.IsNull())
                return false;
            var thisInst = tank.GetComponent<AIECore.TankAIHelper>();
            bool blocksNearby = false;
            foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, (thisInst.RangeToChase / 4), new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
            {
                if (foundBlock.block.IsNotNull() && foundBlock.GetComponent<WorldSpaceObject>().IsEnabled)
                {
                    if (!foundBlock.block.tank && foundBlock.holderStack == null && Singleton.Manager<ManPointer>.inst.DraggingItem != foundBlock)
                    {
                        if (foundBlock.block.PreExplodePulse)
                            continue; //explode? no thanks
                                      //Debug.Log("TACtical AI: RepairLerp - block " + foundBlock.name + " has " + cBlocks.FindAll(delegate (TankBlock cand) { return cand.blockPoolID == foundBlock.block.blockPoolID; }).Count() + " matches");
                        if (TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.blockType == foundBlock.block.BlockType; }).Count() > 0)
                        {
                            blocksNearby = true;
                            break;
                        }
                    }
                }
            }
            if (thisInst.AIState == 2)
            {
                var mind = tank.GetComponent<Enemy.RCore.EnemyMind>();
                if ((mind.AllowRepairsOnFly || (thisInst.lastEnemy.IsNull())) && (blocksNearby || KickStart.EnemiesHaveCreativeInventory || mind.AllowInfBlocks))
                {
                    return true;
                }
            }
            else if (thisInst.AIState == 1 && (blocksNearby || thisInst.useInventory))
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
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount && !overrideChecker)
            {
                if (!overrideChecker)
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  INVALID SAVED TECH DESIGN MEMORY!!!");
                TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to repair");

                List<TankBlock> fBlocks = new List<TankBlock>();
                foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, (thisInst.RangeToChase / 4), new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
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
                //Debug.Log("TACtical AI: RepairLerp - Found " + attachAttempts + " loose blocks to use");
                for (int step = 0; step < attachAttempts; step++)
                {
                    TankBlock foundBlock = fBlocks[step];
                    bool attemptW = false;
                    // if we are smrt, run heavier operation
                    List<BlockMemory> posBlocks = TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.blockType == foundBlock.BlockType; });
                    //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                    for (int step2 = 0; step2 < posBlocks.Count; step2++)
                    {
                        BlockMemory template = posBlocks.ElementAt(step2);
                        attemptW = AttemptBlockAttach(tank, template, foundBlock, TechMemor);
                        if (attemptW)
                        {
                            return true;
                        }
                    }
                }
                if (thisInst.useInventory)
                {
                    //Debug.Log("TACtical AI: RepairLerp - Attempting to repair from inventory");
                    List<BlockTypes> typesToRepair = new List<BlockTypes>();
                    int toFilter = TechMemor.ReturnContents().Count();
                    for (int step = 0; step < toFilter; step++)
                    {
                        typesToRepair.Add(TechMemor.ReturnContents().ElementAt(step).blockType);
                    }
                    typesToRepair = typesToRepair.Distinct().ToList();
                    attachAttempts = typesToRepair.Count();
                    for (int step = 0; step < attachAttempts; step++)
                    {
                        if (!IsBlockStoredInInventory(tank, typesToRepair.ElementAt(step)))
                            continue;
                        TankBlock foundBlock = Singleton.Manager<ManSpawn>.inst.SpawnItem(new ItemTypeInfo(ObjectTypes.Block, (int)typesToRepair.ElementAt(step)), tank.boundsCentreWorldNoCheck + (Vector3.up * AIECore.Extremes(tank.blockBounds.extents)), Quaternion.identity, true).block;
                        bool attemptW = false;


                        List<BlockMemory> posBlocks = TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.blockType == foundBlock.BlockType; });
                        //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                        for (int step2 = 0; step2 < posBlocks.Count; step2++)
                        {
                            BlockMemory template = posBlocks.ElementAt(step2);
                            attemptW = AttemptBlockAttach(tank, template, foundBlock, TechMemor);
                            if (attemptW)
                            {
                                return true;
                            }
                        }
                        IsBlockStoredInInventory(tank, typesToRepair.ElementAt(step), true);
                        foundBlock.trans.Recycle();
                    }
                }
            }
            return false;
        }
        public static bool IsBlockStoredInInventory(Tank tank, BlockTypes blockType , bool returnBlock = false)
        {
            if (tank.IsEnemy())
                return true;
            if (returnBlock)
            {
                if (Singleton.Manager<ManPlayer>.inst.InventoryIsUnrestricted)
                {
                    //no need to return to infinite stockpile
                }
                else
                {
                    try
                    {
                        int availQuant;
                        bool isMP = Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer();
                        if (isMP)
                        {
                            if (Singleton.Manager<NetInventory>.inst.IsAvailableToLocalPlayer(blockType))
                            {
                                availQuant = Singleton.Manager<NetInventory>.inst.GetQuantity(blockType);
                                availQuant++;
                                Singleton.Manager<NetInventory>.inst.SetBlockCount(blockType, availQuant);
                            }
                        }
                        else
                        {
                            if (Singleton.Manager<SingleplayerInventory>.inst.IsAvailableToLocalPlayer(blockType))
                            {
                                availQuant = Singleton.Manager<SingleplayerInventory>.inst.GetQuantity(blockType);
                                availQuant++;
                                Singleton.Manager<SingleplayerInventory>.inst.SetBlockCount(blockType, availQuant);
                            }
                        }
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: AI " + tank.name + ":  Tried to repair but block " + blockType.ToString() + " was not found!");
                    }
                }
                return true;
            }
            bool isAvail = false;
            if (Singleton.Manager<ManPlayer>.inst.InventoryIsUnrestricted)
            {
                isAvail = true;
            }
            else
            {
                try
                {
                    int availQuant;
                    bool isMP = Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer();
                    if (isMP)
                    {
                        if (Singleton.Manager<NetInventory>.inst.IsAvailableToLocalPlayer(blockType))
                        {
                            availQuant = Singleton.Manager<NetInventory>.inst.GetQuantity(blockType);
                            if (availQuant > 0)
                            {
                                availQuant--;
                                isAvail = true;
                                Singleton.Manager<NetInventory>.inst.SetBlockCount(blockType, availQuant);
                            }
                        }
                    }
                    else
                    {
                        if (Singleton.Manager<SingleplayerInventory>.inst.IsAvailableToLocalPlayer(blockType))
                        {
                            availQuant = Singleton.Manager<SingleplayerInventory>.inst.GetQuantity(blockType);
                            if (availQuant > 0)
                            {
                                availQuant--;
                                isAvail = true;
                                Singleton.Manager<SingleplayerInventory>.inst.SetBlockCount(blockType, availQuant);
                            }
                        }
                    }
                }
                catch
                {
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  Tried to repair but block " + blockType.ToString() + " was not found!");
                }
            }
            return isAvail;
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


        public static void SetupForNewTechConstruction(DesignMemory TechMemor, List<TankBlock> tankTemplate, IntVector3 GridCenter)
        {
            TechMemor.SaveTech(tankTemplate.FindAll(delegate (TankBlock cand) { return cand != null; }), GridCenter);
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
