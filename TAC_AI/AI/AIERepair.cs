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
using System.Diagnostics;
using static TAC_AI.ManBaseTeams;
using UnityEngine.Experimental.PlayerLoop;

namespace TAC_AI.AI
{
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

        private static StringBuilder SB = new StringBuilder();

        public class DesignMemory : MonoBehaviour
        {   // Save the design on load!
            private Tank tank;
            internal TankAIHelper Helper;
            internal bool rejectSaveAttempts = false;
            internal bool ranOutOfParts = false;
            internal bool conveyorsBorked = false;
            internal int ReserveSuperGrabs = 0;
            private Func<BlockTypes, bool> purchaseOp;
            private RawBlockMem templateCache;

            private readonly List<RawBlockMem> SavedTech = new List<RawBlockMem>();
            internal bool blockIntegrityDirty = true;
            private Dictionary<BlockTypes, List<RawBlockMem>> fastBlockLookup = new Dictionary<BlockTypes, List<RawBlockMem>>();
            private List<BlockTypes> MissingTypes = new List<BlockTypes>();
            private static Stopwatch saveDelay = new Stopwatch();

            // Handling this
            internal void Initiate(bool DoFirstSave = true)
            {
                tank = gameObject.GetComponent<Tank>();
                Helper = gameObject.GetComponent<TankAIHelper>();
                tank.DetachEvent.Subscribe(Compromised);
                Helper.FinishedRepairEvent.Subscribe(OnFinishedBuilding);
                Helper.TechMemor = this;
                Helper.PendingDamageCheck = true;
                blockIntegrityDirty = true;
                purchaseOp = EnemyPurchase;
                if (DoFirstSave)
                {
                    if (SavedTech.Any() || BookmarkBuilder.TryGet(tank, out BookmarkBuilder BB))
                    {
                        DebugTAC_AI.LogAISetup("Design for " + tank.name + ", ID [" + tank.visible.ID + "] was assigned by BookmarkBuilder, using it.");
                    }
                    else
                    {
                        if (SaveTech())
                            DebugTAC_AI.LogAISetup("Saving base design for " + tank.name + ", ID [" + tank.visible.ID + "] for use in repairs");
                        else
                            DebugTAC_AI.Log("Design for " + tank.name + ", ID [" + tank.visible.ID + "] was assigned somehow??? (rejectSaveAttempts was TRUE!?), using it...");
                        /*
                        DebugTAC_AI.Log("Designs within BookmarkBuilder: " + BookmarkBuilder.count);
                        foreach (var item in BookmarkBuilder.Plans)
                        {
                            DebugTAC_AI.Log(item.Key.visible.ID + ", " + item.Key.name);
                        }
                        */
                    }
                    //Invoke("SaveTech", 0.01f);
                }
                rejectSaveAttempts = false;
            }
            internal void Remove()
            {
                CancelInvoke();
                tank.DetachEvent.Unsubscribe(Compromised);
                Helper.FinishedRepairEvent.Unsubscribe(OnFinishedBuilding);
                DestroyImmediate(this);
            }

