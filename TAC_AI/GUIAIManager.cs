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
    /*
    public class AIManagerTechEntry
    {   // Abandoned UI control system for AI - not enough room on HUD 
        internal Tank tank;
        private Texture2D image;
        private int previousBlockCount;
        private int Health { get { return (int)(RHealth / 50); } }
        private long RHealth;
        private int FullHealth { get { return (int)(RHealth / 50); } }
        private long RFullHealth;

        public AIManagerTechEntry(Tank tech)
        {
            tank = tech;
            Singleton.Manager<ManScreenshot>.inst.RenderTechImage(tech, new IntVector2(1, 1), false, delegate (TechData TD, Texture2D Tex) 
            {
                if (Tex.IsNotNull())
                    image = Tex;
            });
            previousBlockCount = tech.blockman.blockCount;
            tech.AttachEvent.Subscribe(AddBlock);
            tech.DetachEvent.Subscribe(RemoveBlock);
            tech.TankRecycledEvent.Subscribe(OnRecycledTank);
        }

        public void AddBlock(TankBlock bloc, Tank tonk)
        {
            if (tonk == tank)
            {
                RFullHealth += bloc.damage.maxHealth;
            }
        }
        public void RemoveBlock(TankBlock bloc, Tank tonk)
        {
            if (tonk == tank)
            {
                RFullHealth -= bloc.damage.maxHealth;
            }
        }
        public void OnRecycledTank(Tank tonk)
        {
            if (tonk == tank)
            {
                tank.AttachEvent.Unsubscribe(AddBlock);
                tank.DetachEvent.Unsubscribe(RemoveBlock);
                tank.TankRecycledEvent.Unsubscribe(OnRecycledTank);
                tank = null;
            }
        }
    }*/
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
        private static Rect HotWindow = new Rect(0, 0, 200, 310);   // the "window"
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
            /*
            if (Singleton.Manager<ManPointer>.inst.targetTank.IsNotNull() && !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
            {
                var tonk = Singleton.Manager<ManPointer>.inst.targetTank;
                if (tonk.PlayerFocused)
                {
                    lastTank = null;
                    return;
                }
                if (tonk.IsFriendly())
                {
                    lastTank = Singleton.Manager<ManPointer>.inst.targetTank.trans.GetComponent<AI.AIECore.TankAIHelper>();
                    lastTank.RefreshAI();
                    Vector3 Mous = Input.mousePosition;
                    xMenu = Mous.x - 100 - 125;
                    yMenu = Display.main.renderingHeight - Mous.y - 100 + 125;
                }
            }
            else
            {
                Debug.Log("TACtical_AI: SELECTED TANK IS NULL!");
            }
            */
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
                    HotWindow = GUI.Window(AIManagerID, HotWindow, GUIHandler, "<b>AI Mode Select</b>");
                }
            }
        }

        private static void GUIHandler(int ID)
        {
            bool clicked = false;
            bool clickedDriver = false;
            changeAI = fetchAI;
            if (lastTank != null)
            {
                switch (fetchAI)
                {
                    case AIType.Aegis:
                        if ((bool)lastTank.lastEnemy)
                            GUI.tooltip = "In Combat";
                        else if ((bool)lastTank.LastCloseAlly)
                            GUI.tooltip = "Following Ally";
                        else
                            GUI.tooltip = "Protecting Allied";
                        break;
                    case AIType.Assault:
                        GUI.tooltip = "Scouting for Enemies";
                        break;
                    case AIType.Energizer:
                        if ((bool)lastTank.foundGoal)
                            GUI.tooltip = "Charging Ally";
                        else if ((bool)lastTank.foundBase)
                            GUI.tooltip = "Recharging...";
                        else
                            GUI.tooltip = "Task Complete";
                        break;
                    case AIType.Escort:
                        switch (AIDriver)
                        {
                            case AIDriverType.Astronaut:
                                if ((bool)lastTank.lastEnemy)
                                    GUI.tooltip = "In Combat";
                                else
                                    GUI.tooltip = "Floating Escort";
                                break;
                            case AIDriverType.Pilot:
                                if ((bool)lastTank.lastEnemy)
                                    GUI.tooltip = "In Combat";
                                else
                                    GUI.tooltip = "Flying Escort";
                                break;
                            case AIDriverType.Sailor:
                                if ((bool)lastTank.lastEnemy)
                                    GUI.tooltip = "In Combat";
                                else
                                    GUI.tooltip = "Sailing Escort";
                                break;
                            default:
                                if ((bool)lastTank.lastEnemy)
                                    GUI.tooltip = "In Combat";
                                else
                                    GUI.tooltip = "Land Escort";
                                break;
                        }
                        break;
                    case AIType.MTMimic:
                        if (lastTank.OnlyPlayerMT)
                        {
                            if ((bool)lastTank.LastCloseAlly)
                                GUI.tooltip = "Copying Player";
                            else
                                GUI.tooltip = "Searching for Player";
                        }
                        else
                        {
                            if((bool)lastTank.LastCloseAlly)
                                GUI.tooltip = "Copying Close Ally";
                            else
                                GUI.tooltip = "Searching for Ally";
                        }
                        break;
                    case AIType.MTSlave:
                        if ((bool)lastTank.DANGER)
                            GUI.tooltip = "Weapons Active";
                        else
                            GUI.tooltip = "Weapons Primed";
                        break;
                    case AIType.MTTurret:
                        if ((bool)lastTank.DANGER)
                            GUI.tooltip = "Shooting at Enemy";
                        else if ((bool)lastTank.lastEnemy)
                            GUI.tooltip = "Aiming at Enemy";
                        else
                            GUI.tooltip = "Face the Danger";
                        break;
                    case AIType.Prospector:
                        if ((bool)lastTank.foundGoal)
                            GUI.tooltip = "Mining Resources";
                        else if ((bool)lastTank.foundBase)
                            GUI.tooltip = "Returning Resources";
                        else
                            GUI.tooltip = "No Resources Detected!";
                        break;
                    case AIType.Scrapper:
                        GUI.tooltip = "Scavenging Blocks";
                        break;
                }
                
                // Drivers
                if (GUI.Button(new Rect(20, 30, 80, 30), AIDriver == AIDriverType.Tank ? new GUIContent("<color=#f23d3dff>TANK</color>", "ACTIVE") : new GUIContent("Tank", "Avoids Water")))
                {
                    AIDriver = AIDriverType.Tank;
                    clickedDriver = true;
                }
                if (GUI.Button(new Rect(100, 30, 80, 30), isAviatorAvail ? AIDriver == AIDriverType.Pilot ? new GUIContent("<color=#f23d3dff>PILOT</color>", "ACTIVE") : new GUIContent("Pilot", "Fly Plane or Heli") : new GUIContent("<color=#808080ff>pilot</color>", "Need HE or VEN AI")))
                {
                    if (isAviatorAvail)
                    {
                        AIDriver = AIDriverType.Pilot;
                        clickedDriver = true;
                    }
                }
                if (GUI.Button(new Rect(20, 60, 80, 30), isBuccaneerAvail && KickStart.isWaterModPresent ? AIDriver == AIDriverType.Sailor ? new GUIContent("<color=#f23d3dff>SHIP</color>", "ACTIVE") : new GUIContent("Ship", "Stay in water") : new GUIContent("<color=#808080ff>ship</color>", "Need GSO or VEN AI")))
                {
                    if (isBuccaneerAvail && KickStart.isWaterModPresent)
                    {
                        AIDriver = AIDriverType.Sailor;
                        clickedDriver = true;
                    }
                }
                if (GUI.Button(new Rect(100, 60, 80, 30), isAstrotechAvail ? AIDriver == AIDriverType.Astronaut ? new GUIContent("<color=#f23d3dff>SPACE</color>", "ACTIVE") : new GUIContent("Space", "Fly above") : new GUIContent("<color=#808080ff>space</color>", "Need BF AI")))
                {
                    if (isAstrotechAvail)
                    {
                        AIDriver = AIDriverType.Astronaut;
                        clickedDriver = true;
                    }
                }

                GUI.Label(new Rect(20, 95, 160, 20), !lastTank.name.NullOrEmpty() ? lastTank.name : "NO NAME");

                // Tasks
                // top - Escort
                if (GUI.Button(new Rect(20, 110, 80, 30), fetchAI == AIType.Escort ? new GUIContent("<color=#f23d3dff>ESCORT</color>", "ACTIVE") : new GUIContent("Escort", "Follows player")))
                {
                    changeAI = AIType.Escort;
                    clicked = true;
                }
                if (GUI.Button(new Rect(100, 110, 80, 30), lastTank.RTSControlled ? new GUIContent("<color=#f23d3dff>ORDER</color>", "ACTIVE") : new GUIContent("Order", "Go to last target")))
                {
                    bool toTog = !lastTank.RTSControlled;
                    lastTank.SetRTSState(toTog);
                    int select = 0;
                    int amount = PlayerRTSControl.inst.LocalPlayerTechsControlled.Count;
                    for (int step = 0; amount > step; step++)
                    {
                        AIECore.TankAIHelper tankInst = PlayerRTSControl.inst.LocalPlayerTechsControlled.ElementAt(step);
                        if ((bool)tankInst && tankInst != lastTank)
                        {
                            select++;
                            tankInst.SetRTSState(toTog);
                        }
                    }
                    if (select > 0)
                    {
                        Debug.Log("TACtical_AI: GUIAIManager - Set " + select + " Techs to RTSMode " + toTog);
                        inst.Invoke("DelayedExtraNoise", 0.15f);
                    }
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                    if (!lastTank)
                        return;
                }
                // upper right - MT
                if (GUI.Button(new Rect(100, 140, 80, 30), fetchAI == AIType.MTSlave ? new GUIContent("<color=#f23d3dff>STATIC</color>", "ACTIVE") : new GUIContent("Static", "Weapons only")))
                {
                    changeAI = AIType.MTSlave;
                    clicked = true;
                }
                if (GUI.Button(new Rect(100, 170, 80, 30), fetchAI == AIType.MTTurret ? new GUIContent("<color=#f23d3dff>TURRET</color>", "ACTIVE") : new GUIContent("Turret", "Aim, then fire")))
                {
                    changeAI = AIType.MTTurret;
                    clicked = true;
                }
                if (GUI.Button(new Rect(100, 200, 80, 30), fetchAI == AIType.MTMimic ? new GUIContent("<color=#f23d3dff>MIMIC</color>", "ACTIVE") : new GUIContent("Mimic", "Copy closest Tech")))
                {
                    changeAI = AIType.MTMimic;
                    clicked = true;
                }
                // upper left, bottom - Aux modes
                if (GUI.Button(new Rect(20, 140, 80, 30), isProspectorAvail ? fetchAI == AIType.Prospector ? new GUIContent("<color=#f23d3dff>MINER</color>", "ACTIVE") : new GUIContent("Miner", "Needs Receiver Base") : new GUIContent("<color=#808080ff>miner</color>", "Need GSO or GC AI")))
                {
                    if (isProspectorAvail)
                    {
                        changeAI = AIType.Prospector;
                        clicked = true;
                    }
                }
                if (GUI.Button(new Rect(20, 170, 80, 30), isAssassinAvail ? fetchAI == AIType.Assault ? new GUIContent("<color=#f23d3dff>SCOUT</color>", "ACTIVE") : new GUIContent("Scout", "Needs Charging Base") : new GUIContent("<color=#808080ff>scout</color>", "Need HE AI")))
                {
                    if (isAssassinAvail)
                    {
                        changeAI = AIType.Assault;
                        clicked = true;
                    }
                }
                if (GUI.Button(new Rect(20, 200, 80, 30), isAegisAvail ? fetchAI == AIType.Aegis ? new GUIContent("<color=#f23d3dff>PROTECT</color>", "ACTIVE") : new GUIContent("Protect","Follow Closest Ally") : new GUIContent("<color=#808080ff>protect</color>", "Need GSO AI")))
                {
                    if (isAegisAvail)
                    {
                        changeAI = AIType.Aegis;
                        clicked = true;
                    }
                }
                if (GUI.Button(new Rect(20, 230, 80, 30), isEnergizerAvail ? fetchAI == AIType.Energizer ? new GUIContent("<color=#f23d3dff>CHARGER</color>", "ACTIVE") : new GUIContent("Charger", "Need Charge Base & Charger") : new GUIContent("<color=#808080ff>charger</color>", "Need GC AI")))
                {
                    if (isEnergizerAvail)
                    {
                        changeAI = AIType.Energizer;
                        clicked = true;
                    }
                }
                if (GUI.Button(new Rect(100, 230, 80, 30), isScrapperAvail ? fetchAI == AIType.Scrapper ? new GUIContent("<color=#f23d3dff>FETCH</color>", "ACTIVE") : new GUIContent("Fetch", "Need Block Receiving Base") : new GUIContent("<color=#808080ff>fetch</color>", "Need GC AI")))
                {
                    if (isScrapperAvail)
                    {
                        changeAI = AIType.Scrapper;
                        clicked = true;
                    }
                }

                GUI.Label(new Rect(20, 270, 160, 40), GUI.tooltip);
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
                Debug.Log("TACtical_AI: SELECTED TANK IS NULL!");
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
                        NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, (AIType)(-1), driver);

                        lastTank.OnSwitchAI(false);
                        if (lastTank.DriverType != driver)
                        {
                            WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                            AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                        }
                        lastTank.DriverType = driver;
                        lastTank.ForceAllAIsToEscort();
                        lastTank.ForceRebuildAlignment();
                        lastTank.TestForFlyingAIRequirement();

                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
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
                    lastTank.ForceRebuildAlignment();
                    lastTank.TestForFlyingAIRequirement();

                }
                windowTimer = 4;
                inst.TrySetOptionDriverRTS(driver);
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
                Debug.Log("TACtical_AI: Set " + lastTank.name + " to driver " + driver);
            }
            catch { }
        }
        private void TrySetOptionDriverRTS(AIDriverType driver)
        {
            if (!(bool)PlayerRTSControl.inst)
                return;
            if (PlayerRTSControl.PlayerIsInRTS || PlayerRTSControl.PlayerRTSOverlay)
            {
                PlayerRTSControl.inst.PurgeAllNull();
                int select = 0;
                int amount = PlayerRTSControl.inst.LocalPlayerTechsControlled.Count;
                for (int step = 0; amount > step; step++)
                {
                    AIECore.TankAIHelper tankInst = PlayerRTSControl.inst.LocalPlayerTechsControlled.ElementAt(step);
                    if ((bool)tankInst && tankInst != lastTank)
                    {
                        select++;
                        SetOptionDriverCase(tankInst, driver);
                    }
                }
                Debug.Log("TACtical_AI: TrySetOptionRTS - Set " + amount + " Techs to drive " + driver);
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
                case AIDriverType.Unset:
                    break;
                default:
                    Debug.LogError("TACtical_AI: Encountered illegal AIDriverType on AI Driver switch!");
                    break;
            }
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(tankInst.tank.netTech.netId.Value, (AIType)(-1), locDediAI);
                    tankInst.OnSwitchAI(false);
                    if (tankInst.DriverType != driver)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tankInst.tank.visible);
                        AIGlobals.PopupPlayerInfo(driver.ToString(), worPos);
                    }
                    tankInst.ForceAllAIsToEscort();
                    tankInst.DriverType = locDediAI;
                    lastTank.ForceRebuildAlignment();
                    tankInst.TestForFlyingAIRequirement();

                }
                catch (Exception e)
                {
                    Debug.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
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
                lastTank.ForceRebuildAlignment();
                tankInst.TestForFlyingAIRequirement();

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
                    NetworkHandler.TryBroadcastNewAIState(lastTank.tank.netTech.netId.Value, dediAI, AIDriverType.Unset);

                    lastTank.OnSwitchAI(!Input.GetKey(KickStart.MultiSelect));
                    if (lastTank.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    lastTank.DediAI = dediAI;
                    lastTank.ForceAllAIsToEscort();
                    fetchAI = dediAI;
                    lastTank.ForceRebuildAlignment();
                    lastTank.TestForFlyingAIRequirement();

                }
                catch (Exception e)
                {
                    Debug.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                lastTank.OnSwitchAI(!Input.GetKey(KickStart.MultiSelect));
                if (lastTank.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(lastTank.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                lastTank.DediAI = dediAI;
                lastTank.ForceAllAIsToEscort();
                fetchAI = dediAI;
                lastTank.ForceRebuildAlignment();
                lastTank.TestForFlyingAIRequirement();

            }
            inst.TrySetOptionRTS(dediAI);
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            //CloseSubMenuClickable();
        }
        private void TrySetOptionRTS(AIType dediAI)
        {
            if (!(bool)PlayerRTSControl.inst)
                return;
            if (PlayerRTSControl.PlayerIsInRTS || PlayerRTSControl.PlayerRTSOverlay)
            {
                PlayerRTSControl.inst.PurgeAllNull();
                int select = 0;
                int amount = PlayerRTSControl.inst.LocalPlayerTechsControlled.Count;
                for (int step = 0; amount > step; step++)
                {
                    AIECore.TankAIHelper tankInst = PlayerRTSControl.inst.LocalPlayerTechsControlled.ElementAt(step);
                    if ((bool)tankInst && tankInst != lastTank)
                    {
                        select++;
                        SetOptionCase(tankInst, dediAI);
                    }
                }
                Debug.Log("TACtical_AI: TrySetOptionRTS - Set " + amount + " Techs to mode " + dediAI);
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
                        locDediAI = AIType.MTSlave;
                    break;
                case AIType.Scrapper:
                    if (tankInst.isScrapperAvail)
                        locDediAI = dediAI;
                    else
                        locDediAI = AIType.MTSlave;
                    break;
                default:
                    locDediAI = dediAI;
                    break;
            }
            if (ManNetwork.IsNetworked)
            {
                try
                {
                    NetworkHandler.TryBroadcastNewAIState(tankInst.tank.netTech.netId.Value, locDediAI, AIDriverType.Unset);
                    tankInst.OnSwitchAI(!Input.GetKey(KickStart.MultiSelect));
                    tankInst.ForceAllAIsToEscort();
                    if (tankInst.DediAI != dediAI)
                    {
                        WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tankInst.tank.visible);
                        AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                    }
                    tankInst.DediAI = locDediAI;
                    lastTank.ForceRebuildAlignment();
                    tankInst.TestForFlyingAIRequirement();

                }
                catch (Exception e)
                {
                    Debug.Log("TACtical_AI: Error on sending AI Option change!!!\n" + e);
                }
            }
            else
            {
                tankInst.OnSwitchAI(!Input.GetKey(KickStart.MultiSelect));
                tankInst.ForceAllAIsToEscort();
                if (tankInst.DediAI != dediAI)
                {
                    WorldPosition worPos = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(tankInst.tank.visible);
                    AIGlobals.PopupPlayerInfo(dediAI.ToString(), worPos);
                }
                tankInst.DediAI = locDediAI;
                lastTank.ForceRebuildAlignment();
                tankInst.TestForFlyingAIRequirement();

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


        public static void LaunchSubMenuClickable()
        {
            if (!KickStart.EnableBetterAI)
            {
                return;
            }
            if (lastTank.IsNull())
            {
                try
                {
                    if (PlayerRTSControl.inst.IsNotNull() && (PlayerRTSControl.PlayerIsInRTS || PlayerRTSControl.PlayerRTSOverlay))
                    {
                        if (PlayerRTSControl.inst.LocalPlayerTechsControlled.Count > 0)
                        {
                            Vector3 Mous = Input.mousePosition;
                            xMenu = Mous.x - (10 + HotWindow.width);
                            yMenu = Display.main.renderingHeight - Mous.y + 10;
                            lastTank = PlayerRTSControl.inst.LocalPlayerTechsControlled.ElementAt(0);
                        }
                        else
                        {
                            Debug.Log("TACtical_AI: No techs selected!");
                            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                            return;
                        }
                    }
                    else
                    {
                        Debug.Log("TACtical_AI: No techs selected!");
                        return;
                    }
                }
                catch
                {
                    Debug.Log("TACtical_AI: TANK IS NULL!");
                    return;
                }
            }
            lastTank.RefreshAI();
            GetInfo(lastTank);
            Debug.Log("TACtical_AI: Opened AI menu!");
            AIDriver = lastTank.DriverType;
            fetchAI = lastTank.DediAI;
            isCurrentlyOpen = true;
            xMenu = Mathf.Clamp(xMenu, 0, Display.main.renderingWidth - HotWindow.width);
            yMenu = Mathf.Clamp(yMenu, 0, Display.main.renderingHeight - HotWindow.height);
            HotWindow.x = xMenu;
            HotWindow.y = yMenu;
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
                KickStart.ReleaseControl(AIManagerID);
                Debug.Log("TACtical_AI: Closed AI menu!");
            }
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
                        LaunchSubMenuClickable();
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
