﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using TerraTechETCUtil;

namespace TAC_AI.Templates
{
    internal class CommunityCluster
    {
        internal static void LoadPublicFromFile()
        {
            try
            {
                ClusterF = JsonConvert.DeserializeObject<Dictionary<SpawnBaseTypes, RawTechTemplate>>(FetchPublicFromFile());
            }
            catch { }
        }

        internal static string FetchPublicFromFile()
        {
            string directed = (new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent) + RawTechExporter.up;
            string clusterHold = directed + "commBatch.RTList";
            if (File.Exists(clusterHold))
            {
                return RawTechExporter.LoadCommunityDeployedTechs(clusterHold);
            }
            else
            {
                string clusterHold2 = directed + "batchTechs.json";
                if (File.Exists(clusterHold2))
                {
                    return File.ReadAllText(clusterHold2);
                }
                else
                    DebugTAC_AI.LogError("TACtical_AI: LoadFromDeployed(CommunityCluster) - Files missing or compromized.");
            }
            return "{}";
        }
        internal static void PushDeployedToPublicFile()
        {
            string clusterHold = (new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent) + RawTechExporter.up + "commBatch.RTList";
            string compressedSerial = JsonConvert.SerializeObject(ClusterF, Formatting.None);
            RawTechExporter.SaveExternalRawTechListFileToDisk(clusterHold, compressedSerial);
            string clusterHold2 = new DirectoryInfo(Application.dataPath).Parent.ToString() + RawTechExporter.up + "MassExport" + RawTechExporter.up + "commBatch.RTList";
            RawTechExporter.SaveExternalRawTechListFileToDisk(clusterHold2, compressedSerial);
        }


        internal static string GetLocalToPublic()
        {
            Dictionary<string, RawTechTemplate> ClusterOut = new Dictionary<string, RawTechTemplate>();
            foreach (RawTechTemplate BT in TempManager.ExternalEnemyTechsLocal)
            {
                try
                {
                    string nameBaseType = BT.techName.Replace(" ", "");
                    ClusterOut.Add(nameBaseType, BT);
                }
                catch { }
            }
            return JsonConvert.SerializeObject(ClusterOut, Formatting.Indented, RawTechExporter.JSONDEV);
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
                ManUI.inst.ShowErrorPopup("TAC_AI: Please update the SpawnBaseTypes with the new additions, which can be found in MassExport in your TerraTech Directory by the name of \"ESpawnBaseTypes.json\"");
            Organize(ref dictSorted);
            ClusterF = dictSorted;
        }
        internal static string GetActiveDeployed()
        {
            return JsonConvert.SerializeObject(ClusterF, Formatting.Indented);
        }


        internal static void Organize(ref Dictionary<SpawnBaseTypes, RawTechTemplate> dict)
        {
            dict = dict.OrderBy(x => x.Value.faction).ThenBy(x => x.Value.terrain)
                .ThenBy(x => x.Value.purposes.Contains(BasePurpose.NotStationary))
                .ThenBy(x => x.Value.purposes.Contains(BasePurpose.NANI))
                .ThenBy(x => x.Value.techName).ToDictionary(x => x.Key, x => x.Value);
        }


        internal static Dictionary<SpawnBaseTypes, RawTechTemplate> ClusterF = new Dictionary<SpawnBaseTypes, RawTechTemplate>();


    }
}
