using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;
using TAC_AI.World;

namespace TAC_AI
{
    public class GUIAIManager : MonoBehaviour
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
        internal static AIECore.TankAIHelper lastTank;

        // Mode - Setting
        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 350);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;

        // Tech Tracker


        private static float windowTimer = 0;
        private const int AIManagerID = 8001;


        public static void Initiate()
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
        public static void DeInit()
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
            lastTank = tank.trans.GetComponent<AIECore.TankAIHelper>();
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
                    AIGlobals.FetchResourcesFromGame(); 
                    AIGlobals.StartUI();
                    HotWindow = GUI.Window(AIManagerID, HotWindow, GUIHandler, "AI Mode Select", AIGlobals.MenuLeft);
                    AIGlobals.EndUI();
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

            bool clicked = false;
            bool clickedDriver = false;
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

                bool stuckAnchored = lastTank.tank.IsAnchored && !lastTank.PlayerAllowAnchoring;

                // Drivers
                if (stuckAnchored)
                {
                    string textAnchor = "Base";
                    if (GUI.Button(new Rect(20, 30, 160, 60), new GUIContent(textAnchor, "Stationary builder"), AIGlobals.ButtonGreen))
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
                    }
                }
                else if (!lastTank.ActuallyWorks)
                {
                    string textAnchor = "No AI!";
                    if (GUI.Button(new Rect(20, 30, 160, 60), new GUIContent(textAnchor, "Error"), AIGlobals.ButtonRed))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }
                }
                else
                {
                    string textTank = "<color=#ffffffff>Tank</color>";
                    if (GUI.Button(new Rect(20, 30, 80, 30), AIDriver == AIDriverType.Tank ? new GUIContent(textTank, "ACTIVE") : new GUIContent(textTank, "Drive on Wheels or Treads"),
                        AIDriver == AIDriverType.Tank ? AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                    {
                        AIDriver = AIDriverType.Tank;
                        clickedDriver = true;
                    }

                    string textAir = "<color=#ffffffff>Pilot</color>";
                    if (isAviatorAvail)
                    {
                        if (GUI.Button(new Rect(100, 30, 80, 30), AIDriver == AIDriverType.Pilot ? new GUIContent(textAir, "ACTIVE") : new GUIContent(textAir, "Fly Planes or Helicopters"),
                            AIDriver == AIDriverType.Pilot ? AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            AIDriver = AIDriverType.Pilot;
                            clickedDriver = true;
                        }
                    }
                    else if (GUI.Button(new Rect(100, 30, 80, 30), new GUIContent(textAir, "Need HE or VEN AI"), AIGlobals.ButtonGrey))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }

                    string textWater = "<color=#ffffffff>Ship</color>";
                    if (isBuccaneerAvail && KickStart.isWaterModPresent)
                    {
                        if (GUI.Button(new Rect(20, 60, 80, 30), AIDriver == AIDriverType.Sailor ? new GUIContent(textWater, "ACTIVE") : new GUIContent(textWater, "Stay in Water"),
                            AIDriver == AIDriverType.Sailor ? AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            AIDriver = AIDriverType.Sailor;
                            clickedDriver = true;
                        }
                    }
                    else if (GUI.Button(new Rect(20, 60, 80, 30), !KickStart.isWaterModPresent ? new GUIContent(textWater, "Need Water Mod") : new GUIContent(textWater, "Need GSO or VEN AI"), AIGlobals.ButtonGrey))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }

                    string textSpace = "<color=#ffffffff>Space</color>";
                    if (isAstrotechAvail)
                    {
                        if (GUI.Button(new Rect(100, 60, 80, 30), AIDriver == AIDriverType.Astronaut ? new GUIContent(textSpace, "ACTIVE") : new GUIContent(textSpace, "Fly with Antigravity or Hoverbug"),
                            AIDriver == AIDriverType.Astronaut ? AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            AIDriver = AIDriverType.Astronaut;
                            clickedDriver = true;
                        }
                    }
                    else if (GUI.Button(new Rect(100, 60, 80, 30), new GUIContent(textSpace, "Need BF or HE AI"), AIGlobals.ButtonGrey))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }
                }

                GUI.Label(new Rect(20, 90, 160, 25), AIGlobals.UIAlphaText + (!lastTank.name.NullOrEmpty() ? lastTank.name == "recycled tech" ? "None Selected" : lastTank.name : "NO NAME") + "</color>");

                if (stuckAnchored)
                {
                    string textDefend = "<color=#ffffffff>Guard\nBase</color>";
                    if (GUI.Button(new Rect(20, 115, 160, 150), new GUIContent(textDefend, "Anchored Turret"), AIGlobals.ButtonBlueActive))
                    {
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCCab);
                    }
                }
                else if (!lastTank.ActuallyWorks)
                {
                    string textError = "<color=#ffffffff>Self\nDestruct</color>";
                    if (GUI.Button(new Rect(20, 115, 160, 150), new GUIContent(textError, "Destroy this Tech"), AIGlobals.ButtonRed))
                    {
                        if (!ManNetwork.IsNetworked)
                        {
                            lastTank.tank.blockman.Disintegrate();
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateOpen);
                            return;
                        }
                    }
                }
                else
                {
                    // Tasks
                    // top - Escort
                    string textEscort = "<color=#ffffffff>Escort</color>";
                    if (GUI.Button(new Rect(20, 115, 80, 30), fetchAI == AIType.Escort ? new GUIContent(textEscort, "ACTIVE") : new GUIContent(textEscort, "Follows player"),
                        fetchAI == AIType.Escort ? AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                    {
                        changeAI = AIType.Escort;
                        clicked = true;
                    }

                    string textRTS = "<color=#ffffffff>Order</color>";
                    if (KickStart.AllowStrategicAI)
                    {
                        if (GUI.Button(new Rect(100, 115, 80, 30), lastTank.RTSControlled ? new GUIContent(textRTS, "ACTIVE") : new GUIContent(textRTS, "Go to last target"),
                            lastTank.RTSControlled ? AIGlobals.ButtonGreenActive : AIGlobals.ButtonGreen))
                        {
                            bool toTog = !lastTank.RTSControlled;
                            lastTank.SetRTSState(toTog);
                            int select = 0;
                            int amount = ManPlayerRTS.inst.LocalPlayerTechsControlled.Count;
                            for (int step = 0; amount > step; step++)
                            {
                                AIECore.TankAIHelper tankInst = ManPlayerRTS.inst.LocalPlayerTechsControlled.ElementAt(step);
                                if ((bool)tankInst && tankInst != lastTank)
                                {
                                    select++;
                                    tankInst.SetRTSState(toTog);
                                }
                            }
                            if (select > 0)
                            {
                                DebugTAC_AI.Log("TACtical_AI: GUIAIManager - Set " + select + " Techs to RTSMode " + toTog);
                                inst.Invoke("DelayedExtraNoise", 0.15f);
                            }
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                            if (!lastTank)
                                return;
                        }
                    }
                    else if (GUI.Button(new Rect(100, 115, 80, 30), new GUIContent(textRTS, "RTS Mode Disabled"), AIGlobals.ButtonGrey))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }


                    // upper right - MT
                    string textStation = "<color=#ffffffff>Static</color>";
                    if (GUI.Button(new Rect(100, 145, 80, 30), CantPerformActions ? lastTank.OnlyPlayerMT ? new GUIContent(textStation, "Player not in range") : new GUIContent(textStation, "Ally not in range") :
                        fetchAI == AIType.MTStatic ? new GUIContent(textStation, "ACTIVE") : new GUIContent(textStation, "Mobile Tech Hardpoint"),
                        fetchAI == AIType.MTStatic ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                    {
                        changeAI = AIType.MTStatic;
                        clicked = true;
                    }
                    string textTurret = "<color=#ffffffff>Turret</color>";
                    if (GUI.Button(new Rect(100, 175, 80, 30), CantPerformActions ? lastTank.OnlyPlayerMT ? new GUIContent(textTurret, "Player not in range") : new GUIContent(textTurret, "Ally not in range") :
                        fetchAI == AIType.MTTurret ? new GUIContent(textTurret, "ACTIVE") : new GUIContent(textTurret, "Mobile Tech Turret"),
                        fetchAI == AIType.MTTurret ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                    {
                        changeAI = AIType.MTTurret;
                        clicked = true;
                    }
                    string textMimic = "<color=#ffffffff>Mimic</color>";
                    if (GUI.Button(new Rect(100, 205, 80, 30), CantPerformActions ? lastTank.OnlyPlayerMT ? new GUIContent(textMimic, "Player not in range") : new GUIContent(textMimic, "Ally not in range")
                        : fetchAI == AIType.MTMimic ? new GUIContent(textMimic, "ACTIVE") : new GUIContent(textMimic, "Mobile Tech Copycat"),
                        fetchAI == AIType.MTMimic ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                    {
                        changeAI = AIType.MTMimic;
                        clicked = true;
                    }


                    // upper left, bottom - Aux modes
                    string textMiner = "<color=#ffffffff>Miner</color>";
                    if (isProspectorAvail)
                    {
                        if (GUI.Button(new Rect(20, 145, 80, 30), fetchAI == AIType.Prospector && !CantPerformActions ? new GUIContent(textMiner, "ACTIVE") : new GUIContent(textMiner, "Needs Receiver Base"),
                          fetchAI == AIType.Prospector ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            changeAI = AIType.Prospector;
                            clicked = true;
                        }
                    }
                    else if (GUI.Button(new Rect(20, 145, 80, 30), new GUIContent(textMiner, "Need GSO or GC AI"),
                        AIGlobals.ButtonGrey))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }

                    string textAttack = "<color=#ffffffff>Scout</color>";
                    if (isAssassinAvail)
                    {
                        if (GUI.Button(new Rect(20, 175, 80, 30), fetchAI == AIType.Assault && !CantPerformActions ? new GUIContent(textAttack, "ACTIVE") : new GUIContent(textAttack, "Needs Charging Base"),
                            fetchAI == AIType.Assault ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            changeAI = AIType.Assault;
                            clicked = true;

                        }
                    }
                    else if (GUI.Button(new Rect(20, 175, 80, 30), new GUIContent(textAttack, "Need HE AI"), AIGlobals.ButtonGrey))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }

                    string textProtect = "<color=#ffffffff>Protect</color>";
                    if (isAegisAvail)
                    {
                        if (GUI.Button(new Rect(20, 205, 80, 30), CantPerformActions ? new GUIContent(textProtect, "No Allies Nearby") : fetchAI == AIType.Aegis ? new GUIContent(textProtect, "ACTIVE") : new GUIContent(textProtect, "Follow Closest Ally"),
                            fetchAI == AIType.Aegis ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            changeAI = AIType.Aegis;
                            clicked = true;
                        }
                    }
                    else if (GUI.Button(new Rect(20, 205, 80, 30), new GUIContent(textProtect, "Need GSO AI"), AIGlobals.ButtonGrey))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }

                    string textCharge = "<color=#ffffffff>Charger</color>";
                    if (isEnergizerAvail)
                    {
                        if (GUI.Button(new Rect(20, 235, 80, 30), fetchAI == AIType.Energizer && !CantPerformActions ? new GUIContent(textCharge, "ACTIVE") : new GUIContent(textCharge, "Need Charge Base & Charger"),
                            fetchAI == AIType.Energizer ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            changeAI = AIType.Energizer;
                            clicked = true;
                        }
                    }
                    else if (GUI.Button(new Rect(20, 235, 80, 30), new GUIContent(textCharge, "Need GC AI"), AIGlobals.ButtonGrey))
                    {
                    }

                    string textScrap = "<color=#ffffffff>Fetch</color>";
                    if (isScrapperAvail)
                    {
                        if (GUI.Button(new Rect(100, 235, 80, 30), fetchAI == AIType.Scrapper && !CantPerformActions ? new GUIContent(textScrap, "ACTIVE") : new GUIContent(textScrap, "Need Block Receiving Base"),
                            fetchAI == AIType.Scrapper ? CantPerformActions ? AIGlobals.ButtonRedActive : AIGlobals.ButtonBlueActive : AIGlobals.ButtonBlue))
                        {
                            changeAI = AIType.Scrapper;
                            clicked = true;
                        }
                    }
                    else if (GUI.Button(new Rect(100, 235, 80, 30), new GUIContent(textScrap, "Need GC AI"), AIGlobals.ButtonGrey))
                    {
                    }
                }
                if (lastTank.PlayerAllowAnchoring)
                {
                    if (lastTank.tank.Anchors.NumPossibleAnchors > 0)
                    {
                        if (lastTank.CanAnchorSafely)
                        {
                            if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Stop & Anchor", "Fixate to ground"), AIGlobals.ButtonGreen))
                            {
                                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimSolarGen);
                                if (ManNetwork.IsHost)
                                {
                                    lastTank.PlayerAllowAnchoring = false;
                                    lastTank.TryAnchor();
                                }
                                AIDriver = AIDriverType.Stationary;
                                clickedDriver = true;
                            }
                        }
                        else if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Enemy Jammed", "Enemy too close!"), AIGlobals.ButtonRed))
                        {
                        }
                    }
                    else if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("No Anchors", "Needs working anchors"), AIGlobals.ButtonGrey))
                    {
                    }
                }
                else if (GUI.Button(new Rect(20, 265, 160, 30), new GUIContent("Mobilize", "Detach from ground"), AIGlobals.ButtonGreen))
                {
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                    if (ManNetwork.IsHost)
                    {
                        lastTank.UnAnchor();
                        lastTank.PlayerAllowAnchoring = true;
                    }
                    AIDriver = AIDriverType.AutoSet;
                    clickedDriver = true;
                }
                

                GUI.Label(new Rect(20, 295, 160, 50), AIGlobals.UIAlphaText + GUI.tooltip + "</color>");
                if (clickedDriver)
                {
                    SetOptionDriver(AIDriver);
                }
                if (clicked)
                {
                    SetOption(changeAI);
                }
            }
            else
            {
                DebugTAC_AI.Log("TACtical_AI: SELECTED TANK IS NULL!");
                //lastTank = Singleton.Manager<ManPointer>.inst.targetVisible.transform.root.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>();
                CloseSubMenuClickable();
            }
            //GUI.DragWindow();
        }

        internal static FieldInfo bubble = typeof(Tank).GetField("m_Overlay", BindingFlags.NonPublic | BindingFlags.Instance);
        /// <summary>
        /// Only sets the first unit
        /// </summary>
        /// <param name="driver"></param>
        public static void SetOptionDriver(AIDriverType driver)
        {
            try
            {
                if (!lastTank)
                    return;
                if (!lastTank.tank)
                    return;
                if (ManNetwork.IsNetworked)
                {
                    try
                    {
                        NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, AIType.Null, driver);

                        lastTank.OnSwitchAI(false);
                        if (lastTank.DriverType != driver)
                        {
                            WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                            AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                        }
                        lastTank.DriverType = driver;
                        lastTank.ForceAllAIsToEscort();
                        lastTank.SetupMovementAIController();

                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
                    }
                }
                else
                {
                    lastTank.OnSwitchAI(false);
                    if (lastTank.DriverType != driver)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                        AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                    }
                    lastTank.DriverType = driver;
                    lastTank.ForceAllAIsToEscort();
                    lastTank.SetupMovementAIController();

                }
                inst.TrySetOptionDriverRTS(driver);
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                DebugTAC_AI.Log("TACtical_AI: Set " + lastTank.name + " to driver " + driver);
            }
            catch { }
        }
        private void TrySetOptionDriverRTS(AIDriverType driver)
        {
            if (!(bool)ManPlayerRTS.inst)
                return;
            if (ManPlayerRTS.PlayerIsInRTS || ManPlayerRTS.PlayerRTSOverlay)
            {
                int select = 0;
                int amount = ManPlayerRTS.inst.LocalPlayerTechsControlled.Count;
                for (int step = 0; amount > step; step++)
                {
                    AIECore.TankAIHelper tankInst = ManPlayerRTS.inst.LocalPlayerTechsControlled.ElementAt(step);
                    if ((bool)tankInst && tankInst != lastTank)
                    {
                        select++;
                        SetOptionDriverCase(tankInst, driver);
                    }
                }
                DebugTAC_AI.Log("TACtical_AI: TrySetOptionRTS - Set " + amount + " Techs to drive " + driver);
                if (select > 0)
                    Invoke("DelayedExtraNoise", 0.15f);
            }
        }
        private static void SetOptionDriverCase(AIECore.TankAIHelper tankInst, AIDriverType driver)
        {
            if (tankInst.IsNull())
                return;
            AIDriverType locDediAI = AIDriverType.Tank;
            switch (driver)
            {
                case AIDriverType.Astronaut:
                    if (tankInst.isAstrotechAvail)
                        locDediAI = driver;
                    break;
                case AIDriverType.Pilot:
                    if (tankInst.isAviatorAvail)
                        locDediAI = driver;
                    break;
                case AIDriverType.Sailor:
                    if (tankInst.isBuccaneerAvail)
                        locDediAI = driver;
                    break;
                case AIDriverType.Stationary:
                    if (tankInst.tank.Anchors.NumPossibleAnchors < 1 || !tankInst.CanAnchorSafely)
                        return;
                    break;
                case AIDriverType.AutoSet:
                    break;
                default:
                    DebugTAC_AI.LogError("TACtical_AI: Encountered illegal AIDriverType on AI Driver switch!");
                    break;
            }
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(tankInst.tank.netTech.netId.Value, AIType.Null, locDediAI);
                    tankInst.OnSwitchAI(false);
                    if (tankInst.DriverType != driver)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tankInst.tank.visible);
                        AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                    }
                    tankInst.ForceAllAIsToEscort();
                    tankInst.DriverType = locDediAI;
                    tankInst.SetupMovementAIController();

                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                tankInst.OnSwitchAI(false);
                if (tankInst.DriverType != driver)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tankInst.tank.visible);
                    AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                }
                tankInst.ForceAllAIsToEscort();
                tankInst.DriverType = locDediAI;
                tankInst.SetupMovementAIController();

            }
        }

        public static void SetOption(AIType dediAI)
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
                    lastTank.ForceAllAIsToEscort();
                    fetchAI = dediAI;
                    lastTank.SetupMovementAIController();

                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
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
                lastTank.ForceAllAIsToEscort();
                fetchAI = dediAI;
                lastTank.SetupMovementAIController();

            }
            inst.TrySetOptionRTS(dediAI);
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            //CloseSubMenuClickable();
        }
        private void TrySetOptionRTS(AIType dediAI)
        {
            if (!(bool)ManPlayerRTS.inst)
                return;
            if (ManPlayerRTS.PlayerIsInRTS || ManPlayerRTS.PlayerRTSOverlay)
            {
                int select = 0;
                int amount = ManPlayerRTS.inst.LocalPlayerTechsControlled.Count;
                for (int step = 0; amount > step; step++)
                {
                    AIECore.TankAIHelper tankInst = ManPlayerRTS.inst.LocalPlayerTechsControlled.ElementAt(step);
                    if ((bool)tankInst && tankInst != lastTank)
                    {
                        select++;
                        SetOptionCase(tankInst, dediAI);
                    }
                }
                DebugTAC_AI.Log("TACtical_AI: TrySetOptionRTS - Set " + amount + " Techs to mode " + dediAI);
                if (select > 0)
                    Invoke("DelayedExtraNoise", 0.15f);
            }
        }
        private static void SetOptionCase(AIECore.TankAIHelper tankInst, AIType dediAI)
        {
            if (tankInst.IsNull())
                return;
            AIType locDediAI;
            switch (dediAI)
            {
                case AIType.Aegis:
                    if (tankInst.isAegisAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Aviator:
                    if (tankInst.isAviatorAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Buccaneer:
                    if (tankInst.isBuccaneerAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Energizer:
                    if (tankInst.isEnergizerAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.Escort;
                    break;
                case AIType.Prospector:
                    if (tankInst.isProspectorAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.MTStatic;
                    break;
                case AIType.Scrapper:
                    if (tankInst.isScrapperAvail)
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
                    NetworkHandler.TryBroadcastNewAIState(tankInst.tank.netTech.netId.Value, locDediAI, AIDriverType.Null);
                    tankInst.OnSwitchAI(true);
                    tankInst.ForceAllAIsToEscort();
                    if (tankInst.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tankInst.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    tankInst.DediAI = locDediAI;
                    tankInst.SetupMovementAIController();

                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                tankInst.OnSwitchAI(false);
                tankInst.ForceAllAIsToEscort();
                if (tankInst.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tankInst.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                tankInst.DediAI = locDediAI;
                tankInst.SetupMovementAIController();

            }
        }
        public void DelayedExtraNoise()
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

        public static void ResetInfo()
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
        public static void GetInfo(AIECore.TankAIHelper AIEx)
        {
            if (AIEx.isAegisAvail)
                isAegisAvail = true;
            if (AIEx.isAssassinAvail)
                isAssassinAvail = true;

            // Collectors
            if (AIEx.isProspectorAvail)
                isProspectorAvail = true;
            if (AIEx.isScrapperAvail)
                isScrapperAvail = true;
            if (AIEx.isEnergizerAvail)
                isEnergizerAvail = true;

            // Pilots
            if (AIEx.isAviatorAvail)
                isAviatorAvail = true;
            if (AIEx.isBuccaneerAvail)
                isBuccaneerAvail = true;
            if (AIEx.isAstrotechAvail)
                isAstrotechAvail = true;
        }


        public static void LaunchSubMenuClickableRTS()
        {
            if (!KickStart.EnableBetterAI || !ManPlayerRTS.inst.IsNotNull())
            {
                return;
            }
            if (ManPlayerRTS.inst.Leading)
            {
                try
                {
                    if (ManPlayerRTS.PlayerIsInRTS || ManPlayerRTS.PlayerRTSOverlay)
                    {
                        if (ManPlayerRTS.inst.LocalPlayerTechsControlled.Count > 0)
                        {
                            Vector3 Mous = Input.mousePosition;
                            xMenu = Mous.x - (10 + HotWindow.width);
                            yMenu = Display.main.renderingHeight - Mous.y + 10;
                            lastTank = ManPlayerRTS.inst.Leading;
                        }
                        else
                        {
                            DebugTAC_AI.Log("TACtical_AI: No techs selected!");
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                            return;
                        }
                    }
                    else
                    {
                        DebugTAC_AI.Log("TACtical_AI: No techs selected!");
                        return;
                    }
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: TANK IS NULL!");
                    return;
                }
            }
            if (!lastTank)
                return;

            lastTank.RefreshAI();
            GetInfo(lastTank);
            DebugTAC_AI.Log("TACtical_AI: Opened AI menu!");
            AIDriver = lastTank.DriverType;
            fetchAI = lastTank.DediAI;
            MoveMenuToCursor(true);
            isCurrentlyOpen = true;
            windowTimer = 0.25f;
            GUIWindow.SetActive(true);
        }
        public static void LaunchSubMenuClickable(bool centerOnMouse = false)
        {
            if (!KickStart.EnableBetterAI)
            {
                return;
            }
            if (IsTankNull())
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: TANK IS NULL!");
                return;
            }
            lastTank.RefreshAI();
            GetInfo(lastTank);
            DebugTAC_AI.Log("TACtical_AI: Opened AI menu!");
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
        public static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                lastTank = null;
                ResetInfo();
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                DebugTAC_AI.Log("TACtical_AI: Closed AI menu!");
            }
        }

        public static void MoveMenuToCursor(bool centerOnMouse)
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
        public static bool MouseIsOverSubMenu()
        {
            if (!KickStart.EnableBetterAI)
            {
                return false;
            }
            if (isCurrentlyOpen)
            {
                Vector3 Mous = Input.mousePosition;
                Mous.y = Display.main.renderingHeight - Mous.y;
                float xMenuMin = HotWindow.x;
                float xMenuMax = HotWindow.x + HotWindow.width;
                float yMenuMin = HotWindow.y;
                float yMenuMax = HotWindow.y + HotWindow.height;
                //Debug.Log(Mous + " | " + xMenuMin + " | " + xMenuMax + " | " + yMenuMin + " | " + yMenuMax);
                if (Mous.x > xMenuMin && Mous.x < xMenuMax && Mous.y > yMenuMin && Mous.y < yMenuMax)
                {
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
            if (windowTimer > 0)
            {
                windowTimer -= Time.deltaTime;
            }
            if (windowTimer < 0 && !MouseIsOverSubMenu())
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
