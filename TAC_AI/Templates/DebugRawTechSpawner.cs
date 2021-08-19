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

        private static void GUIHandlerPlayer(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionY = false;
            int index = 0;
            int Entries = TempManager.ExternalEnemyTechs.Count();
            for (int step = 0; step < Entries; step++)
            {
                BaseTemplate temp = TempManager.ExternalEnemyTechs[step];
                if (VertPosOff > 600)
                {
                    VertPosOff = 0;
                    HoriPosOff += 200;
                    MaxExtensionY = true;
                }
                if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, 200, 30), temp.techName.ToString()))
                {
                    index = step;
                    clicked = true;
                }
                VertPosOff += 30;
            }
            if (VertPosOff > 600)
            {
                VertPosOff = 0;
                HoriPosOff += 200;
                MaxExtensionY = true;
            }

            if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, 200, 30), "<color=#f23d3dff><b>PURGE ENEMIES</b></color>"))
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

            HotWindow.width = HoriPosOff + 240;
            if (MaxExtensionY)
                HotWindow.height = 680;
            else
                HotWindow.height = VertPosOff + 80;
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
            bool MaxExtensionY = false; 
            SpawnBaseTypes type = SpawnBaseTypes.NotAvail;
            foreach (KeyValuePair<SpawnBaseTypes, BaseTemplate> temp in TempManager.techBases)
            {
                if (VertPosOff > 600)
                {
                    VertPosOff = 0;
                    HoriPosOff += 200;
                    MaxExtensionY = true;
                }
                if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, 200, 30), temp.Key.ToString()))
                {
                    type = temp.Key;
                    clicked = true;
                }
                VertPosOff += 30;
            }
            if (VertPosOff > 600)
            {
                VertPosOff = 0;
                HoriPosOff += 200;
                MaxExtensionY = true;
            }

            if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, 200, 30), "<color=#f23d3dff><b>PURGE ENEMIES</b></color>"))
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

            HotWindow.width = HoriPosOff + 240;
            if (MaxExtensionY)
                HotWindow.height = 680;
            else
                HotWindow.height = VertPosOff + 80;
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
                RawTechLoader.SpawnEnemyTechExternal(GetPlayerPos(), -1, Vector3.forward, val);
            }
            else
            {
                RawTechLoader.SpawnEnemyTechExternal(GetPlayerPos(), -1, Vector3.forward, val);
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
                        RawTechLoader.SpawnBase(GetPlayerPos(), -1, type, false);
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
        private static bool CheckValidMode()
        {
            if (EnabledAllModes)
                return true;
            if (Singleton.Manager<ManGameMode>.inst.IsCurrent<ModeMisc>())
            {
                return true;
            }
            return false;
        }
        private static Vector3 GetPlayerPos()
        {
            try
            {
                PlayerLoc = Singleton.playerTank.boundsCentreWorld;
                return Singleton.playerTank.boundsCentreWorld + (Vector3.forward * 64);
            }
            catch 
            {
                return PlayerLoc + (Vector3.forward * 64);
            }
        }
    }
}
