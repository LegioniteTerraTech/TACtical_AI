using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;
using System.IO;
using Newtonsoft.Json;


namespace TAC_AI.Templates
{
    internal class DebugRawTechSpawner : MonoBehaviour
    {
        private static readonly bool Enabled = true;

        internal static bool IsCurrentlyEnabled = false;

        private static Vector3 PlayerLoc = Vector3.zero;
        private static Vector3 PlayerFow = Vector3.forward;
        private static bool isCurrentlyOpen = false;
        private static bool isPrefabs = false;
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
                Debug.Log("TACtical_AI: Raw Techs Debugger launched (DEV)");
            #else
                Debug.Log("TACtical_AI: Raw Techs Debugger launched");
            #endif

            Instantiate(new GameObject()).AddComponent<DebugRawTechSpawner>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplayTechLoader>();
            GUIWindow.SetActive(false);
        }
        public static void ShouldBeActive()
        {
            IsCurrentlyEnabled = CheckValidMode();
        }



        internal class GUIDisplayTechLoader : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isCurrentlyOpen && KickStart.CanUseMenu)
                {
                    if (!isPrefabs)
                    {
                        HotWindow = GUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerPlayer, "<b>Debug Local Spawns</b>");
                    }
                    else
                    {
                        HotWindow = GUI.Window(RawTechSpawnerID, HotWindow, GUIHandlerPreset, "<b>Debug Prefab Spawns</b>");
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


        private static void GUIHandlerPlayer(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionX = false;
            bool MaxExtensionY = false;
            int index = 0;

            List<BaseTemplate> listTemp;
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
                    listTemp = listTemp.OrderBy(x => x.terrain).ThenBy(x => x.purposes.Contains(BasePurpose.NotStationary)).ThenBy(x => x.techName).ToList();
                }
                catch { }
            }


            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), InstantLoad ? redStart + "Instant ON</b></color>" : redStart + "Instant Off</b></color>"))
            {
                InstantLoad = !InstantLoad;
            }

            HoriPosOff += ButtonWidth;

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), ShowDebugNaviLines ? redStart + "Hide Paths</b></color>" : redStart + "Show Paths</b></color>"))
            {
                ShowDebugNaviLines = !ShowDebugNaviLines;
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
                        listTemp = listTemp.OrderBy(x => x.terrain).ThenBy(x => x.purposes.Contains(BasePurpose.NotStationary)).ThenBy(x => x.techName).ToList();

                        RawTechExporter.ValidateEnemyFolder();
                        string export = RawTechExporter.RawTechsDirectory + RawTechExporter.up + "Bundled";
                        Directory.CreateDirectory(export);

                        RawTechExporter.MakeExternalRawTechListFile(export + RawTechExporter.up + "RawTechs.RTList", listTemp);
                    }
                    catch { }
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