            internal void OnFinishedBuilding(TankAIHelper unused)
            {
                if (conveyorsBorked)
                {
                    RawTechLoader.ReconstructConveyorSequencing(tank);
                    conveyorsBorked = false;
                }
            }
            internal void Compromised(TankBlock removedBlock, Tank tank)
            {
                if (Helper.AIAlign == AIAlignment.Player)
                {
                    if (!ManNetwork.IsNetworked)
                    {
                        if (!Helper.BoltsFired && ManPointer.inst.targetVisible)
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
                        if (Helper.lastPlayer)
                        {
                            if (Helper.lastEnemyGet && !Helper.BoltsFired)// only save when not in combat Or exploding bolts
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
            public bool SaveTech()
            {
                if (rejectSaveAttempts)
                    return false;
                blockIntegrityDirty = false;
                MissingTypes.Clear();

                if (KickStart.DesignsToLog)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory - DESIGNS TO LOG IS ENABLED!!!");
                    TechToJSONLog();
                    return true;
                }
                SavedTech.Clear();

                foreach (TankBlock bloc in tank.blockman.IterateBlocks())
                {
                    RawBlockMem mem = new RawBlockMem
                    {
                        t = bloc.name,
                        p = bloc.cachedLocalPosition,
                        r = bloc.cachedLocalRotation.rot
                    };
                    SavedTech.Add(mem);
                }
                if (!SavedTech.Any())
                {
                    DebugTAC_AI.Info(KickStart.ModID + ": INVALID TECH DATA SAVED FOR TANK " + tank.name + "\n" +StackTraceUtility.ExtractStackTrace());
                }
                DebugTAC_AI.Info(KickStart.ModID + ":  DesignMemory - Saved " + tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - tank.CentralBlock.cachedLocalPosition).sqrMagnitude).ToList();
                ValidateTechIfNeeded();
                BuildTechQuickLookup();
                return true;
            }
            public void SaveTech(List<TankBlock> overwrite)
            {
                blockIntegrityDirty = true;
                MissingTypes.Clear();
                SavedTech.Clear();
                foreach (TankBlock bloc in overwrite)
                {
                    RawBlockMem mem = new RawBlockMem
                    {
                        t = bloc.name,
                        p = bloc.cachedLocalPosition,
                        r = bloc.cachedLocalRotation.rot
                    };
                    SavedTech.Add(mem);
                }
                DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory - Overwrote(SaveTech) " + tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
                ValidateTechIfNeeded();
                BuildTechQuickLookup();
                rejectSaveAttempts = true;
            }

            private void ValidateTechIfNeeded()
            {
                if (ManNetwork.IsNetworked)
                    if (!ModTechsDatabase.ValidateBlocksInTechAndPurgeIfNeeded(SavedTech))
                        DebugTAC_AI.Log(KickStart.ModID + ": DesignMemory - Found illegal blocks for " + tank.name
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
                foreach (var item in IterateBlockTypesFromMemory())
                {
                    string Name = ManSpawn.inst.GetBlockPrefab(item).name;

                    int hash = Name.GetHashCode();
                    if (!fastBlockLookup.TryGetValue(item, out List<RawBlockMem> newEntry))
                    {
                        newEntry = new List<RawBlockMem>();
                        foreach (var position in SavedTech.FindAll(delegate (RawBlockMem cand) { return cand.t.GetHashCode() == hash; }))
                        {
                            newEntry.Add(position);
                        }
                        fastBlockLookup.Add(item, newEntry);
                        //Debug.Info(KickStart.ModID + ": BuildTechQuickLookup - processed " + LP.Count + " entries for " + item);
                    }
                    else
                    {
                        /*
                        foreach (var position in SavedTech.FindAll(delegate (RawBlockMem cand) { return cand.t.GetHashCode() == hash; }))
                        {
                            newEntry.Add(position);
                        }*/
                    }
                }
            }


            public void MemoryToTech(List<RawBlockMem> overwrite)
            {   // Loading a Tech from the BlockMemory
                blockIntegrityDirty = true;
                MissingTypes.Clear();
                SavedTech.Clear();
                foreach (RawBlockMem mem in overwrite)
                {
                    BlockTypes type = BlockIndexer.StringToBlockType(mem.t);
                    if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory - " + tank.name + ": could not save " + mem.t + " in blueprint due to illegal block.");
                        continue;
                    }
                    // get rid of floating point errors
                    mem.TidyUp();
                    SavedTech.Add(mem);
                }
                DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory - Overwrote(MemoryToTech) " + tank.name + ", ID (" + tank.visible.ID + ")");
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
                ValidateTechIfNeeded();
                BuildTechQuickLookup();
                rejectSaveAttempts = true;
            }
            public TankBlock TryFindProperRootBlock(List<TankBlock> ToSearch)
            {
                return RawTechTemplate.FindProperRootBlockExternal(ToSearch);
            }
            public List<RawBlockMem> TechToMemory()
            {
                return RawTechTemplate.TechToMemoryExternal(tank);
            }

            private void VerifyIntegrity()
            {
                if (!blockIntegrityDirty)
                    return;
                IEnumerable<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList().AsEnumerable();
                if (!cBlocks.Any(x => true))
                {
                    DebugTAC_AI.Assert(true, KickStart.ModID + ": ASSERT - VerifyIntegrity - Called on Tank with ZERO blocks!");
                    return;
                }
                MissingTypes.Clear();
                foreach (BlockTypes repairCase in IterateBlockTypesFromMemory())
                {
                    int present = cBlocks.Count(delegate (TankBlock cand) { return repairCase == cand.BlockType; });
                    string Name = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(repairCase).name;

                    int mem2 = ReturnAllPositionsOfType(repairCase).Count;
                    if (mem2 > present)// are some blocks not accounted for?
                        MissingTypes.Add(repairCase);
                }
                DebugTAC_AI.Info(KickStart.ModID + ": VerifyIntegrity - Executed with " + MissingTypes.Count + " results");
                blockIntegrityDirty = false;
            }
            private void VerifyIntegritySLOW()
            {
                if (!blockIntegrityDirty)
                    return;
                List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                if (cBlocks.Count() == 0)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ASSERT - VerifyIntegrity - Called on Tank with ZERO blocks!");
                    return;
                }
                MissingTypes.Clear();
                List<BlockTypes> typesToRepair = new List<BlockTypes>();
                List<RawBlockMem> mem = IterateReturnContents();
                int toFilter = mem.Count;
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

                    int mem2 = mem.FindAll(delegate (RawBlockMem cand) { return Name == cand.t; }).Count;
                    if (mem2 > present)// are some blocks not accounted for?
                        MissingTypes.Add(typesToRepair[step]);
                }
                DebugTAC_AI.Log(KickStart.ModID + ": VerifyIntegrity - Executed with " + MissingTypes.Count + " results");
                blockIntegrityDirty = false;
            }

