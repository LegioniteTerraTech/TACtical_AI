using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using TerraTechETCUtil;

namespace TAC_AI.Templates
{
    public class RawTechLoader : MonoBehaviour
    {
        internal static RawTechLoader inst;

        static readonly bool ForceSpawn = false;  // Test a specific base
        static readonly SpawnBaseTypes forcedBaseSpawn = SpawnBaseTypes.GSOMidBase;
        private static readonly Queue<QueueInstantTech> TechBacklog = new Queue<QueueInstantTech>();

        public const char baseChar = '¥';
        public const char turretChar = '⛨';


        public static void Initiate()
        {
            if (!inst)
                inst = new GameObject("EnemyWorldManager").AddComponent<RawTechLoader>();
            CursorChanger.AddNewCursors();
            if (dataPrefabber == null)
            {
                dataPrefabber = new TechData
                {
                    Name = "ERROR",
                    m_Bounds = new IntVector3(new Vector3(18, 18, 18)),
                    m_SkinMapping = new Dictionary<uint, string>(),
                    m_TechSaveState = new Dictionary<int, TechComponent.SerialData>(),
                    m_CreationData = new TechData.CreationData
                    {
                        m_Creator = "RawTech Import",
                        m_UserProfile = null,
                    },
                    m_BlockSpecs = new List<TankPreset.BlockSpec>()
                };
            }
        }
        public static void DeInitiate()
        {
            if (inst)
            {
                Destroy(inst);
                inst = null;
            }
        }
        public void ClearQueue()
        {
            TechBacklog.Clear();
        }
        public void TryPushTechSpawn()
        {
            if (TechBacklog.Count > 0)
            {
                QueueInstantTech QIT = TechBacklog.Dequeue();
                if (!QIT.PushSpawn())
                {   // Try again later
                    TechBacklog.Enqueue(QIT);
                }
            }
        }
        public void LateUpdate()
        {
            if (TechBacklog.Count > 0)
            {
                TryPushTechSpawn();
            }
        }





