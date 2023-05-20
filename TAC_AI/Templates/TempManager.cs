using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TerraTechETCUtil;

namespace TAC_AI.Templates
{
    public static class TempManager
    {
        private static int lastExtLocalCount = 0;
        private static int lastExtModCount = 0;


        /// <summary>
        /// Hosts active techs
        /// </summary>
        public static Dictionary<SpawnBaseTypes, RawTechTemplate> techBases;

        public static List<RawTechTemplate> ExternalEnemyTechsLocal;

        public static List<RawTechTemplate> ExternalEnemyTechsMods;

        public static List<RawTechTemplate> ExternalEnemyTechsAll;


        public static void ValidateAllStringTechs()
        {
            ValidateAndAddAllInternalTechs();
            ValidateAndAddAllExternalTechs();
        }
        public static void ValidateAndAddAllInternalTechs(bool reloadPublic = true)
        {
            List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> preCompile = new List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>>();

            preCompile.AddRange(CommunityStorage.ReturnAllCommunityStored(reloadPublic));
            preCompile.AddRange(TempStorage.techBasesPrefab);


            Dictionary<SpawnBaseTypes, RawTechTemplate> techBasesProcessing = preCompile.ToDictionary(x => x.Key, x => x.Value);

            techBases = new Dictionary<SpawnBaseTypes, RawTechTemplate>();
            foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in techBasesProcessing)
            {
                if (ValidateBlocksInTech(ref pair.Value.savedTech, pair.Value))
                {
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
                ExternalEnemyTechsLocal = new List<RawTechTemplate>();
                List<RawTechTemplate> ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechs();
                foreach (RawTechTemplate raw in ExternalTechsRaw)
                {
                    if (ValidateBlocksInTech(ref raw.savedTech, raw))
                    {
                        ExternalEnemyTechsLocal.Add(raw);
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: Could not load local RawTech " + raw.techName + " as the load operation encountered an error");
                    }
                }
                DebugTAC_AI.Log("TACtical_AI: Pushed " + ExternalEnemyTechsLocal.Count + " RawTechs from the Local pool");
                lastExtLocalCount = tCount;


                ExternalEnemyTechsMods = new List<RawTechTemplate>();
                ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechsExternalMods();
                foreach (RawTechTemplate raw in ExternalTechsRaw)
                {
                    if (ValidateBlocksInTech(ref raw.savedTech, raw))
                    {
                        ExternalEnemyTechsMods.Add(raw);
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: Could not load ModBundle RawTech " + raw.techName + " as the load operation encountered an error");
                    }
                }
                DebugTAC_AI.Log("TACtical_AI: Pushed " + ExternalEnemyTechsMods.Count + " RawTechs from the Mod pool");
                lastExtModCount = tMCount;


                ExternalEnemyTechsAll = new List<RawTechTemplate>();
                ExternalEnemyTechsAll.AddRange(ExternalEnemyTechsLocal);
                ExternalEnemyTechsAll.AddRange(ExternalEnemyTechsMods);
                DebugTAC_AI.Log("TACtical_AI: Pushed a total of " + ExternalEnemyTechsAll.Count + " to the external tech pool.");
                DebugRawTechSpawner.Organize(ref ExternalEnemyTechsLocal);
                DebugRawTechSpawner.Organize(ref ExternalEnemyTechsMods);
            }
        }

