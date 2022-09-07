using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;
using TerraTechETCUtil;

namespace TAC_AI.World
{
    public class PlayerRTSUI : MonoBehaviour
    {
        internal static readonly List<RTSUnitDisp> unitsSelected = new List<RTSUnitDisp>();
        internal static readonly List<RTSUnitDisp> unitsPast = new List<RTSUnitDisp>();
        private static AIECore.TankAIHelper Leader;

        private const int ButtonWidth = 80;
        private const int ButtonHeight = 80;

        private static readonly int borderSize = 4;
        private static readonly int wF = ButtonWidth - borderSize;
        private static readonly int hF = ButtonHeight - borderSize;


        private const int MaxCountWidth = 12;
        private const int MaxCountHeight = 2;
        private static readonly int MaxWindowHeight = MaxCountHeight * ButtonHeight;
        private static readonly int MaxWindowWidth = MaxCountWidth * ButtonWidth;

        private static float widthW = MaxWindowWidth + Offset;
        private static float heightW = MaxWindowHeight + Offset;
        private static Rect HotWindow = new Rect(0, 0, widthW, heightW + 20);   // the "window"
        private static Vector2 scrolll = new Vector2(0, 0);
        private static float scrolllSize = 50;
        private static int Offset = 20;

        public static bool MouseIsOverSubMenu()
        {
            if (!KickStart.EnableBetterAI || unitsSelected == null || unitsSelected.Count() == 0)
            {
                return false;
            }
            if (ManPlayerRTS.PlayerIsInRTS && ManPlayerRTS.inst.Leading)
            {
                Vector3 Mous = Input.mousePosition;
                Mous.y = Display.main.renderingHeight - Mous.y;
                float xMenuMin = HotWindow.x;
                float xMenuMax = HotWindow.x + HotWindow.width;
                float yMenuMin = HotWindow.y;
                float yMenuMax = HotWindow.y + HotWindow.height;
                //DebugTAC_AI.Log(Mous + " | " + xMenuMin + " | " + xMenuMax + " | " + yMenuMin + " | " + yMenuMax);
                if (Mous.x > xMenuMin && Mous.x < xMenuMax && Mous.y > yMenuMin && Mous.y < yMenuMax)
                {
                    return true;
                }
            }
            return false;
        }
        public static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("PlayerRTSUI").AddComponent<GUIRTSDisplay>();
            AIECore.TankAIManager.TechRemovedEvent.Subscribe(OnTechRemoved);
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            AIECore.TankAIManager.TechRemovedEvent.Unsubscribe(OnTechRemoved);
            Destroy(inst.gameObject);
            inst = null;
            DebugTAC_AI.Log("TACtical_AI: Removed PlayerRTSUI.");

        }

        public static void OnTechRemoved(AIECore.TankAIHelper helper)
        {
            LastSelectedCount = 0;
        }


        public static void SetActive(bool active)
        {
            unitListActive = active;
        }
        private static bool unitListActive = false;



