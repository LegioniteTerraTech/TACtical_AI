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
        private static int lastExtCount = 0;

        public static void ValidateAllStringTechs()
        {
            List<KeyValuePair<SpawnBaseTypes, BaseTemplate>> preCompile = new List<KeyValuePair<SpawnBaseTypes, BaseTemplate>>();

            preCompile.AddRange(CommunityStorage.ReturnAllCommunityStored());
            preCompile.AddRange(TempStorage.techBasesPrefab);

            TempStorage.techBasesAll = preCompile.ToDictionary(x => x.Key, x => x.Value);

            techBases = new Dictionary<SpawnBaseTypes, BaseTemplate>();
            foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> pair in TempStorage.techBasesAll)
            {
                if (ValidateBlocksInTech(pair.Value.savedTech))
                {
                    techBases.Add(pair.Key, pair.Value);
                }
                else 
                {
                    Debug.Log("TACtical AIs: Could not load " + pair.Value.techName + " as it contained missing blocks");
                }
            }

            TempStorage.techBasesAll.Clear(); // GC, do your duty
            CommunityStorage.UnloadRemainingUnused();

            ValidateAndAddAllExternalTechs();
        }
        public static void ValidateAndAddAllExternalTechs()
        {
            ExternalEnemyTechs = new List<BaseTemplate>();
            int tCount = RawTechExporter.GetTechCounts();
            if (tCount != lastExtCount)
            {
                List<BaseTemplate> ExternalTechsRaw = RawTechExporter.LoadAllEnemyTechs();
                foreach (BaseTemplate raw in ExternalTechsRaw)
                {
                    if (ValidateBlocksInTech(raw.savedTech))
                    {
                        ExternalEnemyTechs.Add(raw);
                    }
                    else
                    {
                        Debug.Log("TACtical AIs: Could not load " + raw.techName + " as it contained missing blocks");
                    }
                }
                lastExtCount = tCount;
            }
        }

        public static bool ValidateBlocksInTech(string toLoad)
        {
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
            bool valid = true;
            foreach (BlockMemory bloc in mem)
            {
                BlockTypes type = AIERepair.StringToBlockType(bloc.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    valid = false;
            }
            return valid;
        }

        public static Dictionary<SpawnBaseTypes, BaseTemplate> techBases;
        public static List<BaseTemplate> ExternalEnemyTechs;
    }
}
