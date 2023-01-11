using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;

namespace TAC_AI.World
{
    public class ManEnemySiege : MonoBehaviour
    {
        private static ManEnemySiege inst;
        private UIMultiplayerHUD warningBanner;

        private static readonly string displayName = "Enemy Siege Health: ";
        private static int MaxHP = 100;
        private static float RaidCooldown = 0;
        private static bool ready = false;
        public static bool InProgress => inProgress;
        private static bool inProgress = false;
        public static EnemyPresence SiegingEnemyTeam {
            get {
                if (!inst)
                    return null;
                return inst.EP; 
            }
        }
        private EnemyPresence EP;
        private int Team = 0;
        private readonly List<Tank> techsInvolved = new List<Tank>();


        private static readonly FieldInfo attackBar = typeof(ManBlockLimiter).GetField("m_MaximumUsage", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo attackName = typeof(UIBlockLimit).GetField("m_TotalCountLabel", BindingFlags.NonPublic | BindingFlags.Instance);


        public static void Init()
        {
            if (!inst)
            {
                inst = new GameObject("ManEnemySiege").AddComponent<ManEnemySiege>();
                inst.warningBanner = (UIMultiplayerHUD)Singleton.Manager<ManHUD>.inst.GetHudElement(ManHUD.HUDElementType.Multiplayer);
            }
        }
        public static void ResetSiegeTimer(bool halfDelay = false)
        {
            if (halfDelay)
                RaidCooldown = AIGlobals.RaidCooldownTimeSecs / 2;
            else
                RaidCooldown = AIGlobals.RaidCooldownTimeSecs;
        }
        public static bool LaunchSiege(EnemyPresence enemyTeamInvolved)
        {
            if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                return false;
            if (!InProgress && (RaidCooldown <= 0 || AIGlobals.TurboAICheat))
            {
                if (enemyTeamInvolved.GlobalMobileTechCount() + 1 > KickStart.EnemyTeamTechLimit)
                {
                    inst.EP = enemyTeamInvolved;
                    WarnPlayers();
                    inProgress = true;
                    return true;
                }
            }
            return false;
        }
        public static void CancelSiege(EnemyPresence enemyTeamInvolved)
        {
            if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                return;
            if (enemyTeamInvolved == SiegingEnemyTeam)
            {
                EndSiege(true, true);
            }
        }

        public static void UpdateThis()
        {
            if (inst)
            {
                if (ManNetwork.IsHost)
                {
                    // Sieges are "All In" for the AI.  They should not retreat since a siege is a long distance attack.
                    if (inst.EP == null || !Singleton.playerTank)
                        return; // Cannot run while no targetable player Tech is present
                    CallAllRaidersToPlayerTile();
                    TryRefocusIdleRaidersOnPlayer();
                    ScrapStuckOrFrozenRaiders();
                    CheckSiegeEnded();
                }
            }
        }

        private static void CallAllRaidersToPlayerTile()
        {
            try
            {
                inst.EP.SetEvent(Singleton.playerTank.visible.tileCache.tile.Coord);
            }
            catch
            {
                DebugTAC_AI.Log("ManEnemySiege - Player Tech does not have a valid coord.  Could not update this frame.");
                return;
            }
        }
        private static void TryRefocusIdleRaidersOnPlayer()
        {
            inst.techsInvolved.Clear();
            foreach (Tank tech in ManTechs.inst.CurrentTechs)
            {
                if ((bool)tech?.visible?.isActive)
                {
                    if (tech.Team == inst.EP.Team && !tech.IsAnchored)
                    {
                        if (!tech.IsSleeping)
                        {
                            var helper = tech.GetHelperInsured();
                            if (!helper.lastEnemy)
                            {
                                helper.lastEnemy = Singleton.playerTank?.visible;
                                var mind = tech.GetComponent<EnemyMind>();
                                if (mind)
                                {
                                    if (mind.CommanderAttack == EnemyAttack.Coward)
                                        mind.CommanderAttack = EnemyAttack.Bully;
                                    mind.CommanderMind = EnemyAttitude.Homing;
                                }
                            }
                            inst.techsInvolved.Add(tech);
                        }
                        else
                            inst.techsFrozen.Add(tech);
                    }
                }
            }
        }
        private static void ScrapStuckOrFrozenRaiders()
        {
            int count = inst.techsFrozen.Count;
            for (int step = 0; step < count; count--)
            {
                Tank tech = inst.techsFrozen[0];
                inst.techsFrozen.RemoveAt(0);
                if ((bool)tech?.visible)
                {
                    UnloadedBases.RecycleLoadedTechToTeam(tech);
                    SpecialAISpawner.Purge(tech);
                }
            }
            inst.techsFrozen.Clear();
        }
        private static void CheckSiegeEnded()
        {
            var mainBase = UnloadedBases.GetTeamFunder(inst.EP);
            bool defeatedAllUnits = !Tank.IsEnemy(inst.Team, ManPlayer.inst.PlayerTeam) || (inst.techsInvolved.Count == 0 && !inst.EP.HasMobileETUs());
            if ((mainBase != null && !UnloadedBases.IsPlayerWithinProvokeDist(mainBase.tilePos)) || defeatedAllUnits)
            {
                NetworkHandler.TryBroadcastNewEnemySiege(inst.Team, MaxHP, false);
                EndSiege(shouldCooldown: defeatedAllUnits);
            }
        }



        public static void EndSiege(bool immedeate = false, bool shouldCooldown = false)
        {
            if (!inst || !ready)
                return;
            Singleton.Manager<ManHUD>.inst.HideHudElement(ManHUD.HUDElementType.BlockLimit);
            inst.EP.ResetEvent();
            inst.EP = null;
            ready = false;
            if (immedeate)
            {
                Singleton.Manager<ManHUD>.inst.DeInitialiseHudElement(ManHUD.HUDElementType.BlockLimit);
                inProgress = false;
            }
            else
            {
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.MissionComplete);
                inst.Invoke("EndSiege2", 1);
            }
            if (shouldCooldown)
                ResetSiegeTimer();
            else
                RaidCooldown = 0;
        }
        public void EndSiege2()
        {
            if (!inst)
                return;
            Singleton.Manager<ManHUD>.inst.DeInitialiseHudElement(ManHUD.HUDElementType.BlockLimit);
            inProgress = false;
        }

