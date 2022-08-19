using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.Serialization;
using UnityEngine.Networking;
using UnityEngine;
using TAC_AI.Templates;
using TerraTechETCUtil;

namespace TAC_AI.AI
{
    [Serializable]
    public class BlockMemory
    {   // Save the blocks!
        public string t = BlockTypes.GSOAIController_111.ToString(); //blocktype
        public Vector3 p = Vector3.zero;
        public OrthoRotation.r r = OrthoRotation.r.u000;

        /// <summary>
        /// get rid of floating point errors
        /// </summary>
        public void TidyUp()
        {
            p.x = Mathf.RoundToInt(p.x);
            p.y = Mathf.RoundToInt(p.y);
            p.z = Mathf.RoundToInt(p.z);
        }
    }
    /*
    [Serializable]
    public class BlockMemoryLegacy
    {   // Save the blocks!
        public BlockTypes blockType = BlockTypes.GSOAIController_111;
        public Vector3 CachePos = Vector3.zero;
        public OrthoRotation.r CacheRot = OrthoRotation.r.u000;
    }
    */

    public class AIERepair
    {
        /// <summary>
        /// Auto-repair handler for both enemy and allied AIs
        /// </summary>

        // The Delay in how long 
        public static float RepairDelayMulti = 5;
        public static float RepairDelayCombatMulti = 3;
        public static float RepairDelayCombatMultiBase = 5;
        // ALLIED
        // Basic AIs
        public static short DelayNormal = 8;
        // Smarter AIs
        public static short DelaySmart = 2;
        // ENEMY
        // General Enemy AIs
        public static short DelayEnemy = 60; // this is divided by 2 later on
        // Enemy Base AIs
        public static short DelayBase = 30; // Divided by difficulty

        // STATUS UPDATE
        public static bool NonPlayerAttachAllow = false;
        public static bool BulkAdding = false;

        // -- Calculated --
        // Basic AIs
        internal static short delaySafe;
        internal static short delayCombat;
        // Smarter AIs
        internal static short sDelaySafe;
        internal static short sDelayCombat;
        // Enemy Mobile AIs
        internal static short eDelaySafe;
        internal static short eDelayCombat;
        // Enemy Base AIs
        internal static short bDelaySafe;
        internal static short bDelayCombat;

        public static void RefreshDelays()
        {
            delaySafe = (short)(DelayNormal * RepairDelayMulti);
            delayCombat = (short)(DelayNormal * RepairDelayMulti * RepairDelayCombatMulti);

            sDelaySafe = (short)(DelaySmart * RepairDelayMulti);
            sDelayCombat = (short)(DelaySmart * RepairDelayMulti * RepairDelayCombatMulti);

            eDelaySafe = (short)(DelayEnemy * RepairDelayMulti);
            eDelayCombat = (short)(DelayEnemy * RepairDelayMulti * RepairDelayCombatMulti);

            bDelaySafe = (short)((DelayBase * RepairDelayMulti) / KickStart.BaseDifficulty);
            bDelayCombat = (short)((DelayBase * RepairDelayMulti) / (KickStart.BaseDifficulty / RepairDelayCombatMultiBase));
        }

        public class DesignMemory : MonoBehaviour
        {   // Save the design on load!
            private Tank tank;
            public AIECore.TankAIHelper thisInst;
            public bool rejectSaveAttempts = false;
            public bool ranOutOfParts = false;
            public bool conveyorsBorked = false;
            public int ReserveSuperGrabs = 0;
            public Func<BlockTypes, bool> purchaseOp;
            private BlockMemory templateCache;

            private List<BlockMemory> SavedTech = new List<BlockMemory>();
            public bool blockIntegrityDirty = true;
            private Dictionary<BlockTypes, List<BlockMemory>> fastBlockLookup = new Dictionary<BlockTypes, List<BlockMemory>>();
            private List<BlockTypes> MissingTypes = new List<BlockTypes>();

            // Handling this
            public void Initiate(bool DoFirstSave = true)
            {
                tank = gameObject.GetComponent<Tank>();
                thisInst = gameObject.GetComponent<AIECore.TankAIHelper>();
                tank.DetachEvent.Subscribe(Compromised);
                thisInst.FinishedRepairEvent.Subscribe(OnFinishedBuilding);
                thisInst.TechMemor = this;
                thisInst.PendingDamageCheck = true;
                blockIntegrityDirty = true;
                rejectSaveAttempts = false;
                purchaseOp = EnemyPurchase;
                if (DoFirstSave)
                {
                    SaveTech();
                    //Invoke("SaveTech", 0.01f);
                }
            }
            public void Remove()
            {
                CancelInvoke();
                tank.DetachEvent.Unsubscribe(Compromised);
                thisInst.FinishedRepairEvent.Unsubscribe(OnFinishedBuilding);
                gameObject.GetComponent<AIECore.TankAIHelper>().TechMemor = null;
                DestroyImmediate(this);
            }

            public void OnFinishedBuilding()
            {
                if (conveyorsBorked)
                {
                    RawTechLoader.ReconstructConveyorSequencing(tank);
                    conveyorsBorked = false;
                }
            }
            public void Compromised(TankBlock removedBlock, Tank tank)
            {
                if (thisInst.AIState == AIAlignment.Player)
                {
                    if (!ManNetwork.IsNetworked)
                    {
                        if (!thisInst.BoltsFired && ManPointer.inst.targetVisible)
                        {
                            if (removedBlock == ManPointer.inst.targetVisible.block)
                            {
                                Invoke("SaveTech", 0.01f);
                                return;
                            }
                        }
                    }
                    else if (ManNetwork.IsHost)
                    {
                        if (thisInst.lastPlayer)
                        {
                            if (thisInst.lastEnemy && !thisInst.BoltsFired)// only save when not in combat Or exploding bolts
                            {
                                Invoke("SaveTech", 0.01f);
                                return;
                            }
                        }
                    }
                }
                blockIntegrityDirty = true;
                if (removedBlock.GetComponent<ModuleItemConveyor>())
                    conveyorsBorked = true;
            }


