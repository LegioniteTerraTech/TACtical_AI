using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;
using TAC_AI.Templates;
using TerraTechETCUtil;

namespace TAC_AI.World
{
    internal class PlayerRTSUI : MonoBehaviour
    {
        internal static readonly List<RTSUnitDisp> unitsSelected = new List<RTSUnitDisp>();
        internal static readonly List<RTSUnitDisp> unitsPast = new List<RTSUnitDisp>();
        private static HashSet<Tank> techsAltered = new HashSet<Tank>();
        private static TankAIHelper Leader;

        private const int ButtonWidth = 80;
        private const int ButtonHeight = 80;

        private static readonly int borderSize = 4;
        private static readonly int wF = ButtonWidth - borderSize;
        private static readonly int hF = ButtonHeight - borderSize;


        private const int MaxCountWidthUnits = 8;
        private const int MaxCountWidthButtons = 3;
        private const int MaxCountHeight = 2;
        private static readonly int MaxWindowHeight = MaxCountHeight * ButtonHeight;
        private static readonly int MaxWindowWidthUnits = MaxCountWidthUnits * ButtonWidth;
        private static readonly int MaxWindowWidthSpacer = ButtonWidth;
        private static readonly int MaxWindowWidthButtons = MaxCountWidthButtons * ButtonWidth;
        private static readonly int MaxWindowWidth = MaxWindowWidthUnits + MaxWindowWidthButtons + MaxWindowWidthSpacer;

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
            TankAIManager.TechRemovedEvent.Subscribe(OnTechRemoved);
            ManTechs.inst.TankBlockAttachedEvent.Subscribe(OnTechChanged);
            ManTechs.inst.TankBlockDetachedEvent.Subscribe(OnTechChanged);
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            ManTechs.inst.TankBlockDetachedEvent.Unsubscribe(OnTechChanged);
            ManTechs.inst.TankBlockAttachedEvent.Unsubscribe(OnTechChanged);
            TankAIManager.TechRemovedEvent.Unsubscribe(OnTechRemoved);
            Destroy(inst.gameObject);
            inst = null;
            DebugTAC_AI.Log(KickStart.ModID + ": Removed PlayerRTSUI.");
        }

        public static void OnTechChanged(Tank tank, TankBlock block)
        {
            if (!techsAltered.Contains(tank))
                techsAltered.Add(tank);
        }
        public static void OnTechRemoved(TankAIHelper helper)
        {
            LastSelectedCount = 0;
        }


        public static void SetActive(bool active)
        {
            unitListActive = active;
        }
        private static bool unitListActive = false;

