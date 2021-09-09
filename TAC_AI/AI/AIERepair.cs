using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.Serialization;
using UnityEngine.Networking;
using UnityEngine;

namespace TAC_AI.AI
{
    [Serializable]
    public class BlockMemory
    {   // Save the blocks!
        public string t = BlockTypes.GSOAIController_111.ToString(); //blocktype
        public Vector3 p = Vector3.zero;
        public OrthoRotation.r r = OrthoRotation.r.u000;
    }
    public class ListPositions
    {   // Save the blocks!
        public List<Vector3> p = new List<Vector3>();
        public List<OrthoRotation.r> r = new List<OrthoRotation.r>();
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
        public class DesignMemory : MonoBehaviour
        {   // Save the design on load!
            private Tank Tank;
            public AIECore.TankAIHelper thisInst;
            public bool rejectSaveAttempts = false;
            public bool ranOutOfParts = false;
            public int ReserveSuperGrabs = 0;

            public List<BlockMemory> SavedTech = new List<BlockMemory>();

            // Handling this
            public void Initiate(bool DoFirstSave = true)
            {
                Tank = gameObject.GetComponent<Tank>();
                thisInst = gameObject.GetComponent<AIECore.TankAIHelper>();
                thisInst.TechMemor = this;
                rejectSaveAttempts = false;
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

                if (KickStart.DesignsToLog)
                {
                    Debug.Log("TACtical_AI:  DesignMemory - DESIGNS TO LOG IS ENABLED!!!");
                    TechToJSON();
                    return;
                }
                List<TankBlock> ToSave = Tank.blockman.IterateBlocks().ToList();
                SavedTech.Clear();

                foreach (TankBlock bloc in ToSave)
                {
                    BlockMemory mem = new BlockMemory();
                    mem.t = bloc.name;
                    mem.p = bloc.cachedLocalPosition;
                    mem.r = bloc.cachedLocalRotation.rot;
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
            }
            public void SaveTech(List<TankBlock> overwrite)
            {
                rejectSaveAttempts = true;
                SavedTech.Clear();
                foreach (TankBlock bloc in overwrite)
                {
                    BlockMemory mem = new BlockMemory();
                    mem.t = bloc.name;
                    mem.p = bloc.cachedLocalPosition;
                    mem.r = bloc.cachedLocalRotation.rot;
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
                List<BlockMemory> clean = new List<BlockMemory>();
                foreach (BlockMemory mem in overwrite)
                {
                    BlockTypes type = StringToBlockType(mem.t);
                    if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    {
                        Debug.Log("TACtical_AI:  DesignMemory - " + Tank.name + ": could not save " + mem.t + " in blueprint due to illegal block.");
                        continue;
                    }
                    // get rid of floating point errors
                    mem.p.x = Mathf.RoundToInt(mem.p.x);
                    mem.p.y = Mathf.RoundToInt(mem.p.y);
                    mem.p.z = Mathf.RoundToInt(mem.p.z);
                    clean.Add(mem);
                }
                SavedTech = clean;
                Debug.Log("TACtical_AI:  DesignMemory - Overwrote " + Tank.name);
                //build AROUND the cab pls
                //if (SavedTech.Count() > 1)
                //    SavedTech = new List<BlockMemory>(SavedTech).OrderBy((blok) => (blok.CachePos - Vector3.zero).sqrMagnitude).ToList();
            }
            public TankBlock TryFindProperRootBlock(List<TankBlock> ToSearch)
            {
                return FindProperRootBlockExternal(ToSearch);
            }
            public List<BlockMemory> TechToMemory()
            {
                return TechToMemoryExternal(Tank);
            }

            // Advanced
            public bool ChanceGrabBackBlock(TankBlock blockLoss)
            {
                if (UnityEngine.Random.Range(0, 500) < KickStart.Difficulty)
                {
                    //Debug.Log("TACtical_AI: Enemy AI " + Tank.name + " reclaim attempt success");
                    ReserveSuperGrabs++;
                    if (KickStart.EnemyBlockDropChance == 0)
                        blockLoss.damage.SelfDestruct(0.75f);
                    return true;
                }
                return false;
            }
            public bool TryAttachExistingBlock(TankBlock foundBlock)
            {
                bool attemptW = false;
                // if we are smrt, run heavier operation
                List<BlockMemory> posBlocks = ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.t == foundBlock.name; });
                //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                for (int step2 = 0; step2 < posBlocks.Count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = AttemptBlockAttach(Tank, template, foundBlock, this);
                    if (attemptW)
                    {
                        return true;
                    }
                }
                return false;
            }

