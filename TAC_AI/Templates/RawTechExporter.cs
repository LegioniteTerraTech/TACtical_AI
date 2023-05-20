using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Ionic.Zlib;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;
using TerraTechETCUtil;

namespace TAC_AI.Templates
{
    public class AIBookmarker : MonoBehaviour
    {   // External AI-setting interface - used to set Tech AI state externally
        public EnemyHandling commander = EnemyHandling.Wheeled;
        public EAttackMode attack = EAttackMode.Circle;
        public EnemyAttitude attitude = EnemyAttitude.Default;
        public EnemySmarts smarts = EnemySmarts.Default;
        public EnemyBolts bolts = EnemyBolts.Default;
    }

    /// <summary>
    /// Don't try bothering with anything sneaky with this - it's built against illegal blocks and block rotations.
    /// </summary>
    public class RawTechTemplateFast
    {   // External builder interface - use to save Techs externally
        public string Name = "unset";
        public string Blueprint;
        public bool InfBlocks;
        public bool IsAnchored;
        public FactionTypesExt Faction;
        public bool NonAggressive = false;
        public bool Eradicator = false;
        public int Cost = 0;
    }

    public class DEVTypeEnumConverter : StringEnumConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is BasePurpose)
            {
                writer.WriteValue(Enum.GetName(typeof(BasePurpose), (BasePurpose)value));
                return;
            }
            else if (value is BaseTerrain)
            {
                writer.WriteValue(Enum.GetName(typeof(BaseTerrain), (BaseTerrain)value));
                return;
            }
            else if (value is FactionTypesExt)
            {
                writer.WriteValue(Enum.GetName(typeof(FactionTypesExt), (FactionTypesExt)value));
                return;
            }

            base.WriteJson(writer, value, serializer);
        }
    }

    public class RawTechExporter : MonoBehaviour
    {
        public static RawTechExporter inst;
        public static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        public static bool isOpen;
        public static bool pendingInGameReload;
        public static string up = "\\";

        public static bool ExportJSONInsteadOfRAWTECH = false;

        public static string DLLDirectory;
        public static string BaseDirectory;
        public static string RawTechsDirectory;


        public static JsonSerializerSettings JSONDEV = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new DEVTypeEnumConverter() },
        };


        // AI Icons
        public static Dictionary<AIDriverType, Sprite> aiBackplates;
        public static Dictionary<AIType, Sprite> aiIcons;
        public static Dictionary<EnemySmarts, Sprite> aiIconsEnemy;
        public static AIECore.TankAIHelper lastTech;

        private static bool firstInit = false;

        // GUI
        private const int RawTechExporterID = 846321;
        private static Sprite referenceAIIcon;
        public static void Initiate()
        {
            if (inst)
                return;
            referenceAIIcon = Resources.FindObjectsOfTypeAll<Sprite>().ToList().Find(delegate 
                (Sprite cand)
            { return cand.name == "Icon_AI_Guard"; });
#if STEAM
            // Steam does not support RawTech loading the same way as Unofficial.
            if (!firstInit)
            {
                SetupWorkingDirectoriesSteam(); 
                GUIWindow = new GameObject();
                GUIWindow.AddComponent<GUIRawDisplay>();
                GUIWindow.SetActive(false);
                aiBackplates = new Dictionary<AIDriverType, Sprite> {
                    {AIDriverType.Pilot,  LoadSprite("AI_BackAir.png", true) },
                    {AIDriverType.Sailor,  LoadSprite("AI_BackSea.png", true) },
                    {AIDriverType.Astronaut,  LoadSprite("AI_BackSpace.png", true) },
                };
                aiIcons = new Dictionary<AIType, Sprite> {
                    {AIType.Escort,  LoadSprite("AI_Tank.png") },
                    {AIType.MTMimic,  LoadSprite("AI_Mimic.png") },
                    {AIType.MTStatic,  LoadSprite("AI_MT.png") },
                    {AIType.MTTurret,  LoadSprite("AI_Turret.png") },
                    {AIType.Aegis,  LoadSprite("AI_Aegis.png") },
                    {AIType.Assault,  LoadSprite("AI_Assault.png") },
                    {AIType.Prospector,  LoadSprite("AI_Harvest.png") },
                    {AIType.Scrapper,  LoadSprite("AI_Scrapper.png") },
                    {AIType.Energizer,  LoadSprite("AI_Energizer.png") },
                    {AIType.Aviator,  LoadSprite("AI_Pilot.png") },
                    {AIType.Buccaneer,  LoadSprite("AI_Ship.png") },
                    {AIType.Astrotech,  LoadSprite("AI_Space.png") },
                };
                aiIconsEnemy = new Dictionary<EnemySmarts, Sprite> {
                    {EnemySmarts.Mild,  LoadSprite("E_Mild.png") },
                    {EnemySmarts.Meh,  LoadSprite("E_Meh.png") },
                    {EnemySmarts.Smrt,  LoadSprite("E_Smrt.png") },
                    {EnemySmarts.IntAIligent,  LoadSprite("E_Intel.png") },
                };
                firstInit = true;
                DebugTAC_AI.Log("TACtical_AI: FirstInit RawTechExporter");
            }
            inst = Instantiate(new GameObject("RawTechExporter")).AddComponent<RawTechExporter>();
            inst.Invoke("LateInitiate", 0.001f);
#else
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX)
            {
                up = "/";
            }
            inst = Instantiate(new GameObject("RawTechExporter")).AddComponent<RawTechExporter>();
            inst.Invoke("LateInitiate", 0.001f);
            if (!firstInit)
            {
                GUIWindow = new GameObject();
                GUIWindow.AddComponent<GUIRawDisplay>();
                GUIWindow.SetActive(false);
                SetupWorkingDirectories();
                aiIcons = new Dictionary<AIType, Sprite> {
                {AIType.Escort,  LoadSprite("AI_Tank.png") },
                {AIType.MTMimic,  LoadSprite("AI_Mimic.png") },
                {AIType.MTStatic,  LoadSprite("AI_MT.png") },
                {AIType.MTTurret,  LoadSprite("AI_Turret.png") },
                {AIType.Aegis,  LoadSprite("AI_Aegis.png") },
                {AIType.Assault,  LoadSprite("AI_Assault.png") },
                {AIType.Prospector,  LoadSprite("AI_Harvest.png") },
                {AIType.Scrapper,  LoadSprite("AI_Scrapper.png") },
                {AIType.Energizer,  LoadSprite("AI_Energizer.png") },
                {AIType.Aviator,  LoadSprite("AI_Pilot.png") },
                {AIType.Buccaneer,  LoadSprite("AI_Ship.png") },
                {AIType.Astrotech,  LoadSprite("AI_Space.png") },
            };
                aiIconsEnemy = new Dictionary<EnemySmarts, Sprite> {
                {EnemySmarts.Mild,  LoadSprite("E_Mild.png") },
                {EnemySmarts.Meh,  LoadSprite("E_Meh.png") },
                {EnemySmarts.Smrt,  LoadSprite("E_Smrt.png") },
                {EnemySmarts.IntAIligent,  LoadSprite("E_Intel.png") },
            };
                firstInit = true;
            }