        private static long tempHP = 0;
        public static void WarnPlayers()
        {
            if (!inst)
                return;
            tempHP = 0;
            foreach (EnemyTechUnit ETU in inst.EP.ETUs)
                tempHP += ETU.Health;

            inst.Invoke("ThrowWarnPlayers", 3);
        }
        public void ThrowWarnPlayers()
        {
            Team = EP.Team;
            NetworkHandler.TryBroadcastNewEnemySiege(Team, tempHP, true);
            InitSiegeWarning(Team, tempHP);
        }
        public static void InitSiegeWarning(int team, long tempHPIn)
        {
            if (!inst)
                DebugTAC_AI.Log("TACtical_AI: ManEnemySiege - InitSiegeWarning inst IS NULL");
            inst.Team = team;
            tempHP = tempHPIn;
            inProgress = true;
            Singleton.Manager<ManHUD>.inst.InitialiseHudElement(ManHUD.HUDElementType.BlockLimit);
            Singleton.Manager<ManHUD>.inst.ShowHudElement(ManHUD.HUDElementType.BlockLimit);
            Singleton.Manager<ManHUD>.inst.InitialiseHudElement(ManHUD.HUDElementType.Multiplayer);
            Singleton.Manager<ManHUD>.inst.ShowHudElement(ManHUD.HUDElementType.Multiplayer);
            if (!inst.warningBanner)
                inst.warningBanner = (UIMultiplayerHUD)Singleton.Manager<ManHUD>.inst.GetHudElement(ManHUD.HUDElementType.Multiplayer);
            if (!inst.warningBanner)
            {
                DebugTAC_AI.Log("TACtical_AI: ManEnemySiege - warningBanner IS NULL");
                return;
            }

            int totalHealth = 0;
            foreach (EnemyTechUnit tech in inst.EP.ETUs)
            {
                if (tech != null && tech.MoveSpeed > 10)
                {
                    totalHealth += tech.tech.m_TechData.m_BlockSpecs.Count;
                }
            }

            MaxHP = totalHealth;
            ready = true;
            inst.warningBanner.Message = "WARNING: Siege Inbound";
            attackBar.SetValue(ManBlockLimiter.inst, totalHealth);
            ManSFX.inst.PlayMiscLoopingSFX(ManSFX.MiscSfxType.PayloadIncoming);
            inst.Invoke("RemoveWarning", 4f);

            ManBlockLimiter.CostChangeInfo CCI = new ManBlockLimiter.CostChangeInfo
            {
                m_VisibleID = 0,
                m_TechCost = MaxHP,
                m_CategoryCost = 0,
                m_TechCategory = ManBlockLimiter.TechCategory.Player,
                m_TeamColour = 4
            };
            ManBlockLimiter.inst.CostChangedEvent.Send(CCI);
            UIBlockLimit UIBL = (UIBlockLimit)Singleton.Manager<ManHUD>.inst.GetHudElement(ManHUD.HUDElementType.BlockLimit);
            if (!UIBL)
                return;
            //raidingTeam
            Text tex = (Text)attackName.GetValue(UIBL);
            tex.text = displayName +" <b>EN-ROUTE</b>";
            attackName.SetValue(UIBL, tex);
            DebugTAC_AI.Log("TACtical_AI: ManEnemySiege - Repurposed ManBlockLimiter and UIBlockLimit for Raid UI");

        }
        public void RemoveWarning()
        {
            ManSFX.inst.StopMiscLoopingSFX(ManSFX.MiscSfxType.PayloadIncoming);
            warningBanner.Message = "";
        }

