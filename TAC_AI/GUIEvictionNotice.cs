using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;

namespace TAC_AI.AI
{
    public class GUIEvictionNotice : MonoBehaviour
    {
        //Handles the display that's triggered on right-click on friendly or neutral base team
        private static GUIEvictionNotice inst;
        public static Vector3 PlayerLoc = Vector3.zero;
        public static bool isCurrentlyOpen = false;
        internal static Tank lastTank;

        // Mode - Setting
        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 160, 140);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;

        // Tech Tracker
        private static string randomEvict = "Scram!";
        private static string teamName;
        private static readonly string[] randomEvictSayings = new string[10]
            {
                "Scram!",
                "Beat it!",
                "Go away",
                "This is my turf",
                "Tresspasser!",
                "Leave now",
                "Scurry on",
                "No room here",
                "Goodbye!",
                "You cannot stay",
            };


        private static float windowTimer = 0;
        private const int EvictionID = 8008;
        private static RBases.EnemyBaseFunder funder;


        public static void Initiate()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject()).AddComponent<GUIEvictionNotice>();
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(Click);
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
            Singleton.Manager<ManPointer>.inst.MouseEvent.Unsubscribe(Click);
            GUIWindow.SetActive(false);
            inst.enabled = false;
            Destroy(inst.gameObject);
            inst = null;
        }

        public static void Click(ManPointer.Event button, bool down, bool yes)
        {
            if (button == ManPointer.Event.RMB && down && KickStart.IsIngame && Input.GetKey(KeyCode.T))
            {
                if (ManPointer.inst.targetTank && AIGlobals.IsFriendlyBaseTeam(ManPointer.inst.targetTank.Team))
                {
                    GetTank(ManPointer.inst.targetTank);
                }
            }
        }

        public static void GetTank(Tank tank)
        {
            lastTank = tank;
            Vector3 Mous = Input.mousePosition;
            xMenu = Mous.x - (HotWindow.width / 2);
            yMenu = Display.main.renderingHeight - Mous.y - 10;
            List<string> select = randomEvictSayings.ToList();
            select.Remove(randomEvict);
            funder = RBases.GetTeamFunder(tank.Team);
            randomEvict =  "<b>" + select.GetRandomEntry() + "</b>";
            teamName = TeamNamer.GetTeamName(lastTank.Team).ToString();
            LaunchSubMenuClickable();
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
                    HotWindow = GUI.Window(EvictionID, HotWindow, GUIHandler, "<b>You Say:</b>");
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
            int teamFunds = 0;
            funder = RBases.GetTeamFunder(lastTank.Team);
            if (funder != null)
            {
                teamFunds = funder.BuildBucks;
            }
            else
            {
                var teamUnloaded = ManEnemyWorld.GetTeam(lastTank.Team);
                if (teamUnloaded != null)
                {
                    if (UnloadedBases.GetTeamFunder(teamUnloaded) != null)
                    {
                        teamFunds = UnloadedBases.GetTeamFunder(teamUnloaded).BuildBucks;
                    }
                }
            }
            GUI.Label(new Rect(10, 30, 140, 30), "<b>" + teamName + "</b>");
            GUI.Label(new Rect(10, 50, 140, 30), (teamFunds > 0 ? "<b>Team ¥¥: </b>" + teamFunds : "<b>No Bases?</b>"));
            if (GUI.Button(new Rect(10, 70, 140, 30), "<b>Coming Soon</b>"))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Open);
            }
            if (GUI.Button(new Rect(10, 100, 140, 30), randomEvict))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LoadTech);
                ManEnemyWorld.ChangeTeam(lastTank.Team, AIGlobals.GetRandomEnemyBaseTeam());
                CloseSubMenuClickable();
            }
            GUI.DragWindow();
        }


        public static void LaunchSubMenuClickable()
        {
            if (!KickStart.EnableBetterAI)
            {
                return;
            }
            Debug.Log("TACtical_AI: Opened Eviction menu!");
            isCurrentlyOpen = true;
            xMenu = Mathf.Clamp(xMenu, 0, Display.main.renderingWidth - HotWindow.width);
            yMenu = Mathf.Clamp(yMenu, 0, Display.main.renderingHeight - HotWindow.height);
            HotWindow.x = xMenu;
            HotWindow.y = yMenu;
            windowTimer = 1.25f;
            GUIWindow.SetActive(true);
        }
        public static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                lastTank = null;
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl(EvictionID);
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
        }
    }
}