            // Save operations
            public void SaveTech()
            {
                if (rejectSaveAttempts)
                    return;
                blockIntegrityDirty = false;
                MissingTypes.Clear();

                if (KickStart.DesignsToLog)
                {
                    DebugTAC_AI.Log("TACtical_AI:  DesignMemory - DESIGNS TO LOG IS ENABLED!!!");
                    TechToJSONLog();
                    return;
                }
                List<TankBlock> ToSave = tank.blockman.IterateBlocks().ToList();
                SavedTech.Clear();

                foreach (TankBlock bloc in ToSave)
                {
                    BlockMemory mem = new BlockMemory
                    {
                        t = bloc.name,
                        p = bloc.cachedLocalPosition,
                        r = bloc.cachedLocalRotation.rot
                    };
                    SavedTech.Add(mem);
                }
                if (ToSave.Count() == 0)
                {
                    DebugTAC_AI.Info("TACtical_AI: INVALID TECH DATA SAVED FOR TANK " + tank.name + "\n" +StackTraceUtility.ExtractStackTrace());
                }
                DebugTAC_AI.Info("TACtical_AI:  DesignMemory - Saved " + tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - tank.CentralBlock.cachedLocalPosition).sqrMagnitude).ToList();
                ValidateTechIfNeeded();
                BuildTechQuickLookup();
            }
            public void SaveTech(List<TankBlock> overwrite)
            {
                rejectSaveAttempts = true;
                blockIntegrityDirty = true;
                MissingTypes.Clear();
                SavedTech.Clear();
                foreach (TankBlock bloc in overwrite)
                {
                    BlockMemory mem = new BlockMemory
                    {
                        t = bloc.name,
                        p = bloc.cachedLocalPosition,
                        r = bloc.cachedLocalRotation.rot
                    };
                    SavedTech.Add(mem);
                }
                DebugTAC_AI.Log("TACtical_AI:  DesignMemory - Overwrote(SaveTech) " + tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
                ValidateTechIfNeeded();
                BuildTechQuickLookup();
            }

            private void ValidateTechIfNeeded()
            {
                if (ManNetwork.IsNetworked)
                    if (!TempManager.ValidateBlocksInTech(ref SavedTech))
                        DebugTAC_AI.Log("TACtical_AI: DesignMemory - Found illegal blocks for " + tank.name
                            + " and purged them from memory to prevent self-repair meltdown");
            }

            /// <summary>
            /// CALL THIS AFTER CHANGING SavedTech!!!
            /// Can be a source of Hash Collisions.  
            /// No Hash Collisions have occurred yet however.
            /// </summary>
            /// <param name="blockGOName"></param>
            /// <returns></returns>
            private void BuildTechQuickLookup()
            {
                fastBlockLookup.Clear();
                List<BlockTypes> types = GetBlockTypesFromMemory();
                foreach (var item in types)
                {
                    string Name = ManSpawn.inst.GetBlockPrefab(item).name;
                    List<BlockMemory> LP = new List<BlockMemory>();

                    int hash = Name.GetHashCode();
                    List<BlockMemory> BM = SavedTech.FindAll(delegate (BlockMemory cand) { return cand.t.GetHashCode() == hash; });
                    foreach (var position in BM)
                    {
                        LP.Add(position);
                    }
                    fastBlockLookup.Add(item, LP);
                    //Debug.Info("TACtical_AI: BuildTechQuickLookup - processed " + LP.Count + " entries for " + item);
                }
            }


            public void MemoryToTech(List<BlockMemory> overwrite)
            {   // Loading a Tech from the BlockMemory
                rejectSaveAttempts = true;
                blockIntegrityDirty = true;
                MissingTypes.Clear();
                SavedTech.Clear();
                List<BlockMemory> clean = new List<BlockMemory>();
                foreach (BlockMemory mem in overwrite)
                {
                    BlockTypes type = BlockIndexer.StringToBlockType(mem.t);
                    if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    {
                        DebugTAC_AI.Log("TACtical_AI:  DesignMemory - " + tank.name + ": could not save " + mem.t + " in blueprint due to illegal block.");
                        continue;
                    }
                    // get rid of floating point errors
                    mem.TidyUp();
                    clean.Add(mem);
                }
                SavedTech = clean;
                DebugTAC_AI.Log("TACtical_AI:  DesignMemory - Overwrote(MemoryToTech) " + tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
                ValidateTechIfNeeded();
                BuildTechQuickLookup();
            }
            public TankBlock TryFindProperRootBlock(List<TankBlock> ToSearch)
            {
                return FindProperRootBlockExternal(ToSearch);
            }
            public List<BlockMemory> TechToMemory()
            {
                return TechToMemoryExternal(tank);
            }

            private void VerifyIntegrity()
            {
                if (!blockIntegrityDirty)
                    return;
                List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                if (cBlocks.Count() == 0)
                {
                    DebugTAC_AI.Assert(true, "TACtical AI: ASSERT - VerifyIntegrity - Called on Tank with ZERO blocks!");
                    return;
                }
                MissingTypes.Clear();
                List<BlockTypes> typesToRepair = GetBlockTypesFromMemory();
                int typesToSearch = typesToRepair.Count();

                foreach (BlockTypes repairCase in typesToRepair)
                {
                    int present = cBlocks.FindAll(delegate (TankBlock cand) { return repairCase == cand.BlockType; }).Count;
                    string Name = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(repairCase).name;

                    int mem2 = ReturnAllPositionsOfType(repairCase).Count;
                    if (mem2 > present)// are some blocks not accounted for?
                        MissingTypes.Add(repairCase);
                }
                DebugTAC_AI.Log("TACtical AI: VerifyIntegrity - Executed with " + MissingTypes.Count + " results");
                blockIntegrityDirty = false;
            }
            private void VerifyIntegritySLOW()
            {
                if (!blockIntegrityDirty)
                    return;
                List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                if (cBlocks.Count() == 0)
                {
                    DebugTAC_AI.Log("TACtical AI: ASSERT - VerifyIntegrity - Called on Tank with ZERO blocks!");
                    return;
                }
                MissingTypes.Clear();
                List<BlockTypes> typesToRepair = new List<BlockTypes>();
                List<BlockMemory> mem = ReturnContents();
                int toFilter = mem.Count();
                List<string> filteredNames = new List<string>();
                for (int step = 0; step < toFilter; step++)
                {
                    filteredNames.Add(mem.ElementAt(step).t);
                }
                filteredNames = filteredNames.Distinct().ToList();
                for (int step = 0; step < filteredNames.Count; step++)
                {
                    typesToRepair.Add(BlockIndexer.StringToBlockType(filteredNames.ElementAt(step)));
                }

                int toFilter2 = typesToRepair.Count();
                for (int step = 0; step < toFilter2; step++)
                {
                    int present = cBlocks.FindAll(delegate (TankBlock cand) { return typesToRepair[step] == cand.BlockType; }).Count;
                    string Name = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(typesToRepair[step]).name;

                    int mem2 = mem.FindAll(delegate (BlockMemory cand) { return Name == cand.t; }).Count;
                    if (mem2 > present)// are some blocks not accounted for?
                        MissingTypes.Add(typesToRepair[step]);
                }
                DebugTAC_AI.Log("TACtical AI: VerifyIntegrity - Executed with " + MissingTypes.Count + " results");
                blockIntegrityDirty = false;
            }

            // Gets
            public bool HasFullHealth()
            {
                return thisInst.DamageThreshold.Approximately(0);
            }

            /// <summary>
            /// Returns true if the tech is damaged and has blocks to use
            /// </summary>
            /// <param name="tank"></param>
            /// <param name="mind"></param>
            /// <returns></returns>
            public bool SystemsCheck()
            {
                float totalDesignBlocks = (float)SavedTech.Count;
                if (totalDesignBlocks == 0)
                {
                    DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + " has 0 saved blocks in TechMemor.  How?");
                    return false;
                }
                thisInst.DamageThreshold = (1 - (tank.blockman.blockCount / totalDesignBlocks)) * 100;
                DebugTAC_AI.Info("TACtical_AI: Tech " + tank.name + " has damage percent of " + thisInst.DamageThreshold);
                if (!thisInst.DamageThreshold.Approximately(0) && !ranOutOfParts)
                {
                    return true;
                }
                return false;
            }
            public bool IsDesignComplete()
            {   // Saving a Tech from the BlockMemory
                return SavedTech.Count == tank.blockman.blockCount;
            }
            public List<BlockTypes> GetMissingBlockTypes()
            {
                VerifyIntegrity();
                return MissingTypes;
            }
            public List<BlockTypes> GetBlockTypesFromMemory()
            {
                if (fastBlockLookup.Count > 0)
                {
                    List<BlockTypes> typesBatchCache = new List<BlockTypes>();
                    foreach (var item in fastBlockLookup)
                    {
                        typesBatchCache.Add(item.Key);
                    }
                    return typesBatchCache;
                }
                List<BlockMemory> mem = ReturnContents();
                int toFilter = mem.Count();
                List<string> filteredNames = new List<string>();
                for (int step = 0; step < toFilter; step++)
                {
                    filteredNames.Add(mem.ElementAt(step).t);
                }
                filteredNames = filteredNames.Distinct().ToList();
                List<BlockTypes> typesToRepair = new List<BlockTypes>();
                for (int step = 0; step < filteredNames.Count; step++)
                {
                    typesToRepair.Add(BlockIndexer.StringToBlockType(filteredNames.ElementAt(step)));
                }
                return typesToRepair.Distinct().ToList();
            }


            public void UpdateMissingBlockTypes(List<BlockTypes> currentlyMissing)
            {
                MissingTypes = currentlyMissing;
            }



            // Advanced
            public bool ChanceGrabBackBlock()
            {
                if (KickStart.EnemyBlockDropChance == 0)
                    return false;
                if (KickStart.CommitDeathMode)
                {
                    if (UnityEngine.Random.Range(0, 500) < 150)
                    {
                        //Debug.Log("TACtical_AI: Enemy AI " + tank.name + " reclaim attempt success");
                        ReserveSuperGrabs++;
                        return true;
                    }
                }
                else
                {
                    if (UnityEngine.Random.Range(0, 500) < KickStart.Difficulty)
                    {
                        //Debug.Log("TACtical_AI: Enemy AI " + tank.name + " reclaim attempt success");
                        ReserveSuperGrabs++;
                        return true;
                    }
                }
                return false;
            }
            public bool TryAttachExistingBlock(TankBlock foundBlock)
            {
                bool attemptW;
                // if we are smrt, run heavier operation
                List<BlockMemory> posBlocks = ReturnAllPositionsOfTypeSLOW(foundBlock.name);
                //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                for (int step2 = 0; step2 < posBlocks.Count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = QueueBlockAttach(template, foundBlock);
                    if (attemptW)
                    {
                        return true;
                    }
                }
                return false;
            }
            // AI Self-Construction Animations
            public void TryAttachHeldBlock()
            {
                if (!gameObject.activeSelf)
                    return;
                bool success;
                try
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
                    TankBlock held = thisInst.HeldBlock;
                    if (held == null)
                    {
                        DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach NULL BLOCK");
                        templateCache = null;
                        return;
                    }
                    if (templateCache == null)
                    {
                        DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach block but no template was cached!!");
                        return;
                    }

                    thisInst.DropBlock();
                    success = AIBlockAttachRequest(tank, templateCache, held);

                    if (success)
                    {
                        if (held.visible.InBeam)
                            held.visible.SetHolder(null);

                        //Debug.Log("TACtical_AI: AI " + tank.name + ":  Attaching " + canidate.name);
                        if (!KickStart.MuteNonPlayerRacket)
                        {
                            FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                            FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                            ManSFX.inst.AttachInstanceToPosition(soundSteal[(int)held.BlockConnectionAudioType].PlayEvent(), held.centreOfMassWorld);
                        }
                    }
                    if (tank.IsAnchored && thisInst.AIState == AIAlignment.NonPlayer)
                    {
                        MakeMinersMineUnlimited();
                    }
                }
                catch { }
                templateCache = null;
            }
            public void RushAttachOpIfNeeded()
            {
                if (templateCache == null)
                    return;
                //Debug.Log("RUSHING ATTACH OP");
                TryAttachHeldBlock();
                CancelInvoke("TryAttachHeldBlock");
            }
            public void AttachOperation(Visible block, BlockMemory mem, out Vector3 offsetVec)
            {
                if (templateCache != null)
                {
                    RushAttachOpIfNeeded();
                }
                thisInst.HoldBlock(block, mem);
                templateCache = mem;
                offsetVec = (mem.p - tank.blockBounds.center).normalized;
                Invoke("TryAttachHeldBlock", AIGlobals.BlockAttachDelay);
                lastAttached = block.block.BlockType;
            }


            // JSON
            public void TechToJSONLog()
            {   // Saving a Tech from the BlockMemory
                List<BlockMemory> mem = TechToMemory();
                if (mem.Count == 0)
                    return;
                SavedTech.Clear();
                SavedTech.AddRange(mem);
                StringBuilder JSONTechRAW = new StringBuilder();
                JSONTechRAW.Append(JsonUtility.ToJson(mem.First()));
                for (int step = 1; step < mem.Count; step++)
                {
                    JSONTechRAW.Append("|");
                    JSONTechRAW.Append(JsonUtility.ToJson(mem.ElementAt(step)));
                }
                string JSONTechRAWout = JSONTechRAW.ToString();
                StringBuilder JSONTech = new StringBuilder();
                foreach (char ch in JSONTechRAWout)
                {
                    if (ch == '"')
                    {
                        JSONTech.Append(Templates.RawTechExporter.up);
                        JSONTech.Append(ch);
                    }
                    else
                        JSONTech.Append(ch);
                }
                DebugTAC_AI.Log("TACtical_AI: " + JSONTech.ToString());
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
                mem.Add(JsonUtility.FromJson<BlockMemory>(blockCase.ToString()));
                //Debug.Log("TACtical_AI:  DesignMemory: saved " + mem.Count);
                MemoryToTech(mem);
            }

            // CONSTRUCTION
            /// <summary>
            /// Bookmarks the build's data into the Tech for incremental building, but will not 
            ///   guarentee completion.
            /// </summary>
            /// <param name="thisInst"></param>
            /// <param name="JSON"></param>
            public void SetupForNewTechConstruction(AIECore.TankAIHelper thisInst, string JSON)
            {
                JSONToTech(JSON);
                CheckGameTamperedWith(tank, this);
                thisInst.PendingDamageCheck = true;
            }