        float delay = 0;
        private readonly List<Tank> techsFrozen = new List<Tank>();
        public void Update()
        {
            if (!ManPauseGame.inst.IsPaused && RaidCooldown > 0)
                RaidCooldown -= Time.deltaTime;
            if (!ready)
                return;
            if (Team == 0)
                return;
            delay += Time.deltaTime;

            if (delay > 1)
            {
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                {
                    techsInvolved.Clear();
                    foreach (Tank tech in ManTechs.inst.CurrentTechs)
                    {
                        if ((bool)tech?.visible.isActive)
                        {
                            if (tech.Team == inst.EP.Team)
                            {
                                if (!tech.IsAnchored)
                                {
                                    if (!tech.IsSleeping)
                                        techsInvolved.Add(tech);
                                }
                            }
                        }
                    }
                }
                int totalHealth = 0;
                foreach (Tank tech in techsInvolved)
                {
                    if ((bool)tech?.visible.isActive)
                    {
                        totalHealth += tech.blockman.blockCount;
                    }
                }
                UpdatePercentBar(totalHealth);
                delay = 0;
            }
        }

        // Blue is 0
        // Red is -1
        // Green is 1
        // Yellow is 2
        const int dispVal = 100;
        public void UpdatePercentBar(int combinedEnemyHealth)
        {
            if (dispVal == combinedEnemyHealth || combinedEnemyHealth <= 0)
                return;

            ManBlockLimiter.CostChangeInfo CCI = new ManBlockLimiter.CostChangeInfo
            {
                m_VisibleID = 0,
                m_TechCost = combinedEnemyHealth,
                m_CategoryCost = combinedEnemyHealth,
                m_TechCategory = ManBlockLimiter.TechCategory.Player,
                m_TeamColour = 5
            };
            //DebugTAC_AI.Log("TACtical_AI: ManEnemySiege - Color case " + teamC);
            ManBlockLimiter.inst.CostChangedEvent.Send(CCI);
            UIBlockLimit UIBL = (UIBlockLimit)Singleton.Manager<ManHUD>.inst.GetHudElement(ManHUD.HUDElementType.BlockLimit);
            if (!UIBL)
                return;
            //raidingTeam
            Text tex = (Text)attackName.GetValue(UIBL);
            tex.text = displayName + combinedEnemyHealth + "/" + MaxHP;
            attackName.SetValue(UIBL, tex);
            //teamC++;
        }
    }
}
