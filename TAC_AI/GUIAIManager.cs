﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;

namespace TAC_AI
{
    public class GUIAIManager : MonoBehaviour
    {
        //Handles the display that's triggered on AI change 
        //  Circle hud wheel when the player assigns a new AI state
        //  TODO - add the hook needed to get the UI to pop up on Guard selection
        public static Vector3 PlayerLoc = Vector3.zero;
        public static bool isCurrentlyOpen = false;
        private static AIType fetchAI = AIType.Escort;
        private static AIType changeAI = AIType.Escort;
        private static AI.AIECore.TankAIHelper lastTank;

        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;


        private static int windowTimer = 0;


        public static void Initiate()
        {
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Subscribe(OnPlayerSwap);

            Instantiate(new GameObject()).AddComponent<GUIAIManager>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplay>();
            GUIWindow.SetActive(false);
            Vector3 Mous = Input.mousePosition;
            xMenu = 0;
            yMenu = 0;
        }

        public static void OnPlayerSwap(Tank tonk)
        {
            CloseSubMenuClickable();
        }
        public static void GetTank(Tank tank)
        {
            lastTank = tank.trans.GetComponent<AI.AIECore.TankAIHelper>();
            Vector3 Mous = Input.mousePosition;
            xMenu = Mous.x - 225;
            yMenu = Display.main.renderingHeight - Mous.y + 25;
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
                if (isCurrentlyOpen)
                {
                    HotWindow = GUI.Window(8001, HotWindow, GUIHandler, "<b>AI Mode Select</b>");
                }
            }
        }

        private static void GUIHandler(int ID)
        {
            bool clicked = false;
            changeAI = fetchAI;
            if (lastTank != null)
            {
                if (GUI.Button(new Rect(20, 40, 80, 30), fetchAI == AIType.Escort ? "<color=#f23d3dff>ESCORT</color>" : "Escort"))
                {
                    changeAI = AIType.Escort;
                    clicked = true;
                }
                if (GUI.Button(new Rect(100, 40, 80, 30), fetchAI == AIType.MTSlave ? "<color=#f23d3dff>SLAVE</color>" : "Slave"))
                {
                    changeAI = AIType.MTSlave;
                    clicked = true;
                }
                if (lastTank.isAssassinAvail)
                {
                    if (GUI.Button(new Rect(20, 70, 80, 30), fetchAI == AIType.Assault ? "<color=#f23d3dff>KILL</color>" : "Kill"))
                    {
                        changeAI = AIType.Assault;
                        clicked = true;
                    }
                }
                if (GUI.Button(new Rect(100, 70, 80, 30), fetchAI == AIType.MTTurret? "<color=#f23d3dff>TURRET</color>" : "Turret"))
                {
                    changeAI = AIType.MTTurret;
                    clicked = true;
                }
                if (lastTank.isAegisAvail)
                {
                    if (GUI.Button(new Rect(20, 100, 80, 30), fetchAI == AIType.Aegis ? "<color=#f23d3dff>COLLECT</color>" : "Aegis"))
                    {
                        changeAI = AIType.Aegis;
                        clicked = true;
                    }
                }
                if (GUI.Button(new Rect(100, 100, 80, 30), fetchAI == AIType.MTMimic ? "<color=#f23d3dff>MIMIC</color>" : "Mimic"))
                {
                    changeAI = AIType.MTMimic;
                    clicked = true;
                }
                if (lastTank.isProspectorAvail)
                {
                    if (GUI.Button(new Rect(20, 130, 80, 30), fetchAI == AIType.Prospector ? "<color=#f23d3dff>MINER</color>" : "Miner"))
                    {
                        changeAI = AIType.Prospector;
                        clicked = true;
                    }
                }
                if (lastTank.isAviatorAvail)
                {
                    if (GUI.Button(new Rect(100, 130, 80, 30), fetchAI == AIType.Aviator ? "<color=#f23d3dff>PILOT</color>" : "Pilot"))
                    {
                        changeAI = AIType.Aviator;
                        clicked = true;
                    }
                }
                /*
                // N/A!
                if (lastTank.isScrapperAvail)
                {
                    if (GUI.Button(new Rect(20, 160, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.Scrapper ? "<color=#f23d3dff>FETCH</color>" : "Fetch"))
                    {
                        changeAI = AI.AIEnhancedCore.DediAIType.Scrapper;
                        clicked = true;
                    }
                }
                */
                if (lastTank.isBuccaneerAvail && KickStart.isWaterModPresent)
                {
                    if (GUI.Button(new Rect(100, 160, 80, 30), fetchAI == AIType.Buccaneer ? "<color=#f23d3dff>SHIP</color>" : "Ship"))
                    {
                        changeAI = AIType.Buccaneer;
                        clicked = true;
                    }
                }
                /*
                // N/A!
                if (lastTank.isEnergizerAvail)
                {
                    if (GUI.Button(new Rect(20, 190, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.Energizer ? "<color=#f23d3dff>POWER</color>" : "Power"))
                    {
                        changeAI = AI.AIEnhancedCore.DediAIType.Energizer;
                        clicked = true;
                    }
                }
                */
                if (lastTank.isAstrotechAvail)
                {
                    if (GUI.Button(new Rect(100, 190, 80, 30), fetchAI == AIType.Astrotech ? "<color=#f23d3dff>SPACE</color>" : "Space"))
                    {
                        changeAI = AIType.Astrotech;
                        clicked = true;
                    }
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

            }
            //GUI.DragWindow();
        }

        public static void SetOption(AIType dediAI)
        {
            lastTank.DediAI = dediAI;
            fetchAI = dediAI;
            lastTank.TestForFlyingAIRequirement();
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            CloseSubMenuClickable();
        }


        public static void LaunchSubMenuClickable()
        {
            if (lastTank.IsNull() || !KickStart.EnableBetterAI)
            {
                Debug.Log("TACtical_AI: TANK IS NULL!");
                return;
            }
            lastTank.RefreshAI();
            Debug.Log("TACtical_AI: Launched AI menu!");
            fetchAI = lastTank.DediAI;
            isCurrentlyOpen = true;
            HotWindow = new Rect(xMenu, yMenu, 200, 230);
            windowTimer = 120;
            GUIWindow.SetActive(true);
        }
        public static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                lastTank = null;
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                Debug.Log("TACtical_AI: Closed AI menu!");
            }
        }


        private void Update()
        {
            if (windowTimer > 0)
            {
                windowTimer--;
            }
            if (windowTimer == 0)
            {
                CloseSubMenuClickable();
                windowTimer = -1;
            }
        }
    }
}
