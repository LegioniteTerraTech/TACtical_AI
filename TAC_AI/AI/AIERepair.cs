using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Serialization;
using UnityEngine;

namespace TAC_AI.AI
{
    [Serializable]
    public class BlockMemory
    {   // Save the blocks!
        public BlockTypes blockType = BlockTypes.GSOAIController_111;
        public Vector3 CachePos = Vector3.zero;
        public OrthoRotation.r CacheRot = OrthoRotation.r.u000;
    }


    public class AIERepair
    {
        /// <summary>
        /// Auto-repair handler for both enemy and allied AIs
        /// </summary>
        public class DesignMemory : MonoBehaviour
        {   // Save the design on load!
            private Tank Tank;
            public AIECore.TankAIHelper thisInst;
            public bool rejectSaveAttempts = false;
            public int limitedParts = -1;   //-1 for inf

            public List<BlockMemory> SavedTech = new List<BlockMemory>();

            // Handling this
            public void Initiate(bool DoFirstSave = true)
            {
                Tank = gameObject.GetComponent<Tank>();
                thisInst = gameObject.GetComponent<AIECore.TankAIHelper>();
                thisInst.TechMemor = this;
                if (DoFirstSave)
                    SaveTech();
            }
            public void Remove()
            {
                gameObject.GetComponent<AIECore.TankAIHelper>().TechMemor = null;
                DestroyImmediate(this);
            }

            // Save operations
            public void SaveTech()
            {
                if (rejectSaveAttempts)
                    return;
                List<TankBlock> ToSave = Tank.blockman.IterateBlocks().ToList();
                SavedTech.Clear();
                foreach (TankBlock bloc in ToSave)
                {
                    BlockMemory mem = new BlockMemory();
                    mem.blockType = bloc.BlockType;
                    mem.CachePos = bloc.cachedLocalPosition;
                    mem.CacheRot = bloc.cachedLocalRotation.rot;
                    SavedTech.Add(mem);
                }
                if (ToSave.Count() == 0)
                {
                    Debug.Log("TACtical_AI: INVALID TECH DATA SAVED FOR TANK " + Tank.name);
                }
                Debug.Log("TACtical_AI:  DesignMemory - Saved " + Tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Tank.CentralBlock.cachedLocalPosition).sqrMagnitude).ToList();
                
