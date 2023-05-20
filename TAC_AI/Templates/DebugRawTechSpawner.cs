using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;
using TAC_AI.AI.Movement;
using TAC_AI.World;
using System.IO;
using Newtonsoft.Json;
using TerraTechETCUtil;


namespace TAC_AI.Templates
{
    internal enum DebugMenus
    {
        Prefabs,
        Local,
        DebugLog,
    }
    internal class DebugRawTechSpawner : MonoBehaviour
    {
        private static readonly bool Enabled = true;

        internal static bool CanOpenDebugSpawnMenu = false;
        private static DebugMenus menu = DebugMenus.Prefabs;

        private static Vector3 PlayerLoc = Vector3.zero;
        private static Vector3 PlayerFow = Vector3.forward;
        private static bool UIIsCurrentlyOpen = false;
        private static bool toggleDebugLock = false;
        private static bool InstantLoad = false;
        internal static bool DevCheatNoAttackPlayer = false;
        internal static bool DevCheatPlayerEnemyBaseTeam = false;
        private static bool ShowLocal = true;

        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        private const int RawTechSpawnerID = 8002;

        private const string redStart = "<color=#ffcccbff><b>";//"<color=#f23d3dff><b>";

        public static void Initiate()
        {
            if (!Enabled)
                return;

            #if DEBUG
                DebugTAC_AI.Log("TACtical_AI: Raw Techs Debugger launched (DEV)");
            #else
                DebugTAC_AI.Log("TACtical_AI: Raw Techs Debugger launched");
            #endif

            Instantiate(new GameObject()).AddComponent<DebugRawTechSpawner>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplayTechLoader>();
            GUIWindow.SetActive(false);
        }
        public static void ShouldBeActive()
        {
            CanOpenDebugSpawnMenu = CheckValidMode();
        }


        public static bool IsOverMenu()
        {
            if (UIIsCurrentlyOpen && KickStart.CanUseMenu)
            {
                Vector3 Mous = Input.mousePosition;
                Mous.y = Display.main.renderingHeight - Mous.y;
                return HotWindow.Contains(Mous);
            }
            return false;
        }