            // Load operation
            public List<BlockMemory> ReturnContents()
            {
                if (SavedTech.Count() == 0)
                {
                    DebugTAC_AI.Log("TACtical_AI: INVALID TECH DATA STORED FOR TANK " + tank.name);
                    DebugTAC_AI.Log("TACtical_AI: " + StackTraceUtility.ExtractStackTrace());
                }
                return new List<BlockMemory>(SavedTech);
            }

            /// <summary>
            /// Can be a source of Hash Collisions.  
            /// No Hash Collisions have occurred yet however.
            /// </summary>
            /// <param name="blockGOName"></param>
            /// <returns></returns>
            public List<BlockMemory> ReturnAllPositionsOfTypeSLOW(string blockGOName)
            {
                int hash = blockGOName.GetHashCode();
                return SavedTech.FindAll(delegate (BlockMemory cand) { return cand.t.GetHashCode() == hash; });
            }

            /// <summary>
            /// SUPER SLOW
            /// Can be a source of Hash Collisions.  
            /// No Hash Collisions have occurred yet however.
            /// </summary>
            /// <param name="blockGOName"></param>
            /// <returns></returns>
            public List<BlockMemory> ReturnAllPositionsOfMultipleTypes(List<BlockTypes> types)
            {
                List<int> hashes = new List<int>();
                foreach (var item in types)
                {
                    try
                    {
                        hashes.Add(ManSpawn.inst.GetBlockPrefab(item).name.GetHashCode());
                    }
                    catch { }
                }
                return SavedTech.FindAll(delegate (BlockMemory cand) { return hashes.Contains(cand.t.GetHashCode()); });
            }

            public List<BlockMemory> ReturnAllPositionsOfType(BlockTypes blocktype)
            {

                if (fastBlockLookup.TryGetValue(blocktype, out List<BlockMemory> mems))
                {
                    //Debug.Info("TACtical_AI:  DesignMemory - ReturnAllPositionsOfType " + tank.name + " looked for " + blocktype + " and found " + mems.Count);
                    return mems;
                }
                //Debug.Info("TACtical_AI:  DesignMemory - ReturnAllPositionsOfType " + tank.name + " looked for " + blocktype + " and found nothing");
                return new List<BlockMemory>();
            }

            // Infinite money for enemy autominer bases - resources are limited
            public void MakeMinersMineUnlimited()
            {   // make autominers mine deep based on biome
                try
                {
                    CancelInvoke("DoMakeMinersMineUnlimited");
                    Invoke("DoMakeMinersMineUnlimited", 2f);
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: MakeMinersMineUnlimited - game is being stubborn");
                }
            }
            public void DoMakeMinersMineUnlimited()
            {   // make autominers mine deep based on biome
                try
                {
                    thisInst.AdjustAnchors();
                    //Debug.Log("TACtical_AI: " + tank.name + " is trying to mine unlimited");
                    foreach (ModuleItemProducer module in tank.blockman.IterateBlockComponents<ModuleItemProducer>())
                    {
                        module.gameObject.GetOrAddComponent<ReverseCache>().SaveComponents();
                    }
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: DoMakeMinersMineUnlimited - game is being stubborn");
                }
            }


            //External
            /// <summary>
            /// Mandatory for techs that have plans to be built over time by the building AI.
            /// The first block being an anchored block will determine if the entire techs live or not
            ///   on spawning. If this fails, there's a good chance the AI could have wasted money on it.
            /// </summary>
            /// <param name="ToSearch">The list of blocks to find the new root in</param>
            /// <returns>The new Root block</returns>
            public static TankBlock FindProperRootBlockExternal(List<TankBlock> ToSearch)
            {
                bool IsAnchoredAnchorPresent = false;
                float close = 128 * 128;
                TankBlock newRoot = ToSearch.First();
                foreach (TankBlock bloc in ToSearch)
                {
                    Vector3 blockPos = bloc.CalcFirstFilledCellLocalPos();
                    float sqrMag = blockPos.sqrMagnitude;
                    if (bloc.GetComponent<ModuleAnchor>() && bloc.GetComponent<ModuleAnchor>().IsAnchored)
                    {   // If there's an anchored anchor, then we base the root off of that
                        //  It's probably a base
                        IsAnchoredAnchorPresent = true;
                        break;
                    }
                    if (sqrMag < close && (bloc.GetComponent<ModuleTechController>() || bloc.GetComponent<ModuleAIBot>()))
                    {
                        close = sqrMag;
                        newRoot = bloc;
                    }
                }
                if (IsAnchoredAnchorPresent)
                {
                    close = 128 * 128;
                    foreach (TankBlock bloc in ToSearch)
                    {
                        Vector3 blockPos = bloc.CalcFirstFilledCellLocalPos();
                        float sqrMag = blockPos.sqrMagnitude;
                        if (sqrMag < close && bloc.GetComponent<ModuleAnchor>() && bloc.GetComponent<ModuleAnchor>().IsAnchored)
                        {
                            close = sqrMag;
                            newRoot = bloc;
                        }
                    }
                }
                return newRoot;
            }

            /// <summary>
            /// Mandatory for techs that have plans to be built over time by the building AI.
            /// The first block being an anchored block will determine if the entire techs live or not
            ///   on spawning. If this fails, there's a good chance the AI could have wasted money on it.
            /// </summary>
            /// <param name="ToSearch">The list of saved blocks to find the new root in</param>
            /// <returns>The new Root block</returns>
            public static TankPreset.BlockSpec FindProperRootBlockExternal(List<TankPreset.BlockSpec> ToSearch)
            {
                bool IsAnchoredAnchorPresent = false;
                float close = 128 * 128;
                TankPreset.BlockSpec newRoot = ToSearch.First();
                foreach (TankPreset.BlockSpec blocS in ToSearch)
                {
                    TankBlock bloc = ManSpawn.inst.GetBlockPrefab(BlockIndexer.StringToBlockType(blocS.block));
                    Vector3 blockPos = blocS.position + new OrthoRotation((OrthoRotation.r)blocS.orthoRotation) * bloc.filledCells[0];
                    if (bloc.GetComponent<ModuleAnchor>() && blocS.CheckIsAnchored())
                    {
                        IsAnchoredAnchorPresent = true;
                        break;
                    }
                    float sqrMag = blockPos.sqrMagnitude;
                    if (sqrMag < close && (bloc.GetComponent<ModuleTechController>() || bloc.GetComponent<ModuleAIBot>()))
                    {
                        close = sqrMag;
                        newRoot = blocS;
                    }
                }
                if (IsAnchoredAnchorPresent)
                {
                    close = 128 * 128;
                    foreach (TankPreset.BlockSpec blocS in ToSearch)
                    {
                        TankBlock bloc = ManSpawn.inst.GetBlockPrefab(BlockIndexer.StringToBlockType(blocS.block));
                        Vector3 blockPos = blocS.position + new OrthoRotation((OrthoRotation.r)blocS.orthoRotation) * bloc.filledCells[0];
                        float sqrMag = blockPos.sqrMagnitude;
                        if (sqrMag < close && bloc.GetComponent<ModuleAnchor>() && blocS.CheckIsAnchored())
                        {
                            close = sqrMag;
                            newRoot = blocS;
                        }
                    }
                }
                return newRoot;
            }

            /// <summary>
            /// Mandatory for techs that have plans to be built over time by the building AI.
            /// Since the first block placed ultimately determines the base rotation of the Tech
            ///  (Arrow shown on Radar/minimap) we must be ABSOLUTELY SURE to build teh Tech in relation
            ///   to that first block.
            ///   Any alteration on the first block's rotation will have severe consequences in the long run.
            ///   
            /// Split techs on the other hand are mostly free from this issue.
            /// </summary>
            /// <param name="ToSearch">The list of blocks to find the new foot in</param>
            /// <returns></returns>
            public static List<BlockMemory> TechToMemoryExternal(Tank tank)
            {
                // This resaves the whole tech cab-forwards regardless of original rotation
                //   It's because any solutions that involve the cab in a funny direction will demand unholy workarounds.
                //   I seriously don't know why the devs didn't try it this way, perhaps due to lag reasons.
                //   or the blocks that don't allow upright placement (just detach those lmao)
                List<BlockMemory> output = new List<BlockMemory>();
                List<TankBlock> ToSave = tank.blockman.IterateBlocks().ToList();
                Vector3 coreOffset = Vector3.zero;
                Quaternion coreRot;
                TankBlock rootBlock = FindProperRootBlockExternal(ToSave);
                if (rootBlock != null)
                {
                    if (rootBlock != ToSave.First())
                    {
                        ToSave.Remove(rootBlock);
                        ToSave.Insert(0, rootBlock);
                    }
                    coreOffset = rootBlock.trans.localPosition;
                    coreRot = rootBlock.trans.localRotation;
                    tank.blockman.SetRootBlock(rootBlock);
                }
                else
                    coreRot = new OrthoRotation(OrthoRotation.r.u000);

                foreach (TankBlock bloc in ToSave)
                {
                    if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(bloc.BlockType))
                        continue;
                    Quaternion deltaRot = Quaternion.Inverse(coreRot);
                    BlockMemory mem = new BlockMemory
                    {
                        t = bloc.name,
                        p = deltaRot * (bloc.trans.localPosition - coreOffset)
                    };
                    // get rid of floating point errors
                    mem.TidyUp();
                    //Get the rotation
                    mem.r = SetCorrectRotation(bloc.trans.localRotation, deltaRot).rot;
                    if (!IsValidRotation(bloc, mem.r))
                    {   // block cannot be saved - illegal rotation.
                        DebugTAC_AI.Log("TACtical_AI:  DesignMemory - " + tank.name + ": could not save " + bloc.name + " in blueprint due to illegal rotation.");
                        continue;
                    }
                    output.Add(mem);
                }
                DebugTAC_AI.Info("TACtical_AI:  DesignMemory - Saved " + tank.name + " to memory format");

