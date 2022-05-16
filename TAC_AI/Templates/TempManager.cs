using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;

namespace TAC_AI.Templates
{
    public static class TempManager
    {
        private static int lastExtLocalCount = 0;
        private static int lastExtModCount = 0;

        public static void ValidateAllStringTechs()
        {
            ValidateAndAddAllInternalTechs();
            ValidateAndAddAllExternalTechs();
        }
        public static void ValidateAndAddAllInternalTechs(bool reloadPublic = true)
        {
            List<KeyValuePair<SpawnBaseTypes, BaseTemplate>> preCompile = new List<KeyValuePair<SpawnBaseTypes, BaseTemplate>>();

            preCompile.AddRange(CommunityStorage.ReturnAllCommunityStored(reloadPublic));
            preCompile.AddRange(TempStorage.techBasesPrefab);


            Dictionary<SpawnBaseTypes, BaseTemplate> techBasesProcessing = preCompile.ToDictionary(x => x.Key, x => x.Value);

            techBases = new Dictionary<SpawnBaseTypes, BaseTemplate>();
            foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in techBasesProcessing)
            {
                if (ValidateBlocksInTech(ref pair.Value.savedTech, out int basePrice))
                {
                    pair.Value.baseCost = basePrice;
                    techBases.Add(pair.Key, pair.Value);
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not load " + pair.Value.techName + " as the load operation encountered an error");
                }
            }
            CommunityCluster.Organize(ref techBases);

            techBasesProcessing.Clear(); // GC, do your duty
            CommunityStorage.UnloadRemainingUnused();
        }
        public static void ValidateAndAddAllExternalTechs(bool force = false)
        {
            RawTechExporter.ValidateEnemyFolder();
            int tCount = RawTechExporter.GetTechCounts();
            int tMCount = RawTechExporter.GetRawTechsCountExternalMods();
            if (tCount != lastExtLocalCount || lastExtModCount != tMCount || force)
            {
                ExternalEnemyTechsLocal = new List<BaseTemplate>();
                List<BaseTemplate> ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechs();
                foreach (BaseTemplate raw in ExternalTechsRaw)
                {
                    if (ValidateBlocksInTech(ref raw.savedTech, out int basePrice))
                    {
                        raw.baseCost = basePrice;
                        ExternalEnemyTechsLocal.Add(raw);
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: Could not load local RawTech " + raw.techName + " as the load operation encountered an error");
                    }
                }
                DebugTAC_AI.Log("TACtical_AI: Pushed " + ExternalEnemyTechsLocal.Count + " RawTechs from the Local pool");
                lastExtLocalCount = tCount;


                ExternalEnemyTechsMods = new List<BaseTemplate>();
                ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechsExternalMods();
                foreach (BaseTemplate raw in ExternalTechsRaw)
                {
                    if (ValidateBlocksInTech(ref raw.savedTech, out int basePrice))
                    {
                        raw.baseCost = basePrice;
                        ExternalEnemyTechsMods.Add(raw);
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: Could not load ModBundle RawTech " + raw.techName + " as the load operation encountered an error");
                    }
                }
                DebugTAC_AI.Log("TACtical_AI: Pushed " + ExternalEnemyTechsMods.Count + " RawTechs from the Mod pool");
                lastExtModCount = tMCount;


                ExternalEnemyTechsAll = new List<BaseTemplate>();
                ExternalEnemyTechsAll.AddRange(ExternalEnemyTechsLocal);
                ExternalEnemyTechsAll.AddRange(ExternalEnemyTechsMods);
                DebugTAC_AI.Log("TACtical_AI: Pushed a total of " + ExternalEnemyTechsAll.Count + " to the external tech pool.");
            }
        }

        public static bool ValidateBlocksInTech(ref string toLoad, out int basePrice)
        {
            StringBuilder RAW = new StringBuilder();
            foreach (char ch in toLoad)
            {
                if (ch != RawTechExporter.up.ToCharArray()[0])
                {
                    RAW.Append(ch);
                }
            }
            List<BlockMemory> mem = new List<BlockMemory>();
            StringBuilder blockCase = new StringBuilder();
            string RAWout = RAW.ToString();
            try
            {
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
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: ValidateBlocksInTech - Loading error - File was edited or corrupted!");
                basePrice = 0;
                return false;
            }
            bool valid = true;
            basePrice = 0;
            if (mem.Count == 0)
            {
                DebugTAC_AI.Log("TACtical_AI: ValidateBlocksInTech - FAILED as no blocks were present!");
                return false;
            }
            foreach (BlockMemory bloc in mem)
            {
                BlockTypes type = AIERepair.StringToBlockType(bloc.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                {
                    valid = false;
                    continue;
                }
                basePrice += Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(type);
                bloc.t = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type).name;
            }

            // Rebuild in workable format
            toLoad = AIERepair.DesignMemory.MemoryToJSONExternal(mem);

            return valid;
        }

        /// <summary>
        /// returns true if ALL blocks in tech are valid
        /// </summary>
        /// <param name="toScreen"></param>
        /// <param name="basePrice"></param>
        /// <returns></returns>
        public static bool ValidateBlocksInTech(ref List<BlockMemory> toScreen)
        {
            if (toScreen.Count == 0)
            {
                DebugTAC_AI.Log("TACtical_AI: ValidateBlocksInTech - FAILED as no blocks were present!");
                return false;
            }
            List<BlockMemory> validated = new List<BlockMemory>();
            bool valid = true;
            foreach (BlockMemory bloc in toScreen)
            {
                BlockTypes type = AIERepair.StringToBlockType(bloc.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type))
                {
                    valid = false;
                    continue;
                }
                bloc.t = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type).name;
                validated.Add(bloc);
            }
            return valid;
        }
        public static bool ValidateBlocksInTechStrict(ref string toLoad)
        {
            if (toLoad.NullOrEmpty())
                return false;

            StringBuilder RAW = new StringBuilder();
            foreach (char ch in toLoad)
            {
                if (ch != RawTechExporter.up.ToCharArray()[0])
                {
                    RAW.Append(ch);
                }
            }
            List<BlockMemory> mem = new List<BlockMemory>();
            StringBuilder blockCase = new StringBuilder();
            string RAWout = RAW.ToString();
            try
            {
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
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: ValidateBlocksInTechStrict - Loading error - File was edited or corrupted!");
                return false;
            }
            bool valid = true;
            int cabHash = ManSpawn.inst.GetBlockPrefab(BlockTypes.GSOCockpit_111).name.GetHashCode();
            foreach (BlockMemory bloc in mem)
            {
                BlockTypes type = AIERepair.StringToBlockType(bloc.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type) || (type == BlockTypes.GSOCockpit_111 && bloc.t.GetHashCode() != cabHash))
                {
                    valid = false;
                    continue;
                }
                bloc.t = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type).name;
            }

            // Rebuild in workable format
            toLoad = AIERepair.DesignMemory.MemoryToJSONExternal(mem);

            return valid;
        }

        /// <summary>
        /// Hosts active techs
        /// </summary>
        public static Dictionary<SpawnBaseTypes, BaseTemplate> techBases;

        public static List<BaseTemplate> ExternalEnemyTechsLocal;

        public static List<BaseTemplate> ExternalEnemyTechsMods;

        public static List<BaseTemplate> ExternalEnemyTechsAll;




    }
}
