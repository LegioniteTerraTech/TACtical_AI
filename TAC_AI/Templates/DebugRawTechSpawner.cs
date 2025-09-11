using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DevCommands;
using Newtonsoft.Json;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.AI.Movement;
using TAC_AI.World;
using TerraTechETCUtil;
using UnityEngine;


namespace TAC_AI.Templates
{
    internal enum DebugMenus
    {
        Prefabs,
        Local,
        DebugLog,
        RawTechsFolders,
    }
    internal class DebugRawTechSpawner : MonoBehaviour
    {
        private static readonly bool Enabled = true;

        internal static bool CanOpenDebugSpawnMenu
        {
            get 
            { 
                if (inst)
                    return inst.enabled;
                return false;
            }
            set
            {
                if (inst)
                {
                    if (!value)
                    {
                        CloseSubMenuClickable();
                        //if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Plus))
                        //    CheckAndDestroyAllInvalidVisibles();
                    }
                    inst.enabled = value;
                }
            }
        }
        private static DebugMenus menu = DebugMenus.Prefabs;

        private static Vector3 PlayerLoc = Vector3.zero;
        private static Vector3 PlayerFow = Vector3.forward;
        private static bool UIIsCurrentlyOpen => GUIWindow.activeSelf;
        internal static bool CanCommandOtherTeams => AllowPlayerCommandEnemies && CanOpenDebugSpawnMenu;
        private static bool toggleDebugLock = false;
        private static bool InstantLoad = false;
        internal static bool AINoAttackPlayer = false;
        internal static bool AllowPlayerBuildEnemies = false;
        internal static bool AllowPlayerCommandEnemies = false;
        private static bool ShowLocal = true;

        private static GameObject GUIWindow;
        private static DebugRawTechSpawner inst;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        private const int RawTechSpawnerID = 8002;

        private const string redStart = "<color=#ffcccbff><b>";//"<color=#f23d3dff><b>";
        private static List<Tank> techCache = new List<Tank>();

        public static void Initiate()
        {
            if (!Enabled)
                return;

            #if DEBUG
                DebugTAC_AI.Log(KickStart.ModID + ": Raw Techs Debugger launched (DEV)");
            #else
                DebugTAC_AI.Log(KickStart.ModID + ": Raw Techs Debugger launched");
#endif

            inst = Instantiate(new GameObject()).AddComponent<DebugRawTechSpawner>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplayTechLoader>();
            GUIWindow.SetActive(false);
        }
        public static void ShouldBeActive()
        {
            CanOpenDebugSpawnMenu = CheckValidMode();
            if (!CanOpenDebugSpawnMenu)
            {
                AINoAttackPlayer = false;
                AllowPlayerBuildEnemies = false;
                AllowPlayerCommandEnemies = false;
            }
        }


