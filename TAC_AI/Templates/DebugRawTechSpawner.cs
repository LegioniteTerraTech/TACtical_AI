using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;


namespace TAC_AI.Templates
{
    internal class DebugRawTechSpawner : MonoBehaviour
    {
        private static bool Enabled = true;
        private static bool EnabledAllModes = false;

        private static bool IsCurrentlyEnabled = false;

        private static Vector3 PlayerLoc = Vector3.zero;
        private static bool isCurrentlyOpen = false;
        private static bool isPrefabs = false;
        private static bool toggleDebugLock = false;

        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"


        public static void Initiate()
        {
            if (!Enabled)
                return;

            #if DEBUG
                Debug.Log("TACtical_AI: Raw Techs Debugger launched (DEV)");
                EnabledAllModes = true;
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
                if (isCurrentlyOpen)
                {
                    if (!isPrefabs)
                    {
                        HotWindow = GUI.Window(8002, HotWindow, GUIHandlerPlayer, "<b>Debug Local Spawns</b>");
                    }
                    else
                    {
                        HotWindow = GUI.Window(8002, HotWindow, GUIHandlerPreset, "<b>Debug Prefab Spawns</b>");
                    }
                }
            }
        }

        private static Vector2 scrolll = new Vector2(0, 0);
        private static float scrolllSize = 50;
        private const int ButtonWidth = 200;
        private const int MaxCountWidth = 4;
        private const int MaxWindowHeight = 500;
        private static int MaxWindowWidth = MaxCountWidth * ButtonWidth;
        private static void GUIHandlerPlayer(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionX = false;
            bool MaxExtensionY = false;
            int index = 0;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 40, HotWindow.height), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), "<color=#f23d3dff><b>PURGE ENEMIES</b></color>"))
            {
                try
                {
                    int techCount = Singleton.Manager<ManTechs>.inst.CurrentTechs.Count();
                    for (int step = 0; step < techCount; step++)
                    {
                        Tank tech = Singleton.Manager<ManTechs>.inst.CurrentTechs.ElementAt(step);
                        if (tech.IsEnemy() && tech.visible.isActive && tech.name != "DPS Target")
                        {
                            SpecialAISpawner.Purge(tech);
                            techCount--;
                            step--;
                        }
                    }
                }
                catch { }
            }

            HoriPosOff += ButtonWidth;

            if (TempManager.ExternalEnemyTechs == null)
            {
                if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, ButtonWidth, 30), "There's Nothing In"))
                {
                    SpawnTech(SpawnBaseTypes.NotAvail);
                }
                HoriPosOff += ButtonWidth;
                if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, ButtonWidth, 30), "The Enemies Folder!"))
                {
                    SpawnTech(SpawnBaseTypes.NotAvail);
                }
                return;
            }

            int Entries = TempManager.ExternalEnemyTechs.Count();
            for (int step = 0; step < Entries; step++)
            {
                try
                {
                    BaseTemplate temp = TempManager.ExternalEnemyTechs[step];
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
        }
        private static void GUIHandlerPreset(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionX = false;
            bool MaxExtensionY = false;
            SpawnBaseTypes type = SpawnBaseTypes.NotAvail;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 40, HotWindow.height), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), "<color=#f23d3dff><b>PURGE ENEMIES</b></color>"))
            {
                try
                {
                    int techCount = Singleton.Manager<ManTechs>.inst.CurrentTechs.Count();
                    for (int step = 0; step < techCount; step++)
                    {
                        Tank tech = Singleton.Manager<ManTechs>.inst.CurrentTechs.ElementAt(step);
                        if (tech.IsEnemy() && tech.visible.isActive)
                        {
                            SpecialAISpawner.Purge(tech);
                            techCount--;
                            step--;
                        }
                    }
                }
                catch { }
            }
            HoriPosOff += ButtonWidth;
            foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> temp in TempManager.techBases)
            {
                if (HoriPosOff >= MaxWindowWidth)
                {
                    HoriPosOff = 0;
                    VertPosOff += 30;
                    MaxExtensionX = true;
                    if (VertPosOff >= MaxWindowHeight)
                        MaxExtensionY = true;
                }
                string disp;
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

                if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), disp))
                {
                    type = temp.Key;
                    clicked = true;
                }
                HoriPosOff += ButtonWidth;
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


            BaseTemplate val = TempManager.ExternalEnemyTechs[index];
            if (val.purposes.Contains(BasePurpose.NotStationary))
            {
                RawTechLoader.SpawnEnemyTechExt(GetPlayerPos(), -1, Vector3.forward, val);
            }
            else
            {
                RawTechLoader.SpawnEnemyTechExt(GetPlayerPos(), -1, Vector3.forward, val);
                /*
                if (val.purposes.Contains(BasePurpose.Defense))
                    RawTechLoader.spa(GetPlayerPos(), -1, type, false);
                if (val.purposes.Contains(BasePurpose.Headquarters))
                {
                    int extraBB = 0;
                    SpawnBaseTypes type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                    if (TempManager.techBases.TryGetValue(type2, out _))
                    {
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() + (Vector3.forward * 64), 90, type2, false);
                    }
                    type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                    if (TempManager.techBases.TryGetValue(type2, out _))
                    {
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() - (Vector3.forward * 64), 90, type2, false);
                    }
                    type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                    if (TempManager.techBases.TryGetValue(type2, out _))
                    {
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() + (Vector3.right * 64), 90, type2, false);
                    }
                    type2 = RawTechLoader.GetEnemyBaseType(val.faction, BasePurpose.Defense, val.terrain);
                    if (TempManager.techBases.TryGetValue(type2, out _))
                    {
                        extraBB += RawTechLoader.SpawnBase(GetPlayerPos() - (Vector3.right * 64), 90, type2, false);
                    }
                    RawTechLoader.SpawnBase(GetPlayerPos(), 90, type, true, extraBB);
                    Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                }
                else
                    RawTechLoader.SpawnBase(GetPlayerPos(), -1, type, true);
                */
            }

        }
        public static void SpawnTech(SpawnBaseTypes type)
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);

            if (TempManager.techBases.TryGetValue(type, out BaseTemplate val))
            {
                if (val.purposes.Contains(BasePurpose.NotStationary))
                {
                    RawTechLoader.SpawnMobileTech(GetPlayerPos(), Vector3.forward, -1, type);
                }
                else
                {
                    if (val.purposes.Contains(BasePurpose.Defense))
                        RawTechLoader.SpawnBase(GetPlayerPos(), UnityEngine.Random.Range(5, 365), type, false);
                    else if (val.purposes.Contains(BasePurpose.Headquarters))
                    {
                        int team = UnityEngine.Random.Range(5, 365);
                        int extraBB = 0;
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
                        }
                        RawTechLoader.SpawnBase(GetPlayerPos(), team, type, true, extraBB);
                        Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                    }
                    else
                        RawTechLoader.SpawnBase(GetPlayerPos(), UnityEngine.Random.Range(5, 365), type, true);
                }
            }

            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
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
        /// <summary>
        /// endPosGlobal is GLOBAL ROTATION in relation to local tech.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="num"></param>
        /// <param name="endPosGlobal"></param>
        /// <param name="color"></param>
        internal static void DrawDirIndicator(GameObject obj, int num, Vector3 endPosGlobal, Color color)
        {
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
            Vector3[] vecs = new Vector3[2] { pos, endPosGlobal + pos };
            lr.SetPositions(vecs);
            Destroy(gO, Time.deltaTime);
        }
        private static bool CheckValidMode()
        {
            if (EnabledAllModes)
                return true;
            if (Singleton.Manager<ManGameMode>.inst.IsCurrent<ModeMisc>() || (Singleton.Manager<ManGameMode>.inst.IsCurrent<ModeCoOpCreative>() && ManNetwork.IsHost))
            {
                return true;
            }
            return false;
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

    }
}