        internal class GUIDisplayTechLoader : MonoBehaviour
        {
            private void OnGUI()
            {
                if (UIIsCurrentlyOpen && KickStart.CanUseMenu)
                {
                    switch (menu)
                    {
                        case DebugMenus.Prefabs:
                            HotWindow = GUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerPreset, "<b>Debug Prefab Spawns</b>");
                            break;
                        case DebugMenus.Local:
                            HotWindow = GUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerPlayer, "<b>Debug Local Spawns</b>");
                            break;
                        default:
                            HotWindow = GUILayout.Window(RawTechSpawnerID, HotWindow, GUIHandlerDebug, "<b>Debug Mod Info</b>");
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
        private static List<FactionTypesExt> openedFactions = new List<FactionTypesExt>();


        private static void GUIHandlerDebug(int ID)
        {
            HotWindow.height = MaxWindowHeight + 80;
            HotWindow.width = MaxWindowWidth + 60;
            scrolll = GUILayout.BeginScrollView(scrolll);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(HotWindow.width / 2));
            AIECore.TankAIManager.GUIManaged.GUIGetTotalManaged();
            SpecialAISpawner.GUIManaged.GUIGetTotalManaged();
            AIEPathMapper.GUIManaged.GUIGetTotalManaged();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            ManPlayerRTS.GUIGetTotalManaged();
            ManEnemyWorld.GUIManaged.GUIGetTotalManaged();
            ManEnemySiege.GUIGetTotalManaged();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
        private static void GUIHandlerPlayer(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionX = false;
            bool MaxExtensionY = false;
            int index = 0;

            List<RawTechTemplate> listTemp;
            if (ShowLocal)
                listTemp = TempManager.ExternalEnemyTechsLocal;
            else
                listTemp = TempManager.ExternalEnemyTechsMods;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width -20, HotWindow.height - 40), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "PURGE ENEMIES</b></color>"))
            {
                RemoveAllEnemies();
            }

            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "Sort Entire List</b></color>"))
            {
                try
                {
                    if (ShowLocal)
                    {
                        Organize(ref TempManager.ExternalEnemyTechsLocal);
                        listTemp = TempManager.ExternalEnemyTechsLocal;
                    }
                    else
                    {
                        Organize(ref TempManager.ExternalEnemyTechsMods);
                        listTemp = TempManager.ExternalEnemyTechsMods;
                    }
                }
                catch { }
            }


            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), InstantLoad ? redStart + "Instant ON</b></color>" : redStart + "Instant Off</b></color>"))
            {
                InstantLoad = !InstantLoad;
            }

            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), ShowDebugFeedBack ? redStart + "Hide AI Debug</b></color>" : redStart + "Show AI Debug</b></color>"))
            {
                ShowDebugFeedBack = !ShowDebugFeedBack;
            }

            HoriPosOff = 0;
            VertPosOff += 30;
            MaxExtensionX = true;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), ShowLocal ? redStart + "Showing Local</b></color>" : redStart + "Showing Mods</b></color>"))
            {
                ShowLocal = !ShowLocal;
                return;
            }

            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "Correct Forwards</b></color>"))
            {
                if (Singleton.playerTank)
                    AIERepair.DesignMemory.RebuildTechForwards(Singleton.playerTank);
                return;
            }

            HoriPosOff += ButtonWidth;

            if (ShowLocal)
            {
                if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "Bundle ALL for Mod</b></color>"))
                {
                    try
                    {
                        OrganizeDeploy(ref listTemp);

                        RawTechExporter.ValidateEnemyFolder();
                        string export = RawTechExporter.RawTechsDirectory + RawTechExporter.up + "Bundled";
                        Directory.CreateDirectory(export);

                        RawTechExporter.MakeExternalRawTechListFile(export + RawTechExporter.up + "RawTechs.RTList", listTemp);
                    }
                    catch { }
                }
                HoriPosOff += ButtonWidth;
                if (HoriPosOff >= MaxWindowWidth)
                {
                    HoriPosOff = 0;
                    VertPosOff += 30;
                    MaxExtensionX = true;
                    if (VertPosOff >= MaxWindowHeight)
                        MaxExtensionY = true;
                }
                if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth * 2, 30), redStart + "Bundle Active Player Techs for Mod</b></color>"))
                {
                    try
                    {
                        List<RawTechTemplate> temps = new List<RawTechTemplate>();
                        HashSet<string> names = new HashSet<string>();
                        foreach (var item in ManTechs.inst.IteratePlayerTechs())
                        {
                            if (!item.PlayerFocused && item.name != null && !names.Contains(item.name))
                            {
                                names.Add(item.name);
                                var TD = new TechData();
                                TD.SaveTech(item, false, false);
                                temps.Add(new RawTechTemplate(TD));
                            }
                        }
                        OrganizeDeploy(ref temps);

                        RawTechExporter.ValidateEnemyFolder();
                        string export = RawTechExporter.RawTechsDirectory + RawTechExporter.up + "Bundled";
                        Directory.CreateDirectory(export);

                        RawTechExporter.MakeExternalRawTechListFile(export + RawTechExporter.up + "RawTechs.RTList", listTemp);
                    }
                    catch { }
                }

                HoriPosOff += ButtonWidth;
                HoriPosOff += ButtonWidth;
                if (HoriPosOff >= MaxWindowWidth)
                {
                    HoriPosOff = 0;
                    VertPosOff += 30;
                    MaxExtensionX = true;
                    if (VertPosOff >= MaxWindowHeight)
                        MaxExtensionY = true;
                }
            }
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "CLEAR TRACKED</b></color>"))
            {
                DestroyAllInvalidVisibles();
            }

            HoriPosOff += ButtonWidth;
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }

#if DEBUG
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "LOCAL TO COM</b></color>"))
            {
                try
                {
                    OrganizeDeploy(ref listTemp);

                    string export = new DirectoryInfo(Application.dataPath).Parent.ToString() + RawTechExporter.up + "MassExport";
                    Directory.CreateDirectory(export);

                    List<string> toWrite = new List<string>();

                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("--------------- <<< MASS EXPORTING >>> --------------------");
                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("");

                    List<string> basetypeNames = new List<string>();
                    StringBuilder SB = new StringBuilder();
                    foreach (RawTechTemplate BT in listTemp)
                    {
                        string nameBaseType = BT.techName.Replace(" ", "");
                        basetypeNames.Add(nameBaseType);
                        toWrite.Add("{ SpawnBaseTypes." + nameBaseType + ", new BaseTemplate {");
                        toWrite.Add("    techName = \"" + BT.techName + "\",");
                        toWrite.Add("    faction = FactionTypesExt." + BT.faction.ToString() + ",");
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
                    toWrite.Add("");
                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("");
                    File.WriteAllText(export + RawTechExporter.up + "Techs.json", ""); // CLEAR
                    File.AppendAllLines(export + RawTechExporter.up + "Techs.json", toWrite);
                    toWrite.Clear();

                    foreach (string str in basetypeNames)
                    {
                        toWrite.Add(str + ",");
                    }

                    toWrite.Add("");
                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("---------------- <<< END EXPORTING >>> --------------------");
                    toWrite.Add("-----------------------------------------------------------");
                    File.WriteAllText(export + RawTechExporter.up + "ESpawnBaseTypes.json", ""); // CLEAR
                    File.AppendAllLines(export + RawTechExporter.up + "ESpawnBaseTypes.json", toWrite);

                    File.WriteAllText(export + RawTechExporter.up + "batchNew.json", CommunityCluster.GetLocalToPublic());
                }
                catch { }
            }

            HoriPosOff += ButtonWidth;
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "COM PULL EXISTING</b></color>"))
            {
                try
                {
                    string export = new DirectoryInfo(Application.dataPath).Parent.ToString() + RawTechExporter.up + "MassExport";
                    if (!Directory.Exists(export))
                        Directory.CreateDirectory(export);
                    Dictionary<SpawnBaseTypes, RawTechTemplate> BTs = JsonConvert.DeserializeObject<Dictionary<SpawnBaseTypes, RawTechTemplate>>(CommunityCluster.FetchPublicFromFile());
                    CommunityCluster.Organize(ref BTs);
                    Dictionary<int, RawTechTemplate> BTsInt = BTs.ToList().ToDictionary(x => (int)x.Key, x => x.Value);
                    File.WriteAllText(export + RawTechExporter.up + "batchEdit.json", JsonConvert.SerializeObject(BTsInt, Formatting.Indented, RawTechExporter.JSONDEV));
                }
                catch (Exception e) {
                    Debug.LogError("TAC_AI: ERROR - " + e);
                    ManUI.inst.ShowErrorPopup("TAC_AI: ERROR - " + e); 
                }
            }

            HoriPosOff += ButtonWidth;
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "COM TEST FORMAT</b></color>"))
            {
                try
                {
                    string import = new DirectoryInfo(Application.dataPath).Parent.ToString() + RawTechExporter.up + "MassExport";
                    if (Directory.Exists(import))
                    {
                        string importJSON = import + RawTechExporter.up + "batchEdit.json";
                        if (File.Exists(importJSON))
                        {
                            CommunityCluster.DeployUncompressed(importJSON);
                            TempManager.ValidateAndAddAllInternalTechs(false);
                        }
                        else
                            ManUI.inst.ShowErrorPopup("TAC_AI: ERROR - Please pull existing first.");
                    }
                }
                catch(Exception e) { ManUI.inst.ShowErrorPopup("TAC_AI: ERROR - " + e); }
            }

            HoriPosOff += ButtonWidth;
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }


            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "COM PUSH PUBLIC</b></color>"))
            {
                try
                {
                    string import = new DirectoryInfo(Application.dataPath).Parent.ToString() + RawTechExporter.up + "MassExport";
                    if (Directory.Exists(import))
                    {
                        string importJSON = import + RawTechExporter.up + "batchEdit.json";
                        if (File.Exists(importJSON))
                        {
                            CommunityCluster.DeployUncompressed(importJSON);
                            CommunityCluster.PushDeployedToPublicFile();
                            TempManager.ValidateAndAddAllInternalTechs();
                        }
                        else
                            ManUI.inst.ShowErrorPopup("TAC_AI: ERROR - Please pull existing first.");
                    }
                }
                catch (Exception e) { ManUI.inst.ShowErrorPopup("TAC_AI: ERROR - " + e); }
            }


            HoriPosOff += ButtonWidth;
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }


            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "PURGE DUPLICATES</b></color>"))
            {
                try
                {
                    List<int> exists = new List<int>();
                    foreach (RawTechTemplate bt in TempManager.techBases.Values)
                    {
                        exists.Add(bt.techName.GetHashCode());
                    }

                    int count = listTemp.Count();
                    for (int step = 0; step < count; step++)
                    {
                        RawTechTemplate BT = listTemp[step];
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

            HoriPosOff += ButtonWidth;
            

            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }
            /*
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "PURGE MISSING</b></color>"))
            {
                try
                {
                    int count = listTemp.Count();
                    for (int step = 0; step < count; step++)
                    {
                        RawTechTemplate BT = listTemp[step];
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
            }*/

            HoriPosOff += ButtonWidth;
            

            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), DevCheatNoAttackPlayer ? redStart + "Attack Player Off</b></color>" : redStart + "Attack Player ON</b></color>"))
            {
                DevCheatNoAttackPlayer = !DevCheatNoAttackPlayer;
            }
            HoriPosOff += ButtonWidth;
            

            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }

#endif

            if (listTemp == null || listTemp.Count() == 0)
            {
                if (ShowLocal)
                {
                    if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, ButtonWidth, 30), "There's Nothing In"))
                    {
                        SpawnTech(SpawnBaseTypes.NotAvail);
                    }
                    HoriPosOff += ButtonWidth;

                    if (HoriPosOff >= MaxWindowWidth)
                    {
                        HoriPosOff = 0;
                        VertPosOff += 30;
                        MaxExtensionX = true;
                        if (VertPosOff >= MaxWindowHeight)
                            MaxExtensionY = true;
                    }
                    if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, ButtonWidth, 30), "The Enemies Folder!"))
                    {
                        SpawnTech(SpawnBaseTypes.NotAvail);
                    }
                }
                else
                {
                    if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, ButtonWidth, 30), "None in Mods."))
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
                    RawTechTemplate temp = listTemp[step];
                    if (HoriPosOff >= MaxWindowWidth)
                    {
                        HoriPosOff = 0;
                        VertPosOff += 30;
                        MaxExtensionX = true;
                        if (VertPosOff >= MaxWindowHeight)
                            MaxExtensionY = true;
                    }
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
                    if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), disp))
                    {
                        index = step;
                        clicked = true;
                    }
                    HoriPosOff += ButtonWidth;
                }
                catch { }// error on handling something
            }

            GUI.EndScrollView();
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
        private static void GUIHandlerPreset(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionX = false;
            bool MaxExtensionY = false;
            SpawnBaseTypes type = SpawnBaseTypes.NotAvail;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 20, HotWindow.height -40), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "PURGE ENEMIES</b></color>"))
            {
                DebugTAC_AI.Log("TACtical AI: RemoveAllEnemies - CALLED");
                RemoveAllEnemies();
            }
            HoriPosOff += ButtonWidth;
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), InstantLoad ? redStart + "Instant ON</b></color>" : redStart + "Instant Off</b></color>"))
            {
                InstantLoad = !InstantLoad;
            }
            HoriPosOff += ButtonWidth;
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), ShowDebugFeedBack ? redStart + "Hide AI Debug</b></color>" : redStart + "Show AI Debug</b></color>"))
            {
                ShowDebugFeedBack = !ShowDebugFeedBack;
            }
            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "SPAWN Priced</b></color>"))
            {
                RawTechLoader.SpawnRandomTechAtPosHead(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), terrainType: BaseTerrain.Any, maxPrice: KickStart.EnemySpawnPriceMatching);
            }
            HoriPosOff += ButtonWidth;

            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }
#if DEBUG
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), DevCheatNoAttackPlayer ? redStart + "Attack Player Off</b></color>" : redStart + "Attack Player ON</b></color>"))
            {
                DevCheatNoAttackPlayer = !DevCheatNoAttackPlayer;
            }
            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), DevCheatPlayerEnemyBaseTeam ? redStart + "ENEMY Team</b></color>" : redStart + "Player Team</b></color>"))
            {
                DevCheatPlayerEnemyBaseTeam = !DevCheatPlayerEnemyBaseTeam;
            }
            HoriPosOff += ButtonWidth;
