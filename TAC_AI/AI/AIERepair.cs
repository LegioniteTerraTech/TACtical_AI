using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
            public bool unlimitedParts = false;
            public bool ranOutOfParts = false;

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
                StringBuilder JSONTechRAW = new StringBuilder();
                JSONTechRAW.Append(JsonUtility.ToJson(SavedTech.First()));
                for (int step = 1; step < SavedTech.Count; step++)
                {
                    JSONTechRAW.Append("|");
                    JSONTechRAW.Append(JsonUtility.ToJson(SavedTech.ElementAt(step)));
                }
                string JSONTechRAWout = JSONTechRAW.ToString();
                StringBuilder JSONTech = new StringBuilder();
                foreach (char ch in JSONTechRAWout)
                {
                    if (ch == '"')
                    {
                        JSONTech.Append("\\");
                        JSONTech.Append(ch);
                    }
                    else
                        JSONTech.Append(ch);
                }
                Debug.Log("TACtical_AI: " + JSONTech.ToString());
            }
            public void JSONToTech(string toLoad)
            {   // Loading a Tech from the BlockMemory
                StringBuilder RAW = new StringBuilder();
                foreach (char ch in toLoad)
                {
                    if (ch != '\\')
                    {
                        RAW.Append(ch);
                    }
                }
                List<BlockMemory> mem = new List<BlockMemory>();
                StringBuilder blockCase = new StringBuilder();
                string RAWout = RAW.ToString();
                foreach (char ch in RAWout)
                {
                    if (ch == '|')//new block
                    {
                        mem.Add(JsonUtility.FromJson<BlockMemory>(blockCase.ToString()));
                        blockCase.Clear();
                    }
                    else
                        blockCase.Append(ch);
                }
                Debug.Log("TACtical_AI:  DesignMemory: saved " + mem.Count);
                MemoryToTech(mem);
            }
            public void SetupForNewTechConstruction(AIECore.TankAIHelper thisInst, string JSON)
            {
                JSONToTech(JSON);
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
            StringBuilder RAW = new StringBuilder();
            foreach (char ch in toLoad)
            {
                if (ch != '\\')
                {
                    RAW.Append(ch);
                }
            }
            BlockMemory mem = new BlockMemory();
            string RAWout = RAW.ToString();
            StringBuilder blockCase = new StringBuilder();
            foreach (char ch in RAWout)
            {
                if (ch == '|')//new block
                {
                    mem = JsonUtility.FromJson<BlockMemory>(blockCase.ToString());
                    break;
                }
                else
                    blockCase.Append(ch);
            }
            return mem.blockType;
        }

        //COMPLICATED MESS that re-attaches loose blocks for AI techs, does not apply to allied Techs FOR NOW.
        public static bool AttemptBlockAttach(Tank tank, BlockMemory template, TankBlock canidate, DesignMemory TechMemor, bool useLimitedSupplies = false)
        {
            if (!TechMemor.unlimitedParts && useLimitedSupplies)
            {
                if (!Enemy.RBases.PurchasePossible(canidate.BlockType, tank.Team))
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  depleted block reserves!!!");
                    TechMemor.ranOutOfParts = true;
                    TechMemor.thisInst.PendingSystemsCheck = false;
                    return false;
                }
            }
            TechMemor.ranOutOfParts = false;
            bool success;
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            success = Singleton.Manager<ManLooseBlocks>.inst.RequestAttachBlock(tank, canidate, template.CachePos, new OrthoRotation(template.CacheRot));
            if (success)
            { 
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                if (!TechMemor.unlimitedParts && useLimitedSupplies)
                {
                    if (Enemy.RBases.TryMakePurchase(canidate.BlockType, tank.Team))
                    {
                        Debug.Log("TACtical_AI: AI " + tank.name + ": bought " + canidate + " from the shop for " + Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(canidate.BlockType, true));

                        if (!KickStart.MuteNonPlayerRacket)
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                        return true;
                    }
                }

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
                List<BlockTypes> typesMissing = GetMissingBlockTypes(TechMemor, cBlocks);

                List<TankBlock> fBlocks = FindBlocksNearbyTank(tank, (thisInst.RangeToChase / 4));
                fBlocks = fBlocks.OrderBy((blok) => (blok.centreOfMassWorld - tank.boundsCentreWorld).sqrMagnitude).ToList();

                if (TryAttachExistingBlockFromList(tank, TechMemor, fBlocks))
                    return true;
                if (thisInst.useInventory)
                {
                    //Debug.Log("TACtical AI: RepairLerp - Attempting to repair from inventory");
                    if (TrySpawnAndAttachBlockFromList(tank, TechMemor, typesMissing, true))
                        return true;
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
        public static bool RepairStepper(AIECore.TankAIHelper thisInst, Tank tank, DesignMemory TechMemor, int Delay = 25, bool Super = false)
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
                        thisInst.repairClock = Delay / 4;

                }
                else
                    thisInst.repairClock--;
            }
            return thisInst.PendingSystemsCheck;
        }


        // Repair Utilities
        public static List<BlockTypes> GetMissingBlockTypes(DesignMemory TechMemor, List<TankBlock> cBlocks)
        {
            List<BlockTypes> typesToRepair = new List<BlockTypes>();
            int toFilter = TechMemor.ReturnContents().Count();
            for (int step = 0; step < toFilter; step++)
            {
                typesToRepair.Add(TechMemor.ReturnContents().ElementAt(step).blockType);
            }
            typesToRepair = typesToRepair.Distinct().ToList();

            List<BlockTypes> typesMissing = new List<BlockTypes>();
            int toFilter2 = typesToRepair.Count();
            for (int step = 0; step < toFilter2; step++)
            {
                int present = cBlocks.FindAll(delegate (TankBlock cand) { return typesToRepair[step] == cand.BlockType; }).Count;
                int mem = TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return typesToRepair[step] == cand.blockType; }).Count;
                if (mem > present)// are some blocks not accounted for?
                    typesMissing.Add(typesToRepair[step]);
            }
            return typesMissing;
        }
        public static List<TankBlock> FindBlocksNearbyTank(Tank tank, float radius)
        {
            List <TankBlock> fBlocks = new List<TankBlock>();
            foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, radius, new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
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
            return fBlocks;
        }
        public static bool TryAttachExistingBlockFromList(Tank tank, DesignMemory TechMemor, List<TankBlock> foundBlocks)
        {
            int attachAttempts = foundBlocks.Count();
            //Debug.Log("TACtical AI: RepairLerp - Found " + attachAttempts + " loose blocks to use");
            for (int step = 0; step < attachAttempts; step++)
            {
                TankBlock foundBlock = foundBlocks[step];
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
            return false;
        }
        public static bool TrySpawnAndAttachBlockFromList(Tank tank, DesignMemory TechMemor, List<BlockTypes> typesMissing, bool playerInventory = false)
        {
            int attachAttempts = typesMissing.Count();
            for (int step = 0; step < attachAttempts; step++)
            {
                if (playerInventory)
                    if (!IsBlockStoredInInventory(tank, typesMissing.ElementAt(step)))
                        continue;
                TankBlock foundBlock = Singleton.Manager<ManSpawn>.inst.SpawnBlock(typesMissing.ElementAt(step), tank.boundsCentreWorldNoCheck + (Vector3.up * AIECore.Extremes(tank.blockBounds.extents)), Quaternion.identity);
                bool attemptW = false;

                List<BlockMemory> posBlocks = TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.blockType == foundBlock.BlockType; });
                //Debug.Log("TACtical AI: TurboRepair - potental spots " + posBlocks.Count + " for block " + foundBlock);
                for (int step2 = 0; step2 < posBlocks.Count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = AttemptBlockAttach(tank, template, foundBlock, TechMemor);
                    if (attemptW)
                    {
                        foundBlock.InitNew();
                        return true;
                    }
                }
                if (playerInventory)
                    IsBlockStoredInInventory(tank, typesMissing.ElementAt(step), true);
                //Debug.Log("TACtical AI: TurboRepair - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

                foundBlock.transform.Recycle();
                // if everything fails, resort to timbuktu
                //foundBlock.damage.SelfDestruct(0.1f);
                //Vector3 yeet = Vector3.forward * 450000;
                //foundBlock.transform.position = yeet;
            }
            return false;
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
                var mind = tank.GetComponent<Enemy.EnemyMind>();
                if ((mind.AllowRepairsOnFly || (thisInst.lastEnemy.IsNull())) && (blocksNearby || KickStart.EnemiesHaveCreativeInventory || mind.AllowInvBlocks))
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
            if (TechMemor.ReturnContents().Count != tank.blockman.IterateBlocks().Count() && !TechMemor.ranOutOfParts)
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
            if (TechMemor.ReturnContents().Count != tank.blockman.IterateBlocks().Count() && !TechMemor.ranOutOfParts)
            {
                return true;
            }
            return false;
        }
        public static bool SystemsCheckBolts(Tank tank, DesignMemory TechMemor)
        {
            if (TechMemor.ReturnContents().Count != tank.blockman.IterateBlocks().Count())
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
        
        /// <summary>
        /// Builds a Tech instantly, no requirements
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="TechMemor"></param>
        public static void Turboconstruct(Tank tank, DesignMemory TechMemor, bool fullyCharge = true)
        {
            Debug.Log("TACtical_AI:  DesignMemory: Turboconstructing " + tank.name);
            int cBCount = tank.blockman.IterateBlocks().ToList().Count();
            int RepairAttempts = TechMemor.ReturnContents().Count() - cBCount;
            try
            {
                while (RepairAttempts > 0)
                {
                    TurboRepair(tank, TechMemor);
                    RepairAttempts--;
                }
            }
            catch { return; }
            if (fullyCharge)
                tank.EnergyRegulator.SetAllStoresAmount(1);
        }
        public static void TurboRepair(Tank tank, DesignMemory TechMemor)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: TurboRepair called with no valid EnemyDesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
                return;
            }
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount)
            {
                TechMemor.SaveTech();
                return;
            }
            if (savedBCount != cBCount)
            {
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to repair");
                List<BlockTypes> typesMissing = GetMissingBlockTypes(TechMemor, cBlocks);

                //Debug.Log("TACtical AI: TurboRepair - Attempting to repair from infinity - " + typesToRepair.Count());
                TrySpawnAndAttachBlockFromList(tank, TechMemor, typesMissing);
            }
            return;
        }

    }
}