            // Gets
            public bool HasFullHealth()
            {
                return Helper.DamageThreshold.Approximately(0);
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
                    DebugTAC_AI.Info(KickStart.ModID + ": Tech " + tank.name + " has 0 saved blocks in TechMemor.  How?");
                    return false;
                }
                Helper.DamageThreshold = (1 - (tank.blockman.blockCount / totalDesignBlocks)) * 100;
                DebugTAC_AI.Info(KickStart.ModID + ": Tech " + tank.name + " has damage percent of " + Helper.DamageThreshold);
                if (!Helper.DamageThreshold.Approximately(0) && !ranOutOfParts)
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
            private static List<BlockTypes> typesToRepair = new List<BlockTypes>();
            private static HashSet<string> namesDone = new HashSet<string>();
            public IEnumerable<BlockTypes> IterateBlockTypesFromMemory()
            {
                if (fastBlockLookup.Count > 0)
                    return fastBlockLookup.Keys;
                List<RawBlockMem> mem = IterateReturnContents();
                namesDone.Clear();
                typesToRepair.Clear();
                for (int step = 0; step < mem.Count; step++)
                {
                    if (!namesDone.Contains(mem[step].t))
                    {
                        typesToRepair.Add(BlockIndexer.StringToBlockType(mem[step].t));
                        namesDone.Add(mem[step].t);
                    }
                }
                return typesToRepair;
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
                    if (UnityEngine.Random.Range(0, 2000) < 150)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Enemy AI " + tank.name + " reclaim attempt success");
                        ReserveSuperGrabs++;
                        return true;
                    }
                }
                else
                {
                    if (UnityEngine.Random.Range(0, 2000) < KickStart.Difficulty)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": Enemy AI " + tank.name + " reclaim attempt success");
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
                List<RawBlockMem> posBlocks = ReturnAllPositionsOfTypeSLOW(foundBlock.name);
                //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                for (int step2 = 0; step2 < posBlocks.Count; step2++)
                {
                    RawBlockMem template = posBlocks.ElementAt(step2);
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
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
                    TankBlock held = Helper.HeldBlock;
                    if (held == null)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to attach NULL BLOCK");
                        templateCache = null;
                        return;
                    }
                    if (templateCache == null)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to attach block but no template was cached!!");
                        return;
                    }

                    Helper.DropBlock();
                    success = AIBlockAttachRequest(tank, templateCache, held, false);

                    if (success)
                    {
                        if (held.visible.InBeam)
                            held.visible.SetHolder(null);

                        //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Attaching " + canidate.name);
                        if (!KickStart.MuteNonPlayerRacket)
                        {
                            FieldInfo attachSFX = typeof(ManTechBuilder).GetField("m_BlockAttachSFXEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                            FMODEvent[] soundSteal = (FMODEvent[])attachSFX.GetValue(Singleton.Manager<ManTechBuilder>.inst);
                            ManSFX.inst.AttachInstanceToPosition(soundSteal[(int)held.BlockConnectionAudioType].PlayEvent(), held.centreOfMassWorld);
                        }
                    }
                    
                    if (tank.IsAnchored && Helper.AIAlign == AIAlignment.NonPlayer && held.GetComponent<ModuleItemProducer>())
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
                //DebugTAC_AI.Log("RUSHING ATTACH OP");
                TryAttachHeldBlock();
                CancelInvoke("TryAttachHeldBlock");
            }
            public void AttachOperation(Visible block, RawBlockMem mem, out Vector3 offsetVec)
            {
                if (templateCache != null)
                {
                    RushAttachOpIfNeeded();
                }
                Helper.HoldBlock(block, mem);
                templateCache = mem;
                offsetVec = (mem.p - tank.blockBounds.center).normalized;
                Invoke("TryAttachHeldBlock", AIGlobals.BlockAttachDelay);
                lastAttached = block.block.BlockType;
            }


            // JSON
            public void TechToJSONLog()
            {   // Saving a Tech from the BlockMemory
                List<RawBlockMem> mem = TechToMemory();
                if (mem.Count == 0)
                    return;
                SavedTech.Clear();
                SavedTech.AddRange(mem);
                SB.Append(JsonUtility.ToJson(mem.FirstOrDefault()));
                for (int step = 1; step < mem.Count; step++)
                {
                    SB.Append("|");
                    SB.Append(JsonUtility.ToJson(mem.ElementAt(step)));
                }
                string JSONTechRAWout = SB.ToString();
                SB.Clear();
                foreach (char ch in JSONTechRAWout)
                {
                    if (ch == '"')
                    {
                        SB.Append('\\');
                        SB.Append(ch);
                    }
                    else
                        SB.Append(ch);
                }
                DebugTAC_AI.Log(KickStart.ModID + ": " + SB.ToString());
                SB.Clear();
            }
            public void JSONToTech(string toLoad)
            {   // Loading a Tech from the BlockMemory
                if (toLoad.NullOrEmpty())
                    throw new NullReferenceException("JSONToTech input field is null");
                
                foreach (char ch in toLoad)
                {
                    if (ch != '\\')
                    {
                        SB.Append(ch);
                    }
                }
                List<RawBlockMem> mem = new List<RawBlockMem>();
                string RAWout = SB.ToString();
                SB.Clear();
                foreach (char ch in RAWout)
                {
                    if (ch == '|')//new block
                    {
                        try
                        {
                            mem.Add(JsonUtility.FromJson<RawBlockMem>(SB.ToString()));
                        }
                        catch { DebugTAC_AI.Log("JSONToTech failed on entry " + mem.Count); }
                        SB.Clear();
                    }
                    else
                        SB.Append(ch);
                }
                try
                {
                    mem.Add(JsonUtility.FromJson<RawBlockMem>(SB.ToString()));
                }
                catch { DebugTAC_AI.Log("JSONToTech failed on last entry"); }
                SB.Clear();
                //DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory: saved " + mem.Count);
                MemoryToTech(mem);
            }

            // CONSTRUCTION
            /// <summary>
            /// Bookmarks the build's data into the Tech for incremental building, but will not 
            ///   guarentee completion.
            /// </summary>
            /// <param name="helper"></param>
            /// <param name="JSON"></param>
            public void SetupForNewTechConstruction(TankAIHelper helper, List<RawBlockMem> inst)
            {
                MemoryToTech(inst);
                DebugTAC_AI.LogAISetup("TechMemor of " + tank.name + " has " + SavedTech.Count + " entries");
                CheckGameTamperedWith(tank, this);
                helper.PendingDamageCheck = true;
            }

            // Load operation
            public List<RawBlockMem> ReturnContents()
            {
                return new List<RawBlockMem>(IterateReturnContents());
            }
            /// <summary>
            /// creates no junk but DO NOT ALTER!!!
            /// </summary>
            /// <returns></returns>
            internal List<RawBlockMem> IterateReturnContents()
            {
                if (SavedTech.Count() == 0)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": INVALID TECH DATA STORED FOR TANK " + tank.name);
                    DebugTAC_AI.Log(KickStart.ModID + ": " + StackTraceUtility.ExtractStackTrace());
                }
                return SavedTech;
            }

            /// <summary>
            /// Can be a source of Hash Collisions.  
            /// No Hash Collisions have occurred yet however.
            /// </summary>
            /// <param name="blockGOName"></param>
            /// <returns></returns>
            public List<RawBlockMem> ReturnAllPositionsOfTypeSLOW(string blockGOName)
            {
                int hash = blockGOName.GetHashCode();
                return SavedTech.FindAll(delegate (RawBlockMem cand) { return cand.t.GetHashCode() == hash; });
            }

            private static HashSet<int> hashCache = new HashSet<int>();

            /// <summary>
            /// SUPER SLOW
            /// Can be a source of Hash Collisions.  
            /// No Hash Collisions have occurred yet however.
            /// </summary>
            /// <param name="blockGOName"></param>
            /// <returns></returns>
            public List<RawBlockMem> ReturnAllPositionsOfMultipleTypes(List<BlockTypes> types)
            {
                hashCache.Clear();
                foreach (var item in types)
                {
                    try
                    {
                        hashCache.Add(ManSpawn.inst.GetBlockPrefab(item).name.GetHashCode());
                    }
                    catch { }
                }
                return SavedTech.FindAll(delegate (RawBlockMem cand) { return hashCache.Contains(cand.t.GetHashCode()); });
            }

            private static readonly List<RawBlockMem> emptyMem = new List<RawBlockMem>();
            public List<RawBlockMem> ReturnAllPositionsOfType(BlockTypes blocktype)
            {
                if (fastBlockLookup.TryGetValue(blocktype, out List<RawBlockMem> mems))
                {
                    //Debug.Info(KickStart.ModID + ":  DesignMemory - ReturnAllPositionsOfType " + tank.name + " looked for " + blocktype + " and found " + mems.Count);
                    return mems;
                }
                //Debug.Info(KickStart.ModID + ":  DesignMemory - ReturnAllPositionsOfType " + tank.name + " looked for " + blocktype + " and found nothing");
                return emptyMem;
            }

            // Infinite money for enemy autominer bases - resources are limited
            public void MakeMinersMineUnlimited()
            {   // make autominers mine deep based on biome
                try
                {
                    CancelInvoke("TryMakeMinersMineUnlimited");
                    Invoke("TryMakeMinersMineUnlimited", 2f);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": MakeMinersMineUnlimited - game is being stubborn");
                }
            }

            public class AutoMineInfMessage : MessageBase
            {
                public AutoMineInfMessage() { }
                public AutoMineInfMessage(uint netTechID)
                {
                    this.netTechID = netTechID;
                }
                public bool Execute()
                {
                    if (this.GetTech(netTechID, out Tank tech))
                    {
                        DoMakeMinersMineUnlimited(tech);
                        return true;
                    }
                    return false;
                }
                public uint netTechID;
            }
            private static NetworkHook<AutoMineInfMessage> netHookMiner = new NetworkHook<AutoMineInfMessage>(
                "TAC_AI.AutoMineInfMessage", OnReceiveAutomineUpdate, NetMessageType.ToClientsOnly);
            internal static bool OnReceiveAutomineUpdate(AutoMineInfMessage update, bool isServer)
            {
                return update.Execute();
            }

            internal void TryMakeMinersMineUnlimited()
            {
                if (netHookMiner.CanBroadcast())
                {
                    netHookMiner.TryBroadcast(new AutoMineInfMessage(tank.GetTechNetID()));
                }
                else
                    DoMakeMinersMineUnlimited(tank);
            }
            internal static void DoMakeMinersMineUnlimited(Tank tankInst)
            {   // make autominers mine deep based on biome
                try
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": " + tank.name + " is trying to mine unlimited");
                    foreach (ModuleItemProducer module in tankInst.blockman.IterateBlockComponents<ModuleItemProducer>())
                    {
                        module.gameObject.GetOrAddComponent<ReverseCache>().SaveComponents();
                    }
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": DoMakeMinersMineUnlimited - game is being stubborn");
                }
            }


            // EXPERIMENT
            public static void RebuildTechForwards(Tank tank)
            {
                List<RawBlockMem> mem = RawTechTemplate.TechToMemoryExternal(tank);
                List<TankBlock> blocks = DitchAllBlocks(tank, true);
                TurboconstructExt(tank, mem, blocks, false);
            }
            public static List<TankBlock> DitchAllBlocks(Tank tank, bool addToThisFrameLater)
            {
                List<TankBlock> blockCache = tank.blockman.IterateBlocks().ToList();
                tank.blockman.Disintegrate(true, addToThisFrameLater);
                return blockCache;
            }


            internal bool HandlePurchase()
            {
                if (purchaseOp == null)
                    return true;
                return purchaseOp.Invoke(lastAttached);
            }

            private bool EnemyPurchase(BlockTypes BlockType)
            {
                if (!KickStart.EnemiesHaveCreativeInventory)
                {
                    float priceIsRight = Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(BlockType, true);
                    if (Enemy.RLoadedBases.TryMakePurchase(BlockType, tank.Team))
                    {
                        DebugTAC_AI.Info(KickStart.ModID + ": AI " + tank.name + ": bought " + BlockType
                            + " from the shop for " + priceIsRight);

                        if (!KickStart.MuteNonPlayerRacket)
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                        return true;
                    }
                    else
                    {
                        DebugTAC_AI.LogDevOnlyAssert(KickStart.ModID + ": AI " + tank.name + ": Could not afford " + BlockType
                            + " from the shop for " + priceIsRight + " because ");
                       DebugTAC_AI.LogDevOnlyAssert(" Funds are " + ManBaseTeams.GetTeamMoney(tank.Team) + ", but cost is " +
                                priceIsRight);
                        return false;
                    }
                }
                else
                    return true;
            }


            /// REPAIR OPERATIONS

            //Controlling code that re-attaches loose blocks for AI techs.
            internal BlockTypes lastAttached;
            internal bool QueueBlockAttach(RawBlockMem template, TankBlock canidate, bool NeedPurchase = false)
            {
                if (ManNetwork.IsNetworked)
                    return AttemptBlockAttachImmediate(template, canidate, NeedPurchase);

                if (!tank.visible.isActive || !canidate)
                {
                    lastAttached = BlockTypes.GSOAIController_111;
                    return false;
                }

                ranOutOfParts = false;
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
                if (tank.CanAttachBlock(canidate, template.p, new OrthoRotation(template.r)))
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                    if (!NeedPurchase || HandlePurchase())
                    {
                        AttachOperation(canidate.visible, template, out _);
                        lastAttached = canidate.BlockType;
                        return true;
                    }
                    else
                    {
                        ranOutOfParts = true;
                        DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Team " + tank.Team + " is out of parts!");
                        lastAttached = BlockTypes.GSOAIController_111;
                        return true;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Could not attach block " +
                   //     "- no available AP connections between block and Tech target coordinates!");
                    return false;
                }
            }
            internal bool QueueBlockAttach(RawBlockMem template, TankBlock canidate, out Vector3 offsetVec, bool purchase = false)
            {
                offsetVec = Vector3.zero;
                if (ManNetwork.IsNetworked)
                    return AttemptBlockAttachImmediate(template, canidate, purchase);

                if (!tank.visible.isActive || !canidate)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": QueueBlockAttach Canidate is NotNull : " + canidate);
                    lastAttached = BlockTypes.GSOAIController_111;
                    return false;
                }

                ranOutOfParts = false;
                //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
                if (tank.CanAttachBlock(canidate, template.p, new OrthoRotation(template.r)))
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                    if (!purchase || HandlePurchase())
                    {
                        AttachOperation(canidate.visible, template, out offsetVec);
                        lastAttached = canidate.BlockType;
                        return true;
                    }
                    else
                    {
                        ranOutOfParts = true;
                        DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Team " + tank.Team + " is out of parts!");
                        lastAttached = BlockTypes.GSOAIController_111;
                        return true;
                    }
                }
                else
                {
                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": Could not attach block " +
                    //    "- no available AP connections between block and Tech target coordinates!");
                    lastAttached = BlockTypes.GSOAIController_111;
                    return false;
                }
            }
            internal bool AttemptBlockAttachImmediate(RawBlockMem template, TankBlock canidate, bool purchase = false)
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
                bool success = AIBlockAttachRequest(tank, template, canidate, false);

                if (success)
                {
                    if (canidate.visible.InBeam)
                        canidate.visible.SetHolder(null);
                    lastAttached = canidate.BlockType;

                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                    if (purchase && !HandlePurchase())
                    {
                        ranOutOfParts = true;
                        return false;
                    }

                    //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Attaching " + canidate.name);
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
            private static List<TankBlock> fBlocks = new List<TankBlock>();
            private static List<TankBlock> fBlocksOut = new List<TankBlock>();
            public List<TankBlock> FindBlocksNearbyTank()
            {
                fBlocksOut.Clear();
                foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, AIGlobals.MaxBlockGrabRange, AIGlobals.blockBitMask))
                {
                    if ((bool)foundBlock.block && foundBlock.GetComponent<WorldSpaceObject>().IsEnabled)
                    {
                        if (!(bool)foundBlock.block.tank && foundBlock.ColliderSwapper.CollisionEnabled
                            && foundBlock.IsInteractible && (!foundBlock.InBeam || (foundBlock.InBeam
                            && foundBlock.holderStack.myHolder.block.LastTechTeam == tank.Team))
                            && Singleton.Manager<ManPointer>.inst.DraggingItem != foundBlock &&
                            foundBlock != Helper.HeldBlock)
                        {
                            if (foundBlock.block.PreExplodePulse)
                                continue; //explode? no thanks
                                          //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - block " + foundBlock.name + " has " + cBlocks.FindAll(delegate (TankBlock cand) { return cand.blockPoolID == foundBlock.block.blockPoolID; }).Count() + " matches");
                            fBlocks.Add(foundBlock.block);
                        }
                    }
                }
                fBlocksOut.AddRange(fBlocks.OrderBy((blok) => (blok.centreOfMassWorld - tank.boundsCentreWorld).sqrMagnitude));
                fBlocks.Clear();
                return fBlocksOut;
            }
            internal bool TryAttachExistingBlockFromList(ref List<BlockTypes> typesMissing, ref List<TankBlock> foundBlocks, bool denySD = false)
            {
                int attachAttempts = foundBlocks.Count();
                //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - Found " + attachAttempts + " loose blocks to use");
                for (int step = 0; step < attachAttempts; step++)
                {
                    TankBlock foundBlock = foundBlocks[step];
                    BlockTypes BT = foundBlock.BlockType;
                    if (!typesMissing.Contains(BT))
                        continue;
                    bool attemptW;
                    // if we are smrt, run heavier operation
                    List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(BT);
                    int count = posBlocks.Count;
                    //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        RawBlockMem template = posBlocks.ElementAt(step2);
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
                //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - Found " + attachAttempts + " loose blocks to use");
                for (int step = 0; step < attachAttempts; step++)
                {
                    TankBlock foundBlock = foundBlocks[step];
                    BlockTypes BT = foundBlock.BlockType;
                    if (!typesMissing.Contains(BT))
                        continue;
                    bool attemptW;
                    // if we are smrt, run heavier operation

                    List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(BT);
                    int count = posBlocks.Count;
                    //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        RawBlockMem template = posBlocks.ElementAt(step2);
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
                //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - Types Missing " + attachAttempts + " | playerInv " + playerInventory);
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
                        Helper.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    TankBlock foundBlock = RawTechLoader.SpawnBlockS(bType, tank.boundsCentreWorldNoCheck + 
                        (Vector3.up * (Helper.lastTechExtents + 10)), Quaternion.identity, out bool worked);
                    if (!worked)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - Could not spawn block");
                        continue;
                    }
                    bool attemptW;

                    List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(bType);
                    int count = posBlocks.Count();
                    if (count == 0)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - THERE'S NO MORE BLOCK POSITIONS TO ATTACH!");
                        ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                        typesMissing.RemoveAt(step);
                        attachAttempts--;
                        step--;
                        continue;
                    }
                    //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        RawBlockMem template = posBlocks.ElementAt(step2);
                        attemptW = QueueBlockAttach(template, foundBlock, out Vector3 offsetVec, purchase);
                        if (attemptW)
                        {
                            //foundBlock.InitNew();
                            foundBlock.trans.position = tank.boundsCentreWorldNoCheck + (tank.trans.TransformDirection(offsetVec).SetY(0.65f).normalized * (Helper.lastTechExtents + 10));
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
                    //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

                    ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                    // if everything fails, resort to timbuktu
                    //foundBlock.damage.SelfDestruct(0.1f);
                    //Vector3 yeet = Vector3.forward * 450000;
                    //foundBlock.transform.position = yeet;
                }
                //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - ATTACH ATTEMPT FAILED!");
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
                        if (!Helper.PendingDamageCheck)
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
                        Helper.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    bool attemptW;

                    List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(bType);
                    int count = posBlocks.Count();
                    if (count == 0)
                    {
                        //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - THERE'S NO MORE BLOCK POSITIONS TO ATTACH!");
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
                            DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - Could not spawn block " + bType);
                            continue;
                        }
                        for (int step2 = 0; step2 < count; step2++)
                        {
                            RawBlockMem template = posBlocks.ElementAt(step2);
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
                        Helper.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    TankBlock foundBlock = RawTechLoader.SpawnBlockS(bType, tank.boundsCentreWorldNoCheck + (Vector3.up * (Helper.lastTechExtents + 10)), Quaternion.identity, out bool worked);
                    if (!worked)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - Could not spawn block");
                        continue;
                    }
                    bool attemptW;

                    List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(bType);
                    int count = posBlocks.Count();
                    if (count == 0)
                    {
                        ManLooseBlocks.inst.RequestDespawnBlock(foundBlock, DespawnReason.Host);
                        typesMissing.RemoveAt(step);
                        attachAttempts--;
                        step--;
                        continue;
                    }
                    //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                    for (int step2 = 0; step2 < count; step2++)
                    {
                        RawBlockMem template = posBlocks.ElementAt(step2);
                        attemptW = QueueBlockAttach(template, foundBlock, out Vector3 offsetVec, purchase);
                        if (attemptW)
                        {
                            foundBlock.trans.position = tank.boundsCentreWorldNoCheck + (tank.trans.TransformDirection(offsetVec).SetY(0.65f).normalized * (Helper.lastTechExtents + 10));
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
                    //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

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
                        if (!Helper.PendingDamageCheck)
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
                        Helper.PendingDamageCheck = false;
                        return false;
                    }
                    ranOutOfParts = false;

                    List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(bType);
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
                            DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - Could not spawn block " + bType);
                            continue;
                        }
                        bool attemptW;

                        //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                        for (int step2 = 0; step2 < count; step2++)
                        {
                            RawBlockMem template = posBlocks.ElementAt(step2);
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
                        //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

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
                    Helper.PendingDamageCheck = false;
                    return false;
                }
                ranOutOfParts = false;

                List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(bType);
                int count = posBlocks.Count();
                if (count == 0)
                    return false;


                TankBlock prefabBlock = RawTechLoader.GetPrefabFiltered(bType, blockSpawnPos);
                if (!prefabBlock)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": IterateAndTryAttachBlockSkinMP - Could not fetch block");
                    return false;
                }
                bool attemptW;


                //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                for (int step2 = 0; step2 < count; step2++)
                {
                    RawBlockMem template = posBlocks.ElementAt(step2);
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
                //DebugTAC_AI.Log(KickStart.ModID + ": IterateAndTryAttachBlock - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");
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
                    Helper.PendingDamageCheck = false;
                    return false;
                }
                ranOutOfParts = false;

                TankBlock prefabBlock = RawTechLoader.GetPrefabFiltered(bType, blockSpawnPos);
                if (!prefabBlock)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": IterateAndTryAttachBlockSkinMP - Could not fetch block");
                    return false;
                }
                bool attemptW;

                List<RawBlockMem> posBlocks = ReturnAllPositionsOfType(bType);
                int count = posBlocks.Count();
                if (count == 0)
                    return false;

                //DebugTAC_AI.Log(KickStart.ModID + ": TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                for (int step2 = 0; step2 < count; step2++)
                {
                    RawBlockMem template = posBlocks.ElementAt(step2);
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
                //DebugTAC_AI.Log(KickStart.ModID + ": IterateAndTryAttachBlock - ATTACH ATTEMPT FAILED!  BLOCK MAY BE COMPROMISED!");

                return false;
            }
        }

        public static bool IsValidRotation(TankBlock TB, OrthoRotation.r r)
        {

            return true; // can't fetch proper context for some reason
            /*
            Singleton.Manager<ManTechBuilder>.inst.ClearBlockRotationOverride(TB);
            OrthoRotation.r[] rots = Singleton.Manager<ManTechBuilder>.inst.GetBlockRotationOrder(TB);
            Singleton.Manager<ManTechBuilder>.inst.ClearBlockRotationOverride(TB);
            if (rots != null && rots.Length > 0 && !rots.Contains(r))
            {   // block cannot be saved - illegal rotation.
                return false;
            }
            return true;*/
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


        public static bool AIBlockAttachRequest(Tank tank, RawBlockMem template, TankBlock canidate, bool mandatory)
        {
            bool success;
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            if (!ManNetwork.inst.IsMultiplayer())
            {
                success = Singleton.Manager<ManLooseBlocks>.inst.RequestAttachBlock(tank, canidate, template.p, new OrthoRotation(template.r));
                if (!success && mandatory)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ": RequestAttachBlock - Failed to attach " + canidate.name + " at " + template.p);
                    throw new Exception(KickStart.ModID + ": a block that SHOULD be attached was rejected!");
                }
            }
            else
                success = BlockAttachNetworkOverride(tank, template, canidate);
            return success;
        }
        private static bool BlockAttachNetworkOverride(Tank tank, RawBlockMem template, TankBlock canidate)
        {
            if (!ManNetwork.IsHost)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ASSERT: BlockAttachNetworkOverride - Called in non-host sitsuation!");
                return false;// CANNOT DO THIS WHEN NOT HOST OR ERROR 
            }
            bool attached = false;
            
            if (canidate == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": BlockAttachNetworkOverride - BLOCK IS NULL!");
            }
            else
            {
                NetBlock netBlock = canidate.netBlock;
                if (netBlock.IsNull())
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": BlockAttachNetworkOverride - NetBlock could not be found on AI block attach attempt!");
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
            if (TechMemor.IsNull())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp called with no valid DesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
                return false;
            }
            int savedBCount = TechMemor.IterateReturnContents().Count;
            int cBCount = tank.blockman.IterateBlocks().Count();
            //DebugTAC_AI.Log(KickStart.ModID + ": saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Player AI " + tank.name + ":  New blocks were added without " +
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
        private static bool RepairLerp(Tank tank, DesignMemory TechMemor, TankAIHelper helper, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to repair");
            if (ManNetwork.IsNetworked)
                return RepairLerpInstant(tank, TechMemor, helper, ref fBlocks, ref typesMissing);

            if (TechMemor.TryAttachExistingBlockFromList(ref typesMissing, ref fBlocks))
                return true;
            if (helper.UseInventory)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - Attempting to repair from inventory");
                RawTechLoader.ResetSkinIDSet();
                if (TechMemor.TrySpawnAndAttachBlockFromList(ref typesMissing, true))
                    return true;
            }
            return false;
        }
        private static bool RepairLerpInstant(Tank tank, DesignMemory TechMemor, TankAIHelper helper, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Trying to repair");

            if (TechMemor.TryAttachExistingBlockFromListInst( ref typesMissing, ref fBlocks))
                return true;
            if (helper.UseInventory)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - Attempting to repair from inventory");
                RawTechLoader.ResetSkinIDSet();
                if (TechMemor.TrySpawnAndAttachBlockFromListInst(ref typesMissing, true))
                    return true;
            }
            return false;
        }
        internal static bool InstaRepair(Tank tank, DesignMemory TechMemor, int RepairAttempts = 0)
        {
            bool success = false;
            if (TechMemor.SystemsCheck() && PreRepairPrep(tank, TechMemor))
            {
                //List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                TechMemor.RushAttachOpIfNeeded();
                if (RepairAttempts == 0)
                    RepairAttempts = TechMemor.IterateReturnContents().Count();

                TankAIHelper helper = TechMemor.Helper;

                List<TankBlock> fBlocks = TechMemor.FindBlocksNearbyTank();
                List<BlockTypes> typesMissing = TechMemor.GetMissingBlockTypes();
                RawTechLoader.ResetSkinIDSet();
                BulkAdding = true;
                while (RepairAttempts > 0)
                {
                    bool worked = RepairLerpInstant(tank, TechMemor, helper, ref fBlocks, ref typesMissing);
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
        internal static bool RepairStepper(TankAIHelper helper, Tank tank, DesignMemory TechMemor, bool AdvancedAI = false, bool Combat = false)
        {
            if (helper.RepairStepperClock <= 0)
            {
                float prevVal = helper.RepairStepperClock;

                if (AIGlobals.TurboAICheat)
                {
                    helper.RepairStepperClock = 0;
                    helper.TechMemor.ReserveSuperGrabs = 5 * KickStart.AIClockPeriod;
                }
                else if (Combat)
                {
                    if (AdvancedAI)
                        helper.RepairStepperClock = sDelayCombat;
                    else
                        helper.RepairStepperClock = delayCombat;
                }
                else
                {
                    if (AdvancedAI)
                        helper.RepairStepperClock = sDelaySafe;
                    else
                        helper.RepairStepperClock = delaySafe;
                }
                if (helper.PendingDamageCheck) //&& helper.AttemptedRepairs == 0)
                {
                    if (helper.RepairStepperClock < 1)
                        helper.RepairStepperClock = 1;
                    int initialBlockCount = tank.blockman.blockCount;
                    float OverdueTime = Mathf.Abs(prevVal / helper.RepairStepperClock);
                    if (OverdueTime >= 2)
                    {
                        int blocksToAdd = Mathf.CeilToInt(OverdueTime);
                        helper.PendingDamageCheck = !InstaRepair(tank, TechMemor, blocksToAdd);
                        helper.RepairStepperClock -= (OverdueTime - blocksToAdd) * helper.RepairStepperClock;
                    }
                    else if (TechMemor.SystemsCheck() && PreRepairPrep(tank, TechMemor))
                    {   // Cheaper to check twice than to use GetMissingBlockTypes when not needed.
                        helper.RepairStepperClock -= OverdueTime * helper.RepairStepperClock;
                        TechMemor.RushAttachOpIfNeeded();
                        List<TankBlock> fBlocks = TechMemor.FindBlocksNearbyTank();
                        List<BlockTypes> typesMissing = TechMemor.GetMissingBlockTypes();

                        if (!RepairLerp(tank, TechMemor, helper, ref fBlocks, ref typesMissing))
                        {
                            //DebugTAC_AI.Log(KickStart.ModID + ": AlliedRepairStepper - unknown error on RepairLerp for " + tank.name);
                        } 
                        TechMemor.UpdateMissingBlockTypes(typesMissing);
                        helper.PendingDamageCheck = TechMemor.SystemsCheck();
                        //helper.AttemptedRepairs = 1;
                    }
                    else
                        helper.PendingDamageCheck = false;

                    if (!helper.PendingDamageCheck && initialBlockCount != tank.blockman.blockCount)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": AlliedRepairStepper - Done for " + tank.name);
                        helper.FinishedRepairEvent.Send(helper);
                    }
                    //DebugTAC_AI.Log(KickStart.ModID + ": RepairStepper(" + tank.name + ") - Pending check: " + helper.PendingSystemsCheck);
                }
            }
            else
                helper.RepairStepperClock -= KickStart.AIClockPeriod;
            return helper.PendingDamageCheck;
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
            var helper = tank.GetComponent<TankAIHelper>();
            bool blocksNearby = false;
            foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, (helper.MaxCombatRange / 4), AIGlobals.blockBitMask))
            {
                if (foundBlock.block.IsNotNull() && foundBlock.GetComponent<WorldSpaceObject>().IsEnabled)
                {
                    if (!foundBlock.block.tank && foundBlock.holderStack == null && Singleton.Manager<ManPointer>.inst.DraggingItem != foundBlock)
                    {
                        if (foundBlock.block.PreExplodePulse)
                            continue; //explode? no thanks
                                      //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - block " + foundBlock.name + " has " + cBlocks.FindAll(delegate (TankBlock cand) { return cand.blockPoolID == foundBlock.block.blockPoolID; }).Count() + " matches");
                        if (TechMemor.IterateReturnContents().FindAll(delegate (RawBlockMem cand) { return cand.t == foundBlock.block.name; }).Count() > 0)
                        {
                            blocksNearby = true;
                            break;
                        }
                    }
                }
            }
            if (helper.AIAlign == AIAlignment.NonPlayer)
            {
                var mind = tank.GetComponent<Enemy.EnemyMind>();
                if ((mind.AllowRepairsOnFly || (helper.lastEnemyGet.IsNull())) && (blocksNearby || KickStart.EnemiesHaveCreativeInventory || mind.AllowInvBlocks))
                {
                    return true;
                }
            }
            else if (helper.AIAlign == AIAlignment.Player && (blocksNearby || helper.UseInventory))
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
                    return true;
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
                        DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Tried to repair but block " + blockType.ToString() + " was not found!");
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
                    DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Tried to repair but block " + blockType.ToString() + " was not found!");
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
            DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory: Turboconstructing " + tank.name + ", count " + TechMemor.IterateReturnContents().Count());
            int cBCount = tank.blockman.blockCount;
            int RepairAttempts = TechMemor.IterateReturnContents().Count() - cBCount + 3;
            if (TechMemor.IsNull())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair called with no valid EnemyDesignMemory!!!");
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
            DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory: Turboconstructing " + tank.name + ", count " + TechMemor.IterateReturnContents().Count());
            int cBCount = tank.blockman.blockCount;
            int RepairAttempts = TechMemor.IterateReturnContents().Count() - cBCount + 3;
            if (TechMemor.IsNull())
            {
                DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair called with no valid EnemyDesignMemory!!!");
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
            int savedBCount = TechMemor.IterateReturnContents().Count;
            int cBCount = tank.blockman.blockCount;
            //DebugTAC_AI.Log(KickStart.ModID + ": saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount != cBCount)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - Attempting to repair from infinity - " + typesMissing.Count());
                if (!TechMemor.TrySpawnAndAttachBlockFromListInst(ref typesMissing, false, false))
                    DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - attach attempt failed");
            }
            return;
        }
        internal static void TurboRepairSupplies(Tank tank, DesignMemory TechMemor, ref List<BlockTypes> typesMissing, ref List<TankBlock> provided)
        {
            int savedBCount = TechMemor.IterateReturnContents().Count;
            int cBCount = tank.blockman.blockCount;
            //DebugTAC_AI.Log(KickStart.ModID + ": saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount != cBCount)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - Attempting to repair from infinity - " + typesMissing.Count());
                if (!TechMemor.TryAttachExistingBlockFromListInst(ref typesMissing, ref provided, false))
                    DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - attach attempt failed");
            }
            return;
        }


        // External major operations
        /// <summary>
        /// Builds a Tech instantly, no requirements
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="TechMemor"></param>
        public static void TurboconstructExt(Tank tank, List<RawBlockMem> Mem, bool fullyCharge = true)
        {
            DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory: Turboconstructing " + tank.name);
            int cBCount = tank.blockman.blockCount;
            int RepairAttempts = Mem.Count() - cBCount + 3;
            try
            {
                List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                List<BlockTypes> typesMissing = IterateMissingBlockTypesExt(Mem, cBlocks);
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
        public static void TurboconstructExt(Tank tank, List<RawBlockMem> Mem, List<TankBlock> provided, bool fullyCharge = true)
        {
            DebugTAC_AI.Log(KickStart.ModID + ":  DesignMemory: Turboconstructing " + tank.name);
            int cBCount = tank.blockman.blockCount;
            int RepairAttempts = Mem.Count() - cBCount + 3;
            try
            {
                List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
                List<BlockTypes> typesMissing = IterateMissingBlockTypesExt(Mem, cBlocks);
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
        public static void TurboRepairExt(Tank tank, List<RawBlockMem> Mem, ref List<BlockTypes> typesMissing)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            int savedBCount = Mem.Count;
            int cBCount = cBlocks.Count;
            if (savedBCount != cBCount)
            {

                //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - Attempting to repair from infinity - " + typesToRepair.Count());
                if (!TrySpawnAndAttachBlockFromListExt(tank, Mem, ref typesMissing))
                    DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - attach attempt failed");
            }
            return;
        }
        public static void TurboRepairExt(Tank tank, List<RawBlockMem> Mem, ref List<BlockTypes> typesMissing, ref List<TankBlock> provided)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            int savedBCount = Mem.Count;
            int cBCount = cBlocks.Count;
            if (savedBCount != cBCount)
            {

                //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - Attempting to repair from infinity - " + typesToRepair.Count());
                if (!TryAttachExistingBlockFromListExt(tank, Mem, ref typesMissing, ref provided))
                    DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - attach attempt failed");
            }
            return;
        }

        private static List<BlockTypes> typesToRepair = new List<BlockTypes>();
        private static List<BlockTypes> typesMissing = new List<BlockTypes>();
        public static List<BlockTypes> IterateMissingBlockTypesExt(List<RawBlockMem> Mem, List<TankBlock> cBlocks)
        {
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

            typesMissing.Clear();
            int toFilter2 = typesToRepair.Count();
            for (int step = 0; step < toFilter2; step++)
            {
                int present = cBlocks.FindAll(delegate (TankBlock cand) { return typesToRepair[step] == cand.BlockType; }).Count;
                string Name = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(typesToRepair[step]).name;

                int mem = Mem.FindAll(delegate (RawBlockMem cand) { return Name == cand.t; }).Count;
                if (mem > present)// are some blocks not accounted for?
                    typesMissing.Add(typesToRepair[step]);
            }
            typesToRepair.Clear();
            return typesMissing;
        }
        private static bool TrySpawnAndAttachBlockFromListExt(Tank tank, List<RawBlockMem> Mem, ref List<BlockTypes> typesMissing)
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
                List<RawBlockMem> posBlocks = Mem.FindAll(delegate (RawBlockMem cand) { return cand.t.GetHashCode() == hash;});
                if (posBlocks.Count == 0)
                {
                    typesMissing.RemoveAt(step);
                    step--;
                    attachAttempts--;
                    continue;
                }
                //DebugTAC_AI.Log(KickStart.ModID + ": TurboRepair - potental spots " + posBlocks.Count + " for block " + foundBlock.name);
                for (int step2 = 0; step2 < posBlocks.Count; step2++)
                {
                    RawBlockMem template = posBlocks.ElementAt(step2);
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
        public static bool TryAttachExistingBlockFromListExt(Tank tank, List<RawBlockMem> mem, ref List<BlockTypes> typesMissing, ref List<TankBlock> foundBlocks, bool denySD = false)
        {
            int attachAttempts = foundBlocks.Count();
            //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - Found " + attachAttempts + " loose blocks to use");
            for (int step = 0; step < attachAttempts; step++)
            {
                TankBlock foundBlock = foundBlocks[step];
                BlockTypes BT = foundBlock.BlockType;
                if (!typesMissing.Contains(BT))
                    continue;
                bool attemptW;
                // if we are smrt, run heavier operation
                int hash = foundBlock.name.GetHashCode();
                List<RawBlockMem> posBlocks = mem.FindAll(delegate (RawBlockMem cand) { return cand.t.GetHashCode() == hash;});
                int count = posBlocks.Count;
                //DebugTAC_AI.Log(KickStart.ModID + ": RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                for (int step2 = 0; step2 < count; step2++)
                {
                    RawBlockMem template = posBlocks.ElementAt(step2);
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
        private static bool AttemptBlockAttachExt(Tank tank, RawBlockMem template, TankBlock canidate)
        {
            //DebugTAC_AI.Log(KickStart.ModID + ": (AttemptBlockAttachExt) AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            return AIBlockAttachRequest(tank, template, canidate, false);
        }


        // Util
        private static void CheckGameTamperedWith(Tank tank, DesignMemory mem)
        {
            try
            {
                if (mem.IterateReturnContents().FirstOrDefault() == null && tank.blockman.GetBlockAtPosition(new IntVector3(0, 0, 0)) == null)
                    return;
                string blockCurrent = tank.blockman.GetBlockAtPosition(new IntVector3(0, 0, 0)).name;
                string blockSaved = mem.IterateReturnContents().FirstOrDefault().t;
                if (blockCurrent != blockSaved)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Expected " + blockSaved + " at 0,0,0 local blockman, found " + (blockCurrent.NullOrEmpty() ? "NO BLOCK" : blockCurrent.ToString()) + " instead.");
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": AI " + tank.name + ":  Block Hierachy compromised!");
            }
        }
    }
}