                return output;
            }

            /// <summary>
            /// Mandatory for techs that have plans to be built over time by the building AI.
            /// Since the first block placed ultimately determines the base rotation of the Tech
            ///  (Arrow shown on Radar/minimap) we must be ABSOLUTELY SURE to build teh Tech in relation
            ///   to that first block.
            ///   Any alteration on the first block's rotation will have severe consequences in the long run.
            ///   
            /// Split techs on the other hand are mostly free from this issue.
            /// </summary>
            /// <param name="ToSearch">The list of blocks to find the new foot in</param>
            /// <returns></returns>
            public static List<BlockMemory> TechToMemoryExternal(TechData tank)
            {
                // This resaves the whole tech cab-forwards regardless of original rotation
                //   It's because any solutions that involve the cab in a funny direction will demand unholy workarounds.
                //   I seriously don't know why the devs didn't try it this way, perhaps due to lag reasons.
                //   or the blocks that don't allow upright placement (just detach those lmao)
                List<BlockMemory> output = new List<BlockMemory>();
                List<TankPreset.BlockSpec> ToSave = tank.m_BlockSpecs;
                Quaternion coreRot;
                TankPreset.BlockSpec rootBlock = FindProperRootBlockExternal(ToSave);
                if (rootBlock.m_BlockType != ToSave.First().m_BlockType)
                {
                    ToSave.Remove(rootBlock);
                    ToSave.Insert(0, rootBlock);
                }
                Vector3 coreOffset = rootBlock.position;
                coreRot = new OrthoRotation((OrthoRotation.r)rootBlock.orthoRotation);

                foreach (TankPreset.BlockSpec blocS in ToSave)
                {
                    BlockTypes BT = BlockIndexer.StringToBlockType(blocS.block);
                    TankBlock bloc = ManSpawn.inst.GetBlockPrefab(BT);
                    if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(BT))
                        continue;
                    Quaternion deltaRot = Quaternion.Inverse(coreRot);
                    BlockMemory mem = new BlockMemory
                    {
                        t = bloc.name,
                        p = deltaRot * (blocS.position - coreOffset)
                    };
                    // get rid of floating point errors
                    mem.TidyUp();
                    //Get the rotation
                    mem.r = SetCorrectRotation(new OrthoRotation((OrthoRotation.r)rootBlock.orthoRotation), deltaRot).rot;
                    if (!IsValidRotation(bloc, mem.r))
                    {   // block cannot be saved - illegal rotation.
                        continue;
                    }
                    output.Add(mem);
                }
                return output;
            }
            public static string TechToJSONExternal(Tank tank)
            {   // Saving a Tech from the BlockMemory
                return MemoryToJSONExternal(TechToMemoryExternal(tank));
            }
            public static string MemoryToJSONExternal(List<BlockMemory> mem)
            {   // Saving a Tech from the BlockMemory
                if (mem.Count == 0)
                    return null;
                StringBuilder JSONTechRAW = new StringBuilder();
                JSONTechRAW.Append(JsonUtility.ToJson(mem.First()));
                for (int step = 1; step < mem.Count; step++)
                {
                    JSONTechRAW.Append("|");
                    JSONTechRAW.Append(JsonUtility.ToJson(mem.ElementAt(step)));
                }
                string JSONTechRAWout = JSONTechRAW.ToString();
                StringBuilder JSONTech = new StringBuilder();
                foreach (char ch in JSONTechRAWout)
                {
                    if (ch == '"')
                    {
                        //JSONTech.Append("\\");
                        JSONTech.Append(ch);
                    }
                    else
                        JSONTech.Append(ch);
                }
                //Debug.Log("TACtical_AI: " + JSONTech.ToString());
                return JSONTech.ToString();
            }
            public static List<BlockMemory> JSONToMemoryExternal(string toLoad)
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
                mem.Add(JsonUtility.FromJson<BlockMemory>(blockCase.ToString()));
                //Debug.Log("TACtical_AI:  DesignMemory: saved " + mem.Count);
                return mem;
            }

            // EXPERIMENT
            public static void RebuildTechForwards(Tank tank)
            {
                List<BlockMemory> mem = TechToMemoryExternal(tank);
                List<TankBlock> blocks = DitchAllBlocks(tank, true);
                TurboconstructExt(tank, mem, blocks, false);
            }
            public static List<TankBlock> DitchAllBlocks(Tank tank, bool addToThisFrameLater)
            {
                try
                {
                    List<TankBlock> blockCache = tank.blockman.IterateBlocks().ToList();
                    tank.blockman.Disintegrate(true, addToThisFrameLater);
                    return blockCache;
                }
                catch { }
                return new List<TankBlock>();
            }


            public bool HandlePurchase()
            {
                if (purchaseOp == null)
                    return true;
                return purchaseOp.Invoke(lastAttached);
            }

            public bool EnemyPurchase(BlockTypes BlockType)
            {
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    if (Enemy.RBases.TryMakePurchase(BlockType, tank.Team))
                    {
                        DebugTAC_AI.Info("TACtical_AI: AI " + tank.name + ": bought " + BlockType
                            + " from the shop for "
                            + Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(BlockType, true));

                        if (!KickStart.MuteNonPlayerRacket)
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }


            /// REPAIR OPERATIONS

            //Controlling code that re-attaches loose blocks for AI techs.
            public BlockTypes lastAttached;
            public bool QueueBlockAttach(BlockMemory template, TankBlock canidate, bool purchase = false)
            {
                if (ManNetwork.IsNetworked)
                    return AttemptBlockAttachImmediate(template, canidate, purchase);

                if (!tank.visible.isActive || !canidate)
                {
                    lastAttached = BlockTypes.GSOAIController_111;
                    return false;
                }

                ranOutOfParts = false;
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
                if (tank.CanAttachBlock(canidate, template.p, new OrthoRotation(template.r)))
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                    if (!purchase || HandlePurchase())
                    {
                        AttachOperation(canidate.visible, template, out _);
                        lastAttached = canidate.BlockType;
                        return true;
                    }
                    else
                    {
                        ranOutOfParts = true;
                        lastAttached = BlockTypes.GSOAIController_111;
                        return true;
                    }
                }
                else
                    return false;
            }
            public bool QueueBlockAttach(BlockMemory template, TankBlock canidate, out Vector3 offsetVec, bool purchase = false)
            {
                offsetVec = Vector3.zero;
                if (ManNetwork.IsNetworked)
                    return AttemptBlockAttachImmediate(template, canidate, purchase);

                if (!tank.visible.isActive || !canidate)
                {
                    lastAttached = BlockTypes.GSOAIController_111;
                    return false;
                }

                ranOutOfParts = false;
                //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
                if (tank.CanAttachBlock(canidate, template.p, new OrthoRotation(template.r)))
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                    if (!purchase || HandlePurchase())
                    {
                        AttachOperation(canidate.visible, template, out offsetVec);
                        lastAttached = canidate.BlockType;
                        return true;
                    }
                    else
                    {
                        ranOutOfParts = true;
                        lastAttached = BlockTypes.GSOAIController_111;
                        return true;
                    }
                }
                else
                {
                    lastAttached = BlockTypes.GSOAIController_111;
                    return false;
                }
            }
            public bool AttemptBlockAttachImmediate(BlockMemory template, TankBlock canidate, bool purchase = false)
            {
                lastAttached = BlockTypes.GSOAIController_111;
                if (!tank.visible.isActive || !canidate)
                {
                    // If we try to attach to a tech that doesn't exist, it corrupts and breaks ALL future techs that spawn.
                    //   The game breaks, yadda yadda, ManUpdate looses it's marbles, causing bullets and wheels to freak out.
                    //   In other words, *Unrecoverable crash*
                    //
                    //      So we end the madness here
                    return false;
                }

                RushAttachOpIfNeeded();

                ranOutOfParts = false;
                bool success = AIBlockAttachRequest(tank, template, canidate);

                if (success)
                {
                    if (canidate.visible.InBeam)
                        canidate.visible.SetHolder(null);
                    lastAttached = canidate.BlockType;

                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                    if (purchase && !HandlePurchase())
                    {
                        ranOutOfParts = true;
                        return false;
                    }

                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Attaching " + canidate.name);
                    if (!KickStart.MuteNonPlayerRacket)
                    {
                        FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                        FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                        ManSFX.inst.AttachInstanceToPosition(soundSteal[(int)canidate.BlockConnectionAudioType].PlayEvent(), canidate.centreOfMassWorld);
                    }
                }
                return success;
            }


            // Repair Utilities
            public List<TankBlock> FindBlocksNearbyTank()
            {
                List<TankBlock> fBlocks = new List<TankBlock>();
                foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, AIGlobals.MaxBlockGrabRange, new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
                {
                    if ((bool)foundBlock.block && foundBlock.GetComponent<WorldSpaceObject>().IsEnabled)
                    {
                        if (!(bool)foundBlock.block.tank && foundBlock.ColliderSwapper.CollisionEnabled
                            && foundBlock.IsInteractible && (!foundBlock.InBeam || (foundBlock.InBeam
                            && foundBlock.holderStack.myHolder.block.LastTechTeam == tank.Team))
                            && Singleton.Manager<ManPointer>.inst.DraggingItem != foundBlock &&
                            foundBlock != thisInst.HeldBlock)
                        {
                            if (foundBlock.block.PreExplodePulse)
                                continue; //explode? no thanks
                                          //Debug.Log("TACtical AI: RepairLerp - block " + foundBlock.name + " has " + cBlocks.FindAll(delegate (TankBlock cand) { return cand.blockPoolID == foundBlock.block.blockPoolID; }).Count() + " matches");
                            fBlocks.Add(foundBlock.block);
                        }
                    }
                }
                fBlocks = fBlocks.OrderBy((blok) => (blok.centreOfMassWorld - tank.boundsCentreWorld).sqrMagnitude).ToList();
                return fBlocks;
            }
            internal bool TryAttachExistingBlockFromList(ref List<BlockTypes> typesMissing, ref List<TankBlock> foundBlocks, bool denySD = false)
            {
                int attachAttempts = foundBlocks.Count();
                //Debug.Log("TACtical AI: RepairLerp - Found " + attachAttempts + " loose blocks to use");
                for (int step = 0; step < attachAttempts; step++)
                {
                    TankBlock foundBlock = foundBlocks[step];
                    BlockTypes BT = foundBlock.BlockType;
                    if (!typesMissing.Contains(BT))
                        continue;
                    bool attemptW;
                    // if we are smrt, run heavier operation
                    List<BlockMemory> posBlocks = ReturnAllPositionsOfType(BT);
                    int count = posBlocks.Count;
                    //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        BlockMemory template = posBlocks.ElementAt(step2);
                        attemptW = QueueBlockAttach(template, foundBlock);
                        if (attemptW)
                        {
                            if (denySD)
                            {
                                foundBlock.damage.AbortSelfDestruct();
                            }
                            if (count == 1)
                                typesMissing.Remove(BT);
                            foundBlocks.RemoveAt(step);
                            return true;
                        }
                    }
                }
                return false;
            }
            internal bool TryAttachExistingBlockFromListInst(ref List<BlockTypes> typesMissing, ref List<TankBlock> foundBlocks, bool denySD = false)
            {
                int attachAttempts = foundBlocks.Count();
                //Debug.Log("TACtical AI: RepairLerp - Found " + attachAttempts + " loose blocks to use");
                for (int step = 0; step < attachAttempts; step++)
                {
                    TankBlock foundBlock = foundBlocks[step];
                    BlockTypes BT = foundBlock.BlockType;
                    if (!typesMissing.Contains(BT))
                        continue;
                    bool attemptW;
                    // if we are smrt, run heavier operation

                    List<BlockMemory> posBlocks = ReturnAllPositionsOfType(BT);
                    int count = posBlocks.Count;
                    //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        BlockMemory template = posBlocks.ElementAt(step2);
                        attemptW = AttemptBlockAttachImmediate(template, foundBlock);
                        if (attemptW)
                        {
                            if (denySD)
                            {
                                foundBlock.damage.AbortSelfDestruct();
                            }
                            if (count == 1)
                                typesMissing.Remove(BT);
                            foundBlocks.RemoveAt(step);
                            return true;
                        }
                    }
                }
                return false;
            }

            internal bool TrySpawnAndAttachBlockFromList(ref List<BlockTypes> typesMissing, bool playerInventory = false, bool purchase = false)
            {
                int attachAttempts = typesMissing.Count();
                for (int step = 0; step < attachAttempts; step++)
                {
                    BlockTypes bType = typesMissing.ElementAt(step);
                    if (playerInventory)
                    {
                        if (!BlockAvailInInventory(tank, bType))
                            continue;
                    }
                    else if (purchase && !HandlePurchase())
                    {
                        ranOutOfParts = true;
                        thisInst.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    TankBlock foundBlock = RawTechLoader.SpawnBlockS(bType, tank.boundsCentreWorldNoCheck + (Vector3.up * (thisInst.lastTechExtents + 10)), Quaternion.identity, out bool worked);
                    if (!worked)
                    {
                        DebugTAC_AI.Log("TACtical AI: TrySpawnAndAttachBlockFromList - Could not spawn block");
                        continue;
                    }
                    bool attemptW;

                    List<BlockMemory> posBlocks = ReturnAllPositionsOfType(bType);
                    int count = posBlocks.Count();
                    if (count == 0)
                    {
                        DebugTAC_AI.Log("TACtical AI: TrySpawnAndAttachBlockFromList - THERE'S NO MORE BLOCK POSITIONS TO ATTACH!");
                        ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                        typesMissing.RemoveAt(step);
                        attachAttempts--;
                        step--;
                        continue;
                    }
                    //Debug.Log("TACtical AI: TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        BlockMemory template = posBlocks.ElementAt(step2);
                        attemptW = QueueBlockAttach(template, foundBlock, out Vector3 offsetVec, purchase);
                        if (attemptW)
                        {
                            //foundBlock.InitNew();
                            foundBlock.trans.position = tank.boundsCentreWorldNoCheck + (tank.trans.TransformDirection(offsetVec).SetY(0.65f).normalized * (thisInst.lastTechExtents + 10));
                            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                            if (effect)
                            {
                                effect.transform.Spawn(foundBlock.centreOfMassWorld);
                            }
                            if (count == 1)
                                typesMissing.Remove(bType);
                            if (playerInventory)
                                BlockAvailInInventory(tank, bType, true);
                            return true;
                        }
                    }
                    //Debug.Log("TACtical AI: TurboRepair - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

                    ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                    // if everything fails, resort to timbuktu
                    //foundBlock.damage.SelfDestruct(0.1f);
                    //Vector3 yeet = Vector3.forward * 450000;
                    //foundBlock.transform.position = yeet;
                }
                return false;
            }

            internal bool TrySpawnAndAttachBlockFromListInst(ref List<BlockTypes> typesMissing, bool playerInventory = false, bool purchase = false)
            {
                int attachAttempts = typesMissing.Count();
                Vector3 blockSpawnPos = tank.boundsCentreWorldNoCheck + (Vector3.up * 128);
                if (ManNetwork.IsNetworked)
                {
                    for (int step = 0; step < attachAttempts; step++)
                    {
                        BlockTypes bType = typesMissing.ElementAt(step);
                        if (IterateAndTryAttachBlockMP(bType, ref typesMissing, blockSpawnPos))
                            return true;
                        if (!thisInst.PendingDamageCheck)
                            return false;
                    }
                    return false;
                }
                for (int step = 0; step < attachAttempts; step++)
                {
                    BlockTypes bType = typesMissing.ElementAt(step);
                    if (playerInventory)
                    {
                        if (!BlockAvailInInventory(tank, bType))
                            continue;
                    }
                    else if (purchase && !HandlePurchase())
                    {
                        ranOutOfParts = true;
                        thisInst.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    bool attemptW;

                    List<BlockMemory> posBlocks = ReturnAllPositionsOfType(bType);
                    int count = posBlocks.Count();
                    if (count == 0)
                    {
                        //DebugTAC_AI.Log("TACtical AI: TrySpawnAndAttachBlockFromList - THERE'S NO MORE BLOCK POSITIONS TO ATTACH!");
                        typesMissing.RemoveAt(step);
                        attachAttempts--;
                        step--;
                        continue;
                    }
                    else
                    {
                        TankBlock foundBlock = RawTechLoader.SpawnBlockS(bType, blockSpawnPos, Quaternion.identity, out bool worked);
                        if (!worked)
                        {
                            DebugTAC_AI.Log("TACtical AI: TrySpawnAndAttachBlockFromList - Could not spawn block " + bType);
                            continue;
                        }
                        for (int step2 = 0; step2 < count; step2++)
                        {
                            BlockMemory template = posBlocks.ElementAt(step2);
                            attemptW = AttemptBlockAttachImmediate(template, foundBlock, purchase);
                            if (attemptW)
                            {
                                //foundBlock.InitNew();
                                if (count == 1)
                                    typesMissing.Remove(bType);
                                if (playerInventory)
                                    BlockAvailInInventory(tank, bType, true);
                                return true;
                            }
                        }
                        ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                    }
                }
                return false;
            }

            /// <summary>
            /// Queued with 0.75 sec delay
            /// </summary>
            /// <param name="tank"></param>
            /// <param name="TechMemor"></param>
            /// <param name="typesMissing"></param>
            /// <param name="playerInventory"></param>
            /// <param name="useLimitedSupplies"></param>
            /// <returns></returns>
            internal bool TrySpawnAndAttachBlockFromListWithSkin(ref List<BlockTypes> typesMissing, bool playerInventory = false, bool purchase = false)
            {
                int attachAttempts = typesMissing.Count();
                for (int step = 0; step < attachAttempts; step++)
                {
                    BlockTypes bType = typesMissing.ElementAt(step);
                    if (playerInventory)
                    {
                        if (!BlockAvailInInventory(tank, bType))
                            continue;
                    }
                    else if (purchase && !HandlePurchase())
                    {
                        ranOutOfParts = true;
                        thisInst.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    TankBlock foundBlock = RawTechLoader.SpawnBlockS(bType, tank.boundsCentreWorldNoCheck + (Vector3.up * (thisInst.lastTechExtents + 10)), Quaternion.identity, out bool worked);
                    if (!worked)
                    {
                        DebugTAC_AI.Log("TACtical AI: TrySpawnAndAttachBlockFromList - Could not spawn block");
                        continue;
                    }
                    bool attemptW;

                    List<BlockMemory> posBlocks = ReturnAllPositionsOfType(bType);
                    int count = posBlocks.Count();
                    if (count == 0)
                    {
                        ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                        typesMissing.RemoveAt(step);
                        attachAttempts--;
                        step--;
                        continue;
                    }
                    //Debug.Log("TACtical AI: TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        BlockMemory template = posBlocks.ElementAt(step2);
                        attemptW = QueueBlockAttach(template, foundBlock, out Vector3 offsetVec, purchase);
                        if (attemptW)
                        {
                            foundBlock.trans.position = tank.boundsCentreWorldNoCheck + (tank.trans.TransformDirection(offsetVec).SetY(0.65f).normalized * (thisInst.lastTechExtents + 10));
                            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                            if (effect)
                            {
                                effect.transform.Spawn(foundBlock.centreOfMassWorld);
                            }
                            foundBlock.SetSkinByUniqueID(RawTechLoader.GetSkinIDSetForTeam(tank.Team, (int)ManSpawn.inst.GetCorporation(bType)));
                            //foundBlock.InitNew();
                            if (count == 1)
                                typesMissing.Remove(bType);
                            if (playerInventory)
                                BlockAvailInInventory(tank, bType, true);
                            return true;
                        }
                    }
                    //Debug.Log("TACtical AI: TurboRepair - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

                    ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                    // if everything fails, resort to timbuktu
                    //foundBlock.damage.SelfDestruct(0.1f);
                    //Vector3 yeet = Vector3.forward * 450000;
                    //foundBlock.transform.position = yeet;
                }
                return false;
            }

            /// <summary>
            /// Handled instantly
            /// </summary>
            /// <param name="tank"></param>
            /// <param name="TechMemor"></param>
            /// <param name="typesMissing"></param>
            /// <param name="playerInventory"></param>
            /// <param name="useLimitedSupplies"></param>
            /// <returns></returns>
            internal bool TrySpawnAndAttachBlockFromListWithSkinInst(ref List<BlockTypes> typesMissing, bool playerInventory = false, bool purchase = false)
            {
                int attachAttempts = typesMissing.Count();
                Vector3 blockSpawnPos = tank.boundsCentreWorldNoCheck + (Vector3.up * 128);
                if (ManNetwork.IsNetworked)
                {
                    for (int step = 0; step < attachAttempts; step++)
                    {
                        BlockTypes bType = typesMissing.ElementAt(step);
                        if (IterateAndTryAttachBlockSkinMP(bType, ref typesMissing, blockSpawnPos))
                            return true;
                        if (!thisInst.PendingDamageCheck)
                            return false;
                    }
                    return false;
                }
                for (int step = 0; step < attachAttempts; step++)
                {
                    BlockTypes bType = typesMissing.ElementAt(step);
                    if (playerInventory)
                    {
                        if (!BlockAvailInInventory(tank, bType))
                            continue;
                    }
                    else if (purchase && !HandlePurchase())
                    {
                        ranOutOfParts = true;
                        thisInst.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    List<BlockMemory> posBlocks = ReturnAllPositionsOfType(bType);
                    int count = posBlocks.Count();
                    if (count == 0)
                    {
                        typesMissing.RemoveAt(step);
                        attachAttempts--;
                        step--;
                        continue;
                    }
                    else
                    {
                        TankBlock foundBlock = RawTechLoader.SpawnBlockS(bType, blockSpawnPos, Quaternion.identity, out bool worked);
                        if (!worked)
                        {
                            DebugTAC_AI.Log("TACtical AI: TrySpawnAndAttachBlockFromList - Could not spawn block " + bType);
                            continue;
                        }
                        bool attemptW;

                        //Debug.Log("TACtical AI: TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                        for (int step2 = 0; step2 < count; step2++)
                        {
                            BlockMemory template = posBlocks.ElementAt(step2);
                            attemptW = AttemptBlockAttachImmediate(template, foundBlock, purchase);
                            if (attemptW)
                            {
                                foundBlock.SetSkinByUniqueID(RawTechLoader.GetSkinIDSetForTeam(tank.Team, (int)ManSpawn.inst.GetCorporation(bType)));
                                //foundBlock.InitNew();
                                if (count == 1)
                                    typesMissing.Remove(bType);
                                if (playerInventory)
                                    BlockAvailInInventory(tank, bType, true);
                                return true;
                            }
                        }
                        //Debug.Log("TACtical AI: TurboRepair - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

                        ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                    }
                    // if everything fails, resort to timbuktu
                    //foundBlock.damage.SelfDestruct(0.1f);
                    //Vector3 yeet = Vector3.forward * 450000;
                    //foundBlock.transform.position = yeet;
                }
                return false;
            }


            //tank.boundsCentreWorldNoCheck + (Vector3.up * 128)
            private bool IterateAndTryAttachBlockMP(BlockTypes bType, ref List<BlockTypes> typesMissing, Vector3 blockSpawnPos, bool playerInventory = false, bool purchase = false)
            {
                if (playerInventory)
                {
                    if (!BlockAvailInInventory(tank, bType))
                        return false;
                }
                else if (purchase && !HandlePurchase())
                {
                    ranOutOfParts = true;
                    thisInst.PendingDamageCheck = false;
                    return false;
                }
                ranOutOfParts = false;

                List<BlockMemory> posBlocks = ReturnAllPositionsOfType(bType);
                int count = posBlocks.Count();
                if (count == 0)
                    return false;


                TankBlock prefabBlock = RawTechLoader.GetPrefabFiltered(bType, blockSpawnPos);
                if (!prefabBlock)
                {
                    DebugTAC_AI.Log("TACtical AI: IterateAndTryAttachBlockSkinMP - Could not fetch block");
                    return false;
                }
                bool attemptW;


                //Debug.Log("TACtical AI: TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                for (int step2 = 0; step2 < count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = tank.CanAttachBlock(prefabBlock, template.p, new OrthoRotation(template.r));
                    if (attemptW)
                    {
                        TankBlock foundBlock = RawTechLoader.SpawnBlockNoCheck(bType, blockSpawnPos, Quaternion.identity);

                        AttemptBlockAttachImmediate(template, foundBlock, purchase);

                        //foundBlock.InitNew();
                        if (count == 1)
                            typesMissing.Remove(bType);
                        if (playerInventory)
                            BlockAvailInInventory(tank, bType, true);
                        prefabBlock.visible.RemoveFromGame();
                        return true;
                    }
                }
                //Debug.Log("TACtical AI: IterateAndTryAttachBlock - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");
                prefabBlock.visible.RemoveFromGame();
                return false;
            }
            private bool IterateAndTryAttachBlockSkinMP(BlockTypes bType, ref List<BlockTypes> typesMissing, Vector3 blockSpawnPos, bool playerInventory = false, bool purchase = false)
            {
                if (playerInventory)
                {
                    if (!BlockAvailInInventory(tank, bType))
                        return false;
                }
                else if (purchase && !HandlePurchase())
                {
                    ranOutOfParts = true;
                    thisInst.PendingDamageCheck = false;
                    return false;
                }
                ranOutOfParts = false;

                TankBlock prefabBlock = RawTechLoader.GetPrefabFiltered(bType, blockSpawnPos);
                if (!prefabBlock)
                {
                    DebugTAC_AI.Log("TACtical AI: IterateAndTryAttachBlockSkinMP - Could not fetch block");
                    return false;
                }
                bool attemptW;

                List<BlockMemory> posBlocks = ReturnAllPositionsOfType(bType);
                int count = posBlocks.Count();
                if (count == 0)
                    return false;

                //Debug.Log("TACtical AI: TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                for (int step2 = 0; step2 < count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = tank.CanAttachBlock(prefabBlock, template.p, new OrthoRotation(template.r));
                    if (attemptW)
                    {
                        TankBlock foundBlock = RawTechLoader.SpawnBlockNoCheck(bType, blockSpawnPos, Quaternion.identity);

                        AttemptBlockAttachImmediate(template, foundBlock, purchase);
                        foundBlock.SetSkinByUniqueID(RawTechLoader.GetSkinIDSetForTeam(tank.Team, (int)ManSpawn.inst.GetCorporation(bType)));
                        //foundBlock.InitNew();
                        if (count == 1)
                            typesMissing.Remove(bType);
                        if (playerInventory)
                            BlockAvailInInventory(tank, bType, true);
                        prefabBlock.visible.RemoveFromGame();
                        return true;
                    }
                }
                prefabBlock.visible.RemoveFromGame();
                //Debug.Log("TACtical AI: IterateAndTryAttachBlock - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

                return false;
            }
        }

        public static bool IsValidRotation(TankBlock TB, OrthoRotation.r r)
        {

            return true; // can't fetch proper context for some reason
            Singleton.Manager<ManTechBuilder>.inst.ClearBlockRotationOverride(TB);
            OrthoRotation.r[] rots = Singleton.Manager<ManTechBuilder>.inst.GetBlockRotationOrder(TB);
            Singleton.Manager<ManTechBuilder>.inst.ClearBlockRotationOverride(TB);
            if (rots != null && rots.Length > 0 && !rots.Contains(r))
            {   // block cannot be saved - illegal rotation.
                return false;
            }
            return true;
        }

        public static OrthoRotation SetCorrectRotation(Quaternion blockRot, Quaternion changeRot)
        {
            Quaternion qRot2 = Quaternion.identity;
            Vector3 endRotF = blockRot * Vector3.forward;
            Vector3 endRotU = blockRot * Vector3.up;
            Vector3 foA = changeRot * endRotF;
            Vector3 upA = changeRot * endRotU;
            qRot2.SetLookRotation(foA, upA);
            OrthoRotation rot = new OrthoRotation(qRot2);
            if (rot != qRot2)
            {
                bool worked = false;
                for (int step = 0; step < OrthoRotation.NumDistinctRotations; step++)
                {
                    OrthoRotation rotT = new OrthoRotation(OrthoRotation.AllRotations[step]);
                    bool isForeMatch = rotT * Vector3.forward == foA;
                    bool isUpMatch = rotT * Vector3.up == upA;
                    if (isForeMatch && isUpMatch)
                    {
                        rot = rotT;
                        worked = true;
                        break;
                    }
                }
                if (!worked)
                {
                    DebugTAC_AI.Log("RandomAdditions: ReplaceBlock - Matching failed - OrthoRotation is missing edge case");
                }
            }
            return rot;
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

            return BlockIndexer.StringToBlockType(mem.t);
        }


        public static bool AIBlockAttachRequest(Tank tank, BlockMemory template, TankBlock canidate)
        {
            bool success;
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            if (!ManNetwork.inst.IsMultiplayer())
                success = Singleton.Manager<ManLooseBlocks>.inst.RequestAttachBlock(tank, canidate, template.p, new OrthoRotation(template.r));
            else
                success = BlockAttachNetworkOverride(tank, template, canidate);
            return success;
        }
        private static bool BlockAttachNetworkOverride(Tank tank, BlockMemory template, TankBlock canidate)
        {
            if (!ManNetwork.IsHost)
            {
                DebugTAC_AI.Log("TACtical_AI: ASSERT: BlockAttachNetworkOverride - Called in non-host sitsuation!");
                return false;// CANNOT DO THIS WHEN NOT HOST OR ERROR 
            }
            bool attached = false;
            
            if (canidate == null)
            {
                DebugTAC_AI.Log("TACtical_AI: BlockAttachNetworkOverride - BLOCK IS NULL!");
            }
            else
            {
                NetBlock netBlock = canidate.netBlock;
                if (netBlock.IsNull())
                {
                    DebugTAC_AI.Log("TACtical_AI: BlockAttachNetworkOverride - NetBlock could not be found on AI block attach attempt!");
                }
                else
                {
                    NonPlayerAttachAllow = true;
                    BlockAttachedMessage message = new BlockAttachedMessage
                    {
                        m_TechNetId = tank.netTech.netId,
                        m_BlockPosition = template.p,
                        m_BlockOrthoRotation = (int)template.r,
                        m_BlockPoolID = canidate.blockPoolID,
                    };
                    Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.BlockAttach, message, tank.netTech.OwnerNetId);
                    attached = canidate.tank == tank;
                    NonPlayerAttachAllow = false;
                }
            }
            return attached;
        }




        // Player AI respective repair operations
        private static bool PreRepairPrep(Tank tank, DesignMemory TechMemor)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (TechMemor.IsNull())
            {
                DebugTAC_AI.Log("TACtical_AI: RepairLerp called with no valid DesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
                return false;
            }
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount)
            {
                DebugTAC_AI.Log("TACtical_AI: Player AI " + tank.name + ":  New blocks were added without " +
                    "being saved before building.  Was the player messing with the Tech?");
                TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                return true;
            }
            return false;
        }
        private static bool RepairLerp(Tank tank, DesignMemory TechMemor, AIECore.TankAIHelper thisInst, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing)
        {
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to repair");
            if (ManNetwork.IsNetworked)
                return RepairLerpInstant(tank, TechMemor, thisInst, ref fBlocks, ref typesMissing);

            if (TechMemor.TryAttachExistingBlockFromList(ref typesMissing, ref fBlocks))
                return true;
            if (thisInst.useInventory)
            {
                //Debug.Log("TACtical AI: RepairLerp - Attempting to repair from inventory");
                RawTechLoader.ResetSkinIDSet();
                if (TechMemor.TrySpawnAndAttachBlockFromList(ref typesMissing, true))
                    return true;
            }
            return false;
        }
        private static bool RepairLerpInstant(Tank tank, DesignMemory TechMemor, AIECore.TankAIHelper thisInst, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing)
        {
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to repair");

            if (TechMemor.TryAttachExistingBlockFromListInst( ref typesMissing, ref fBlocks))
                return true;
            if (thisInst.useInventory)
            {
                //Debug.Log("TACtical AI: RepairLerp - Attempting to repair from inventory");
                RawTechLoader.ResetSkinIDSet();
                if (TechMemor.TrySpawnAndAttachBlockFromListInst(ref typesMissing, true))
                    return true;
            }
            return false;
        }
        public static bool InstaRepair(Tank tank, DesignMemory TechMemor, int RepairAttempts = 0)
        {
            bool success = false;
            if (TechMemor.SystemsCheck() && PreRepairPrep(tank, TechMemor))
            {
                //List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                TechMemor.RushAttachOpIfNeeded();
                if (RepairAttempts == 0)
                    RepairAttempts = TechMemor.ReturnContents().Count();

                AIECore.TankAIHelper help = TechMemor.thisInst;

                List<TankBlock> fBlocks = TechMemor.FindBlocksNearbyTank();
                List<BlockTypes> typesMissing = TechMemor.GetMissingBlockTypes();
                RawTechLoader.ResetSkinIDSet();
                BulkAdding = true;
                while (RepairAttempts > 0)
                {
                    bool worked = RepairLerpInstant(tank, TechMemor, help, ref fBlocks, ref typesMissing);
                    if (!worked)
                        break;
                    if (!TechMemor.SystemsCheck())
                    {
                        success = true;
                        break;
                    }
                    RepairAttempts--;
                }
                TechMemor.UpdateMissingBlockTypes(typesMissing);
                BulkAdding = false;
            }
            return success;
        }
        public static bool RepairStepper(AIECore.TankAIHelper thisInst, Tank tank, DesignMemory TechMemor, bool AdvancedAI = false, bool Combat = false)
        {
            if (thisInst.RepairStepperClock <= 0)
            {
                float prevVal = thisInst.RepairStepperClock;

                if (AIGlobals.TurboAICheat)
                {
                    thisInst.RepairStepperClock = 0;
                    thisInst.TechMemor.ReserveSuperGrabs = 5 * KickStart.AIClockPeriod;
                }
                else if (Combat)
                {
                    if (AdvancedAI)
                        thisInst.RepairStepperClock = sDelayCombat;
                    else
                        thisInst.RepairStepperClock = delayCombat;
                }
                else
                {
                    if (AdvancedAI)
                        thisInst.RepairStepperClock = sDelaySafe;
                    else
                        thisInst.RepairStepperClock = delaySafe;
                }
                if (thisInst.PendingDamageCheck) //&& thisInst.AttemptedRepairs == 0)
                {
                    if (thisInst.RepairStepperClock < 1)
                        thisInst.RepairStepperClock = 1;
                    int initialBlockCount = tank.blockman.blockCount;
                    float OverdueTime = Mathf.Abs(prevVal / thisInst.RepairStepperClock);
                    if (OverdueTime >= 2)
                    {
                        int blocksToAdd = Mathf.CeilToInt(OverdueTime);
                        thisInst.PendingDamageCheck = !InstaRepair(tank, TechMemor, blocksToAdd);
                        thisInst.RepairStepperClock -= (OverdueTime - blocksToAdd) * thisInst.RepairStepperClock;
                    }
                    else if (TechMemor.SystemsCheck() && PreRepairPrep(tank, TechMemor))
                    {   // Cheaper to check twice than to use GetMissingBlockTypes when not needed.
                        thisInst.RepairStepperClock -= OverdueTime * thisInst.RepairStepperClock;
                        TechMemor.RushAttachOpIfNeeded();
                        List<TankBlock> fBlocks = TechMemor.FindBlocksNearbyTank();
                        List<BlockTypes> typesMissing = TechMemor.GetMissingBlockTypes();

                        RepairLerp(tank, TechMemor, thisInst, ref fBlocks, ref typesMissing);
                        TechMemor.UpdateMissingBlockTypes(typesMissing);
                        thisInst.PendingDamageCheck = TechMemor.SystemsCheck();
                        //thisInst.AttemptedRepairs = 1;
                    }
                    else
                        thisInst.PendingDamageCheck = false;

                    if (!thisInst.PendingDamageCheck && initialBlockCount != tank.blockman.blockCount)
                    {
                        DebugTAC_AI.Log("TACtical_AI: AlliedRepairStepper - Done for " + tank.name);
                        thisInst.FinishedRepairEvent.Send();
                    }
                    //Debug.Log("TACtical AI: RepairStepper(" + tank.name + ") - Pending check: " + thisInst.PendingSystemsCheck);
                }
            }
            else
                thisInst.RepairStepperClock -= KickStart.AIClockPeriod;
            return thisInst.PendingDamageCheck;
        }
        
        /*// Abandoned - Too Technical!
        public static void AssistedRepair(Tank tank)
        {
            List<Binding.SnapshotLiveData> Snaps = ManSnapshots.inst.m_Snapshots.ToList();
            if (Snaps.Count == 0)
                return;
            foreach (Binding.SnapshotLiveData snap in Snaps)
            {
                try
                {
                    snap.m_Snapshot.
                }
                catch
                {
                }
            }
        }*/


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
                        if (TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.t == foundBlock.block.name; }).Count() > 0)
                        {
                            blocksNearby = true;
                            break;
                        }
                    }
                }
            }
            if (thisInst.AIState == AIAlignment.NonPlayer)
            {
                var mind = tank.GetComponent<Enemy.EnemyMind>();
                if ((mind.AllowRepairsOnFly || (thisInst.lastEnemy.IsNull())) && (blocksNearby || KickStart.EnemiesHaveCreativeInventory || mind.AllowInvBlocks))
                {
                    return true;
                }
            }
            else if (thisInst.AIState == AIAlignment.Player && (blocksNearby || thisInst.useInventory))
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
            return TechMemor.SystemsCheck();
        }
        public static bool BlockAvailInInventory(Tank tank, BlockTypes blockType, bool consumeBlock = false)
        {
            if (!ManSpawn.IsPlayerTeam(tank.Team))
                return true;// Non-player Teams don't actually come with limited inventories.  strange right?
            if (!consumeBlock)
            {
                if (Singleton.Manager<ManPlayer>.inst.InventoryIsUnrestricted)
                {
                    //no need to return to infinite stockpile
                }
                else
                {
                    try
                    {
                        bool isMP = Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer();
                        if (isMP)
                        {
                            if (Singleton.Manager<NetInventory>.inst.IsAvailableToLocalPlayer(blockType))
                            {
                                return Singleton.Manager<NetInventory>.inst.GetQuantity(blockType) > 0;
                            }
                        }
                        else
                        {
                            if (Singleton.Manager<SingleplayerInventory>.inst.IsAvailableToLocalPlayer(blockType))
                            {
                                return Singleton.Manager<SingleplayerInventory>.inst.GetQuantity(blockType) > 0;
                            }
                        }
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Tried to repair but block " + blockType.ToString() + " was not found!");
                    }
                }
                return false;
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
                    DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Tried to repair but block " + blockType.ToString() + " was not found!");
                }
            }
            return isAvail;
        }


        // EXPERIMENTAL - AI-Based new Tech building
        internal static void SetupForNewTechConstruction(DesignMemory TechMemor, List<TankBlock> tankTemplate)
        {
            TechMemor.SaveTech(tankTemplate.FindAll(delegate (TankBlock cand) { return cand != null; }));
        }
        internal static void Turboconstruct(Tank tank, DesignMemory TechMemor, bool fullyCharge = true)
        {
            DebugTAC_AI.Log("TACtical_AI:  DesignMemory: Turboconstructing " + tank.name + ", count " + TechMemor.ReturnContents().Count());
            int cBCount = tank.blockman.IterateBlocks().ToList().Count();
            int RepairAttempts = TechMemor.ReturnContents().Count() - cBCount + 3;
            if (TechMemor.IsNull())
            {
                DebugTAC_AI.Log("TACtical_AI: TurboRepair called with no valid EnemyDesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
                return;
            }
            try
            {
                TechMemor.RushAttachOpIfNeeded();
                List<BlockTypes> typesMissing = TechMemor.GetMissingBlockTypes();
                while (RepairAttempts > 0)
                {
                    TurboRepair(tank, TechMemor, ref typesMissing);
                    RepairAttempts--;
                }
                TechMemor.UpdateMissingBlockTypes(typesMissing);
            }
            catch { return; }
            if (fullyCharge)
                tank.EnergyRegulator.SetAllStoresAmount(1);
        }
        internal static void Turboconstruct(Tank tank, DesignMemory TechMemor, ref List<TankBlock> provided)
        {
            DebugTAC_AI.Log("TACtical_AI:  DesignMemory: Turboconstructing " + tank.name + ", count " + TechMemor.ReturnContents().Count());
            int cBCount = tank.blockman.IterateBlocks().ToList().Count();
            int RepairAttempts = TechMemor.ReturnContents().Count() - cBCount + 3;
            if (TechMemor.IsNull())
            {
                DebugTAC_AI.Log("TACtical_AI: TurboRepair called with no valid EnemyDesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
                return;
            }
            try
            {
                TechMemor.RushAttachOpIfNeeded();
                List<BlockTypes> typesMissing = TechMemor.GetMissingBlockTypes();
                while (RepairAttempts > 0)
                {
                    TurboRepairSupplies(tank, TechMemor, ref typesMissing, ref provided);
                    RepairAttempts--;
                }
                TechMemor.UpdateMissingBlockTypes(typesMissing);
            }
            catch { return; }
        }
        internal static void TurboRepair(Tank tank, DesignMemory TechMemor, ref List<BlockTypes> typesMissing)
        {
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = tank.blockman.IterateBlocks().ToList().Count;
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount != cBCount)
            {
                //Debug.Log("TACtical AI: TurboRepair - Attempting to repair from infinity - " + typesMissing.Count());
                if (!TechMemor.TrySpawnAndAttachBlockFromListInst(ref typesMissing, false, false))
                    DebugTAC_AI.Log("TACtical AI: TurboRepair - attach attempt failed");
            }
            return;
        }
        internal static void TurboRepairSupplies(Tank tank, DesignMemory TechMemor, ref List<BlockTypes> typesMissing, ref List<TankBlock> provided)
        {
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = tank.blockman.IterateBlocks().ToList().Count;
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount != cBCount)
            {
                //Debug.Log("TACtical AI: TurboRepair - Attempting to repair from infinity - " + typesMissing.Count());
                if (!TechMemor.TryAttachExistingBlockFromListInst(ref typesMissing, ref provided, false))
                    DebugTAC_AI.Log("TACtical AI: TurboRepair - attach attempt failed");
            }
            return;
        }


        // External major operations
        /// <summary>
        /// Builds a Tech instantly, no requirements
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="TechMemor"></param>
        public static void TurboconstructExt(Tank tank, List<BlockMemory> Mem, bool fullyCharge = true)
        {
            DebugTAC_AI.Log("TACtical_AI:  DesignMemory: Turboconstructing " + tank.name);
            int cBCount = tank.blockman.IterateBlocks().ToList().Count();
            int RepairAttempts = Mem.Count() - cBCount + 3;
            try
            {
                List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                List<BlockTypes> typesMissing = GetMissingBlockTypesExt(Mem, cBlocks);
                while (RepairAttempts > 0)
                {
                    TurboRepairExt(tank, Mem, ref typesMissing);
                    RepairAttempts--;
                }
            }
            catch { return; }
            if (fullyCharge)
                tank.EnergyRegulator.SetAllStoresAmount(1);
        }
        /// <summary>
        /// Builds a Tech instantly, no requirements
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="TechMemor"></param>
        public static void TurboconstructExt(Tank tank, List<BlockMemory> Mem, List<TankBlock> provided, bool fullyCharge = true)
        {
            DebugTAC_AI.Log("TACtical_AI:  DesignMemory: Turboconstructing " + tank.name);
            int cBCount = tank.blockman.IterateBlocks().ToList().Count();
            int RepairAttempts = Mem.Count() - cBCount + 3;
            try
            {
                List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                List<BlockTypes> typesMissing = GetMissingBlockTypesExt(Mem, cBlocks);
                while (RepairAttempts > 0)
                {
                    TurboRepairExt(tank, Mem, ref typesMissing, ref provided);
                    RepairAttempts--;
                }
            }
            catch { return; }
            if (fullyCharge)
                tank.EnergyRegulator.SetAllStoresAmount(1);
        }
        /// <summary>
        /// EXTERNAL
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="Mem"></param>
        /// <param name="typesMissing"></param>
        public static void TurboRepairExt(Tank tank, List<BlockMemory> Mem, ref List<BlockTypes> typesMissing)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            int savedBCount = Mem.Count;
            int cBCount = cBlocks.Count;
            if (savedBCount != cBCount)
            {

                //Debug.Log("TACtical AI: TurboRepair - Attempting to repair from infinity - " + typesToRepair.Count());
                if (!TrySpawnAndAttachBlockFromListExt(tank, Mem, ref typesMissing))
                    DebugTAC_AI.Log("TACtical AI: TurboRepair - attach attempt failed");
            }
            return;
        }
        public static void TurboRepairExt(Tank tank, List<BlockMemory> Mem, ref List<BlockTypes> typesMissing, ref List<TankBlock> provided)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            int savedBCount = Mem.Count;
            int cBCount = cBlocks.Count;
            if (savedBCount != cBCount)
            {

                //Debug.Log("TACtical AI: TurboRepair - Attempting to repair from infinity - " + typesToRepair.Count());
                if (!TryAttachExistingBlockFromListExt(tank, Mem, ref typesMissing, ref provided))
                    DebugTAC_AI.Log("TACtical AI: TurboRepair - attach attempt failed");
            }
            return;
        }
        public static List<BlockTypes> GetMissingBlockTypesExt(List<BlockMemory> Mem, List<TankBlock> cBlocks)
        {
            List<BlockTypes> typesToRepair = new List<BlockTypes>();
            int toFilter = Mem.Count();
            List<string> filteredNames = new List<string>();
            for (int step = 0; step < toFilter; step++)
            {
                filteredNames.Add(Mem.ElementAt(step).t);
            }
            filteredNames = filteredNames.Distinct().ToList();
            for (int step = 0; step < filteredNames.Count; step++)
            {
                typesToRepair.Add(BlockIndexer.StringToBlockType(filteredNames.ElementAt(step)));
            }
            //typesToRepair = typesToRepair.Distinct().ToList();

            List<BlockTypes> typesMissing = new List<BlockTypes>();
            int toFilter2 = typesToRepair.Count();
            for (int step = 0; step < toFilter2; step++)
            {
                int present = cBlocks.FindAll(delegate (TankBlock cand) { return typesToRepair[step] == cand.BlockType; }).Count;
                string Name = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(typesToRepair[step]).name;

                int mem = Mem.FindAll(delegate (BlockMemory cand) { return Name == cand.t; }).Count;
                if (mem > present)// are some blocks not accounted for?
                    typesMissing.Add(typesToRepair[step]);
            }
            return typesMissing;
        }
        private static bool TrySpawnAndAttachBlockFromListExt(Tank tank, List<BlockMemory> Mem, ref List<BlockTypes> typesMissing)
        {
            int attachAttempts = typesMissing.Count();
            for (int step = 0; step < attachAttempts; step++)
            {
                BlockTypes bType = typesMissing.ElementAt(step);

                TankBlock foundBlock = null;

                foundBlock = Templates.RawTechLoader.SpawnBlockS(bType, tank.boundsCentreWorldNoCheck + (Vector3.up * 64), Quaternion.identity, out bool worked);
                if (!worked)
                {
                    continue;
                }
                bool attemptW;

                int hash = foundBlock.name.GetHashCode();
                List<BlockMemory> posBlocks = Mem.FindAll(delegate (BlockMemory cand) { return cand.t.GetHashCode() == hash;});
                if (posBlocks.Count == 0)
                {
                    typesMissing.RemoveAt(step);
                    step--;
                    attachAttempts--;
                    continue;
                }
                //Debug.Log("TACtical AI: TurboRepair - potental spots " + posBlocks.Count + " for block " + foundBlock.name);
                for (int step2 = 0; step2 < posBlocks.Count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = AttemptBlockAttachExt(tank, template, foundBlock);
                    if (attemptW)
                    {
                        //foundBlock.InitNew();
                        return true;
                    }
                }
                foundBlock.transform.Recycle();
            }
            return false;
        }
        public static bool TryAttachExistingBlockFromListExt(Tank tank, List<BlockMemory> mem, ref List<BlockTypes> typesMissing, ref List<TankBlock> foundBlocks, bool denySD = false)
        {
            int attachAttempts = foundBlocks.Count();
            //Debug.Log("TACtical AI: RepairLerp - Found " + attachAttempts + " loose blocks to use");
            for (int step = 0; step < attachAttempts; step++)
            {
                TankBlock foundBlock = foundBlocks[step];
                BlockTypes BT = foundBlock.BlockType;
                if (!typesMissing.Contains(BT))
                    continue;
                bool attemptW;
                // if we are smrt, run heavier operation
                int hash = foundBlock.name.GetHashCode();
                List<BlockMemory> posBlocks = mem.FindAll(delegate (BlockMemory cand) { return cand.t.GetHashCode() == hash;});
                int count = posBlocks.Count;
                //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                for (int step2 = 0; step2 < count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = AttemptBlockAttachExt(tank, template, foundBlock);
                    if (attemptW)
                    {
                        if (denySD)
                        {
                            foundBlock.damage.AbortSelfDestruct();
                        }
                        foundBlocks.RemoveAt(step);
                        return true;
                    }
                }
            }
            return false;
        }
        private static bool AttemptBlockAttachExt(Tank tank, BlockMemory template, TankBlock canidate)
        {
            //Debug.Log("TACtical_AI: (AttemptBlockAttachExt) AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            return AIBlockAttachRequest(tank, template, canidate);
        }


        // Util
        private static void CheckGameTamperedWith(Tank tank, DesignMemory mem)
        {
            string blockCurrent = tank.blockman.GetBlockAtPosition(new IntVector3(0, 0, 0)).name;
            string blockSaved = mem.ReturnContents().First().t;
            if (blockCurrent != blockSaved)
            {
                DebugTAC_AI.Log("TACtical_AI: AI " + tank.name + ":  Expected " + blockSaved + " at 0,0,0 local blockman, found " + (blockCurrent.NullOrEmpty() ? "NO BLOCK" : blockCurrent.ToString()) + " instead.");
            }
        }
    }
}
