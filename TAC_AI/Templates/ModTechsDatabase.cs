using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.AI;
using TerraTechETCUtil;
using System.IO;
using System.Collections;

namespace TAC_AI.Templates
{
    internal static class ModTechsDatabase
    {
        public const int MinimumLocalTechsToTriggerWarning = 32;
        private static int lastExtLocalCount = 0;
        private static int lastExtModCount = 0;


        /// <summary>
        /// Hosts active techs
        /// </summary>
        public static Dictionary<SpawnBaseTypes, RawTech> InternalPopTechs;

        public static List<RawTech> ExtPopTechsLocal = new List<RawTech>();

        public static List<RawTech> ExtPopTechsMods = new List<RawTech>();

        public static List<RawTech> ExtPopTechsAll = new List<RawTech>();

        private static void AddInternalTechs(List<KeyValuePair<SpawnBaseTypes, RawTech>> compile)
        {
            foreach (KeyValuePair<SpawnBaseTypes, RawTech> pair in compile)
                InternalPopTechs.Add(pair.Key, pair.Value);
        }
        internal static void ValidateAndAdd(Dictionary<SpawnBaseTypes, RawTechTemplate> preCompile, Dictionary<SpawnBaseTypes, RawTech> target)
        {
            foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in preCompile)
            {
                RawTech inst = pair.Value.ToActive();
                try
                {
                    if (inst.ValidateBlocksInTech(false, true))
                    {
                        target.Add(pair.Key, inst);
                    }
                    else
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": (Prefabs) Could not load " + pair.Value.techName + 
                            " as the load operation encountered an error");
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.LogLoad(KickStart.ModID + ": (Prefabs) Could not load " + pair.Value.techName + 
                        " as the load operation encountered a serious error - " + e);
                }
            }
            preCompile.Clear(); // GC, do your duty
        }
        internal static void ValidateAndAdd(List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> preCompile, List<KeyValuePair<SpawnBaseTypes, RawTech>> target)
        {
            foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in preCompile)
            {
                try
                {
                    RawTech inst = pair.Value.ToActive();
                    try
                    {
                        if (inst.ValidateBlocksInTech(true, false))
                        {
                            target.Add(new KeyValuePair<SpawnBaseTypes, RawTech>(pair.Key, inst));
                        }
                        else
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": (CommunityInternal) Could not load " + pair.Value.techName +
                                " as the load operation encountered an error");
                        }
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.LogLoad(KickStart.ModID + ": (CommunityInternal) Could not load " + pair.Value.techName +
                            " as the load operation encountered a serious error - " + e);
                    }
                }
                catch
                {
                    DebugTAC_AI.LogLoad(KickStart.ModID + ": (CommunityInternal) Could not load " + pair.Value.techName +
                        " as it was completely corrupted");
                }
            }
            preCompile.Clear(); // GC, do your duty
        }
        internal static void ValidateAndAdd(List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> preCompile, Dictionary<SpawnBaseTypes, RawTech> target)
        {
            foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in preCompile)
            {
                try
                {
                    RawTech inst = pair.Value.ToActive();
                    if (inst.ValidateBlocksInTech(true, false))
                    {
                        target.Add(pair.Key, inst);
                    }
                    else
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": (VanillaInternal) Could not load " + pair.Value.techName + " as the load operation encountered an error");
                    }
                }
                catch { }
            }
            preCompile.Clear(); // GC, do your duty
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
        public static bool DoWeNotHaveEnoughLocalTechs()
        {
            return ExtPopTechsAll.Count < MinimumLocalTechsToTriggerWarning;
        }
        public static void ValidateAndAddAllInternalTechs(bool reloadPublic = true)
        {
            InternalPopTechs = new Dictionary<SpawnBaseTypes, RawTech>();
            try
            {
#if !DEBUG
                if (!KickStart.TryForceOnlyPlayerSpawns)
                    ValidateAndAdd(SpecialAISpawner.ReturnAllBaseGameSpawns(), InternalPopTechs);
#endif
                AddInternalTechs(TempStorage.techBasesPrefab.ToList());
                AddInternalTechs(CommunityStorage.ReturnAllCommunityStored());
                if (reloadPublic)
                    AddInternalTechs(CommunityStorage.ReturnAllCommunityClustered());
                CommunityCluster.Organize(ref InternalPopTechs);

                CommunityStorage.UnloadRemainingUnused();
                InvokeHelper.Invoke(DelayedValidateAndAddBaseGameTechs, 3);
            }
            catch (InsufficientMemoryException)
            {
                GC.Collect(); // Do it NOW IT'S AN EMERGENCY
                DebugTAC_AI.FatalError("Advanced AI ran COMPLETELY OUT of memory when loading enemy spawns." +
                    " Some enemies might be corrupted!");
            }
            catch (OutOfMemoryException)
            {
                GC.Collect(); // Do it NOW IT'S AN EMERGENCY
                DebugTAC_AI.FatalError("Advanced AI ran out of memory when loading enemy spawns." +
                    " Some enemies might be corrupted!");
            }
        }
        private static IEnumerator CurrCoroutine = null;
        private static float startTime;
        public static void ValidateAndAddAllExternalTechs(bool force = false)
        {
            if (CurrCoroutine != null)
                InvokeHelper.CancelCoroutine(CurrCoroutine);
            startTime = Time.time;
            CurrCoroutine = DoValidateAndAddAllExternalTechsAsync(force);
            InvokeHelper.InvokeCoroutine(CurrCoroutine);
        }
        private static IEnumerator DoValidateAndAddAllExternalTechsAsync(bool force)
        {
            RawTechExporter.ValidateEnemyFolder();
            int tCount = RawTechExporter.GetTechCounts();
            int tMCount = RawTechExporter.GetRawTechsCountExternalMods();
            if (tCount != lastExtLocalCount || lastExtModCount != tMCount || force)
            {
                ExtPopTechsLocal.Clear();
                List<RawTechTemplate> ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechs();
                foreach (RawTechTemplate raw in ExternalTechsRaw)
                {
                    RawTech inst = raw.ToActive();
                    if (inst.ValidateBlocksInTech(true, false))
                    {
                        ExtPopTechsLocal.Add(inst);
                    }
                    else
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Could not load local RawTech " + inst.techName + " as the load operation encountered an error");
                    }
                    yield return null;
                }
                DebugTAC_AI.Log(KickStart.ModID + ": Pushed " + ExtPopTechsLocal.Count + " RawTechs from the Local pool");
                lastExtLocalCount = tCount;


                ExtPopTechsMods.Clear();
                ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechsExternalMods();
                foreach (RawTechTemplate raw in ExternalTechsRaw)
                {
                    RawTech inst = raw.ToActive();
                    if (inst.ValidateBlocksInTech(true, false))
                    {
                        if (inst.purposes == null)
                            inst.purposes = new HashSet<BasePurpose>();
                        if (inst.techName == null)
                            inst.techName = "<NULL>";
                        ExtPopTechsMods.Add(inst);
                    }
                    else
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Could not load ModBundle RawTech " + raw.techName + " as the load operation encountered an error");
                    }
                    yield return null;
                }
                DebugTAC_AI.Log(KickStart.ModID + ": Pushed " + ExtPopTechsMods.Count + " RawTechs from the Mod pool");
                lastExtModCount = tMCount;


                ExtPopTechsAll.Clear();
                ExtPopTechsAll.AddRange(ExtPopTechsLocal);
                ExtPopTechsAll.AddRange(ExtPopTechsMods);
                DebugTAC_AI.Log(KickStart.ModID + ": Pushed a total of " + ExtPopTechsAll.Count + " to the external tech pool.");
                DebugRawTechSpawner.Organize(ref ExtPopTechsLocal);
                DebugRawTechSpawner.Organize(ref ExtPopTechsMods);
                DebugTAC_AI.Log(KickStart.ModID + ": Finished in " + (Time.time - startTime).ToString("F") + " seconds");
            }
        }
        public static void ValidateAndAddAllExternalTechsIMMEDEATELY(bool force = false)
        {
            try
            {
                RawTechExporter.ValidateEnemyFolder();
                int tCount = RawTechExporter.GetTechCounts();
                int tMCount = RawTechExporter.GetRawTechsCountExternalMods();
                if (tCount != lastExtLocalCount || lastExtModCount != tMCount || force)
                {
                    ExtPopTechsLocal.Clear();
                    List<RawTechTemplate> ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechs();
                    foreach (RawTechTemplate raw in ExternalTechsRaw)
                    {
                        RawTech inst = raw.ToActive();
                        if (inst.ValidateBlocksInTech(true, false))
                        {
                            ExtPopTechsLocal.Add(inst);
                        }
                        else
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": Could not load local RawTech " + raw.techName + " as the load operation encountered an error");
                        }
                    }
                    DebugTAC_AI.Log(KickStart.ModID + ": Pushed " + ExtPopTechsLocal.Count + " RawTechs from the Local pool");
                    lastExtLocalCount = tCount;


                    ExtPopTechsMods.Clear();
                    ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechsExternalMods();
                    foreach (RawTechTemplate raw in ExternalTechsRaw)
                    {
                        RawTech inst = raw.ToActive();
                        if (inst.ValidateBlocksInTech(true, false))
                        {
                            if (inst.purposes == null)
                                inst.purposes = new HashSet<BasePurpose>();
                            if (inst.techName == null)
                                inst.techName = "<NULL>";
                            ExtPopTechsMods.Add(inst);
                        }
                        else
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": Could not load ModBundle RawTech " + raw.techName + " as the load operation encountered an error");
                        }
                    }
                    DebugTAC_AI.Log(KickStart.ModID + ": Pushed " + ExtPopTechsMods.Count + " RawTechs from the Mod pool");
                    lastExtModCount = tMCount;


                    ExtPopTechsAll.Clear();
                    ExtPopTechsAll.AddRange(ExtPopTechsLocal);
                    ExtPopTechsAll.AddRange(ExtPopTechsMods);
                    DebugTAC_AI.Log(KickStart.ModID + ": Pushed a total of " + ExtPopTechsAll.Count + " to the external tech pool.");
                    DebugRawTechSpawner.Organize(ref ExtPopTechsLocal);
                    DebugRawTechSpawner.Organize(ref ExtPopTechsMods);
                }
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log("CRASH on ValidateAndAddAllExternalTechs - " + e);
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
        public static bool ValidateBlocksInTech_OBSOLETE(ref string toLoad, RawTech templateToCheck)
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
                    if (!BlockIndexer.StringToBlockType(bloc.t, out BlockTypes type))
                        throw new NullReferenceException("Block does not exists - \nBlockName: " +
                            (bloc.t.NullOrEmpty() ? "<NULL>" : bloc.t));
                    if (!ManMods.inst.IsModdedBlock(type) && !ManSpawn.inst.IsTankBlockLoaded(type))
                        throw new NullReferenceException("Block is not loaded - \nBlockName: " +
                            (bloc.t.NullOrEmpty() ? "<NULL>" : bloc.t));

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
                        catch (InvalidOperationException e)
                        {
                            throw e;
                        }
                        catch (Exception)
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
                toLoad = RawTechBase.MemoryToJSONExternal(mem);

                return valid;
            }
            catch (Exception e)
            {
                SB.Clear();
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ValidateBlocksInTech - Tech " + templateToCheck.techName + " is invalid! - " + e);
                }
                catch (Exception)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ValidateBlocksInTech - Tech <?NULL?> is invalid! - " + e);
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
                for (int step = toScreen.Count - 1; step >= 0; step--)
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
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ValidateBlocksInTech - Tech was corrupted via unexpected mod changes! - " + e);
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