        private static GUIRTSDisplay inst;
        private const int AIRTSDisplayID = 8015;
        internal class GUIRTSDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (!ManPauseGame.inst.IsPaused && !ManPlayerRTS.BoxSelecting)
                {
                    AltUI.StartUI();
                    if (unitListActive && ManPlayerRTS.inst.Leading)
                    {
                        HotWindow = GUI.Window(AIRTSDisplayID, HotWindow, GUIHandlerControl, "Tech Select", AltUI.MenuLeft);
                    }
                    if (!setHovered && hovered?.tank?.visible && hovered.tank.visible.isActive)
                    {
                        Vector3 Mous = Input.mousePosition;
                        Mous.y = Display.main.renderingHeight - Mous.y;
                        toolWindow.x = Mathf.Clamp(Mous.x + 16, 0, Display.main.renderingWidth - toolWindow.width);
                        toolWindow.y = Mathf.Clamp(Mous.y + 16, 0, Display.main.renderingHeight - toolWindow.height);
                        toolWindow = GUI.Window(AIRTSDisplayToolID, toolWindow, GUIHandlerInfo, "A.I. Status", AltUI.MenuLeft);
                    }
                    AltUI.EndUI();
                }
            }
        }

        private static int LastSelectedCount = 0;
        private static bool setHovered = false;
        private static void UpdateSelected()
        {
            if (Leader != ManPlayerRTS.inst.Leading || LastSelectedCount != ManPlayerRTS.inst.LocalPlayerTechsControlled.Count)
            {
                Leader = ManPlayerRTS.inst.Leading;

                foreach (var item in unitsSelected)
                {
                    if (!item.unit || !item.unit.tank.visible.isActive 
                        || !ManPlayerRTS.inst.LocalPlayerTechsControlled.Contains(item.unit))
                        unitsPast.Add(item);
                }
                foreach (var item in unitsPast)
                {
                    unitsSelected.Remove(item);
                }
                unitsPast.Clear();
                foreach (var item in ManPlayerRTS.inst.LocalPlayerTechsControlled)
                {
                    if (item && item.tank.visible.isActive && !unitsSelected.Exists(delegate (RTSUnitDisp cand) { return item == cand.unit; }))
                    {
                        unitsSelected.Add(new RTSUnitDisp(item));
                    }
                }
                LastSelectedCount = ManPlayerRTS.inst.LocalPlayerTechsControlled.Count;
            }
        }
        private static void GUIHandlerControl(int ID)
        {
            UpdateSelected();

            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            int index = 0;

            scrolll = GUI.BeginScrollView(new Rect(0, Offset, HotWindow.width, HotWindow.height - Offset), scrolll, new Rect(0, 0, HotWindow.width - Offset, scrolllSize));

            if (GUI.Button(new Rect(HoriPosOff, VertPosOff, ButtonWidth, ButtonHeight), "Select\nALL"))
            {
                ManPlayerRTS.inst.SelectAllPlayer();
            }
            HoriPosOff += ButtonWidth;

            setHovered = false;
            if (unitsSelected != null && unitsSelected.Count() != 0)
            {
                IntVector2 vec = new IntVector2(ButtonWidth, ButtonHeight);
                int Entries = unitsSelected.Count();
                for (int step = 0; step < Entries; step++)
                {
                    try
                    {
                        RTSUnitDisp temp = unitsSelected[step];
                        if (HoriPosOff >= MaxWindowWidth)
                        {
                            HoriPosOff = 0;
                            VertPosOff += ButtonHeight;
                        }
                        if (unitsSelected[step].ShowOnUI(HoriPosOff, VertPosOff, vec))
                        {
                            if (Event.current.button == 1)
                            {
                                GUIAIManager.GetTank(unitsSelected[step].unit.tank);
                                GUIAIManager.LaunchSubMenuClickable(true);
                            }
                            else
                            {
                                index = step;
                                clicked = true;
                            }
                        }
                        HoriPosOff += ButtonWidth;
                    }
                    catch { }// error on handling something
                }
            }


            GUI.EndScrollView();
            scrolllSize = VertPosOff + ButtonHeight;

            if (clicked)
            {
                RTSUnitDisp temp = unitsSelected[index];
                ManPlayerRTS.inst.ClearList();
                ManPlayerRTS.inst.SelectTank(temp.unit);
                ManPlayerRTS.SetSelectHalo(temp.unit, true);
                //TechUnit.SetRTSState(true);
                //DebugTAC_AI.Log("TACtical_AI: Selected Tank " + grabbedTech.name + ".");
                ManPlayerRTS.inst.SelectUnitSFX();
            }
            if (MouseIsOverSubMenu())
            {
                if (setHovered)
                {
                    string action = hovered.GetActionStatus(out bool notAble);
                    Vector3 Mous = Input.mousePosition;
                    Mous.y = Display.main.renderingHeight - Mous.y;
                    Vector2 newPos = Vector2.zero;
                    newPos.x = Mathf.Clamp(Mous.x + 16, 0, Display.main.renderingWidth - toolWindow.width);
                    newPos.y = Mathf.Clamp(Mous.y + 16, 0, Display.main.renderingHeight - toolWindow.height);
                    Vector2 hotWindowLocal = newPos - HotWindow.position;
                    GUI.Box(new Rect(hotWindowLocal.x, hotWindowLocal.y, toolWindow.width, toolWindow.height), "A.I. Status", AltUI.MenuLeft);
                    if (notAble)
                        GUI.Label(new Rect(20 + hotWindowLocal.x, 15 + hotWindowLocal.y, 160, 60), AltUI.UIAlphaText + action + " (Unable)</color>");
                    else
                        GUI.Label(new Rect(20 + hotWindowLocal.x, 15 + hotWindowLocal.y, 160, 60), AltUI.UIAlphaText + action + "</color>");
                }
                else
                    ManPlayerRTS.inst.SetPlayerHovered(null);
            }

            //GUI.DragWindow();
        }


        internal static void ResetPos()
        {
            HotWindow.x = (Display.main.renderingWidth - HotWindow.width) / 2;
            HotWindow.y = Display.main.renderingHeight - HotWindow.height;
        }


        private const int AIRTSDisplayToolID = 8016;
        private static Rect toolWindow = new Rect(0, 0, 200, 80);   // the "window"
        private static AIECore.TankAIHelper hovered => ManPlayerRTS.inst.PlayerHovered;
        private static void GUIHandlerInfo(int ID)
        {
            if (hovered)
            {
                string action = hovered.GetActionStatus(out bool notAble);
                if (notAble)
                    GUI.Label(new Rect(20, 15, 160, 60), AltUI.UIAlphaText + action + " (Unable)</color>");
                else
                    GUI.Label(new Rect(20, 15, 160, 60), AltUI.UIAlphaText + action + "</color>");
            }
        }
        public class RTSUnitDisp
        {
            public AIECore.TankAIHelper unit;
            public Texture2D unitVis;
            public bool IsLeading => unit == Leader;
            public float lastHealth = 0;
            public float lastEnergy = 0;
            private static Texture2D NoHPBar;
            private static Texture2D HPBarGreen; //= Texture2D.whiteTexture;
            private static Texture2D HPBarRed;// = Texture2D.blackTexture;
            private static Texture2D HPBarBlue;// = Texture2D.blackTexture;
            private static Texture2D OutlineMain;
            private static Texture2D OutlineFollower;
            private static Texture2D OutlinePlayer;
            private static Texture2D OutlineBlack = Texture2D.blackTexture;
            private static Texture2D OutlineWhite = Texture2D.whiteTexture;

            public RTSUnitDisp(AIECore.TankAIHelper thisInst)
            {
                if (HPBarGreen == null)
                    InitMain();
                if (!Init(thisInst))
                    DebugTAC_AI.Exception("TACtical_AI.PlayerRTSUI.RTSUnitDisp: Tried to init a null or unloaded Tech");
            }

            public static void InitMain()
            {
                NoHPBar = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                NoHPBar.SetPixels(0, 0, 2, 2, new Color[4]{
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                });
                NoHPBar.Apply();
                HPBarGreen = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                HPBarGreen.SetPixels(0, 0, 2, 2, new Color[4]{
                    new Color(0.2f, 1, 0.3f, 1f),
                    new Color(0.2f, 1, 0.3f, 1f),
                    new Color(0.2f, 1, 0.3f, 1f),
                    new Color(0.2f, 1, 0.3f, 1f),
                });
                HPBarGreen.Apply();
                HPBarRed = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                HPBarRed.SetPixels(0, 0, 2, 2, new Color[4]{
                    new Color(1, 0.2f, 0.3f, 1f),
                    new Color(1, 0.2f, 0.3f, 1f),
                    new Color(1, 0.2f, 0.3f, 1f),
                    new Color(1, 0.2f, 0.3f, 1f),
                });
                HPBarRed.Apply();
                HPBarBlue = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                HPBarBlue.SetPixels(0, 0, 2, 2, new Color[4]{
                    new Color(0.1f, 0.75f, 1, 1f),
                    new Color(0.1f, 0.75f, 1, 1f),
                    new Color(0.1f, 0.75f, 1, 1f),
                    new Color(0.1f, 0.75f, 1, 1f),
                });
                HPBarBlue.Apply();
                Texture2D[] res = Resources.FindObjectsOfTypeAll<Texture2D>();
                for (int step = 0; step < res.Length; step++)
                {
                    Texture2D resCase = res[step];
                    if (resCase && !resCase.name.NullOrEmpty())
                    {
                        if (resCase.name == "TechLoader_Highlight_03")
                            OutlineMain = resCase;
                        else if (resCase.name == "TechLoader_Highlight_02")
                            OutlinePlayer = resCase;
                        else if (resCase.name == "TechLoader_Highlight_01")
                            OutlineFollower = resCase;
                    }
                }
            }

            public bool Init(AIECore.TankAIHelper tech)
            {
                if (tech != null)
                {
                    unit = tech;
                    try
                    {
                        return TryMakePortrait();
                    }
                    catch { }
                }
                return false;
            }

            public static bool MouseIsOver(Rect bax)
            {
                Vector3 Mous = Input.mousePosition;
                Mous.y = Display.main.renderingHeight - Mous.y;
                float xMenuMin = HotWindow.x + bax.x;
                float xMenuMax = HotWindow.x + bax.x + bax.width;
                float yMenuMin = HotWindow.y + bax.y;
                float yMenuMax = HotWindow.y + bax.y + bax.height;
                //DebugTAC_AI.Log(Mous + " | " + xMenuMin + " | " + xMenuMax + " | " + yMenuMin + " | " + yMenuMax);
                if (Mous.x > xMenuMin && Mous.x < xMenuMax && Mous.y > yMenuMin && Mous.y < yMenuMax)
                {
                    return true;
                }
                return false;
            }
            public bool TryMakePortrait()
            {
                var tank = unit.GetComponent<Tank>();
                if (!tank.visible.isActive)
                    return false;
                Singleton.Manager<ManScreenshot>.inst.RenderTechImage(unit.GetComponent<Tank>(), new IntVector2(96, 96), false, delegate (TechData techData, Texture2D techImage)
                {
                    if (techImage.IsNotNull())
                    {
                        unitVis = techImage;
                    }
                });
                return true;
            }
            public bool ShowOnUI(int posOnUIX, int posOnUIY, IntVector2 size)
            {
                Rect bax = new Rect(posOnUIX, posOnUIY, size.x, size.y);
                if (unit == null)
                {
                    GUI.DrawTexture(bax, HPBarRed);
                    return false;
                }
                bool select = GUI.Button(bax, "");
                bool useHealth = unit.CanDetectHealth();
                if (useHealth)
                {
                    lastHealth = unit.GetHealth();
                    GUI.DrawTexture(bax, HPBarRed);
                    GUI.DrawTexture(new Rect(posOnUIX, posOnUIY + (size.y * (1 - (lastHealth / 100f))), size.x, size.y * (lastHealth / 100f)), HPBarGreen);
                }
                else
                {
                    GUI.DrawTexture(bax, NoHPBar);
                }
                if (unit.CanStoreEnergy())
                {
                    lastEnergy = unit.GetEnergyPercent();
                    GUI.DrawTexture(new Rect(posOnUIX, posOnUIY + (size.y * (1 - lastEnergy)), size.x / 2, size.y * lastEnergy), HPBarBlue);
                }
                GUI.DrawTexture(bax, unitVis);
                if (unit.tank.PlayerFocused)
                {
                    if (OutlinePlayer)
                    {
                        GUI.DrawTexture(bax, OutlinePlayer);
                    }
                    else
                    {
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY, borderSize, size.y), OutlineBlack);
                        GUI.DrawTexture(new Rect(posOnUIX + wF, posOnUIY, borderSize, size.y), OutlineBlack);
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY, size.x, borderSize), OutlineBlack);
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY + hF, size.x, borderSize), OutlineBlack);
                    }
                }
                else if (IsLeading)
                {
                    if (OutlineMain)
                    {
                        GUI.DrawTexture(bax, OutlineMain);
                    }
                    else
                    {
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY, borderSize, size.y), OutlineBlack);
                        GUI.DrawTexture(new Rect(posOnUIX + wF, posOnUIY, borderSize, size.y), OutlineBlack);
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY, size.x, borderSize), OutlineBlack);
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY + hF, size.x, borderSize), OutlineBlack);
                    }
                }
                else
                {
                    if (OutlineFollower)
                    {
                        GUI.DrawTexture(bax, OutlineFollower);
                    }
                    else
                    {
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY, borderSize, size.y), OutlineWhite);
                        GUI.DrawTexture(new Rect(posOnUIX + wF, posOnUIY, borderSize, size.y), OutlineWhite);
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY, size.x, borderSize), OutlineWhite);
                        GUI.DrawTexture(new Rect(posOnUIX, posOnUIY + hF, size.x, borderSize), OutlineWhite);
                    }
                }
                if (MouseIsOver(bax))
                {
                    ManPlayerRTS.inst.SetPlayerHovered(unit);
                    setHovered = true;
                }
                //if (useHealth)
                //    GUI.Label(new Rect(posOnUIX, posOnUIY, size.x, size.y), "<color=#000000ff><b>" + Mathf.FloorToInt(lastHealth).ToString() + "</b></color>");
                return select;
            }
        }
    }
}
