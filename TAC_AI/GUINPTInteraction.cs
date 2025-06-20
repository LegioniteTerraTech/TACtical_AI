using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using TerraTechETCUtil;

namespace TAC_AI.AI
{
    internal class GUINPTInteraction : MonoBehaviour
    {
        public class NetworkedNPTBribe : MessageBase
        {
            public NetworkedNPTBribe() { }
            public NetworkedNPTBribe(int playerID, uint techID, int bribeAmount)
            {
                PlayerID = playerID;
                TechID = techID;
                BribeAmount = bribeAmount;
            }

            public int PlayerID;
            public uint TechID;
            public int BribeAmount;
        }
        private static NetworkHook<NetworkedNPTBribe> netHook = new NetworkHook<NetworkedNPTBribe>(
            "TAC_AI.NetworkedNPTBribe", OnReceiveNPTBribe, NetMessageType.ToServerOnly);

        internal static void InsureNetHooks()
        {
            netHook.Enable();
        }

        //Handles the display that's triggered on right-click on friendly or neutral base team
        private static GUINPTInteraction inst;
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


        internal static void Initiate()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject()).AddComponent<GUINPTInteraction>();
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
        internal static void DeInit()
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
            if (button == ManPointer.Event.RMB && down && KickStart.IsIngame && Input.GetKey(KeyCode.T) && ManPointer.inst.targetTank)
            {
                int team = ManPointer.inst.targetTank.Team;
                if (team == ManPlayer.inst.PlayerTeam || AIGlobals.IsBaseTeamDynamic(team))
                {
                    if (ManPointer.inst.targetTank)
                        GetTank(ManPointer.inst.targetTank);
                }
            }
        }

        internal static void GetTank(Tank tank)
        {
            lastTank = tank; 
            try
            {
                Vector3 Mous = Input.mousePosition;
                xMenu = Mous.x - (HotWindow.width / 2);
                yMenu = Display.main.renderingHeight - Mous.y - 10;
            }
            catch (Exception)
            {
                throw new Exception("Display is null");
            }

            try
            {
                techCost = Mathf.RoundToInt(RawTechBase.GetBBCost(lastTank) * AIGlobals.BribeMulti);
            }
            catch (Exception)
            {
                throw new Exception("techCost is worthless");
            }
            try
            {
                if (ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTank.Team, out var ETD))
                {
                    var teamUnloaded = ManEnemyWorld.GetTeam(lastTank.Team);
                    if (teamUnloaded != null)
                    {
                        teamCost = teamUnloaded.GlobalTeamCost();
                    }
                }
            }
            catch (Exception)
            {
                throw new Exception("IsValid is worthless");
            }
            try
            {
                randomAllow = "<b>" + randomAllowSayings.GetRandomEntry() + "</b>";

                randomEvict = "<b>" + randomEvictSayings.GetRandomEntry() + "</b>";

                teamName = TeamNamer.GetTeamName(lastTank.Team).ToString();
            }
            catch (Exception)
            {
                throw new Exception("teamName is worthless");
            }
            LaunchSubMenuClickable();
            // BROKEN!!!!
            //AIGlobals.ModularMenu.OpenGUI(lastTank.blockman.IterateBlocks().FirstOrDefault());
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
                    HotWindow = GUILayout.Window(EvictionID, HotWindow, GUIHandler, "You Say:", AltUI.MenuLeft);
                    AltUI.EndUI();
                }
            }
        }

        private static void GUIHandler(int ID)
        {
            try
            {
                if (IsTankNull())
                {
                    CloseSubMenuClickable();
                }
                else
                {
                    if (playerTeam == lastTank.Team)
                        GUIOwnTeam();
                    else if (ManBaseTeams.IsTeammate(lastTank.Team, playerTeam))
                        GUIAlliedAutoTeam();
                    else if (ManBaseTeams.TryGetBaseTeamAny(lastTank.Team, out var ETD) && !ETD.IsReadonly)
                    {
                        if (lastTank.GetComponent<RLoadedBases.EnemyBaseFunder>())
                            GUIBaseTeamStatic();
                        else
                            GUIBaseTeamTech();
                    }
                    else
                        GUILoneTech();
                    GUI.DragWindow();
                }
            }
            catch (ExitGUIException e)
            {
                throw e;
            }
            catch { }
        }



        private static int playerTeam => ManPlayer.inst.PlayerTeam;
        private static void GUIOwnTeam()
        {
            GUILayout.Label("<b>" + teamName + "</b>");//¥¥
            if (GUILayout.Button(new GUIContent("Enable Auto", "Automatically Build Bases"), AltUI.ButtonGreen))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.RadarOn);
                TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, 0);
                CloseSubMenuClickable();
            }
            if (!GUI.tooltip.NullOrEmpty())
                GUILayout.Label(GUI.tooltip);
        }


        private static void GUIAlliedAutoTeam()
        {
            GUILayout.Label("<b>" + teamName + "</b>");//¥¥
            if (ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTank.Team, out var ETD))
            {
                GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
                GUILayout.Label("Money: ", AltUI.LabelWhite);
                GUILayout.Label(ETD.BuildBucks.ToString(), AltUI.LabelBlue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button(new GUIContent("Disable Auto", "Stop Building Bases"), AltUI.ButtonGreen))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIIdle);
                TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, 0);
                CloseSubMenuClickable();
            }
            int moneyGive;
            if (ManPlayer.inst.CanAfford(50000))
                moneyGive = 50000;
            else
                moneyGive = Mathf.Clamp(ManPlayer.inst.GetCurrentMoney(), 0, 49999);
            if (moneyGive > 0 && GUILayout.Button(new GUIContent("Give " + moneyGive + " BB", "Give the AI some materials"), AltUI.ButtonGreen))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, moneyGive);
            }
            if (!GUI.tooltip.NullOrEmpty())
                GUILayout.Label(GUI.tooltip);
        }


        private static void GUIBaseTeamStatic()
        { // Bases that store BB
            int teamFunds = 0;
            if (ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTank.Team, out var ETD))
            {
                teamFunds = ETD.BuildBucks;
            }
            else
            {
                var teamUnloaded = ManEnemyWorld.GetTeam(lastTank.Team);
                if (teamUnloaded != null)
                {
                    NP_BaseUnit EBU = UnloadedBases.RefreshTeamMainBaseIfAnyPossible(teamUnloaded);
                    if (EBU != null)
                    {
                        teamFunds = EBU.BuildBucks;
                        teamCost = teamUnloaded.GlobalTeamCost();
                    }
                }
            }
            GUILayout.Label("<b>" + teamName + "</b>");//¥¥
            GUILayout.BeginHorizontal();
            GUILayout.Label("¥¥: ");
            if (ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTank.Team, out ETD))
                GUILayout.Label(ETD.BuildBucks.ToString());
            else
                GUILayout.Label("???");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            int lastTeam = lastTank.Team;
            DispBaseBribe(lastTeam, teamFunds);
            DispBaseAnnoy(lastTeam, teamFunds);
            if (!GUI.tooltip.NullOrEmpty())
                GUILayout.Label(GUI.tooltip);
        }
        private static void DispBaseBribe(int lastTeam, int teamFunds)
        {
            if (teamFunds > 0)
            {
                GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
                GUILayout.Label("Bribe: ", AltUI.LabelWhite);
                GUILayout.Label((teamFunds + teamCost).ToString(), AltUI.LabelBlue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
                GUILayout.Label("<b>No Bases? :C</b>");
            GUIContent bribeButton;
            bool afford = ManPlayer.inst.CanAfford(teamFunds + teamCost);
            bool canPursuade = ManBaseTeams.CanAlterRelations(playerTeam, lastTeam);
            if (ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTeam, out var ETD))
            {
                if (ManPlayer.inst.PlayerTeam == lastTeam)
                {
                    bribeButton = new GUIContent(randomAllow, "Fully Allied!");
                    GUILayout.Button(bribeButton, AltUI.ButtonGrey);
                }
                else
                {
                    if (!canPursuade)
                        bribeButton = new GUIContent(randomAllow, "Refuses to be bribed");
                    if (!afford)
                        bribeButton = new GUIContent(randomAllow, "Not enough BB");
                    else
                    {
                        string nextAllyState;
                        if (ManBaseTeams.IsFriendlyBaseTeam(lastTeam))
                        {
                            nextAllyState = "Your Team";
                        }
                        else if (ManBaseTeams.IsSubNeutralBaseTeam(lastTeam))
                        {
                            nextAllyState = "Non-Hostile";
                        }
                        else if (ManBaseTeams.IsEnemyBaseTeam(lastTeam))
                        {
                            nextAllyState = "Neutral";
                        }
                        else
                            nextAllyState = "Neutral";
                        bribeButton = new GUIContent(randomAllow, "Bribe to " + nextAllyState);
                    }
                    if (GUILayout.Button(bribeButton, (afford && canPursuade) ? AltUI.ButtonGreen : AltUI.ButtonGrey))
                    {
                        if (afford && canPursuade)
                        {
                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                            ManPlayer.inst.PayMoney(teamFunds);
                            ETD.AddBuildBucks(teamFunds);
                            if (ManBaseTeams.IsFriendlyBaseTeam(lastTeam))
                            {
                                int newTeam = ManPlayer.inst.PlayerTeam;
                                ManEnemyWorld.ChangeTeam(lastTeam, newTeam);
                                ManEnemyWorld.TeamBribeEvent.Send(lastTeam, newTeam);
                            }
                            else
                            {
                                ETD.ImproveRelations(playerTeam);
                                if (ManBaseTeams.IsFriendlyBaseTeam(lastTeam))
                                {
                                    UIHelpersExt.BigF5broningBanner(playerTeam,
                                        ETD.teamName + " is now allied!");
                                }
                                ManEnemyWorld.TeamBribeEvent.Send(lastTeam, lastTeam);
                            }
                            CloseSubMenuClickable();
                        }
                        else
                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    }
                }
            }
        }
        private static void DispBaseAnnoy(int lastTeam, int teamFunds)
        {
            if (Singleton.playerTank && ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTeam, out var ETD))
            {
                if (ManBaseTeams.IsTeammate(ETD.teamID, playerTeam))
                {
                    if (GUILayout.Button(new GUIContent(randomEvict, "Return"), AltUI.ButtonRed))
                    {
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent(randomEvict, "Annoy the Team"), AltUI.ButtonRed))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.PayloadIncoming);
                        var mind = lastTank?.GetComponent<EnemyMind>();
                        if (!ManBaseTeams.IsEnemyBaseTeam(lastTeam))
                        {
                            ETD.DegradeRelations(playerTeam);
                            if (ManBaseTeams.IsEnemy(ETD.teamID, playerTeam))
                            {
                                ManEnemyWorld.TeamWarEvent.Send(lastTeam, lastTeam);
                                UIHelpersExt.BigF5broningBanner(TeamNamer.GetTeamName(lastTeam) + " is now hostile!");
                            }
                            CloseSubMenuClickable();
                        }
                        else
                            UIHelpersExt.BigF5broningBanner(TeamNamer.GetTeamName(lastTeam) + " is angry!");
                        if (mind)
                            RLoadedBases.RequestFocusFireNPTs(mind, Singleton.playerTank.visible, RequestSeverity.ThinkMcFly);
                    }
                }
            }
            else if (GUILayout.Button(new GUIContent(randomEvict, "You have no tech!"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }

        }


        private static void GUIBaseTeamTech()
        {
            GUILayout.Label("<b>" + teamName + "</b>");//¥¥
            if (playerTeam == lastTank.Team)
            {
                if (GUILayout.Button(new GUIContent("Enable Auto", "Automatically Build Bases"), AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.RadarOn);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, 0);
                    CloseSubMenuClickable();
                }
            }
            else if (ManBaseTeams.IsTeammate(lastTank.Team, playerTeam))
            {
                if (ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTank.Team, out var ETD))
                {
                    GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
                    GUILayout.Label("Money: ", AltUI.LabelWhite);
                    GUILayout.Label(ETD.BuildBucks.ToString(), AltUI.LabelBlue);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }

                if (GUILayout.Button(new GUIContent("Disable Auto", "Stop Building Bases"), AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIIdle);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, 0);
                    CloseSubMenuClickable();
                }
                int moneyGive;
                if (ManPlayer.inst.CanAfford(50000))
                    moneyGive = 50000;
                else
                    moneyGive = Mathf.Clamp(ManPlayer.inst.GetCurrentMoney(), 0, 49999);
                if (moneyGive > 0 && GUILayout.Button(new GUIContent("Give " + moneyGive + " BB", "Give the AI some materials"), AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, moneyGive);
                }
            }
            else
            {
                DispTechBribe();
                DispTechAnnoy();
            }
            if (!GUI.tooltip.NullOrEmpty())
                GUILayout.Label(GUI.tooltip);
        }
        private static void DispTechBribe()
        {
            if (techCost > 0)
            {
                GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
                GUILayout.Label("Bribe: ", AltUI.LabelWhite);
                GUILayout.Label(techCost.ToString(), AltUI.LabelBlue);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
                GUILayout.Label("<b>Free tech?</b>");
            GUIContent bribeButton;
            bool afford = ManPlayer.inst.CanAfford(techCost);
            if (afford)
            {
                bribeButton = new GUIContent(randomAllow, "Bribe the Tech");
                if (GUILayout.Button(bribeButton, AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, techCost);
                    CloseSubMenuClickable();
                }
            }
            else
            {
                bribeButton = new GUIContent(randomAllow, "Not enough BB");
                if (GUILayout.Button(bribeButton, AltUI.ButtonGrey))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                }
            }
        }
        private static void DispTechAnnoy()
        {
            if (Singleton.playerTank)
            {
                if (GUILayout.Button(new GUIContent(randomEvict, "Provoke"), AltUI.ButtonRed))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, 0);
                    CloseSubMenuClickable();
                }
            }
            else if (GUILayout.Button(new GUIContent(randomEvict, "You have no tech!"), AltUI.ButtonGrey))
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }
        }


        private static void GUILoneTech()
        {
            GUILayout.Label("<b>" + teamName + "</b>");//¥¥
            if (playerTeam == lastTank.Team)
            {
                if (GUILayout.Button(new GUIContent("Enable Auto", "Automatically Build Bases"), AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.RadarOn);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, 0);
                    CloseSubMenuClickable();
                }
            }
            else if (ManBaseTeams.IsTeammate(lastTank.Team, playerTeam))
            {
                if (ManBaseTeams.TryGetBaseTeamDynamicOnly(lastTank.Team, out var ETD))
                {
                    GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
                    GUILayout.Label("Money: ", AltUI.LabelWhite);
                    GUILayout.Label(ETD.BuildBucks.ToString(), AltUI.LabelBlue);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }

                if (GUILayout.Button(new GUIContent("Disable Auto", "Stop Building Bases"), AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIIdle);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, 0);
                    CloseSubMenuClickable();
                }
                int moneyGive;
                if (ManPlayer.inst.CanAfford(50000))
                    moneyGive = 50000;
                else
                    moneyGive = Mathf.Clamp(ManPlayer.inst.GetCurrentMoney(), 0, 49999);
                if (moneyGive > 0 && GUILayout.Button(new GUIContent("Give " + moneyGive + " BB", "Give the AI some materials"), AltUI.ButtonGreen))
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Buy);
                    TrySendNPTBribe(ManNetwork.inst.MyPlayer, lastTank, moneyGive);
                }
            }
            else
            {
            }
            if (!GUI.tooltip.NullOrEmpty())
                GUILayout.Label(GUI.tooltip);
        }



        /// <summary>
        /// To be called on the requesting client to inform the server of changes.
        /// </summary>
        /// <param name="player">Sending player (from their own client)</param>
        /// <param name="targetTech">The targeted Tech</param>
        /// <param name="bribeAmount">Set to 0 to toggle team automation.  
        /// Any value above zero will be given to the targeted Tech's team's Build Bucks</param>
        public static void TrySendNPTBribe(NetPlayer player, Tank targetTech, int bribeAmount)
        {
            if (netHook.CanBroadcast() && !ManNetwork.IsHost)
            {
                if (player)
                    netHook.TryBroadcast(new NetworkedNPTBribe(player.PlayerID, targetTech.GetTechNetID(), bribeAmount));
            }
            else
            {
                if (player?.CurTech?.tech && targetTech)
                    DoNPTBribe(Singleton.playerTank.visible, targetTech, bribeAmount);
            }
        }
        private static bool OnReceiveNPTBribe(NetworkedNPTBribe command, bool isServer)
        {
            var player = ManNetwork.inst.GetPlayer(command.PlayerID);
            var targetTech = ManNetTechs.inst.FindTech(command.TechID);
            if (player?.CurTech?.tech && targetTech)
            {
                if (command.BribeAmount > 0)
                {
                    if (ManPlayer.inst.CanAfford(command.BribeAmount))
                        DoNPTBribe(player.CurTech.tech.visible, targetTech.tech, command.BribeAmount);
                }
                else
                {
                    DoNPTBribe(player.CurTech.tech.visible, targetTech.tech, command.BribeAmount);
                }
                return true;
            }
            return false;
        }
        private static void DoNPTBribe(Visible provoker, Tank tech, int bribeAmount)
        {
            if (provoker.tank.Team == tech.Team)
            {
                var BT = ManBaseTeams.GetTeamAIBaseTeam(provoker.tank.Team);
                tech.SetTeam(BT.teamID);
            }
            else if (ManBaseTeams.IsTeammate(tech.Team, provoker.tank.Team))
            {
                if (bribeAmount > 0 && ManPlayer.inst.CanAfford(bribeAmount))
                {
                    if (ManBaseTeams.TryGetBaseTeamDynamicOnly(tech.Team, out var ETD))
                    {
                        ManPlayer.inst.PayMoney(bribeAmount);
                        ETD.AddBuildBucks(bribeAmount);
                    }
                }
                else
                {
                    if (ManBaseTeams.TryGetBaseTeamDynamicOnly(tech.Team, out var ETD))
                    {
                        ManPlayer.inst.AddMoney(ETD.BuildBucks);
                        ETD.SetBuildBucks = 0;
                    }
                    ManEnemyWorld.ChangeTeam(tech.Team, provoker.tank.Team);
                }
            }
            else
            {
                if (bribeAmount > 0)
                {
                    ManPlayer.inst.PayMoney(bribeAmount);
                    tech.SetTeam(provoker.tank.Team);
                }
                else
                {
                    var mind = tech?.GetComponent<EnemyMind>();
                    if (mind)
                    {
                        bool wasProvoked = false;
                        if (mind.CommanderSmarts <= EnemySmarts.Meh)
                            wasProvoked = true;
                        else if (!mind.AIControl.lastEnemy)
                            wasProvoked = true;
                        if (wasProvoked)
                        {
                            int newTeam = AIGlobals.GetRandomEnemyBaseTeam();
                            tech.SetTeam(newTeam);
                            mind = tech.GetComponent<EnemyMind>();
                            if (mind)
                            {
                                mind.GetRevengeOn(provoker);
                                mind.AIControl.Provoked = AIGlobals.ProvokeTime;
                            }
                        }
                        UIHelpersExt.BigF5broningBanner(mind.Tank.name + " is angry!");
                    }
                }
            }
        }
        


        internal static void LaunchSubMenuClickable()
        {
            if (!KickStart.EnableBetterAI)
            {
                return;
            }
            DebugTAC_AI.Log(KickStart.ModID + ": Opened Eviction menu!");
            isCurrentlyOpen = true;
            xMenu = Mathf.Clamp(xMenu, 0, Display.main.renderingWidth - HotWindow.width);
            yMenu = Mathf.Clamp(yMenu, 0, Display.main.renderingHeight - HotWindow.height);
            HotWindow.x = xMenu;
            HotWindow.y = yMenu;
            windowTimer = 1.25f;
            GUIWindow.SetActive(true);
        }
        internal static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                lastTank = null;
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                DebugTAC_AI.Log(KickStart.ModID + ": Closed AI menu!");
            }
        }

        private static bool MouseIsOverSubMenu()
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