        /// <summary>
        /// Checks all of the blocks in a BaseTemplate Tech to make sure it's safe to spawn as well as calculate other requirements for it.
        /// </summary>
        /// <param name="toLoad"></param>
        /// <param name="templateToCheck"></param>
        /// <param name="basePrice"></param>
        /// <param name="greatestFaction"></param>
        /// <returns></returns>
        public static bool ValidateBlocksInTech(ref string toLoad, RawTechTemplate templateToCheck)
        {
            try
            {
                StringBuilder RAW = new StringBuilder();
                foreach (char ch in toLoad)
                {
                    if (ch != RawTechExporter.up.ToCharArray()[0])
                    {
                        RAW.Append(ch);
                    }
                }
                List<RawBlockMem> mem = new List<RawBlockMem>();
                StringBuilder blockCase = new StringBuilder();
                string RAWout = RAW.ToString();
                FactionLevel greatestFaction = FactionLevel.GSO;
                try
                {
                    foreach (char ch in RAWout)
                    {
                        if (ch == '|')//new block
                        {
                            mem.Add(JsonUtility.FromJson<RawBlockMem>(blockCase.ToString()));
                            blockCase.Clear();
                        }
                        else
                            blockCase.Append(ch);
                    }
                    mem.Add(JsonUtility.FromJson<RawBlockMem>(blockCase.ToString()));
                }
                catch
                {
                    DebugTAC_AI.Assert(true, "TACtical_AI: ValidateBlocksInTech - Loading error - File was edited or corrupted!");
                    greatestFaction = FactionLevel.GSO;
                    return false;
                }
                bool valid = true;
                if (mem.Count == 0)
                {
                    greatestFaction = FactionLevel.GSO;
                    DebugTAC_AI.Log("TACtical_AI: ValidateBlocksInTech - FAILED as no blocks were present!");
                    return false;
                }
                int basePrice = 0;
                foreach (RawBlockMem bloc in mem)
                {
                    BlockTypes type = BlockIndexer.StringToBlockType(bloc.t);
                    if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    {
                        valid = false;
                        continue;
                    }

                    FactionSubTypes FST = Singleton.Manager<ManSpawn>.inst.GetCorporation(type);
                    FactionLevel FL = RawTechUtil.GetFactionLevel(FST);
                    if (FL >= FactionLevel.ALL)
                    {
                        if (ManMods.inst.IsModdedCorp(FST))
                        {
                            ModdedCorpDefinition MCD = ManMods.inst.GetCorpDefinition(FST);
                            if (Enum.TryParse(MCD.m_RewardCorp, out FactionSubTypes FST2))
                            {
                                FST = FST2;
                            }
                            else
                                throw new Exception("There's a block given that has an invalid corp \nBlockType: " + type);
                        }
                        else
                            throw new Exception("There's a block given that has an invalid corp \nCorp: " + FL + " \nBlockType: " + type);
                    }
                    if (greatestFaction < FL)
                        greatestFaction = FL;
                    basePrice += Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(type);
                    bloc.t = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type).name;
                }
                templateToCheck.baseCost = basePrice;
                templateToCheck.factionLim = greatestFaction;
                templateToCheck.blockCount = mem.Count;

                // Rebuild in workable format
                toLoad = RawTechTemplate.MemoryToJSONExternal(mem);

                return valid;
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: ValidateBlocksInTech - Tech was corrupted via unexpected mod changes!");
                return false;
            }
        }

        /// <summary>
        /// returns true if ALL blocks in tech are valid
        /// </summary>
        /// <param name="toScreen"></param>
        /// <returns></returns>
        public static bool ValidateBlocksInTechAndPurgeIfNeeded(List<RawBlockMem> toScreen)
        {
            try
            {
                if (toScreen.Count == 0)
                {
                    DebugTAC_AI.Log("TACtical_AI: ValidateBlocksInTechAndPurgeIfNeeded - FAILED as no blocks were present!");
                    return false;
                }
                bool valid = true;
                for (int step = toScreen.Count - 1; step > -1; step++)
                {
                    RawBlockMem bloc = toScreen[step];
                    BlockTypes type = BlockIndexer.StringToBlockType(bloc.t);
                    if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type))
                    {
                        valid = false;
                        toScreen.RemoveAt(step);
                        continue;
                    }
                    bloc.t = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type).name;
                }
                return valid;
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: ValidateBlocksInTech - Tech was corrupted via unexpected mod changes!");
                return false; 
            }
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
            List<RawBlockMem> mem = new List<RawBlockMem>();
            StringBuilder blockCase = new StringBuilder();
            string RAWout = RAW.ToString();
            try
            {
                foreach (char ch in RAWout)
                {
                    if (ch == '|')//new block
                    {
                        mem.Add(JsonUtility.FromJson<RawBlockMem>(blockCase.ToString()));
                        blockCase.Clear();
                    }
                    else
                        blockCase.Append(ch);
                }
                mem.Add(JsonUtility.FromJson<RawBlockMem>(blockCase.ToString()));
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: ValidateBlocksInTechStrict - Loading error - File was edited or corrupted!");
                return false;
            }
            bool valid = true;
            int cabHash = ManSpawn.inst.GetBlockPrefab(BlockTypes.GSOCockpit_111).name.GetHashCode();
            foreach (RawBlockMem bloc in mem)
            {
                BlockTypes type = BlockIndexer.StringToBlockType(bloc.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type) || (type == BlockTypes.GSOCockpit_111 && bloc.t.GetHashCode() != cabHash))
                {
                    valid = false;
                    continue;
                }
                bloc.t = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type).name;
            }

            // Rebuild in workable format
            toLoad = RawTechTemplate.MemoryToJSONExternal(mem);

            return valid;
        }

    }
}
