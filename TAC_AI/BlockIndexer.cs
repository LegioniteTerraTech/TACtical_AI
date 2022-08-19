using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

#if !STEAM
using Nuterra.BlockInjector;
#endif

namespace TerraTechETCUtil
{
    /// <summary>
    /// A temporary way of doing reverse block lookups for the time being
    /// </summary>
    public class BlockIndexer : MonoBehaviour
    {
        private static BlockIndexer inst;
        private static bool isBlockInjectorPresent => TAC_AI.KickStart.isBlockInjectorPresent;
        private static string FolderDivider => TAC_AI.Templates.RawTechExporter.up;
        private static bool Compiled = false;


        /// <summary>
        /// Searches Block Injector for the block based on root GameObject name.
        /// </summary>
        /// <param name="mem">The name of the block's root GameObject.  This is also set in the Official Mod Tool by the Name ID (filename of the .json), not the name you give it.</param>
        /// <returns>The Block Type to use if it found it, otherwise returns BlockTypes.GSOCockpit_111</returns>
        public static BlockTypes StringToBlockType(string mem)
        {
            if (!Enum.TryParse(mem, out BlockTypes type))
            {
                if (!TryGetMismatchNames(mem, ref type))
                {
                    if (StringToBIBlockType(mem, out BlockTypes BTC))
                    {
                        return BTC;
                    }
                    type = GetBlockIDLogFree(mem);
                }
            }
            return type;
        }
        /// <summary>
        /// Searches the ENTIRE GAME for the block based on root GameObject name.
        /// </summary>
        /// <param name="mem">The name of the block's root GameObject.  This is also set in the Official Mod Tool by the Name ID (filename of the .json), not the name you give it.</param>
        /// <param name="BT">The Block Type to use if it found it</param>
        /// <returns>True if it found it in Block Injector</returns>
        public static bool StringToBlockType(string mem, out BlockTypes BT)
        {
            if (!Enum.TryParse(mem, out BT))
            {
                if (!TryGetMismatchNames(mem, ref BT))
                {
                    if (StringToBIBlockType(mem, out BT))
                        return true;
                    if (GetBlockIDLogFree(mem, out BT))
                        return true;
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Searches Block Injector for the block based on root GameObject name.
        /// </summary>
        /// <param name="mem">The name of the block's root GameObject.  This is also set in the Official Mod Tool by the Name ID (filename of the .json), not the name you give it.</param>
        /// <param name="BT">The Block Type to use if it found it</param>
        /// <returns>True if it found it in Block Injector</returns>
        public static bool StringToBIBlockType(string mem, out BlockTypes BT) // BLOCK INJECTOR
        {
            BT = BlockTypes.GSOAIController_111;

#if !STEAM
            if (!isBlockInjectorPresent)
                return false;
#endif
            if (TryGetIDSwap(mem.GetHashCode(), out BlockTypes BTC))
            {
                BT = BTC;
                return true;
            }
            return false;
        }

        public static BlockTypes GetBlockIDLogFree(string name)
        {
            if (ModdedBlocksGrabbed == null)
                PrepareModdedBlocksSearch();
            if (ModdedBlocksGrabbed != null && ModdedBlocksGrabbed.TryGetValue(name, out int blockType))
                return (BlockTypes)blockType;
            else if (name == "GSO_Exploder_A1_111")
                return (BlockTypes)622;
            return BlockTypes.GSOCockpit_111;
        }

        public static bool GetBlockIDLogFree(string name, out BlockTypes BT)
        {
            if (ModdedBlocksGrabbed == null)
                PrepareModdedBlocksSearch();
            if (ModdedBlocksGrabbed != null && ModdedBlocksGrabbed.TryGetValue(name, out int blockType))
            {
                BT = (BlockTypes)blockType;
                return true;
            }
            else if (name == "GSO_Exploder_A1_111")
            {
                BT = (BlockTypes)622;
                return true;
            }
            BT = BlockTypes.GSOCockpit_111;
            return false;
        }


        // Logless block loader
        private static Dictionary<string, int> ModdedBlocksGrabbed;
        private static readonly FieldInfo allModdedBlocks = typeof(ManMods).GetField("m_BlockIDReverseLookup", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Dictionary<int, BlockTypes> errorNames = new Dictionary<int, BlockTypes>();
        public static void ResetBlockLookupList()
        {
            if (!Compiled)
                return;
            errorNames.Clear();
            Compiled = false;
        }
        /// <summary>
        /// Builds the lookup to use when using block names to find BlockTypes
        /// </summary>
        public static void ConstructBlockLookupListDelayed()
        {
            if (Compiled)
                return;
            if (inst == null)
            {
                inst = new GameObject("BlockIndexerUtil").AddComponent<BlockIndexer>();
            }
            inst.Invoke("ConstructBlockLookupListTrigger", 0.01f);
        }
        private void ConstructBlockLookupListTrigger()
        {
            ConstructBlockLookupList();
        }

        /// <summary>
        /// Builds the lookup to use when using block names to find BlockTypes
        /// </summary>
        public static void ConstructBlockLookupList()
        {
            if (Compiled)
                return;
            try
            {
                List<BlockTypes> types = Singleton.Manager<ManSpawn>.inst.GetLoadedTankBlockNames().ToList();
                foreach (BlockTypes type in types)
                {
                    TankBlock prefab = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type);
                    string name = prefab.name;
                    if (prefab.GetComponent<Damageable>() && type.ToString() != name) //&& !Singleton.Manager<ManMods>.inst.IsModdedBlock(type))
                    {
                        int hash = name.GetHashCode();
                        if (!errorNames.Keys.Contains(hash))
                        {
                            errorNames.Add(hash, type);
#if DEBUG
                            /*
                        if ((int)type > 5000)
                            Debug.Log("TACtical_AI: ConstructErrorBlocksList - Added Modded Block " + name + " | " + type.ToString());
                            */
#endif
                        }
                    }
                }
#if !STEAM
                if (isBlockInjectorPresent)
#endif
                ConstructModdedIDList();
            }
            catch { };

            Debug.Log("BlockUtils: ConstructErrorBlocksList - There are " + errorNames.Count + " blocks with names not equal to their type");
            Compiled = true;
        }

        /// <summary>
        /// Delay this until AFTER Block Injector to setup the lookups
        /// </summary>
        /// <summary>
        /// Call at least once to hook up to modding
        /// </summary>
        public static void PrepareModdedBlocksSearch()
        {
            ModdedBlocksGrabbed = (Dictionary<string, int>)allModdedBlocks.GetValue(Singleton.Manager<ManMods>.inst);
        }

        public static void ConstructModdedIDList()
        {
#if STEAM
            ModSessionInfo session = (ModSessionInfo)access.GetValue(ManMods.inst);
            UnOf_Offi.Clear();
            try
            {
                Dictionary<int, string> blocc = session.BlockIDs;
                foreach (KeyValuePair<int, string> pair in blocc)
                {
                    ModdedBlockDefinition MBD = ManMods.inst.FindModdedAsset<ModdedBlockDefinition>(pair.Value);

                    string SCAN = MBD.m_Json.text;

                    if (SCAN.Contains("NuterraBlock"))
                    {
                        int num = 0;
                        string name = "";
                        if (FindInt(SCAN, "\"ID\":", ref num)) //&& FindText(SCAN, "\"Name\" :", ref name))
                        {
                            UnOf_Offi.Add(("_C_BLOCK:" + num.ToString()).GetHashCode(), (BlockTypes)ManMods.inst.GetBlockID(MBD.name));
                            //Debug.Log("TACtical_AI: ConstructModdedIDList - " + "_C_BLOCK:" + num.ToString() + " | " + MBD.name + " | " + (BlockTypes)ManMods.inst.GetBlockID(MBD.name));
                        }
                    }
                }
            }
            catch { Debug.Log("BlockUtils: ConstructModdedIDList - Error on compile"); };
            Debug.Log("BlockUtils: ConstructModdedIDList - compiled " + UnOf_Offi.Count());
#else
            try
            {
                foreach (KeyValuePair<int, CustomBlock> pair in BlockLoader.CustomBlocks)
                {
                    CustomBlock CB = pair.Value;
                    if (CB != null)
                    {
                        var MCB = CB.Prefab.GetComponent<ModuleCustomBlock>();
                        if (GetNameJSON(MCB.FilePath, out string outp, true))
                            Offi_UnOf.Add(outp.GetHashCode(), (BlockTypes)pair.Key);
                    }
                }
            }
            catch { Debug.Log("TACtical_AI: ConstructModdedIDList - Error on compile"); };
            Debug.Log("TACtical_AI: ConstructModdedIDList - compiled " + Offi_UnOf.Count());
#endif
        }


        private static bool TryGetMismatchNames(string name, ref BlockTypes type)
        {
            if (errorNames.TryGetValue(name.GetHashCode(), out BlockTypes val))
            {
                type = val;
                return true;
            }
            return false;
        }
        private static bool GetNameJSON(string FolderDirectory, out string output, bool excludeJSON)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == FolderDivider.ToCharArray()[0])
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }
            if (!final.ToString().Contains(".RAWTECH"))
            {
                if (!final.ToString().Contains(".JSON") && !excludeJSON)
                {
                    output = null;
                    return false;
                }
                else
                    final.Remove(final.Length - 5, 5);// remove ".JSON"
            }
            else
                final.Remove(final.Length - 8, 8);// remove ".RAWTECH"

            output = final.ToString();
            return true;
        }

#if STEAM
        private static Dictionary<int, BlockTypes> UnOf_Offi = new Dictionary<int, BlockTypes>();
#else
        private static readonly Dictionary<int, BlockTypes> Offi_UnOf = new Dictionary<int, BlockTypes>();
#endif
        private static FieldInfo access = typeof(ManMods).GetField("m_CurrentSession", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool TryGetIDSwap(int hash, out BlockTypes blockType)
        {
#if STEAM
            return UnOf_Offi.TryGetValue(hash, out blockType);
#else
            return Offi_UnOf.TryGetValue(hash, out blockType);
#endif
        }

        private static bool FindInt(string text, string searchBase, ref int intCase)
        {
            int indexFind = text.IndexOf(searchBase);
            if (indexFind >= 0)
            {
                int searchEnd = 0;
                int searchLength = 0;
                string output = "";
                try
                {
                    searchEnd = indexFind + searchBase.Length;
                    searchLength = text.Substring(searchEnd).IndexOf(",");
                    if (searchLength != -1)
                    {
                        output = text.Substring(searchEnd, searchLength);
                        intCase = (int)float.Parse(output);
                        return true;
                    }
                    //Debug.Log(searchEnd + " | " + searchLength + " | " + output + " | ");
                }
                catch (Exception e) { Debug.LogError(searchEnd + " | " + searchLength + " | " + output + " | " + e); }
            }
            return false;
        }
        private static bool FindText(string text, string searchBase, ref string name)
        {
            int indexFind = text.IndexOf(searchBase);
            if (indexFind >= 0)
            {
                int searchEnd = 0;
                int searchLength = 0;
                string output = "";
                try
                {
                    searchEnd = indexFind + searchBase.Length;
                    searchLength = text.Substring(searchEnd).IndexOf(",");
                    output = text.Substring(searchEnd, searchLength);
                    name = output;
                    return true;
                }
                catch (Exception e) { Debug.LogError(searchEnd + " | " + searchLength + " | " + output + " | " + e); }
            }
            return false;
        }

    }
}