#endif
            FactionTypesExt currentFaction = FactionTypesExt.NULL;
            string disp;
            foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> temp in TempManager.techBases)
            {
                if (HoriPosOff >= MaxWindowWidth)
                {
                    HoriPosOff = 0;
                    VertPosOff += 30;
                    MaxExtensionX = true;
                    if (VertPosOff >= MaxWindowHeight)
                        MaxExtensionY = true;
                }
                if (currentFaction != temp.Value.faction)
                {
                    currentFaction = temp.Value.faction;

                    if (HoriPosOff > 0)
                    {
                        VertPosOff += 30;
                        HoriPosOff = 0;
                    }
                    FactionSubTypes FST = KickStart.CorpExtToCorp(temp.Value.faction);
                    if (FST == FactionSubTypes.EXP)
                        disp = "RR";
                    else if (KickStart.IsFactionExtension(temp.Value.faction))
                        disp = temp.Value.faction.ToString();
                    else if (ManMods.inst.IsModdedCorp(FST))
                        disp = ManMods.inst.FindCorpShortName(FST);
                    else
                        disp = temp.Value.faction.ToString();
                    if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth * MaxCountWidth, 30), "<b>" + disp + "</b>"))
                    {
                        if (openedFactions.Contains(currentFaction))
                            openedFactions.Remove(currentFaction);
                        else
                            openedFactions.Add(currentFaction);
                    }
                    MaxExtensionX = true;
                    VertPosOff += 30;
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
                                disp = "<color=#90ee90ff>" + temp.Key.ToString() + "</color>";
                                break;
                            case BaseTerrain.Air:
                                disp = "<color=#ffa500ff>" + temp.Key.ToString() + "</color>";
                                break;
                            case BaseTerrain.Sea:
                                disp = "<color=#add8e6ff>" + temp.Key.ToString() + "</color>";
                                break;
                            case BaseTerrain.Space:
                                disp = "<color=#ffff00ff>" + temp.Key.ToString() + "</color>";
                                break;
                            default:
                                disp = temp.Key.ToString();
                                break;
                        }
                    }
                    else
                        disp = temp.Key.ToString();

                    if (temp.Value.purposes.Contains(BasePurpose.NANI))
                    {
                        disp = "[E] " + disp;
                    }
                    if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), disp))
                    {
                        type = temp.Key;
                        clicked = true;
                    }
                    HoriPosOff += ButtonWidth;
                }
            }
            GUI.EndScrollView();
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

        public static void RemoveTechLocal(int index)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);

            if (ShowLocal)
                TempManager.ExternalEnemyTechsLocal.RemoveAt(index);
            else
               TempManager.ExternalEnemyTechsMods.RemoveAt(index);
        }
        public static void SpawnTechLocal(List<RawTechTemplate> temps, int index)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);

            RawTechTemplate val = temps[index];
            Tank tank = null;

            if (val.purposes.Contains(BasePurpose.NotStationary))
            {
                tank = RawTechLoader.SpawnMobileTechPrefab(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), val, pop: true);
            }
            else
            {
                if (InstantLoad)
                {
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
                        RawTechLoader.SpawnBase(GetPlayerPos(), Vector3.forward, team, val, true, ExtraBB: extraBB);
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

            if (TempManager.techBases.TryGetValue(type, out RawTechTemplate val))
            {
                Tank tank = null;
                if (val.purposes.Contains(BasePurpose.NotStationary))
                {
                    tank = RawTechLoader.SpawnMobileTechPrefab(GetPlayerPos(), GetPlayerForward(), AIGlobals.GetRandomBaseTeam(), type, pop: true);
                }
                else
                {
                    if (InstantLoad)
                    {
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
                            RawTechLoader.SpawnBase(GetPlayerPos(), team, type, true, extraBB);
                            Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                        }
                        else
                            RawTechLoader.SpawnBase(GetPlayerPos(), AIGlobals.GetRandomBaseTeam(), type, true);
                    }
                }
                if (tank)
                    RawTechLoader.ChargeAndClean(tank);
            }

            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
        }

        public static void RemoveAllEnemies()
        {
            try
            {
                List<Tank> toRemove = new List<Tank>();
                foreach (var item in Singleton.Manager<ManTechs>.inst.IterateTechsWhere(x => x.visible.isActive &&
                (AIGlobals.IsBaseTeam(x.Team) || x.IsPopulation) && x.name != "DPS Target"))
                {
                    toRemove.Add(item);
                }
                foreach (var tech in toRemove)
                {
                    SpecialAISpawner.Purge(tech);
                }
            }
            catch { }
        }

        public static void RemoveOrphanTrackedVisibles()
        {
            try
            {
                List<TrackedVisible> toDestroy = ManVisible.inst.AllTrackedVisibles.ToList();
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

                        if (AIGlobals.IsBaseTeam(remove.TeamID) && (remove.visible == null || (remove.visible != null && !remove.visible.isActive)))
                        {
                            DebugTAC_AI.Log("TACtical_AI: RemoveOrphanTrackedVisibles - iterating " + remove.TeamID + " | " 
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
                    DebugTAC_AI.Log("TACtical_AI: RemoveOrphanTrackedVisibles - removed " + removed);
            }
            catch { }
        }

        public static void LaunchSubMenuClickable()
        {
            if (!UIIsCurrentlyOpen)
            {
                RawTechExporter.ReloadExternal();
                DebugTAC_AI.Log("TACtical_AI: Opened Raw Techs Debug menu!");
                UIIsCurrentlyOpen = true;
                GUIWindow.SetActive(true);
            }
        }
        public static void CloseSubMenuClickable()
        {
            if (UIIsCurrentlyOpen)
            {
                UIIsCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                DebugTAC_AI.Log("TACtical_AI: Closed Raw Techs Debug menu!");
            }
        }


        private void Update()
        {
            if (CanOpenDebugSpawnMenu)
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    if (Input.GetKey(KeyCode.Y))
                    {
                        toggleDebugLock = false;
                        menu = DebugMenus.Local;
                        LaunchSubMenuClickable();
                    }
                    else if (Input.GetKey(KeyCode.U))
                    {
                        toggleDebugLock = false;
                        menu = DebugMenus.Prefabs;
                        LaunchSubMenuClickable();
                    }
                    else if (Input.GetKeyDown(KeyCode.Backspace))
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
            else
            {
                CloseSubMenuClickable();
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Plus))
                    DestroyAllInvalidVisibles();
            }
        }

        internal static void DestroyAllInvalidVisibles()
        {
            foreach (var item in new List<TrackedVisible>(ManVisible.inst.AllTrackedVisibles))
            {
                if (item == null)
                    continue;
                if (item.ObjectType == ObjectTypes.Vehicle)
                {
                    if (ManWorld.inst.TileManager.IsTileAtPositionLoaded(item.Position))
                    {
                        if (item.wasDestroyed || item.visible == null)
                        {
                            if (AIGlobals.IsBaseTeam(item.TeamID))
                            {
                                DebugTAC_AI.Log("  Invalid Base Team Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                                ManVisible.inst.StopTrackingVisible(item.ID);
                            }
                            else
                                DebugTAC_AI.Log("  Invalid Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                        }
                        else
                            DebugTAC_AI.Log("  Not Destroyed Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                    }
                    else
                        DebugTAC_AI.Log("  Other Tech visible " + item.ID + ",  Team " + item.TeamID + ",  Destroyed " + item.wasDestroyed);
                }
            }
        }


        // Utilities
#if DEBUG
        internal static bool ShowDebugFeedBack = true;
#else
        internal static bool ShowDebugFeedBack = false;
#endif
        private static GameObject DirPrefab = null;
        private static void InsureDirPrefab()
        {
            if (DirPrefab != null)
                return;
            DirPrefab = Instantiate(new GameObject("DebugLine"), null, false);
            var trans = DirPrefab.transform;
            trans.rotation = Quaternion.identity;
            trans.position = Vector3.zero;
            trans.localScale = Vector3.one;

            var lr = trans.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = DirPrefab.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startWidth = 0.5f;
                lr.useWorldSpace = false;
            }
            lr.startColor = Color.green;
            lr.endColor = Color.green;
            Vector3[] vecs = new Vector3[2] { Vector3.zero, Vector3.up };
            lr.SetPositions(vecs);
            DirPrefab.SetActive(false);
        }
        /// <summary>
        /// endPosGlobal is GLOBAL ROTATION in relation to local tech.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="num"></param>
        /// <param name="endPosGlobal"></param>
        /// <param name="color"></param>
        internal static void DrawDirIndicator(GameObject obj, int num, Vector3 endPosGlobalSpaceOffset, Color color)
        {
            if (!ShowDebugFeedBack || !CanOpenDebugSpawnMenu)
                return;
            GameObject gO;
            var line = obj.transform.Find("DebugLine " + num);
            if (!(bool)line)
            { 
                gO = Instantiate(new GameObject("DebugLine " + num), obj.transform, false);
            }
            else
                gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startWidth = 0.5f;
            }
            lr.startColor = color;
            lr.endColor = color;
            Vector3 pos = obj.transform.position;
            Vector3[] vecs = new Vector3[2] { pos, endPosGlobalSpaceOffset + pos };
            lr.SetPositions(vecs);
            Destroy(gO, Time.deltaTime);
        }
        public static void DrawDirIndicator(Vector3 startPos, Vector3 endPos, Color color, float decayTime = 0)
        {
            if (!ShowDebugFeedBack || !CanOpenDebugSpawnMenu)
                return;
            InsureDirPrefab();
            GameObject gO;
            gO = Instantiate(DirPrefab, null);

            var lr = gO.GetComponent<LineRenderer>();
            lr.startColor = color;
            lr.endColor = color;
            gO.transform.position = startPos;
            Vector3[] vecs = new Vector3[2] { Vector3.zero, gO.transform.InverseTransformPoint(endPos) };
            lr.SetPositions(vecs);
            gO.SetActive(true);
            Destroy(gO, (decayTime <= 0) ? Time.deltaTime : decayTime);
            //DebugTAC_AI.Log("SPAWN DrawDirIndicator(World)");
        }
        private const int circleEdgeCount = 32;
        public static void DrawDirIndicatorCircle(Vector3 center, Vector3 normal, Vector3 flat, float radius, Color color, float decayTime = 0)
        {
            if (!ShowDebugFeedBack || !CanOpenDebugSpawnMenu)
                return;
            InsureDirPrefab();
            GameObject gO;
            gO = Instantiate(DirPrefab, null);

            var lr = gO.GetComponent<LineRenderer>();
            lr.startColor = color;
            lr.endColor = color;
            gO.transform.position = center;
            gO.transform.rotation = Quaternion.identity;
            lr.positionCount = circleEdgeCount + 1;
            Vector3[] vecs = new Vector3[circleEdgeCount + 1];
            for (int step = 0; step <= circleEdgeCount; step++)
            {
                vecs[step] = Quaternion.AngleAxis(360 * ((float)step / circleEdgeCount), normal) * flat * radius;
            }
            lr.SetPositions(vecs);
            gO.SetActive(true);
            Destroy(gO, (decayTime <= 0) ? Time.deltaTime : decayTime);
            //DebugTAC_AI.Log("SPAWN DrawDirIndicator(World)");
        }
        public static void DrawDirIndicatorSphere(Vector3 center, float radius, Color color, float decayTime = 0)
        {
            DrawDirIndicatorCircle(center, Vector3.up, Vector3.forward, radius, color, decayTime);
            DrawDirIndicatorCircle(center, Vector3.right, Vector3.forward, radius, color, decayTime);
            DrawDirIndicatorCircle(center, Vector3.forward, Vector3.up, radius, color, decayTime);
        }
        public static void DrawDirIndicatorRecPriz(Vector3 center, Vector3 size, Color color, float decayTime = 0)
        {
            Vector3 sizeCons = size / 2;
            Vector3 fruCorner = new Vector3(sizeCons.x, sizeCons.y, sizeCons.z);
            Vector3 frdCorner = new Vector3(sizeCons.x, -sizeCons.y, sizeCons.z);
            Vector3 rruCorner = new Vector3(sizeCons.x, sizeCons.y, -sizeCons.z);
            Vector3 rrdCorner = new Vector3(sizeCons.x, -sizeCons.y, -sizeCons.z);
            Vector3 fluCorner = new Vector3(-sizeCons.x, sizeCons.y, sizeCons.z);
            Vector3 fldCorner = new Vector3(-sizeCons.x, -sizeCons.y, sizeCons.z);
            Vector3 rluCorner = new Vector3(-sizeCons.x, sizeCons.y, -sizeCons.z);
            Vector3 rldCorner = new Vector3(-sizeCons.x, -sizeCons.y, -sizeCons.z);

            DrawDirIndicator(center + fruCorner, center + frdCorner, color, decayTime);
            DrawDirIndicator(center + fluCorner, center + fldCorner, color, decayTime);
            DrawDirIndicator(center + fruCorner, center + fluCorner, color, decayTime);
            DrawDirIndicator(center + frdCorner, center + fldCorner, color, decayTime);

            DrawDirIndicator(center + fruCorner, center + rruCorner, color, decayTime);
            DrawDirIndicator(center + fluCorner, center + rluCorner, color, decayTime);
            DrawDirIndicator(center + frdCorner, center + rrdCorner, color, decayTime);
            DrawDirIndicator(center + fldCorner, center + rldCorner, color, decayTime);

            DrawDirIndicator(center + rruCorner, center + rrdCorner, color, decayTime);
            DrawDirIndicator(center + rluCorner, center + rldCorner, color, decayTime);
            DrawDirIndicator(center + rruCorner, center + rluCorner, color, decayTime);
            DrawDirIndicator(center + rrdCorner, center + rldCorner, color, decayTime);
        }
        public static void DrawDirIndicatorRecPriz(Vector3 center, Quaternion rotation, Vector3 size, Color color, float decayTime = 0)
        {
            Vector3 sizeCons = size / 2;
            Vector3 fruCorner = rotation * new Vector3(sizeCons.x, sizeCons.y, sizeCons.z);
            Vector3 frdCorner = rotation * new Vector3(sizeCons.x, -sizeCons.y, sizeCons.z);
            Vector3 rruCorner = rotation * new Vector3(sizeCons.x, sizeCons.y, -sizeCons.z);
            Vector3 rrdCorner = rotation * new Vector3(sizeCons.x, -sizeCons.y, -sizeCons.z);
            Vector3 fluCorner = rotation * new Vector3(-sizeCons.x, sizeCons.y, sizeCons.z);
            Vector3 fldCorner = rotation * new Vector3(-sizeCons.x, -sizeCons.y, sizeCons.z);
            Vector3 rluCorner = rotation * new Vector3(-sizeCons.x, sizeCons.y, -sizeCons.z);
            Vector3 rldCorner = rotation * new Vector3(-sizeCons.x, -sizeCons.y, -sizeCons.z);

            DrawDirIndicator(center + fruCorner, center + frdCorner, color, decayTime);
            DrawDirIndicator(center + fluCorner, center + fldCorner, color, decayTime);
            DrawDirIndicator(center + fruCorner, center + fluCorner, color, decayTime);
            DrawDirIndicator(center + frdCorner, center + fldCorner, color, decayTime);

            DrawDirIndicator(center + fruCorner, center + rruCorner, color, decayTime);
            DrawDirIndicator(center + fluCorner, center + rluCorner, color, decayTime);
            DrawDirIndicator(center + frdCorner, center + rrdCorner, color, decayTime);
            DrawDirIndicator(center + fldCorner, center + rldCorner, color, decayTime);

            DrawDirIndicator(center + rruCorner, center + rrdCorner, color, decayTime);
            DrawDirIndicator(center + rluCorner, center + rldCorner, color, decayTime);
            DrawDirIndicator(center + rruCorner, center + rluCorner, color, decayTime);
            DrawDirIndicator(center + rrdCorner, center + rldCorner, color, decayTime);
        }
        public static void DrawDirIndicatorRecPrizExt(Vector3 center, Vector3 extents, Color color, float decayTime = 0)
        {
            Vector3 fruCorner = new Vector3(extents.x, extents.y, extents.z);
            Vector3 frdCorner = new Vector3(extents.x, -extents.y, extents.z);
            Vector3 rruCorner = new Vector3(extents.x, extents.y, -extents.z);
            Vector3 rrdCorner = new Vector3(extents.x, -extents.y, -extents.z);
            Vector3 fluCorner = new Vector3(-extents.x, extents.y, extents.z);
            Vector3 fldCorner = new Vector3(-extents.x, -extents.y, extents.z);
            Vector3 rluCorner = new Vector3(-extents.x, extents.y, -extents.z);
            Vector3 rldCorner = new Vector3(-extents.x, -extents.y, -extents.z);

            DrawDirIndicator(center + fruCorner, center + frdCorner, color, decayTime);
            DrawDirIndicator(center + fluCorner, center + fldCorner, color, decayTime);
            DrawDirIndicator(center + fruCorner, center + fluCorner, color, decayTime);
            DrawDirIndicator(center + frdCorner, center + fldCorner, color, decayTime);

            DrawDirIndicator(center + fruCorner, center + rruCorner, color, decayTime);
            DrawDirIndicator(center + fluCorner, center + rluCorner, color, decayTime);
            DrawDirIndicator(center + frdCorner, center + rrdCorner, color, decayTime);
            DrawDirIndicator(center + fldCorner, center + rldCorner, color, decayTime);

            DrawDirIndicator(center + rruCorner, center + rrdCorner, color, decayTime);
            DrawDirIndicator(center + rluCorner, center + rldCorner, color, decayTime);
            DrawDirIndicator(center + rruCorner, center + rluCorner, color, decayTime);
            DrawDirIndicator(center + rrdCorner, center + rldCorner, color, decayTime);
        }

        private static bool CheckValidMode()
        {
#if DEBUG
            return true;
#else
            if (KickStart.enablePainMode && (Singleton.Manager<ManGameMode>.inst.IsCurrent<ModeMisc>() || (Singleton.Manager<ManGameMode>.inst.IsCurrent<ModeCoOpCreative>() && ManNetwork.IsHost)))
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

        internal static void Organize(ref List<RawTechTemplate> list)
        {
            list = list.OrderBy(x => x.terrain)
                .ThenBy(x => x.purposes.Contains(BasePurpose.NotStationary))
                .ThenBy(x => x.purposes.Contains(BasePurpose.NANI))
                .ThenBy(x => x.techName).ToList();
        }
        internal static void OrganizeDeploy(ref List<RawTechTemplate> list)
        {
            list = list.OrderBy(x => x.faction).ThenBy(x => x.terrain)
                .ThenBy(x => x.purposes.Contains(BasePurpose.NotStationary))
                .ThenBy(x => x.purposes.Contains(BasePurpose.NANI))
                .ThenBy(x => x.techName).ToList();
        }
    }
}