        // Main initiation function
        /// <summary>
        /// Returns 0 if failed, otherwise the BB cost of the spawned Tech
        /// </summary>
        internal static bool TryStartBase(Tank tank, TankAIHelper helper, BasePurpose purpose = BasePurpose.Harvesting)
        {
            try
            {
                if (!KickStart.enablePainMode || !KickStart.AllowEnemiesToStartBases)
                    return false;
                if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && !Singleton.Manager<ManNetwork>.inst.IsServer)
                    return false; // no want each client to have enemies spawn in new bases - stacked base incident!

                MakeSureCanExistWithBase(tank);

                if (GetEnemyBaseCountSearchRadius(tank.boundsCentreWorldNoCheck, AIGlobals.StartBaseMinSpacing) >= KickStart.MaxEnemyBaseLimit)
                {
                    int teamswatch = ReassignToExistingEnemyBaseTeam();
                    if (teamswatch == -1)
                        return false;
                    tank.SetTeam(teamswatch);
                    RemoveFromManPopIfNotLoner(tank);
                    return false;
                }

                if (GetEnemyBaseCountForTeam(tank.Team) > 0)
                    return false; // want no base spam on world load

                Vector3 pos = (tank.rootBlockTrans.forward * (helper.lastTechExtents + 8)) + tank.boundsCentreWorldNoCheck;

                if (!IsRadiusClearOfTechObst(pos, helper.lastTechExtents))
                {   // try behind
                    pos = (-tank.rootBlockTrans.forward * (helper.lastTechExtents + 8)) + tank.boundsCentreWorldNoCheck;

                    if (!IsRadiusClearOfTechObst(pos, helper.lastTechExtents))
                        return false;
                }

                int GradeLim = 0;
                try
                {
                    if (ManLicenses.inst.GetLicense(tank.GetMainCorp()).IsDiscovered)
                        GradeLim = ManLicenses.inst.GetLicense(tank.GetMainCorp()).CurrentLevel;
                }
                catch
                {
                    GradeLim = 99; // - creative or something else
                }

                // We validated?  
                //   Alright let's spawn the base!
                int startingMoney = DoSpawnBaseAtPosition(tank, pos, tank.Team, purpose, GradeLim);
                if (ManBaseTeams.TryInsureBaseTeam(tank.Team, out var teamInst))
                    teamInst.AddBuildBucks(startingMoney);

                AIWiki.hintBase.Show();
                InvokeHelper.Invoke(() =>
                {
                    AIWiki.hintBaseInteract.Show();
                }, 16);
                switch (ManBaseTeams.GetRelationsWritablePriority(ManPlayer.inst.PlayerTeam, tank.Team, TeamRelations.Enemy))
                {
                    case TeamRelations.Enemy:
                        AIWiki.hintInvader.Show();
                        break;
                    case TeamRelations.SameTeam:
                        AIWiki.hintInvader.Show();
                        break;
                }
                return true;
            }
            catch (Exception e)
            {
                DebugTAC_AI.ErrorReport("Epic Error on AI Base Spawning:\n" + e);
                DebugTAC_AI.Log("Epic Error on AI Base Spawning:\n" + e);
            }
            return false;
        }
        internal static bool TrySpawnBaseAtPositionNoFounder(FactionSubTypes FTE, Vector3 pos, int Team, BasePurpose purpose, int grade = 99)
        {
            try
            {
                if (!KickStart.enablePainMode || !KickStart.AllowEnemiesToStartBases)
                    return false;
                if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && !Singleton.Manager<ManNetwork>.inst.IsServer)
                    return false; // no want each client to have enemies spawn in new bases - stacked base incident!

                if (GetEnemyBaseCountSearchRadius(pos, AIGlobals.StartBaseMinSpacing) >= KickStart.MaxEnemyBaseLimit)
                {
                    return false;
                }

                if (GetEnemyBaseCountForTeam(Team) > 0)
                    return false; // want no base spam on world load

                int GradeLim = 0;
                try
                {
                    if (ManLicenses.inst.GetLicense(FTE).IsDiscovered)
                        GradeLim = ManLicenses.inst.GetLicense(FTE).CurrentLevel;
                }
                catch
                {
                    GradeLim = 99; // - creative or something else
                }

                // We validated?  
                //   Alright let's spawn the base!
                int startingMoney = DoSpawnBaseAtPositionNoFounder(FTE, pos, Team, purpose, GradeLim);
                if (ManBaseTeams.TryInsureBaseTeam(Team, out var teamInst))
                    teamInst.AddBuildBucks(startingMoney);

                AIWiki.hintBase.Show();
                InvokeHelper.Invoke(() =>
                {
                    AIWiki.hintBaseInteract.Show();
                }, 16);
                switch (ManBaseTeams.GetRelationsWritablePriority(ManPlayer.inst.PlayerTeam, Team, TeamRelations.Enemy))
                {
                    case TeamRelations.Enemy:
                        AIWiki.hintInvader.Show();
                        break;
                    case TeamRelations.SameTeam:
                        AIWiki.hintInvader.Show();
                        break;
                }
                return true;
            }
            catch (Exception e)
            {
                DebugTAC_AI.ErrorReport("Epic Error on AI Base Spawning:\n" + e);
                DebugTAC_AI.Log("Epic Error on AI Base Spawning:\n" + e);
            }
            return false;
        }
        internal static int FORCESpawnBaseAtPositionNoFounder(FactionSubTypes FTE, Vector3 pos, int Team, BasePurpose purpose, int grade = 99) =>
            DoSpawnBaseAtPositionNoFounder(FTE, pos, Team, purpose, grade);
        /// <summary>
        /// Tries to spawn a base expansion.  Returns false if we failed to spawn
        /// </summary>
        internal static bool SpawnBaseExpansion(Tank spawnerTank, Vector3 pos, int Team, RawTech type)
        {   // All bases are off-set rotated right to prevent the base from being built diagonally
            TryClearAreaForBase(pos);

            bool haveBB = (type.purposes.Contains(BasePurpose.Harvesting) || type.purposes.Contains(BasePurpose.TechProduction)) && !type.purposes.Contains(BasePurpose.NotStationary);

            BaseTerrain BT = BaseTerrain.Land;
            if (haveBB)
            {
                if (spawnerTank.GetComponent<AIControllerAir>())
                {
                    BT = BaseTerrain.Air;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(pos))
                    {
                        BT = BaseTerrain.Sea;
                    }
                }
            }
            else
            {   // Defense
                if (!RLoadedBases.PurchasePossible(type.baseCost, Team))
                    return false;
                if (spawnerTank.GetComponent<AIControllerAir>())
                {
                    BT = BaseTerrain.Air;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(pos))
                    {
                        BT = BaseTerrain.Sea;
                    }
                }
            }

            switch (BT)
            {
                case BaseTerrain.Air:
                    return SpawnAirBase(spawnerTank.rootBlockTrans.right, pos, Team, type, false, haveBB) > 0;
                case BaseTerrain.Sea:
                    return SpawnSeaBase(spawnerTank.rootBlockTrans.right, pos, Team, type, false, haveBB) > 0;
                default:
                    return SpawnLandBase(spawnerTank.rootBlockTrans.right, pos, Team, type, false, haveBB) > 0;
            }
        }

        internal static int SpawnTechFromBaseMobile(Vector3 pos, int Team, RawTech toSpawn)
        {

            if (!KickStart.AISelfRepair)
            {
                new BombSpawnTech(pos, Vector3.forward, Team, toSpawn, false, 0);
                return toSpawn.baseCost;
            }
            else
                return SpawnTechFragment(pos, Team, toSpawn);
        }



        /// <summary>
        /// Spawns a LOYAL enemy base 
        /// - this means this shouldn't be called for capture base missions.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        private static int DoSpawnBaseAtPosition(Tank spawnerTank, Vector3 pos, int Team, BasePurpose purpose, int grade = 99)
        {
            TryClearAreaForBase(pos);

            // this shouldn't be able to happen without being the server or being in single player
            bool haveBB;
            switch (purpose)
            {
                case BasePurpose.Headquarters:
                    haveBB = true;
                    break;
                case BasePurpose.HarvestingNoHQ:
                case BasePurpose.Harvesting:
                case BasePurpose.TechProduction:
                case BasePurpose.AnyNonHQ:
                    haveBB = true;
                    break;
                default:
                    haveBB = false;
                    break;
            }

            int extraBB; // Extras for new bases
            if (TankExtentions.GetMainCorp(spawnerTank) == FactionSubTypes.GSO)
            {
                switch (grade)
                {
                    case 0: // Really early game
                        extraBB = 500;
                        break;
                    case 1:
                        extraBB = 25000;
                        break;
                    case 2: // Tech builders active
                        extraBB = 50000;
                        break;
                    case 3:
                        extraBB = 75000;
                        break;
                    default:
                        extraBB = 100000;
                        break;
                }
            }
            else
            {
                switch (grade)
                {
                    case 0:
                        extraBB = 10000;
                        break;
                    case 1: // Tech builders active
                        extraBB = 50000;
                        break;
                    default:
                        extraBB = 75000;
                        break;
                }
            }
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            try
            {
                float divider = 5 / Singleton.Manager<ManLicenses>.inst.GetLicense(FactionSubTypes.GSO).CurrentLevel;
                extraBB = (int)(extraBB / divider);
            }
            catch { }



            // Are we a defended HQ?
            if (purpose == BasePurpose.Headquarters)
            {   // Summon additional defenses - DO NOT LET THIS RECURSIVELY TRIGGER!!!
                extraBB += DoSpawnBaseAtPosition(spawnerTank, pos + (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += DoSpawnBaseAtPosition(spawnerTank, pos - (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += DoSpawnBaseAtPosition(spawnerTank, pos + (Vector3.right * 64), Team, BasePurpose.Defense);
                extraBB += DoSpawnBaseAtPosition(spawnerTank, pos - (Vector3.right * 64), Team, BasePurpose.Defense);
                Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
            }

            // Now spawn teh main host
            FactionSubTypes FTE = TankExtentions.GetMainCorp(spawnerTank);
            BaseTerrain BT = BaseTerrain.Land;
            if (spawnerTank.GetComponent<AIControllerAir>())
            {
                BT = BaseTerrain.Air;
            }
            else if (KickStart.isWaterModPresent)
            {
                if (AIEPathing.AboveTheSea(pos))
                {
                    BT = BaseTerrain.Sea;
                }
            }

            RawTech BTemp;
            RawTechPopParams RTF = RawTechPopParams.Default;
            RTF.Faction = FTE;
            RTF.Terrain = BT;
            RTF.Purpose = purpose;
            RTF.Progression = lvl;
            RTF.TargetFactionGrade = grade;
            BTemp = FilteredSelectFromAll(RTF, true, AIGlobals.CancelOnErrorTech);
            if (BTemp == null)
                return 0;

            int finalBBCost = 0;
            switch (BT)
            {
                case BaseTerrain.Air:
                    finalBBCost = SpawnAirBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                    break;
                case BaseTerrain.Sea:
                    finalBBCost = SpawnSeaBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                    break;
                default:
                    finalBBCost = SpawnLandBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                    break;
            }
            if (finalBBCost > 0)
            {
                switch (purpose)
                {
                    case BasePurpose.Headquarters:
                        try
                        {
                            if (KickStart.DisplayEnemyEvents)
                            {
                                WorldPosition pos2 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);

                                switch (ManBaseTeams.GetRelationsWritablePriority(Team, ManBaseTeams.playerTeam, TeamRelations.Enemy))
                                {
                                    case TeamRelations.Enemy:
                                        AIGlobals.PopupEnemyInfo("Invader HQ!", pos2);
                                        TankAIManager.SendChatServer("Invader HQ!");
                                        TankAIManager.SendChatServer("Protect your terra prospectors!!");
                                        break;
                                    case TeamRelations.SubNeutral:
                                        AIGlobals.PopupSubNeutralInfo("Rival HQ!", pos2);
                                        TankAIManager.SendChatServer("Rival HQ!");
                                        TankAIManager.SendChatServer("Careful around them!");
                                        break;
                                    case TeamRelations.Neutral:
                                        AIGlobals.PopupNeutralInfo("Neutral HQ!", pos2);
                                        TankAIManager.SendChatServer("Neutral HQ!");
                                        break;
                                    case TeamRelations.Friendly:
                                        AIGlobals.PopupAllyInfo("Friendly HQ!", pos2);
                                        break;
                                    case TeamRelations.AITeammate:
                                        AIGlobals.PopupPlayerInfo("Allied HQ!", pos2);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        catch { }
                        break;
                    case BasePurpose.HarvestingNoHQ:
                    case BasePurpose.Harvesting:
                    case BasePurpose.TechProduction:
                    case BasePurpose.AnyNonHQ:

                        try
                        {
                            if (KickStart.DisplayEnemyEvents)
                            {
                                WorldPosition pos2 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);

                                switch (ManBaseTeams.GetRelationsWritablePriority(Team, ManBaseTeams.playerTeam, TeamRelations.Enemy))
                                {
                                    case TeamRelations.Enemy:
                                        AIGlobals.PopupEnemyInfo("Invader!", pos2);

                                        TankAIManager.SendChatServer("Invading Prospector Spotted!");
                                        TankAIManager.SendChatServer("Protect your terra prospectors!!");
                                        break;
                                    case TeamRelations.SubNeutral:
                                        AIGlobals.PopupSubNeutralInfo("Rival!", pos2);

                                        TankAIManager.SendChatServer("Rival Prospector Spotted!");
                                        TankAIManager.SendChatServer("They came here for your resources!");
                                        break;
                                    case TeamRelations.Neutral:
                                        AIGlobals.PopupNeutralInfo("Miner!", pos2);

                                        TankAIManager.SendChatServer("Miner Spotted!");
                                        TankAIManager.SendChatServer("They cannot be attacked without declaring war first. Watch your ores!");
                                        break;
                                    case TeamRelations.Friendly:
                                        AIGlobals.PopupAllyInfo("Ally!", pos2);

                                        TankAIManager.SendChatServer("Allied Prospector Spotted!");
                                        TankAIManager.SendChatServer("They will help, but they will also use resources nearby. Watch your ores!");
                                        break;
                                    case TeamRelations.AITeammate:
                                        AIGlobals.PopupPlayerInfo("Automatic!", pos2);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        catch { }
                        break;
                }
            }
            return finalBBCost;
        }

        private static int DoSpawnBaseAtPositionNoFounder(FactionSubTypes FTE, Vector3 pos, int Team, BasePurpose purpose, int grade = 99)
        {
            TryClearAreaForBase(pos);

            // this shouldn't be able to happen without being the server or being in single player
            bool haveBB;
            switch (purpose)
            {
                case BasePurpose.Headquarters:
                    haveBB = true;
                    break;
                case BasePurpose.HarvestingNoHQ:
                case BasePurpose.Harvesting:
                case BasePurpose.TechProduction:
                case BasePurpose.AnyNonHQ:
                    haveBB = true;
                    break;
                default:
                    haveBB = false;
                    break;
            }

            int extraBB; // Extras for new bases
            if (FTE == FactionSubTypes.GSO)
            {
                switch (grade)
                {
                    case 0: // Really early game
                        extraBB = 500;
                        break;
                    case 1:
                        extraBB = 25000;
                        break;
                    case 2: // Tech builders active
                        extraBB = 50000;
                        break;
                    case 3:
                        extraBB = 75000;
                        break;
                    default:
                        extraBB = 100000;
                        break;
                }
            }
            else
            {
                switch (grade)
                {
                    case 0:
                        extraBB = 10000;
                        break;
                    case 1: // Tech builders active
                        extraBB = 50000;
                        break;
                    default:
                        extraBB = 75000;
                        break;
                }
            }
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            try
            {
                float divider = 5 / Singleton.Manager<ManLicenses>.inst.GetLicense(FactionSubTypes.GSO).CurrentLevel;
                extraBB = (int)(extraBB / divider);
            }
            catch { }



            // Are we a defended HQ?
            if (purpose == BasePurpose.Headquarters)
            {   // Summon additional defenses - DO NOT LET THIS RECURSIVELY TRIGGER!!!
                extraBB += DoSpawnBaseAtPositionNoFounder(FTE, pos + (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += DoSpawnBaseAtPositionNoFounder(FTE, pos - (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += DoSpawnBaseAtPositionNoFounder(FTE, pos + (Vector3.right * 64), Team, BasePurpose.Defense);
                extraBB += DoSpawnBaseAtPositionNoFounder(FTE, pos - (Vector3.right * 64), Team, BasePurpose.Defense);
                Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
            }

            // Now spawn teh main host
            BaseTerrain BT = BaseTerrain.Land;
            if (KickStart.isWaterModPresent)
            {
                if (AIEPathing.AboveTheSea(pos))
                {
                    BT = BaseTerrain.Sea;
                }
            }

            RawTech BTemp = null;
            RawTechPopParams RTF = RawTechPopParams.Default;
            RTF.Faction = FTE;
            RTF.Terrain = BT;
            RTF.Purpose = purpose;
            RTF.Progression = lvl;
            RTF.TargetFactionGrade = grade;
            BTemp = FilteredSelectFromAll(RTF, true, AIGlobals.CancelOnErrorTech);
            if (BTemp == null)
                return 0;

            int finalBBCost = 0;
            switch (BT)
            {
                case BaseTerrain.Air:
                    finalBBCost = SpawnAirBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                    break;
                case BaseTerrain.Sea:
                    finalBBCost = SpawnSeaBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                    break;
                default:
                    finalBBCost = SpawnLandBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                    break;
            }
            if (finalBBCost > 0)
            {
                switch (purpose)
                {
                    case BasePurpose.Headquarters:
                        try
                        {
                            if (KickStart.DisplayEnemyEvents)
                            {
                                WorldPosition pos2 = WorldPosition.FromScenePosition(pos);
                                switch (ManBaseTeams.GetRelationsWritablePriority(Team, ManBaseTeams.playerTeam, TeamRelations.Enemy))
                                {
                                    case TeamRelations.Enemy:
                                        AIGlobals.PopupEnemyInfo("Invader HQ!", pos2);
                                        TankAIManager.SendChatServer("Invader HQ!");
                                        TankAIManager.SendChatServer("Protect your terra prospectors!!");
                                        break;
                                    case TeamRelations.SubNeutral:
                                        AIGlobals.PopupSubNeutralInfo("Rival HQ!", pos2);
                                        TankAIManager.SendChatServer("Rival HQ!");
                                        TankAIManager.SendChatServer("Careful around them!");
                                        break;
                                    case TeamRelations.Neutral:
                                        AIGlobals.PopupNeutralInfo("Neutral HQ!", pos2);
                                        TankAIManager.SendChatServer("Neutral HQ!");
                                        break;
                                    case TeamRelations.Friendly:
                                        AIGlobals.PopupAllyInfo("Friendly HQ!", pos2);
                                        break;
                                    case TeamRelations.AITeammate:
                                        AIGlobals.PopupPlayerInfo("Allied HQ!", pos2);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        catch { }
                        break;
                    case BasePurpose.HarvestingNoHQ:
                    case BasePurpose.Harvesting:
                    case BasePurpose.TechProduction:
                    case BasePurpose.AnyNonHQ:
                        try
                        {
                            if (KickStart.DisplayEnemyEvents)
                            {
                                WorldPosition pos2 = WorldPosition.FromScenePosition(pos);

                                switch (ManBaseTeams.GetRelationsWritablePriority(Team, ManBaseTeams.playerTeam, TeamRelations.Enemy))
                                {
                                    case TeamRelations.Enemy:
                                        AIGlobals.PopupEnemyInfo("Invader!", pos2);

                                        TankAIManager.SendChatServer("Invading Prospector Spotted!");
                                        TankAIManager.SendChatServer("Protect your terra prospectors!!");
                                        break;
                                    case TeamRelations.SubNeutral:
                                        AIGlobals.PopupSubNeutralInfo("Rival!", pos2);

                                        TankAIManager.SendChatServer("Rival Prospector Spotted!");
                                        TankAIManager.SendChatServer("They came here for your resources!");
                                        break;
                                    case TeamRelations.Neutral:
                                        AIGlobals.PopupNeutralInfo("Miner!", pos2);

                                        TankAIManager.SendChatServer("Miner Spotted!");
                                        TankAIManager.SendChatServer("They cannot be attacked without declaring war first. Watch your ores!");
                                        break;
                                    case TeamRelations.Friendly:
                                        AIGlobals.PopupAllyInfo("Ally!", pos2);

                                        TankAIManager.SendChatServer("Allied Prospector Spotted!");
                                        TankAIManager.SendChatServer("They will help, but they will also use resources nearby. Watch your ores!");
                                        break;
                                    case TeamRelations.AITeammate:
                                        AIGlobals.PopupPlayerInfo("Automatic!", pos2);
                                        break;
                                    default:
                                        break;
                                }// finish realloc
                            }
                        }
                        catch { }
                        break;
                    default:
                        haveBB = false;
                        break;
                }

            }
            return finalBBCost;
        }


        // Now General Usage
        /// <summary>
        /// Spawn a cab, and then add parts until we reach a certain point
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        /// <returns></returns>
        internal static int SpawnTechFragment(Vector3 pos, int Team, RawTech toSpawn)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = AIGlobals.LookRot(Vector3.forward, Vector3.up);

            BlockTypes bType = toSpawn.GetFirstBlock();
            TankBlock block = SpawnBlockS(bType, pos, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnLandBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }
            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
            if (effect)
            {
                effect.transform.Spawn(block.centreOfMassWorld);
            }
            ResetSkinIDSet();
            block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

            int cost = toSpawn.baseCost;
            Tank theTech;
            theTech = TechFromBlock(block, Team, toSpawn.techName + " ⟰");

            if (theTech)
            {
                var namesav = BookmarkBuilder.Init(theTech, toSpawn);
                namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                namesav.faction = RawTechUtil.CorpExtToCorp(toSpawn.faction);
                namesav.unprovoked = false;
                namesav.instant = false;
            }

            return cost;
        }

        /// <summary>
        /// For loading bases from Debug
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        /// <param name="storeBB"></param>
        /// <param name="ExtraBB"></param>
        /// <returns></returns>
        internal static int SpawnBase(Vector3 pos, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {
            return SpawnBase(pos, Vector3.forward, Team, GetBaseTemplate(toSpawn), storeBB, ExtraBB);
        }
        internal static int SpawnBase(Vector3 pos, Vector3 facing, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {
            return SpawnBase(pos, facing, Team, GetBaseTemplate(toSpawn), storeBB, ExtraBB);
        }
        internal static int SpawnBase(Vector3 pos, int Team, RawTech toSpawn, bool storeBB, int ExtraBB = 0)
        {
            return SpawnBase(pos, Vector3.forward, Team, toSpawn, storeBB, ExtraBB);
        }
        public static bool BypassSpawnCheckOnce = false;
        internal static int SpawnBase(Vector3 pos, Vector3 facing, int Team, RawTech toSpawn, bool storeBB, int ExtraBB = 0)
        {
#if DEBUG
            if (!AIGlobals.IsBaseTeamDynamic(Team) && !BypassSpawnCheckOnce)
            {
                //*
                DebugTAC_AI.Assert(KickStart.ModID + ": SpawnBase - Unexpected non-base team assigned to base spawn " + Team);
                // */ DebugTAC_AI.Exception(KickStart.ModID + ": SpawnBase - Unexpected non-base team assigned to base spawn " + Team);
            }
#endif
            BypassSpawnCheckOnce = false;
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = AIGlobals.LookRot(facing, Vector3.up);

            BlockTypes bType = toSpawn.GetFirstBlock();
            TankBlock block = SpawnBlockS(bType, pos, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }
            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
            if (effect)
            {
                effect.transform.Spawn(block.centreOfMassWorld);
            }
            ResetSkinIDSet();
            block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

            Tank theBase;
            if (storeBB)
            {
                int cost = toSpawn.baseCost + toSpawn.startingFunds + ExtraBB;
                theBase = TechFromBlock(block, Team, toSpawn.techName + " ¥¥");
                theBase.FixupAnchors(true);
                theBase.gameObject.GetOrAddComponent<RequestAnchored>();

                if (theBase)
                {
                    var namesav = BookmarkBuilder.Init(theBase, toSpawn);
                    namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                    namesav.faction = RawTechUtil.CorpExtToCorp(toSpawn.faction);
                    namesav.unprovoked = false;
                    namesav.instant = false;
                }

                return cost;
            }
            else
            {
                theBase = TechFromBlock(block, Team, toSpawn.techName + " " + turretChar);
                theBase.FixupAnchors(true);
                theBase.gameObject.GetOrAddComponent<RequestAnchored>();

                if (theBase)
                {
                    var namesav = BookmarkBuilder.Init(theBase, toSpawn);
                    namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                    namesav.faction = RawTechUtil.CorpExtToCorp(toSpawn.faction);
                    namesav.unprovoked = false;
                    namesav.instant = false;
                }

                return toSpawn.baseCost;
            }
        }
        internal static Tank GetSpawnBase(Vector3 pos, Vector3 facing, int Team, RawTech toSpawn, bool storeBB, int ExtraBB = 0)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = AIGlobals.LookRot(facing, Vector3.up);

            BlockTypes bType = toSpawn.GetFirstBlock();
            TankBlock block = SpawnBlockS(bType, pos, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": GetSpawnBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return null;
            }
            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
            if (effect)
            {
                effect.transform.Spawn(block.centreOfMassWorld);
            }
            ResetSkinIDSet();
            block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

            Tank theBase;
            if (storeBB)
            {
                int cost = toSpawn.baseCost + toSpawn.startingFunds + ExtraBB;
                theBase = TechFromBlock(block, Team, toSpawn.techName + " ¥¥");
                ManBaseTeams.InsureBaseTeam(Team).AddBuildBucks(cost);
            }
            else
            {
                theBase = TechFromBlock(block, Team, toSpawn.techName + " " + turretChar);
            }
            theBase.gameObject.GetOrAddComponent<RequestAnchored>();
            theBase.FixupAnchors(true);
            return theBase;
        }
        /// <summary>
        /// For loading bases from Debug
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        /// <param name="storeBB"></param>
        /// <param name="ExtraBB"></param>
        /// <returns></returns>
        internal static Tank SpawnBaseInstant(Vector3 pos, Vector3 forwards, int Team, SpawnBaseTypes toSpawn, bool storeBB, int ExtraBB = 0)
        {
            return SpawnBaseInstant(pos, forwards, Team, GetBaseTemplate(toSpawn), storeBB, ExtraBB);
        }
        internal static Tank SpawnBaseInstant(Vector3 pos, Vector3 forwards, int Team, RawTech toSpawn, bool storeBB, int ExtraBB = 0)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            Vector3 position = pos;
            position.y = offset;

            RawTechPopParams RTF = RawTechPopParams.Default;
            RTF.SpawnCharged = true;

            Tank theBase;
            if (storeBB)
                theBase = InstantTech(pos, forwards, Team, toSpawn.techName + " ¥¥", toSpawn.savedTech, RTF);
            else
            {
                theBase = InstantTech(pos, forwards, Team, toSpawn.techName + " " + turretChar, toSpawn.savedTech, RTF);
            }


            theBase.FixupAnchors(true);
            theBase.gameObject.GetOrAddComponent<RequestAnchored>();
            var namesav = BookmarkBuilder.Init(theBase, toSpawn);
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = RawTechUtil.CorpExtToCorp(toSpawn.faction);
            namesav.unprovoked = false;
            namesav.instant = true;
            return theBase;
        }
        /// <summary>
        /// For loading bases for natural enemy spawns
        /// </summary>
        /// <param name="spawnerForwards"></param>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        /// <param name="storeBB"></param>
        /// <param name="SpawnBB"></param>
        /// <returns></returns>
        private static int SpawnLandBase(Vector3 spawnerForwards, Vector3 pos, int Team, RawTech toSpawn, bool Starting, bool storeBB, int SpawnBB = 0)
        {
            if ((Starting && AIGlobals.StartingBasesAreAirdropped) || !KickStart.AISelfRepair)
            {   // Spawn a base instantly via base bomb
                new BombSpawnTech(pos, spawnerForwards, Team, toSpawn, storeBB, SpawnBB);
                return toSpawn.baseCost + SpawnBB;
            }
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = AIGlobals.LookRot(spawnerForwards, Vector3.up);

            BlockTypes bType = toSpawn.GetFirstBlock();
            TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnLandBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }
            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
            if (effect)
            {
                effect.transform.Spawn(block.centreOfMassWorld);
            }
            ResetSkinIDSet();
            block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, toSpawn.techName + " ¥¥");
            else
            {
                theBase = TechFromBlock(block, Team, toSpawn.techName + " " + turretChar);
            }

            theBase.FixupAnchors(true);
            theBase.Anchors.TryAnchorAll(true, true);
            theBase.gameObject.GetOrAddComponent<RequestAnchored>();
            var namesav = BookmarkBuilder.Init(theBase, toSpawn);
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = RawTechUtil.CorpExtToCorp(toSpawn.faction);
            namesav.unprovoked = false;
            namesav.instant = false;
            DebugTAC_AI.Log(KickStart.ModID + ": - SpawnLandBase: Spawning Land Base " + toSpawn.techName + ", ID (Still pending...)");
            return toSpawn.baseCost + SpawnBB;
        }
        private static int SpawnSeaBase(Vector3 spawnerForwards, Vector3 pos, int Team, RawTech toSpawn, bool Starting, bool storeBB, int SpawnBB = 0)
        {   // N/A!!! WIP!!!
            DebugTAC_AI.Log(KickStart.ModID + ": - SpawnSeaBase: There's no sea bases stored in the prefab pool.  Consider suggesting one!");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, Starting, SpawnBB);
            /*
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = AIGlobals.LookRot(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnSeaBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " " + turretChar);
            }


            theBase.FixupAnchors(true);
            var namesav =BookmarkBuilder.Init(theBase);
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
            */
        }
        private static int SpawnAirBase(Vector3 spawnerForwards, Vector3 pos, int Team, RawTech toSpawn, bool Starting, bool storeBB, int SpawnBB = 0)
        {   // N/A!!! WIP!!!
            DebugTAC_AI.Log(KickStart.ModID + ": - SpawnAirBase: There's no air bases stored in the prefab pool.  Consider suggesting one!");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, Starting, SpawnBB);
            /*
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = AIGlobals.LookRot(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnAirBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return 0;
            }

            Tank theBase;
            if (storeBB)
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " ¥¥" + (GetBaseStartingFunds(toSpawn) + ExtraBB));
            else
            {
                theBase = TechFromBlock(block, Team, GetEnglishName(toSpawn) + " " + turretChar);
            }


            theBase.FixupAnchors(true);
            var namesav = BookmarkBuilder.Init(theBase);
            namesav.blueprint = baseBlueprint;
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = GetMainCorp(toSpawn);
            namesav.unprovoked = false;
            namesav.instant = false;
            return GetBaseBBCost(baseBlueprint);
            */
        }



        // UNLOADED
        internal static TechData GetBaseExpansionUnloaded(Vector3 pos, NP_Presence EP, RawTech BT, out int[] bIDs)
        {   // All bases are off-set rotated right to prevent the base from being built diagonally
            TryClearAreaForBase(pos);

            bool haveBB = (BT.purposes.Contains(BasePurpose.Harvesting) || BT.purposes.Contains(BasePurpose.TechProduction)) && !BT.purposes.Contains(BasePurpose.NotStationary);
            bIDs = new int[1] { 1 };
            if (haveBB)
            {
                return GetUnloadedBase(BT, EP.Team, haveBB, out bIDs);
            }
            else
            {   // Defense
                if (!RLoadedBases.PurchasePossible(BT.baseCost, EP.Team))
                    return null;
                return GetUnloadedBase(BT, EP.Team, haveBB, out bIDs);
            }
        }
        internal static TechData GetUnloadedBase(RawTech BT, int team, bool storeBB, out int[] blocIDs, int SpawnBB = 0)
        {
            List<RawBlockMem> baseBlueprint = BT.savedTech;
            string name;

            if (storeBB)
                name = BT.techName + " ¥¥";
            else
            {
                name = BT.techName + " " + turretChar;
            }
            return ExportRawTechToTechData(name, baseBlueprint, team, false, out blocIDs);
        }
        internal static TechData GetUnloadedTech(SpawnBaseTypes SBT, int team, bool reuse, out int[] blocIDs)
        {
            return GetUnloadedTech(GetBaseTemplate(SBT), team, reuse, out blocIDs);
        }
        internal static TechData GetUnloadedTech(RawTech BT, int team, bool reuse, out int[] blocIDs)
        {
            List<RawBlockMem> baseBlueprint = BT.savedTech;
            string name = BT.techName;
            return ExportRawTechToTechData(name, baseBlueprint, team, reuse, out blocIDs);
        }


        // Mobile Enemy Techs
        /// <summary>
        /// Spawns a Tech at a position with a directional heading from any cached RAWTECH population.
        /// </summary>
        /// <param name="pos">SCENE position of where to spawn</param>
        /// <param name="forwards">The forwards LookRotation of the spawn relative to the world</param>
        /// <param name="Team">Spawning team</param>
        /// <param name="factionType">population faction to filter by.  Leave NULL to search all.</param>
        /// <param name="terrainType">The terrain to filter by. Leave Any to include all terrain</param>
        /// <param name="subNeutral">Spawn on Sub-Neutral</param>
        /// <param name="snapTerrain">Snap spawning to terrain</param>
        /// <param name="maxGrade">Max allowed grade to filter.  leave at 99 to allow any</param>
        /// <param name="maxPrice">Max allowed price to filter.  leave at 0 to allow any</param>
        /// <returns>A new Tech that's (hopefully) spawned in the world.  Will return null if it fails.</returns>
        public static Tank SpawnRandomTechAtPosHead(Vector3 pos, Vector3 forwards, int Team, RawTechPopParams filter, bool nullOnErrorTech)
        {   // This will try to spawn player-made enemy techs as well
            if (filter.Disarmed)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();
            filter.ForceAnchor = false;
            var RT = FilteredSelectFromAll(filter, true, nullOnErrorTech);
            if (RT == null)
                return null;
            return SpawnMobileTechPrefab(pos, forwards, Team, RT, filter);
        }

        /// <summary>
        /// Spawns a Tech at a position with a directional heading from any cached RAWTECH population.
        /// </summary>
        /// <param name="pos">SCENE position of where to spawn</param>
        /// <param name="forwards">The forwards LookRotation of the spawn relative to the world</param>
        /// <param name="Team">Spawning team</param>
        /// <param name="outTank">The Tech that spawned (if the tech is true)</param>
        /// <param name="factionType">population faction to filter by.  Leave NULL to search all.</param>
        /// <param name="terrainType">The terrain to filter by. Leave Any to include all terrain</param>
        /// <param name="subNeutral">Spawn on Sub-Neutral</param>
        /// <param name="snapTerrain">Snap spawning to terrain</param>
        /// <param name="maxGrade">Max allowed grade to filter.  leave at 99 to allow any</param>
        /// <param name="maxPrice">Max allowed price to filter.  leave at 0 to allow any</param>
        /// <returns>True if outTank is valid.</returns>
        public static bool SpawnRandomTechAtPosHead(Vector3 pos, Vector3 forwards, int Team, out Tank outTank, RawTechPopParams filter)
        {   // This will try to spawn player-made enemy techs as well

            if (filter.Disarmed)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();

            RawTech RT = FilteredSelectFromAll(filter, true, true);
            if (RT != null)
            {
                outTank = SpawnMobileTechPrefab(pos, forwards, Team, RT, filter);
                return true;
            }
            outTank = null;
            return false;
        }
        
        internal static Tank SpawnMobileTechPrefab(Vector3 pos, Vector3 forwards, int Team, RawTech toSpawn, RawTechPopParams filter)
        {
            filter.ForceAnchor = false;
            // Stop it from going INTO THE GROUND
            float height = ManWorld.inst.ProjectToGround(pos, true).y;
            if (pos.y < height)
                pos.y = height;
            Tank theTech = toSpawn.SpawnRawTech(pos, Team, forwards, filter.SnapTerrain, 
                filter.SpawnCharged, filter.RandSkins, filter.ForceCompleted);
            /*
            string baseBlueprint = toSpawn.savedTech;
            Tank theTech = InstantTech(pos, forwards, Team, toSpawn.techName, baseBlueprint, snapTerrain, population: pop);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnMobileTech - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (snapTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }
                Quaternion quat = AIGlobals.LookRot(forwards, Vector3.up);

                BlockTypes bType = toSpawn.GetFirstBlock();
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
                if (!worked)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": SpawnMobileTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }
                var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                if (effect)
                {
                    effect.transform.Spawn(block.centreOfMassWorld);
                }
                ResetSkinIDSet();
                block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

                theTech = TechFromBlock(block, Team, toSpawn.techName);

                var namesav = BookmarkBuilder.Init(theTech, baseBlueprint);
                namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                namesav.faction = RawTechUtil.CorpExtToCorp(toSpawn.faction);
                namesav.unprovoked = subNeutral;
            }
            */
            if (theTech && filter.IsPopulation)
                AddToManPopIfLoner(theTech, false);

            theTech.FixupAnchors(true);

            return theTech;
        }
        
        internal static bool SpawnAttractTech(Vector3 pos, Vector3 forwards, int Team, BaseTerrain terrainType = BaseTerrain.Land,
            FactionSubTypes faction = FactionSubTypes.NULL, BasePurpose purpose = BasePurpose.NotStationary)
        {
            RawTech baseTemplate;
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            RawTechPopParams RTF = RawTechPopParams.Default;
            RTF.Faction = faction;
            RTF.Terrain = terrainType;
            RTF.Purpose = purpose;
            RTF.Progression = lvl;
            RTF.MaxPrice = 0;
            RTF.SearchAttract = true;
            RTF.ExcludeErad = true;
            baseTemplate = FilteredSelectFromAll(RTF, false, true);
            if (baseTemplate == null)
                return false;

            Tank theTech = InstantTech(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, RTF);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Assert(true, KickStart.ModID + ": SpawnAttractTech - Generation failed, falling back to slower, reliable Tech building method");
                SlowTech(pos, forwards, Team, baseTemplate.techName, baseTemplate, RTF);
            }

            DebugTAC_AI.Log(KickStart.ModID + ": SpawnAttractTech - Spawned " + baseTemplate.techName);
            return true;

        }
        
        internal static Tank SpawnTechAutoDetermine(Vector3 pos, Vector3 forwards, int Team, RawTech Blueprint, 
            bool subNeutral = false, bool snapTerrain = true, bool forceInstant = false, bool pop = false, int extraBB = 0)
        {
            List<RawBlockMem> baseBlueprint = Blueprint.savedTech;

            Tank theTech;

            if (subNeutral)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();

            RawTechPopParams RTF = RawTechPopParams.Default;
            RTF.Offset = snapTerrain ? RawTechOffset.RaycastTerrainAndScenery : RawTechOffset.Exact;
            RTF.Purpose = BasePurpose.NotStationary;
            RTF.IsPopulation = pop;

            if (!forceInstant && RTF.ForceAnchor)
            {
                //theTech = null; //InstantTech does not handle this correctly 
                bool storeBB = Blueprint.purposes.Contains(BasePurpose.Harvesting) || Blueprint.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName + " ¥¥", baseBlueprint, RTF);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                {
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName, baseBlueprint, RTF);
                }
            }
            else
            {
                if (Blueprint.purposes.Contains(BasePurpose.Defense))
                {
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName + " " + turretChar, baseBlueprint, RTF);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName, baseBlueprint, RTF);
            }

            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Assert(true ,KickStart.ModID + ": SpawnTechExternal - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (snapTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }

                Quaternion quat = AIGlobals.LookRot(forwards, Vector3.up);

                BlockTypes bType = Blueprint.GetFirstBlock();
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
                if (!worked)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": SpawnEnemyTechExt - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }
                var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                if (effect)
                {
                    effect.transform.Spawn(block.centreOfMassWorld);
                }
                ResetSkinIDSet();
                block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

                bool storeBB = !Blueprint.purposes.Contains(BasePurpose.NotStationary) && (Blueprint.purposes.Contains(BasePurpose.Harvesting) || Blueprint.purposes.Contains(BasePurpose.TechProduction));

                if (storeBB)
                {
                    theTech = TechFromBlock(block, Team, Blueprint.techName + " ¥¥");
                    theTech.FixupAnchors(true);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                {
                    if (Blueprint.purposes.Contains(BasePurpose.Defense))
                    {
                        theTech = TechFromBlock(block, Team, Blueprint.techName + " " + turretChar);
                        theTech.gameObject.AddComponent<RequestAnchored>();
                    }
                    else
                        theTech = TechFromBlock(block, Team, Blueprint.techName);
                }

                var namesav = BookmarkBuilder.Init(theTech, Blueprint);
                namesav.infBlocks = false;
                namesav.faction = RawTechUtil.CorpExtToCorp(Blueprint.faction);
                namesav.unprovoked = subNeutral;
            }

            DebugTAC_AI.Log(KickStart.ModID + ": SpawnTechExternal - Spawned " + Blueprint.techName);

            return theTech;
        }
        
        internal static bool TrySpawnSpecificTech(Vector3 pos, Vector3 forwards, int Team, RawTechPopParams filter)
        {
            RawTech baseTemplate = FilteredSelectFromAll(filter, false, true);
            if (baseTemplate == null)
                return false;
            bool MustBeAnchored = !baseTemplate.purposes.Contains(BasePurpose.NotStationary);

            Tank theTech;
            if (filter.Disarmed && filter.IsPopulation)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();
            if (MustBeAnchored)
            {
                //theTech = null; //InstantTech does not handle this correctly 
                bool storeBB = baseTemplate.purposes.Contains(BasePurpose.Harvesting) || baseTemplate.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName + " ¥¥", 
                        baseTemplate.savedTech, filter);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                {
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, filter);
                }
            }
            else
            {
                if (baseTemplate.purposes.Contains(BasePurpose.Defense))
                {
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName + " " + turretChar, baseTemplate.savedTech, filter);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, filter);
            }

            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Assert(true, KickStart.ModID + ": SpawnSpecificTypeTech - Generation failed, falling back to slower, reliable Tech building method");
                Vector3 position = pos;
                Quaternion quat = AIGlobals.LookRot(forwards, Vector3.up);

                BlockTypes bType = baseTemplate.GetFirstBlock();
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
                if (!worked)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": SpawnSpecificTypeTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return false;
                }
                var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                if (effect)
                {
                    effect.transform.Spawn(block.centreOfMassWorld);
                }
                ResetSkinIDSet();
                block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

                bool storeBB = !baseTemplate.purposes.Contains(BasePurpose.NotStationary) && (baseTemplate.purposes.Contains(BasePurpose.Harvesting) || baseTemplate.purposes.Contains(BasePurpose.TechProduction));

                if (storeBB)
                {
                    theTech = TechFromBlock(block, Team, baseTemplate.techName + " ¥¥");
                    theTech.FixupAnchors(true);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                {
                    if (baseTemplate.purposes.Contains(BasePurpose.Defense))
                    {
                        theTech = TechFromBlock(block, Team, baseTemplate.techName + " " + turretChar);
                        theTech.gameObject.AddComponent<RequestAnchored>();
                    }
                    else
                        theTech = TechFromBlock(block, Team, baseTemplate.techName);
                }

                var namesav = BookmarkBuilder.Init(theTech, baseTemplate);
                namesav.infBlocks = GetEnemyBaseSupplies(baseTemplate);
                namesav.faction = RawTechUtil.CorpExtToCorp(baseTemplate.faction);
            }

            if (theTech.IsNotNull())
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnSpecificTypeTech - Spawned " + baseTemplate.techName);
            return true;
        }
        /// <summary>
        /// Spawns tech with a one update delay to prevent overlapping spawns 
        /// </summary>
        internal static bool TrySpawnSpecificTechSafe(Vector3 pos, Vector3 forwards, int Team, RawTechPopParams filter, Action<Tank> fallbackOp = null)
        {
            RawTech baseTemplate = FilteredSelectFromAll(filter, false, true);
            if (baseTemplate == null)
                return false;

            if (filter.Disarmed && filter.IsPopulation)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();


            if (filter.ForceAnchor)
            {
                //theTech = null; //InstantTech does not handle this correctly 
                bool storeBB = baseTemplate.purposes.Contains(BasePurpose.Harvesting) || baseTemplate.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName + " ¥¥", baseTemplate.savedTech, filter, fallbackOp);
                }
                else
                {
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, filter, fallbackOp);
                }
            }
            else
            {
                if (baseTemplate.purposes.Contains(BasePurpose.Defense))
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName + " " + turretChar, baseTemplate.savedTech, filter, fallbackOp);
                else
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, filter, fallbackOp);
            }
            DebugTAC_AI.Log(KickStart.ModID + ": SpawnSpecificTypeTechSafe - Spawned " + baseTemplate.techName);
            return true;

        }
       

        // imported ENEMY cases
        private static readonly List<int> FailedSearch = new List<int> { -1 };
        /// <summary>
        /// handleFallback is true -> CAN RETURN -1 - CANCEL SPAWN/FALLBACK AT THIS POINT!
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        internal static List<int> GetExternalIndexes(RawTechPopParams filter, List<int> cache, bool handleFallback)
        {
            if (cache.Any())
                DebugTAC_AI.Exception("cache given had some entries in it!  Clear it out first!");
            try
            {   // Filters
                //DebugTAC_AI.Log(KickStart.ModID + ": GetExternalIndexes - Fetching with " + faction + " - " + bestPlayerFaction + " - " + terra + " - " + maxGrade + " - " + maxPrice);

                for (int step = 0; step < ModTechsDatabase.ExtPopTechsAllCount(); step++)
                {
                    RawTech cand = ModTechsDatabase.ExtPopTechsAllLookup(step);
                    if (FilterSelectAll(cand, filter))
                        cache.Add(step);
                }

                if (!cache.Any())
                {
                    if (handleFallback)
                        return FailedSearch;
                    return cache;
                }

                // final list compiling
                cache.Shuffle();

                return cache;
            }
            catch { }

            if (handleFallback)
                return FailedSearch;
            return cache;
        }

        private static List<int> SearchSingleUse = new List<int>();
        internal static int GetExternalIndex(RawTechPopParams filter)
        {
            if (SearchSingleUse.Any())
                throw new InvalidOperationException("SearchSingleUse is already in use! Cannot nest operations!");
            try
            {
                try
                {
                    return GetExternalIndexes(filter, SearchSingleUse, true).GetRandomEntry();
                }
                finally
                {
                    SearchSingleUse.Clear();
                }
            }
            catch { }

            return -1;
        }

        internal static bool FindNextBest(out RawTech nextBest, List<RawTech> toSearch, int currentPrice)
        {
            nextBest = GetBaseTemplate(SpawnBaseTypes.NotAvail);
            try
            {
                int highVal = currentPrice;
                foreach (var item in toSearch)
                {
                    if (highVal < item)
                    {
                        highVal = item;
                        nextBest = item;
                    }
                }
            }
            catch { }
            return GetBaseTemplate(SpawnBaseTypes.NotAvail) != nextBest;
        }

        internal static FactionLevel TryGetPlayerLicenceLevel()
        {
            FactionLevel lvl = FactionLevel.GSO;
            try
            {
                if (!ManGameMode.inst.CanEarnXp())
                    return FactionLevel.ALL;
                if (Singleton.Manager<ManLicenses>.inst.IsLicenseDiscovered(FactionSubTypes.GC))
                    lvl = FactionLevel.GC;
                if (Singleton.Manager<ManLicenses>.inst.IsLicenseDiscovered(FactionSubTypes.VEN))
                    lvl = FactionLevel.VEN;
                if (Singleton.Manager<ManLicenses>.inst.IsLicenseDiscovered(FactionSubTypes.HE))
                    lvl = FactionLevel.HE;
                if (Singleton.Manager<ManLicenses>.inst.IsLicenseDiscovered(FactionSubTypes.BF))
                    lvl = FactionLevel.BF;
                if (Singleton.Manager<ManLicenses>.inst.IsLicenseDiscovered(FactionSubTypes.SJ))
                    lvl = FactionLevel.SJ;
                if (Singleton.Manager<ManLicenses>.inst.IsLicenseDiscovered(FactionSubTypes.EXP))
                    lvl = FactionLevel.EXP;
            }
            catch { }
            return lvl;
        }

        internal static bool ShouldUseCustomTechs(out int found, RawTechPopParams filter)
        {
            try
            {
                if (ShouldUseCustomTechs(ref SearchSingleUse, filter))
                {
                    found = SearchSingleUse.GetRandomEntry();
                    return true;
                }
                found = -1;
                return false;
            }
            finally
            {
                SearchSingleUse.Clear();
            }
        }
        /// <summary>
        /// OBSOLETE: use FilteredSelectFromAll instead!
        /// </summary>
        internal static bool ShouldUseCustomTechs(ref List<int> validIndexes, RawTechPopParams filter)
        {
            if (ShufflerSingleUse.Any())
                throw new InvalidOperationException("ShufflerSingleUse is already in use! Cannot nest operations!");
            try
            {

                validIndexes = GetExternalIndexes(filter, validIndexes, false);
                int CustomTechs = validIndexes.Count;
                ShufflerSingleUse = GetEnemyBaseTypes(filter, ShufflerSingleUse, true);
                int PrefabTechs = ShufflerSingleUse.Count;

                if (validIndexes.FirstOrDefault() == -1)
                    CustomTechs = 0;
                if (ShufflerSingleUse.FirstOrDefault() == SpawnBaseTypes.NotAvail)
                    PrefabTechs = 0;

                int CombinedVal = CustomTechs + PrefabTechs;

                if (KickStart.TryForceOnlyPlayerSpawns)
                {
                    if (CustomTechs > 0)
                    {
                        /*
                        DebugTAC_AI.Log(KickStart.ModID + ": ShouldUseCustomTechs - Forced Local Techs spawn possible: true");
                        DebugTAC_AI.Log(KickStart.ModID + ": ShouldUseCustomTechs - Indexes Available: ");
                        StringBuilder SB = new StringBuilder();
                        foreach (int val in validIndexes)
                        {
                            SB.Append(val + ", ");
                        }
                        DebugTAC_AI.Log(SB.ToString()); */
                        return true;
                    }
                    //else
                    //    DebugTAC_AI.Log(KickStart.ModID + ": ShouldUseCustomTechs - Forced Player-Made Techs spawn possible: false");
                }
                else
                {
                    if (PrefabTechs == 0)
                    {
                        if (CustomTechs > 0)
                        {
                            /*
                                DebugTAC_AI.Log(KickStart.ModID + ": ShouldUseCustomTechs - There's only Local Techs available");
                                DebugTAC_AI.Log(KickStart.ModID + ": ShouldUseCustomTechs - Indexes Available: ");
                                StringBuilder SB = new StringBuilder();
                                foreach (int val in validIndexes)
                                {
                                    SB.Append(val + ", ");
                                }
                                DebugTAC_AI.Log(SB.ToString());*/
                            return true;
                        }
                        //else
                        //    DebugTAC_AI.Log(KickStart.ModID + ": ShouldUseCustomTechs - No Techs found");
                        return false;
                    }
                    float RAND = UnityEngine.Random.Range(0, CombinedVal);
                    //DebugTAC_AI.Log(KickStart.ModID + ": ShouldUseCustomTechs - Chance " + CustomTechs + "/" + CombinedVal + ", meaning a " + (int)(((float)CustomTechs / (float)CombinedVal) * 100f) + "% chance.   RAND value " + RAND);
                    if (RAND > PrefabTechs)
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                ShufflerSingleUse.Clear();
            }
        }
        /// <summary>
        /// OBSOLETE: use FilteredSelectFromAll instead!
        /// </summary>
        internal static bool ShouldUseCustomTechs(RawTechPopParams filter)
        {
            if (SearchSingleUse.Any())
                throw new InvalidOperationException("SearchSingleUse is already in use! Cannot nest operations!");
            if (ShufflerSingleUse.Any())
                throw new InvalidOperationException("ShufflerSingleUse is already in use! Cannot nest operations!");
            try
            {
                int CustomTechs = GetExternalIndexes(filter, SearchSingleUse, false).Count;
                int PrefabTechs = GetEnemyBaseTypes(filter, ShufflerSingleUse, true).Count;
                int CombinedVal = CustomTechs + PrefabTechs;


                float RAND = UnityEngine.Random.Range(0, CombinedVal);
                if (RAND > PrefabTechs)
                {
                    return true;
                }
                return false;
            }
            finally
            {
                SearchSingleUse.Clear();
                ShufflerSingleUse.Clear();
            }
        }
        public static void FilteredSelectBatchFromAll(RawTechPopParams filter, List<RawTech> rTechs, bool fallbackHandling)
        {
            if (rTechs == null)
                throw new InvalidOperationException("rTechs is null");
            if (SearchSingleUse.Any())
                throw new InvalidOperationException("SearchSingleUse is already in use! Cannot nest operations!");
            if (ShufflerSingleUse.Any())
                throw new InvalidOperationException("ShufflerSingleUse is already in use! Cannot nest operations!");
            try
            {
                rTechs.Clear();
                List<int> selectedExt = GetExternalIndexes(filter, SearchSingleUse, fallbackHandling);
                List<SpawnBaseTypes> SBT = GetEnemyBaseTypes(filter, ShufflerSingleUse, fallbackHandling);
                foreach (var item in SBT)
                    rTechs.Add(ModTechsDatabase.InternalPopTechs[item]);
                for (int i = 0; i < selectedExt.Count; i++)
                    rTechs.Add(ModTechsDatabase.ExtPopTechsAllLookup(i));
                rTechs.Shuffle();
            }
            finally { 
                SearchSingleUse.Clear();
                ShufflerSingleUse.Clear();
            }
        }

        internal static RawTech FilteredSelectFromAll(RawTechPopParams filter, bool handleFallback, bool nullIfErrorTech)
        {
            if (SearchSingleUse.Any())
            {
                DebugTAC_AI.LogSpawn(KickStart.ModID + ": SearchSingleUse is already in use! Cannot nest operations");
                throw new InvalidOperationException("SearchSingleUse is already in use! Cannot nest operations!");
            }
            if (ShufflerSingleUse.Any())
            {
                DebugTAC_AI.LogSpawn(KickStart.ModID + ": ShufflerSingleUse is already in use! Cannot nest operations");
                throw new InvalidOperationException("ShufflerSingleUse is already in use! Cannot nest operations!");
            }
            try
            {
                List<int> selectedExt = GetExternalIndexes(filter, SearchSingleUse, handleFallback);
                List<SpawnBaseTypes> SBT = GetEnemyBaseTypes(filter, ShufflerSingleUse, handleFallback);
                int extTechs = selectedExt.Count;
                int PrefabTechs = SBT.Count;
                int CombinedVal = extTechs + PrefabTechs;
                if (extTechs == 0)
                {
                    if (PrefabTechs == 0)
                    {
                        if (KickStart.TryForceOnlyPlayerSpawns)
                            DebugTAC_AI.LogSpawn(KickStart.ModID + ": FilteredSelectFromAll - Forced Player-Made Techs spawn possible: false");
                        DebugTAC_AI.LogSpawn(KickStart.ModID + ": FilteredSelectFromAll - No Techs found");
                        if (handleFallback)
                        {
                            ShufflerSingleUse.Clear();
                            return ModTechsDatabase.InternalPopTechs[InternalFallbackHandler(filter.Faction, ShufflerSingleUse).GetRandomEntry()];
                        }
                        if (nullIfErrorTech)
                            return null;
                        return GetBaseTemplate(SpawnBaseTypes.NotAvail);
                    }
                    else
                    {
                        if (!DebugTAC_AI.NoLogSpawning)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": FilteredSelectFromAll - There's only Prefab Techs available");
                            DebugTAC_AI.Log(KickStart.ModID + ": FilteredSelectFromAll - Indexes Available: ");
                            StringBuilder SB = new StringBuilder();
                            foreach (SpawnBaseTypes val in SBT)
                            {
                                SB.Append(val + ", ");
                            }
                            DebugTAC_AI.Log(SB.ToString());
                        }
                        var toSpawn = SBT.GetRandomEntry();
                        if (!IsBaseTemplateAvailable(toSpawn))
                            DebugTAC_AI.Exception(KickStart.ModID + ": FilteredSelectFromAll - population entry " + toSpawn + " has a null BaseTemplate.  " +
                                "We checked for this earlier! How?");
                        return GetBaseTemplate(toSpawn);
                    }
                }
                else
                {
                    int outcomeExt;
                    if (KickStart.TryForceOnlyPlayerSpawns)
                    {
                        if (!DebugTAC_AI.NoLogSpawning)
                        {
                            DebugTAC_AI.LogSpawn(KickStart.ModID + ": FilteredSelectFromAll - Forced Local Techs spawn possible: true");
                            DebugTAC_AI.Log(KickStart.ModID + ": FilteredSelectFromAll - Indexes Available: ");
                            StringBuilder SB = new StringBuilder();
                            foreach (int val in selectedExt)
                            {
                                SB.Append(val + ", ");
                            }
                            DebugTAC_AI.Log(SB.ToString());
                        }
                        outcomeExt = selectedExt.GetRandomEntry();
                        if (outcomeExt == -1)
                        {
                            if (!DebugTAC_AI.NoLogSpawning)
                                DebugTAC_AI.LogSpawn(KickStart.ModID + ": FilteredSelectFromAll - Could not find any techs!");
                            if (handleFallback)
                            {
                                ShufflerSingleUse.Clear();
                                return ModTechsDatabase.InternalPopTechs[InternalFallbackHandler(filter.Faction, ShufflerSingleUse).GetRandomEntry()];
                            }
                            if (nullIfErrorTech)
                                return null;
                            return GetBaseTemplate(SpawnBaseTypes.NotAvail);
                        }
                        return ModTechsDatabase.ExtPopTechsAllLookup(outcomeExt);
                    }
                    if (PrefabTechs == 0)
                    {
                        if (!DebugTAC_AI.NoLogSpawning)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": FilteredSelectFromAll - There's only Local Techs available");
                            DebugTAC_AI.Log(KickStart.ModID + ": FilteredSelectFromAll - Indexes Available: ");
                            StringBuilder SB = new StringBuilder();
                            foreach (int val in selectedExt)
                            {
                                SB.Append(val + ", ");
                            }
                            DebugTAC_AI.Log(SB.ToString());
                        }
                        return ModTechsDatabase.ExtPopTechsAllLookup(selectedExt.GetRandomEntry());
                    }
                    float RAND = UnityEngine.Random.Range(0, CombinedVal);
                    DebugTAC_AI.LogSpawn(KickStart.ModID + ": FilteredSelectFromAll - Chance to pick local tech " + extTechs + "/" +
                        CombinedVal + ", meaning a " + (((float)extTechs / (float)CombinedVal) * 100f).ToString("0.00") + "% chance.   RAND value " + RAND);
                    if (RAND <= PrefabTechs)
                    {   // Spawn prefab Tech
                        var toSpawn = SBT.GetRandomEntry();
                        if (!IsBaseTemplateAvailable(toSpawn))
                            DebugTAC_AI.Exception(KickStart.ModID + ": FilteredSelectFromAll - population entry " + toSpawn + " has a null BaseTemplate.  " +
                                "We checked for this earlier! How?");
                        return GetBaseTemplate(toSpawn);
                    }
                    else
                    {   // Spawn local Tech
                        outcomeExt = selectedExt.GetRandomEntry();
                        DebugTAC_AI.LogSpawn(KickStart.ModID + ": FilteredSelectFromAll - Spawn local tech");
                        if (outcomeExt == -1)
                        {
                            if (!DebugTAC_AI.NoLogSpawning)
                                DebugTAC_AI.LogSpawn(KickStart.ModID + ": FilteredSelectFromAll - Could not find any techs!");
                            if (handleFallback)
                            {
                                ShufflerSingleUse.Clear();
                                return ModTechsDatabase.InternalPopTechs[InternalFallbackHandler(filter.Faction, ShufflerSingleUse).GetRandomEntry()];
                            }
                            if (nullIfErrorTech)
                                return null;
                            return GetBaseTemplate(SpawnBaseTypes.NotAvail);
                        }
                        return ModTechsDatabase.ExtPopTechsAllLookup(outcomeExt);
                    }
                }
            }
            finally
            {
                SearchSingleUse.Clear();
                ShufflerSingleUse.Clear();
            }
        }


        // player cases - rebuild for bote
        internal static void StripPlayerTechOfBlocks(SpawnBaseTypes techType)
        {
            Tank tech = Singleton.playerTank;
            int playerTeam = tech.Team;
            Vector3 playerPos = tech.transform.position;
            Quaternion playerFacing = tech.transform.rotation;

            RawTech BT = GetBaseTemplate(techType);
            BlockTypes bType = BT.GetFirstBlock();
            TankBlock block = SpawnBlockS(bType, playerPos, playerFacing, out bool worked); 
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": StripPlayerTechOfBlocks - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                return;
            }
            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
            if (effect)
            {
                effect.transform.Spawn(block.centreOfMassWorld);
            }

            tech.visible.RemoveFromGame();

            Tank theTech;
            theTech = TechFromBlock(block, playerTeam, BT.techName);

            Singleton.Manager<ManTechs>.inst.RequestSetPlayerTank(theTech);
        }
        internal static void ReconstructPlayerTech(SpawnBaseTypes techType, SpawnBaseTypes fallbackTechType)
        {
            SpawnBaseTypes toSpawn;
            if (IsBaseTemplateAvailable(techType))
            {
                toSpawn = techType;
            }
            else if (IsBaseTemplateAvailable(fallbackTechType))
            {
                toSpawn = fallbackTechType;
            }
            else
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ReconstructPlayerTech - Failed, could not find main or fallback!");
                return; // compromised - cannot load anything!
            }
            StripPlayerTechOfBlocks(toSpawn);

            RawTech BT = GetBaseTemplate(techType);

            Tank theTech = Singleton.playerTank;

            AIERepair.TurboconstructExt(theTech, BT.savedTech, false);
            DebugTAC_AI.Log(KickStart.ModID + ": ReconstructPlayerTech - Retrofitted player FTUE tech to " + BT.techName);
        }



        // Use this for external code mod cases
        /// <summary>
        /// Spawns a RawTech IMMEDEATELY.  Do NOT Call while calling BlockMan or spawner blocks or the game will break!
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="forwards"></param>
        /// <param name="Blueprint"></param>
        /// <param name="snapTerrain"></param>
        /// <param name="Charged"></param>
        /// <param name="ForceInstant"></param>
        /// <returns></returns>
        public static Tank SpawnTechExternal(Vector3 pos, int Team, Vector3 forwards, RawTechTemplateFast Blueprint, RawTechPopParams filter)
        {
            if (Blueprint == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnTechExternal - Was handed a NULL Blueprint! \n" + StackTraceUtility.ExtractStackTrace());
                return null;
            }
            var baseBlueprint = RawTechBase.JSONToMemoryExternal(Blueprint.Blueprint);
            filter.ForceAnchor = Blueprint.IsAnchored;

            Tank theTech = InstantTech(pos, forwards, Team, Blueprint.Name, baseBlueprint, filter);
            /*
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnTechExternal - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (snapTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }
                Quaternion quat = AIGlobals.LookRot(forwards, Vector3.up);

                BlockTypes bType = RawTechTemplate.JSONToFirstBlock(baseBlueprint);
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked); 
                if (!worked)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": SpawnTechExternal - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }
                var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                if (effect)
                {
                    effect.transform.Spawn(block.centreOfMassWorld);
                }

                theTech = TechFromBlock(block, Team, Blueprint.Name);
                AIERepair.TurboconstructExt(theTech, RawTechTemplate.JSONToMemoryExternal(baseBlueprint), Charged);

                if (ManBaseTeams.IsEnemyBaseTeam(Team) || Team == -1)//enemy
                {
                    var namesav = BookmarkBuilder.Init(theTech, baseBlueprint);
                    namesav.infBlocks = Blueprint.InfBlocks;
                    namesav.faction = Blueprint.Faction;
                    namesav.unprovoked = Blueprint.NonAggressive;
                }
            }*/
            DebugTAC_AI.Log(KickStart.ModID + ": SpawnTechExternal - Spawned " + Blueprint.Name + " at " + pos + ". Snapped to terrain " + filter.SnapTerrain);


            if (Team == -2)//neutral
            {   // be crafty mike and face the player
                theTech.AI.SetBehaviorType(AITreeType.AITypes.FacePlayer);
            }

            return theTech;
        }

        /// <summary>
        /// Spawns a RawTech safely.  There is an update delay on call so it's not immedeate on call.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="forwards"></param>
        /// <param name="Blueprint"></param>
        /// <param name="snapTerrain"></param>
        /// <param name="Charged"></param>
        /// <param name="ForceInstant"></param>
        /// <param name="AfterAction">Assign the action you want given the spawned Tech after it spawns.</param>
        public static void SpawnTechExternalSafe(Vector3 pos, int Team, Vector3 forwards, RawTechTemplateFast Blueprint, RawTechPopParams filter, Action<Tank> AfterAction = null)
        {
            if (Blueprint == null)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnTechExternal - Was handed a NULL Blueprint! \n" + StackTraceUtility.ExtractStackTrace());
                return;
            }
            QueueInstantTech queue;
            queue = new QueueInstantTech(AfterAction, pos, forwards, Team, Blueprint.Name, RawTechBase.JSONToMemoryExternal(Blueprint.Blueprint), filter);
            TechBacklog.Enqueue(queue);
            DebugTAC_AI.Log(KickStart.ModID + ": SpawnTechExternalSafe - Adding to Queue - In Queue: " + TechBacklog.Count);
        }
        public static Tank TechTransformer(Tank tech, List<RawBlockMem> TechBlueprint)
        {
            int team = tech.Team;
            string OGName = tech.name;
            Vector3 techPos = tech.transform.position;
            Quaternion techFacing = tech.transform.rotation;


            Tank theTech = InstantTech(techPos, techFacing * Vector3.forward, team, OGName, TechBlueprint);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Log(KickStart.ModID + ": TechTransformer - Generation failed, falling back to slower, reliable Tech building method");

                BlockTypes bType = TechBlueprint.GetFirstBlock();
                TankBlock block = SpawnBlockS(bType, techPos, techFacing, out bool worked); 
                if (!worked)
                {
                    return tech;
                }
                var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                if (effect)
                {
                    effect.transform.Spawn(block.centreOfMassWorld);
                }

                theTech = TechFromBlock(block, team, OGName);
            }

            tech.visible.RemoveFromGame();

            return theTech;
        }



        // Override
        internal static TankBlock GetPrefabFiltered(BlockTypes type, Vector3 posScene)
        {
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(posScene))
            {
                if (Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type))
                {
                    return Singleton.Manager<ManSpawn>.inst.SpawnBlock(type, posScene, Quaternion.identity);
                }
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": GetPrefabFiltered - Error on block " + type.ToString());
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": GetPrefabFiltered - Error on unfetchable block");
                }
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    DebugTAC_AI.Log(KickStart.ModID + ": GetPrefabFiltered - Could not spawn block!  Block does not exist!");
                else
                    DebugTAC_AI.Log(KickStart.ModID + ": GetPrefabFiltered - Could not spawn block!  Block is invalid in current gamemode!");
            }
            else
            {
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": GetPrefabFiltered - Error on block " + type.ToString());
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": GetPrefabFiltered - Error on unfetchable block");
                }
                DebugTAC_AI.Log(KickStart.ModID + ": GetPrefabFiltered - Could not spawn block!  Block tried to spawn out of bounds!");
            }
            return null;
        }

        internal static TankBlock SpawnBlockS(BlockTypes type, Vector3 position, Quaternion quat, out bool worked)
        {
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(position))
            {
                try
                {
                    if (Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type))
                    {
                        try
                        {
                            worked = true;

                            TankBlock block = Singleton.Manager<ManLooseBlocks>.inst.HostSpawnBlock(type, position, quat, false);
                            if (block == null)
                                throw new NullReferenceException("Expected block of name " +
                                    StringLookup.GetItemName(objectType: ObjectTypes.Block, (int)type) +
                                    " was not found - HostSpawnBlock may have fumbled");
                            var dmg = block.GetComponent<Damageable>();
                            if (dmg)
                            {
                                if (!dmg.IsAtFullHealth)
                                    block.InitNew();
                            }
                            else
                                throw new NullReferenceException("Expected block of name " +
                                    StringLookup.GetItemName(objectType: ObjectTypes.Block, (int)type) +
                                    " has no Damageable.  This should be impossible");
                            return block;
                        }
                        catch (Exception e)
                        {
                            try
                            {
                                throw new Exception(KickStart.ModID + ": SpawnBlockS(IsBlockAllowedInCurrentGameMode) - Error on block " + type, e);
                            }
                            catch (Exception e2)
                            {
                                throw new Exception(KickStart.ModID + ": SpawnBlockS(IsBlockAllowedInCurrentGameMode) - Error on unfetchable block", e2);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        throw new Exception(KickStart.ModID + ": SpawnBlockS(IsBlockAllowedInCurrentGameMode) - Error on block " + type, e);
                    }
                    catch (Exception e2)
                    {
                        throw new Exception(KickStart.ModID + ": SpawnBlockS(IsBlockAllowedInCurrentGameMode) - Error on unfetchable block", e2);
                    }
                }
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    DebugTAC_AI.Log(KickStart.ModID + ": SpawnBlockS - Could not spawn block!  Block does not exist!");
                else
                    DebugTAC_AI.Log(KickStart.ModID + ": SpawnBlockS - Could not spawn block!  Block is invalid in current gamemode!");

            }
            else
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SpawnBlockS - Could not spawn block!  Block tried to spawn out of bounds at " +
                    position.ToString() + "!");
                try
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": SpawnBlockS(CheckIsTileAtPositionLoaded) - Error on block " + type);
                }
                catch (Exception e)
                {
                    throw new Exception(KickStart.ModID + ": SpawnBlockS(CheckIsTileAtPositionLoaded) - Error on unfetchable block", e);
                }
            }
            worked = false;
            return null;
        }

        internal static TankBlock SpawnBlockNoCheck(BlockTypes type, Vector3 position, Quaternion quat)
        {
            TankBlock block = Singleton.Manager<ManLooseBlocks>.inst.HostSpawnBlock(type, position, quat, false);
            var dmg = block.GetComponent<Damageable>();
            if (dmg)
            {
                if (!dmg.IsAtFullHealth)
                    block.InitNew();
            }
            return block;
        }


        private static TechData dataPrefabber;
        private static void CleanupPrefab()
        {
            dataPrefabber.m_SkinMapping.Clear();
            dataPrefabber.m_TechSaveState.Clear();
            dataPrefabber.m_BlockSpecs.Clear();
        }

        internal static Tank TechFromBlock(TankBlock block, int Team, string name)
        {
            Tank theTech;
            if (ManNetwork.IsNetworked)
            {
                theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, name);
                InsureTrackingTank(theTech, false, false);
                return theTech;
            }
            else
            {
                theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, name);
                InsureTrackingTank(theTech, false, false);
            }
            if ((bool)theTech)
                AddToManPopIfLoner(theTech, false);
            return theTech;
        }
        /// <summary>
        /// Spawns tech with a one update delay to prevent overlapping spawns 
        /// </summary>
        internal static void InstantTechSafe(Vector3 pos, Vector3 forward, int Team, string name, List<RawBlockMem> blueprint, RawTechPopParams filter, Action<Tank> fallbackOp = null)
        {
            QueueInstantTech queue = new QueueInstantTech(fallbackOp, pos, forward, Team, name, blueprint, filter);
            TechBacklog.Enqueue(queue);
            DebugTAC_AI.Log(KickStart.ModID + ": InstantTech - Adding to Queue - In Queue: " + TechBacklog.Count);
        }
        internal static Tank InstantTech(Vector3 pos, Vector3 forward, int Team, string name, List<RawBlockMem> mems)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(pos);
            if (!AIGlobals.CanPlaceSafelyInTile(WP.TileCoord, ManWorld.inst.TileManager.GetTileOverlapDirection(WP, 47f)))
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": InstantTech - WARNING - Tech is likely going to spawn OUTSIDE of the loaded terrain, which would cause it to fall out of the world!");
                //return null;
            }
            CleanupPrefab();
            try
            {
                dataPrefabber.Name = name;

                foreach (RawBlockMem mem in mems)
                {
                    BlockTypes type = BlockIndexer.StringToBlockType(mem.t);
                    if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) ||
                            Singleton.Manager<ManSpawn>.inst.IsBlockUsageRestrictedInGameMode(type))
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": InstantTech - Removed " + mem.t + " as it was invalidated");
                        continue;
                    }
                    TankPreset.BlockSpec spec = default;
                    spec.block = mem.t;
                    spec.m_BlockType = type;
                    spec.orthoRotation = new OrthoRotation(mem.r);
                    spec.position = mem.p;
                    spec.saveState = new Dictionary<int, Module.SerialData>();
                    spec.textSerialData = new List<string>();

                    spec.m_SkinID = 0;

                    dataPrefabber.m_BlockSpecs.Add(spec);
                }
                // Stop it from going INTO THE GROUND
                float height = ManWorld.inst.ProjectToGround(pos, true).y;
                if (pos.y < height)
                    pos.y = height;

                Tank theTech = null;
                if (ManNetwork.IsNetworked)
                {
                    uint[] BS = new uint[dataPrefabber.m_BlockSpecs.Count];
                    for (int step = 0; step < dataPrefabber.m_BlockSpecs.Count; step++)
                        BS[step] = Singleton.Manager<ManNetwork>.inst.GetNextHostBlockPoolID();
                    TrackedVisible TV = ManSpawn.inst.SpawnNetworkedTechRef(dataPrefabber, BS, Team,
                        WorldPosition.FromScenePosition(pos).ScenePosition, AIGlobals.LookRot(forward, Vector3.up),
                        null, false, false);
                    if (TV == null)
                    {
                        DebugTAC_AI.FatalError(KickStart.ModID + ": InstantTech(TrackedVisible)[MP] - error on SpawnTank");
                        return null;
                    }
                    if (TV.visible == null)
                    {
                        DebugTAC_AI.FatalError(KickStart.ModID + ": InstantTech(Visible)[MP] - error on SpawnTank");
                        return null;
                    }
                    ManLooseBlocks.inst.RegisterBlockPoolIDsFromTank(TV.visible.tank);
                    theTech = TV.visible.tank;
                }
                else
                {
                    ManSpawn.TankSpawnParams tankSpawn = new ManSpawn.TankSpawnParams
                    {
                        techData = dataPrefabber,
                        blockIDs = null,
                        teamID = Team,
                        position = pos,
                        rotation = AIGlobals.LookRot(forward, Vector3.up),//Singleton.cameraTrans.position - pos
                        ignoreSceneryOnSpawnProjection = true,
                        forceSpawn = true,
                        isPopulation = false,
                        grounded = false,
                    };
                    theTech = Singleton.Manager<ManSpawn>.inst.SpawnTank(tankSpawn, true);
                }
                if (theTech.IsNull())
                {
                    DebugTAC_AI.Exception(KickStart.ModID + ": InstantTech - error on SpawnTank");
                    return null;
                }
                else
                    AddToManPopIfLoner(theTech, false);

                ForceAllBubblesUp(theTech);
                ReconstructConveyorSequencing(theTech);
                AIWiki.ShowTeamInfoFirstTime(Team);

                DebugTAC_AI.LogAISetup(KickStart.ModID + ": InstantTech - Built " + name);

                return theTech;
            }
            finally
            {
                CleanupPrefab();
            }
        }
        internal static Tank InstantTech(Vector3 pos, Vector3 forward, int Team, string name, List<RawBlockMem> mems, RawTechPopParams filter)
        {
            WorldPosition WP = WorldPosition.FromScenePosition(pos);
            if (!AIGlobals.CanPlaceSafelyInTile(WP.TileCoord, ManWorld.inst.TileManager.GetTileOverlapDirection(WP, 47f)))
            {
                DebugTAC_AI.LogError(KickStart.ModID + ": InstantTech - WARNING - Tech is likely going to spawn OUTSIDE of the loaded terrain, which would cause it to fall out of the world!");
                //return null;
            }
            CleanupPrefab();
            try
            {
                dataPrefabber.Name = name;

                bool skinChaotic = false;
                ResetSkinIDSet();
                if (filter.RandSkins)
                {
                    skinChaotic = UnityEngine.Random.Range(0, 100) < 2;
                }
                foreach (RawBlockMem mem in mems)
                {
                    BlockTypes type = BlockIndexer.StringToBlockType(mem.t);
                    if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) ||
                            Singleton.Manager<ManSpawn>.inst.IsBlockUsageRestrictedInGameMode(type))
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": InstantTech - Removed " + mem.t + " as it was invalidated");
                        continue;
                    }
                    TankPreset.BlockSpec spec = default;
                    spec.block = mem.t;
                    spec.m_BlockType = type;
                    spec.orthoRotation = new OrthoRotation(mem.r);
                    spec.position = mem.p;
                    spec.saveState = new Dictionary<int, Module.SerialData>();
                    spec.textSerialData = new List<string>();

                    if (filter.TeamSkins)
                    {
                        FactionSubTypes factType = KickStart.GetCorpExtended(type);
                        FactionSubTypes FST = factType;
                        spec.m_SkinID = GetSkinIDSetForTeam(Team, (int)FST);
                    }
                    else if (filter.RandSkins)
                    {
                        FactionSubTypes factType = KickStart.GetCorpExtended(type);
                        FactionSubTypes FST = factType;
                        if (skinChaotic)
                        {
                            spec.m_SkinID = GetSkinIDRand((int)FST);
                        }
                        else
                        {
                            spec.m_SkinID = GetSkinIDSet((int)FST);
                        }
                    }
                    else
                        spec.m_SkinID = 0;

                    dataPrefabber.m_BlockSpecs.Add(spec);
                }

                switch (filter.Offset)
                {
                    case RawTechOffset.OnGround:
                    case RawTechOffset.RaycastTerrainAndScenery:
                    case RawTechOffset.Exact:
                        // Stop it from going INTO THE GROUND
                        float height = ManWorld.inst.ProjectToGround(pos, true).y;
                        if (pos.y < height)
                            pos.y = height;
                        break;
                    case RawTechOffset.OffGround60Meters:
                        pos.y = ManWorld.inst.ProjectToGround(pos, true).y + 60;
                        break;
                    default:
                        throw new NotImplementedException("filter.positioning is is unknown " + filter.Offset);
                }
                Tank theTech = null;
                if (ManNetwork.IsNetworked)
                {
                    uint[] BS = new uint[dataPrefabber.m_BlockSpecs.Count];
                    for (int step = 0; step < dataPrefabber.m_BlockSpecs.Count; step++)
                        BS[step] = Singleton.Manager<ManNetwork>.inst.GetNextHostBlockPoolID();
                    TrackedVisible TV = ManSpawn.inst.SpawnNetworkedTechRef(dataPrefabber, BS, Team,
                        WorldPosition.FromScenePosition(pos).ScenePosition, AIGlobals.LookRot(forward, Vector3.up), 
                        null, filter.Offset == RawTechOffset.RaycastTerrainAndScenery, filter.IsPopulation);
                    if (TV == null)
                    {
                        DebugTAC_AI.FatalError(KickStart.ModID + ": InstantTech(TrackedVisible)[MP] - error on SpawnTank");
                        return null;
                    }
                    if (TV.visible == null)
                    {
                        DebugTAC_AI.FatalError(KickStart.ModID + ": InstantTech(Visible)[MP] - error on SpawnTank");
                        return null;
                    }
                    ManLooseBlocks.inst.RegisterBlockPoolIDsFromTank(TV.visible.tank);
                    theTech = TV.visible.tank;
                }
                else
                {
                    ManSpawn.TankSpawnParams tankSpawn = new ManSpawn.TankSpawnParams
                    {
                        techData = dataPrefabber,
                        blockIDs = null,
                        teamID = Team,
                        position = pos,
                        rotation = AIGlobals.LookRot(forward, Vector3.up),//Singleton.cameraTrans.position - pos
                        ignoreSceneryOnSpawnProjection = filter.Offset != RawTechOffset.OnGround,
                        forceSpawn = true,
                        isPopulation = filter.IsPopulation
                    };
                    if (filter.ForceAnchor)
                        tankSpawn.grounded = true;
                    else
                        tankSpawn.grounded = filter.Offset == RawTechOffset.OnGround || filter.Offset == RawTechOffset.RaycastTerrainAndScenery;
                    theTech = Singleton.Manager<ManSpawn>.inst.SpawnTank(tankSpawn, true);
                }
                if (theTech.IsNull())
                {
                    DebugTAC_AI.Exception(KickStart.ModID + ": InstantTech - error on SpawnTank");
                    return null;
                }
                else
                    AddToManPopIfLoner(theTech, false);

                ForceAllBubblesUp(theTech);
                ReconstructConveyorSequencing(theTech);
                if (filter.ForceAnchor)
                {
                    theTech.gameObject.AddComponent<RequestAnchored>();
                    theTech.trans.position = theTech.trans.position + new Vector3(0, -0.5f, 0);
                    //theTech.visible.MoveAboveGround();
                }

                DebugTAC_AI.LogAISetup(KickStart.ModID + ": InstantTech - Built " + name);

                return theTech;
            }
            finally
            {
                CleanupPrefab();
            }
        }
        internal static Tank SlowTech(Vector3 pos, Vector3 forward, int Team, string name, RawTech BT, RawTechPopParams filter)
        {
            Tank theTech;
            Vector3 position = pos;
            if (filter.ForceAnchor)
            {
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                position.y = offset;
            }
            Quaternion quat = AIGlobals.LookRot(forward, Vector3.up);

            BlockTypes bType = BT.GetFirstBlock();
            TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": SlowTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED " + StackTraceUtility.ExtractStackTrace().ToString());
                return null;
            }
            var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
            if (effect)
            {
                effect.transform.Spawn(block.centreOfMassWorld);
            }
            ResetSkinIDSet();
            block.SetSkinByUniqueID(GetSkinIDSetForTeam(Team, (int)ManSpawn.inst.GetCorporation(bType)));

            theTech = TechFromBlock(block, Team, name);

            var namesav = BookmarkBuilder.Init(theTech, BT);
            namesav.infBlocks = GetEnemyBaseSupplies(BT);
            namesav.faction = RawTechUtil.CorpExtToCorp(BT.faction);
            namesav.unprovoked = filter.Disarmed;

            ForceAllBubblesUp(theTech);
            ReconstructConveyorSequencing(theTech);
            if (filter.ForceAnchor)
                theTech.gameObject.AddComponent<RequestAnchored>();

            DebugTAC_AI.Log(KickStart.ModID + ": InstantTech - Built " + name);

            return theTech;
        }

        private static List<int> BTs = new List<int>();
        internal static TechData ExportRawTechToTechData(string name, List<RawBlockMem> blueprint, int team, bool reuse, out int[] blockIDs)
        {
            TechData dataPrefabber2;
            if (reuse)
            {
                CleanupPrefab();
                dataPrefabber2 = dataPrefabber;
                dataPrefabber2.Name = name;
            }
            else
            {
                dataPrefabber2 = new TechData
                {
                    Name = name,
                    m_Bounds = new IntVector3(new Vector3(18, 18, 18)),
                    m_SkinMapping = new Dictionary<uint, string>(),
                    m_TechSaveState = new Dictionary<int, TechComponent.SerialData>(),
                    m_CreationData = new TechData.CreationData
                    {
                        m_Creator = "RawTech Import",
                        m_UserProfile = null,
                    },
                    m_BlockSpecs = new List<TankPreset.BlockSpec>()
                };
            }

            bool skinChaotic = UnityEngine.Random.Range(0, 100) < 2;
            bool baseTeamColors = ManBaseTeams.IsEnemyBaseTeam(team);

            ResetSkinIDSet();
            foreach (RawBlockMem mem in blueprint)
            {
                BlockTypes type = BlockIndexer.StringToBlockType(mem.t);

                if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) ||
                        Singleton.Manager<ManSpawn>.inst.IsBlockUsageRestrictedInGameMode(type))
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": InstantTech - Removed " + mem.t + " as it was invalidated");
                    continue;
                }
                if (!BTs.Contains((int)type))
                {
                    BTs.Add((int)type);
                }

                TankPreset.BlockSpec spec = default;
                spec.block = mem.t;
                spec.m_BlockType = type;
                spec.orthoRotation = new OrthoRotation(mem.r);
                spec.position = mem.p;
                spec.saveState = new Dictionary<int, Module.SerialData>();
                spec.textSerialData = new List<string>();
                FactionSubTypes factType = KickStart.GetCorpExtended(type);
                FactionSubTypes FST = factType;
                if (baseTeamColors)
                    spec.m_SkinID = GetSkinIDSetForTeam(team, (int)FST);
                else if (skinChaotic)
                    spec.m_SkinID = GetSkinIDRand((int)FST);
                else
                    spec.m_SkinID = GetSkinIDSet((int)FST);

                dataPrefabber2.m_BlockSpecs.Add(spec);
            }
            //DebugTAC_AI.Log(KickStart.ModID + ": ExportRawTechToTechData - Exported " + name);

            blockIDs = BTs.ToArray();
            BTs.Clear();
            return dataPrefabber2;
        }

        private static readonly Dictionary<int, List<byte>> valid = new Dictionary<int, List<byte>>();
        private static readonly Dictionary<int, byte> valid2 = new Dictionary<int, byte>();
        internal static void ResetSkinIDSet()
        {
            valid.Clear();
            valid2.Clear();
        }
        internal static byte GetSkinIDSet(int faction)
        {
            if (valid2.TryGetValue(faction, out byte num))
            {
                return num;
            }
            else
            {
                try
                {
                    byte pick = GetSkinIDRand(faction);
                    valid2.Add(faction, pick);
                    return pick;
                }
                catch { }// corp has no skins!
            }
            return 0;
        }
        internal static byte GetSkinIDSetForTeam(int team, int faction)
        {
            if (valid2.TryGetValue(faction, out byte num))
            {
                return num;
            }
            else
            {
                try
                {
                    byte pick = GetSkinIDCase(team, faction);
                    valid2.Add(faction, pick);
                    return pick;
                }
                catch { }// corp has no skins!
            }
            return 0;
        }
        private static List<byte> num2 = new List<byte>();
        internal static byte GetSkinIDRand(int faction)
        {
            if (valid.TryGetValue(faction, out List<byte> num))
            {
                return num.GetRandomEntry();
            }
            else
            {
                try
                {
                    num2.Clear();
                    FactionSubTypes FST = (FactionSubTypes)faction;
                    int count = ManCustomSkins.inst.GetNumSkinsInCorp(FST);
                    for (int step = 0; step < count; step++)
                    {
                        byte skin = ManCustomSkins.inst.SkinIndexToID((byte)step, FST);
                        if (!ManDLC.inst.IsSkinLocked(skin, FST))
                        {
                            num2.Add(skin);
                            //DebugTAC_AI.Log("SKINSSSSSS " + ManCustomSkins.inst.GetSkinNameForSnapshot(FST, skin));
                        }
                    }
                    valid.Add(faction, num2);
                    return num2.GetRandomEntry();
                }
                catch { }// corp has no skins!
            }
            return 0;
        }

        internal static byte GetSkinIDCase(int team, int faction)
        {
            if (valid.TryGetValue(faction, out List<byte> num))
            {
                return num[team % num.Count];
            }
            else
            {
                try
                {
                    num2.Clear();
                    FactionSubTypes FST = (FactionSubTypes)faction;
                    int count = ManCustomSkins.inst.GetNumSkinsInCorp(FST);
                    for (int step = 0; step < count; step++)
                    {
                        byte skin = ManCustomSkins.inst.SkinIndexToID((byte)step, FST);
                        if (!ManDLC.inst.IsSkinLocked(skin, FST))
                        {
                            num2.Add(skin);
                        }
                    }
                    valid.Add(faction, num2);
                    return num2[team % num2.Count];
                }
                catch { }// corp has no skins!
            }
            return 0;
        }



        private static readonly FieldInfo manPopManaged = typeof(ManPop).GetField("m_SpawnedTechs", BindingFlags.NonPublic | BindingFlags.Instance);
        /// <summary>
        /// Adds a Tech to the TrackedVisibles list, AKA the map, Tech Manager, ETC
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="ID"></param>
        /// <param name="hide"></param>
        /// <param name="anchored"></param>
        /// <returns></returns>
        internal static TrackedVisible InsureTrackingTank(ManSaveGame.StoredTech tank, int ID, bool hide, bool anchored)
        {
            if (ManNetwork.IsNetworked)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader[stored](MP) - No such tracking function is finished yet - " + tank.m_TechData.Name);
            }
            TrackedVisible tracked = AIGlobals.GetTrackedVisible(ID);
            WorldPosition WP;
            if (tracked != null)
            {   // Already tracked
                //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader - Updating Tracked " + tank.m_TechData.Name);
                WP = AIGlobals.GetWorldPos(tank);
                tracked.RadarType = AIGlobals.DetermineRadarType(ID, WP.ScenePosition, anchored);
            }
            else
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader - Tracking " + tank.m_TechData.Name + " ID " + ID);
                WP = AIGlobals.GetWorldPos(tank);
                tracked = new TrackedVisible(ID, null, ObjectTypes.Vehicle, AIGlobals.DetermineRadarType(ID, WP.ScenePosition, anchored));
                ManVisible.inst.TrackVisible(tracked);
            }
            tracked.SetPos(WP);
            tracked.TeamID = tank.m_TeamID;
            return tracked;
        }
        /// <summary>
        /// Adds a Tech to the TrackedVisibles list, AKA the map, Tech Manager, ETC.  For ACTIVE Techs
        /// </summary>
        /// <param name="tank"></param>
        /// <param name="hide"></param>
        /// <param name="anchored"></param>
        /// <returns></returns>
        internal static TrackedVisible InsureTrackingTank(Tank tank, bool hide, bool anchored)
        {
            if (ManNetwork.IsNetworked)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader(MP) - No such tracking function is finished yet - " + tank.name);
            }
            int ID = tank.visible.ID;
            TrackedVisible tracked = AIGlobals.GetTrackedVisible(ID);
            if (tracked != null)
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader - Updating Tracked " + tank.name);
                tracked.RadarType = AIGlobals.DetermineRadarType(ID, true, anchored);
                return tracked;
            }
            else
            {
                tracked = new TrackedVisible(ID, tank.visible, ObjectTypes.Vehicle, AIGlobals.DetermineRadarType(ID, true, anchored));
                ManVisible.inst.TrackVisible(tracked);
            }
            tracked.SetPos(tank.boundsCentreWorldNoCheck);
            tracked.TeamID = tank.Team;
            //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader - Tracking " + tank.name);
            return tracked;
        }
        internal static void AddToManPopIfLoner(Tank tank, bool hide)
        {
            if (tank.Team == AIGlobals.LonerEnemyTeam) // the wild tech pop number
            {
                List<TrackedVisible> visT = (List<TrackedVisible>)manPopManaged.GetValue(ManPop.inst);
                visT.Add(InsureTrackingTank(tank, hide, tank.IsAnchored));
                //forceInsert.SetValue(ManPop.inst, visT);
                //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader - Added " + tank.name + " into ManPop");
            }
        }
        internal static void RemoveFromManPopIfNotLoner(Tank tank)
        {
            if (tank.Team != AIGlobals.LonerEnemyTeam) // the wild tech pop number
            {
                try
                {
                    TrackedVisible tracked = AIGlobals.GetTrackedVisible(tank.visible.ID);
                    //ManVisible.inst.StopTrackingVisible(tank.visible.ID);
                    List<TrackedVisible> visT = (List<TrackedVisible>)manPopManaged.GetValue(ManPop.inst);
                    visT.Remove(tracked);
                    //forceInsert.SetValue(ManPop.inst, visT);
                    //DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader - Removed " + tank.name + " from ManPop");
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": RawTechLoader - Removal of " + tank.name + " from ManPop failed - " + e);
                }
            }
        }


        // Determination
        public static void TryClearAreaForBase(Vector3 vector3)
        {   //N/A
            // We don't want trees vanishing
            return;
            /*
            int removeCount = 0;
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(vector3, 8, AIGlobals.sceneryBitMask))
            {   // Does not compensate for bases that are 64x64 diagonally!
                if (vis.resdisp.IsNotNull())
                {
                    vis.resdisp.RemoveFromWorld(false);
                    removeCount++;
                }
            }
            DebugTAC_AI.Log(KickStart.ModID + ": removed " + removeCount + " trees around new enemy base setup");*/
        }
        internal static bool GetEnemyBaseSupplies(RawTech toSpawn)
        {
            if (toSpawn.purposes == null)
                return false;
            if (toSpawn.purposes.Contains(BasePurpose.Headquarters))
            {
                return true;
            }
            else if (toSpawn.purposes.Contains(BasePurpose.Harvesting))
            {
                return true;
            }
            return false;
        }

        internal static bool CanBeMiner(EnemyMind mind)
        {
            if (mind.StartedAnchored)
                return false;
            bool can = true;
            if (mind?.Tank && !mind.Tank.name.NullOrEmpty())
            {
                SpawnBaseTypes type = GetEnemyBaseTypeFromName(mind.Tank.name);
                if (type != SpawnBaseTypes.NotAvail)
                {
                    if (ModTechsDatabase.InternalPopTechs.TryGetValue(type, out RawTech val))
                        can = !val.environ;
                }
                else
                {
                    RawTech cand = ModTechsDatabase.ExtPopTechsAllFindByName(mind.Tank.name);
                    if (cand != null)
                        can = cand.environ;
                }
            }
            return can;
        }
        internal static bool ShouldDetonateBoltsNow(EnemyMind mind)
        {
            bool can = false;
            try
            {
                SpawnBaseTypes type = GetEnemyBaseTypeFromName(mind.Tank.name);
                if (type != SpawnBaseTypes.NotAvail)
                {
                    if (ModTechsDatabase.InternalPopTechs.TryGetValue(type, out RawTech val))
                        can = val.deployBoltsASAP;
                }
                else
                {
                    RawTech cand = ModTechsDatabase.ExtPopTechsAllFindByName(mind.Tank.name);
                    if (cand != null)
                        can = cand.deployBoltsASAP;
                }
            }
            catch { }
            return can;
        }
        internal static bool IsBaseTemplateAvailable(SpawnBaseTypes toSpawn)
        {
            if (ModTechsDatabase.InternalPopTechs == null)
                DebugTAC_AI.Exception("IsBaseTemplateAvailable - techBases is null.  This should not be possible.");
            return ModTechsDatabase.InternalPopTechs.ContainsKey(toSpawn);
        }
        internal static RawTech GetBaseTemplate(SpawnBaseTypes toSpawn)
        {
            if (ModTechsDatabase.InternalPopTechs == null)
            {
                DebugTAC_AI.Exception(KickStart.ModID + ": GetBaseTemplate - techBases IS NULL");
                return null;
            }
            if (ModTechsDatabase.InternalPopTechs.TryGetValue(toSpawn, out RawTech baseT))
            {
                if (toSpawn == SpawnBaseTypes.NotAvail)
                    DebugTAC_AI.Assert(KickStart.ModID + ": GetBaseTemplate - Forced to spawn FALLBACK");
                return baseT;
            }
            DebugTAC_AI.Exception(KickStart.ModID + ": GetBaseTemplate - COULD NOT FETCH BaseTemplate FOR ID " + toSpawn + "!");
            return null;
        }


        internal static bool ComparePurposes(RawTechPopParams filter, HashSet<BasePurpose> techPurposes)
        {
            if (techPurposes.Contains(BasePurpose.NotStationary) != filter.Purposes.Contains(BasePurpose.NotStationary))
                return false;
            if (techPurposes.Contains(BasePurpose.NoWeapons) != filter.Purposes.Contains(BasePurpose.NoWeapons))
                return false;
            if (techPurposes.Contains(BasePurpose.Fallback) != filter.Purposes.Contains(BasePurpose.Fallback))
                return false;

            /// Broad REQUIREMENTS
            foreach (BasePurpose purpose in techPurposes)
            {
                switch (purpose)
                {
                    case BasePurpose.Sniper:
                    case BasePurpose.NANI:
                    //case BasePurpose.MPUnsafe:  // also not networked flag - no illegal base in MP
                    // disabled since conveyors are now allowed
                    case BasePurpose.AttractTech:
                        if (!filter.Purposes.Contains(purpose))
                            return false;
                        break;
                }
            }
            /// Specific REQUIREMENTS
            foreach (BasePurpose purpose in filter.Purposes)
            {
                switch (purpose)
                {
                    case BasePurpose.AnyNonHQ:
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        break;
                    case BasePurpose.HarvestingNoHQ:
                        if (techPurposes.Contains(BasePurpose.Headquarters) ||
                            !(techPurposes.Contains(BasePurpose.Harvesting) ||
                            techPurposes.Contains(BasePurpose.HasReceivers)))
                            return false;
                        break;
                    case BasePurpose.Headquarters:
                    case BasePurpose.Autominer:
                    case BasePurpose.Defense:
                    case BasePurpose.Harvesting:
                    case BasePurpose.TechProduction:
                        if (!techPurposes.Contains(purpose))
                            return false;
                        break;
                    case BasePurpose.AttractTech:
                        if (techPurposes.Contains(BasePurpose.NoWeapons) ||
                            techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        break;
                }
            }
            return true;
        }

        private static bool FilterSelectAll(RawTech tech, RawTechPopParams filter)
        {
            if (!ComparePurposes(filter, tech.purposes))
                return false;
            switch (filter.Terrain)
            {
                case BaseTerrain.Any:
                    break;
                case BaseTerrain.AnyNonSea:
                    if (tech.terrain == BaseTerrain.Sea)
                        return false;
                    break;
                case BaseTerrain.Land:
                case BaseTerrain.Sea:
                case BaseTerrain.Air:
                case BaseTerrain.Chopper:
                case BaseTerrain.Space:
                    if (tech.terrain != filter.Terrain)
                        return false;
                    break;
                default:
                    throw new NotImplementedException("Unexpected terra type " + tech);
            }

            if (filter.Progression < tech.factionLim)
                return false;

            if (filter.Faction != FactionSubTypes.NULL)
            {   // Filter by SPECIFIC Corp
                FactionTypesExt FST = (FactionTypesExt)filter.Faction;
                if (ManMods.inst.IsModdedCorp(filter.Faction))
                {
                    if (tech.FactionActual.GetHashCode() != ManMods.inst.FindCorpShortName(filter.Faction).GetHashCode())
                        return false;
                }
                else
                {
                    if (tech.faction != FST || tech.factionLim > filter.Progression)
                        return false;
                }
                if (filter.TargetFactionGrade != 99 && tech.faction != FactionTypesExt.NULL)
                {
                    if (tech.IntendedGrade > filter.TargetFactionGrade)
                        return false;
                }
            }
            else
            {   // Filter out by player grade
                FactionLicense FL = ManLicenses.inst.GetLicense(RawTechUtil.CorpExtToCorp(tech.faction));
                if (FL != null && !FL.HasReachedMaxLevel && tech.IntendedGrade > FL.CurrentLevel)
                    return false;
            }

            //KickStart.DoPopSpawnCostCheck && !KickStart.CommitDeathMode
            if (filter.MaxPrice > 0 && tech.baseCost > filter.MaxPrice)
                return false;

            // prevent laggy techs from entering
            if (filter.SearchAttract && tech.savedTech.Count > AIGlobals.MaxBlockLimitAttract)
                return false;
            return true;
        }


        private static List<SpawnBaseTypes> Shuffler = new List<SpawnBaseTypes>();
        private static List<KeyValuePair<SpawnBaseTypes, RawTech>> canidates = new List<KeyValuePair<SpawnBaseTypes, RawTech>>();
        private static void GetEnemyBaseTypesDebug(RawTechPopParams filter)
        {
            if (!DebugTAC_AI.NoLogSpawning)
            {
                string listAll = "";
                foreach (var item in filter.Purposes)
                {
                    listAll += item + ", ";
                }
                DebugTAC_AI.Log("GetEnemyBaseTypes called with - faction: " + filter.Faction.ToString() +
                    ", bestPlayerFaction: " + filter.Progression.ToString() + ", purposes: " + listAll +
                    "terra: " + filter.Terrain.ToString() + ", searchAttract: " + filter.SearchAttract.ToString() +
                    ", maxGrade: " + filter.TargetFactionGrade.ToString() + ", maxPrice: " + filter.MaxPrice.ToString() +
                    ", subNeutral: " + filter.Disarmed.ToString());
            }
            if (filter.Faction == FactionSubTypes.SPE)
            {
                DebugTAC_AI.Assert("WAIT - WHY THE HECK IS OUR FACTION SPE???");
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter">What to filter by</param>
        /// <param name="cache">the cache to use to store the found types</param>
        /// <returns>The same cache instance with the changes</returns>
        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(RawTechPopParams filter, List<SpawnBaseTypes> cache, bool fallbackHandling)
        {
            GetEnemyBaseTypesDebug(filter);
            if (cache.Any())
                DebugTAC_AI.Exception("cache given had some entries in it!  Clear it out first!");
            try
            {
                // Filters
                //DebugTAC_AI.Log(KickStart.ModID + ": GetEnemyBaseTypes - Fetching with " + faction + " - " + bestPlayerFaction + " - " + terra + " - " + maxGrade + " - " + maxPrice);

                foreach (var item in ModTechsDatabase.InternalPopTechs)
                {
                    if (FilterSelectAll(item.Value, filter))
                        cache.Add(item.Key);
                }
                DebugTAC_AI.LogSpawn("GetEnemyBaseTypes Post-Cull remove NULL: " + cache.Count);
                //DebugTAC_AI.Log(KickStart.ModID + ": GetEnemyBaseTypes - Found " + canidates.Count + " options");
                if (!cache.Any())
                {
                    if (fallbackHandling)
                        return InternalFallbackHandler(filter.Faction, cache);
                    return cache;
                }

                // final list compiling
                cache.Shuffle();

                DebugTAC_AI.LogSpawn("GetEnemyBaseTypes success with: " + cache.Count);
                if (!DebugTAC_AI.NoLogSpawning)
                {
                    foreach (var item in cache)
                    {
                        DebugTAC_AI.Log("- " + item);
                    }
                }
                return cache;
            }
            catch (Exception e)
            {
                DebugTAC_AI.LogSpawn("GetEnemyBaseTypes ERROR: " + e);
            }
            cache.Clear();
            if (fallbackHandling)
                return InternalFallbackHandler(filter.Faction, cache);
            return cache;
        }
        private static List<SpawnBaseTypes> fallback = new List<SpawnBaseTypes> { SpawnBaseTypes.NotAvail };
        internal static List<SpawnBaseTypes> InternalFallbackHandler(FactionSubTypes faction, List<SpawnBaseTypes> cache)
        {
            if (canidates.Any())
                DebugTAC_AI.Exception("Cannot nest InternalFallbackHandler calls!");
            if (cache.Any())
                DebugTAC_AI.Exception("cache given had some entries in it!  Clear it out first!");
            try
            {
                // Filters
                canidates.AddRange(ModTechsDatabase.InternalPopTechs);
                if (faction != FactionSubTypes.NULL)
                {
                    canidates.RemoveAll(x => {
                        return (FactionSubTypes)x.Value.faction != faction ||
                        !x.Value.purposes.Contains(BasePurpose.Fallback) ||
                        (ManNetwork.IsNetworked && x.Value.purposes.Contains(BasePurpose.MPUnsafe));
                    });
                }

                // finally, remove those which are N/A

                if (!canidates.Any())
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": FallbackHandler - COULD NOT FIND FALLBACK FOR " + faction);
                    return FallbackHandlerFailiure();
                }

                // final list compiling
                foreach (KeyValuePair<SpawnBaseTypes, RawTech> pair in canidates)
                    cache.Add(pair.Key);

                cache.Shuffle();

                return cache;
            }
            //catch { } // we resort to legacy
            finally
            {
                canidates.Clear();
            }
        }
        /// <summary>
        /// Only triggered when it absolutely fails. DO NOT EDIT OUTPUT!
        /// </summary>
        /// <param name="faction"></param>
        /// <returns></returns>
        private static List<SpawnBaseTypes> FallbackHandlerFailiure()
        {
            DebugTAC_AI.Assert(KickStart.ModID + ": FallbackHandlerFailiure - FINAL FALLBACK - ErrorTech");
            return fallback;
        }

        private static List<SpawnBaseTypes> ShufflerSingleUse = new List<SpawnBaseTypes>();
        internal static SpawnBaseTypes GetEnemyBaseType(RawTechPopParams filter)
        {
            if (ForceSpawn && !filter.SearchAttract)
                return forcedBaseSpawn;

            try
            {
                try
                {
                    SpawnBaseTypes toSpawn = GetEnemyBaseTypes(filter, ShufflerSingleUse, true).GetRandomEntry();
                    if (!IsBaseTemplateAvailable(toSpawn))
                        DebugTAC_AI.Exception(KickStart.ModID + ": GetEnemyBaseType - population entry " + toSpawn + " has a null BaseTemplate.  How?");
                    return toSpawn;
                }
                finally
                {
                    ShufflerSingleUse.Clear();
                }
            }
            catch (Exception e)
            { DebugTAC_AI.Exception(KickStart.ModID + ": GetEnemyBaseType - Population seach FAILED:\n" + e); } // we resort to legacy
            //DebugTAC_AI.Assert(true, KickStart.ModID + ": GetEnemyBaseType - Population seach FAILED");

            int lowerRANDRange = 1;
            int higherRANDRange = 20;
            if (filter.Faction == FactionSubTypes.GSO)
            {
                lowerRANDRange = 1;
                higherRANDRange = 6;
            }
            else if (filter.Faction == FactionSubTypes.GC)
            {
                lowerRANDRange = 7;
                higherRANDRange = 10;
            }
            else if (filter.Faction == FactionSubTypes.VEN)
            {
                lowerRANDRange = 11;
                higherRANDRange = 14;
            }
            else if (filter.Faction == FactionSubTypes.HE)
            {
                lowerRANDRange = 15;
                higherRANDRange = 20;
            }

            return (SpawnBaseTypes)UnityEngine.Random.Range(lowerRANDRange, higherRANDRange);
        }
        internal static SpawnBaseTypes GetEnemyBaseType(FactionSubTypes faction, FactionLevel lvl, HashSet<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                RawTechPopParams RTF = RawTechPopParams.Default;
                RTF.Purposes = purposes;
                RTF.Faction = faction;
                RTF.Progression = lvl;
                RTF.TargetFactionGrade = maxGrade;
                RTF.SearchAttract = searchAttract;
                RTF.Terrain = terra;
                RTF.MaxPrice = maxPrice;
                RTF.Disarmed = subNeutral;
                return GetEnemyBaseType(RTF);
            }
            catch { }
            DebugTAC_AI.Assert(true, KickStart.ModID + ": GetEnemyBaseType(multiple purposes) - Population seach FAILED");

            return SpawnBaseTypes.NotAvail;
        }

        // External techs
        internal static RawTech GetExtEnemyBaseFromName(string Name)
        {
            try
            {
                /*
                int nameNum = Name.GetHashCode();
                int lookup = ModTechsDatabase.ExtPopTechsAll.FindIndex(delegate (RawTech cand) { return cand.techName.GetHashCode() == nameNum; });
                if (lookup == -1) 
                    return null;
                return ModTechsDatabase.ExtPopTechsAllLookup(lookup);
                */
                return ModTechsDatabase.ExtPopTechsAllFindByName(Name);
            }
            catch
            {
                return null;
            }
        }

        // SpawnBaseTypes (Built-In)
        internal static SpawnBaseTypes GetEnemyBaseTypeFromName(string Name)
        {
            try
            {
                var lookup = ModTechsDatabase.InternalPopTechs.FirstOrDefault(x =>{ return x.Value.techName == Name; });
                if (lookup.Key.Equals(default(KeyValuePair<SpawnBaseTypes, RawTech>))) 
                    return SpawnBaseTypes.NotAvail;
                return lookup.Key;
            }
            catch 
            {
                return SpawnBaseTypes.NotAvail;
            }
        }

        internal static RawTech GetEnemyBaseTypeFromNameFull(string Name)
        {
            SpawnBaseTypes type = GetEnemyBaseTypeFromName(Name);
            if (type == SpawnBaseTypes.NotAvail)
                return GetExtEnemyBaseFromName(Name);
            else
                return GetBaseTemplate(type);
        }

        internal static FactionSubTypes GetMainCorp(SpawnBaseTypes toSpawn)
        {
            return RawTechUtil.CorpExtToCorp(GetBaseTemplate(toSpawn).faction);
        }

        internal static bool IsHQ(SpawnBaseTypes toSpawn)
        {
            if (ModTechsDatabase.InternalPopTechs.TryGetValue(toSpawn, out RawTech baseT))
                return baseT.purposes.Contains(BasePurpose.Headquarters);
            return false;
        }
        internal static bool ContainsPurpose(SpawnBaseTypes toSpawn, BasePurpose purpose)
        {
            if (ModTechsDatabase.InternalPopTechs.TryGetValue(toSpawn, out RawTech baseT))
                return baseT.purposes.Contains(purpose);
            return false;
        }
        private static bool IsRadiusClearOfTechObst(Vector3 pos, float radius)
        {
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, AIGlobals.blockBitMask))
            {
                if (vis.isActive)
                {
                    //if (vis.tank != tank)
                    return false;
                }
            }
            return true;
        }
        internal static bool IsFallback(SpawnBaseTypes type)
        {
            ModTechsDatabase.InternalPopTechs.TryGetValue(type, out RawTech val);
            if (val.purposes.Contains(BasePurpose.Fallback))
                return true;
            DebugTAC_AI.Assert("Failed to find effective Tech, resorting to debug!!!!");
            return false;
        }
        internal static BaseTerrain GetTerrain(Vector3 posScene)
        {
            try
            {
                if (AIEPathing.AboveHeightFromGround(posScene, 25))
                {
                    return BaseTerrain.Air;
                }
                else if (KickStart.isWaterModPresent)
                {
                    if (AIEPathing.AboveTheSea(posScene))
                    {
                        return BaseTerrain.Sea;
                    }
                }
            }
            catch { }
            return BaseTerrain.Land;
        }

        public static bool CanSpawnSafely(SpawnBaseTypes type)
        {
            return !IsBaseTemplateAvailable(type) || IsFallback(type);
        }


        private static HashSet<int> teamsCache = new HashSet<int>();
        internal static int GetEnemyBaseCount()
        {
            int baseCount = 0;
            foreach (var item in Singleton.Manager<ManTechs>.inst.IterateTechs())
            {
                if (!item.IsNeutral() && !ManSpawn.IsPlayerTeam(item.Team) && item.IsAnchored)
                    baseCount++;
            }
            return baseCount;
        }
        internal static int GetEnemyBaseCountSearchRadius(Vector3 pos, float radius)
        {
            teamsCache.Clear();
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, AIGlobals.techBitMask))
            {
                if (vis.tank.IsNotNull())
                {
                    Tank tech = vis.tank;
                    if (!teamsCache.Contains(tech.Team) &&!tech.IsNeutral() && !ManSpawn.IsPlayerTeam(tech.Team) && tech.IsAnchored)
                    {
                        teamsCache.Add(tech.Team);
                    }
                }
            }
            return teamsCache.Count;
        }
        //private static List<Tank> tanksCached = new List<Tank>();
        internal static int GetEnemyBaseCountForTeam(int Team)
        {
            int baseCount = 0; 
            foreach (var tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
            {
                if (tech.IsAnchored && !tech.IsNeutral() && !ManSpawn.IsPlayerTeam(tech.Team) && tech.IsFriendly(Team))
                    baseCount++;
            }
            return baseCount;
        }


        private static void MakeSureCanExistWithBase(Tank tank)
        {
            if (tank.IsPopulation || !tank.IsFriendly(tank.Team) || tank.Team == AIGlobals.LonerEnemyTeam || tank.Team == AIGlobals.DefaultEnemyTeam)
            {
                int set = AIGlobals.GetRandomBaseTeam(true);
                DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " spawned team " + tank.Team + " that fights against themselves, setting to team " + set + " instead");
                tank.SetTeam(set, false);
                RemoveFromManPopIfNotLoner(tank);
            }
        }
        private static int ReassignToExistingEnemyBaseTeam()
        {
            var enemyBaseTeam = ManBaseTeams.GetRandomExistingBaseTeam();
            if (enemyBaseTeam == null)
                return -1;
            return enemyBaseTeam.Team;
        }


        private static List<TankBlock> blocs = new List<TankBlock>();
        private static List<TankBlock> blocs2 = new List<TankBlock>();
        private static List<BlockTypes> types = new List<BlockTypes>();
        private static List<RawBlockMem> mems = new List<RawBlockMem>();
        internal static bool Rebuilding = false;
        internal static void ReconstructConveyorSequencing(Tank tank)
        {
            try
            {
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                    return;
                Rebuilding = true;
                var memory = tank.GetComponent<AIERepair.DesignMemory>();
                if (memory)
                {
                    List<RawBlockMem> mems;
                    foreach (TankBlock chain in tank.blockman.IterateBlocks())
                    {   // intel
                        if (chain.GetComponent<ModuleItemConveyor>())
                        {
                            blocs.Add(chain);
                            if (!types.Contains(chain.BlockType))
                                types.Add(chain.BlockType);
                        }
                    }
                    if (types.Count() == 0)
                        return;
                    mems = memory.ReturnAllPositionsOfMultipleTypes(types);
                    ReconstructConveyorSequencingInternal(tank, mems, types);
                }
                else
                    ReconstructConveyorSequencingNoMem(tank);
                // can't fix - any previous design data was not saved!
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ReconstructConveyorSequencing - error 0");
            }
            finally
            {
                blocs.Clear();
                types.Clear();
                mems.Clear();
                Rebuilding = false;
            }
        }
        private static void ReconstructConveyorSequencingNoMem(Tank tank)
        {
            try
            {
                foreach (TankBlock chain in tank.blockman.IterateBlocks())
                {   // intel
                    if (chain.GetComponent<ModuleItemConveyor>())
                    {
                        RawBlockMem BM = new RawBlockMem
                        {
                            t = chain.name,
                            p = chain.cachedLocalPosition,
                            r = chain.cachedLocalRotation.rot,
                        };
                        mems.Add(BM);
                        blocs.Add(chain);
                        if (!types.Contains(chain.BlockType))
                            types.Add(chain.BlockType);
                    }
                }
                if (types.Count() == 0)
                    return;
                ReconstructConveyorSequencingInternal(tank, mems, types);
                // can't fix - any previous design data was not saved!
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ReconstructConveyorSequencing - error 0");
            }
        }
        private static void ReconstructConveyorSequencingInternal(Tank tank, List<RawBlockMem> memsConvey, List<BlockTypes> types)
        {
            try
            {
                foreach (TankBlock chain in tank.blockman.IterateBlocks())
                {   // intel
                    if (chain.GetComponent<ModuleItemConveyor>())
                    {
                        blocs2.Add(chain);
                        if (!types.Contains(chain.BlockType))
                            types.Add(chain.BlockType);
                    }
                }
                if (memsConvey.Count() == 0)
                    return;

                foreach (TankBlock block in blocs2)
                {   // detach
                    try
                    {
                        if (block.IsAttached)
                            tank.blockman.Detach(block, false, false, false);
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": ReconstructConveyorSequencing - error 1");
                    }
                }

                AIERepair.BulkAdding = true;

                int count = memsConvey.Count;
                for (int stepBloc = 0; stepBloc < blocs2.Count; stepBloc++)
                {
                    for (int step = 0; step < count; step++)
                    {   // reconstruct
                        try
                        {
                            if (blocs2[stepBloc].name == memsConvey[step].t)
                            {
                                TankBlock block = blocs2[stepBloc];
                                if (block == null)
                                    continue;

                                if (!AIERepair.AIBlockAttachRequest(tank, memsConvey[step], block, false))
                                {
                                    //DebugTAC_AI.Log(KickStart.ModID + ": ReconstructConveyorSequencing - error 3");
                                }
                                else
                                {
                                    blocs2[step].damage.AbortSelfDestruct();
                                    blocs2[step].damage.AbortSelfDestruct();
                                    blocs2[step].damage.AbortSelfDestruct();
                                    memsConvey.RemoveAt(step);
                                    count--;
                                    step--;
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": ReconstructConveyorSequencing - error 2");
                        }
                    }
                }
                AIERepair.BulkAdding = false;
                // can't fix - any previous design data was not saved!
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ReconstructConveyorSequencing - error 0");
            }
            finally
            {
                blocs2.Clear();
            }
        }


        internal static FieldInfo charge = typeof(ModuleShieldGenerator).GetField("m_EnergyDeficit", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge2 = typeof(ModuleShieldGenerator).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo charge3 = typeof(ModuleShieldGenerator).GetField("m_Shield", BindingFlags.NonPublic | BindingFlags.Instance);
        private static void ForceAllBubblesUp(Tank tank)
        {
            try
            {
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                    return;

                foreach (ModuleShieldGenerator buubles in tank.blockman.IterateBlockComponents<ModuleShieldGenerator>())
                {   
                    if ((bool)buubles)
                    {
                        charge.SetValue(buubles, 0);
                        charge2.SetValue(buubles, 2);
                        BubbleShield shield = (BubbleShield)charge3.GetValue(buubles);
                        shield.SetTargetScale(buubles.m_Radius);
                    }
                }
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ForceAllBubblesUp - error");
            }
        }
        public static void ChargeAndClean(Tank tank, float fullPercent = 1)
        {
            try
            {
                tank.EnergyRegulator.SetAllStoresAmount(fullPercent);
                ForceAllBubblesUp(tank);
            }
            catch
            {
                DebugTAC_AI.Log(KickStart.ModID + ": ChargeAndClean - error");
            }
        }


        public static int CheapestAutominerPrice(FactionSubTypes FST, FactionLevel lvl)
        {
            try
            {
                RawTechPopParams RTF = RawTechPopParams.Default;
                RTF.Faction = FST;
                RTF.Progression = lvl;
                RTF.Purpose = BasePurpose.Autominer;
                RTF.Terrain = BaseTerrain.Land;
                List<SpawnBaseTypes> types = GetEnemyBaseTypes(RTF, ShufflerSingleUse, false);
                int lowest = 150000;
                RawTech BT;
                foreach (SpawnBaseTypes type in types)
                {
                    BT = GetBaseTemplate(type);
                    int tryThis = BT.baseCost;
                    if (tryThis < lowest)
                    {
                        lowest = tryThis;
                    }
                }
                return lowest;
            }
            finally
            {
                ShufflerSingleUse.Clear();
            }
        }
    }

    /// <summary>
    /// Use to instruct newly spawned Techs that start out as only the root block.
    /// Do NOT use on enemy bases that need to build!!
    /// Register the base in TempManager first then have it spawn as an enemy to auto-set it correctly.
    /// </summary>
    internal class BookmarkBuilder : MonoBehaviour
    {
        public Tank target { get; private set; }
        public RawTech blueprint { get; private set; }
        public bool infBlocks;
        public FactionSubTypes faction;
        public bool unprovoked = false;
        public bool instant = true;
        private BookmarkBuilder(Tank tank, RawTech Blueprint)
        {
            target = tank;
            blueprint = Blueprint;
        }
        internal static BookmarkBuilder Init(Tank tank, RawTech Blueprint)
        {
            if (Blueprint == null)
                throw new NullReferenceException("BookmarkBuilder - Blueprint field cannot be null or empty");
            var helper = tank.GetHelperInsured();
            BookmarkBuilder bookmark = tank.gameObject.AddComponent<BookmarkBuilder>();
            bookmark.target = tank;
            bookmark.blueprint = Blueprint;
            bookmark.HookUp(helper);
            helper.FinishedRepairEvent.Subscribe(bookmark.Finish);
            DebugTAC_AI.Log(KickStart.ModID + ": Tech " + tank.name + " Setup BookmarkBuilder");
            return bookmark;
        }
        internal static bool Exists(Tank tank) => tank.GetComponent<BookmarkBuilder>();
        internal static bool TryGet(Tank tank, out BookmarkBuilder value)
        {
            value = tank.GetComponent<BookmarkBuilder>();
            return value != null;
        }
        internal static bool Remove(Tank tank)
        {
            var BB = tank.GetComponent<BookmarkBuilder>();
            if (BB)
            {
                Destroy(BB);
                return true;
            }
            else
                return false;
        }
        internal void HookUp(TankAIHelper helper)
        {
            helper.AILimitSettings.OverrideForBuilder(true);
            helper.AISetSettings.OverrideForBuilder(true);
            helper.InsureTechMemor("BookmarkBuilder", false);
            helper.TechMemor.SetupForNewTechConstruction(helper, blueprint.savedTech);
        }
        internal void Finish(TankAIHelper helper)
        {
            //DebugTAC_AI.Assert("BookmarkBuilder - Finished building from assignment");
            helper.FinishedRepairEvent.Unsubscribe(Finish);
            Remove(target);
        }
    }

    // For when the spawner is backlogged to prevent corruption
    internal class QueueInstantTech
    {
        internal QueueInstantTech(Action<Tank> endEvent, Vector3 pos, Vector3 forward, int Team, string name, List<RawBlockMem> blueprint, RawTechPopParams filter)
        {
            this.endEvent = endEvent;
            this.name = name;
            this.blueprint = blueprint;
            this.pos = pos;
            this.forward = forward;
            this.Team = Team;
            this.filter = filter;
        }
        readonly int maxAttempts = 30;
        readonly int DelayFrames = 5;
        public int Attempts = 0;
        public Action<Tank> endEvent;
        public string name;
        public List<RawBlockMem> blueprint;
        public Vector3 pos;
        public Vector3 forward;
        public int Team;
        public RawTechPopParams filter;

        internal bool PushSpawn()
        {
            Attempts++;
            if (DelayFrames > Attempts)
                return false; // Delaying...
            if (ManSpawn.inst.IsTechSpawning)
            {
                DebugTAC_AI.Exception(KickStart.ModID + ": QueueInstantTech.PushSpawn: ManSpawn Tech spawning appears to be jammed.  Unable to queue Tech spawn.");
                return false; // Something else is using it!!  Hold off! 
            }
            Tank outcome = RawTechLoader.InstantTech(pos, forward, Team, name, blueprint, filter);
            if ((bool)outcome)
            {
                endEvent.Send(outcome);
                return true;
            }
            if (Attempts > maxAttempts)
                return true; // trash the request
            return false;
        }
    }
    internal class BombSpawnTech
    {
        internal BombSpawnTech(Vector3 pos, Vector3 forward, int Team, RawTech template, bool storeBB, int BB)
        {
            this.pos = pos;
            this.forward = forward;
            this.Team = Team;
            this.BB = BB;
            this.storeBB = storeBB;
            blueprint = template;
            queued.Add(this);
            DBS = ManSpawn.inst.SpawnDeliveryBombNew(pos, DeliveryBombSpawner.ImpactMarkerType.Tech, 1f);
            DBS.BombDeliveredEvent.Subscribe(OnImpact);
        }
        public RawTech blueprint;
        public Vector3 pos;
        public Vector3 forward;
        public int Team;
        public int BB;
        public bool storeBB;
        public DeliveryBombSpawner DBS;
        private static List<BombSpawnTech> queued = new List<BombSpawnTech>();

        public void OnImpact(Vector3 outcome)
        {
            RawTechLoader.SpawnBaseInstant(outcome, forward, Team, blueprint, storeBB, BB - blueprint.startingFunds);
            DBS.BombDeliveredEvent.Unsubscribe(OnImpact);
            queued.Remove(this);
        }
    }

}