#if DEBUG
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "LOCAL TO COM</b></color>"))
            {
                try
                {
                    listTemp = listTemp.OrderBy(x => x.terrain).ThenBy(x => x.purposes.Contains(BasePurpose.NotStationary)).ThenBy(x => x.techName).ToList();

                    string export = new DirectoryInfo(Application.dataPath).Parent.ToString() + RawTechExporter.up + "MassExport";
                    Directory.CreateDirectory(export);

                    List<string> toWrite = new List<string>();

                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("--------------- <<< MASS EXPORTING >>> --------------------");
                    toWrite.Add("-----------------------------------------------------------");
                    toWrite.Add("");

                    List<string> basetypeNames = new List<string>();
                    StringBuilder SB = new StringBuilder();
                    foreach (BaseTemplate BT in listTemp)
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
                        toWrite.Add("    purposes = new List<BasePurpose>{ " + SB.ToString() + "},");
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
                    Dictionary<SpawnBaseTypes, BaseTemplate> BTs = JsonConvert.DeserializeObject<Dictionary<SpawnBaseTypes, BaseTemplate>>(CommunityCluster.FetchPublicFromFile());
                    CommunityCluster.Organize(ref BTs);
                    Dictionary<int, BaseTemplate> BTsInt = BTs.ToList().ToDictionary(x => (int)x.Key, x => x.Value);
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
                    foreach (BaseTemplate bt in TempManager.techBases.Values)
                    {
                        exists.Add(bt.techName.GetHashCode());
                    }

                    int count = listTemp.Count();
                    for (int step = 0; step < count; step++)
                    {
                        BaseTemplate BT = listTemp[step];
                        if (exists.Contains(BT.techName.GetHashCode()))
                        {
                            listTemp.Remove(BT);
                            count--;
                            step--;
                        }
                    }
                    Debug.Log("-----------------------------------------------------------");
                    Debug.Log("----------------- <<< END PURGING >>> ---------------------");
                    Debug.Log("-----------------------------------------------------------");
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

            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), redStart + "PURGE MISSING</b></color>"))
            {
                try
                {
                    int count = listTemp.Count();
                    for (int step = 0; step < count; step++)
                    {
                        BaseTemplate BT = listTemp[step];
                        if (BT.IsMissingBlocks())
                        {
                            listTemp.Remove(BT);
                            count--;
                            step--;
                        }
                    }
                    Debug.Log("-----------------------------------------------------------");
                    Debug.Log("----------------- <<< END PURGING >>> ---------------------");
                    Debug.Log("-----------------------------------------------------------");
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
                    BaseTemplate temp = listTemp[step];
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
                SpawnTechLocal(index);
            }

            GUI.DragWindow();

            if (ShowLocal)
                TempManager.ExternalEnemyTechsLocal = listTemp;
            else
                TempManager.ExternalEnemyTechsMods = listTemp;
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
                RemoveAllEnemies();
            }
            HoriPosOff += ButtonWidth;
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), InstantLoad ? redStart + "Instant ON</b></color>" : redStart + "Instant Off</b></color>"))
            {
                InstantLoad = !InstantLoad;
            }
            HoriPosOff += ButtonWidth;
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), ShowDebugNaviLines ? redStart + "Hide Paths</b></color>" : redStart + "Show Paths</b></color>"))
            {
                ShowDebugNaviLines = !ShowDebugNaviLines;
            }
            HoriPosOff += ButtonWidth;
