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
        public FactionSubTypes Faction;
        public bool NonAggressive = false;
        public bool Eradicator = false;
        public int Cost = 0;
    }

    internal class DEVTypeEnumConverter : StringEnumConverter
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
            else if (value is FactionSubTypes)
            {
                writer.WriteValue(Enum.GetName(typeof(FactionSubTypes), (FactionSubTypes)value));
                return;
            }

            base.WriteJson(writer, value, serializer);
        }
    }

    internal class RawTechExporter : MonoBehaviour
    {
        public static RawTechExporter inst;
        public static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        public static bool isOpen;
        public static bool pendingInGameReload;

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
        public static TankAIHelper lastTech;

        private static bool firstInit = false;

        // GUI
        private const int RawTechExporterID = 846321;
        internal static Sprite GuardAIIcon;

        static void AddHooksToActiveGameInterop()
        {
            ActiveGameInterop.OnRecieve.Add("RetreiveTechPop", (string x) =>
            {
                ActiveGameInterop.TryTransmit("RetreiveTechPop", Path.Combine(RawTechsDirectory, "RawTechs.RTList"));
            });
        }
        public static void Initiate()
        {
            if (inst)
                return;
            GuardAIIcon = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(delegate 
                (Sprite cand)
            { return cand.name == "Icon_AI_Guard"; });
#if STEAM
            // Steam does not support RawTech loading the same way as Unofficial.
            if (!firstInit)
            {
                SetupWorkingDirectoriesSteam();
                AddHooksToActiveGameInterop();
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
                DebugTAC_AI.Log(KickStart.ModID + ": FirstInit RawTechExporter");
                TankAIManager.toggleAuto = new ManToolbar.ToolbarToggle("Autopilot", aiIconsEnemy[EnemySmarts.Meh],
                    TankAIManager.TogglePlayerAutopilot);
                    //new AbilityToggle("Autopilot", aiIconsEnemy[EnemySmarts.IntAIligent], TankAIManager.TogglePlayerAutopilot, 0.2f);
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
            DebugTAC_AI.Log(KickStart.ModID + ": RawTechExporter - Unsubscribing from Pause Screen");
            isSubbed = false;
            Destroy(inst.gameObject);
            inst = null;
        }
       

        private static bool isSubbed = false;
        public void LateInitiate()
        {
            if (!inst || isSubbed)
                return;
            ManPauseGame.inst.PauseEvent.Subscribe(inst.UpdatePauseStatus);
            DebugTAC_AI.Log(KickStart.ModID + ": RawTechExporter - Subscribing to Pause Screen");
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
#if DEBUG
            CheckKeyCombosDEV();
#endif
#if STEAM
            LateInitiate();
#endif
            ReloadCheck();
        }
        public void CheckKeyCombosDEV()
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
            DebugTAC_AI.Log(KickStart.ModID + ": Rebuilding Raw Tech Loader!");
            BlockIndexer.ConstructBlockLookupList();
            ModTechsDatabase.ValidateAndAddAllExternalTechs(true);
        }
        public void ReloadRawTechLoader()
        {
            DebugTAC_AI.Log(KickStart.ModID + ": Rebuilding Raw Tech Loader!");
            BlockIndexer.ConstructBlockLookupList();
            ModTechsDatabase.ValidateAndAddAllExternalTechs(true);
        }
        public static void ReloadCheck()
        {
            if (pendingInGameReload)
            {
                DebugTAC_AI.Info(KickStart.ModID + ": Reloading All Raw Enemy Techs (Ingame)!");
                ReloadTechsNow();
            }
        }
        public static void ReloadTechsNow()
        {
            DebugTAC_AI.Log(KickStart.ModID + ": Reloading All Raw Enemy Techs!");
            pendingInGameReload = false;
            ModTechsDatabase.ValidateAndAddAllExternalTechs();
        }
        

        internal class GUIRawDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isOpen)
                {
                    HotWindow = AltUI.Window(RawTechExporterID, HotWindow, GUIHandler, "Enemy Spawns");
                }
            }
        }
        private static void GUIHandler(int ID)
        {
            //bool snapsAvail = SnapsAvailable();
            bool snapsAvail = true;
            GUI.tooltip = "Make your own techs spawn as enemies!";
            //"The techs are saved to your Raw Techs directory";
            /*
            if (GUILayout.Button(new GUIContent("Save Current", "Export your current Tech to the lightweight RawTech format."), AltUI.ButtonBlue))
            {
                SaveTechToRawJSON(Singleton.playerTank);
            }
            */
            if (GUILayout.Button(new GUIContent("To Enemy", "Save your current Tech to the Enemy Spawn Pool"), 
                AltUI.ButtonBlueLarge, GUILayout.Height(40)))
            {
                SaveEnemyTechToRawJSON(Singleton.playerTank);
                inst.ReloadRawTechLoader();
            }
            if (GUILayout.Button(new GUIContent("All To Enemy", snapsAvail ? "Save ALL your local Techs to the Enemy Spawn Pool." : "Open your Load Techs at least once!"), 
                snapsAvail ? AltUI.ButtonBlueLarge : AltUI.ButtonGreyLarge, GUILayout.Height(40)))
            {
                SaveEnemyTechsToRawBLK();
                inst.ReloadRawTechLoader();
            }
            if (GUILayout.Button(new GUIContent("Global Pool", "Add your Tech or suggest changes to the global Enemy Tech pool!"), 
                AltUI.ButtonOrangeLarge, GUILayout.Height(40)))
            {
                ManSteamworks.inst.OpenOverlayURL("https://steamcommunity.com/workshop/filedetails/discussion/2765217410/3790379982475551622/");
            }
            GUILayout.Label(GUI.tooltip, AltUI.TextfieldBlackHuge, GUILayout.Height(75));
            GUI.DragWindow();
        }

        public static void LaunchSubMenu()
        {
            if (Singleton.playerTank.IsNull() || !KickStart.EnableBetterAI)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": TANK IS NULL!");
                CloseSubMenu();
                return;
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": Opened RawJSON menu");
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
                //DebugTAC_AI.Log(KickStart.ModID + ": Closed RawJSON menu");
            }
        }

        private static FileSystemWatcher watchDog;
        // Setup
        public static void SetupWorkingDirectories()
        {
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            di = di.Parent; // off of this DLL
            DLLDirectory = di.ToString();
            di = di.Parent; // out of the DLL folder
            di = di.Parent; // out of QMods
            BaseDirectory = di.ToString();
#if DEBUG
            DebugTAC_AI.Info(KickStart.ModID + ": DLL folder is at: " + DLLDirectory);
            DebugTAC_AI.Info(KickStart.ModID + ": Raw Techs is at: " + RawTechsDirectory);
            RawTechsDirectory = Path.Combine(di.ToString(), "Raw Techs Community");
#else
            RawTechsDirectory = Path.Combine(di.ToString(), "Raw Techs");
#endif
            ValidateEnemyFolder();
            watchDog = new FileSystemWatcher(RawTechsDirectory);
            watchDog.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName;
            watchDog.EnableRaisingEvents = true;
            watchDog.Created += RefreshAll;
            watchDog.Deleted += RefreshAll;
            watchDog.Changed += RefreshAll;
        }
        public static void RefreshAll(object sender, FileSystemEventArgs e)
        {
            pendingInGameReload = true;
        }
        public static void SetupWorkingDirectoriesSteam()
        {
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            di = di.Parent; // off of this DLL
            DLLDirectory = di.ToString();

            DirectoryInfo Navi = new DirectoryInfo(Application.dataPath);
            Navi = Navi.Parent; // out of the GAME folder
            BaseDirectory = Navi.ToString();
#if DEBUG
            DebugTAC_AI.Info(KickStart.ModID + ": DLL folder is at: " + DLLDirectory);
            DebugTAC_AI.Info(KickStart.ModID + ": Raw Techs is at: " + RawTechsDirectory);
            RawTechsDirectory = Path.Combine(Navi.ToString(), "Raw Techs Community");
#else
            RawTechsDirectory = Path.Combine(Navi.ToString(), "Raw Techs");
#endif
            ValidateEnemyFolder();
            watchDog = new FileSystemWatcher(RawTechsDirectory);
            watchDog.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName;
            watchDog.EnableRaisingEvents = true;
            watchDog.Created += RefreshAll;
            watchDog.Deleted += RefreshAll;
            watchDog.Changed += RefreshAll;
        }


        // Operations
        internal static int GetRawTechsCountExternalMods()
        {
#if STEAM
            int count = 0;
            string location = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent.ToString();// Go to the cluster directory

            DebugTAC_AI.LogDevOnly(KickStart.ModID + ": RegisterExternalCorpTechs - searching in " + location);
            string fileName = "RawTechs.RTList";
            foreach (string directoryLoc in Directory.GetDirectories(location))
            {
                try
                {
                    string GO;
                    GO = Path.Combine(directoryLoc, fileName);
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

        internal static void MakeExternalRawTechListFile(string fileNameAndPath, List<RawTech> content)
        {
            List<RawTechTemplate> templates = new List<RawTechTemplate>();
            foreach (var item in content)
            {
                templates.Add(item.ToTemplate());
            }
            string toWrite = JsonConvert.SerializeObject(templates, Formatting.None);
            SaveExternalRawTechListFileToDisk(fileNameAndPath, toWrite);
        }
        internal static void SaveExternalRawTechListFileToDisk(string fileNameAndPath, string toWrite)
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

        internal static string LoadCommunityDeployedTechs(Stream streamData)
        {
            string toAdd = "";
            //DebugTAC_AI.Log(KickStart.ModID + ": RegisterExternalCorpTechs - searching in " + location);
            try
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": RegisterExternalCorpTechs - looked in " + GO);
                using (GZipStream GZS = new GZipStream(streamData, CompressionMode.Decompress))
                {
                    using (StreamReader SR = new StreamReader(GZS))
                    {
                        toAdd = SR.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            { DebugTAC_AI.Log(KickStart.ModID + ": LoadCommunityDeployedTechs - ERROR " + e);}
            if (!toAdd.NullOrEmpty())
                DebugTAC_AI.Log(KickStart.ModID + ": LoadCommunityDeployedTechs - Got Techs");
            else
                DebugTAC_AI.Log(KickStart.ModID + ": LoadCommunityDeployedTechs - No bundled techs found");
            return toAdd;
        }

        internal static List<RawTechTemplate> LoadAllEnemyTechsExternalMods()
        {
#if STEAM
            List<RawTechTemplate> toAdd = new List<RawTechTemplate>();
            string location = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent.ToString();// Go to the cluster directory

            //DebugTAC_AI.Log(KickStart.ModID + ": RegisterExternalCorpTechs - searching in " + location);

            string fileName = "RawTechs.RTList"; 
            foreach (string directoryLoc in Directory.GetDirectories(location))
            {
                try
                {
                    string GO = Path.Combine(directoryLoc, fileName);
                    //DebugTAC_AI.Log(KickStart.ModID + ": RegisterExternalCorpTechs - looked in " + GO);
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
                catch (Exception e) { DebugTAC_AI.Log(KickStart.ModID + ": LoadAllEnemyTechsExternalMods - ERROR " + e); }
            }
            if (toAdd.Count > 0)
                DebugTAC_AI.Log(KickStart.ModID + ": LoadAllEnemyTechsExternalMods - Added " + toAdd.Count + " techs from mods");
            else
                DebugTAC_AI.Log(KickStart.ModID + ": LoadAllEnemyTechsExternalMods - No bundled techs found");
            return toAdd;
#else
            return new List<BaseTemplate>();
#endif
        }
        public static List<RawTechTemplate> LoadAllEnemyTechs()
        {
            ValidateEnemyFolder();
            List<string> Dirs = GetALLDirectoriesInFolder(Path.Combine(RawTechsDirectory, "Enemies"));
            List<RawTechTemplate> temps = new List<RawTechTemplate>();
            List<string> names;
            DebugTAC_AI.Log(KickStart.ModID + ": LoadAllEnemyTechs - Total directories found in Enemies Folder: " + Dirs.Count());
            foreach (string Dir in Dirs)
            {
                names = GetTechNameListDir(Dir);
                DebugTAC_AI.Log(KickStart.ModID + ": LoadAllEnemyTechs - Total RAW Techs found in " + GetNameDirectory(Dir) + ": " + names.Count());
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
                        FactionSubTypes MainCorp;
                        if (ext.Faction == FactionSubTypes.NULL)
                        {
                            MainCorp = KickStart.GetCorpExtended(RawTechTemplate.JSONToFirstBlock(ext.Blueprint));
                        }
                        else
                            MainCorp = ext.Faction;
                        errorLevel++; // 3
                        temp.purposes = RawTechTemplate.GetHandler(ext.Blueprint, (FactionTypesExt)MainCorp, ext.IsAnchored, out BaseTerrain terra, out int minCorpGrade);
                        temp.IntendedGrade = minCorpGrade;
                        temp.faction = (FactionTypesExt)MainCorp;
                        temp.terrain = terra;

                        temps.Add(temp);
                        DebugTAC_AI.Info(KickStart.ModID + ": Added " + name + " to the RAW Enemy Tech Pool, grade " + minCorpGrade + " " + MainCorp.ToString() + ", of BB Cost " + temp.startingFunds + ".");
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Could not add " + name + " to the RAW Enemy Tech Pool!  Corrupted BuilderExternal(Or tech too small)!! - Error Level " + errorLevel + " ERROR " + e);
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
                search = Path.Combine(RawTechsDirectory, "Enemies");
            }
            else
            {
                search = Path.Combine(BaseDirectory, altDirectoryFromBaseDirectory);
            }
            List<string> toClean = Directory.GetFiles(search).ToList();
            Cleaned = CleanNames(toClean, false);
            return Cleaned;
        }
        internal static int GetTechCounts()
        {
            List<string> Dirs = GetALLDirectoriesInFolder(Path.Combine(RawTechsDirectory, "Enemies"));
            int techCount = 0;
            foreach (string Dir in Dirs)
            {
                techCount += GetTechNameListDir(Dir).Count;
            }
            return techCount;
        }

        private static StringBuilder SB = new StringBuilder();
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
            try
            {
                foreach (char ch in FolderDirectory)
                {
                    if (ch == Path.DirectorySeparatorChar)
                    {
                        SB.Clear();
                    }
                    else
                        SB.Append(ch);
                }
                if (!SB.ToString().Contains(".RAWTECH"))
                {
                    if (!SB.ToString().Contains(".JSON") && !excludeJSON)
                    {
                        output = null;
                        return false;
                    }
                    else
                        SB.Remove(SB.Length - 5, 5);// remove ".JSON"
                }
                else
                    SB.Remove(SB.Length - 8, 8);// remove ".RAWTECH"

                output = SB.ToString();
            }
            finally { SB.Clear(); }
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
                DebugTAC_AI.Log(KickStart.ModID + ": ValidateCost - Invalid tech cost encountered ~ could not handle!");
                ExistingCost = 0;
            }

            return ExistingCost;
        }

        internal static void ValidateEnemyFolder()
        {
            string destination = Path.Combine(RawTechsDirectory, "Enemies");
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
        }

        // Snapshot to RawTech
        public static bool SnapsAvailable()
        {
            try
            {
                List<SnapshotLiveData> Snaps = ManSnapshots.inst.m_Snapshots.ToList();
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
            List<SnapshotDisk> Snaps = ManSnapshots.inst.ServiceDisk.GetSnapshotCollectionDisk().Snapshots.ToList();// ManSnapshots.inst.m_Snapshots.ToList();
            if (Snaps.Count == 0)
                return;
            foreach (SnapshotDisk snap in Snaps)
            {
                try
                {
                    SaveEnemyTechToRawJSONBLK(snap.techData);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": SaveEnemyTechsToRawBLK - Export Failure reported!");
                    try
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Error tech name " + snap.techData.Name);
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": SaveEnemyTechsToRawBLK - COULD NOT FETCH TECHDATA!!!");
                    }
                }
            }
            ReloadTechsNow();
        }


        // JSON Handlers
        public static void SaveTechToRawJSON(Tank tank)
        {
            RawTechTemplateFast builder = new RawTechTemplateFast
            {
                Name = tank.name,
                Faction = TankExtentions.GetMainCorp(tank),//GetTopCorp(tank);
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
                Faction = TankExtentions.GetMainCorp(tank),
                Blueprint = RawTechTemplate.TechToJSONExternal(tank),
                InfBlocks = false,
                IsAnchored = tank.IsAnchored,
                NonAggressive = !IsLethal(tank),
                Eradicator = tank.blockman.blockCount >= AIGlobals.LethalTechSize || tank.blockman.IterateBlockComponents<ModuleWeaponGun>().Count() > 48 || tank.blockman.IterateBlockComponents<ModuleHover>().Count() > 18,
                Cost = RawTechTemplate.GetBBCost(tank)
            };
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveEnemyTechToFile(tank.name, builderJSON);
            ReloadTechsNow();
        }
        public static void SaveEnemyTechToRawJSONBLK(TechData tank)
        {
            RawTechTemplateFast builder = new RawTechTemplateFast();
            string bluep = RawTechTemplate.BlockSpecToJSONExternal(tank.m_BlockSpecs, out int blockCount, out bool lethal, out int hoveCount, out int weapGCount);
            builder.Name = tank.Name;
            builder.Faction = tank.GetMainCorp();
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

        public static void PurgeAllRawTechs()
        {
            ValidateEnemyFolder();
            List<string> Dirs = GetALLDirectoriesInFolder(Path.Combine(RawTechsDirectory, "Enemies"));
            List<string> names;
            DebugTAC_AI.Info(KickStart.ModID + ": PurgeAllRawTechs - Total directories found in Enemies Folder: " + Dirs.Count());
            foreach (string Dir in Dirs)
            {
                names = GetTechNameListDir(Dir);
                DebugTAC_AI.Info(KickStart.ModID + ": PurgeAllRawTechs - Total RAW Techs found in " + GetNameDirectory(Dir) + ": " + names.Count());
                foreach (string name in names)
                {
                    try
                    {
                        string nameActual = Path.GetFileName(name);
                        if (nameActual.EndsWith(".json") || nameActual.EndsWith(".RAWTECH"))
                        {
                            File.Delete(Dir);
                            DebugTAC_AI.Log("Deleted RawTech file at - " + Dir);
                        }
                    }
                    catch { }
                }
            }
            ModTechsDatabase.ExtPopTechsMods.Clear();
            pendingInGameReload = true;
        }


        // Loaders
        private static void SaveTechToFile(string TechName, string RawTechJSON)
        {
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                if (ExportJSONInsteadOfRAWTECH)
                {
                    File.WriteAllText(Path.Combine(RawTechsDirectory, TechName + ".JSON"), RawTechJSON);
                    DebugTAC_AI.Log(KickStart.ModID + ": Saved RawTech.JSON for " + TechName + " successfully.");
                }
                else
                {
                    File.WriteAllText(Path.Combine(RawTechsDirectory, TechName +".RAWTECH"), RawTechJSON);
                    DebugTAC_AI.Log(KickStart.ModID + ": Saved RawTech.RAWTECH for " + TechName + " successfully.");
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static string LoadTechFromFile(string TechName, string altFolderName)
        {
            string destination;
            if (altFolderName == "")
                destination = RawTechsDirectory;
            else
                destination = Path.Combine(BaseDirectory, altFolderName);
            try
            {
                string output;
                if (File.Exists(Path.Combine(destination, TechName + ".JSON")))
                {
                    output = File.ReadAllText(Path.Combine(destination, TechName + ".JSON"));
                    DebugTAC_AI.Info(KickStart.ModID + ": Loaded RawTech.JSON for " + TechName + " successfully.");
                }
                else
                {
                    output = File.ReadAllText(Path.Combine(destination, TechName + ".RAWTECH"));
                    DebugTAC_AI.Info(KickStart.ModID + ": Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                }
                return output;
            }
            catch
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");

                DebugTAC_AI.LogDevOnly(KickStart.ModID + ": Attempted directory - |" + Path.Combine(destination, TechName + ".JSON"));
                return null;
            }
        }
        private static void SaveEnemyTechToFile(string TechName, string RawBaseTechJSON)
        {
            string destination = Path.Combine(RawTechsDirectory, "Enemies", "eLocal");
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Info(KickStart.ModID + ": Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(Path.Combine(RawTechsDirectory, "Enemies")))
            {
                DebugTAC_AI.Info(KickStart.ModID + ": Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(Path.Combine(RawTechsDirectory, "Enemies"));
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Info(KickStart.ModID + ": Generating eLocal folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new eLocal folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new eLocal folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(Path.Combine(destination, TechName + ".JSON"), RawBaseTechJSON);
                DebugTAC_AI.Log(KickStart.ModID + ": Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static void SaveEnemyTechToFileBLK(string TechName, string RawBaseTechJSON)
        {
            string destination = Path.Combine(RawTechsDirectory, "Enemies", "eBulk");
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(Path.Combine(RawTechsDirectory, "Enemies")))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(Path.Combine(RawTechsDirectory, "Enemies"));
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Generating eBulk folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new eBulk folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new eBulk folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(Path.Combine(destination, TechName + ".JSON"), RawBaseTechJSON);
                DebugTAC_AI.Log(KickStart.ModID + ": Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static string LoadEnemyTechFromFile(string TechName, string AltDirectory = "")
        {
            string destination;
            if (AltDirectory == "")
            {
                destination = Path.Combine(RawTechsDirectory, "Enemies");
                if (!Directory.Exists(RawTechsDirectory))
                {
                    DebugTAC_AI.Info(KickStart.ModID + ": Generating Raw Techs folder.");
                    try
                    {
                        Directory.CreateDirectory(RawTechsDirectory);
                        DebugTAC_AI.Log(KickStart.ModID + ": Made new Raw Techs folder successfully.");
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                        return null;
                    }

                }
                if (!Directory.Exists(destination))
                {
                    DebugTAC_AI.Info(KickStart.ModID + ": Generating Enemies folder.");
                    try
                    {
                        Directory.CreateDirectory(destination);
                        DebugTAC_AI.Log(KickStart.ModID + ": Made new Enemies folder successfully.");
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                        return null;
                    }

                }
                try
                {
                    string output;
                    if (File.Exists(Path.Combine(destination, TechName + ".JSON")))
                    {
                        output = File.ReadAllText(Path.Combine(destination, TechName + ".JSON"));
                        DebugTAC_AI.Info(KickStart.ModID + ": Loaded RawTech.JSON for " + TechName + " successfully.");
                    }
                    else
                    {
                        output = File.ReadAllText(Path.Combine(destination, TechName + ".RAWTECH"));
                        DebugTAC_AI.Info(KickStart.ModID + ": Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                    }
                    return output;
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }
            }
            else
            {
                destination = AltDirectory;
                try
                {
                    string output;
                    if (File.Exists(Path.Combine(destination, TechName + ".JSON")))
                    {
                        output = File.ReadAllText(Path.Combine(destination, TechName + ".JSON"));
                        DebugTAC_AI.Info(KickStart.ModID + ": Loaded RawTech.JSON for " + TechName + " successfully.");
                    }
                    else
                    {
                        output = File.ReadAllText(Path.Combine(destination, TechName + ".RAWTECH"));
                        DebugTAC_AI.Info(KickStart.ModID + ": Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                    }
                    return output;
                }
                catch { }
                DebugTAC_AI.Log(KickStart.ModID + ": Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return null;
            }
        }
        private static string FindAndLoadEnemyTechFromFile(string TechName)
        {
            string destination;
            destination = Path.Combine(RawTechsDirectory, "Enemies");
            if (!Directory.Exists(RawTechsDirectory))
            {
                DebugTAC_AI.Info(KickStart.ModID + ": Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Raw Techs folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }

            }
            if (!Directory.Exists(destination))
            {
                DebugTAC_AI.Info(KickStart.ModID + ": Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    DebugTAC_AI.Log(KickStart.ModID + ": Made new Enemies folder successfully.");
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }

            }
            try
            {
                List<string> Dirs = GetALLDirectoriesInFolder(Path.Combine(RawTechsDirectory, "Enemies"));
                List<RawTechTemplate> temps = new List<RawTechTemplate>();
                List<string> names;
                DebugTAC_AI.Info(KickStart.ModID + ": LoadAllEnemyTechs - Total directories found in Enemies Folder: " + Dirs.Count());
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
                DebugTAC_AI.Log(KickStart.ModID + ": Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return null;
            }

        }

        internal static Texture2D FetchTexture(string pngName)
        {
            Texture2D tex = null;
            try
            {
                ModContainer MC = ManMods.inst.FindMod(KickStart.ModID);
                //ResourcesHelper.LookIntoModContents(MC);
                if (MC != null)
                    tex = ResourcesHelper.GetTextureFromModAssetBundle(MC, pngName.Replace(".png", ""), false);
                else
                    DebugTAC_AI.Log(KickStart.ModID + ": ModContainer for Advanced AI DOES NOT EXIST");
                if (!tex)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Icon " + pngName.Replace(".png", "") + " did not exist in AssetBundle, using external...");
                    string destination = Path.Combine(DLLDirectory, "AI_Icons", pngName);
                    tex = FileUtils.LoadTexture(destination);
                }
                if (tex)
                    return tex;
            }
            catch { }
            DebugTAC_AI.Log(KickStart.ModID + ": Could not load Icon " + pngName + "!  \n   File is missing!");
            return null;
        }
        private static Sprite LoadSprite(string pngName, bool RemoveBlackOutline = false)
        {
            try
            {
                Sprite refS = GuardAIIcon;
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
                DebugTAC_AI.Log(KickStart.ModID + ": Loaded Icon " + pngName + " successfully.");
                return output;
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Could not load Icon " + pngName + "!  \n   File is missing!");
                return null;
            }
        }
        internal static Material CreateMaterial(string pngName, Material prefab)
        {
            try
            {
                Material mat = new Material(prefab);
                mat.mainTexture = FetchTexture(pngName);
                DebugTAC_AI.Log(KickStart.ModID + ": Loaded Icon " + pngName + " successfully.");
                return mat;
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Could not load Icon " + pngName + "!  \n   Fail on material creation!");
                return null;
            }
        }
        private static string GetNameDirectory(string FolderDirectory)
        {
            try
            {
                foreach (char ch in FolderDirectory)
                {
                    if (ch == Path.DirectorySeparatorChar)
                    {
                        SB.Clear();
                    }
                    else
                        SB.Append(ch);
                }

                return SB.ToString();
            }
            finally { SB.Clear(); }
        }
        private static string GetDirectoryPathOnly(string FolderDirectory)
        {
            try
            {
                int offsetRemove = 0;
                foreach (char ch in FolderDirectory)
                {
                    if (ch == Path.DirectorySeparatorChar)
                    {
                        offsetRemove = 1;
                    }
                    else
                        offsetRemove++;
                    SB.Append(ch);
                }
                SB.Remove(SB.Length - offsetRemove, offsetRemove);
                return SB.ToString();
            }
            finally { SB.Clear(); }
        }
        private static List<string> GetALLDirectoriesInFolder(string directory)
        {   // 
            List<string> final = new List<string>();
            final.Add(directory);
            foreach (string Dir in Directory.GetDirectories(directory))
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