            // JSON
            public void TechToJSON()
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
                mem.Add(JsonUtility.FromJson<BlockMemory>(blockCase.ToString()));
                //Debug.Log("TACtical_AI:  DesignMemory: saved " + mem.Count);
                MemoryToTech(mem);
            }

            // CONSTRUCTION
            public void SetupForNewTechConstruction(AIECore.TankAIHelper thisInst, string JSON)
            {
                JSONToTech(JSON);
                CheckGameTamperedWith(Tank, this);
                thisInst.PendingSystemsCheck = true;
            }

            // Load operation
            public List<BlockMemory> ReturnContents()
            {
                if (SavedTech.Count() == 0)
                {
                    Debug.Log("TACtical_AI: INVALID TECH DATA STORED FOR TANK " + Tank.name);
                    Debug.Log("TACtical_AI: " + StackTraceUtility.ExtractStackTrace());
                }
                return new List<BlockMemory>(SavedTech);
            }


            //External
            public static TankBlock FindProperRootBlockExternal(List<TankBlock> ToSearch)
            {
                bool IsAnchoredAnchorPresent = false;
                float close = 128;
                TankBlock newRoot = ToSearch.First();
                foreach (TankBlock bloc in ToSearch)
                {
                    Vector3 blockPos = bloc.CalcFirstFilledCellLocalPos();
                    if (bloc.GetComponent<ModuleAnchor>() && bloc.GetComponent<ModuleAnchor>().IsAnchored)
                    {
                        IsAnchoredAnchorPresent = true;
                        break;
                    }
                    if (blockPos.sqrMagnitude < close * close && (bloc.GetComponent<ModuleTechController>() || bloc.GetComponent<ModuleAIBot>()))
                    {
                        close = blockPos.magnitude;
                        newRoot = bloc;
                    }
                }
                if (IsAnchoredAnchorPresent)
                {
                    close = 128;
                    foreach (TankBlock bloc in ToSearch)
                    {
                        Vector3 blockPos = bloc.CalcFirstFilledCellLocalPos();
                        if (blockPos.sqrMagnitude < close * close && bloc.GetComponent<ModuleAnchor>() && bloc.GetComponent<ModuleAnchor>().IsAnchored)
                        {
                            close = blockPos.magnitude;
                            newRoot = bloc;
                        }
                    }
                }
                return newRoot;
            }
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
                    coreRot = rootBlock.cachedLocalRotation;
                    tank.blockman.SetRootBlock(rootBlock);
                }
                else
                    coreRot = new OrthoRotation(OrthoRotation.r.u000);

                foreach (TankBlock bloc in ToSave)
                {
                    if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(bloc.BlockType))
                        continue;
                    BlockMemory mem = new BlockMemory();
                    mem.t = bloc.name;
                    mem.p = Quaternion.Inverse(coreRot) * (bloc.trans.localPosition - coreOffset);
                    // get rid of floating point errors
                    mem.p.x = Mathf.RoundToInt(mem.p.x);
                    mem.p.y = Mathf.RoundToInt(mem.p.y);
                    mem.p.z = Mathf.RoundToInt(mem.p.z);
                    //Get the rotation
                    Quaternion outr =  bloc.cachedLocalRotation * Quaternion.Inverse(coreRot);
                    mem.r = new OrthoRotation(outr).rot;
                    if (!Singleton.Manager<ManTechBuilder>.inst.GetBlockRotationOrder(bloc).Contains(mem.r))
                    {   // block cannot be saved - illegal rotation.
                        Debug.Log("TACtical_AI:  DesignMemory - " + tank.name + ": could not save " + bloc.name + " in blueprint due to illegal rotation.");
                        continue;
                    }
                    output.Add(mem);
                }
                Debug.Log("TACtical_AI:  DesignMemory - Saved " + tank.name + " to memory format");

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
        }
        private static Dictionary<int, BlockTypes> errorNames = new Dictionary<int, BlockTypes>();

        // Get those blocks right!
        public static void ConstructErrorBlocksList()
        {
            errorNames.Clear();
            List<BlockTypes> types = Singleton.Manager<ManSpawn>.inst.GetLoadedTankBlockNames().ToList();
            foreach (BlockTypes type in types)
            {
                TankBlock prefab = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type);
                string name = prefab.name;
                if (prefab.GetComponent<Damageable>() && type.ToString() != name && !Singleton.Manager<ManMods>.inst.IsModdedBlock(type))
                {
                    int hash = name.GetHashCode();
                    if (!errorNames.Keys.Contains(hash))
                    {
                        errorNames.Add(hash, type);
                        //Debug.Log("TACtical_AI: ConstructErrorBlocksList - Added " + name + " | " + type.ToString());
                    }
                }
            }
            Debug.Log("TACtical_AI: ConstructErrorBlocksList - There are " + errorNames.Count + " blocks with names not equal to their type");
        }
        public static bool TryGetMismatchNames(string name, ref BlockTypes type)
        {
            if (errorNames.TryGetValue(name.GetHashCode(), out BlockTypes val))
            {
                type = val;
                return true;
            }
            return false;
        }

        public static BlockTypes StringToBlockType(string mem)
        {
            if (!Enum.TryParse(mem, out BlockTypes type))
            {
                if (!TryGetMismatchNames(mem, ref type))
                    type = (BlockTypes)Singleton.Manager<ManMods>.inst.GetBlockID(mem);
            }
            return type;
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

            return StringToBlockType(mem.t);
        }

        //COMPLICATED MESS that re-attaches loose blocks for AI techs, does not apply to allied Techs FOR NOW.
        public static bool AttemptBlockAttach(Tank tank, BlockMemory template, TankBlock canidate, DesignMemory TechMemor, bool useLimitedSupplies = false)
        {
            if (!tank.visible.isActive)
            {
                // If we try to attach to a tech that doesn't exist, it corrupts and breaks ALL future techs that spawn.
                //   The game breaks, yadda yadda, ManUpdate looses it's marbles, causing bullets and wheels to freak out.
                //   In other words, *Unrecoverable crash*
                //
                //      So we end the madness here
                return false;
            }

            TechMemor.ranOutOfParts = false;
            bool success;
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            if (!ManNetwork.inst.IsMultiplayer())
                success = Singleton.Manager<ManLooseBlocks>.inst.RequestAttachBlock(tank, canidate, template.p, new OrthoRotation(template.r));
            else
                success = BlockAttachNetworkOverride(tank, template, canidate);
            if (success)
            {
                if (canidate.visible.InBeam)
                    canidate.visible.SetHolder(null);

                //Debug.Log("TACtical_AI: AI " + tank.name + ":  " + !TechMemor.unlimitedParts + " | " + useLimitedSupplies);
                if (useLimitedSupplies && !KickStart.EnemiesHaveCreativeInventory)
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
        public static bool AttemptBlockAttachS(Tank tank, BlockMemory template, TankBlock canidate)
        {
            bool success;
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            if (!ManNetwork.inst.IsMultiplayer())
                success = Singleton.Manager<ManLooseBlocks>.inst.RequestAttachBlock(tank, canidate, template.p, new OrthoRotation(template.r));
            else
                success = BlockAttachNetworkOverride(tank, template, canidate);
            return success;
        }
        public static bool BlockAttachNetworkOverride(Tank tank, BlockMemory template, TankBlock canidate)
        {
            if (!ManNetwork.IsHost)
                return false;// CANNOT DO THIS WHEN NOT HOST OR ERROR
            bool attached = false;
            NetTech NetT = NetworkServer.FindLocalObject(tank.netTech.netId).GetComponent<NetTech>();
            if (NetT != null && canidate != null)
            {
                Tank tech = NetT.tech;
                NetBlock netBlock = canidate.netBlock;
                if (netBlock.IsNull())
                {
                    Debug.Log("TACtical_AI: BlockAttachNetworkOverride - NetBlock could not be found on AI block attach attempt!");
                }
                else
                {
                    attached = tech.blockman.AddBlockToTech(canidate, template.p, new OrthoRotation(template.r));
                    if (attached)
                    {
                        Singleton.Manager<ManNetwork>.inst.ServerNetBlockAttachedToTech.Send(tech, netBlock, canidate);
                        Singleton.Manager<ManBlockLimiter>.inst.TagAsInteresting(tech);
                        tech.netTech.SaveTechData();
                        BlockAttachedMessage message = new BlockAttachedMessage
                        {
                            m_TechNetId = tech.netTech.netId,
                            m_BlockPosition = template.p,
                            m_BlockOrthoRotation = (int)template.r,
                            m_BlockPoolID = canidate.blockPoolID
                        };
                        Singleton.Manager<ManNetwork>.inst.SendToAllExceptHost(TTMsgType.BlockAttach, message);
                        if (netBlock.block != null)
                        {
                            netBlock.Disconnect();
                        }
                        netBlock.RemoveClientAuthority();
                        NetworkServer.UnSpawn(netBlock.gameObject);
                        //m_PendingAttach.Remove(netBlock);
                        netBlock.transform.Recycle(worldPosStays: false);
                    }
                }
            }
            return attached;
        }
        

        // Other respective repair operations
        public static bool RepairLerp(Tank tank, DesignMemory TechMemor, AIECore.TankAIHelper thisInst, ref List<BlockTypes> typesMissing, bool overrideChecker = false)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: RepairLerp called with no valid DesignMemory!!!");
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

                List<TankBlock> fBlocks = FindBlocksNearbyTank(tank, thisInst.RangeToChase / 4);
                fBlocks = fBlocks.OrderBy((blok) => (blok.centreOfMassWorld - tank.boundsCentreWorld).sqrMagnitude).ToList();

                if (TryAttachExistingBlockFromList(tank, TechMemor, fBlocks))
                    return true;
                if (thisInst.useInventory)
                {
                    //Debug.Log("TACtical AI: RepairLerp - Attempting to repair from inventory");
                    if (TrySpawnAndAttachBlockFromList(tank, TechMemor, ref typesMissing, true))
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
            if (RepairAttempts == 0)
                RepairAttempts = TechMemor.ReturnContents().Count();

            List<BlockTypes> typesMissing = GetMissingBlockTypes(TechMemor, tank.blockman.IterateBlocks().ToList());
            while (RepairAttempts > 0)
            {
                if (!SystemsCheck(tank, TechMemor))
                {
                    success = true;
                    break;
                }
                bool worked = RepairLerp(tank, TechMemor, tank.GetComponent<AIECore.TankAIHelper>(), ref typesMissing, true);
                if (!worked)
                    break;
                RepairAttempts--;
            }
            return success;
        }
        public static bool RepairStepper(AIECore.TankAIHelper thisInst, Tank tank, DesignMemory TechMemor, int Delay = 25, bool Super = false)
        {
            if (thisInst.repairStepperClock == 1)
            {
                //thisInst.AttemptedRepairs = 0;
                thisInst.repairStepperClock = 0;
            }
            else if (thisInst.repairStepperClock == 0)
            {
                if (thisInst.PendingSystemsCheck) //&& thisInst.AttemptedRepairs == 0)
                {
                    List<BlockTypes> typesMissing = GetMissingBlockTypes(TechMemor, tank.blockman.IterateBlocks().ToList());

                    RepairLerp(tank, TechMemor, thisInst, ref typesMissing);
                    thisInst.PendingSystemsCheck = SystemsCheck(tank, TechMemor);
                    //thisInst.AttemptedRepairs = 1;
                }
                if (!Super)
                    thisInst.repairStepperClock = Delay;
                else
                    thisInst.repairStepperClock = Delay / 4;
            }
            else
                thisInst.repairStepperClock--;
            return thisInst.PendingSystemsCheck;
        }


        // Repair Utilities
        public static List<BlockTypes> GetMissingBlockTypes(DesignMemory TechMemor, List<TankBlock> cBlocks)
        {
            List<BlockTypes> typesToRepair = new List<BlockTypes>();
            List<BlockMemory> mem = TechMemor.ReturnContents();
            int toFilter = mem.Count();
            List<string> filteredNames = new List<string>();
            for (int step = 0; step < toFilter; step++)
            {
                filteredNames.Add(mem.ElementAt(step).t);
            }
            filteredNames = filteredNames.Distinct().ToList();
            for (int step = 0; step < filteredNames.Count; step++)
            {
                typesToRepair.Add(StringToBlockType(filteredNames.ElementAt(step)));
            }

            List<BlockTypes> typesMissing = new List<BlockTypes>();
            int toFilter2 = typesToRepair.Count();
            for (int step = 0; step < toFilter2; step++)
            {
                int present = cBlocks.FindAll(delegate (TankBlock cand) { return typesToRepair[step] == cand.BlockType; }).Count;
                string Name = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(typesToRepair[step]).name;

                int mem2 = mem.FindAll(delegate (BlockMemory cand) { return Name == cand.t; }).Count;
                if (mem2 > present)// are some blocks not accounted for?
                    typesMissing.Add(typesToRepair[step]);
            }
            return typesMissing;
        }
        public static List<TankBlock> FindBlocksNearbyTank(Tank tank, float radius, bool includeSD = false)
        {
            List <TankBlock> fBlocks = new List<TankBlock>();
            foreach (Visible foundBlock in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(tank.boundsCentreWorldNoCheck, radius, new Bitfield<ObjectTypes>()))//new ObjectTypes[1]{ObjectTypes.Block})
            {
                if ((bool)foundBlock.block && foundBlock.GetComponent<WorldSpaceObject>().IsEnabled)
                {
                    if (!(bool)foundBlock.block.tank && (!foundBlock.InBeam || (foundBlock.InBeam && foundBlock.holderStack.myHolder.block.LastTechTeam == tank.Team)) && Singleton.Manager<ManPointer>.inst.DraggingItem != foundBlock)
                    {
                        if (!includeSD)
                        {
                            if (foundBlock.block.PreExplodePulse)
                                continue; //explode? no thanks
                        }
                        else
                        {
                            if (foundBlock.block.GetComponent<Damageable>().Health <= 0)
                                continue;
                        }
                        //Debug.Log("TACtical AI: RepairLerp - block " + foundBlock.name + " has " + cBlocks.FindAll(delegate (TankBlock cand) { return cand.blockPoolID == foundBlock.block.blockPoolID; }).Count() + " matches");
                        fBlocks.Add(foundBlock.block);
                    }
                }
            }
            return fBlocks;
        }
        public static bool TryAttachExistingBlockFromList(Tank tank, DesignMemory TechMemor, List<TankBlock> foundBlocks, bool denySD = false)
        {
            int attachAttempts = foundBlocks.Count();
            //Debug.Log("TACtical AI: RepairLerp - Found " + attachAttempts + " loose blocks to use");
            for (int step = 0; step < attachAttempts; step++)
            {
                TankBlock foundBlock = foundBlocks[step];
                bool attemptW = false;
                // if we are smrt, run heavier operation
                List<BlockMemory> posBlocks = TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.t == foundBlock.name; });
                int count = posBlocks.Count;
                //Debug.Log("TACtical AI: RepairLerp - potental spots " + posBlocks.Count + " for block " + foundBlock);
                for (int step2 = 0; step2 < count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = AttemptBlockAttach(tank, template, foundBlock, TechMemor);
                    if (attemptW)
                    {
                        if (denySD)
                        {
                            foundBlock.damage.AbortSelfDestruct();
                        }
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool TrySpawnAndAttachBlockFromList(Tank tank, DesignMemory TechMemor, ref List<BlockTypes> typesMissing, bool playerInventory = false, bool useLimitedSupplies = false)
        {
            int attachAttempts = typesMissing.Count();
            for (int step = 0; step < attachAttempts; step++)
            {
                BlockTypes bType = typesMissing.ElementAt(step);
                if (playerInventory)
                    if (!IsBlockStoredInInventory(tank, bType))
                        continue;
                if (useLimitedSupplies && !KickStart.EnemiesHaveCreativeInventory)
                {
                    if (!Enemy.RBases.PurchasePossible(bType, tank.Team))
                    {
                        TechMemor.ranOutOfParts = true;
                        TechMemor.thisInst.PendingSystemsCheck = false;
                        continue;
                    }
                }
                TechMemor.ranOutOfParts = false;

                TankBlock foundBlock = null;
                foundBlock = Templates.RawTechLoader.SpawnBlockS(bType, tank.boundsCentreWorldNoCheck + (Vector3.up * 128), Quaternion.identity, out bool worked);
                if (!worked)
                {
                    Debug.Log("TACtical AI: TrySpawnAndAttachBlockFromList - Could not spawn block");
                    continue;
                }
                bool attemptW = false;

                List<BlockMemory> posBlocks = TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.t == foundBlock.name; });
                int count = posBlocks.Count();
                if (count == 0)
                {
                    if (playerInventory)
                        IsBlockStoredInInventory(tank, bType, true);
                    foundBlock.transform.Recycle();
                    typesMissing.RemoveAt(step);
                    attachAttempts--;
                    step--;
                    continue;
                }
                //Debug.Log("TACtical AI: TrySpawnAndAttachBlockFromList - potential spots " + posBlocks.Count + " for block " + foundBlock.name);
                for (int step2 = 0; step2 < count; step2++)
                {
                    BlockMemory template = posBlocks.ElementAt(step2);
                    attemptW = AttemptBlockAttach(tank, template, foundBlock, TechMemor, useLimitedSupplies);
                    if (attemptW)
                    {
                        //foundBlock.InitNew();
                        return true;
                    }
                }
                if (playerInventory)
                    IsBlockStoredInInventory(tank, bType, true);
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
                        if (TechMemor.ReturnContents().FindAll(delegate (BlockMemory cand) { return cand.t == foundBlock.block.name; }).Count() > 0)
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
            if (!(bool)TechMemor)
                return true;
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
        internal static void SetupForNewTechConstruction(DesignMemory TechMemor, List<TankBlock> tankTemplate)
        {
            TechMemor.SaveTech(tankTemplate.FindAll(delegate (TankBlock cand) { return cand != null; }));
        }
        internal static void Turboconstruct(Tank tank, DesignMemory TechMemor, bool fullyCharge = true)
        {
            Debug.Log("TACtical_AI:  DesignMemory: Turboconstructing " + tank.name + ", count " + TechMemor.ReturnContents().Count());
            int cBCount = tank.blockman.IterateBlocks().ToList().Count();
            int RepairAttempts = TechMemor.ReturnContents().Count() - cBCount + 3;
            if (TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: TurboRepair called with no valid EnemyDesignMemory!!!");
                TechMemor = tank.gameObject.AddComponent<DesignMemory>();
                TechMemor.Initiate();
                return;
            }
            try
            {
                List<BlockTypes> typesMissing = GetMissingBlockTypes(TechMemor, tank.blockman.IterateBlocks().ToList());
                while (RepairAttempts > 0)
                {
                    TurboRepair(tank, TechMemor, ref typesMissing);
                    RepairAttempts--;
                }
            }
            catch { return; }
            if (fullyCharge)
                tank.EnergyRegulator.SetAllStoresAmount(1);
        }
        internal static void TurboRepair(Tank tank, DesignMemory TechMemor, ref List<BlockTypes> typesMissing)
        {
            int savedBCount = TechMemor.ReturnContents().Count;
            int cBCount = tank.blockman.IterateBlocks().ToList().Count;
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount != cBCount)
            {
                //Debug.Log("TACtical AI: TurboRepair - Attempting to repair from infinity - " + typesMissing.Count());
                if (!TrySpawnAndAttachBlockFromList(tank, TechMemor, ref typesMissing, false, false))
                    Debug.Log("TACtical AI: TurboRepair - attach attempt failed");
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
            Debug.Log("TACtical_AI:  DesignMemory: Turboconstructing " + tank.name);
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
        public static void TurboRepairExt(Tank tank, List<BlockMemory> Mem, ref List<BlockTypes> typesMissing)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            int savedBCount = Mem.Count;
            int cBCount = cBlocks.Count;
            if (savedBCount != cBCount)
            {

                //Debug.Log("TACtical AI: TurboRepair - Attempting to repair from infinity - " + typesToRepair.Count());
                if (!TrySpawnAndAttachBlockFromListExt(tank, Mem, ref typesMissing))
                    Debug.Log("TACtical AI: TurboRepair - attach attempt failed");
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
                typesToRepair.Add(StringToBlockType(filteredNames.ElementAt(step)));
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
                bool attemptW = false;

                List<BlockMemory> posBlocks = Mem.FindAll(delegate (BlockMemory cand) { return cand.t == foundBlock.name; });
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
        private static bool AttemptBlockAttachExt(Tank tank, BlockMemory template, TankBlock canidate)
        {
            bool success;
            //Debug.Log("TACtical_AI: AI " + tank.name + ":  Trying to attach " + canidate.name + " at " + template.CachePos);
            if (!ManNetwork.inst.IsMultiplayer())
                success = Singleton.Manager<ManLooseBlocks>.inst.RequestAttachBlock(tank, canidate, template.p, new OrthoRotation(template.r));
            else
                success = BlockAttachNetworkOverride(tank, template, canidate);

            return success;
        }


        // Util
        private static void CheckGameTamperedWith(Tank tank, DesignMemory mem)
        {
            string blockCurrent = tank.blockman.GetBlockAtPosition(new IntVector3(0, 0, 0)).name;
            string blockSaved = mem.ReturnContents().First().t;
            if (blockCurrent != blockSaved)
            {
                Debug.Log("TACtical_AI: AI " + tank.name + ":  Expected " + blockSaved + " at 0,0,0 local blockman, found " + blockCurrent + " instead.");
            }
        }
    }
}