#if DEBUG
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
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), DevCheatPlayerEnemyBaseTeam ? redStart + "ENEMY Team</b></color>" : redStart + "Player Team</b></color>"))
            {
                DevCheatPlayerEnemyBaseTeam = !DevCheatPlayerEnemyBaseTeam;
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
            FactionTypesExt currentFaction = FactionTypesExt.NULL;
            string disp;
            foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> temp in TempManager.techBases)
            {
                if (currentFaction != temp.Value.faction)
                {
                    currentFaction = temp.Value.faction;
                    HoriPosOff = 0;
                    VertPosOff += 60;
                    FactionSubTypes FST = KickStart.CorpExtToCorp(temp.Value.faction);
                    if (FST == FactionSubTypes.EXP)
                        disp = "RR";
                    else if (KickStart.IsFactionExtension(temp.Value.faction))
                        disp = temp.Value.faction.ToString();
                    else if (ManMods.inst.IsModdedCorp(FST))
                        disp = ManMods.inst.FindCorpShortName(FST);
                    else
                        disp = temp.Value.faction.ToString();
                    if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff - 30, ButtonWidth * MaxCountWidth, 30), "<b>" + disp + "</b>"))
                    {
                        if (openedFactions.Contains(currentFaction))
                            openedFactions.Remove(currentFaction);
                        else
                            openedFactions.Add(currentFaction);
                    }
                    MaxExtensionX = true;
                    if (VertPosOff >= MaxWindowHeight)
                        MaxExtensionY = true;
                }
                else if (HoriPosOff >= MaxWindowWidth)
                {
                    HoriPosOff = 0;
                    VertPosOff += 30;
                    MaxExtensionX = true;
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

        public static void SpawnTechLocal(int index)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);

            BaseTemplate val = TempManager.ExternalEnemyTechsAll[index];
            Tank tank = null;

            if (val.purposes.Contains(BasePurpose.NotStationary))
            {
                tank = RawTechLoader.SpawnMobileTech(GetPlayerPos(), GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), val, pop: true);
            }
            else
            {
                if (InstantLoad)
                {
                    if (val.purposes.Contains(BasePurpose.Defense))
                        tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), val, false);
                    else if (val.purposes.Contains(BasePurpose.Headquarters))
                    {
                        int team = RawTechLoader.GetRandomBaseTeam();
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
                        tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(),GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), val, true);
                }
                else
                {

                    if (val.purposes.Contains(BasePurpose.Defense))
                        RawTechLoader.SpawnBase(GetPlayerPos(), GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), val, false);
                    else if (val.purposes.Contains(BasePurpose.Headquarters))
                    {
                        int extraBB = 0;
                        int team = RawTechLoader.GetRandomBaseTeam();
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
                        tank = RawTechLoader.GetSpawnBase(GetPlayerPos(), GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), val, true);
                }
            }
            if (tank)
                RawTechLoader.ChargeAndClean(tank);

        }
        public static void SpawnTech(SpawnBaseTypes type)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);

            if (TempManager.techBases.TryGetValue(type, out BaseTemplate val))
            {
                Tank tank = null;
                if (val.purposes.Contains(BasePurpose.NotStationary))
                {
                    tank = RawTechLoader.SpawnMobileTech(GetPlayerPos(), GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), type, pop: true);
                }
                else
                {
                    if (InstantLoad)
                    {
                        if (val.purposes.Contains(BasePurpose.Defense))
                            tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), type, false);
                        else if (val.purposes.Contains(BasePurpose.Headquarters))
                        {
                            int team = RawTechLoader.GetRandomBaseTeam();
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
                            tank = RawTechLoader.SpawnBaseInstant(GetPlayerPos(), GetPlayerForward(), RawTechLoader.GetRandomBaseTeam(), type, true);
                    }
                    else
                    {
                        if (val.purposes.Contains(BasePurpose.Defense))
                            RawTechLoader.SpawnBase(GetPlayerPos(), RawTechLoader.GetRandomBaseTeam(), type, false);
                        else if (val.purposes.Contains(BasePurpose.Headquarters))
                        {
                            int team = RawTechLoader.GetRandomBaseTeam();
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
                            RawTechLoader.SpawnBase(GetPlayerPos(), RawTechLoader.GetRandomBaseTeam(), type, true);
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
                int techCount = Singleton.Manager<ManTechs>.inst.CurrentTechs.Count();
                for (int step = 0; step < techCount; step++)
                {
                    Tank tech = Singleton.Manager<ManTechs>.inst.CurrentTechs.ElementAt(step);
                    if ((RawTechLoader.IsBaseTeam(tech.Team) || tech.Team == -1 || tech.Team == 1) && tech.visible.isActive && tech.name != "DPS Target")
                    {
                        SpecialAISpawner.Purge(tech);
                        techCount--;
                        step--;
                    }
                }
            }
            catch { }
        }

        public static void LaunchSubMenuClickable()
        {
            if (!isCurrentlyOpen)
            {
                RawTechExporter.ReloadExternal();
                Debug.Log("TACtical_AI: Opened Raw Techs Debug menu!");
                isCurrentlyOpen = true;
                GUIWindow.SetActive(true);
            }
        }
        public static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl(RawTechSpawnerID);
                Debug.Log("TACtical_AI: Closed Raw Techs Debug menu!");
            }
        }


        private void Update()
        {
            if (IsCurrentlyEnabled)
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    if (Input.GetKey(KeyCode.Y))
                    {
                        toggleDebugLock = false;
                        isPrefabs = false;
                        LaunchSubMenuClickable();
                    }
                    else if (Input.GetKey(KeyCode.U))
                    {
                        toggleDebugLock = false;
                        isPrefabs = true;
                        LaunchSubMenuClickable();
                    }
                    else if (Input.GetKeyDown(KeyCode.Minus))
                    {
                        if (isPrefabs == false || !toggleDebugLock)
                        {
                            toggleDebugLock = !toggleDebugLock;
                        }
                        isPrefabs = false;
                        if (toggleDebugLock)
                            LaunchSubMenuClickable();
                    }
                    else if (Input.GetKeyDown(KeyCode.Equals))
                    {
                        if (isPrefabs == true || !toggleDebugLock)
                        {
                            toggleDebugLock = !toggleDebugLock;
                        }
                        isPrefabs = true;
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
            }
        }


        // Utilities
#if DEBUG
        internal static bool ShowDebugNaviLines = true;
#else
        internal static bool ShowDebugNaviLines = false;
#endif
        /// <summary>
        /// endPosGlobal is GLOBAL ROTATION in relation to local tech.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="num"></param>
        /// <param name="endPosGlobal"></param>
        /// <param name="color"></param>
        internal static void DrawDirIndicator(GameObject obj, int num, Vector3 endPosGlobalSpaceOffset, Color color)
        {
            if (!ShowDebugNaviLines || !IsCurrentlyEnabled)
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

    }
}
