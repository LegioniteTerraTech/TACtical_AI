using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using TerraTechETCUtil;

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
        private static Rect HotWindow = new Rect(0, 0, 200, 160);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;

        // Tech Tracker
        private static string teamName = "Unknown";
        private static int techCost = 0;
        private static int teamCost = 0;
        private static string randomAllow = "Let's make a deal";
        private static readonly string[] randomAllowSayings = new string[8]
            {
                "Friends?",
                "Build and prosper",
                "There's room here",
                "Mine together",
                "Peace",
                "I need help",
                "Let's make a deal",
                "Mine pact",
            };

        private static string randomEvict = "Scram!";
        private static readonly string[] randomEvictSayings = new string[10]
            {
                "Scram!",
                "Beat it!",
                "Go away",
                "This is my land",
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
                if (ManPointer.inst.targetTank && AIGlobals.IsBaseTeam(ManPointer.inst.targetTank.Team))
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

            techCost = Mathf.RoundToInt(RawTechExporter.GetBBCost(lastTank) * AIGlobals.BribePenalty);
            funder = RBases.GetTeamFunder(tank.Team);
            if (funder)
            {
                var teamUnloaded = ManEnemyWorld.GetTeam(lastTank.Team);
                if (teamUnloaded != null)
                {
                    teamCost = teamUnloaded.GlobalTeamCost();
                }
            }

            List<string> selectA = randomAllowSayings.ToList();
            selectA.Remove(randomAllow);
            randomAllow = "<b>" + selectA.GetRandomEntry() + "</b>";

            List<string> selectE = randomEvictSayings.ToList();
            selectE.Remove(randomEvict);
            randomEvict = "<b>" + selectE.GetRandomEntry() + "</b>";

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
                if (isCurrentlyOpen && KickStart.CanUseMenu && ManNetwork.IsHost)
                {
                    AltUI.StartUI();
                    HotWindow = GUI.Window(EvictionID, HotWindow, GUIHandler, "You Say:", AltUI.MenuLeft);
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
            if (lastTank.GetComponent<RBases.EnemyBaseFunder>())
            {
                GUIBaseTeam();
            }
            else
            {
                GUILoneTech();
            }
            GUI.DragWindow();
        }

        private static void GUIBaseTeam()
        { // Bases that store BB
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
                    EnemyBaseUnit EBU = UnloadedBases.GetTeamFunder(teamUnloaded);
                    if (EBU != null)
                    {
                        teamFunds = EBU.BuildBucks;
                        teamCost = teamUnloaded.GlobalTeamCost();
                    }
                }
            }
            GUI.Label(new Rect(10, 25, 180, 30), "<b>" + teamName + "</b>");//¥¥
            GUI.Label(new Rect(10, 45, 180, 30), (teamFunds > 0 ? "<b>" + (teamFunds + teamCost) + "</b> Bribe: " : "<b>No Bases?</b>"));
            GUIContent bribeButton;
            bool afford = ManPlayer.inst.CanAfford(teamFunds + teamCost);
            int lastTeam = lastTank.Team;
            if (ManPlayer.inst.PlayerTeam == lastTeam)
            {
                bribeButton = new GUIContent(randomAllow, "Fully Allied!");
                if (GUI.Button(new Rect(10, 90, 180, 30), bribeButton, AltUI.ButtonRed))
                {
                }
            }
            else
            {
                if (afford)
                {
                    string nextAllyState;
                    if (AIGlobals.IsFriendlyBaseTeam(lastTeam))
                    {
                        nextAllyState = "Your Team";
                    }
                    else if (AIGlobals.IsNeutralBaseTeam(lastTeam))
                    {
                        nextAllyState = "Friendly";
                    }
                    else if (AIGlobals.IsEnemyBaseTeam(lastTeam))
                    {
                        nextAllyState = "Neutral";
                    }
                    else
                        nextAllyState = "Neutral";
                    bribeButton = new GUIContent(randomAllow, "Bribe to " + nextAllyState);
                }
                else
                {
                    bribeButton = new GUIContent(randomAllow, "Not enough BB");
                }
                if (GUI.Button(new Rect(10, 90, 180, 30), bribeButton, afford ? AltUI.ButtonGreen : AltUI.ButtonGrey))
                {
                    if (afford)
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                        ManPlayer.inst.PayMoney(teamFunds);
                        funder.AddBuildBucks(teamFunds);
                        int newTeam;
                        if (AIGlobals.IsFriendlyBaseTeam(lastTeam))
                        {
                            newTeam = ManPlayer.inst.PlayerTeam;
                        }
                        else if (AIGlobals.IsNeutralBaseTeam(lastTeam))
                        {
                            newTeam = AIGlobals.GetRandomAllyBaseTeam();
                        }
                        else
                        {
                            newTeam = AIGlobals.GetRandomNeutralBaseTeam();
                        }
                        ManEnemyWorld.ChangeTeam(lastTeam, newTeam);
                        ManEnemyWorld.TeamBribeEvent.Send(lastTeam, newTeam);
                        CloseSubMenuClickable();
                    }
                    else
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            if (Singleton.playerTank)
            {
                if (GUI.Button(new Rect(10, 120, 180, 30), new GUIContent(randomEvict, "Fight the Team"), AltUI.ButtonRed))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LoadTech);
                    if (!AIGlobals.IsEnemyBaseTeam(lastTank.Team))
                    {
                        int newTeam = AIGlobals.GetRandomEnemyBaseTeam();
                        ManEnemyWorld.ChangeTeam(lastTeam, newTeam);
                        ManEnemyWorld.TeamWarEvent.Send(lastTeam, newTeam);
                        CloseSubMenuClickable();
                    }
                    RBases.RequestFocusFireNPTs(lastTank, Singleton.playerTank.visible, RequestSeverity.ThinkMcFly);
                }
            }
            else if (GUI.Button(new Rect(10, 120, 180, 30), new GUIContent(randomEvict, "You have no tech!"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            GUI.Label(new Rect(10, 65, 180, 30), GUI.tooltip);
        }
        private static void GUILoneTech()
        {
            GUI.Label(new Rect(10, 25, 180, 30), "<b>" + teamName + "</b>");//¥¥
            GUI.Label(new Rect(10, 45, 180, 30), (techCost > 0 ? "<b>Bribe: " + techCost + "</b>" : "<b>Free tech?</b>"));
            GUIContent bribeButton; ;
            bool afford = ManPlayer.inst.CanAfford(techCost);
            if (afford)
            {
                bribeButton = new GUIContent(randomAllow, "Bribe the Tech");
                if (GUI.Button(new Rect(10, 90, 180, 30), bribeButton, AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                    ManPlayer.inst.PayMoney(techCost);
                    lastTank.SetTeam(ManPlayer.inst.PlayerTeam);
                    CloseSubMenuClickable();
                }
            }
            else
            {
                bribeButton = new GUIContent(randomAllow, "Not enough BB");
                if (GUI.Button(new Rect(10, 90, 180, 30), bribeButton, AltUI.ButtonGrey))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            if (Singleton.playerTank)
            {
                if (GUI.Button(new Rect(10, 120, 180, 30), new GUIContent(randomEvict, "Provoke"), AltUI.ButtonRed))
                {
                    var mind = lastTank.GetComponent<EnemyMind>();
                    if (mind && mind.CommanderSmarts <= EnemySmarts.Meh)
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                        mind.AIControl.lastEnemy = Singleton.playerTank.visible;
                        mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                    }
                    else
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
            else if (GUI.Button(new Rect(10, 120, 180, 30), new GUIContent(randomEvict, "You have no tech!"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

            GUI.Label(new Rect(10, 65, 180, 30), GUI.tooltip);
        }

        public static void LaunchSubMenuClickable()
        {
            if (!KickStart.EnableBetterAI)
            {
                return;
            }
            DebugTAC_AI.Log("TACtical_AI: Opened Eviction menu!");
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
                KickStart.ReleaseControl();
                DebugTAC_AI.Log("TACtical_AI: Closed AI menu!");
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
                //DebugTAC_AI.Log(Mous + " | " + xMenuMin + " | " + xMenuMax + " | " + yMenuMin + " | " + yMenuMax);
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
