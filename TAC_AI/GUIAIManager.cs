using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;
using TAC_AI.World;
using TAC_AI.Templates;
using TerraTechETCUtil;

namespace TAC_AI
{
    internal class GUIAIManager : MonoBehaviour
    {
        //Handles the display that's triggered on AI change 
        //  Circle hud wheel when the player assigns a new AI state
        //  TODO - add the hook needed to get the UI to pop up on Guard selection
        // NOTE: HANDLES RTS SELECTED AIS AS WELL
        private static GUIAIManager inst;
        public static Vector3 PlayerLoc = Vector3.zero;
        public static bool isCurrentlyOpen = false;
        private static AIType fetchAI = AIType.Escort;
        private static AIDriverType AIDriver = AIDriverType.Tank;
        private static AIType changeAI = AIType.Escort;
        internal static TankAIHelper lastTank;


        // Mode - Setting
        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 380);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;
        private static int SelfDestruct = 5;
        private static bool AdvancedToggles = false;

        // Tech Tracker
        private static float windowTimer = 0;
        private const int AIManagerID = 8001;


        internal static void Initiate()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject()).AddComponent<GUIAIManager>();
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Subscribe(OnPlayerSwap);
            if (GUIWindow == null)
            {
                GUIWindow = new GameObject();
                GUIWindow.AddComponent<GUIDisplay>();
                GUIWindow.SetActive(false);
                Vector3 Mous = Input.mousePosition;
                xMenu = 0;
                yMenu = 0;
            }
        }
        internal static void DeInit()
        {
            if (!inst)
                return;
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Unsubscribe(OnPlayerSwap);
            GUIWindow.SetActive(false);
            inst.enabled = false;
            Destroy(inst.gameObject);
            inst = null;
        }

        public static void OnPlayerSwap(Tank tonk)
        {
            CloseSubMenuClickable();
        }
        public static void GetTank(Tank tank)
        {
            ResetInfo();
            lastTank = tank.GetHelperInsured();
            Vector3 Mous = Input.mousePosition;
            xMenu = Mous.x - 225;
            yMenu = Display.main.renderingHeight - Mous.y + 25;
            if (lastTank)
                GetInfo(lastTank);
        }
        public static bool IsTankNull()
        {
            return lastTank.IsNull();
        }

        internal class GUIDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isCurrentlyOpen && KickStart.CanUseMenu)
                {
                    AltUI.StartUI();
                    var title = AltUI.UIAlphaText + (!lastTank.name.NullOrEmpty() ? lastTank.name == "recycled tech" ? "None Selected" : lastTank.name : "NO NAME") + "</color>";
                    //"AI Mode Select"
                    HotWindow = GUI.Window(AIManagerID, HotWindow, GUIHandler, title, AltUI.MenuLeft);
                    if (UIHelpersExt.MouseIsOverSubMenu(HotWindow))
                        ManModGUI.IsMouseOverAnyModGUI = 2;
                    AltUI.EndUI();
                }
            }
        }

        private static void GUIHandler(int ID)
        {
            if (IsTankNull())
            {
                CloseSubMenuClickable();
                return;
            }

            changeAI = fetchAI;
            if (lastTank != null)
            {
                bool CantPerformActions;
                GUI.tooltip = lastTank.GetActionStatus(out CantPerformActions);

                /*
                 * Legacy colored text:
                 * <color=#f23d3dff> - Active Color
                 * (none) - Selectable
                 * <color=#808080ff> - Inactive
                 */

                bool stuckAnchored = lastTank.tank.IsAnchored && !lastTank.PlayerAllowAutoAnchoring;

                if (AdvancedToggles)
                {
                    GUIOptionsDisplay(stuckAnchored, CantPerformActions);
                    if (GUI.Button(new Rect(20, HotWindow.height - 85, 160, 30), new GUIContent("Back", "Main Controls"), AltUI.ButtonBlue))
                    {
                        AdvancedToggles = false;
                    }
                }
                else
                {
                    GUIMainDisplay(stuckAnchored, CantPerformActions);
                    if (GUI.Button(new Rect(20, HotWindow.height - 85, 160, 30), new GUIContent("More Options", "Advanced Settings"), AltUI.ButtonBlue))
                    {
                        AdvancedToggles = true;
                    }
                }
                

                GUI.Label(new Rect(20, HotWindow.height - 55, 160, 50), AltUI.UIAlphaText + GUI.tooltip + "</color>");
            }
            else
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SELECTED TANK IS NULL!");
                //lastTank = Singleton.Manager<ManPointer>.inst.targetVisible.transform.root.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>();
                CloseSubMenuClickable();
            }
            //GUI.DragWindow();
        }

        private static void GUIMainDisplay(bool stuckAnchored, bool CantPerformActions)
        {
            //GUI.Label(new Rect(20, 90, 160, 25), AltUI.UIAlphaText + (!lastTank.name.NullOrEmpty() ? lastTank.name == "recycled tech" ? "None Selected" : lastTank.name : "NO NAME") + "</color>");

            if (stuckAnchored)
            {
                GUIAnchored();
                GUIAnchorButton();
            }
            else if (!lastTank.ActuallyWorks)
            {
                GUINoAI();
            }
            else
            {
                //GUIMobileLegacy(CantPerformActions);
                //GUIAnchorButton();
                GUIMobile(CantPerformActions);
                GUIAnchorButtonLayout();
            }
        }
        private static void GUINoAI()
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 380);
            string textAnchor = "No AI!";
            if (GUI.Button(new Rect(20, 30, 160, 60), new GUIContent(textAnchor, "Cannot Control!"), AltUI.ButtonRed))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textError;
            if (SelfDestruct == 0)
            {
                textError = "<color=#ffffffff>SELF\nDESTRUCT</color>";
                if (GUI.Button(new Rect(20, 115, 160, 150), new GUIContent(textError, "DISMANTLE NOW"), AltUI.ButtonRed))
                {
                    if (!ManNetwork.IsNetworked)
                    {
                        if (lastTank.tank.visible.isActive)
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateOpen);
                        lastTank.tank.blockman.Disintegrate();
                        return;
                    }
                }
            }
            else
            {
                textError = "<color=#ffffffff>Self\nDestruct\nin " + SelfDestruct + "</color>";
                if (GUI.Button(new Rect(20, 115, 160, 150), new GUIContent(textError, "Click to dismantle Tech!"), AltUI.ButtonRed))
                {
                    if (!ManNetwork.IsNetworked)
                    {
                        SelfDestruct--;
                    }
                }
            }
            if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("No Control", "Needs working AI Module"), AltUI.ButtonGrey))
            {
            }
        }
        private static void GUIAnchored()
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 380);
            string textAnchor = "Base";
            if (GUI.Button(new Rect(20, 30, 160, 60), new GUIContent(textAnchor, "Stationary builder"), AltUI.ButtonGreen))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
            }

            string textDefend = "<color=#ffffffff>Guard\nBase</color>";
            if (GUI.Button(new Rect(20, 115, 160, 80), new GUIContent(textDefend, "Anchored"), AltUI.ButtonBlueActive))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCCab);
            }
            string textBuild = "<color=#ffffffff>Build</color>";
            if (GUI.Button(new Rect(20, 200, 160, 60), new GUIContent(textBuild, "Build a Tech"), AltUI.ButtonBlueActive))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCFabricator);
                PlayerRTSUI.ShowTechPlacementUI(lastTank);
            }
        }
        private static void GUIMobileLegacy(bool CantPerformActions)
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 380);
            bool clicked = false;
            bool clickedDriver = false;
            string textTank = "<color=#ffffffff>Tank</color>";
            if (GUI.Button(new Rect(20, 30, 80, 30), AIDriver == AIDriverType.Tank ? new GUIContent(textTank, "ACTIVE") : new GUIContent(textTank, "Drive on Wheels or Treads"),
                AIDriver == AIDriverType.Tank ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                AIDriver = AIDriverType.Tank;
                clickedDriver = true;
            }

            string textAir = "<color=#ffffffff>Pilot</color>";
            if (isAviatorAvail)
            {
                if (GUI.Button(new Rect(100, 30, 80, 30), AIDriver == AIDriverType.Pilot ? new GUIContent(textAir, "ACTIVE") : new GUIContent(textAir, "Fly Planes or Helicopters"),
                    AIDriver == AIDriverType.Pilot ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    AIDriver = AIDriverType.Pilot;
                    clickedDriver = true;
                }
            }
            else if (GUI.Button(new Rect(100, 30, 80, 30), new GUIContent(textAir, "Need HE or VEN AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textWater = "<color=#ffffffff>Ship</color>";
            if (isBuccaneerAvail && KickStart.isWaterModPresent)
            {
                if (GUI.Button(new Rect(20, 60, 80, 30), AIDriver == AIDriverType.Sailor ? new GUIContent(textWater, "ACTIVE") : new GUIContent(textWater, "Stay in Water"),
                    AIDriver == AIDriverType.Sailor ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    AIDriver = AIDriverType.Sailor;
                    clickedDriver = true;
                }
            }
            else if (GUI.Button(new Rect(20, 60, 80, 30), !KickStart.isWaterModPresent ? new GUIContent(textWater, "Need Water Mod") : new GUIContent(textWater, "Need GSO or VEN AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textSpace = "<color=#ffffffff>Space</color>";
            if (isAstrotechAvail)
            {
                if (GUI.Button(new Rect(100, 60, 80, 30), AIDriver == AIDriverType.Astronaut ? new GUIContent(textSpace, "ACTIVE") : new GUIContent(textSpace, "Fly with Antigravity or Hoverbug"),
                    AIDriver == AIDriverType.Astronaut ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    AIDriver = AIDriverType.Astronaut;
                    clickedDriver = true;
                }
            }
            else if (GUI.Button(new Rect(100, 60, 80, 30), new GUIContent(textSpace, "Need BF or HE AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            // Tasks
            // top - Escort
            string textEscort = "<color=#ffffffff>Escort</color>";
            if (GUI.Button(new Rect(20, 115, 80, 30), fetchAI == AIType.Escort ? new GUIContent(textEscort, "ACTIVE") : new GUIContent(textEscort, "Follows player"),
                fetchAI == AIType.Escort ? AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.Escort;
                clicked = true;
            }

            string textRTS = "<color=#ffffffff>Order</color>";
            if (KickStart.AllowPlayerRTSHUD)
            {
                if (GUI.Button(new Rect(100, 115, 80, 30), lastTank.RTSControlled ? new GUIContent(textRTS, "ACTIVE") : new GUIContent(textRTS, "Go to last target"),
                    lastTank.RTSControlled ? AltUI.ButtonGreenActive : AltUI.ButtonGreen))
                {
                    bool toTog = !lastTank.RTSControlled;
                    lastTank.SetRTSState(toTog);
                    int select = 0;
                    int amount = ManWorldRTS.inst.LocalPlayerTechsControlled.Count;
                    for (int step = 0; amount > step; step++)
                    {
                        TankAIHelper helper = ManWorldRTS.inst.LocalPlayerTechsControlled.ElementAt(step);
                        if ((bool)helper && helper != lastTank)
                        {
                            select++;
                            helper.SetRTSState(toTog);
                        }
                    }
                    if (select > 0)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": GUIAIManager - Set " + select + " Techs to RTSMode " + toTog);
                        inst.Invoke("DelayedExtraNoise", 0.15f);
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                    if (!lastTank)
                        return;
                }
            }
            else if (GUI.Button(new Rect(100, 115, 80, 30), new GUIContent(textRTS, "RTS Mode Disabled"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }


            // upper right - MT
            string textStation = "<color=#ffffffff>Static</color>";
            if (GUI.Button(new Rect(100, 145, 80, 30), CantPerformActions ? !lastTank.AllMT ? new GUIContent(textStation, "Player not in range") : new GUIContent(textStation, "Ally not in range") :
                fetchAI == AIType.MTStatic ? new GUIContent(textStation, "ACTIVE") : new GUIContent(textStation, "Mobile Tech Hardpoint"),
                fetchAI == AIType.MTStatic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.MTStatic;
                clicked = true;
            }
            string textTurret = "<color=#ffffffff>Turret</color>";
            if (GUI.Button(new Rect(100, 175, 80, 30), CantPerformActions ? !lastTank.AllMT ? new GUIContent(textTurret, "Player not in range") : new GUIContent(textTurret, "Ally not in range") :
                fetchAI == AIType.MTTurret ? new GUIContent(textTurret, "ACTIVE") : new GUIContent(textTurret, "Mobile Tech Turret"),
                fetchAI == AIType.MTTurret ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.MTTurret;
                clicked = true;
            }
            string textMimic = "<color=#ffffffff>Mimic</color>";
            if (GUI.Button(new Rect(100, 205, 80, 30), CantPerformActions ? !lastTank.AllMT ? new GUIContent(textMimic, "Player not in range") : new GUIContent(textMimic, "Ally not in range")
                : fetchAI == AIType.MTMimic ? new GUIContent(textMimic, "ACTIVE") : new GUIContent(textMimic, "Mobile Tech Copycat"),
                fetchAI == AIType.MTMimic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
            {
                changeAI = AIType.MTMimic;
                clicked = true;
            }


            // upper left, bottom - Aux modes
            string textMiner = "<color=#ffffffff>Miner</color>";
            if (isProspectorAvail)
            {
                if (GUI.Button(new Rect(20, 145, 80, 30), fetchAI == AIType.Prospector && !CantPerformActions ? new GUIContent(textMiner, "ACTIVE") : new GUIContent(textMiner, "Needs Receiver Base"),
                  fetchAI == AIType.Prospector ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Prospector;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(20, 145, 80, 30), new GUIContent(textMiner, "Need GSO or GC AI"),
                AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textAttack = "<color=#ffffffff>Scout</color>";
            if (isAssassinAvail)
            {
                if (GUI.Button(new Rect(20, 175, 80, 30), fetchAI == AIType.Assault && !CantPerformActions ? new GUIContent(textAttack, "ACTIVE") : new GUIContent(textAttack, "Needs Charging Base"),
                    fetchAI == AIType.Assault ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Assault;
                    clicked = true;

                }
            }
            else if (GUI.Button(new Rect(20, 175, 80, 30), new GUIContent(textAttack, "Need HE AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textProtect = "<color=#ffffffff>Protect</color>";
            if (isAegisAvail)
            {
                if (GUI.Button(new Rect(20, 205, 80, 30), CantPerformActions ? new GUIContent(textProtect, "No Allies Nearby") : fetchAI == AIType.Aegis ? new GUIContent(textProtect, "ACTIVE") : new GUIContent(textProtect, "Follow Closest Ally"),
                    fetchAI == AIType.Aegis ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Aegis;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(20, 205, 80, 30), new GUIContent(textProtect, "Need GSO AI"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            string textCharge = "<color=#ffffffff>Charger</color>";
            if (isEnergizerAvail)
            {
                if (GUI.Button(new Rect(20, 235, 80, 30), fetchAI == AIType.Energizer && !CantPerformActions ? new GUIContent(textCharge, "ACTIVE") : new GUIContent(textCharge, "Need Charge Base & Charger"),
                    fetchAI == AIType.Energizer ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Energizer;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(20, 235, 80, 30), new GUIContent(textCharge, "Need GC AI"), AltUI.ButtonGrey))
            {
            }

            string textScrap = "<color=#ffffffff>Fetch</color>";
            if (isScrapperAvail)
            {
                if (GUI.Button(new Rect(100, 235, 80, 30), fetchAI == AIType.Scrapper && !CantPerformActions ? new GUIContent(textScrap, "ACTIVE") : new GUIContent(textScrap, "Need Block Receiving Base"),
                    fetchAI == AIType.Scrapper ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue))
                {
                    changeAI = AIType.Scrapper;
                    clicked = true;
                }
            }
            else if (GUI.Button(new Rect(100, 235, 80, 30), new GUIContent(textScrap, "Need GC AI"), AltUI.ButtonGrey))
            {
            }

            if (clickedDriver)
            {
                SetOptionDriver(AIDriver);
            }
            if (clicked)
            {
                SetOption(changeAI);
            }
        }
        private static void GUIMobile(bool CantPerformActions)
        {
            HotWindow = new Rect(HotWindow.x, HotWindow.y, 200, 420);
            bool clicked = false;
            bool clickedDriver = false;
            Sprite sprite;
            GUIContent tankI;
            if (RawTechExporter.aiIcons.TryGetValue(AIType.Escort, out sprite))
            {
                tankI = AIDriver == AIDriverType.Tank ? new GUIContent(sprite.texture, "ACTIVE")
                    : new GUIContent(sprite.texture, "Drive on Wheels or Treads");
            }
            else
            {
                string textTank = "<color=#ffffffff>Tank</color>";
                tankI = AIDriver == AIDriverType.Tank ? new GUIContent(textTank, "ACTIVE") 
                    : new GUIContent(textTank, "Drive on Wheels or Treads");
            }
            GUILayoutOption GLO = GUILayout.MinWidth(HotWindow.width / 2.5f);
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6f);
            GUILayout.BeginHorizontal(GLH);
            if (GUILayout.Button(tankI, AIDriver == AIDriverType.Tank ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
            {
                AIDriver = AIDriverType.Tank;
                clickedDriver = true;
            }

            DriverButton("Pilot", AIDriverType.Pilot, AIType.Aviator, isAviatorAvail,
                "Fly Planes or Helicopters", "Need HE or VEN AI", ref clickedDriver);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            DriverButton("Ship", AIDriverType.Sailor, AIType.Buccaneer, isBuccaneerAvail && KickStart.isWaterModPresent,
                "Stay in Water", KickStart.isWaterModPresent ? "Need GSO or VEN AI" : "Need Water Mod", ref clickedDriver);

            DriverButton("Space", AIDriverType.Astronaut, AIType.Astrotech, isAstrotechAvail, "Fly with Antigravity or Hoverbug",
                "Need BF or HE AI", ref clickedDriver);
            GUILayout.EndHorizontal();
            GUILayout.Box("", GUILayout.Height(10));

            // Tasks
            // top - Escort
            //string textEscort = "<color=#ffffffff>Escort</color>";
            GUILayout.BeginHorizontal(GLH);
            //Texture texEscort = ManPlayerRTS.GetLineMat().mainTexture;
            //string texEscort = "<color=#ffffffff>Escort</color>";
            //if (GUILayout.Button(fetchAI == AIType.Escort ?
            if (GUILayout.Button(fetchAI == AIType.Escort ?
                new GUIContent(RawTechExporter.GuardAIIcon.texture, "ACTIVE") :
                new GUIContent(RawTechExporter.GuardAIIcon.texture, "Escort player"),
                fetchAI == AIType.Escort ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
            {
                changeAI = AIType.Escort;
                clicked = true;
            }

            Texture textRTS = ManWorldRTS.GetLineMat().mainTexture;
            //string textRTS = "<color=#ffffffff>Order</color>";
            if (KickStart.AllowPlayerRTSHUD)
            {
                if (GUILayout.Button(lastTank.RTSControlled ? new GUIContent(textRTS, "ACTIVE") : new GUIContent(textRTS, "Go to last target"),
                    lastTank.RTSControlled ? AltUI.ButtonGreenActive : AltUI.ButtonGreen, GLO, GLH))
                {
                    bool toTog = !lastTank.RTSControlled;
                    lastTank.SetRTSState(toTog);
                    int select = 0;
                    int amount = ManWorldRTS.inst.LocalPlayerTechsControlled.Count;
                    for (int step = 0; amount > step; step++)
                    {
                        TankAIHelper helper = ManWorldRTS.inst.LocalPlayerTechsControlled.ElementAt(step);
                        if ((bool)helper && helper != lastTank)
                        {
                            select++;
                            helper.SetRTSState(toTog);
                        }
                    }
                    if (select > 0)
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": GUIAIManager - Set " + select + " Techs to RTSMode " + toTog);
                        inst.Invoke("DelayedExtraNoise", 0.15f);
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                    if (!lastTank)
                        return;
                }
            }
            else if (GUILayout.Button(new GUIContent(textRTS, "RTS Mode Disabled"), AltUI.ButtonGrey, GLO, GLH))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            GUILayout.EndHorizontal();

            // upper right - MT
            GUILayout.BeginHorizontal(GLH);
            // upper left, bottom - Aux modes
            AuxButton("Miner", AIType.Prospector, isProspectorAvail, "Needs Receiver Base", "Mine Resources",
                "Need GSO or GC AI", ref CantPerformActions, ref clicked);
            if (RawTechExporter.aiIcons.TryGetValue(AIType.MTStatic, out sprite))
            {
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ? 
                    new GUIContent(sprite.texture, "Player not in range") : new GUIContent(sprite.texture, "Ally not in range") :
                    fetchAI == AIType.MTStatic ? new GUIContent(sprite.texture, "ACTIVE") : new GUIContent(sprite.texture, "Mobile Tech Hardpoint"),
                    fetchAI == AIType.MTStatic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = AIType.MTStatic;
                    clicked = true;
                }
            }
            else
            {
                string textStation = "<color=#ffffffff>Static</color>";
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ? new GUIContent(textStation, "Player not in range") : new GUIContent(textStation, "Ally not in range") :
                    fetchAI == AIType.MTStatic ? new GUIContent(textStation, "ACTIVE") : new GUIContent(textStation, "Mobile Tech Hardpoint"),
                    fetchAI == AIType.MTStatic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = AIType.MTStatic;
                    clicked = true;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            AuxButton("Scout", AIType.Assault, isAssassinAvail, "Needs Charging Base", "Attack Enemies",
                 "Need HE AI", ref CantPerformActions, ref clicked);

            if (RawTechExporter.aiIcons.TryGetValue(AIType.MTTurret, out sprite))
            {
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ?
                    new GUIContent(sprite.texture, "Player not in range") : new GUIContent(sprite.texture, "Ally not in range") :
                    fetchAI == AIType.MTTurret ? new GUIContent(sprite.texture, "ACTIVE") : new GUIContent(sprite.texture, "Mobile Tech Turret"),
                    fetchAI == AIType.MTTurret ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = AIType.MTTurret;
                    clicked = true;
                }
            }
            else
            {
                string textTurret = "<color=#ffffffff>Turret</color>";
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ? new GUIContent(textTurret, "Player not in range") : new GUIContent(textTurret, "Ally not in range") :
                    fetchAI == AIType.MTTurret ? new GUIContent(textTurret, "ACTIVE") : new GUIContent(textTurret, "Mobile Tech Turret"),
                    fetchAI == AIType.MTTurret ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = AIType.MTTurret;
                    clicked = true;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            AuxButton("Protect", AIType.Aegis, isAegisAvail, "No Allies Nearby", "Follow Closest Ally",
                 "Need GSO AI", ref CantPerformActions, ref clicked);

            if (RawTechExporter.aiIcons.TryGetValue(AIType.MTMimic, out sprite))
            {
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ?
                    new GUIContent(sprite.texture, "Player not in range") : new GUIContent(sprite.texture, "Ally not in range")
                    : fetchAI == AIType.MTMimic ? new GUIContent(sprite.texture, "ACTIVE") : new GUIContent(sprite.texture, "Mobile Tech Copycat"),
                    fetchAI == AIType.MTMimic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = AIType.MTMimic;
                    clicked = true;
                }
            }
            else
            {
                string textMimic = "<color=#ffffffff>Mimic</color>";
                if (GUILayout.Button(CantPerformActions ? !lastTank.AllMT ? new GUIContent(textMimic, "Player not in range") : new GUIContent(textMimic, "Ally not in range")
                    : fetchAI == AIType.MTMimic ? new GUIContent(textMimic, "ACTIVE") : new GUIContent(textMimic, "Mobile Tech Copycat"),
                    fetchAI == AIType.MTMimic ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                {
                    changeAI = AIType.MTMimic;
                    clicked = true;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GLH);
            AuxButton("Charger", AIType.Energizer, isEnergizerAvail, "Need Charge Base & Charger", "Recharge Closest Ally",
                 "Need GC AI", ref CantPerformActions, ref clicked);

            AuxButton("Fetch", AIType.Scrapper, isEnergizerAvail, "Need Block Receiving Base", "Collect Blocks",
                 "Need GC AI", ref CantPerformActions, ref clicked);
            GUILayout.EndHorizontal();

            if (clickedDriver)
            {
                SetOptionDriver(AIDriver);
            }
            if (clicked)
            {
                SetOption(changeAI);
            }
        }
        private static void GUIAnchorButton()
        {
            if (!lastTank.tank.IsAnchored)//(lastTank.PlayerAllowAutoAnchoring)
            {
                if (lastTank.tank.Anchors.NumPossibleAnchors < 1)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("No Anchors", "Needs working anchors"), AltUI.ButtonGrey);
                else if (!lastTank.CanAnchorSafely)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Enemy Jammed", "Enemy too close!"), AltUI.ButtonRed);
                else if (!lastTank.CanAttemptAnchor)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Rough Terrain", "Too rough to deploy anchors. Try somewhere else."), AltUI.ButtonRed);
                else
                {
                    if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Stop & Anchor", "Fixate to ground"), AltUI.ButtonGreen))
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                        if (ManNetwork.IsHost)
                        {
                            lastTank.PlayerAllowAutoAnchoring = false;
                            lastTank.TryInsureAnchor();
                        }
                        AIDriver = AIDriverType.Stationary;
                        changeAI = AIType.Escort;
                        SetOptionDriver(AIDriver);
                        SetOption(changeAI);
                    }
                }
            }
            else if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Mobilize", "Detach from ground"), AltUI.ButtonGreen))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                if (ManNetwork.IsHost)
                {
                    lastTank.Unanchor();
                    lastTank.PlayerAllowAutoAnchoring = true;
                }
                AIDriver = AIDriverType.AutoSet;
                SetOptionDriver(AIDriver);
            }
        }
        private static void GUIAnchorButtonLayout()
        {
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6.25f);
            if (lastTank.PlayerAllowAutoAnchoring)
            {
                if (lastTank.tank.Anchors.NumPossibleAnchors < 1)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("No Anchors", "Needs working anchors"), AltUI.ButtonGrey);
                else if (!lastTank.CanAnchorSafely)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Enemy Jammed", "Enemy too close!"), AltUI.ButtonRed);
                else if (!lastTank.CanAttemptAnchor)
                    GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Rough Terrain", "Too rough to deploy anchors. Try somewhere else."), AltUI.ButtonRed);
                else
                {
                    if (GUILayout.Button(new GUIContent("Stop & Anchor", "Fixate to ground"), AltUI.ButtonGreen, GLH))
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                        if (ManNetwork.IsHost)
                        {
                            lastTank.PlayerAllowAutoAnchoring = false;
                            lastTank.TryInsureAnchor();
                        }
                        AIDriver = AIDriverType.Stationary;
                        changeAI = AIType.Escort;
                        SetOptionDriver(AIDriver);
                        SetOption(changeAI);
                    }
                }
            }
            else if (GUILayout.Button(new GUIContent("Mobilize", "Detach from ground"), AltUI.ButtonGreen, GLH))
            {
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                if (ManNetwork.IsHost)
                {
                    lastTank.Unanchor();
                    lastTank.PlayerAllowAutoAnchoring = true;
                }
                if (AIDriver == AIDriverType.Stationary)
                {
                    AIDriver = AIDriverType.AutoSet;
                    SetOptionDriver(AIDriver);
                }
            }
        }

        /// <summary>
        /// Pending - allow toggling of AI special operations
        /// </summary>
        private static void GUIOptionsDisplay(bool stuckAnchored, bool CantPerformActions)
        {
            bool delta = false;

            var lim = lastTank.AILimitSettings;
            var set = lastTank.AISetSettings;
            
            StatusLabel(new Rect(20, 30, 160, 30), "Range: " + lastTank.MaxCombatRange, "Maximum combat range");
            set.ChaseRange = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(25, 30, 150, 30), lastTank.MaxCombatRange,
                0, lim.ChaseRange, AltUI.ScrollHorizontalTransparent, AltUI.ScrollThumbTransparent));
            if (!lastTank.MinCombatRange.Approximately(set.ChaseRange))
                delta = true;

            StatusLabel(new Rect(20, 60, 160, 30), "Spacing: " + lastTank.MinCombatRange, "Spacing from target");
            set.CombatRange = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(25, 60, 150, 30), lastTank.MinCombatRange,
               0, lim.CombatRange, AltUI.ScrollHorizontalTransparent, AltUI.ScrollThumbTransparent));
            if (!lastTank.MinCombatRange.Approximately(set.CombatRange))
                delta = true;


            StatusLabelButton(new Rect(20, 115, 80, 30), "Aware", lastTank.SecondAvoidence, 
                "Better pathing", "Need Non-Anchor AI", ref delta);
            StatusLabelButton(new Rect(100, 115, 80, 30), "Crafty", lastTank.AutoAnchor, 
                "Anchor when idle", "Need GC AI", ref delta);

            set.GUIDisplay(lim, ref delta);

            lastTank.AttackMode = (EAttackMode)Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(25, 235, 150, 30), (int)lastTank.AttackMode, 0, (int)EAttackMode.Ranged));
            StatusLabel(new Rect(20, 235, 160, 30), "Mode: " + lastTank.AttackMode, "Attack method");

            if (delta)
            {
                set.Sync(lastTank, lastTank.AILimitSettings);
                lastTank.AISetSettings = set;
            }
        }

        private static void StatusLabel(Rect pos, string title, string desc)
        {
            string label = "<color=#ffffffff>" + title + "</color>";
            GUI.Label(pos, new GUIContent(label, desc), AltUI.ButtonBlue);
        }
        private static void StatusLabelButton(Rect pos, string title, bool status, string desc, 
            string requirement, ref bool delta)
        {
            string label = "<color=#ffffffff>" + title + "</color>";
            if (status)
            {
                if (GUI.Button(pos, new GUIContent(label, desc), AltUI.ButtonBlue))
                {
                }
            }
            else if (GUI.Button(pos, new GUIContent(label, requirement), AltUI.ButtonGrey))
            {
            }
        }
        internal static void StatusLabelButtonToggle(Rect pos, string title, bool can, ref bool CurState, 
            string desc, string requirement, ref bool delta)
        {
            string label = "<color=#ffffffff>" + title + "</color>";
            if (can)
            {
                if (CurState)
                {
                    if (GUI.Button(pos, new GUIContent(label, desc), AltUI.ButtonGreen))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                        CurState = false;
                        delta = true;
                    }
                }
                else
                {
                    if (GUI.Button(pos, new GUIContent(label, desc), AltUI.ButtonRed))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                        CurState = true;
                        delta = true;
                    }
                }
            }
            else if (GUI.Button(pos, new GUIContent(label, requirement), AltUI.ButtonGrey))
            {
            }
        }

        private static void DriverButton(string title, AIDriverType type, AIType displayType, bool isAvail, 
            string Desc, string reqAI, ref bool clickedDriver)
        {
            GUILayoutOption GLO = GUILayout.MinWidth(HotWindow.width / 2.5f);
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6f);
            if (RawTechExporter.aiIcons.TryGetValue(displayType, out Sprite sprite))
            {
                if (isAvail)
                {
                    if (GUILayout.Button(AIDriver == type ? new GUIContent(sprite.texture, "ACTIVE") : new GUIContent(sprite.texture, Desc),
                      AIDriver == type ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        AIDriver = type;
                        clickedDriver = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(sprite.texture, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            else
            {
                string textTitle = "<color=#ffffffff>" + title + "</color>";
                if (isAvail)
                {
                    if (GUILayout.Button(AIDriver == type ? new GUIContent(textTitle, "ACTIVE") : new GUIContent(textTitle, Desc),
                      AIDriver == type ? AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        AIDriver = type;
                        clickedDriver = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(textTitle, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
        }

        private static void AuxButton(string title, AIType type, bool isAvail, string runReq, string desc, string reqAI, ref bool CantPerformActions, ref bool clicked)
        {
            GUILayoutOption GLO = GUILayout.MinWidth(HotWindow.width / 2.5f);
            GUILayoutOption GLH = GUILayout.Height(HotWindow.width / 6f);
            if (RawTechExporter.aiIcons.TryGetValue(type, out Sprite sprite))
            {
                if (isAvail)
                {
                    if (GUILayout.Button(fetchAI == type && 
                        CantPerformActions ? new GUIContent(sprite.texture, runReq) 
                        : fetchAI == AIType.Aegis ? new GUIContent(sprite.texture, "ACTIVE") : new GUIContent(sprite.texture, desc),
                      fetchAI == type ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        changeAI = type;
                        clicked = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(sprite.texture, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            else
            {
                string textTitle = "<color=#ffffffff>" + title + "</color>";
                if (isAvail)
                {
                    if (GUILayout.Button(fetchAI == type &&
                        CantPerformActions ? new GUIContent(textTitle, runReq)
                        : fetchAI == type ? new GUIContent(textTitle, "ACTIVE") : new GUIContent(textTitle, desc),
                      fetchAI == type ? CantPerformActions ? AltUI.ButtonRedActive : AltUI.ButtonBlueActive : AltUI.ButtonBlue, GLO, GLH))
                    {
                        changeAI = type;
                        clicked = true;
                    }
                }
                else if (GUILayout.Button(new GUIContent(textTitle, reqAI),
                    AltUI.ButtonGrey, GLO, GLH))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
        }


        internal static FieldInfo bubble = typeof(Tank).GetField("m_Overlay", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static void SetOptionDriver(AIDriverType driver, bool playSFX = true)
        {
            try
            {
                if (!lastTank)
                    return;
                if (!lastTank.tank)
                    return;
                SetOptionDriverCase(lastTank, driver);
                inst.TrySetOptionDriverRTS(driver);
                if (playSFX)
                {
                    switch (driver)
                    {
                        case AIDriverType.Tank:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCCab);
                            break;
                        case AIDriverType.Pilot:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFSmallSkidClose);
                            break;
                        case AIDriverType.Sailor:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                            break;
                        case AIDriverType.Astronaut:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFAntiGravEco);
                            break;
                        case AIDriverType.Stationary:
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCDeliCannon);
                            break;
                        default:
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                            break;
                    }
                }
                //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            }
            catch { }
        }
        private void TrySetOptionDriverRTS(AIDriverType driver)
        {
            if (!(bool)ManWorldRTS.inst)
                return;
            if (ManWorldRTS.PlayerIsInRTS || ManWorldRTS.PlayerRTSOverlay)
            {
                int select = 0;
                int amount = ManWorldRTS.inst.LocalPlayerTechsControlled.Count;
                for (int step = 0; amount > step; step++)
                {
                    TankAIHelper helper = ManWorldRTS.inst.LocalPlayerTechsControlled.ElementAt(step);
                    if ((bool)helper && helper != lastTank)
                    {
                        select++;
                        SetOptionDriverCase(helper, driver);
                    }
                }
                DebugTAC_AI.Log(KickStart.ModID + ": TrySetOptionRTS - Set " + amount + " Techs to drive " + driver);
                if (select > 0)
                    Invoke("DelayedExtraNoise", 0.15f);
            }
        }
        private static void SetOptionDriverCase(TankAIHelper helper, AIDriverType driver)
        {
            if (helper.IsNull())
                return;
            bool guess = driver == AIDriverType.AutoSet;
            if (guess)
                DebugTAC_AI.Log(KickStart.ModID + ": Given " + lastTank.name + " set to driver " + driver);
            else
                DebugTAC_AI.Assert(KickStart.ModID + ": Set " + lastTank.name + " to driver " + driver);
            AIDriverType locDediAI = AIDriverType.Tank;
            switch (driver)
            {
                case AIDriverType.Astronaut:
                    if (helper.isAstrotechAvail)
                        locDediAI = driver;
                    break;
                case AIDriverType.Pilot:
                    if (helper.isAviatorAvail)
                        locDediAI = driver;
                    break;
                case AIDriverType.Sailor:
                    if (helper.isBuccaneerAvail)
                        locDediAI = driver;
                    break;
                case AIDriverType.Stationary:
                    if (helper.tank.Anchors.NumPossibleAnchors < 1 || !helper.CanAnchorNow)
                        return;
                    break;
                case AIDriverType.AutoSet:
                    break;
                default:
                    DebugTAC_AI.LogError(KickStart.ModID + ": Encountered illegal AIDriverType on AI Driver switch!");
                    break;
            }
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(helper.tank.netTech.netId.Value, AIType.Null, locDediAI);
                    helper.OnSwitchAI(false);
                    if (guess)
                        helper.ExecuteAutoSetNoCalibrate();
                    else
                        helper.SetDriverType(driver);
                    helper.ForceAllAIsToEscort(true, false);
                    helper.ForceRebuildAlignment();
                    if (helper.DriverType != driver)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                        AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                    }
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                helper.OnSwitchAI(false);
                if (driver == AIDriverType.AutoSet)
                    helper.ExecuteAutoSetNoCalibrate();
                else
                    helper.SetDriverType(driver);
                //DebugTAC_AI.Log(KickStart.ModID + ": 1");
                helper.ForceAllAIsToEscort(true, true);
                //DebugTAC_AI.Log(KickStart.ModID + ": 2");
                helper.ForceRebuildAlignment();
                //DebugTAC_AI.Log(KickStart.ModID + ": 3");
                if (helper.DriverType != driver)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                    AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                }
                DebugTAC_AI.Log(KickStart.ModID + ": 41");
            }
            if (guess)
                DebugTAC_AI.Assert(KickStart.ModID + ": Set " + lastTank.name + " to driver " + driver);
        }

        public static void SetOption(AIType dediAI, bool playSFX = true)
        {
            if (lastTank.IsNull())
                return;
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, dediAI, AIDriverType.Null);
                    lastTank.OnSwitchAI(true);
                    if (lastTank.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    lastTank.DediAI = dediAI;
                    lastTank.ForceAllAIsToEscort(true, false);
                    fetchAI = dediAI;
                    lastTank.ForceRebuildAlignment();

                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                lastTank.OnSwitchAI(true);
                if (lastTank.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                lastTank.DediAI = dediAI;
                lastTank.ForceAllAIsToEscort(true, false);
                fetchAI = dediAI;
                lastTank.ForceRebuildAlignment();

            }
            inst.TrySetOptionRTS(dediAI);
            if (playSFX)
            {
                switch (dediAI)
                {
                    case AIType.Escort:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                        break;
                    case AIType.Assault:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                        break;
                    case AIType.Aegis:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIGuard);
                        break;
                    case AIType.Prospector:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.RadarOn);
                        break;
                    case AIType.Scrapper:
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Craft);
                        break;
                    case AIType.Energizer:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFAirReceiver);
                        break;
                    case AIType.MTTurret:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                        break;
                    case AIType.MTStatic:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                        break;
                    case AIType.MTMimic:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCPayTerminal);
                        break;
                    case AIType.Aviator:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFSmallSkidClose);
                        break;
                    case AIType.Buccaneer:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                        break;
                    case AIType.Astrotech:
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFAntiGravEco);
                        break;
                    default:
                        Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                        break;
                }
            }
            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            //CloseSubMenuClickable();
        }
        private void TrySetOptionRTS(AIType dediAI)
        {
            if (!(bool)ManWorldRTS.inst)
                return;
            if (ManWorldRTS.PlayerIsInRTS || ManWorldRTS.PlayerRTSOverlay)
            {
                int select = 0;
                int amount = ManWorldRTS.inst.LocalPlayerTechsControlled.Count;
                for (int step = 0; amount > step; step++)
                {
                    TankAIHelper helper = ManWorldRTS.inst.LocalPlayerTechsControlled.ElementAt(step);
                    if ((bool)helper && helper != lastTank)
                    {
                        select++;
                        SetOptionCase(helper, dediAI);
                    }
                }
                DebugTAC_AI.Log(KickStart.ModID + ": TrySetOptionRTS - Set " + amount + " Techs to mode " + dediAI);
                if (select > 0)
                    Invoke("DelayedExtraNoise", 0.15f);
            }
        }
        private static void SetOptionCase(TankAIHelper helper, AIType dediAI)
        {
            if (helper.IsNull())
                return;
            AIType locDediAI;
            switch (dediAI)
            {
                case AIType.Aegis:
                    if (helper.isAegisAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Aviator:
                    if (helper.isAviatorAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Buccaneer:
                    if (helper.isBuccaneerAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Energizer:
                    if (helper.isEnergizerAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Prospector:
                    if (helper.isProspectorAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.MTStatic;
                    break;
                case AIType.Scrapper:
                    if (helper.isScrapperAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.MTStatic;
                    break;
                default:
                    locDediAI = dediAI;
                    break;
            }
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(helper.tank.netTech.netId.Value, locDediAI, AIDriverType.Null);
                    helper.OnSwitchAI(true);
                    if (helper.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    helper.DediAI = locDediAI;
                    helper.ForceAllAIsToEscort(true, false);
                    helper.ForceRebuildAlignment();

                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                helper.OnSwitchAI(false);
                if (helper.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(helper.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                helper.DediAI = locDediAI;
                helper.ForceAllAIsToEscort(true, false);
                helper.ForceRebuildAlignment();
            }
        }
        internal void DelayedExtraNoise()
        {
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
        }


        private static bool isAssassinAvail = false;    //Is there an Assassin-enabled AI on this tech?
        private static bool isAegisAvail = false;       //Is there an Aegis-enabled AI on this tech?

        private static bool isProspectorAvail = false;  //Is there a Prospector-enabled AI on this tech?
        private static bool isScrapperAvail = false;    //Is there a Scrapper-enabled AI on this tech?
        private static bool isEnergizerAvail = false;   //Is there a Energizer-enabled AI on this tech?

        private static bool isAviatorAvail = false;
        private static bool isAstrotechAvail = false;
        private static bool isBuccaneerAvail = false;

        internal static void ResetInfo()
        {
            isAegisAvail = false;
            isAssassinAvail = false;

            isProspectorAvail = false;
            isScrapperAvail = false;
            isEnergizerAvail = false;

            isAstrotechAvail = false;
            isAviatorAvail = false;
            isBuccaneerAvail = false;
        }
        internal static void GetInfo(TankAIHelper helper)
        {
            if (helper.isAegisAvail)
                isAegisAvail = true;
            if (helper.isAssassinAvail)
                isAssassinAvail = true;

            // Collectors
            if (helper.isProspectorAvail)
                isProspectorAvail = true;
            if (helper.isScrapperAvail)
                isScrapperAvail = true;
            if (helper.isEnergizerAvail)
                isEnergizerAvail = true;

            // Pilots
            if (helper.isAviatorAvail)
                isAviatorAvail = true;
            if (helper.isBuccaneerAvail)
                isBuccaneerAvail = true;
            if (helper.isAstrotechAvail)
                isAstrotechAvail = true;
        }


        internal static void LaunchSubMenuClickableRTS()
        {
            if (!KickStart.EnableBetterAI || !ManWorldRTS.inst.IsNotNull())
            {
                return;
            }
            if (ManWorldRTS.inst.Leading)
            {
                try
                {
                    if (ManWorldRTS.PlayerIsInRTS || ManWorldRTS.PlayerRTSOverlay)
                    {
                        if (ManWorldRTS.inst.LocalPlayerTechsControlled.Count > 0)
                        {
                            Vector3 Mous = Input.mousePosition;
                            xMenu = Mous.x - (10 + HotWindow.width);
                            yMenu = Display.main.renderingHeight - Mous.y + 10;
                            lastTank = ManWorldRTS.inst.Leading;
                        }
                        else
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": No techs selected!");
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                            return;
                        }
                    }
                    else
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": No techs selected!");
                        return;
                    }
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": TANK IS NULL!");
                    return;
                }
            }
            if (!lastTank)
                return;

            lastTank.RefreshAI();
            GetInfo(lastTank);
            DebugTAC_AI.Log(KickStart.ModID + ": Opened AI menu!");
            AIDriver = lastTank.DriverType;
            fetchAI = lastTank.DediAI;
            MoveMenuToCursor(true);
            isCurrentlyOpen = true;
            windowTimer = 0.25f;
            GUIWindow.SetActive(true);
        }
        internal static void LaunchSubMenuClickable(bool centerOnMouse = false)
        {
            if (!KickStart.EnableBetterAI)
            {
                return;
            }
            if (IsTankNull())
            {
                DebugTAC_AI.Assert(true, KickStart.ModID + ": TANK IS NULL!");
                return;
            }
            lastTank.RefreshAI();
            GetInfo(lastTank);
            AdvancedToggles = false;
            SelfDestruct = 5;
            DebugTAC_AI.Log(KickStart.ModID + ": Opened AI menu!");
            AIDriver = lastTank.DriverType;
            fetchAI = lastTank.DediAI;
            MoveMenuToCursor(centerOnMouse);
            isCurrentlyOpen = true;
            if (centerOnMouse)
                windowTimer = 0.25f;
            else
                windowTimer = 2.25f;
            GUIWindow.SetActive(true);
        }
        internal static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                lastTank = null;
                ResetInfo();
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                DebugTAC_AI.Log(KickStart.ModID + ": Closed AI menu!");
            }
        }

        internal static void MoveMenuToCursor(bool centerOnMouse)
        {
            if (centerOnMouse)
            {
                Vector3 Mous = Input.mousePosition;
                xMenu = Mous.x - (HotWindow.width / 2);
                yMenu = Display.main.renderingHeight - Mous.y - 90;
            }
            xMenu = Mathf.Clamp(xMenu, 0, Display.main.renderingWidth - HotWindow.width);
            yMenu = Mathf.Clamp(yMenu, 0, Display.main.renderingHeight - HotWindow.height);
            HotWindow.x = xMenu;
            HotWindow.y = yMenu;
        }
       

        private void Update()
        {
            if (windowTimer > 0)
            {
                windowTimer -= Time.deltaTime;
            }
            if (windowTimer < 0 && !UIHelpersExt.MouseIsOverSubMenu(HotWindow))
            {
                CloseSubMenuClickable();
                windowTimer = 0;
            }
            if (ManPauseGame.inst.IsPaused)
            {
                if (windowTimer >= 0)
                {
                    CloseSubMenuClickable();
                    windowTimer = 0;
                }
            }
            else
            {
                if (Input.GetKeyDown(KickStart.ModeSelect))
                {
                    if (!isCurrentlyOpen)
                        LaunchSubMenuClickableRTS();
                    else
                    {
                        CloseSubMenuClickable();
                        windowTimer = 0;
                    }
                }
            }
        }
    }
}