#endif
#if DEBUG
            ExportJSONInsteadOfRAWTECH = true;
#endif
            BlockIndexer.PrepareModdedBlocksFetch();
        }

        public static void DeInit()
        {
            if (!inst)
                return;
            ManPauseGame.inst.PauseEvent.Unsubscribe(inst.UpdatePauseStatus);
            DebugTAC_AI.Log("TACtical_AI: RawTechExporter - Unsubscribing from Pause Screen");
            isSubbed = false;
            Destroy(inst.gameObject);
            inst = null;
        }
        internal class GUIRawDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isOpen)
                {
                    AltUI.StartUI();
                    HotWindow = GUI.Window(RawTechExporterID, HotWindow, GUIHandler, "RAW Tech Saving", AltUI.MenuLeft);
                    AltUI.EndUI();
                }
            }
        }

        private static bool isSubbed = false;
        public void LateInitiate()
        {
            if (!inst || isSubbed)
                return;
            ManPauseGame.inst.PauseEvent.Subscribe(inst.UpdatePauseStatus);
            DebugTAC_AI.Log("TACtical_AI: RawTechExporter - Subscribing to Pause Screen");
            if (KickStart.isBlockInjectorPresent)
                BlockIndexer.ConstructModdedIDList();
            isSubbed = true;
            // Was causing way too many issues with enemies
            //Globals.inst.m_BlockSurvivalChance = KickStart.EnemyBlockDropChance / 100.0f;
        }

        public void UpdatePauseStatus(bool paused)
        {
            if (paused && SpecialAISpawner.CreativeMode)
                LaunchSubMenu();
            else
                CloseSubMenu();
        }
        public void Update()
        {
            CheckKeyCombos();
#if STEAM
            LateInitiate();
#endif
        }
        public void CheckKeyCombos()
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftAlt))
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    ReloadRawTechLoader();
                    if (!SpecialAISpawner.thisActive)
                        pendingInGameReload = true;
                }
                if (Input.GetKeyDown(KeyCode.B))
                {
                    Invoke("ReloadRawTechLoaderInsured", 0);
                }
            }
        }
        public void ReloadRawTechLoaderInsured()
        {
            float timeDelay = Time.time;
            DebugTAC_AI.Log("TACtical_AI: Rebuilding Raw Tech Loader!");
            BlockIndexer.ConstructBlockLookupList();
            TempManager.ValidateAndAddAllExternalTechs(true);
            timeDelay = Time.time - timeDelay;
            DebugTAC_AI.Log("TACtical_AI: Done in " + timeDelay + " seconds");
        }
        public void ReloadRawTechLoader()
        {
            float timeDelay = Time.time;
            DebugTAC_AI.Log("TACtical_AI: Rebuilding Raw Tech Loader!");
            BlockIndexer.ConstructBlockLookupList();
            TempManager.ValidateAndAddAllExternalTechs(true);
            timeDelay = Time.time - timeDelay;
            DebugTAC_AI.Log("TACtical_AI: Done in " + timeDelay + " seconds");
        }
        public static void Reload()
        {
            if (pendingInGameReload)
            {
                float timeDelay = Time.time;
                DebugTAC_AI.Log("TACtical_AI: Reloading All Raw Enemy Techs (Ingame)!");
                TempManager.ValidateAndAddAllExternalTechs(true);
                timeDelay = Time.time - timeDelay;
                DebugTAC_AI.Log("TACtical_AI: Done in " + timeDelay + " seconds");
                pendingInGameReload = false;
            }
        }
        public static void ReloadExternal()
        {
            float timeDelay = Time.time;
            DebugTAC_AI.Log("TACtical_AI: Reloading All Raw Enemy Techs!");
            TempManager.ValidateAndAddAllExternalTechs();
            timeDelay = Time.time - timeDelay;
            DebugTAC_AI.Log("TACtical_AI: Done in " + timeDelay + " seconds");
            pendingInGameReload = false;
        }

        private static void GUIHandler(int ID)
        {
            bool snapsAvail = SnapsAvailable();
            if (GUI.Button(new Rect(20, 30, 160, 40), new GUIContent("<b><color=#ffffffff>SAVE CURRENT</color></b>", "Save current Tech to the Raw Techs directory"), AltUI.ButtonBlue))
            {
                SaveTechToRawJSON(Singleton.playerTank);
            }
            if (GUI.Button(new Rect(20, 70, 160, 40), new GUIContent("<b><color=#ffffffff>+ ENEMY POP</color></b>", "Save current Tech to Raw Enemies pop in eLocal."), AltUI.ButtonBlue))
            {
                SaveEnemyTechToRawJSON(Singleton.playerTank);
                inst.ReloadRawTechLoader();
            }
            if (GUI.Button(new Rect(20, 110, 160, 40), new GUIContent("<b><color=#ffffffff>+ ALL SNAPS</color></b>", snapsAvail ? "Save ALL snapshots to Raw Enemies pop in eBulk." : "Open the snapshots menu at least once first!"), snapsAvail ? AltUI.ButtonBlue : AltUI.ButtonGrey))
            {
                SaveEnemyTechsToRawBLK();
                inst.ReloadRawTechLoader();
            }
            GUI.Label(new Rect(20, 160, 150, 75), AltUI.UIAlphaText + GUI.tooltip + "</color>");
            GUI.DragWindow();
        }
        public static void LaunchSubMenu()
        {
            if (Singleton.playerTank.IsNull() || !KickStart.EnableBetterAI)
            {
                //DebugTAC_AI.Log("TACtical_AI: TANK IS NULL!");
                CloseSubMenu();
                return;
            }
            //DebugTAC_AI.Log("TACtical_AI: Opened RawJSON menu");
            isOpen = true;
            GUIWindow.SetActive(true);
        }
        public static void CloseSubMenu()
        {
            if (isOpen)
            {
                isOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                //DebugTAC_AI.Log("TACtical_AI: Closed RawJSON menu");
            }
        }


        // Setup
        public static void SetupWorkingDirectories()
        {
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            di = di.Parent; // off of this DLL
            DLLDirectory = di.ToString();
            di = di.Parent; // out of the DLL folder
            di = di.Parent; // out of QMods
            BaseDirectory = di.ToString();
            RawTechsDirectory = di.ToString() + up + "Raw Techs";
#if DEBUG
            DebugTAC_AI.Log("TACtical_AI: DLL folder is at: " + DLLDirectory);
            DebugTAC_AI.Log("TACtical_AI: Raw Techs is at: " + RawTechsDirectory);
#endif
            ValidateEnemyFolder();
        }
        public static void SetupWorkingDirectoriesSteam()
        {
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            di = di.Parent; // off of this DLL
            DLLDirectory = di.ToString();

            DirectoryInfo Navi = new DirectoryInfo(Application.dataPath);
            Navi = Navi.Parent; // out of the GAME folder
            BaseDirectory = Navi.ToString();
            RawTechsDirectory = Navi.ToString() + up + "Raw Techs";

            ValidateEnemyFolder();
        }


        // Operations
        internal static int GetRawTechsCountExternalMods()
        {
#if STEAM
            int count = 0;
            string location = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent.ToString();// Go to the cluster directory

            DebugTAC_AI.LogDevOnly("TACtical_AI: RegisterExternalCorpTechs - searching in " + location);
            foreach (string directoryLoc in Directory.GetDirectories(location))
            {
                try
                {
                    string GO;
                    string fileName = "RawTechs.RTList";
                    GO = directoryLoc + up + fileName;
                    if (File.Exists(GO))
                    {
                        count++;
                    }
                    else
                        break;
                }
                catch { }
            }
            return count;
#else
            return 0;
#endif
        }

        internal static void MakeExternalRawTechListFile(string fileNameAndPath, List<RawTechTemplate> content)
        {
            string toWrite = JsonConvert.SerializeObject(content, Formatting.None);
            MakeExternalRawTechListFile(fileNameAndPath, toWrite);
        }
        internal static void MakeExternalRawTechListFile(string fileNameAndPath, string toWrite)
        {
            using (FileStream FS = File.Create(fileNameAndPath))
            {
                using (GZipStream GZS = new GZipStream(FS, CompressionMode.Compress))
                {
                    using (StreamWriter SW = new StreamWriter(GZS))
                    {
                        SW.WriteLine(toWrite);
                        SW.Flush();
                    }
                }
            }
        }

        internal static string LoadCommunityDeployedTechs(string location)
        {
            string toAdd = "";
            //DebugTAC_AI.Log("TACtical_AI: RegisterExternalCorpTechs - searching in " + location);
            try
            {
                //DebugTAC_AI.Log("TACtical_AI: RegisterExternalCorpTechs - looked in " + GO);
                if (File.Exists(location))
                {
                    using (FileStream FS = File.Open(location, FileMode.Open, FileAccess.Read))
                    {
                        using (GZipStream GZS = new GZipStream(FS, CompressionMode.Decompress))
                        {
                            using (StreamReader SR = new StreamReader(GZS))
                            {
                                toAdd = SR.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            { DebugTAC_AI.Log("TACtical_AI: LoadCommunityDeployedTechs - ERROR " + e);}
            if (!toAdd.NullOrEmpty())
                DebugTAC_AI.Log("TACtical_AI: LoadCommunityDeployedTechs - Added Techs");
            else
                DebugTAC_AI.Log("TACtical_AI: LoadCommunityDeployedTechs - No bundled techs found");
            return toAdd;
        }

        internal static List<RawTechTemplate> LoadAllEnemyTechsExternalMods()
        {
#if STEAM
            List<RawTechTemplate> toAdd = new List<RawTechTemplate>();
            string location = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent.ToString();// Go to the cluster directory

            //DebugTAC_AI.Log("TACtical_AI: RegisterExternalCorpTechs - searching in " + location);
            foreach (string directoryLoc in Directory.GetDirectories(location))
            {
                try
                {
                    string fileName = "RawTechs.RTList";
                    string GO = directoryLoc + up + fileName;
                    //DebugTAC_AI.Log("TACtical_AI: RegisterExternalCorpTechs - looked in " + GO);
                    if (File.Exists(GO))
                    {
                        using (FileStream FS = File.Open(GO, FileMode.Open, FileAccess.Read))
                        {
                            using (GZipStream GZS = new GZipStream(FS, CompressionMode.Decompress))
                            {
                                using (StreamReader SR = new StreamReader(GZS))
                                {
                                    List<RawTechTemplate> ext = JsonConvert.DeserializeObject<List<RawTechTemplate>>(SR.ReadToEnd());
                                    toAdd.AddRange(ext);
                                }
                            }
                        }
                    }
                }
                catch (Exception e) { DebugTAC_AI.Log("TACtical_AI: LoadAllEnemyTechsExternalMods - ERROR " + e); }
            }
            if (toAdd.Count > 0)
                DebugTAC_AI.Log("TACtical_AI: LoadAllEnemyTechsExternalMods - Added " + toAdd.Count + " techs from mods");
            else
                DebugTAC_AI.Log("TACtical_AI: LoadAllEnemyTechsExternalMods - No bundled techs found");
            return toAdd;
#else
            return new List<BaseTemplate>();
#endif
        }
        public static List<RawTechTemplate> LoadAllEnemyTechs()
        {
            ValidateEnemyFolder();
            List<string> Dirs = GetALLDirectoriesInFolder(RawTechsDirectory + up + "Enemies");
            List<RawTechTemplate> temps = new List<RawTechTemplate>();
            List<string> names;
            DebugTAC_AI.Log("TACtical_AI: LoadAllEnemyTechs - Total directories found in Enemies Folder: " + Dirs.Count());
            foreach (string Dir in Dirs)
            {
                names = GetTechNameListDir(Dir);
                DebugTAC_AI.Log("TACtical_AI: LoadAllEnemyTechs - Total RAW Techs found in " + GetNameDirectory(Dir) + ": " + names.Count());
                foreach (string name in names)
                {
                    int errorLevel = 0;
                    try
                    {
                        RawTechTemplateFast ext = LoadEnemyTech(name, Dir);
                        errorLevel++; // 1
                        RawTechTemplate temp = new RawTechTemplate
                        {
                            techName = ext.Name,
                            savedTech = ext.Blueprint,
                            startingFunds = ValidateCost(ext.Blueprint, ext.Cost),
                        };
                        errorLevel++; // 2
                        FactionTypesExt MainCorp;
                        if (ext.Faction == FactionTypesExt.NULL)
                        {
                            MainCorp = KickStart.GetCorpExtended(AIERepair.JSONToFirstBlock(ext.Blueprint));
                        }
                        else
                            MainCorp = ext.Faction;
                        errorLevel++; // 3
                        temp.purposes = RawTechTemplate.GetHandler(ext.Blueprint, MainCorp, ext.IsAnchored, out BaseTerrain terra, out int minCorpGrade);
                        temp.IntendedGrade = minCorpGrade;
                        temp.faction = MainCorp;
                        temp.terrain = terra;

                        temps.Add(temp);
                        DebugTAC_AI.Info("TACtical_AI: Added " + name + " to the RAW Enemy Tech Pool, grade " + minCorpGrade + " " + MainCorp.ToString() + ", of BB Cost " + temp.startingFunds + ".");
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Could not add " + name + " to the RAW Enemy Tech Pool!  Corrupted BuilderExternal(Or tech too small)!! - Error Level " + errorLevel + " ERROR " + e);
                    }
                }
            }
            return temps;
        }
        internal static List<string> GetAllNames(string directory)
        {
            List<string> Cleaned = new List<string>();
            List<string> Dirs = GetALLDirectoriesInFolder(directory);
            Dirs.Add(directory); 
            
            foreach (string Dir in Dirs)
            {
                Cleaned.AddRange(GetTechNameList(Dir));
            }

            return Cleaned;
        }
        internal static List<string> GetTechNameListDir(string directory, bool ExcludeJSON = false)
        {
            List<string> toClean = Directory.GetFiles(directory).ToList();
            List<string> Cleaned = CleanNames(toClean, ExcludeJSON);
            return Cleaned;
        }
        internal static List<string> GetTechNameList(string altDirectoryFromBaseDirectory = null)
        {
            string search;
            List<string> Cleaned;
            if (altDirectoryFromBaseDirectory == null)
            {
                search = RawTechsDirectory + up + "Enemies";
            }
            else
            {
                search = BaseDirectory + up + altDirectoryFromBaseDirectory;
            }
            List<string> toClean = Directory.GetFiles(search).ToList();
            Cleaned = CleanNames(toClean, false);
            return Cleaned;
        }
        internal static int GetTechCounts()
        {
            List<string> Dirs = GetALLDirectoriesInFolder(RawTechsDirectory + up + "Enemies");
            int techCount = 0;
            foreach (string Dir in Dirs)
            {
                techCount += GetTechNameListDir(Dir).Count;
            }
            return techCount;
        }

        private static List<string> CleanNames(List<string> FolderDirectories, bool excludeJSON)
        {
            List<string> Cleaned = new List<string>();
            foreach (string cleaning in FolderDirectories)
            {
                if (!GetNameJSON(cleaning, out string output, excludeJSON))
                    continue;
                Cleaned.Add(output);
            }
            return Cleaned;
        }
        internal static bool GetNameJSON(string FolderDirectory, out string output, bool excludeJSON)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == up.ToCharArray()[0])
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
        private static readonly FieldInfo forceVal = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance); 
        
        public static int ValidateCost(string blueprint, int ExistingCost)
        {
            if (ExistingCost <= 0)
            {
                List<RawBlockMem> mems = RawTechTemplate.JSONToMemoryExternal(blueprint);
                ExistingCost = RawTechTemplate.GetBBCost(RawTechTemplate.JSONToMemoryExternal(blueprint));
            }
            if (ExistingCost <= 0)
            {
                DebugTAC_AI.Log("TACtical_AI: ValidateCost - Invalid tech cost encountered ~ could not handle!");
                ExistingCost = 0;
            }

            return ExistingCost;
        }

        internal static void ValidateEnemyFolder()
        {
            string destination = RawTechsDirectory + up + "Enemies";
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Log("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Log("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
        }

        // Snapshot to RawTech
        public static bool SnapsAvailable()
        {
            try
            {
                List<Binding.SnapshotLiveData> Snaps = ManSnapshots.inst.m_Snapshots.ToList();
                if (Snaps.Count > 0)
                    return true;
            }
            catch
            {
                // error
                return false;
            }
            // No snaps loaded.
            return false;
        }
        public static void SaveEnemyTechsToRawBLK()
        {
            List<Binding.SnapshotLiveData> Snaps = ManSnapshots.inst.m_Snapshots.ToList();
            if (Snaps.Count == 0)
                return;
            foreach (Binding.SnapshotLiveData snap in Snaps)
            {
                try
                {
                    SaveEnemyTechToRawJSONBLK(snap.m_Snapshot.techData);
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: SaveEnemyTechsToRawBLK - Export Failure reported!");
                    try
                    {
                        DebugTAC_AI.Log("TACtical_AI: Error tech name " + snap.m_Snapshot.techData.Name);
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: SaveEnemyTechsToRawBLK - COULD NOT FETCH TECHDATA!!!");
                    }
                }
            }
            ReloadExternal();
        }


        // JSON Handlers
        public static void SaveTechToRawJSON(Tank tank)
        {
            RawTechTemplateFast builder = new RawTechTemplateFast
            {
                Name = tank.name,
                Faction = tank.GetMainCorpExt(),//GetTopCorp(tank);
                Blueprint = RawTechTemplate.TechToJSONExternal(tank),
                InfBlocks = false,
                IsAnchored = tank.IsAnchored,
                NonAggressive = !IsLethal(tank),
                Eradicator = tank.blockman.blockCount >= AIGlobals.LethalTechSize || tank.blockman.IterateBlockComponents<ModuleWeaponGun>().Count() > 48 || tank.blockman.IterateBlockComponents<ModuleHover>().Count() > 18,
                Cost = RawTechTemplate.GetBBCost(tank)
            };
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveTechToFile(tank.name, builderJSON);
        }
        public static void SaveEnemyTechToRawJSON(Tank tank)
        {
            RawTechTemplateFast builder = new RawTechTemplateFast
            {
                Name = tank.name,
                Faction = tank.GetMainCorpExt(),
                Blueprint = RawTechTemplate.TechToJSONExternal(tank),
                InfBlocks = false,
                IsAnchored = tank.IsAnchored,
                NonAggressive = !IsLethal(tank),
                Eradicator = tank.blockman.blockCount >= AIGlobals.LethalTechSize || tank.blockman.IterateBlockComponents<ModuleWeaponGun>().Count() > 48 || tank.blockman.IterateBlockComponents<ModuleHover>().Count() > 18,
                Cost = RawTechTemplate.GetBBCost(tank)
            };
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveEnemyTechToFile(tank.name, builderJSON);
            ReloadExternal();
        }
        public static void SaveEnemyTechToRawJSONBLK(TechData tank)
        {
            RawTechTemplateFast builder = new RawTechTemplateFast();
            string bluep = RawTechTemplate.BlockSpecToJSONExternal(tank.m_BlockSpecs, out int blockCount, out bool lethal, out int hoveCount, out int weapGCount);
            builder.Name = tank.Name;
            builder.Faction = tank.GetMainCorpExt();
            builder.Blueprint = bluep;
            builder.InfBlocks = false;
            builder.IsAnchored = tank.CheckIsAnchored();
            builder.NonAggressive = lethal;
            builder.Eradicator = blockCount >= AIGlobals.LethalTechSize || weapGCount > 48 || hoveCount > 18;
            builder.Cost = tank.GetValue();
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveEnemyTechToFileBLK(tank.Name, builderJSON);
        }
        public static RawTechTemplateFast LoadTechFromRawJSON(string TechName, string altFolderName = "")
        {
            string loaded = LoadTechFromFile(TechName, altFolderName);
            return JsonUtility.FromJson<RawTechTemplateFast>(loaded);
        }
        internal static RawTechTemplateFast LoadEnemyTech(string TechName, string altDirect = "")
        {
            string loaded = LoadEnemyTechFromFile(TechName, altDirect);
            return JsonUtility.FromJson<RawTechTemplateFast>(loaded);
        }
        internal static RawTechTemplateFast SearchAndLoadEnemyTech(string TechName)
        {
            string loaded = FindAndLoadEnemyTechFromFile(TechName);
            return JsonUtility.FromJson<RawTechTemplateFast>(loaded);
        }



        // Loaders
        private static void SaveTechToFile(string TechName, string RawTechJSON)
        {
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Log("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                if (ExportJSONInsteadOfRAWTECH)
                {
                    File.WriteAllText(RawTechsDirectory + up + TechName + ".JSON", RawTechJSON);
                    DebugTAC_AI.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
                }
                else
                {
                    File.WriteAllText(RawTechsDirectory + up + TechName + ".RAWTECH", RawTechJSON);
                    DebugTAC_AI.Log("TACtical_AI: Saved RawTech.RAWTECH for " + TechName + " successfully.");
                }
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static string LoadTechFromFile(string TechName, string altFolderName)
        {
            string destination;
            if (altFolderName == "")
                destination = RawTechsDirectory;
            else
                destination = BaseDirectory + up + altFolderName;
            try
            {
                string output;
                if (File.Exists(destination + up + TechName + ".JSON"))
                {
                    output = File.ReadAllText(destination + up + TechName + ".JSON");
                    DebugTAC_AI.Info("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                }
                else
                {
                    output = File.ReadAllText(destination + up + TechName + ".RAWTECH");
                    DebugTAC_AI.Info("TACtical_AI: Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                }
                return output;
            }
            catch
            {
                DebugTAC_AI.LogError("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");

                DebugTAC_AI.LogDevOnly("TACtical_AI: Attempted directory - |" + destination + up + TechName + ".JSON");
                return null;
            }
        }
        private static void SaveEnemyTechToFile(string TechName, string RawBaseTechJSON)
        {
            string destination = RawTechsDirectory + up + "Enemies" + up + "eLocal";
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Info("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(RawTechsDirectory + up + "Enemies"))
            {
                DebugTAC_AI.Info("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory + up + "Enemies");
                    DebugTAC_AI.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Info("TACtical_AI: Generating eLocal folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log("TACtical_AI: Made new eLocal folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new eLocal folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(destination + up + TechName + ".JSON", RawBaseTechJSON);
                DebugTAC_AI.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static void SaveEnemyTechToFileBLK(string TechName, string RawBaseTechJSON)
        {
            string destination = RawTechsDirectory + up + "Enemies" + up + "eBulk";
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Log("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(RawTechsDirectory + up + "Enemies"))
            {
                DebugTAC_AI.Log("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory + up + "Enemies");
                    DebugTAC_AI.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Log("TACtical_AI: Generating eBulk folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log("TACtical_AI: Made new eBulk folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new eBulk folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(destination + up + TechName + ".JSON", RawBaseTechJSON);
                DebugTAC_AI.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static string LoadEnemyTechFromFile(string TechName, string AltDirectory = "")
        {
            string destination;
            if (AltDirectory == "")
            {
                destination = RawTechsDirectory + up + "Enemies";
                if (!Directory.Exists(RawTechsDirectory))
                {
                    DebugTAC_AI.Info("TACtical_AI: Generating Raw Techs folder.");
                    try
                    {
                        Directory.CreateDirectory(RawTechsDirectory);
                        DebugTAC_AI.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                        return null;
                    }

                }
                if (!Directory.Exists(destination))
                {
                    DebugTAC_AI.Info("TACtical_AI: Generating Enemies folder.");
                    try
                    {
                        Directory.CreateDirectory(destination);
                        DebugTAC_AI.Log("TACtical_AI: Made new Enemies folder successfully.");
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                        return null;
                    }

                }
                try
                {
                    string output;
                    if (File.Exists(destination + up + TechName + ".JSON"))
                    {
                        output = File.ReadAllText(destination + up + TechName + ".JSON");
                        DebugTAC_AI.Info("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                    }
                    else
                    {
                        output = File.ReadAllText(destination + up + TechName + ".RAWTECH");
                        DebugTAC_AI.Info("TACtical_AI: Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                    }
                    return output;
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }
            }
            else
            {
                destination = AltDirectory;
                try
                {
                    string output;
                    if (File.Exists(destination + up + TechName + ".JSON"))
                    {
                        output = File.ReadAllText(destination + up + TechName + ".JSON");
                        DebugTAC_AI.Info("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                    }
                    else
                    {
                        output = File.ReadAllText(destination + up + TechName + ".RAWTECH");
                        DebugTAC_AI.Info("TACtical_AI: Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                    }
                    return output;
                }
                catch { }
                DebugTAC_AI.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return null;
            }
        }
        private static string FindAndLoadEnemyTechFromFile(string TechName)
        {
            string destination;
            destination = RawTechsDirectory + up + "Enemies";
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Info("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Info("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }

            }
            try
            {
                List<string> Dirs = GetALLDirectoriesInFolder(RawTechsDirectory + up + "Enemies");
                List<RawTechTemplate> temps = new List<RawTechTemplate>();
                List<string> names;
                DebugTAC_AI.Info("TACtical_AI: LoadAllEnemyTechs - Total directories found in Enemies Folder: " + Dirs.Count());
                foreach (string Dir in Dirs)
                {
                    names = GetTechNameListDir(Dir);
                    if (names.Contains(TechName))
                    {
                        return LoadEnemyTechFromFile(TechName, Dir);
                    }
                }
                return null;
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return null;
            }

        }

        internal static Texture2D FetchTexture(string pngName)
        {
            Texture2D tex = null;
            try
            {
                ModContainer MC = ManMods.inst.FindMod("Advanced AI");
                //ResourcesHelper.LookIntoModContents(MC);
                if (MC != null)
                    tex = ResourcesHelper.GetTextureFromModAssetBundle(MC, pngName.Replace(".png", ""), false);
                else
                    DebugTAC_AI.Log("TACtical_AI: ModContainer for Advanced AI DOES NOT EXIST");
                if (!tex)
                {
                    DebugTAC_AI.Log("TACtical_AI: Icon " + pngName.Replace(".png", "") + " did not exist in AssetBundle, using external...");
                    string destination = DLLDirectory + up + "AI_Icons" + up + pngName;
                    tex = FileUtils.LoadTexture(destination);
                }
                if (tex)
                    return tex;
            }
            catch { }
            DebugTAC_AI.Log("TACtical_AI: Could not load Icon " + pngName + "!  \n   File is missing!");
            return null;
        }
        private static Sprite LoadSprite(string pngName, bool RemoveBlackOutline = false)
        {
            try
            {
                Sprite refS = referenceAIIcon;
                Texture2D texRef = FetchTexture(pngName);
                Sprite output;
                if (!RemoveBlackOutline)
                {
                    Texture2D tex = new Texture2D(texRef.width, texRef.height, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Bilinear;
                    tex.anisoLevel = refS.texture.anisoLevel;
                    tex.mipMapBias = 0;
                    tex.requestedMipmapLevel = 2;
                    tex.SetPixels(texRef.GetPixels());
                    tex.Apply(false);
                    output = Sprite.Create(tex, new Rect(0, 0, texRef.width, texRef.height), Vector2.zero, refS.pixelsPerUnit, 0, SpriteMeshType.FullRect, refS.border);
                }
                else
                    output = Sprite.Create(texRef, new Rect(0, 0, texRef.width, texRef.height), Vector2.zero, refS.pixelsPerUnit, 0, SpriteMeshType.FullRect, refS.border);
                DebugTAC_AI.Log("TACtical_AI: Loaded Icon " + pngName + " successfully.");
                return output;
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: Could not load Icon " + pngName + "!  \n   File is missing!");
                return null;
            }
        }
        internal static Material CreateMaterial(string pngName, Material prefab)
        {
            try
            {
                Material mat = new Material(prefab);
                mat.mainTexture = FetchTexture(pngName);
                DebugTAC_AI.Log("TACtical_AI: Loaded Icon " + pngName + " successfully.");
                return mat;
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: Could not load Icon " + pngName + "!  \n   Fail on material creation!");
                return null;
            }
        }
        private static string GetNameDirectory(string FolderDirectory)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == up.ToCharArray()[0])
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }

            return final.ToString();
        }
        private static string GetDirectoryPathOnly(string FolderDirectory)
        {
            StringBuilder final = new StringBuilder();
            int offsetRemove = 0;
            foreach (char ch in FolderDirectory)
            {
                if (ch == up.ToCharArray()[0])
                {
                    offsetRemove = 1;
                }
                else
                    offsetRemove++;
                final.Append(ch);
            }
            final.Remove(final.Length - offsetRemove, offsetRemove);
            return final.ToString();
        }
        private static List<string> GetALLDirectoriesInFolder(string directory)
        {   // 
            List<string> final = new List<string>();
            List<string> Dirs = Directory.GetDirectories(directory).ToList();
            final.Add(directory);
            foreach (string Dir in Dirs)
            {
                final.Add(Dir);
                final.AddRange(GetALLDirectoriesInFolder(Dir));
            }
            final = final.Distinct().ToList();
            return final;
        }
        private static bool IsLethal(Tank tank)
        {   // 
            return tank.blockman.IterateBlockComponents<ModuleWeapon>().Count() > tank.blockman.IterateBlockComponents<ModuleTechController>().Count();
        }
    }
}