        internal class GUIDisplayTechLoader : MonoBehaviour
        {
            internal void OnGUI()
            {
                if (UIIsCurrentlyOpen && KickStart.CanUseMenu)
                {
                    Action CloseCallback;
                    if (toggleDebugLock)
                        CloseCallback = ForceCloseSubMenuClickable;
                    else
                        CloseCallback = null;
                    switch (menu)
                    {
                        case DebugMenus.Prefabs:
                            HotWindow = AltUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerPreset, 
                                "RawTech Prefab Spawns", CloseCallback);
                            break;
                        case DebugMenus.Local:
                            HotWindow = AltUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerPlayer,
                                "RawTech Local Spawns", CloseCallback);
                            break;
                        case DebugMenus.RawTechsFolders:
                            HotWindow = AltUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerFolderSelect,
                                "RawTech Folders", CloseCallback);
                            break;
                        default:
                            HotWindow = AltUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerDebug, 
                                "Advanced AI Mod Info", CloseCallback);
                            break;
                    }
                }
            }
        }

        private static Vector2 scrolll = new Vector2(0, 0);
        private static float scrolllSize = 50;
        private const int ButtonWidth = 200;
        private const int MaxCountWidth = 4;
        private const int MaxWindowHeight = 500;
        private static readonly int MaxWindowWidth = MaxCountWidth * ButtonWidth;
        private static List<FactionSubTypes> openedFactions = new List<FactionSubTypes>();

        private static bool clicked = false;
        private static int VertPosOff = 0;
        private static int HoriPosOff = 0;
        private static bool MaxExtensionX = false;
        private static bool MaxExtensionY = false;
        private static SpawnBaseTypes type = SpawnBaseTypes.NotAvail;
        private static int index = 0;

        private static void ResetMenuPlacer()
        {
            clicked = false;
            VertPosOff = 0;
            HoriPosOff = 0;
            MaxExtensionX = false;
            MaxExtensionY = false;
            type = SpawnBaseTypes.NotAvail;
            index = 0;
        }
        private static void StepMenuPlacer()
        {
            HoriPosOff += ButtonWidth;
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }
        }
        private static void StepMenuPlacerPartial()
        {
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }
        }

        private static void GUIHandlerDebug(int ID)
        {
            HotWindow.height = MaxWindowHeight + 80;
            HotWindow.width = MaxWindowWidth + 60;
            scrolll = GUILayout.BeginScrollView(scrolll);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(HotWindow.width / 2));
            TankAIManager.GUIManaged.GUIGetTotalManaged();
            SpecialAISpawner.GUIManaged.GUIGetTotalManaged();
            AIEPathMapper.GUIManaged.GUIGetTotalManaged();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            ManWorldRTS.GUIGetTotalManaged();
            ManEnemyWorld.GUIManaged.GUIGetTotalManaged();
            ManBaseTeams.GUIManaged.GUIGetTotalManaged();
            ManEnemySiege.GUIGetTotalManaged();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
        internal static void OpenInExplorer(string directory)
        {
            switch (SystemInfo.operatingSystemFamily)
            {
                case OperatingSystemFamily.MacOSX:
                    Process.Start(new ProcessStartInfo("file://" + directory));
                    break;
                case OperatingSystemFamily.Linux:
                case OperatingSystemFamily.Windows:
                    Process.Start(new ProcessStartInfo("explorer.exe", directory));
                    break;
                default:
                    throw new Exception("This operating system is UNSUPPORTED by RandomAdditions");
            }
        }
        internal static string GetBaseTypeGenerated(string name)
        {
            return name.Replace(" ", "");
        }
        private static void GUIHandlerPlayer(int ID)
        {
            ResetMenuPlacer();

            List<RawTech> listTemp;
            if (ShowLocal)
                listTemp = ModTechsDatabase.ExtPopTechsLocal;
            else
                listTemp = ModTechsDatabase.ExtPopTechsMods;

            //scrolll = GUI.BeginScrollView(new Rect(0, 64, HotWindow.width - 20, HotWindow.height - 64), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            scrolll = GUILayout.BeginScrollView(scrolll);

            GUILayout.BeginHorizontal();
            RemoveAllEnemiesImmedeatelyButton();

            HoriPosOff += ButtonWidth;

            if (GUILayout.Button(redStart + "Sort Entire List</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                try
                {
                    if (ShowLocal)
                    {
                        Organize(ref ModTechsDatabase.ExtPopTechsLocal);
                        listTemp = ModTechsDatabase.ExtPopTechsLocal;
                    }
                    else
                    {
                        Organize(ref ModTechsDatabase.ExtPopTechsMods);
                        listTemp = ModTechsDatabase.ExtPopTechsMods;
                    }
                }
                catch { }
            }


            HoriPosOff += ButtonWidth;

            if (GUILayout.Button(InstantLoad ? redStart + "Instant ON</b></color>" : redStart + "Instant Off</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                InstantLoad = !InstantLoad;
            }

            HoriPosOff += ButtonWidth;

            if (GUILayout.Button(ShowDebugFeedBack ? redStart + "Hide AI Debug</b></color>" : redStart + "Show AI Debug</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                ShowDebugFeedBack = !ShowDebugFeedBack;
            }

            StepMenuPlacer();

            if (GUILayout.Button(ShowLocal ? redStart + "Showing Local</b></color>" : redStart + "Showing Mods</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                ShowLocal = !ShowLocal;
                return;
            }

            HoriPosOff += ButtonWidth;

            if (GUILayout.Button(redStart + "Correct Forwards</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                if (Singleton.playerTank)
                    AIERepair.DesignMemory.RebuildTechForwards(Singleton.playerTank);
                return;
            }

            HoriPosOff += ButtonWidth;

            if (ShowLocal)
            {
                if (GUILayout.Button(redStart + "Bundle ALL for Mod</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
                {
                    try
                    {
                        OrganizeDeploy(ref listTemp);

                        RawTechExporter.ValidateEnemyFolder();
                        string export = Path.Combine(RawTechExporter.RawTechsDirectory, "Bundled");
                        Directory.CreateDirectory(export);

                        string rtListDir = Path.Combine(export, "RawTechs.RTList");
                        RawTechExporter.MakeExternalRawTechListFile(rtListDir, listTemp);

                        OpenInExplorer(rtListDir);
                    }
                    catch { }
                }
                AltUI.Tooltip.GUITooltip("Exports it as a RawTechs.RTList file to attach to your mod as a batch of spawnable Techs.");
                
                StepMenuPlacer();
                if (GUILayout.Button(redStart + "Bundle Player Techs</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
                {
                    try
                    {
                        List<RawTech> temps = new List<RawTech>();
                        HashSet<string> names = new HashSet<string>();
                        foreach (var item in ManTechs.inst.IteratePlayerTechs())
                        {
                            if (!item.PlayerFocused && item.name != null && !names.Contains(item.name))
                            {
                                names.Add(item.name);
                                var TD = new TechData();
                                TD.SaveTech(item, false, false);
                                temps.Add(new RawTech(TD));
                            }
                        }
                        OrganizeDeploy(ref temps);

                        RawTechExporter.ValidateEnemyFolder();
                        string export = Path.Combine(RawTechExporter.RawTechsDirectory, "Bundled");
                        Directory.CreateDirectory(export);

                        string rtListDir = Path.Combine(export, "RawTechs.RTList");
                        RawTechExporter.MakeExternalRawTechListFile(rtListDir, listTemp);
                        OpenInExplorer(rtListDir);
                    }
                    catch { }
                }
                AltUI.Tooltip.GUITooltip("ALL active player Techs in the world for your Mod.  This might be laggy!" +
                           "  Exports it as a RawTechs.RTList file to attach to your mod as a batch of spawnable Techs.");

                StepMenuPlacer();
                if (GUILayout.Button(redStart + "Bundle Folder</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
                {
                    InvokeHelper.Invoke(() =>
                    {
                        menu = DebugMenus.RawTechsFolders;
                        UpdateFolders();
                    }, 0);
                }
                StepMenuPlacer();
                if (ActiveGameInterop.inst && ActiveGameInterop.IsReady && 
                    GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth * 2, 30), redStart + "Push To Editor</b></color>"))
                    ActiveGameInterop.TryTransmit("RetreiveTechPop", Path.Combine(RawTechExporter.RawTechsDirectory,
                        "Bundled", "RawTechs.RTList"));
                StepMenuPlacer();
            }
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }

            if (GUILayout.Button(redStart + "CLEAR TRACKED</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                CheckAndDestroyAllInvalidVisibles(true);
            }
            StepMenuPlacer();

#if DEBUG
            if (GUILayout.Button(redStart + "Remove Present</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                foreach (var item in CommunityStorage.CommunityStored)
                    listTemp.Remove(item.Value);
                foreach (var item in TempStorage.techBasesPrefab)
                    listTemp.Remove(item.Value);
                ManHUD.inst.InitialiseHudElement(ManHUD.HUDElementType.TechLoader);
                ManHUD.inst.ShowHudElement(ManHUD.HUDElementType.TechLoader);
                InvokeHelper.InvokeSingle(ClearDupeTechs, 1);
            }
            StepMenuPlacer();

            if (GUILayout.Button(redStart + "LOCAL TO COM</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                try
                {
                    OrganizeDeploy(ref listTemp);

                    string export = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
                    Directory.CreateDirectory(export);

                    List<string> toWrite = new List<string>();

                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("--------------- <<< MASS EXPORTING >>> --------------------");
                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("");

                    HashSet<string> basetypeNames = new HashSet<string>();
                    List<string> basetypeNamesOrdered = new List<string>();
                    StringBuilder SB = new StringBuilder();
                    foreach (RawTech RT in listTemp.OrderBy(x => x.techName))
                    {
                        RawTechTemplate BT = RT.ToTemplate();
                        string nameBaseType = GetBaseTypeGenerated(BT.techName);
                        if (!nameBaseType.Contains('#') && basetypeNames.Add(nameBaseType))
                        {
                            basetypeNamesOrdered.Add(nameBaseType);
                            toWrite.Add("{ SpawnBaseTypes." + nameBaseType + ", new BaseTemplate {");
                            toWrite.Add("    techName = \"" + BT.techName + "\",");
                            toWrite.Add("    faction = FactionSubTypes." + BT.faction.ToString() + ",");
                            toWrite.Add("    IntendedGrade = " + BT.IntendedGrade + ",");
                            toWrite.Add("    terrain = BaseTerrain." + BT.terrain.ToString() + ",");
                            SB.Clear();
                            foreach (BasePurpose BP in BT.purposes)
                                SB.Append("BasePurpose." + BP.ToString() + ", ");
                            toWrite.Add("    purposes = new HashSet<BasePurpose>{ " + SB.ToString() + "},");
                            SB.Clear();
                            toWrite.Add("    deployBoltsASAP = " + BT.purposes.Contains(BasePurpose.NotStationary).ToString().ToLower() + ",");
                            toWrite.Add("    environ = " + (BT.faction == FactionTypesExt.GT).ToString().ToLower() + ",");
                            toWrite.Add("    startingFunds = " + BT.startingFunds + ",");
                            toWrite.Add("    savedTech = \"" + BT.savedTech.Replace("\"", "\\\"") + "\",");
                            toWrite.Add("} },");
                        }
                    }
                    toWrite.Add("");
                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("");
                    File.WriteAllText(Path.Combine(export, "Techs.json"), ""); // CLEAR
                    File.AppendAllLines(Path.Combine(export, "Techs.json"), toWrite);
                    toWrite.Clear();

                    foreach (string str in basetypeNamesOrdered)
                    {
                        toWrite.Add(str + ",");
                    }
                    /*

                    toWrite.Add("");
                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("---------------- <<< END EXPORTING >>> --------------------");
                    toWrite.Add("-----------------------------------------------------------");
                    */
                    File.WriteAllText(Path.Combine(export, "ESpawnBaseTypes.json"), ""); // CLEAR
                    File.AppendAllLines(Path.Combine(export, "ESpawnBaseTypes.json"), toWrite);

                    File.WriteAllText(Path.Combine(export, "batchNew.json"), CommunityCluster.GetLocalToPublic());
                    OpenInExplorer(Path.Combine(export, "batchNew.json"));
                }
                catch { }
            }
            AltUI.Tooltip.GUITooltip("1-Setup: Exports your LOCAL RawTechs as a batchNew.json");


            StepMenuPlacer();

            if (GUILayout.Button(redStart + "REPLACE EDIT</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                string import = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
                if (Directory.Exists(import))
                {
                    string importJSON = Path.Combine(import, "batchNew.json");
                    string EndJSON = Path.Combine(import, "batchEdit.json");
                    if (File.Exists(importJSON))
                    {
                        File.Copy(importJSON, EndJSON, true);
                    }
                    else
                        ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ERROR - Please press LOCAL TO COM first.");
                }
            }
            AltUI.Tooltip.GUITooltip("2-Prepare: Copies batchNew.json onto batchEdit.json.");
            
            StepMenuPlacer();

            if (GUILayout.Button(redStart + "COM PULL EXISTING</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                try
                {
                    string export = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
                    if (!Directory.Exists(export))
                        Directory.CreateDirectory(export);
                    Dictionary<SpawnBaseTypes, RawTechTemplate> BTs = JsonConvert.DeserializeObject<Dictionary<SpawnBaseTypes, RawTechTemplate>>(CommunityCluster.FetchPublicTechs());
                    CommunityCluster.Organize(ref BTs);
                    Dictionary<int, RawTechTemplate> BTsInt = BTs.ToList().ToDictionary(x => (int)x.Key, x => x.Value);
                    File.WriteAllText(Path.Combine(export, "batchEdit.json"), JsonConvert.SerializeObject(BTsInt, Formatting.Indented));//, RawTechExporter.JSONDEV));
                    OpenInExplorer(Path.Combine(export, "batchEdit.json"));
                }
                catch (Exception e) {
                    DebugTAC_AI.Log(KickStart.ModID + ": ERROR - " + e);
                    ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ERROR - See log!");
                }
            }
            AltUI.Tooltip.GUITooltip("2-Unpack: Takes currently loaded population and pushes it to batchEdit.json");

            StepMenuPlacer();

            if (GUILayout.Button(redStart + "COM TEST FORMAT</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                try
                {
                    string import = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
                    if (Directory.Exists(import))
                    {
                        string importJSON = Path.Combine(import, "batchEdit.json");
                        if (File.Exists(importJSON))
                        {
                            CommunityCluster.DeployUncompressed(importJSON);
                            ModTechsDatabase.ValidateAndAddAllInternalTechs(false);
                        }
                        else
                            ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ERROR - Please pull existing first.");
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ERROR - " + e);
                    ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ERROR - See log!");
                }
            }
            AltUI.Tooltip.GUITooltip("3-Test: Imports batchEdit.json to replace the currently loaded population");


            StepMenuPlacer();


            if (GUILayout.Button(redStart + "COM PUSH PUBLIC</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                try
                {
                    string import = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
                    if (Directory.Exists(import))
                    {
                        string importJSON = Path.Combine(import, "batchEdit.json");
                        if (File.Exists(importJSON))
                        {
                            CommunityCluster.DeployUncompressed(importJSON);
                            CommunityCluster.PushDeployedToPublicFile();
                            try
                            {
                                ModTechsDatabase.ValidateAndAddAllInternalTechs();
                                string clusterHold2 = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport", "commBatch.RTList");
                                OpenInExplorer(clusterHold2);
                            }
                            catch (Exception e)
                            {
                                DebugTAC_AI.Log(KickStart.ModID + ": Minor Error - " + e);
                            }
                        }
                        else
                            ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ERROR - Please pull existing first.");
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": ERROR - " + e);
                    ManUI.inst.ShowErrorPopup(KickStart.ModID + ": ERROR - See log!"); 
                }
            }
            AltUI.Tooltip.GUITooltip("4-Finalize: Exports batchEdit.json to the commBatch.RTList file, maing it public." +
                        "  You still have to drag it into UnityEditor though");


            StepMenuPlacer();


            if (GUILayout.Button(redStart + "PURGE DUPLICATES</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                try
                {
                    List<int> exists = new List<int>();
                    foreach (RawTech bt in ModTechsDatabase.InternalPopTechs.Values)
                    {
                        exists.Add(bt.techName.GetHashCode());
                    }

                    int count = listTemp.Count();
                    for (int step = 0; step < count; step++)
                    {
                        RawTech BT = listTemp[step];
                        if (exists.Contains(BT.techName.GetHashCode()))
                        {
                            listTemp.Remove(BT);
                            count--;
                            step--;
                        }
                    }
                    DebugTAC_AI.Log("-----------------------------------------------------------");
                    DebugTAC_AI.Log("----------------- <<< END PURGING >>> ---------------------");
                    DebugTAC_AI.Log("-----------------------------------------------------------");
                }
                catch { }
            }

            StepMenuPlacer();
            /*
            if (GUILayout.Button(redStart + "PURGE MISSING</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                try
                {
                    int count = listTemp.Count();
                    for (int step = 0; step < count; step++)
                    {
                        RawTech BT = listTemp[step];
                        if (BT.IsMissingBlocks())
                        {
                            listTemp.Remove(BT);
                            count--;
                            step--;
                        }
                    }
                    DebugTAC_AI.Log("-----------------------------------------------------------");
                    DebugTAC_AI.Log("----------------- <<< END PURGING >>> ---------------------");
                    DebugTAC_AI.Log("-----------------------------------------------------------");
                }
                catch { }
            }
            StepMenuPlacer();*/


            if (GUILayout.Button(AINoAttackPlayer ? redStart + "Attack Player Off</b></color>" : redStart + "Attack Player ON</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                AINoAttackPlayer = !AINoAttackPlayer;
            }
            StepMenuPlacer();

#endif

            if (listTemp == null || listTemp.Count() == 0)
            {
                if (ShowLocal)
                {
                    if (GUILayout.Button("There's Nothing In"))
                    {
                        SpawnTech(SpawnBaseTypes.NotAvail);
                    }
                    StepMenuPlacer();
                    if (GUILayout.Button("The Enemies Folder!"))
                    {
                        SpawnTech(SpawnBaseTypes.NotAvail);
                    }
                }
                else
                {
                    if (GUILayout.Button("None in Mods."))
                    {
                        SpawnTech(SpawnBaseTypes.NotAvail);
                    }
                }
                return;
            }

            int Entries = listTemp.Count();
            for (int step = 0; step < Entries; step++)
            {
                try
                {
                    RawTech temp = listTemp[step];
                    StepMenuPlacerPartial();
                    string disp;
                    if (temp.purposes.Contains(BasePurpose.NotStationary))
                    {
                        switch (temp.terrain)
                        {
                            case BaseTerrain.Land:
                                disp = "<color=#90ee90ff>" + temp.techName.ToString() + "</color>";
                                break;
                            case BaseTerrain.Air:
                                disp = "<color=#ffa500ff>" + temp.techName.ToString() + "</color>";
                                break;
                            case BaseTerrain.Sea:
                                disp = "<color=#add8e6ff>" + temp.techName.ToString() + "</color>";
                                break;
                            case BaseTerrain.Space:
                                disp = "<color=#ffff00ff>" + temp.techName.ToString() + "</color>";
                                break;
                            default:
                                disp = temp.techName.ToString();
                                break;
                        }
                    }
                    else
                        disp = temp.techName.ToString();
                    if (temp.purposes.Contains(BasePurpose.NANI))
                    {
                        disp = "[E] " + disp;
                    }
                    if (GUILayout.Button(disp, GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
                    {
                        index = step;
                        clicked = true;
                    }
                    HoriPosOff += ButtonWidth;
                }
                catch { }// error on handling something
            }
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            scrolllSize = VertPosOff + 80;

            if (MaxExtensionY)
                HotWindow.height = MaxWindowHeight + 80;
            else
                HotWindow.height = VertPosOff + 80;

            if (MaxExtensionX)
                HotWindow.width = MaxWindowWidth + 60;
            else
                HotWindow.width = HoriPosOff + 60;
            if (clicked)
            {
                if (Input.GetKey(KeyCode.Backspace))
                    RemoveTechLocal(index);
                else
                    SpawnTechLocal(listTemp, index);
            }

            GUI.DragWindow();
        }
        private static void ClearDupeTechs()
        {
            if (ManSnapshots.inst.m_QueryStatus.Value != ManSnapshots.QueryStatus.Done)
            {
                DebugTAC_AI.Log("ClearDupeTechs() Not ready yet, waiting...");
                InvokeHelper.InvokeSingle(ClearDupeTechs, 1);
            }
            else
            {
                DebugTAC_AI.Log("ClearDupeTechs() Ready!");
                try
                {
                    int count = 0;
                    var ListTemp2 = TempStorage.techBasesPrefab.ToList();
                    HashSet<string> basetypeNames = new HashSet<string>();
                    for (int i = 0; i < ManSnapshots.inst.SnapshotCollection.Count; i++)
                    {
                        try
                        {
                            Snapshot snap = ManSnapshots.inst.SnapshotCollection.ElementAt(i).m_Snapshot;
                            string name = snap.m_Name.Value;
                            string nameBaseType = name.Replace(" ", "");
                            if (nameBaseType.Contains('#') || !basetypeNames.Add(nameBaseType) || 
                                (Enum.TryParse<SpawnBaseTypes>(nameBaseType, out var type) && type <= SpawnBaseTypes.GSOQuickBuck) ||
                                CommunityStorage.CommunityStored.Exists(x => { return x.Value.techName == name; }) ||
                                ListTemp2.Exists(x => { return x.Value.techName == name; }))
                            {
                                count++;
                                DebugTAC_AI.Log("DELETED " + name);
                                ManSnapshots.inst.ServiceDisk.DeleteSnapshot(snap);
                            }
                        }
                        catch { }
                    }
                    DebugTAC_AI.Log("DELETED " + count + " snaps!");
                }
                catch { }
            }
        }
        private static void GUIHandlerPreset(int ID)
        {
            ResetMenuPlacer();
            
           // scrolll = GUILayout.BeginScrollView(new Rect(0, 64, HotWindow.width - 20, HotWindow.height -64), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            scrolll = GUILayout.BeginScrollView(scrolll);

            GUILayout.BeginHorizontal();
            RemoveAllEnemiesImmedeatelyButton();
            HoriPosOff += ButtonWidth;
            if (GUILayout.Button(InstantLoad ? redStart + "Instant ON</b></color>" : redStart + "Instant Off</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                InstantLoad = !InstantLoad;
            }
            HoriPosOff += ButtonWidth;
            if (GUILayout.Button(ShowDebugFeedBack ? redStart + "Hide AI Debug</b></color>" : redStart + "Show AI Debug</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                ShowDebugFeedBack = !ShowDebugFeedBack;
            }
            HoriPosOff += ButtonWidth;

#if !DEBUG
            if (GUILayout.Button(AIEPathMapper.ShowPathingGIZMO ? redStart + "Hide Pathing</b></color>" : redStart + "Show Pathing</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                AIEPathMapper.ShowPathingGIZMO = !AIEPathMapper.ShowPathingGIZMO;
            }
            StepMenuPlacer();
#endif

            if (GUILayout.Button(redStart + "SPAWN Priced</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                RawTechPopParams RTF = RawTechPopParams.Default;
                RTF.Terrain = BaseTerrain.Any;
                RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                var temp = RawTechLoader.SpawnRandomTechAtPosHead(GetPlayerPos(), GetPlayerForward(), 
                    AIGlobals.GetRandomBaseTeam(), RTF, true);
                if (temp == null)
                    AIGlobals.PopupEnemyInfo("Fallback Error", WorldPosition.FromScenePosition(GetPlayerPos()));
            }
            StepMenuPlacer();

            if (GUILayout.Button(redStart + "SPAWN Founder</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                RawTechPopParams RTF = RawTechPopParams.Default;
                RTF.Terrain = BaseTerrain.Any;
                RTF.Purpose = BasePurpose.Harvesting;
                RTF.MaxPrice = KickStart.EnemySpawnPriceMatching;
                var temp = RawTechLoader.SpawnRandomTechAtPosHead(GetPlayerPos(), GetPlayerForward(), 
                    AIGlobals.GetRandomBaseTeam(), RTF, true);
                if (temp == null)
                    AIGlobals.PopupEnemyInfo("Fallback Error", WorldPosition.FromScenePosition(GetPlayerPos()));

                RawTechLoader.TryStartBase(temp, temp.GetHelperInsured(), BasePurpose.HarvestingNoHQ);
            }
            StepMenuPlacer();
#if DEBUG
            if (GUILayout.Button(redStart + "Pop to Snaps</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                CommunityCluster.SaveCommunityPoolBackToDisk();
            }
            AltUI.Tooltip.GUITooltip("POP CONTROL: Exports the current community pool to the snaps.");
            StepMenuPlacer();
            if (GUILayout.Button(redStart + "Local to Snaps</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                SaveLocalPoolBackToDisk();
            }
            AltUI.Tooltip.GUITooltip("POP CONTROL: Exports the current local pool to the snaps.");
            StepMenuPlacer();
            if (GUILayout.Button(redStart + "Snaps to Pop</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                string export = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "MassExport");
                File.WriteAllText(Path.Combine(export, "commBatch.RTList"), CommunityCluster.ExportSnapsToCommunityPool());
                OpenInExplorer(Path.Combine(export, "commBatch.RTList"));
            }
            AltUI.Tooltip.GUITooltip("POP CONTROL: Exports ALL snaps to the community pool.  " +
                        "You still have to move it to UnityEditor");
            StepMenuPlacer();
            /*
            if (GUILayout.Button(redStart + "Purge RawTechs</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                RawTechExporter.PurgeAllRawTechs();
            }
            StepMenuPlacer();
            */

#endif
            if (GUILayout.Button(AINoAttackPlayer ? redStart + "Attack Player Off</b></color>" : redStart + "Attack Player ON</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                AINoAttackPlayer = !AINoAttackPlayer;
            }
            StepMenuPlacer();

            if (GUILayout.Button(AllowPlayerBuildEnemies ? redStart + "ALL Team</b></color>" : redStart + "Player Team</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                AllowPlayerBuildEnemies = !AllowPlayerBuildEnemies;
            }
            StepMenuPlacer();
            if (GUILayout.Button(AllowPlayerCommandEnemies ? redStart + "ALL Command</b></color>" : redStart + "Player Command</b></color>", GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
            {
                AllowPlayerCommandEnemies = !AllowPlayerCommandEnemies;
            }
            StepMenuPlacer();
            FactionSubTypes currentFaction = (FactionSubTypes)(-1);
            string disp;
            foreach (KeyValuePair<SpawnBaseTypes, RawTech> temp in ModTechsDatabase.InternalPopTechs)
            {
                StepMenuPlacerPartial();
                FactionSubTypes FST = RawTechUtil.CorpExtToCorp(temp.Value.faction);
                if (currentFaction != FST)
                {
                    currentFaction = FST;

                    if (HoriPosOff > 0)
                    {
                        VertPosOff += 30;
                        HoriPosOff = 0;
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    if (FST == FactionSubTypes.EXP)
                        disp = "RR";
                    else if (RawTechUtil.IsFactionExtension(temp.Value.faction))
                        disp = temp.Value.faction.ToString();
                    else if (ManMods.inst.IsModdedCorp(FST))
                        disp = ManMods.inst.FindCorpShortName(FST);
                    else
                        disp = temp.Value.faction.ToString();
                    if (GUILayout.Button("<b>" + disp + "</b>"))
                    {
                        if (openedFactions.Contains(currentFaction))
                            openedFactions.Remove(currentFaction);
                        else
                            openedFactions.Add(currentFaction);
                    }
                    MaxExtensionX = true;
                    VertPosOff += 30;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (VertPosOff >= MaxWindowHeight)
                        MaxExtensionY = true;
                }
                if (openedFactions.Contains(currentFaction))
                {
                    if (temp.Value.purposes.Contains(BasePurpose.NotStationary))
                    {
                        switch (temp.Value.terrain)
                        {
                            case BaseTerrain.Land:
                                disp = "<color=#90ee90ff>" + temp.Value.techName.ToString() + "</color>";
                                break;
                            case BaseTerrain.Air:
                                disp = "<color=#ffa500ff>" + temp.Value.techName.ToString() + "</color>";
                                break;
                            case BaseTerrain.Sea:
                                disp = "<color=#add8e6ff>" + temp.Value.techName.ToString() + "</color>";
                                break;
                            case BaseTerrain.Space:
                                disp = "<color=#ffff00ff>" + temp.Value.techName.ToString() + "</color>";
                                break;
                            default:
                                disp = temp.Value.techName.ToString();
                                break;
                        }
                    }
                    else
                        disp = temp.Value.techName.ToString();

                    if (temp.Value.purposes.Contains(BasePurpose.NANI))
                    {
                        disp = "[E] " + disp;
                    }
                    if (GUILayout.Button(disp, GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
                    {
                        type = temp.Key;
                        clicked = true;
                    }
                    HoriPosOff += ButtonWidth;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            scrolllSize = VertPosOff + 80;

            if (MaxExtensionY)
                HotWindow.height = MaxWindowHeight + 80;
            else
                HotWindow.height = VertPosOff + 80;

            if (MaxExtensionX)
                HotWindow.width = MaxWindowWidth + 60;
            else
                HotWindow.width = HoriPosOff + 60;
            if (clicked)
            {
                SpawnTech(type);
            }
            GUI.DragWindow();
        }

        static DirectoryInfo pather = new DirectoryInfo(RawTechExporter.RawTechsDirectory);
        static string selectedFolder = null;
        static List<KeyValuePair<string, int>> folders = new List<KeyValuePair<string, int>>();
        private static void UpdateFolders()
        {
            folders.Clear();
            foreach (var item in Directory.EnumerateDirectories(pather.ToString()))
            {
                folders.Add(new KeyValuePair<string, int>(Path.GetFileNameWithoutExtension(item),
                    Directory.EnumerateFiles(item, "*.json").Count() + 
                    Directory.EnumerateFiles(item, "*.RAWTECH").Count()));
            }
        }
        private static void GUIHandlerFolderSelect(int ID)
        {
            //scrolll = GUI.BeginScrollView(new Rect(0, 64, HotWindow.width - 20, HotWindow.height - 64), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            scrolll = GUILayout.BeginScrollView(scrolll);

            if (!GUILayout.Button("Exit", AltUI.ButtonRed))
            {
                if (pather.ToString() != RawTechExporter.RawTechsDirectory)
                {
                    if (GUILayout.Button("Exit Folder " + pather.Name, AltUI.ButtonRed))
                    {
                        pather = pather.Parent;
                        UpdateFolders();
                    }
                }
                try
                {
                    foreach (var item in folders)
                    {
                        GUILayout.BeginHorizontal();
                        if (item.Key == "Enemies")
                        {
                            if (GUILayout.Button(item.Key, AltUI.ButtonRed))
                            {
                                pather = new DirectoryInfo(Path.Combine(pather.ToString(), item.Key));
                                UpdateFolders();
                            }
                        }
                        else if (GUILayout.Button(item.Key))
                        {
                            pather = new DirectoryInfo(Path.Combine(pather.ToString(), item.Key));
                            UpdateFolders();
                        }
                        if (GUILayout.Button("EXPORT", GUILayout.Width(80)))
                        {
                            ExportAllWithin(Path.Combine(pather.ToString(), item.Key));
                            pather = new DirectoryInfo(RawTechExporter.RawTechsDirectory);
                            InvokeHelper.Invoke(() => { menu = DebugMenus.DebugLog; }, 0);
                        }
                        GUILayout.Label(item.Value.ToString(), GUILayout.Width(60));
                        GUILayout.EndHorizontal();
                    }
                }
                catch (ExitGUIException e)
                {
                    throw e;
                }
                catch { }
            }
            else
            {
                InvokeHelper.Invoke(() => { menu = DebugMenus.DebugLog; }, 0);
            }
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
        private static void ExportAllWithin(string path)
        {
            try
            {
                List<RawTech> temps = new List<RawTech>();
                HashSet<string> names = new HashSet<string>();
                OrganizeDeploy(ref temps);

                foreach (var item in Directory.EnumerateFiles(path))
                {
                    string name = Path.GetFileNameWithoutExtension(item);
                    var inst = ModTechsDatabase.ExtPopTechsLocal.Find(x => x.techName == name);
                    if (inst != null)
                        temps.Add(inst);
                }
                if (temps.Count > 0)
                {
                    string export = Path.Combine(path, "Bundled");
                    Directory.CreateDirectory(export);

                    RawTechExporter.MakeExternalRawTechListFile(Path.Combine(export, "RawTechs.RTList"), temps);
                }
            }
            catch { }
        }

        public static void RemoveTechLocal(int index)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);

            if (ShowLocal)
                ModTechsDatabase.ExtPopTechsLocal.RemoveAt(index);
            else
               ModTechsDatabase.ExtPopTechsMods.RemoveAt(index);
        }
        public static void SpawnTechLocal(List<RawTech> temps, int index)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);

            RawTech val = temps[index];
            Tank tank = null;

            RawTechPopParams RTF;
            if (val.purposes.Contains(BasePurpose.NotStationary))
            {
                RTF = RawTechPopParams.Default;
                RTF.IsPopulation = true;
                RawTechLoader.BypassSpawnCheckOnce = true;
                tank = RawTechLoader.SpawnMobileTechPrefab(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), val, RTF);
            }
            else
            {
                if (InstantLoad)
                {
                    RawTechLoader.BypassSpawnCheckOnce = true;
                    if (val.purposes.Contains(BasePurpose.Defense))
                        tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), val, false);
                    else if (val.purposes.Contains(BasePurpose.Headquarters))
                    {
                        int team = AIGlobals.GetRandomBaseTeam();
                        /*
                        int index2;
                        BaseTemplate val2;
                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() + (Vector3.forward * 64),  Vector3.forward, team, val2, false));

                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() - (Vector3.forward * 64), Vector3.forward, team, val2, false));

                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() + (Vector3.right * 64), Vector3.forward, team, val2, false));

                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() - (Vector3.right * 64), Vector3.forward, team, val2, false));
                        */
                        tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), Vector3.forward, team, val, true);
                        Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                    }
                    else
                        tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(),GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), val, true);
                }
                else
                {

                    if (val.purposes.Contains(BasePurpose.Defense))
                        RawTechLoader.SpawnBase(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), val, false);
                    else if (val.purposes.Contains(BasePurpose.Headquarters))
                    {
                        int extraBB = 0;
                        int team = AIGlobals.GetRandomBaseTeam();
                        /*
                        int index2;
                        BaseTemplate val2;
                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() + (Vector3.forward * 64),  Vector3.forward, team, val2, false);

                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() - (Vector3.forward * 64),  Vector3.forward, team, val2, false);

                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() + (Vector3.right * 64),  Vector3.forward, team, val2, false);

                        index2 = RawTechLoader.GetExternalIndex(val.faction, BasePurpose.Defense, val.terrain);
                        val2 = TempManager.ExternalEnemyTechs[index2];
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() - (Vector3.right * 64), Vector3.forward, team, val2, false);
                        */
                        int BB = RawTechLoader.SpawnBase(GetPlayerPos(), Vector3.forward, team, val, true, ExtraBB: extraBB);
                        ManBaseTeams.InsureBaseTeam(team).AddBuildBucks(BB);
                        Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                    }
                    else
                        tank = RawTechLoader.GetSpawnBase(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), val, true);
                }
            }
            if (tank)
                RawTechLoader.ChargeAndClean(tank);

        }
        public static void SpawnTech(SpawnBaseTypes type)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);

            if (ModTechsDatabase.InternalPopTechs.TryGetValue(type, out RawTech val))
            {
                Tank tank = null;
                if (val.purposes.Contains(BasePurpose.NotStationary))
                {
                    RawTechPopParams RTF = RawTechPopParams.Default;
                    RTF.IsPopulation = true;
                    RTF.SpawnCharged = true;
                    RawTechLoader.BypassSpawnCheckOnce = true;
                    tank = RawTechLoader.SpawnMobileTechPrefab(GetPlayerPos(), GetPlayerForward(), 
                        AIGlobals.GetRandomBaseTeam(), RawTechLoader.GetBaseTemplate(type), RTF);
                }
                else
                {
                    if (InstantLoad)
                    {
                        RawTechLoader.BypassSpawnCheckOnce = true;
                        if (val.purposes.Contains(BasePurpose.Defense))
                            tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), type, false);
                        else if (val.purposes.Contains(BasePurpose.Headquarters))
                        {
                            int team = AIGlobals.GetRandomBaseTeam();
                            /*
                            SpawnBaseTypes type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() + (Vector3.forward * 64), Vector3.forward, team, type2, false));
                            }
                            type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() - (Vector3.forward * 64), Vector3.forward, team, type2, false));
                            }
                            type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() + (Vector3.right * 64), Vector3.forward, team, type2, false));
                            }
                            type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                RawTechLoader.ChargeAndClean(RawTechLoader.SpawnBaseInstant(GetPlayerPos() - (Vector3.right * 64), Vector3.forward, team, type2, false));
                            }*/
                            tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), Vector3.forward, team, type, true);
                            Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                        }
                        else
                            tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), type, true);
                    }
                    else
                    {
                        if (val.purposes.Contains(BasePurpose.Defense))
                            RawTechLoader.SpawnBase(GetPlayerPos(), AIGlobals.GetRandomBaseTeam(), type, false);
                        else if (val.purposes.Contains(BasePurpose.Headquarters))
                        {
                            int team = AIGlobals.GetRandomBaseTeam();
                            int extraBB = 0;
                            /*
                            SpawnBaseTypes type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                extraBB += RawTechLoader.SpawnBase(GetPlayerPos() + (Vector3.forward * 64), team, type2, false);
                            }
                            type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                extraBB += RawTechLoader.SpawnBase(GetPlayerPos() - (Vector3.forward * 64), team, type2, false);
                            }
                            type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                extraBB += RawTechLoader.SpawnBase(GetPlayerPos() + (Vector3.right * 64), team, type2, false);
                            }
                            type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                            if (TempManager.techBases.TryGetValue(type2, out _))
                            {
                                extraBB += RawTechLoader.SpawnBase(GetPlayerPos() - (Vector3.right * 64), team, type2, false);
                            }*/
                            int BB = RawTechLoader.SpawnBase(GetPlayerPos(), team, type, true, extraBB);
                            ManBaseTeams.InsureBaseTeam(team).AddBuildBucks(BB);
                            Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                        }
                        else
                        {
                            int team = AIGlobals.GetRandomBaseTeam();
                            int BB = RawTechLoader.SpawnBase(GetPlayerPos(), team, type, true);
                            ManBaseTeams.InsureBaseTeam(team).AddBuildBucks(BB);
                        }
                    }
                }
                if (tank)
                    RawTechLoader.ChargeAndClean(tank);
            }

            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
        }

        public static void RemoveAllEnemiesImmedeatelyButton()
        {
            try
            {
                bool TrackedMode = Input.GetKey(KeyCode.LeftShift);
                bool ALLMode = Input.GetKey(KeyCode.LeftControl);
                if (GUILayout.Button(ALLMode ? (redStart + "PURGE ALL ENEMIES</b></color>") : 
                    (TrackedMode ? (redStart + "PURGE TRACKED ENEMIES</b></color>") : 
                    (redStart + "PURGE ENEMIES</b></color>")), GUILayout.Width(ButtonWidth), GUILayout.Height(30)))
                {
                    bool defaultMode = true;
                    if (TrackedMode)
                    { // REMOVE ALL TRACKED
                        DebugTAC_AI.Log(KickStart.ModID + ": RemoveAllEnemiesImmedeately - CALLED TrackedMode");
                        defaultMode = false;
                        try
                        {
                            List<ManSaveGame.StoredTech> storTechs = new List<ManSaveGame.StoredTech>();
                            foreach (var item in ManVisible.inst.AllTrackedVisibles)
                            {
                                if (item != null)
                                {
                                    if (item.visible?.tank != null && AIGlobals.TechIsSafelyRemoveable(item.visible.tank))
                                    {
                                        techCache.Add(item.visible.tank);
                                    }
                                    else
                                    {
                                        ManSaveGame.StoredTech storTech = AIGlobals.FindStoredTech(item.ID, item.GetWorldPosition().TileCoord, true);
                                        if (storTech != null && AIGlobals.TechIsSafelyRemoveable(storTech))
                                        {
                                            storTechs.Add(storTech);
                                        }
                                    }
                                }
                            }
                            foreach (var tech in techCache)
                            {
                                AIGlobals.Purge(tech);
                            }
                            foreach (var tech in storTechs)
                            {
                                AIGlobals.Purge(tech, true);
                            }
                        }
                        finally
                        {
                            techCache.Clear();
                        }
                    }
                    if (ALLMode)
                    { // REMOVE ALL STORED
                        DebugTAC_AI.Log(KickStart.ModID + ": RemoveAllEnemiesImmedeately - CALLED ALLMode");
                        defaultMode = false;
                        var STs = ManSaveGame.inst.CurrentState?.m_StoredTiles;
                        List<ManSaveGame.StoredTech> storTechs = new List<ManSaveGame.StoredTech>();
                        foreach (var tile in STs)
                        {
                            if (tile.Value != null && tile.Value.m_StoredVisibles.TryGetValue((int)ObjectTypes.Vehicle, out var tankList))
                            {
                                foreach (var vis in tankList)
                                {
                                    if (vis != null && vis is ManSaveGame.StoredTech tech && AIGlobals.TechIsSafelyRemoveable(tech))
                                    {
                                        storTechs.Add(tech);
                                    }
                                }
                            }
                        }
                        foreach (var tech in storTechs)
                        {
                            AIGlobals.RemoveStoredTech(tech.m_ID, tech.m_WorldPosition.TileCoord, true);
                        }
                    }
                    if (defaultMode)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": RemoveAllEnemiesImmedeately - CALLED defaultMode");
                        try
                        {
                            foreach (var item in ManTechs.inst.CurrentTechs)
                            {
                                if (item != null && item.visible.isActive &&
                                    AIGlobals.TechIsSafelyRemoveable(item))
                                {
                                    DebugTAC_AI.Log("Chain Purge add: " + item.name);
                                    techCache.Add(item);
                                }
                            }
                            foreach (var tech in techCache)
                            {
                                DebugTAC_AI.Log("Chain Purging: " + tech.name);
                                AIGlobals.Purge(tech);
                            }
                        }
                        finally
                        {
                            techCache.Clear();
                        }
                    }
                }
            }
            catch (ExitGUIException e) { throw e; }
            catch (Exception e){
                DebugTAC_AI.Log("Crash in DebugRawTechSpawner.RemoveAllEnemiesImmedeately(): " + e);
            }
        }

        private static List<TrackedVisible> toDestroy = new List<TrackedVisible>();
        public static void RemoveOrphanTrackedVisibles()
        {
            try
            {
                toDestroy.AddRange(ManVisible.inst.AllTrackedVisibles);
                int length = toDestroy.Count;
                int removed = 0;
                for (int step = 0; step < length;)
                {
                    TrackedVisible remove = toDestroy[step];
                    if (remove.ObjectType != ObjectTypes.Vehicle)
                    {
                        step++;
                        continue;
                    }
                    WorldPosition WP = remove.GetWorldPosition();
                    WorldTile WT = ManWorld.inst.TileManager.LookupTile(WP.TileCoord);
                    if (WT != null && WT.IsLoaded)
                    {
                        if (WT.StoredVisiblesWaitingToLoad != null && WT.StoredVisiblesWaitingToLoad.Count > 0 && remove.visible.isActive)
                        {
                            step++;
                            continue; // TECHS ARE LOADING AND IF WE REMOVE IT NOW IT WILL IGNORE TEAMS
                        }

                        if (AIGlobals.IsBaseTeamDynamic(remove.TeamID) && (remove.visible == null || (remove.visible != null && !remove.visible.isActive)))
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": RemoveOrphanTrackedVisibles - iterating " + remove.TeamID + " | "
                                + remove.RadarTeamID + " | " + remove.RawRadarTeamID + " | " + remove.RadarMarkerConfig + " | "
                                + (remove.visible ? "active" : "inactive"));

                            toDestroy.RemoveAt(step);
                            try
                            {
                                //ManVisible.inst.ObliterateTrackedVisibleFromWorld(remove.ID); 
                                ManWorld.inst.TileManager.GetStoredTileIfNotSpawned(remove.Position, false).RemoveSavedVisible(ObjectTypes.Vehicle, remove.ID);

                            }
                            catch { }
                            try
                            {
                                ManVisible.inst.StopTrackingVisible(remove.ID);
                            }
                            catch { }
                            remove.StopTracking();
                            removed++;
                            length--;
                            continue;
                        }
                    }
                    step++;
                }
                if (removed > 0)
                    DebugTAC_AI.Log(KickStart.ModID + ": RemoveOrphanTrackedVisibles - removed " + removed);
            }
            catch { }
            finally
            {
                toDestroy.Clear();
            }
        }

        public static void LaunchSubMenuClickable()
        {
            if (!UIIsCurrentlyOpen)
            {
                RawTechExporter.ReloadTechsNow();
                DebugTAC_AI.Log(KickStart.ModID + ": Opened Raw Techs Debug menu!");
                GUIWindow.SetActive(true);
            }
        }
        public static void LaunchSubMenuClickable(DebugMenus type)
        {
            if (!UIIsCurrentlyOpen)
            {
                menu = type;
                RawTechExporter.ReloadTechsNow();
                DebugTAC_AI.Log(KickStart.ModID + ": Opened Raw Techs Debug menu!");
                GUIWindow.SetActive(true);
            }
        }
        public static void CloseSubMenuClickable()
        {
            if (UIIsCurrentlyOpen)
            {
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                DebugTAC_AI.Log(KickStart.ModID + ": Closed Raw Techs Debug menu!");
            }
        }
        public static void ForceCloseSubMenuClickable()
        {
            toggleDebugLock = false;
            GUIWindow.SetActive(false);
            KickStart.ReleaseControl();
            DebugTAC_AI.Log(KickStart.ModID + ": Force-Closed Raw Techs Debug menu!");
        }


        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    if (menu == DebugMenus.DebugLog || !toggleDebugLock)
                    {
                        toggleDebugLock = !toggleDebugLock;
                    }
                    menu = DebugMenus.DebugLog;
                    if (toggleDebugLock)
                        LaunchSubMenuClickable();
                }
                else if (Input.GetKeyDown(KeyCode.Minus))
                {
                    if (menu == DebugMenus.Local || !toggleDebugLock)
                    {
                        toggleDebugLock = !toggleDebugLock;
                    }
                    menu = DebugMenus.Local;
                    if (toggleDebugLock)
                        LaunchSubMenuClickable();
                }
                else if (Input.GetKeyDown(KeyCode.Equals))
                {
                    if (menu == DebugMenus.Prefabs || !toggleDebugLock)
                    {
                        toggleDebugLock = !toggleDebugLock;
                    }
                    menu = DebugMenus.Prefabs;
                    if (toggleDebugLock)
                        LaunchSubMenuClickable();
                }
                else if (toggleDebugLock)
                {
                    LaunchSubMenuClickable();
                }
                else if (!toggleDebugLock)
                {
                    CloseSubMenuClickable();
                }
            }
            else if (toggleDebugLock)
            {
                LaunchSubMenuClickable();
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Y))
                {
                    toggleDebugLock = false;
                }
                else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.U))
                {
                    toggleDebugLock = false;
                }
            }
            else if (!toggleDebugLock)
            {
                CloseSubMenuClickable();
            }
        }

        private static bool DoCullInvalidVisibles = true;
        internal static void CheckAndDestroyAllInvalidVisibles(bool checkALLJSONTilesToo)
        {
            AIGlobals.LogAllTrackedEnemyBaseVisibles();
            if (DoCullInvalidVisibles)
            {
                foreach (var item in new List<TrackedVisible>(ManVisible.inst.AllTrackedVisibles))
                {
                    if (item == null)
                        continue;
                    if (item.ObjectType == ObjectTypes.Vehicle)
                    {
                        if (ManWorld.inst.TileManager.IsTileAtPositionLoaded(item.Position))
                        {
                            WorldTile WT = ManWorld.inst.TileManager.LookupTile(item.GetWorldPosition().TileCoord);
                            if (WT.StoredVisiblesWaitingToLoad != null && WT.StoredVisiblesWaitingToLoad.Exists(x => x.m_ID == item.HostID))
                            {
                                DebugTAC_AI.Info("  Waiting to load visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                            }
                            else if (item.wasDestroyed || item.visible == null)
                            {
                                ManSaveGame.StoredTech ST = AIGlobals.FindStoredTech(item.ID, item.GetWorldPosition().TileCoord, checkALLJSONTilesToo);
                                if (ST != null)
                                {
                                    if (ManBaseTeams.IsBaseTeamAny(item.TeamID))
                                    {
                                        DebugTAC_AI.Info("  Invalid Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                        ManVisible.inst.StopTrackingVisible(item.ID);
                                    }
                                    else if (AIGlobals.IsBaseTeamDynamicOrUnregistered(item.TeamID))
                                    {
                                        DebugTAC_AI.Info("  Invalid UNREGISTERED Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                        ManVisible.inst.StopTrackingVisible(item.ID);
                                    }
                                    else
                                        DebugTAC_AI.Info("  Invalid Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                }
                                else
                                {
                                    if (ManBaseTeams.IsBaseTeamAny(item.TeamID))
                                    {
                                        DebugTAC_AI.Info("  NULL Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                        ManVisible.inst.StopTrackingVisible(item.ID);
                                    }
                                    else if (AIGlobals.IsBaseTeamDynamicOrUnregistered(item.TeamID))
                                    {
                                        DebugTAC_AI.Info("  NULL UNREGISTERED Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                        ManVisible.inst.StopTrackingVisible(item.ID);
                                    }
                                    else
                                        DebugTAC_AI.Info("  NULL Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                }
                            }
                            else
                                DebugTAC_AI.Info("  Active Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                        }
                        else
                            DebugTAC_AI.Info("  Unloaded Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                    }
                }
            }
        }

        internal static void SaveLocalPoolBackToDisk()
        {
            SnapshotCollectionDisk SCD = null;
            try
            {
                SCD = ManSnapshots.inst?.ServiceDisk?.GetSnapshotCollectionDisk();
            }
            catch { }
            if (SCD == null)
                throw new NullReferenceException("ManSnapshots.inst.ServiceDisk failed to load");
            if (ModTechsDatabase.ExtPopTechsLocal == null)
                throw new NullReferenceException("ModTechsDatabase.ExternalEnemyTechsLocal failed to load");
            foreach (var item in ModTechsDatabase.ExtPopTechsLocal)
            {
                try
                {
                    if (item == null)
                        continue;
                    var snap = SCD.FindSnapshot(item.techName);
                    if (snap == null)
                        techsToSaveSnapshots.Enqueue(item);
                }
                catch { }
            }
            DoSaveLocalTechBackToDisk();
        }
        private static Queue<RawTech> techsToSaveSnapshots = new Queue<RawTech>();
        private static void DoSaveLocalTechBackToDisk(bool success = true)
        {
            if (!techsToSaveSnapshots.Any() || !success)
                return;
            RawTech RT = techsToSaveSnapshots.Dequeue();
            var techD = RawTechLoader.GetUnloadedTech(RT, ManPlayer.inst.PlayerTeam, true, out _);
            ManScreenshot.inst.RenderTechImage(techD, ManSnapshots.inst.GetDiskSnapshotImageSize(),
                true, (TechData techData, Texture2D techImage) =>
                {
                    Singleton.Manager<ManSnapshots>.inst.SaveSnapshotRender(techD, techImage,
                        RT.techName, false, DoSaveLocalTechBackToDisk);
                });
        }





        // Utilities
#if DEBUG
        /*
        internal static bool ShowDebugFeedBack = true;
        //*/ internal static bool ShowDebugFeedBack = false;
#else
        internal static bool ShowDebugFeedBack = false;
#endif
        internal static bool CheckValidMode()
        {
#if DEBUG
            return true;
#else
            if (ManGameMode.inst.IsCurrent<ModeMisc>() || 
                (ManDevCommands.inst.CommandAccessLevel >= Access.Cheat && ManNetwork.IsHost))
            {
                return true;
            }
            return false;
#endif
        }
        private static Vector3 GetPlayerPos()
        {
            try
            {
                PlayerLoc = Singleton.camera.transform.position;
                return Singleton.camera.transform.position + (Singleton.camera.transform.forward * 64);
            }
            catch 
            {
                return PlayerLoc + (Vector3.forward * 64);
            }
        }
        private static Vector3 GetPlayerForward()
        {
            try
            {
                PlayerFow = Singleton.camera.transform.forward;
                return PlayerFow;
            }
            catch
            {
                return PlayerFow;
            }
        }

        internal static void Organize(ref List<RawTech> list)
        {
            try
            {
                var listTemp = list.OrderBy(x => x.terrain)
                    .ThenBy(x => x.purposes.Contains(BasePurpose.NotStationary))
                    .ThenBy(x => x.purposes.Contains(BasePurpose.NANI))
                    .ThenBy(x => x.techName.NullOrEmpty() ? "<NULL>" : x.techName).ToList();
                if (listTemp != null)
                    list = listTemp;
            }
            catch { }
        }
        internal static void OrganizeDeploy(ref List<RawTech> list)
        {
            try
            {
                var listTemp = list.OrderBy(x => x.faction).ThenBy(x => x.terrain)
                .ThenBy(x => x.purposes.Contains(BasePurpose.NotStationary))
                .ThenBy(x => x.purposes.Contains(BasePurpose.NANI))
                    .ThenBy(x => x.techName.NullOrEmpty() ? "<NULL>" : x.techName).ToList();
                if (listTemp != null)
                    list = listTemp;
            }
            catch { }
        }
    }
}
