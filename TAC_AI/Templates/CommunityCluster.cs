using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using TerraTechETCUtil;
using Snapshots;

namespace TAC_AI.Templates
{
    internal class CommunityCluster
    {
        internal static void LoadPublicFromFile()
        {
            try
            {
                ClusterF = JsonConvert.DeserializeObject<Dictionary<SpawnBaseTypes, RawTechTemplate>>(FetchPublicTechs());
            }
            catch (Exception e)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": LoadFromDeployed(CommunityCluster) - Files failed to load! - " + e);
            }
        }

        internal static string FetchPublicTechs()
        {
            string directed = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.ToString();
            string clusterHold = Path.Combine(directed, "commBatch.RTList");
            ResourcesHelper.ShowDebug = true;
            try
            {
#if DEBUG
                string import = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
                string clusterHold3 = Path.Combine(import, "batchEdit.json");
                if (File.Exists(clusterHold3))
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": FetchPublicTechs(CommunityCluster) - Loading from DEV test population file...");
                    return File.ReadAllText(clusterHold3);
                }
#endif
                string clusterHold2 = Path.Combine(directed, "batchTechs.json");
                if (File.Exists(clusterHold2))
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": FetchPublicTechs(CommunityCluster) - Loading from test population file...");
                    return File.ReadAllText(clusterHold2);
                }
                byte[] textData = KickStartTAC_AI.oInst.GetModContainer().GetBinaryFromModAssetBundle("commBatch");
                if (textData != null)
                {
                    try
                    {
                        using (MemoryStream FS = new MemoryStream(textData))
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": FetchPublicTechs(CommunityCluster) - Loading from main population file...");
                            return RawTechExporter.LoadCommunityDeployedTechs(FS);
                        }
                    }
                    catch (Exception e)
                    { DebugTAC_AI.Log(KickStart.ModID + ": FetchPublicTechs(CommunityCluster) - commBatch - ERROR " + e); }
                }

                //textData = KickStartTAC_AI.oInst.GetModContainer().GetTextFromModAssetBundle("commBatch");
                if (textData == null)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": FetchPublicTechs(CommunityCluster) - FAILED from assetbundle, trying local...");
                    if (File.Exists(clusterHold))
                    {
                        using (FileStream FS = File.Open(clusterHold, FileMode.Open, FileAccess.Read))

                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": FetchPublicTechs(CommunityCluster)[EXTERNAL] - Loading from main population file...");
                            return RawTechExporter.LoadCommunityDeployedTechs(FS);
                        }
                    }
                }
                //if (textData == null)
                //    DebugTAC_AI.Assert("commBatch could not be found!");
                DebugTAC_AI.Log(KickStart.ModID + ": FetchPublicTechs(CommunityCluster) - File " + Path.GetFileNameWithoutExtension("commBatch.RTList") +
                    " missing or compromized - looking into our contents:");
                ResourcesHelper.LookIntoModContents(KickStartTAC_AI.oInst.GetModContainer());
                return "{}";
            }
            finally
            {
                ResourcesHelper.ShowDebug = false;
            }
        }
        internal static void PushDeployedToPublicFile()
        {
            string clusterHold = Path.Combine(new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.ToString(), "commBatch.RTList");
            Dictionary<string, RawTechTemplate> rawExportFast = new Dictionary<string, RawTechTemplate>();
            foreach (var item in ClusterF)
            {
                rawExportFast.Add(((int)item.Key).ToString(), item.Value);
            }
            string compressedSerial = JsonConvert.SerializeObject(rawExportFast, Formatting.None);
            RawTechExporter.SaveExternalRawTechListFileToDisk(clusterHold, compressedSerial);
            string clusterHold2 = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport", "commBatch.RTList");
            RawTechExporter.SaveExternalRawTechListFileToDisk(clusterHold2, compressedSerial);
        }


        internal static string GetLocalToPublic()
        {
            Dictionary<string, RawTechTemplate> ClusterOut = new Dictionary<string, RawTechTemplate>();
            int SBTCounter = (int)SpawnBaseTypes.GSOQuickBuck;
            foreach (RawTech BT in ModTechsDatabase.ExtPopTechsLocal.OrderBy(x => x.techName))
            {
                try
                {
                    string nameBaseType = BT.techName.Replace(" ", "");
                    if (!nameBaseType.Contains('#'))
                    {
                        SBTCounter++;
                        ClusterOut.Add(SBTCounter.ToString(), BT.ToTemplate());
                    }
                }
                catch { }
            }
            return JsonConvert.SerializeObject(ClusterOut, Formatting.Indented);//, RawTechExporter.JSONDEV);
        }


        internal static void DeployUncompressed(string location)
        {
            Dictionary<string, RawTechTemplate> dict = JsonConvert.DeserializeObject<Dictionary<string, RawTechTemplate>>(File.ReadAllText(location));
            Dictionary<SpawnBaseTypes, RawTechTemplate> dictSorted = new Dictionary<SpawnBaseTypes, RawTechTemplate>();
            bool needsToAddToSpawnBaseTypes = false;
            foreach (var item in dict)
            {
                if (Enum.TryParse(item.Key, out SpawnBaseTypes res))
                {
                    if (!dictSorted.TryGetValue(res, out _))
                        dictSorted.Add(res, item.Value);
                    //Else, overlapping entry
                }
                else
                    needsToAddToSpawnBaseTypes = true;
            }
            if (needsToAddToSpawnBaseTypes)
            {
                ManUI.inst.ShowErrorPopup(KickStart.ModID + ": Please update the SpawnBaseTypes with the new additions, which can be found in MassExport in your TerraTech Directory by the name of \"ESpawnBaseTypes.json\"");
                DebugRawTechSpawner.OpenInExplorer(location);
            }
            Organize(ref dictSorted);
            ClusterF = dictSorted;
        }
        internal static string GetActiveDeployed()
        {
            return JsonConvert.SerializeObject(ClusterF, Formatting.Indented);
        }
        internal static void SaveCommunityPoolBackToDisk()
        {
            LoadPublicFromFile();
            var disk = ManSnapshots.inst.ServiceDisk.GetSnapshotCollectionDisk();
            if (disk == null)
                throw new NullReferenceException("ManSnapshots.inst.ServiceDisk failed to load");
            foreach (var item in ClusterF)
            {
                RawTech inst = item.Value.ToActive();
                var snap = disk.FindSnapshot(inst.techName);
                if (snap == null)
                    techsToSaveSnapshots.Enqueue(inst);
            }
            DoSaveCommunityTechBackToDisk();
            ClusterF.Clear();
        }
        private static Queue<RawTech> techsToSaveSnapshots = new Queue<RawTech>();
        private static void DoSaveCommunityTechBackToDisk(bool success = true)
        {
            if (!techsToSaveSnapshots.Any() || !success)
                return;
            RawTech RT = techsToSaveSnapshots.Dequeue();
            var techD = RawTechLoader.GetUnloadedTech(RT, ManPlayer.inst.PlayerTeam, true, out _);
            ManScreenshot.inst.RenderTechImage(techD, ManSnapshots.inst.GetDiskSnapshotImageSize(),
                true, (TechData techData, Texture2D techImage) =>
                {
                    Singleton.Manager<ManSnapshots>.inst.SaveSnapshotRender(techData, techImage,
                        RT.techName, false, DoSaveCommunityTechBackToDisk);
                });
        }

        internal static string ExportSnapsToCommunityPool()
        {
            HashSet<string> basetypeNames = new HashSet<string>();
            List<string> basetypeNamesOrdered = new List<string>();
            Dictionary<string, RawTechTemplate> ClusterOut = new Dictionary<string, RawTechTemplate>();
            int SBTCounter = (int)SpawnBaseTypes.GSOQuickBuck;
            var disk = ManSnapshots.inst.ServiceDisk.GetSnapshotCollectionDisk();
            if (disk == null)
                throw new NullReferenceException("ManSnapshots.inst.ServiceDisk failed to load");
            foreach (var item in disk.Snapshots)
            {
                Snapshot inst = item;
                if (inst != null)
                {
                    string nameBaseType = DebugRawTechSpawner.GetBaseTypeGenerated(item.m_Name.Value);
                    if (basetypeNames.Add(nameBaseType))
                    {
                        SBTCounter++;
                        ClusterOut.Add(SBTCounter.ToString(), new RawTechTemplate(inst.techData, false));
                    }
                }
            }
            string export = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
            File.WriteAllText(Path.Combine(export, "ESpawnBaseTypes.json"), ""); // CLEAR
            List<string> toWrite = new List<string>();
            foreach (string str in basetypeNamesOrdered)
            {
                toWrite.Add(str + ",");
            }
            File.AppendAllLines(Path.Combine(export, "ESpawnBaseTypes.json"), toWrite);
            return JsonConvert.SerializeObject(ClusterOut, Formatting.Indented);//, RawTechExporter.JSONDEV);
        }


        internal static void Organize(ref Dictionary<SpawnBaseTypes, RawTechTemplate> dict)
        {
            dict = dict.OrderBy(x => x.Value.faction).ThenBy(x => x.Value.terrain)
                .ThenBy(x => x.Value.purposes.Contains(BasePurpose.NotStationary))
                .ThenBy(x => x.Value.purposes.Contains(BasePurpose.NANI))
                .ThenBy(x => x.Value.techName).ToDictionary(x => x.Key, x => x.Value);
        }
        internal static void Organize(ref Dictionary<SpawnBaseTypes, RawTech> dict)
        {
            dict = dict.OrderBy(x => x.Value.faction).ThenBy(x => x.Value.terrain)
                .ThenBy(x => x.Value.purposes.Contains(BasePurpose.NotStationary))
                .ThenBy(x => x.Value.purposes.Contains(BasePurpose.NANI))
                .ThenBy(x => x.Value.techName).ToDictionary(x => x.Key, x => x.Value);
        }


        internal static Dictionary<SpawnBaseTypes, RawTechTemplate> ClusterF = new Dictionary<SpawnBaseTypes, RawTechTemplate>();


    }
}