        public static void ShowTechPlacementUI(TankAIHelper thisInst)
        {
            /*
            ManHUD.inst.ShowHudElement(ManHUD.HUDElementType.TechLoader, null);
            UITechLoaderHUD UITL = (UITechLoaderHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.TechLoader);
            if (UITL)
            {
                var UITS = UITL.GetComponentInChildren<UITechSelector>(true);
                if (UITS)
                {
                    UITS.
                }
            }
            */
            if (Input.GetKey(KickStart.MultiSelect) && thisInst?.lastBuiltTech != null)
            {
                if (DoTechBuild(thisInst, thisInst.lastBuiltTech))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIGuard);
                    InvokeHelper.Invoke(ManSFX.inst.PlayUISFX, 0.225f, ManSFX.UISfxType.Craft);
                }
            }
            else
            {
                if (ManUI.inst.IsScreenShowing(ManUI.ScreenType.TechLoaderScreen))
                {
                    if (thisInst?.lastBuiltTech != null)
                    {
                        ManUI.inst.PopScreen(false);
                        if (DoTechBuild(thisInst, thisInst.lastBuiltTech))
                        {
                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIGuard);
                            InvokeHelper.Invoke(ManSFX.inst.PlayUISFX, 0.225f, ManSFX.UISfxType.Craft);
                        }
                    }
                }
                else
                {
                    ManUI.inst.GoToScreen(ManUI.ScreenType.TechLoaderScreen);
                    UIScreenTechLoader UISTL = (UIScreenTechLoader)ManUI.inst.GetScreen(ManUI.ScreenType.TechLoaderScreen);
                    if (UISTL != null)
                    {
                        UISTL.SelectorCallback = (Snapshot s) => { DoTechBuild(thisInst, s); };
                    }
                }
            }
        }
        private static bool DoTechBuild(TankAIHelper doingTech, Snapshot snap)
        {
            if (doingTech?.tank && doingTech.tank.visible.isActive && doingTech.tank.IsAnchored)
            {
                doingTech.lastBuiltTech = snap;
                return AIEBases.BaseConstructTech(doingTech.tank, snap);
            }
            return false;
        }

        public static void DisplayHud(bool show)
        {
            ManHUD.inst.Show(ManHUD.HideReason.Paused, show);
        }

        private static GUIRTSDisplay inst;
        private const int AIRTSDisplayID = 8015;
        private static GUIStyle SmallTextTitle = null;
        private static GUIStyle SmallTextDesc = null;
        private static Canvas hudBack = null;
        private static Vector2 hudBackOrigin;
        public static void RTSDamageWarnings(float duration, float strength)
        {
            if (inst == null)
                return;
            inst.CancelInvoke("RTSDamageWarningEnd");
            Singleton.Manager<CameraManager>.inst.SetChromaticAberrationIntensity(strength);
            Singleton.Manager<CameraManager>.inst.SetGraphicOptionEnabled(CameraManager.GraphicOption.CA, true);
            inst.Invoke("RTSDamageWarningEnd", duration);
        }
        internal class GUIRTSDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (!ManPauseGame.inst.IsPaused && !ManPlayerRTS.BoxSelecting)
                {
                    AltUI.StartUI();
                    if (SmallTextTitle == null)
                    {
                        SmallTextTitle = new GUIStyle(AltUI.LabelBlueTitle);
                        SmallTextTitle.fontSize = 11;
                        SmallTextTitle.clipping = TextClipping.Overflow;
                        SmallTextTitle.wordWrap = true;

                        SmallTextDesc = new GUIStyle(AltUI.LabelBlackTitle);
                        SmallTextDesc.font = AltUI.ExoFontExtraBold;
                        SmallTextDesc.fontSize = 10;
                        SmallTextDesc.clipping = TextClipping.Overflow;
                        SmallTextDesc.wordWrap = false;

                        hudBack = ManHUD.inst.GetComponent<Canvas>();
                        hudBackOrigin = hudBack.pixelRect.position;
                    }
                    if (unitListActive && !AIGlobals.HideHud)
                    {
                        if (ManPlayerRTS.inst.Leading || ManPlayerRTS.PlayerRTSOverlay)
                        {
                            DisplayHud(true);
                            string controlName;
                            if (CommandQueued != null)
                                controlName = CommandQueued.Method.Name.Replace("_", " ");
                            else
                                controlName = "Tech Select";
                            HotWindow = GUI.Window(AIRTSDisplayID, HotWindow, GUIHandlerControl, controlName, AltUI.MenuLeft);

                            IDStep = AIRTSHealthStartID;
                            if (HPBarGreen == null)
                                InitTextures();
                            foreach (var item in ManTechs.inst.IteratePlayerTechs())
                            {
                                GUIShowHP(item);
                            }
                            if (ManPlayerRTS.inst.OtherHovered != null)
                            {
                                GUIShowHP(ManPlayerRTS.inst.OtherHovered.tank);
                            }
                            else if (ManPointer.inst.targetVisible != null)
                            {
                                var HP = ManPointer.inst.targetVisible.damageable;
                                if (HP && !HP.Invulnerable)
                                {
                                    GUIShowHP(ManPointer.inst.targetVisible);
                                }
                            }
                            while (Pending.Any())
                            {
                                Pending.Dequeue();
                            }
                            while (Did.Any())
                            {
                                var windowD = Did.Dequeue();
                                windowD.Display();
                                Pending.Enqueue(windowD);
                            }
                        }
                        else
                            DisplayHud(false);
                    }
                    AltUI.EndUI();
                    if (!setHovered && hovered?.tank?.visible && hovered.tank.visible.isActive)
                    {
                        string action = hovered.GetActionStatus(out bool notAble);
                        if (notAble)
                            AltUI.TooltipWorld("A.I. Status", action + " (Unable)");
                        else
                            AltUI.TooltipWorld("A.I. Status", action);
                    }
                }
            }
            public void RTSDamageWarningEnd()
            {
                Singleton.Manager<CameraManager>.inst.SetChromaticAberrationIntensity(0);
                Singleton.Manager<CameraManager>.inst.SetGraphicOptionEnabled(CameraManager.GraphicOption.CA, false);
            }
        }

        private const float BorderSize = 1.5f;
        private static void GUIShowHP(Tank tech)
        {
            try
            {
                GUIData data;
                if (!Pending.Any())
                    data = new GUIData();
                else
                    data = Pending.Dequeue();
                Did.Enqueue(data);
                var curHP = tech.GetHelperInsured();
                data.AlwaysShowName = false;
                data.HullHP = curHP.GetHealth();
                data.HullHPMax = curHP.GetHealthMax();
                if (data.HullHPMax <= 0 || data.HullHP <= 0)
                    return;
                data.WidthHealthBar = (curHP.lastTechExtents * 2) + 80;
                bool storeE = curHP.CanStoreEnergy();
                float height = 38;
                Vector3 AboveTechUI = Singleton.cameraTrans.up * curHP.lastTechExtents;
                Vector3 OnScreenPos = Singleton.camera.WorldToScreenPoint(tech.boundsCentreWorldNoCheck + AboveTechUI);
                if (OnScreenPos.z <= 0)
                    return;

                data.HealthPos = new Rect(OnScreenPos.x - ((data.WidthHealthBar / 2) + BorderSize), 
                    Display.main.renderingHeight - (OnScreenPos.y + height),
                    data.WidthHealthBar + BorderSize + BorderSize, height);
                UIHelpersExt.ClampMenuToScreen(ref data.HealthPos, false);

                if (storeE)
                {
                    data.Energy = curHP.GetEnergy();
                    data.EnergyMax = curHP.GetEnergyMax();
                }
                else
                {
                    data.EnergyMax = 0;
                }
                data.name = tech.name.NullOrEmpty() ? "NULL" : tech.name;
            }
            catch (Exception e)
            { throw new Exception("PlayerRTSUI.GUIShowHP(Tank) has errored: " + e); }
        }
        private static void GUIShowHP(Visible vis)
        {
            try
            {
                GUIData data;
                if (!Pending.Any())
                    data = new GUIData();
                else
                    data = Pending.Dequeue();
                Did.Enqueue(data);
                data.AlwaysShowName = true;
                data.HullHP = vis.damageable.Health;
                data.HullHPMax = vis.damageable.MaxHealth;
                if (data.HullHPMax <= 0 || data.HullHP <= 0)
                    return;
                data.WidthHealthBar = 80;
                float height = 38;
                
                Vector3 AboveTechUI = Singleton.cameraTrans.up * vis.GetCheapBounds();
                Vector3 OnScreenPos = Singleton.camera.WorldToScreenPoint(vis.centrePosition + AboveTechUI);
                if (OnScreenPos.z <= 0)
                    return;

                data.HealthPos = new Rect(OnScreenPos.x - ((data.WidthHealthBar / 2) + BorderSize),
                    Display.main.renderingHeight - (OnScreenPos.y + height),
                    data.WidthHealthBar + BorderSize + BorderSize, height);
                UIHelpersExt.ClampMenuToScreen(ref data.HealthPos, false);
                data.EnergyMax = 0;
                data.name = StringLookup.GetItemName(vis.m_ItemType);
                IDStep++;
            }
            catch (Exception e)
            { throw new Exception("PlayerRTSUI.GUIShowHP(Visible) has errored: " + e); }
        }
        private static Queue<GUIData> Did = new Queue<GUIData>();
        private static Queue<GUIData> Pending = new Queue<GUIData>();
        private const int AIRTSHealthStartID = 18017;
        private static int IDStep = AIRTSHealthStartID;
        public class GUIData
        {

            public bool AlwaysShowName = false;
            public string name;
            public Rect HealthPos;
            public float WidthHealthBar = 1;
            public float HullHP = 1;
            public float HullHPMax = 1;
            public float Energy = 1;
            public float EnergyMax = 1;

            public void Display()
            {
                GUI.Window(IDStep, HealthPos, GUIHandlerHealthbar, "", SmallTextTitle);
                IDStep++;
            }
            private void GUIHandlerHealthbar(int ID)
            {
                float WidthHealthBarExt = WidthHealthBar + BorderSize + BorderSize;
                //GUI.DrawTexture(new Rect(0, 0, WidthHealthBar, 14), OutlineWhite, ScaleMode.ScaleAndCrop);
                if (AlwaysShowName || Input.GetKey(KickStart.MultiSelect))
                {
                    GUI.DrawTexture(new Rect(0, 0, WidthHealthBarExt, 14), OutlineBlack, ScaleMode.ScaleAndCrop);
                    GUI.Label(new Rect(0, 0, WidthHealthBarExt, 14), name, SmallTextTitle);
                }
                //GUI.DrawTexture(new Rect(0, 10, WidthHealthBar, 16), OutlineBlack, ScaleMode.ScaleAndCrop);
                if (EnergyMax > 0)
                {
                    GUI.DrawTexture(new Rect(0, 14 - BorderSize, WidthHealthBarExt, 20 + BorderSize + BorderSize), OutlineBlack, ScaleMode.ScaleAndCrop);
                    GUI.DrawTexture(new Rect(BorderSize, 14, WidthHealthBar, 20), HPBarRed, ScaleMode.ScaleAndCrop);
                    GUI.DrawTexture(new Rect(BorderSize, 14, (Energy / EnergyMax) * WidthHealthBar, 20), HPBarBlue, ScaleMode.ScaleAndCrop);
                    GUI.Label(new Rect(BorderSize, 12, WidthHealthBar, 14), (int)Energy + "/" + (int)EnergyMax, SmallTextDesc);
                    GUI.DrawTexture(new Rect(BorderSize, 24, (HullHP / HullHPMax) * WidthHealthBar, 10), HPBarGreen, ScaleMode.ScaleAndCrop);
                    GUI.Label(new Rect(BorderSize, 22, WidthHealthBar, 14), HullHP + "/" + HullHPMax, SmallTextDesc);
                }
                else
                {
                    GUI.DrawTexture(new Rect(0, 14 - BorderSize, WidthHealthBarExt, 10 + BorderSize + BorderSize), OutlineBlack, ScaleMode.ScaleAndCrop);
                    GUI.DrawTexture(new Rect(BorderSize, 14, WidthHealthBar, 10), HPBarRed, ScaleMode.ScaleAndCrop);
                    GUI.DrawTexture(new Rect(BorderSize, 14, (HullHP / HullHPMax) * WidthHealthBar, 10), HPBarGreen, ScaleMode.ScaleAndCrop);
                    GUI.Label(new Rect(BorderSize, 12, WidthHealthBar, 14), HullHP + "/" + HullHPMax, SmallTextDesc);
                }
            }
        }

        private static int LastSelectedCount = 0;
        private static bool setHovered = false;
        internal static Action<Vector3> CommandQueued
        {
            get => ManPlayerRTS.CommandQueued;
            set => ManPlayerRTS.CommandQueued = value;
        }
        private static void UpdateSelected()
        {
            if (Leader != ManPlayerRTS.inst.Leading || LastSelectedCount != ManPlayerRTS.inst.LocalPlayerTechsControlled.Count)
            {
                Leader = ManPlayerRTS.inst.Leading;
                SelfDestruct = 5;

                foreach (var item in unitsSelected)
                {
                    if (!item.unit || !item.unit.tank.visible.isActive || techsAltered.Contains(item.unit.tank)
                        || !ManPlayerRTS.inst.LocalPlayerTechsControlled.Contains(item.unit))
                        unitsPast.Add(item);
                }
                foreach (var item in unitsPast)
                {
                    unitsSelected.Remove(item);
                }
                unitsPast.Clear();
                techsAltered.Clear();
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
            GUIHandlerUnits();
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical(GUILayout.Width(MaxWindowWidthButtons));
                GUIHandlerCommands();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            catch (ExitGUIException e)
            { throw e; }
            catch { }
        }
        private static void GUIHandlerUnits()
        {
            UpdateSelected();

            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            int index = 0;

            scrolll = GUI.BeginScrollView(new Rect(0, Offset, HotWindow.width, HotWindow.height - Offset), scrolll, new Rect(0, 0, HotWindow.width - Offset, scrolllSize));

            if (GUI.Button(new Rect(HoriPosOff, VertPosOff, ButtonWidth, ButtonHeight), "Select\nALL"))
            {
                ManPlayerRTS.inst.ControlAllPlayer();
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
                        if (HoriPosOff >= MaxWindowWidthUnits)
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
                    catch (ExitGUIException e)
                    { throw e; }
                    catch { }// error on handling something
                }
            }


            GUI.EndScrollView();
            scrolllSize = VertPosOff + ButtonHeight;

            if (clicked)
            {
                RTSUnitDisp temp = unitsSelected[index];
                ManPlayerRTS.inst.ClearList();
                ManPlayerRTS.inst.StartControlling(temp.unit, ManPlayerRTS.inst.LocalPlayerTechsControlled);
                ManPlayerRTS.SetSelectHalo(temp.unit, true);
                //TechUnit.SetRTSState(true);
                //DebugTAC_AI.Log(KickStart.ModID + ": Selected Tank " + grabbedTech.name + ".");
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
        private static TankAIHelper lastTank => ManPlayerRTS.inst.Leading;
        private static List<TankAIHelper> lastTanks => ManPlayerRTS.inst.LocalPlayerTechsControlled.ToList();
        private static void GUIHandlerCommands()
        {
            TankAIHelper helper = ManPlayerRTS.inst.Leading;
            if (helper != null)
            {
                if (helper.ActuallyWorks)
                {
                    if (!helper.tank.IsAnchored || helper.CanAutoAnchor)
                        CommandsMobile();
                    else
                        CommandsAnchored();
                }
                else
                    CommandsStatic();
            }
            else
                CommandsNone();
        }
        private static void CommandsMobile()
        {
            Sprite SPR;
            Texture tex = null;
            GUILayoutOption GLOh = GUILayout.Height(ButtonHeight);
            GUILayout.BeginHorizontal(GLOh);
            CommandButton("Move", ManPlayerRTS.GetLineMat().mainTexture, "Move to Destination", true,
                "Unanchor Tech", "ERROR", false, true, CommandMove);

            /*
            if (RawTechExporter.aiIcons.TryGetValue(AIType.Assault, out SPR))
                tex = SPR.texture;
            CommandButton("Attack", tex, "Attack Tech", true,
                "Unanchor Tech", "ERROR", false, false, CommandMove);
            */
            EmptyButton();

            if (RawTechExporter.aiIcons.TryGetValue(AIType.Aegis, out SPR))
                tex = SPR.texture;
            CommandButton("Stop", tex,
                "Stop all actions", true, "", "ERROR", false, !lastTank.lastEnemy, CommandStop);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            /*
            if (RawTechExporter.aiIcons.TryGetValue(AIType.MTMimic, out SPR))
                tex = SPR.texture;
            CommandButton("Patrol", tex, "Patrol Between", true,
                "Unanchor Tech", "ERROR", false, false, CommandMove);*/
            if (RawTechExporter.aiIcons.TryGetValue(AIType.MTStatic, out SPR))
                tex = SPR.texture;
            CommandButton("Explode", tex, "Self-Destruct", true,
                "ERROR", "ERROR", false, false, CommandExplode);
            EmptyButton();
            CommandButton("Anchor", null, "Anchor to ground", lastTank.tank.Anchors.NumPossibleAnchors > 0,
                "No Anchors", "Enemy too close!", !lastTank.CanAnchorSafely, lastTank.tank.IsAnchored, CommandAnchor);
            GUILayout.EndHorizontal();
        }

        private static void CommandsAnchored()
        {
            Sprite SPR;
            Texture tex = null;
            GUILayoutOption GLOh = GUILayout.Height(ButtonHeight);
            GUILayout.BeginHorizontal(GLOh);
            EmptyButton();
            if (RawTechExporter.aiIcons.TryGetValue(AIType.Assault, out SPR))
                tex = SPR.texture;
            CommandButton("Attack", tex, "Attack Tech", true, "Unanchor Tech", 
                "ERROR", false, lastTank.lastEnemy, CommandMove);

            if (RawTechExporter.aiIcons.TryGetValue(AIType.Aegis, out SPR))
                tex = SPR.texture;
            CommandButton("Stop", tex, "Stop all actions", true, "", "ERROR", 
                false, !lastTank.lastEnemy, CommandStop);

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GLOh);
            if (RawTechExporter.aiIcons.TryGetValue(AIType.MTStatic, out SPR))
                tex = SPR.texture;
            CommandButton("Explode", tex, "Self-Destruct", true,
                "ERROR", "ERROR", false, false, CommandExplode);
            if (RawTechExporter.aiIcons.TryGetValue(AIType.Scrapper, out SPR))
                tex = SPR.texture;
            CommandButton("Build", tex, "Build a Tech", true,
                "ERROR", "ERROR", false, false, CommandBuild);
            CommandButton("Un-Anchor", null, "Un-Anchor from ground", true,
                "ERROR", "ERROR", false, false, CommandAnchor);
            GUILayout.EndHorizontal();
        }
        private static void CommandsStatic()
        {
            Sprite SPR;
            Texture tex = null;
            GUILayoutOption GLOh = GUILayout.Height(ButtonHeight);
            GUILayout.BeginHorizontal(GLOh);
            EmptyButton();
            EmptyButton();
            EmptyButton();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (RawTechExporter.aiIcons.TryGetValue(AIType.MTStatic, out SPR))
                tex = SPR.texture;
            CommandButton("Explode", tex, "Self-Destruct", true,
                "ERROR", "ERROR", false, false, CommandExplode);
            EmptyButton();
            EmptyButton();
            GUILayout.EndHorizontal();
        }
        private static void CommandsNone()
        {
            Sprite SPR;
            Texture tex = null;
            GUILayoutOption GLOh = GUILayout.Height(ButtonHeight);
            GUILayout.BeginHorizontal(GLOh);
            EmptyButton();
            EmptyButton();
            EmptyButton();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EmptyButton();
            EmptyButton();
            EmptyButton();
            GUILayout.EndHorizontal();
        }

        private static void CommandMove()
        {
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            GUIAIManager.LaunchSubMenuClickableRTS();
        }
        private static int SelfDestruct = 5;
        private static void CommandExplode()
        {
            if (SelfDestruct == 0)
            {
                if (!ManNetwork.IsNetworked)
                {
                    if (lastTank.tank.visible.isActive)
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateOpen);
                        for (int step = lastTanks.Count - 1; step > -1; step--)
                        {
                            lastTanks[step].tank.blockman.Disintegrate();
                        }
                    }
                    return;
                }
            }
            else
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Back);
                SelfDestruct--;
            }
        }
        private static void CommandStop()
        {
            for (int step = lastTanks.Count - 1; step > -1; step--)
            {
                var item = lastTanks[step];
                item.RTSControlled = false;
                item.lastEnemy = null;
                item.ForceAllAIsToEscort(false, true);
            }
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIIdle);
        }
        private static void CommandBuild()
        {
            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCFabricator);
            ShowTechPlacementUI(lastTank);
        }
        private static void CommandAnchor()
        {
            GUIAIManager.lastTank = lastTank;
            if (lastTank.tank.IsAnchored)
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                if (ManNetwork.IsHost)
                {
                    for (int step = lastTanks.Count - 1; step > -1; step--)
                    {
                        var item = lastTanks[step];
                        item.UnAnchor();
                        item.PlayerAllowAutoAnchoring = true;
                    }
                }
                GUIAIManager.SetOptionDriver(AIDriverType.AutoSet, false);
            }
            else
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                if (ManNetwork.IsHost)
                {
                    for (int step = lastTanks.Count - 1; step > -1; step--)
                    {
                        var item = lastTanks[step];
                        item.PlayerAllowAutoAnchoring = false;
                        item.TryAnchor();
                    }
                }
                GUIAIManager.SetOptionDriver(AIDriverType.Stationary, false);
                GUIAIManager.SetOption(AIType.Escort, false);
            }
        }

        private static void EmptyButton()
        {
            CommandButton("", null, "", false, "ERROR", "ERROR", false, false, null);
        }
        private static void CommandButton(string title, Texture tex, string desc, bool isAvail, string availReq, string runReq, bool CantPerformActions, bool selected, Action act)
        {
            GUILayoutOption GLO = GUILayout.Width(ButtonWidth);
            GUILayoutOption GLO2 = GUILayout.Height(ButtonHeight);
            if (tex == null)
            {
                if (isAvail)
                {
                    if (GUILayout.Button(CantPerformActions ? new GUIContent(title, runReq) : new GUIContent(title, desc),
                      selected ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLO2))
                    {
                        if (act != null)
                        {
                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                            act.Invoke();
                        }
                    }
                }
                else if (GUILayout.Button(new GUIContent(title, availReq),
                    AltUI.ButtonGrey, GLO, GLO2))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            else
            {
                if (isAvail)
                {
                    if (GUILayout.Button(CantPerformActions ? new GUIContent(tex, runReq) : new GUIContent(tex, desc),
                      selected ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLO2))
                    {
                        if (act != null)
                        {
                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                            act.Invoke();
                        }
                    }
                }
                else if (GUILayout.Button(new GUIContent(tex, availReq),
                    AltUI.ButtonGrey, GLO, GLO2))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
        }


        internal static void ResetPos()
        {
            HotWindow.x = (Display.main.renderingWidth - HotWindow.width) / 2;
            HotWindow.y = Display.main.renderingHeight - HotWindow.height;
        }


        private const int AIRTSDisplayToolID = 8016;
        private static Rect toolWindow = new Rect(0, 0, 200, 80);   // the "window"
        private static TankAIHelper hovered => ManPlayerRTS.inst.PlayerHovered ? ManPlayerRTS.inst.PlayerHovered : ManPlayerRTS.inst.OtherHovered;
        private static void GUIHandlerInfo(int ID)
        {
            if (hovered)
            {
                string action = hovered.GetActionStatus(out bool notAble);
                if (notAble)
                    GUI.Label(new Rect(20, 15, 160, 60), action + " (Unable)", AltUI.LabelBlack);
                else
                    GUI.Label(new Rect(20, 15, 160, 60), action, AltUI.LabelBlack);
            }
        }

        private static Texture2D NoHPBar;
        private static Texture2D HPBarGreen; //= Texture2D.whiteTexture;
        private static Texture2D HPBarRed;// = Texture2D.blackTexture;
        private static Texture2D HPBarBlue;// = Texture2D.blackTexture;
        private static Texture2D OutlineMain;
        private static Texture2D OutlineFollower;
        private static Texture2D OutlinePlayer;
        private static Texture2D OutlineBlack;
        private static Texture2D OutlineWhite = Texture2D.whiteTexture;
        public static void InitTextures()
        {
            Color colorC = AltUI.ColorDefaultGrey;
            OutlineBlack = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            OutlineBlack.SetPixels(0, 0, 2, 2, new Color[4] { colorC, colorC, colorC, colorC, });
            OutlineBlack.Apply();

            colorC = new Color(0.2f, 0.2f, 0.2f, 1f);
            NoHPBar = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            NoHPBar.SetPixels(0, 0, 2, 2, new Color[4] { colorC, colorC, colorC, colorC, });
            NoHPBar.Apply();

            colorC = new Color(0.525f, 1, 0.75f, 1f);
            HPBarGreen = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            HPBarGreen.SetPixels(0, 0, 2, 2, new Color[4] { colorC, colorC, colorC, colorC, });
            HPBarGreen.Apply();

            //colorC = new Color(1, 0.3f, 0.4f, 1f);
            colorC = new Color(1, 0.525f, 0.75f, 1f);
            HPBarRed = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            HPBarRed.SetPixels(0, 0, 2, 2, new Color[4]{ colorC, colorC, colorC, colorC, });
            HPBarRed.Apply();

            //colorC = new Color(0.225f, 0.75f, 1, 1f);
            colorC = new Color(0.525f, 0.85f, 1, 1f);
            HPBarBlue = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            HPBarBlue.SetPixels(0, 0, 2, 2, new Color[4] { colorC, colorC, colorC, colorC, });
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
        public class RTSUnitDisp
        {
            public TankAIHelper unit;
            public Texture2D unitVis;
            public bool IsLeading => unit == Leader;
            public float lastHealth = 0;
            public float lastEnergy = 0;

            public RTSUnitDisp(TankAIHelper thisInst)
            {
                if (HPBarGreen == null)
                    InitTextures();
                if (!Init(thisInst))
                    DebugTAC_AI.Exception("TACtical_AI.PlayerRTSUI.RTSUnitDisp: Tried to init a null or unloaded Tech");
            }


            public bool Init(TankAIHelper tech)
            {
                if (tech != null)
                {
                    unit = tech;
                    try
                    {
                        return TryMakePortrait();
                    }
                    catch (Exception e)
                    {
                        throw new Exception("TACtical_AI.PlayerRTSUI.RTSUnitDisp: Tried to init a null or unloaded Tech - " + e);
                    }
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
                unitVis = ManUI.inst.GetSprite(new ItemTypeInfo(ObjectTypes.Block, -1)).texture;
                return true;
            }
            public bool ShowOnUI(int posOnUIX, int posOnUIY, IntVector2 size)
            {
                if (unitVis == null)
                    throw new NullReferenceException("RTSUnitDisp.unitVis cannot be null");
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
                    lastHealth = unit.GetHealth100();
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