                if (KickStart.DesignsToLog)
                {
                    Debug.Log("TACtical_AI:  DesignMemory - DESIGNS TO LOG IS ENABLED!!!");
                    TechToJSON();
                }
            }
            public void SaveTech(List<TankBlock> overwrite)
            {
                rejectSaveAttempts = true;
                SavedTech.Clear();
                foreach (TankBlock bloc in overwrite)
                {
                    BlockMemory mem = new BlockMemory();
                    mem.blockType = bloc.BlockType;
                    mem.CachePos = bloc.cachedLocalPosition;
                    mem.CacheRot = bloc.cachedLocalRotation.rot;
                    SavedTech.Add(mem);
                }
                Debug.Log("TACtical_AI:  DesignMemory - Overwrote " + Tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
            }
            public void MemoryToTech(List<BlockMemory> overwrite)
            {   // Loading a Tech from the BlockMemory
                rejectSaveAttempts = true;
                SavedTech.Clear();
                SavedTech = overwrite;
                Debug.Log("TACtical_AI:  DesignMemory - Overwrote " + Tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
            }


            // JSON
            public void TechToJSON()
            {   // Saving a Tech from the BlockMemory
                //BlockMemory mem = new BlockMemory();
                if (SavedTech.Count == 0)
                    return;
                string JSONTechRAW = JsonUtility.ToJson(SavedTech.First());
                for (int step = 1; step < SavedTech.Count; step++)
                {
                    JSONTechRAW += "|";
                    JSONTechRAW += JsonUtility.ToJson(SavedTech.ElementAt(step));
                }
                string JSONTech = "";
                foreach (char ch in JSONTechRAW)
                {
                    if (ch == '"')
                    {
                        JSONTech += "\\";
                        JSONTech += ch.ToString();
                    }
                    else
                        JSONTech += ch.ToString();
                }
                Debug.Log("TACtical_AI: " + JSONTech);
            }
            public void JSONToTech(string toLoad, bool useLimitedParts = false)
            {   // Loading a Tech from the BlockMemory
                string RAW = "";
                foreach (char ch in toLoad)
                {
                    if (ch != '\\')
                    {
                        RAW += ch.ToString();
                    }
                }
                List<BlockMemory> mem = new List<BlockMemory>();
                string blockCase = "";
                foreach (char ch in RAW)
                {
                    if (ch == '|')//new block
                    {
                        mem.Add(JsonUtility.FromJson<BlockMemory>(blockCase));
                        blockCase = "";
                    }
                    else
                        blockCase += ch.ToString();
                }
                Debug.Log("TACtical_AI:  DesignMemory: saved " + mem.Count);
                if (useLimitedParts)
                    limitedParts = mem.Count + 5;// spare parts
                MemoryToTech(mem);
            }
            public void SetupForNewTechConstruction(AIECore.TankAIHelper thisInst, string JSON, bool useLimitedParts = false)
            {
                JSONToTech(JSON, useLimitedParts);
                thisInst.PendingSystemsCheck = true;
            }

            // Load operation
            public List<BlockMemory> ReturnContents()
            {
                if (SavedTech.Count() == 0)
                {
                    Debug.Log("TACtical_AI: INVALID TECH DATA STORED FOR TANK " + Tank.name);
                }
                return new List<BlockMemory>(SavedTech);
            }
        }
        public static BlockTypes JSONToFirstBlock(string toLoad)
        {   // Loading a Tech from the BlockMemory
            string RAW = "";
            foreach (char ch in toLoad)
            {
                if (ch != '\\')
                {
                    RAW += ch.ToString();
                }
            }
            BlockMemory mem = new BlockMemory();
            string blockCase = "";
            foreach (char ch in RAW)
            {
                if (ch == '|')//new block
                {
                    mem = JsonUtility.FromJson<BlockMemory>(blockCase);
                    break;
                }
                else
                    blockCase += ch.ToString();
            }
            return mem.blockType;
        }

        //COMPLICATED MESS that re-attaches loose blocks for AI techs, does not apply to allied Techs FOR NOW.
        public static bool AttemptBlockAttach(Tank tank, BlockMemory template, TankBlock canidate, DesignMemory TechMemor)
        {
            if (TechMemor.limitedParts == 0)
            {
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  depleted block reserves!!!");
                TechMemor.thisInst.PendingSystemsCheck = false;
                return false;
            }
            bool success;
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            success = tank.blockman.AddBlockToTech(canidate, template.CachePos, new OrthoRotation(template.CacheRot));
            if (success)
            {
                if (TechMemor.limitedParts > 0)
                    TechMemor.limitedParts--;
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Attaching " + canidate.name);
                if (!KickStart.MuteNonPlayerRacket)
                {
                    FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                    FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                    soundSteal[(int)canidate.BlockConnectionAudioType].PlayOneShot();
                }
            }
            return success;
        }


        // Other respective repair operations
        public static bool RepairLerp(Tank tank, DesignMemory TechMemor, AIECore.TankAIHelper thisInst, bool overrideChecker = false)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: RepairLerp called with no valid EnemyDesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
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
        public static bool RepairStepper(AIECore.TankAIHelper thisInst, Tank tank, DesignMemory TechMemor, int Delay = 14, bool Super = false)
        {
            if (thisInst.PendingSystemsCheck && thisInst.AttemptedRepairs == 0)
            {
                RepairLerp(tank, TechMemor, thisInst);
                thisInst.PendingSystemsCheck = SystemsCheck(tank, TechMemor);
                thisInst.AttemptedRepairs = 1;
            }
            else
            {
                if (thisInst.repairClock == 1)
                {
                    thisInst.AttemptedRepairs = 0;
                    thisInst.repairClock = 0;
                }
                else if (thisInst.repairClock == 0)
                {
                    if (!Super)
                        thisInst.repairClock = Delay;
                    else
                        thisInst.repairClock = Delay / 6;

                }
                else
                    thisInst.repairClock--;
            }
            return thisInst.PendingSystemsCheck;
        }


        // Booleenssd
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
        /// <summary>
        /// Returns true if the tech is damaged and has blocks to use
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="mind"></param>
        /// <returns></returns>
        public static bool SystemsCheck(Tank tank, DesignMemory TechMemor)
        {
            if (TechMemor.ReturnContents().Count != tank.blockman.IterateBlocks().Count() && TechMemor.limitedParts != 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Returns true if the tech is damaged, has blocks to use and DesignMemory is present
        /// </summary>
        /// <param name="tank"></param>
        /// <returns></returns>
        public static bool SystemsCheck(Tank tank)
        {
            var TechMemor = tank.GetComponent<DesignMemory>();
            if (TechMemor.IsNull())
                return false;
            if (TechMemor.ReturnContents().Count != tank.blockman.IterateBlocks().Count() && TechMemor.limitedParts != 0)
            {
                return true;
            }
            return false;
        }
        public static bool IsBlockStoredInInventory(Tank tank, BlockTypes blockType, bool returnBlock = false)
        {
            if (tank.IsEnemy())
                return true;// enemies don't actually come with limited inventories.  strange right?
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


        // EXPERIMENTAL - AI-Based new Tech building
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
                if (thisInst.repairClock == 1)
                {
                    thisInst.AttemptedRepairs = 0;
                    thisInst.repairClock = 0;
                }
                else if (thisInst.repairClock == 0)
                    thisInst.repairClock = 20;
                else
                    thisInst.repairClock--;
            }
            return thisInst.PendingSystemsCheck;
        }

    }
}
