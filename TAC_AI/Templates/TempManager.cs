using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TerraTechETCUtil;
using System.IO;

namespace TAC_AI.Templates
{
    internal static class TempManager
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

        private static void ValidateAndAddTechs(List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> preCompile)
        {
            foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in preCompile)
            {
                if (ValidateBlocksInTech(ref pair.Value.savedTech, pair.Value))
                {
                    techBases.Add(pair.Key, pair.Value);
                }
                else
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not load " + pair.Value.techName + " as the load operation encountered an error");
                }
            }
            CommunityCluster.Organize(ref techBases);

            preCompile.Clear(); // GC, do your duty
            CommunityStorage.UnloadRemainingUnused();
        }

        /// <summary>
        /// Add in some Vanilla Techs (PENDING)
        /// </summary>
        public static void DelayedValidateAndAddBaseGameTechs()
        {
            /*
            if (ManPresetFilter.inst.IsSettingUp || ManPop.inst.IsSettingUp)
                InvokeHelper.Invoke(DelayedValidateAndAddBaseGameTechs, 0.25f);
            else
                ValidateAndAddTechs(SpecialAISpawner.ReturnAllBaseGameSpawns());
            */
        }

        public static void ValidateAllStringTechs()
        {
            ValidateAndAddAllInternalTechs();
            ValidateAndAddAllExternalTechs();
        }
        public static void ValidateAndAddAllInternalTechs(bool reloadPublic = true)
        {
            techBases = new Dictionary<SpawnBaseTypes, RawTechTemplate>();
            List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> preCompile = new List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>>();

            try
            {
                preCompile.AddRange(SpecialAISpawner.ReturnAllBaseGameSpawns());
                preCompile.AddRange(CommunityStorage.ReturnAllCommunityStored(reloadPublic));
                preCompile.AddRange(TempStorage.techBasesPrefab);
                ValidateAndAddTechs(preCompile);
                InvokeHelper.Invoke(DelayedValidateAndAddBaseGameTechs, 3);
            }
            catch (InsufficientMemoryException)
            {
                preCompile.Clear(); // GC, do your duty
                GC.Collect(); // Do it NOW IT'S AN EMERGENCY
                DebugTAC_AI.FatalError("Advanced AI ran COMPLETELY OUT of memory when loading enemy spawns." +
                    " Some enemies might be corrupted!");
            }
            catch (OutOfMemoryException)
            {
                preCompile.Clear(); // GC, do your duty
                GC.Collect(); // Do it NOW IT'S AN EMERGENCY
                DebugTAC_AI.FatalError("Advanced AI ran out of memory when loading enemy spawns." +
                    " Some enemies might be corrupted!");
            }
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
                        DebugTAC_AI.Log(KickStart.ModID + ": Could not load local RawTech " + raw.techName + " as the load operation encountered an error");
                    }
                }
                DebugTAC_AI.Log(KickStart.ModID + ": Pushed " + ExternalEnemyTechsLocal.Count + " RawTechs from the Local pool");
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
                        DebugTAC_AI.Log(KickStart.ModID + ": Could not load ModBundle RawTech " + raw.techName + " as the load operation encountered an error");
                    }
                }
                DebugTAC_AI.Log(KickStart.ModID + ": Pushed " + ExternalEnemyTechsMods.Count + " RawTechs from the Mod pool");
                lastExtModCount = tMCount;


                ExternalEnemyTechsAll = new List<RawTechTemplate>();
                ExternalEnemyTechsAll.AddRange(ExternalEnemyTechsLocal);
                ExternalEnemyTechsAll.AddRange(ExternalEnemyTechsMods);
                DebugTAC_AI.Log(KickStart.ModID + ": Pushed a total of " + ExternalEnemyTechsAll.Count + " to the external tech pool.");
                DebugRawTechSpawner.Organize(ref ExternalEnemyTechsLocal);
                DebugRawTechSpawner.Organize(ref ExternalEnemyTechsMods);
            }
        }

        private static StringBuilder SB = new StringBuilder();

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
                foreach (char ch in toLoad)
                {
                    if (ch != Path.DirectorySeparatorChar)
                    {
                        SB.Append(ch);
                    }
                }
                List<RawBlockMem> mem = new List<RawBlockMem>();
                string RAWout = SB.ToString();
                SB.Clear();
                FactionLevel greatestFaction = FactionLevel.GSO;
                try
                {
                    foreach (char ch in RAWout)
                    {
                        if (ch == '|')//new block
                        {
                            mem.Add(JsonUtility.FromJson<RawBlockMem>(SB.ToString()));
                            SB.Clear();
                        }
                        else
                            SB.Append(ch);
                    }
                    mem.Add(JsonUtility.FromJson<RawBlockMem>(SB.ToString()));
                    SB.Clear();
                }
                catch
                {
                    SB.Clear();
                    throw new Exception("File was edited or corrupted.");
                }
                bool valid = true;
                if (mem.Count == 0)
                {
                    greatestFaction = FactionLevel.GSO;
                    throw new Exception("No blocks were present on the Tech.  Nothing could be placed at all.");
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
                        try
                        {
                            if (ManMods.inst.IsModdedCorp(FST))
                            {
                                ModdedCorpDefinition MCD = ManMods.inst.GetCorpDefinition(FST);
                                if (Enum.TryParse(MCD.m_RewardCorp, out FactionSubTypes FST2))
                                {
                                    FST = FST2;
                                }
                                else
                                    throw new InvalidOperationException("Block with invalid m_RewardCorp - \nBlockType: " + type.ToString());
                            }
                            else
                                throw new InvalidOperationException("Block with invalid corp - \nCorp Level: " + FL.ToString() + " \nBlockType: " + type.ToString());
                        }
                        catch (InvalidOperationException)
                        {
                            throw new Exception("Block with invalid data - \nBlockType: <?NULL?>");
                        }
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
                SB.Clear();
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ValidateBlocksInTech - Tech " + templateToCheck.techName + " was corrupted via unexpected mod changes!");
                }
                catch (Exception)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ValidateBlocksInTech - Tech <?NULL?> was corrupted via unexpected mod changes!");
                }
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
                    DebugTAC_AI.Log(KickStart.ModID + ": ValidateBlocksInTechAndPurgeIfNeeded - FAILED as no blocks were present!");
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
                DebugTAC_AI.Log(KickStart.ModID + ": ValidateBlocksInTech - Tech was corrupted via unexpected mod changes!");
                return false; 
            }
        }
        public static bool ValidateBlocksInTechStrict(ref string toLoad)
        {
            if (toLoad.NullOrEmpty())
                return false;

            bool valid = true;
            try
            {
                foreach (char ch in toLoad)
                {
                    if (ch != Path.DirectorySeparatorChar)
                    {
                        SB.Append(ch);
                    }
                }
                List<RawBlockMem> mem = new List<RawBlockMem>();
                string RAWout = SB.ToString();
                SB.Clear();
                try
                {
                    foreach (char ch in RAWout)
                    {
                        if (ch == '|')//new block
                        {
                            mem.Add(JsonUtility.FromJson<RawBlockMem>(SB.ToString()));
                            SB.Clear();
                        }
                        else
                            SB.Append(ch);
                    }
                    mem.Add(JsonUtility.FromJson<RawBlockMem>(SB.ToString())); 
                    SB.Clear();
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

                }
                catch
                {
                    SB.Clear();
                    DebugTAC_AI.Assert(true, KickStart.ModID + ": ValidateBlocksInTechStrict - Loading error(2) - File was edited or corrupted!");
                    return false;
                }
            }
            catch { 
                SB.Clear();
                DebugTAC_AI.Assert(true, KickStart.ModID + ": ValidateBlocksInTechStrict - Loading error(1) - File was edited or corrupted!");
                return false;
            }
            return valid;
        }

    }
}
