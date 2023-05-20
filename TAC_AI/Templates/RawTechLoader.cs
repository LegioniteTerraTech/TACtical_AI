using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using TAC_AI.AI;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
using TerraTechETCUtil;

namespace TAC_AI.Templates
{
    /// <summary>
    /// Use to instruct newly spawned Techs that start out as only the root block.
    /// Do NOT use on enemy bases that need to build!!
    /// Register the base in TempManager first then have it spawn as an enemy to auto-set it correctly.
    /// </summary>
    internal class BookmarkBuilder : MonoBehaviour
    {
        public string blueprint { get; private set; }
        public bool infBlocks;
        public FactionTypesExt faction;
        public bool unprovoked = false;
        public bool instant = true;
        internal static BookmarkBuilder Init(Tank tank, string Blueprint)
        {
            if (Blueprint.NullOrEmpty())
                throw new NullReferenceException("BookmarkBuilder - Blueprint field cannot be null or empty");
            var help = tank.GetHelperInsured();
            var bookmark = tank.gameObject.GetOrAddComponent<BookmarkBuilder>();
            bookmark.blueprint = Blueprint;
            bookmark.HookUp(help);
            help.FinishedRepairEvent.Subscribe(bookmark.Finish);
            return bookmark;
        }
        internal void HookUp(AIECore.TankAIHelper help)
        {
            help.AutoRepair = true;
            help.AdvancedAI = true;
            help.InsureTechMemor("BookmarkBuilder", false);
            help.TechMemor.SetupForNewTechConstruction(help, blueprint);
        }
        internal void Finish(AIECore.TankAIHelper help)
        {
            DebugTAC_AI.Assert("BookmarkBuilder - Finish");
            help.FinishedRepairEvent.Unsubscribe(Finish);
            Destroy(this);
        }
    }
    internal class RequestAnchored : MonoBehaviour
    {
        sbyte delay = 4;
        private void Update()
        {
            delay--;
            if (delay == 0)
                Destroy(this);
        }
    }

    // For when the spawner is backlogged to prevent corruption
    internal class QueueInstantTech
    {
        internal QueueInstantTech(Action<Tank> endEvent, Vector3 pos, Vector3 forward, int Team, string name, string blueprint, bool grounded, bool ForceAnchor, bool population, bool skins)
        {
            this.endEvent = endEvent;
            this.name = name;
            this.blueprint = blueprint;
            this.pos = pos;
            this.forward = forward;
            this.Team = Team;
            this.grounded = grounded;
            this.ForceAnchor = ForceAnchor;
            this.population = population;
            this.skins = skins;
        }
        readonly int maxAttempts = 30; 
        readonly int DelayFrames = 5;
        public int Attempts = 0;
        public Action<Tank> endEvent;
        public string name;
        public string blueprint;
        public Vector3 pos;
        public Vector3 forward;
        public int Team;
        public bool grounded;
        public bool skins;
        public bool ForceAnchor = false;
        public bool population = false;

        internal bool PushSpawn()
        {
            Attempts++;
            if (DelayFrames > Attempts)
                return false; // Delaying...
            if (ManSpawn.inst.IsTechSpawning)
            {
                DebugTAC_AI.Exception("TACtical_AI: QueueInstantTech.PushSpawn: ManSpawn Tech spawning appears to be jammed.  Unable to queue Tech spawn.");
                return false; // Something else is using it!!  Hold off! 
            }
            Tank outcome = RawTechLoader.InstantTech(pos, forward, Team, name, blueprint, grounded, ForceAnchor, population, skins);
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
        internal BombSpawnTech(Vector3 pos, Vector3 forward, int Team, RawTechTemplate template, bool storeBB, int BB)
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
        public RawTechTemplate blueprint;
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

    public class RawTechLoader : MonoBehaviour
    {
        internal static RawTechLoader inst;

        /// <summary>
        /// FOR CASES INVOLVING FIRST TECH SPAWNS, NOT ADDITIONS TO TEAMS
        /// </summary>
        internal static bool UseFactionSubTypes = false;  // Force FactionSubTypes instead of FactionTypesExt
        static readonly bool ForceSpawn = false;  // Test a specific base
        static readonly SpawnBaseTypes forcedBaseSpawn = SpawnBaseTypes.GSOMidBase;
        private static readonly Queue<QueueInstantTech> TechBacklog = new Queue<QueueInstantTech>();

        public const char turretChar = '⛨';


        public static void Initiate()
        {
            if (!inst)
                inst = new GameObject("EnemyWorldManager").AddComponent<RawTechLoader>();
            CursorChanger.AddNewCursors();
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
        internal static void TryStartBase(Tank tank, AIECore.TankAIHelper thisInst, BasePurpose purpose = BasePurpose.Harvesting)
        {
            if (!KickStart.enablePainMode || !KickStart.AllowEnemiesToStartBases)
                return;
            if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && !Singleton.Manager<ManNetwork>.inst.IsServer)
                return; // no want each client to have enemies spawn in new bases - stacked base incident!

            MakeSureCanExistWithBase(tank);

            if (GetEnemyBaseCountSearchRadius(tank.boundsCentreWorldNoCheck, AIGlobals.StartBaseMinSpacing) >= KickStart.MaxEnemyBaseLimit)
            {
                int teamswatch = ReassignToExistingEnemyBaseTeam();
                if (teamswatch == -1)
                    return;
                tank.SetTeam(teamswatch);
                TryRemoveFromPop(tank);
                return;
            }

            if (GetEnemyBaseCountForTeam(tank.Team) > 0)
                return; // want no base spam on world load

            Vector3 pos = (tank.rootBlockTrans.forward * (thisInst.lastTechExtents + 8)) + tank.boundsCentreWorldNoCheck;

            if (!IsRadiusClearOfTechObst(pos, thisInst.lastTechExtents))
            {   // try behind
                pos = (-tank.rootBlockTrans.forward * (thisInst.lastTechExtents + 8)) + tank.boundsCentreWorldNoCheck;

                if (!IsRadiusClearOfTechObst(pos, thisInst.lastTechExtents))
                    return;
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
            StartBaseAtPosition(tank, pos, tank.Team, purpose, GradeLim);
        }


        /// <summary>
        /// Spawns a LOYAL enemy base 
        /// - this means this shouldn't be called for capture base missions.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        internal static int StartBaseAtPosition(Tank spawnerTank, Vector3 pos, int Team, BasePurpose purpose, int grade = 99)
        {
            TryClearAreaForBase(pos);

            // this shouldn't be able to happen without being the server or being in single player
            bool haveBB;
            switch (purpose)
            {
                case BasePurpose.Headquarters:
                    haveBB = true;
                    try
                    {
                        if (KickStart.DisplayEnemyEvents)
                        {
                            WorldPosition pos2 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);

                            AIGlobals.PopupEnemyInfo("Enemy HQ!", pos2);

                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Enemy HQ!");
                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                        }
                    }
                    catch { }
                    break;
                case BasePurpose.HarvestingNoHQ:
                case BasePurpose.Harvesting:
                case BasePurpose.TechProduction:
                case BasePurpose.AnyNonHQ:
                    haveBB = true;

                    try
                    {
                        if (KickStart.DisplayEnemyEvents)
                        {
                            if (AIGlobals.IsEnemyBaseTeam(spawnerTank.Team))
                            {
                                WorldPosition pos3 = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(spawnerTank.visible);

                                AIGlobals.PopupEnemyInfo("Rival!", pos3);

                                Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Rival Prospector Spotted!");
                                Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                            }
                        }
                    }
                    catch { }
            break;
                default:
                    haveBB = false;
                    break;
            }

            int extraBB; // Extras for new bases
            if (spawnerTank.GetMainCorpExt() == FactionTypesExt.GSO)
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
                float divider = 5 / Singleton.Manager<ManLicenses>.inst.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel;
                extraBB = (int)(extraBB / divider);
            }
            catch { }



            // Are we a defended HQ?
            if (purpose == BasePurpose.Headquarters)
            {   // Summon additional defenses - DO NOT LET THIS RECURSIVELY TRIGGER!!!
                extraBB += StartBaseAtPosition(spawnerTank, pos + (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += StartBaseAtPosition(spawnerTank, pos - (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += StartBaseAtPosition(spawnerTank, pos + (Vector3.right * 64), Team, BasePurpose.Defense);
                extraBB += StartBaseAtPosition(spawnerTank, pos - (Vector3.right * 64), Team, BasePurpose.Defense);
                Singleton.Manager<ManSFX>.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEPayTerminal);
            }

            // Now spawn teh main host
            FactionTypesExt FTE = spawnerTank.GetMainCorpExt();
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

            RawTechTemplate BTemp = null;
            if (ShouldUseCustomTechs(out List<int> valid, spawnerTank.GetMainCorpExt(), lvl, purpose, BT, false, grade))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex == -1)
                {
                    DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(SpawnBaseAtPosition) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                }
                else
                {
                    BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                    //SpawnEnemyTechExtBase(pos, Team, Vector3.forward, BTemp);
                    //return BTemp.startingFunds;
                }
            }
            if (BTemp == null)
            {
                BTemp = GetBaseTemplate(GetEnemyBaseType(FTE, lvl, purpose, BT, maxGrade: grade));
            }

            switch (BT)
            {
                case BaseTerrain.Air: 
                    return SpawnAirBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                case BaseTerrain.Sea: 
                    return SpawnSeaBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                default:
                    return SpawnLandBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
            }
        }
        internal static int StartBaseAtPositionNoFounder(FactionTypesExt FTE, Vector3 pos, int Team, BasePurpose purpose, int grade = 99)
        {
            TryClearAreaForBase(pos);

            // this shouldn't be able to happen without being the server or being in single player
            bool haveBB;
            switch (purpose)
            {
                case BasePurpose.Headquarters:
                    haveBB = true;
                    try
                    {
                        if (KickStart.DisplayEnemyEvents)
                        {
                            AIGlobals.PopupEnemyInfo("Enemy HQ!", WorldPosition.FromScenePosition(pos));

                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Enemy HQ!");
                            Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                        }
                    }
                    catch { }
                    break;
                case BasePurpose.HarvestingNoHQ:
                case BasePurpose.Harvesting:
                case BasePurpose.TechProduction:
                case BasePurpose.AnyNonHQ:
                    haveBB = true;

                    try
                    {
                        if (KickStart.DisplayEnemyEvents)
                        {
                            if (AIGlobals.IsEnemyBaseTeam(Team))
                            {
                                AIGlobals.PopupEnemyInfo("Rival!", WorldPosition.FromScenePosition(pos));

                                Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Rival Prospector Spotted!");
                                Singleton.Manager<UIMPChat>.inst.AddMissionMessage("Protect your terra prospectors!!");
                            }
                        }
                    }
                    catch { }
                    break;
                default:
                    haveBB = false;
                    break;
            }

            int extraBB; // Extras for new bases
            if (FTE == FactionTypesExt.GSO)
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
                float divider = 5 / Singleton.Manager<ManLicenses>.inst.GetLicense(KickStart.CorpExtToCorp(FactionTypesExt.GSO)).CurrentLevel;
                extraBB = (int)(extraBB / divider);
            }
            catch { }



            // Are we a defended HQ?
            if (purpose == BasePurpose.Headquarters)
            {   // Summon additional defenses - DO NOT LET THIS RECURSIVELY TRIGGER!!!
                extraBB += StartBaseAtPositionNoFounder(FTE, pos + (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += StartBaseAtPositionNoFounder(FTE, pos - (Vector3.forward * 64), Team, BasePurpose.Defense);
                extraBB += StartBaseAtPositionNoFounder(FTE, pos + (Vector3.right * 64), Team, BasePurpose.Defense);
                extraBB += StartBaseAtPositionNoFounder(FTE, pos - (Vector3.right * 64), Team, BasePurpose.Defense);
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

            RawTechTemplate BTemp = null;
            if (ShouldUseCustomTechs(out List<int> valid, FTE, lvl, purpose, BT, false, grade))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex == -1)
                {
                    DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs(SpawnBaseAtPosition) - Critical error on call - Expected a Custom Local Tech to exist but found none!");
                }
                else
                {
                    BTemp = TempManager.ExternalEnemyTechsAll[spawnIndex];
                    //SpawnEnemyTechExtBase(pos, Team, Vector3.forward, BTemp);
                    //return BTemp.startingFunds;
                }
            }
            if (BTemp == null)
            {
                BTemp = GetBaseTemplate(GetEnemyBaseType(FTE, lvl, purpose, BT, maxGrade: grade));
            }

            switch (BT)
            {
                case BaseTerrain.Air:
                    return SpawnAirBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                case BaseTerrain.Sea:
                    return SpawnSeaBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
                default:
                    return SpawnLandBase(Vector3.forward, pos, Team, BTemp, haveBB, true, BTemp.startingFunds + extraBB);
            }
        }
        internal static bool SpawnBaseExpansion(Tank spawnerTank, Vector3 pos, int Team, SpawnBaseTypes type)
        {   // All bases are off-set rotated right to prevent the base from being built diagonally
            return SpawnBaseExpansion(spawnerTank, pos, Team, GetBaseTemplate(type));
        }
        internal static bool SpawnBaseExpansion(Tank spawnerTank, Vector3 pos, int Team, RawTechTemplate type)
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


        // Now General Usage
        /// <summary>
        /// Spawn a cab, and then add parts until we reach a certain point
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="Team"></param>
        /// <param name="toSpawn"></param>
        /// <returns></returns>
        internal static int SpawnTechFragment(Vector3 pos, int Team, RawTechTemplate toSpawn)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = toSpawn.savedTech;
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            BlockTypes bType = AIERepair.JSONToFirstBlock(baseBlueprint);
            TankBlock block = SpawnBlockS(bType, pos, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: SpawnLandBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                var namesav = BookmarkBuilder.Init(theTech, baseBlueprint);
                namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
                namesav.faction = toSpawn.faction;
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
        internal static int SpawnBase(Vector3 pos, int Team, RawTechTemplate toSpawn, bool storeBB, int ExtraBB = 0)
        {
            return SpawnBase(pos, Vector3.forward, Team, toSpawn, storeBB, ExtraBB);
        }
        internal static int SpawnBase(Vector3 pos, Vector3 facing, int Team, RawTechTemplate toSpawn, bool storeBB, int ExtraBB = 0)
        {
            if (!AIGlobals.IsBaseTeam(Team))
                DebugTAC_AI.Assert("TACtical_AI: SpawnBase - Unexpected non-base team assigned to base spawn " + Team);
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = toSpawn.savedTech;
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(facing, Vector3.up);

            BlockTypes bType = AIERepair.JSONToFirstBlock(baseBlueprint);
            TankBlock block = SpawnBlockS(bType, pos, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: SpawnBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                theBase = TechFromBlock(block, Team, toSpawn.techName + " ¥¥" + cost);
                theBase.FixupAnchors(true);
                theBase.gameObject.GetOrAddComponent<RequestAnchored>();

                return cost;
            }
            else
            {
                theBase = TechFromBlock(block, Team, toSpawn.techName + " " + turretChar);
                theBase.FixupAnchors(true);
                theBase.gameObject.GetOrAddComponent<RequestAnchored>();

                return toSpawn.baseCost;
            }
        }
        internal static Tank GetSpawnBase(Vector3 pos, Vector3 facing, int Team, RawTechTemplate toSpawn, bool storeBB, int ExtraBB = 0)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = toSpawn.savedTech;
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(facing, Vector3.up);

            BlockTypes bType = AIERepair.JSONToFirstBlock(baseBlueprint);
            TankBlock block = SpawnBlockS(bType, pos, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: GetSpawnBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                theBase = TechFromBlock(block, Team, toSpawn.techName + " ¥¥" + cost);
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
        internal static Tank SpawnBaseInstant(Vector3 pos, Vector3 forwards, int Team, RawTechTemplate toSpawn, bool storeBB, int ExtraBB = 0)
        {
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = toSpawn.savedTech;
            Vector3 position = pos;
            position.y = offset;

            Tank theBase;
            if (storeBB)
                theBase = InstantTech(pos, forwards, Team, toSpawn.techName + " ¥¥" + (toSpawn.startingFunds + ExtraBB), baseBlueprint, true, true, UseTeam: true);
            else
            {
                theBase = InstantTech(pos, forwards, Team, toSpawn.techName + " " + turretChar, baseBlueprint, true, true, UseTeam: true);
            }


            theBase.FixupAnchors(true);
            theBase.gameObject.GetOrAddComponent<RequestAnchored>();
            var namesav = BookmarkBuilder.Init(theBase, baseBlueprint);
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = toSpawn.faction;
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
        private static int SpawnLandBase(Vector3 spawnerForwards, Vector3 pos, int Team, RawTechTemplate toSpawn, bool Starting, bool storeBB, int SpawnBB = 0)
        {
            if (Starting && AIGlobals.StartingBasesAreAirdropped)
            {
                new BombSpawnTech(pos, spawnerForwards, Team, toSpawn, storeBB, SpawnBB);
                return toSpawn.baseCost;
            }
            Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
            string baseBlueprint = toSpawn.savedTech;
            Vector3 position = pos;
            position.y = offset;
            Quaternion quat = Quaternion.LookRotation(spawnerForwards, Vector3.up);

            BlockTypes bType = AIERepair.JSONToFirstBlock(baseBlueprint);
            TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: SpawnLandBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                theBase = TechFromBlock(block, Team, toSpawn.techName + " ¥¥" + SpawnBB);
            else
            {
                theBase = TechFromBlock(block, Team, toSpawn.techName + " " + turretChar);
            }

            theBase.FixupAnchors(true);
            theBase.Anchors.TryAnchorAll(true, true);
            theBase.gameObject.GetOrAddComponent<RequestAnchored>();
            var namesav = BookmarkBuilder.Init(theBase,baseBlueprint);
            namesav.infBlocks = GetEnemyBaseSupplies(toSpawn);
            namesav.faction = toSpawn.faction;
            namesav.unprovoked = false;
            namesav.instant = false;
            return toSpawn.baseCost;
        }
        private static int SpawnSeaBase(Vector3 spawnerForwards, Vector3 pos, int Team, RawTechTemplate toSpawn, bool Starting, bool storeBB, int SpawnBB = 0)
        {   // N/A!!! WIP!!!
            DebugTAC_AI.Log("TACtical_AI: - SpawnSeaBase: There's no sea bases stored in the prefab pool.  Consider suggesting one!");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, Starting, SpawnBB);
            /*
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: SpawnSeaBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
        private static int SpawnAirBase(Vector3 spawnerForwards, Vector3 pos, int Team, RawTechTemplate toSpawn, bool Starting, bool storeBB, int SpawnBB = 0)
        {   // N/A!!! WIP!!!
            DebugTAC_AI.Log("TACtical_AI: - SpawnAirBase: There's no air bases stored in the prefab pool.  Consider suggesting one!");
            return SpawnLandBase(spawnerForwards, pos, Team, toSpawn, storeBB, Starting, SpawnBB);
            /*
            Vector3 position = AIEPathing.ForceOffsetToSea(pos);
            string baseBlueprint = GetBlueprint(toSpawn);
            Quaternion quat = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            TankBlock block = SpawnBlockS(AIERepair.JSONToFirstBlock(baseBlueprint), position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: SpawnAirBase - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
        internal static TechData GetBaseExpansionUnloaded(Vector3 pos, NP_Presence EP, RawTechTemplate BT, out int[] bIDs)
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
        private static TechData GetUnloadedBase(RawTechTemplate BT, int team, bool storeBB, out int[] blocIDs, int SpawnBB = 0)
        {
            string baseBlueprint = BT.savedTech;
            string name;

            if (storeBB)
                name = BT.techName + " ¥¥" + SpawnBB;
            else
            {
                name = BT.techName + " " + turretChar;
            }
            return ExportRawTechToTechData(name, baseBlueprint, team, out blocIDs);
        }
        internal static TechData GetUnloadedTech(SpawnBaseTypes SBT, int team, out int[] blocIDs)
        {
            return GetUnloadedTech(GetBaseTemplate(SBT), team, out blocIDs);
        }
        internal static TechData GetUnloadedTech(RawTechTemplate BT, int team, out int[] blocIDs)
        {
            string baseBlueprint = BT.savedTech;
            string name = BT.techName;
            return ExportRawTechToTechData(name, baseBlueprint, team, out blocIDs);
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
        internal static Tank SpawnRandomTechAtPosHead(Vector3 pos, Vector3 forwards, int Team, FactionTypesExt factionType = FactionTypesExt.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool subNeutral = false, bool snapTerrain = true, int maxGrade = 99, int maxPrice = 0)
        {   // This will try to spawn player-made enemy techs as well
            if (subNeutral)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            if (ShouldUseCustomTechs(out List<int> valid, factionType, lvl, BasePurpose.NotStationary, terrainType, false, maxGrade, maxPrice: maxPrice, subNeutral: subNeutral))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex != -1)
                {
                    return SpawnMobileTechPrefab(pos, forwards, Team, TempManager.ExternalEnemyTechsAll[spawnIndex], subNeutral, snapTerrain);
                }
                DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - Critical error on call - Expected a Custom Local Tech to exist but found none!");
            }
            return SpawnMobileTechPrefab(pos, forwards, Team, GetBaseTemplate(GetEnemyBaseType(factionType, lvl, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, subNeutral: subNeutral, maxPrice: maxPrice)), subNeutral, snapTerrain);
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
        internal static bool SpawnRandomTechAtPosHead(Vector3 pos, Vector3 forwards, int Team, out Tank outTank, FactionTypesExt factionType = FactionTypesExt.NULL, BaseTerrain terrainType = BaseTerrain.Land, bool subNeutral = false, bool snapTerrain = true, int maxGrade = 99, int maxPrice = 0)
        {   // This will try to spawn player-made enemy techs as well

            if (subNeutral)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();

            FactionLevel lvl = TryGetPlayerLicenceLevel();
            if (ShouldUseCustomTechs(out List<int> valid, factionType, lvl, BasePurpose.NotStationary, terrainType, false, maxGrade, subNeutral: subNeutral, maxPrice: maxPrice))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex != -1)
                {
                    outTank = SpawnMobileTechPrefab(pos, forwards, Team, TempManager.ExternalEnemyTechsAll[valid.GetRandomEntry()], subNeutral, snapTerrain);
                    return true;
                }
                DebugTAC_AI.Log("TACtical_AI: SpawnRandomTechAtPosHead - Critical error on call - Expected a Custom Local Tech to exist but found none!");
            }
            SpawnBaseTypes type = GetEnemyBaseType(factionType, lvl, BasePurpose.NotStationary, terrainType, maxGrade: maxGrade, subNeutral: subNeutral, maxPrice: maxPrice);
            if (type == SpawnBaseTypes.NotAvail)
            {
                outTank = null;
                return false;
            }
            outTank = SpawnMobileTechPrefab(pos, forwards, Team, GetBaseTemplate(type), subNeutral, snapTerrain);

            return true;
        }
        
        internal static Tank SpawnMobileTechPrefab(Vector3 pos, Vector3 forwards, int Team, SpawnBaseTypes toSpawn, bool subNeutral = false, bool snapTerrain = true, bool pop = false)
        {
            return SpawnMobileTechPrefab(pos, forwards, Team, GetBaseTemplate(toSpawn), subNeutral, snapTerrain, pop);
        }
        internal static Tank SpawnMobileTechPrefab(Vector3 pos, Vector3 forwards, int Team, RawTechTemplate toSpawn, bool subNeutral = false, bool snapTerrain = true, bool pop = false)
        {
            string baseBlueprint = toSpawn.savedTech;

            Tank theTech = InstantTech(pos, forwards, Team, toSpawn.techName, baseBlueprint, snapTerrain, population: pop);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Log("TACtical_AI: SpawnMobileTech - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (snapTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }
                Quaternion quat = Quaternion.LookRotation(forwards, Vector3.up);

                BlockTypes bType = AIERepair.JSONToFirstBlock(baseBlueprint);
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
                if (!worked)
                {
                    DebugTAC_AI.Log("TACtical_AI: SpawnMobileTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                namesav.faction = toSpawn.faction;
                namesav.unprovoked = subNeutral;
            }

            theTech.FixupAnchors(true);

            return theTech;
        }
        
        internal static bool SpawnAttractTech(Vector3 pos, Vector3 forwards, int Team, BaseTerrain terrainType = BaseTerrain.Land, FactionTypesExt faction = FactionTypesExt.NULL, BasePurpose purpose = BasePurpose.NotStationary)
        {
            RawTechTemplate baseTemplate;
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            if (ShouldUseCustomTechs(out List<int> valid, faction, lvl, BasePurpose.NotStationary, terrainType, true))
            {
                int spawnIndex = valid.GetRandomEntry();
                if (spawnIndex != -1)
                {
                    baseTemplate = TempManager.ExternalEnemyTechsAll[spawnIndex];
                }
                else
                {
                    DebugTAC_AI.Log("TACtical_AI: ASSERT - ShouldUseCustomTechs - Expected a Custom Local tech to exist but found none!");
                    return false;
                }
            }
            else
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseType(faction, lvl, purpose, terrainType, true);
                baseTemplate = GetBaseTemplate(toSpawn);
            }

            Tank theTech = InstantTech(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, terrainType == BaseTerrain.Land, !baseTemplate.purposes.Contains(BasePurpose.NotStationary));
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Assert(true, "TACtical_AI: SpawnAttractTech - Generation failed, falling back to slower, reliable Tech building method");
                SlowTech(pos, forwards, Team, baseTemplate.techName, baseTemplate, terrainType == BaseTerrain.Land, !baseTemplate.purposes.Contains(BasePurpose.NotStationary));
            }

            DebugTAC_AI.Log("TACtical_AI: SpawnAttractTech - Spawned " + baseTemplate.techName);
            return true;

        }
        
        internal static Tank SpawnTechAutoDetermine(Vector3 pos, Vector3 forwards, int Team, RawTechTemplate Blueprint, bool subNeutral = false, bool snapTerrain = true, bool forceInstant = false, bool pop = false, int extraBB = 0)
        {
            string baseBlueprint = Blueprint.savedTech;

            Tank theTech;

            if (subNeutral)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();

            bool MustBeAnchored = !Blueprint.purposes.Contains(BasePurpose.NotStationary);

            if (!forceInstant && MustBeAnchored)
            {
                //theTech = null; //InstantTech does not handle this correctly 
                bool storeBB = Blueprint.purposes.Contains(BasePurpose.Harvesting) || Blueprint.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName + " ¥¥" + (Blueprint.startingFunds + extraBB), baseBlueprint, snapTerrain, MustBeAnchored, pop);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                {
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName, baseBlueprint, snapTerrain, MustBeAnchored, pop);
                }
            }
            else
            {
                if (Blueprint.purposes.Contains(BasePurpose.Defense))
                {
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName + " " + turretChar, baseBlueprint, snapTerrain, MustBeAnchored, pop);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                    theTech = InstantTech(pos, forwards, Team, Blueprint.techName, baseBlueprint, snapTerrain, MustBeAnchored, pop);
            }

            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Assert(true ,"TACtical_AI: SpawnTechExternal - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (snapTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }

                Quaternion quat = Quaternion.LookRotation(forwards, Vector3.up);

                BlockTypes bType = AIERepair.JSONToFirstBlock(baseBlueprint);
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
                if (!worked)
                {
                    DebugTAC_AI.Log("TACtical_AI: SpawnEnemyTechExt - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                    theTech = TechFromBlock(block, Team, Blueprint.techName + " ¥¥" + (Blueprint.startingFunds + extraBB));
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

                var namesav = BookmarkBuilder.Init(theTech, baseBlueprint);
                namesav.infBlocks = false;
                namesav.faction = Blueprint.faction;
                namesav.unprovoked = subNeutral;
            }

            DebugTAC_AI.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.techName);

            return theTech;
        }
        
        internal static bool SpawnSpecificTech(Vector3 pos, Vector3 forwards, int Team, HashSet<BasePurpose> purposes, BaseTerrain terrainType = BaseTerrain.Land, FactionTypesExt faction = FactionTypesExt.NULL, bool subNeutral = false, bool snapTerrain = true, int maxGrade = 99, int maxPrice = 0, bool isPopulation = false)
        {
            RawTechTemplate baseTemplate;
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            if (ShouldUseCustomTechs(faction, purposes, terrainType, true))
            {
                baseTemplate = TempManager.ExternalEnemyTechsAll[GetExternalIndex(faction, lvl, purposes, terrainType, AIGlobals.IsAttract, maxGrade, maxPrice, subNeutral)];
            }
            else
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseType(faction, lvl, purposes, terrainType, AIGlobals.IsAttract, maxGrade, maxPrice, subNeutral);
                baseTemplate = GetBaseTemplate(toSpawn);
            }
            bool MustBeAnchored = !baseTemplate.purposes.Contains(BasePurpose.NotStationary);

            Tank theTech;
            if (subNeutral && isPopulation)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();
            if (MustBeAnchored)
            {
                //theTech = null; //InstantTech does not handle this correctly 
                bool storeBB = baseTemplate.purposes.Contains(BasePurpose.Harvesting) || baseTemplate.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName + " ¥¥" + 5000000, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                {
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation);
                }
            }
            else
            {
                if (baseTemplate.purposes.Contains(BasePurpose.Defense))
                {
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName + " " + turretChar, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation);
                    theTech.gameObject.AddComponent<RequestAnchored>();
                }
                else
                    theTech = InstantTech(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation);
            }

            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Assert(true, "TACtical_AI: SpawnSpecificTypeTech - Generation failed, falling back to slower, reliable Tech building method");
                Vector3 position = pos;
                Quaternion quat = Quaternion.LookRotation(forwards, Vector3.up);

                BlockTypes bType = AIERepair.JSONToFirstBlock(baseTemplate.savedTech);
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
                if (!worked)
                {
                    DebugTAC_AI.Log("TACtical_AI: SpawnSpecificTypeTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                    theTech = TechFromBlock(block, Team, baseTemplate.techName + " ¥¥" + 5);
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

                var namesav = BookmarkBuilder.Init(theTech, baseTemplate.savedTech);
                namesav.infBlocks = GetEnemyBaseSupplies(baseTemplate);
                namesav.faction = baseTemplate.faction;
            }

            if (theTech.IsNotNull())
                DebugTAC_AI.Log("TACtical_AI: SpawnSpecificTypeTech - Spawned " + baseTemplate.techName);
            return true;
        }
        internal static void SpawnSpecificTechSafe(Vector3 pos, Vector3 forwards, int Team, HashSet<BasePurpose> purposes, BaseTerrain terrainType = BaseTerrain.Land, FactionTypesExt faction = FactionTypesExt.NULL, bool subNeutral = false, bool snapTerrain = true, int maxGrade = 99, int maxPrice = 0, bool isPopulation = false, Action<Tank> fallbackOp = null)
        {
            RawTechTemplate baseTemplate;
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            if (ShouldUseCustomTechs(faction, purposes, terrainType, true))
            {
                baseTemplate = TempManager.ExternalEnemyTechsAll[GetExternalIndex(faction, lvl, purposes, terrainType, AIGlobals.IsAttract, maxGrade, maxPrice, subNeutral)];
            }
            else
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseType(faction, lvl, purposes, terrainType, AIGlobals.IsAttract, maxGrade, maxPrice, subNeutral);
                baseTemplate = GetBaseTemplate(toSpawn);
            }
            if (subNeutral && isPopulation)
                Team = AIGlobals.GetRandomSubNeutralBaseTeam();


            bool MustBeAnchored = !baseTemplate.purposes.Contains(BasePurpose.NotStationary);

            if (MustBeAnchored)
            {
                //theTech = null; //InstantTech does not handle this correctly 
                bool storeBB = baseTemplate.purposes.Contains(BasePurpose.Harvesting) || baseTemplate.purposes.Contains(BasePurpose.TechProduction);

                if (storeBB)
                {
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName + " ¥¥" + 5000000, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation, fallbackOp);
                }
                else
                {
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation, fallbackOp);
                }
            }
            else
            {
                if (baseTemplate.purposes.Contains(BasePurpose.Defense))
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName + " " + turretChar, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation, fallbackOp);
                else
                    InstantTechSafe(pos, forwards, Team, baseTemplate.techName, baseTemplate.savedTech, snapTerrain, MustBeAnchored, isPopulation, fallbackOp);
            }
            DebugTAC_AI.Log("TACtical_AI: SpawnSpecificTypeTechSafe - Spawned " + baseTemplate.techName);

        }


        // imported ENEMY cases
        private static readonly List<int> FailedSearch = new List<int> { -1 };
        internal static List<int> GetExternalIndexes(FactionTypesExt faction, FactionLevel bestPlayerFaction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            try
            {   // Filters
                List<RawTechTemplate> canidates;
                //DebugTAC_AI.Log("TACtical_AI: GetExternalIndexes - Fetching with " + faction + " - " + bestPlayerFaction + " - " + terra + " - " + maxGrade + " - " + maxPrice);
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.ExternalEnemyTechsAll.FindAll
                            (delegate (RawTechTemplate cand) { return cand.factionLim <= bestPlayerFaction; });
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        FactionSubTypes FST = KickStart.CorpExtToCorp(faction);
                        canidates = TempManager.ExternalEnemyTechsAll.FindAll
                            (delegate (RawTechTemplate cand) { return KickStart.CorpExtToCorp(cand.faction) == FST; }).FindAll
                            (delegate (RawTechTemplate cand) { return cand.factionLim <= bestPlayerFaction; });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.ExternalEnemyTechsAll.FindAll
                            (delegate (RawTechTemplate cand) { return cand.faction == faction; }).FindAll
                            (delegate (RawTechTemplate cand) { return cand.factionLim <= bestPlayerFaction; });
                    }
                }

                bool multiplayer = ManNetwork.IsNetworked;
                bool cantErad = (!KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= AIGlobals.MaxEradicatorTechs);
                canidates = canidates.FindAll(delegate (RawTechTemplate cand)
                {
                    HashSet<BasePurpose> techPurposes = cand.purposes;
                    return ComparePurposes(new HashSet<BasePurpose> { purpose }, techPurposes, cantErad, subNeutral, searchAttract, multiplayer);
                });

                if (terra == BaseTerrain.AnyNonSea)
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.terrain != BaseTerrain.Sea; });
                }
                else
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.terrain == terra; });
                }

                if (maxGrade != 99)
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.IntendedGrade <= maxGrade; });
                }

                if (maxPrice > 0)
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.startingFunds <= maxPrice; });
                }

                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return RawTechTemplate.JSONToMemoryExternal(cand.savedTech).Count <= AIGlobals.MaxBlockLimitAttract; });
                }

                if (!canidates.Any())
                    return FailedSearch;

                // final list compiling
                List<int> final = new List<int>();
                foreach (RawTechTemplate temp in canidates)
                    final.Add(TempManager.ExternalEnemyTechsAll.IndexOf(temp));

                final.Shuffle();

                return final;
            }
            catch { }

            return FailedSearch;
        }
        internal static List<int> GetExternalIndexes(FactionTypesExt faction, FactionLevel bestPlayerFaction, HashSet<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            try
            {
                // Filters
                //DebugTAC_AI.Log("TACtical_AI: GetExternalIndexes - Fetching with " + faction + " - " + bestPlayerFaction + " - " + terra + " - " + maxGrade + " - " + maxPrice);
                List<RawTechTemplate> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.ExternalEnemyTechsAll.FindAll
                            (delegate (RawTechTemplate cand) { return cand.factionLim <= bestPlayerFaction; });
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        FactionSubTypes FST = KickStart.CorpExtToCorp(faction);
                        canidates = TempManager.ExternalEnemyTechsAll.FindAll
                            (delegate (RawTechTemplate cand) { return KickStart.CorpExtToCorp(cand.faction) == FST; }).FindAll
                            (delegate (RawTechTemplate cand) { return cand.factionLim <= bestPlayerFaction; });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.ExternalEnemyTechsAll.FindAll
                        (delegate (RawTechTemplate cand) { return cand.faction == faction; }).FindAll
                            (delegate (RawTechTemplate cand) { return cand.factionLim <= bestPlayerFaction; });
                    }
                }

                bool multiplayer = ManNetwork.IsNetworked;
                bool cantErad = !KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= AIGlobals.MaxEradicatorTechs;
                canidates = canidates.FindAll(delegate (RawTechTemplate cand)
                {
                    HashSet<BasePurpose> techPurposes = cand.purposes;
                    return ComparePurposes(purposes, techPurposes, cantErad, subNeutral, searchAttract, multiplayer);
                });
                if (terra == BaseTerrain.AnyNonSea)
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.terrain != BaseTerrain.Sea; });
                }
                else
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.terrain == terra; });
                }

                if (maxGrade != 99)
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.IntendedGrade <= maxGrade; });
                }

                if (maxPrice > 0)
                {
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return cand.startingFunds <= maxPrice; });
                }
                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (RawTechTemplate cand) { return RawTechTemplate.JSONToMemoryExternal(cand.savedTech).Count <= AIGlobals.MaxBlockLimitAttract; });
                }

                if (!canidates.Any())
                    return FailedSearch;

                // final list compiling
                List<int> final = new List<int>();
                foreach (RawTechTemplate temp in canidates)
                    final.Add(TempManager.ExternalEnemyTechsAll.IndexOf(temp));

                final.Shuffle();

                return final;
            }
            catch { }

            return FailedSearch;
        }
        
        internal static int GetExternalIndex(FactionTypesExt faction, FactionLevel bestPlayerFaction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            try
            {
                return GetExternalIndexes(faction, bestPlayerFaction, purpose, terra, searchAttract, maxGrade, maxPrice, subNeutral).GetRandomEntry();
            }
            catch { }

            return -1;
        }
        internal static int GetExternalIndex(FactionTypesExt faction, FactionLevel bestPlayerFaction, HashSet<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            try
            {
                return GetExternalIndexes(faction, bestPlayerFaction, purposes, terra, searchAttract, maxGrade, maxPrice, subNeutral).GetRandomEntry();
            }
            catch { }

            return -1;
        }

        internal static bool FindNextBest(out RawTechTemplate nextBest, List<RawTechTemplate> toSearch, int currentPrice)
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

                foreach (var item in (FactionLevel[])Enum.GetValues(typeof(FactionLevel)))
                {
                    FactionSubTypes lvlC = (FactionSubTypes)item;
                    if (Singleton.Manager<ManLicenses>.inst.IsLicenseDiscovered(lvlC))
                    {
                        lvl = item;
                    }
                    else
                        break;
                }
            }
            catch { }
            return lvl;
        }


        internal static bool ShouldUseCustomTechs(out List<int> validIndexes, FactionTypesExt faction, FactionLevel bestPlayerFaction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            bool cacheSubTypes = UseFactionSubTypes;
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            validIndexes = GetExternalIndexes(faction, lvl, purpose, terra, searchAttract, maxGrade, maxPrice, subNeutral);
            int CustomTechs = validIndexes.Count;
            UseFactionSubTypes = cacheSubTypes;
            List<SpawnBaseTypes> SBT = GetEnemyBaseTypes(faction, lvl, purpose, terra, searchAttract, maxGrade, maxPrice, subNeutral);
            UseFactionSubTypes = cacheSubTypes;
            int PrefabTechs = SBT.Count;
             
            if (validIndexes.First() == -1)
                CustomTechs = 0;
            if (SBT.First() == SpawnBaseTypes.NotAvail)
                PrefabTechs = 0;

            int CombinedVal = CustomTechs + PrefabTechs;

            if (KickStart.TryForceOnlyPlayerSpawns)
            {
                if (CustomTechs > 0)
                {
                    /*
                    DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - Forced Local Techs spawn possible: true");
                    DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - Indexes Available: ");
                    StringBuilder SB = new StringBuilder();
                    foreach (int val in validIndexes)
                    {
                        SB.Append(val + ", ");
                    }
                    DebugTAC_AI.Log(SB.ToString()); */
                    return true;
                }
                //else
                //    DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - Forced Player-Made Techs spawn possible: false");
            }
            else
            {
                if (PrefabTechs == 0)
                {
                    if (CustomTechs > 0)
                    {
                    /*
                        DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - There's only Local Techs available");
                        DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - Indexes Available: ");
                        StringBuilder SB = new StringBuilder();
                        foreach (int val in validIndexes)
                        {
                            SB.Append(val + ", ");
                        }
                        DebugTAC_AI.Log(SB.ToString());*/
                        return true;
                    }
                    //else
                    //    DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - No Techs found");
                    return false;
                }
                float RAND = UnityEngine.Random.Range(0, CombinedVal);
                //DebugTAC_AI.Log("TACtical_AI: ShouldUseCustomTechs - Chance " + CustomTechs + "/" + CombinedVal + ", meaning a " + (int)(((float)CustomTechs / (float)CombinedVal) * 100f) + "% chance.   RAND value " + RAND);
                if (RAND > PrefabTechs)
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool ShouldUseCustomTechs(FactionTypesExt faction, HashSet<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            FactionLevel lvl = TryGetPlayerLicenceLevel();
            int CustomTechs = GetExternalIndexes(faction, lvl, purposes, terra, searchAttract, maxGrade, maxPrice, subNeutral).Count;
            int PrefabTechs = GetEnemyBaseTypes(faction, lvl, purposes, terra, searchAttract, maxGrade, maxPrice, subNeutral).Count;

            int CombinedVal = CustomTechs + PrefabTechs;

            float RAND = UnityEngine.Random.Range(0, CombinedVal);
            if (RAND > PrefabTechs)
            {
                return true;
            }
            return false;
        }


        // player cases - rebuild for bote
        internal static void StripPlayerTechOfBlocks(SpawnBaseTypes techType)
        {
            Tank tech = Singleton.playerTank;
            int playerTeam = tech.Team;
            Vector3 playerPos = tech.transform.position;
            Quaternion playerFacing = tech.transform.rotation;

            RawTechTemplate BT = GetBaseTemplate(techType);
            BlockTypes bType = AIERepair.JSONToFirstBlock(BT.savedTech);
            TankBlock block = SpawnBlockS(bType, playerPos, playerFacing, out bool worked); 
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: StripPlayerTechOfBlocks - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
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
                DebugTAC_AI.Log("TACtical_AI: ReconstructPlayerTech - Failed, could not find main or fallback!");
                return; // compromised - cannot load anything!
            }
            StripPlayerTechOfBlocks(toSpawn);

            RawTechTemplate BT = GetBaseTemplate(techType);

            Tank theTech = Singleton.playerTank;

            AIERepair.TurboconstructExt(theTech, RawTechTemplate.JSONToMemoryExternal(BT.savedTech), false);
            DebugTAC_AI.Log("TACtical_AI: ReconstructPlayerTech - Retrofitted player FTUE tech to " + BT.techName);
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
        public static Tank SpawnTechExternal(Vector3 pos, int Team, Vector3 forwards, RawTechTemplateFast Blueprint, bool snapTerrain = false, bool Charged = false, bool randomSkins = false)
        {
            if (Blueprint == null)
            {
                DebugTAC_AI.Log("TACtical_AI: SpawnTechExternal - Was handed a NULL Blueprint! \n" + StackTraceUtility.ExtractStackTrace());
                return null;
            }
            string baseBlueprint = Blueprint.Blueprint;

            Tank theTech = InstantTech(pos, forwards, Team, Blueprint.Name, baseBlueprint, snapTerrain, ForceAnchor: Blueprint.IsAnchored, Team == -1, randomSkins);

            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Log("TACtical_AI: SpawnTechExternal - Generation failed, falling back to slower, reliable Tech building method");

                Vector3 position = pos;
                if (snapTerrain)
                {
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                    position.y = offset;
                }
                Quaternion quat = Quaternion.LookRotation(forwards, Vector3.up);

                BlockTypes bType = AIERepair.JSONToFirstBlock(baseBlueprint);
                TankBlock block = SpawnBlockS(bType, position, quat, out bool worked); 
                if (!worked)
                {
                    DebugTAC_AI.Log("TACtical_AI: SpawnTechExternal - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED");
                    return null;
                }
                var effect = ManSpawn.inst.GetCustomSpawnEffectPrefabs(ManSpawn.CustomSpawnEffectType.Smoke);
                if (effect)
                {
                    effect.transform.Spawn(block.centreOfMassWorld);
                }

                theTech = TechFromBlock(block, Team, Blueprint.Name);
                AIERepair.TurboconstructExt(theTech, RawTechTemplate.JSONToMemoryExternal(baseBlueprint), Charged);

                if (AIGlobals.IsEnemyBaseTeam(Team) || Team == -1)//enemy
                {
                    var namesav = BookmarkBuilder.Init(theTech, baseBlueprint);
                    namesav.infBlocks = Blueprint.InfBlocks;
                    namesav.faction = Blueprint.Faction;
                    namesav.unprovoked = Blueprint.NonAggressive;
                }
            }
                DebugTAC_AI.Log("TACtical_AI: SpawnTechExternal - Spawned " + Blueprint.Name + " at " + pos + ". Snapped to terrain " + snapTerrain);


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
        public static void SpawnTechExternalSafe(Vector3 pos, int Team, Vector3 forwards, RawTechTemplateFast Blueprint, bool snapTerrain = false, bool randomSkins = false, Action<Tank> AfterAction = null)
        {
            if (Blueprint == null)
            {
                DebugTAC_AI.Log("TACtical_AI: SpawnTechExternal - Was handed a NULL Blueprint! \n" + StackTraceUtility.ExtractStackTrace());
                return;
            }
            QueueInstantTech queue;
            queue = new QueueInstantTech(AfterAction, pos, forwards, Team, Blueprint.Name, Blueprint.Blueprint, snapTerrain, Blueprint.IsAnchored, Team == -1, randomSkins);
            TechBacklog.Enqueue(queue);
            DebugTAC_AI.Log("TACtical_AI: SpawnTechExternalSafe - Adding to Queue - In Queue: " + TechBacklog.Count);
        }
        public static Tank TechTransformer(Tank tech, string JSONTechBlueprint)
        {
            int team = tech.Team;
            string OGName = tech.name;
            Vector3 techPos = tech.transform.position;
            Quaternion techFacing = tech.transform.rotation;


            Tank theTech = InstantTech(techPos, techFacing * Vector3.forward, team, OGName, JSONTechBlueprint, false);
            if (theTech.IsNull())
            {   // Generate via the failsafe method
                DebugTAC_AI.Log("TACtical_AI: TechTransformer - Generation failed, falling back to slower, reliable Tech building method");

                BlockTypes bType = AIERepair.JSONToFirstBlock(JSONTechBlueprint);
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
                    DebugTAC_AI.Log("TACtical AI: GetPrefabFiltered - Error on block " + type.ToString());
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical AI: GetPrefabFiltered - Error on unfetchable block");
                }
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    DebugTAC_AI.Log("TACtical AI: GetPrefabFiltered - Could not spawn block!  Block does not exist!");
                else
                    DebugTAC_AI.Log("TACtical AI: GetPrefabFiltered - Could not spawn block!  Block is invalid in current gamemode!");
            }
            else
            {
                try
                {
                    DebugTAC_AI.Log("TACtical AI: GetPrefabFiltered - Error on block " + type.ToString());
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical AI: GetPrefabFiltered - Error on unfetchable block");
                }
                DebugTAC_AI.Log("TACtical AI: GetPrefabFiltered - Could not spawn block!  Block tried to spawn out of bounds!");
            }
            return null;
        }

        internal static TankBlock SpawnBlockS(BlockTypes type, Vector3 position, Quaternion quat, out bool worked)
        {
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(position))
            {
                if (Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type))
                {
                    worked = true;

                    TankBlock block = Singleton.Manager<ManLooseBlocks>.inst.HostSpawnBlock(type, position, quat, false);
                    var dmg = block.GetComponent<Damageable>();
                    if (dmg)
                    {
                        if (!dmg.IsAtFullHealth)
                            block.InitNew();
                    }
                    return block;
                }
                try
                {
                    DebugTAC_AI.Log("TACtical AI: SpawnBlockS - Error on block " + type.ToString());
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical AI: SpawnBlockS - Error on unfetchable block");
                }
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    DebugTAC_AI.Log("TACtical AI: SpawnBlockS - Could not spawn block!  Block does not exist!");
                else
                    DebugTAC_AI.Log("TACtical AI: SpawnBlockS - Could not spawn block!  Block is invalid in current gamemode!");

            }
            else
            {
                try
                {
                    DebugTAC_AI.Log("TACtical AI: SpawnBlockS - Error on block " + type.ToString());
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical AI: SpawnBlockS - Error on unfetchable block");
                }
                DebugTAC_AI.Log("TACtical AI: SpawnBlockS - Could not spawn block!  Block tried to spawn out of bounds!");
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



        internal static Tank TechFromBlock(TankBlock block, int Team, string name)
        {
            Tank theTech;
            if (ManNetwork.IsNetworked)
            {
                theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, name);
                TrackTank(theTech);
                return theTech;
            }
            else
            {
                theTech = Singleton.Manager<ManSpawn>.inst.WrapSingleBlock(null, block, Team, name);
                TrackTank(theTech);
            }
            if ((bool)theTech)
                TryForceIntoPop(theTech);
            return theTech;
        }
        internal static void InstantTechSafe(Vector3 pos, Vector3 forward, int Team, string name, string blueprint, bool grounded, bool ForceAnchor = false, bool population = false, Action<Tank> fallbackOp = null, bool randomSkins = true)
        {
            QueueInstantTech queue = new QueueInstantTech(fallbackOp, pos, forward, Team, name, blueprint, grounded, ForceAnchor, population, randomSkins);
            TechBacklog.Enqueue(queue);
            DebugTAC_AI.Log("TACtical_AI: InstantTech - Adding to Queue - In Queue: " + TechBacklog.Count);
        }
        internal static Tank InstantTech(Vector3 pos, Vector3 forward, int Team, string name, string blueprint, bool grounded, bool ForceAnchor = false, bool population = false, bool randomSkins = true, bool UseTeam = false)
        {
            TechData data = new TechData
            {
                Name = name,
                m_Bounds = new IntVector3(new Vector3(18, 18, 18)),
                m_SkinMapping = new Dictionary<uint, string>(),
                m_TechSaveState = new Dictionary<int, TechComponent.SerialData>(),
                m_CreationData = new TechData.CreationData(),
                m_BlockSpecs = new List<TankPreset.BlockSpec>()
            };
            List<RawBlockMem> mems = RawTechTemplate.JSONToMemoryExternal(blueprint);

            bool skinChaotic = false;
            ResetSkinIDSet();
            if (randomSkins)
            {
                skinChaotic = UnityEngine.Random.Range(0, 100) < 2;
            }
            foreach (RawBlockMem mem in mems)
            {
                BlockTypes type = BlockIndexer.StringToBlockType(mem.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) ||
                        Singleton.Manager<ManSpawn>.inst.IsBlockUsageRestrictedInGameMode(type))
                {
                    DebugTAC_AI.Log("TACtical_AI: InstantTech - Removed " + mem.t + " as it was invalidated");
                    continue;
                }
                TankPreset.BlockSpec spec = default;
                spec.block = mem.t;
                spec.m_BlockType = type;
                spec.orthoRotation = new OrthoRotation(mem.r);
                spec.position = mem.p;
                spec.saveState = new Dictionary<int, Module.SerialData>();
                spec.textSerialData = new List<string>();

                if (UseTeam)
                {
                    FactionTypesExt factType = KickStart.GetCorpExtended(type);
                    FactionSubTypes FST = KickStart.CorpExtToCorp(factType);
                    spec.m_SkinID = GetSkinIDSetForTeam(Team, (int)FST);
                }
                else if (randomSkins)
                {
                    FactionTypesExt factType = KickStart.GetCorpExtended(type);
                    FactionSubTypes FST = KickStart.CorpExtToCorp(factType);
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

                data.m_BlockSpecs.Add(spec);
            }

            Tank theTech = null;
            if (ManNetwork.IsNetworked)
            {
                uint[] BS = new uint[data.m_BlockSpecs.Count];
                for (int step = 0; step < data.m_BlockSpecs.Count; step++)
                {
                    BS[step] = Singleton.Manager<ManNetwork>.inst.GetNextHostBlockPoolID();
                }
                TrackedVisible TV = ManNetwork.inst.SpawnNetworkedNonPlayerTech(data, BS, 
                    WorldPosition.FromScenePosition(pos).ScenePosition, 
                    Quaternion.LookRotation(forward, Vector3.up), grounded);
                if (TV == null)
                {
                    DebugTAC_AI.FatalError("TACtical_AI: InstantTech(TrackedVisible)[MP] - error on SpawnTank");
                    return null;
                }
                if (TV.visible == null)
                {
                    DebugTAC_AI.FatalError("TACtical_AI: InstantTech(Visible)[MP] - error on SpawnTank");
                    return null;
                }
                theTech = TV.visible.tank;
            }
            else
            {
                ManSpawn.TankSpawnParams tankSpawn = new ManSpawn.TankSpawnParams
                {
                    techData = data,
                    blockIDs = null,
                    teamID = Team,
                    position = pos,
                    rotation = Quaternion.LookRotation(forward, Vector3.up),//Singleton.cameraTrans.position - pos
                    ignoreSceneryOnSpawnProjection = false,
                    forceSpawn = true,
                    isPopulation = population
                };
                if (ForceAnchor)
                    tankSpawn.grounded = true;
                else
                    tankSpawn.grounded = grounded;
                theTech = Singleton.Manager<ManSpawn>.inst.SpawnTank(tankSpawn, true);
            }
            if (theTech.IsNull())
            {
                DebugTAC_AI.Exception("TACtical_AI: InstantTech - error on SpawnTank");
                return null;
            }
            else
                TryForceIntoPop(theTech);

            ForceAllBubblesUp(theTech);
            ReconstructConveyorSequencing(theTech);
            if (ForceAnchor)
            {
                theTech.gameObject.AddComponent<RequestAnchored>();
                theTech.trans.position = theTech.trans.position + new Vector3(0, -0.5f, 0);
                //theTech.visible.MoveAboveGround();
            }

            DebugTAC_AI.Log("TACtical_AI: InstantTech - Built " + name);

            return theTech;
        }
        internal static Tank SlowTech(Vector3 pos, Vector3 forward, int Team, string name, RawTechTemplate BT, bool grounded, bool ForceAnchor = false, bool subNeutral = false)
        {
            Tank theTech;
            Vector3 position = pos;
            if (grounded)
            {
                Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out float offset);
                position.y = offset;
            }
            Quaternion quat = Quaternion.LookRotation(forward, Vector3.up);

            BlockTypes bType = AIERepair.JSONToFirstBlock(BT.savedTech);
            TankBlock block = SpawnBlockS(bType, position, quat, out bool worked);
            if (!worked)
            {
                DebugTAC_AI.Log("TACtical_AI: SlowTech - FAILIURE TO SPAWN TECH!!!  FIRST BLOCK WAS NULL OR TILE NOT LOADED " + StackTraceUtility.ExtractStackTrace().ToString());
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

            var namesav = BookmarkBuilder.Init(theTech, BT.savedTech);
            namesav.infBlocks = GetEnemyBaseSupplies(BT);
            namesav.faction = BT.faction;
            namesav.unprovoked = subNeutral;

            ForceAllBubblesUp(theTech);
            ReconstructConveyorSequencing(theTech);
            if (ForceAnchor)
                theTech.gameObject.AddComponent<RequestAnchored>();

            DebugTAC_AI.Log("TACtical_AI: InstantTech - Built " + name);

            return theTech;
        }


        private static List<int> BTs = new List<int>();
        internal static TechData ExportRawTechToTechData(string name, string blueprint, int team, out int[] blockIDs)
        {
            TechData data = new TechData
            {
                Name = name,
                m_Bounds = new IntVector3(new Vector3(18, 18, 18)),
                m_SkinMapping = new Dictionary<uint, string>(),
                m_TechSaveState = new Dictionary<int, TechComponent.SerialData>(),
                m_CreationData = new TechData.CreationData(),
                m_BlockSpecs = new List<TankPreset.BlockSpec>()
            };
            List<RawBlockMem> mems = RawTechTemplate.JSONToMemoryExternal(blueprint);

            bool skinChaotic = UnityEngine.Random.Range(0, 100) < 2;
            bool baseTeamColors = AIGlobals.IsEnemyBaseTeam(team);

            ResetSkinIDSet();
            foreach (RawBlockMem mem in mems)
            {
                BlockTypes type = BlockIndexer.StringToBlockType(mem.t);
                if (!Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type) ||
                        Singleton.Manager<ManSpawn>.inst.IsBlockUsageRestrictedInGameMode(type))
                {
                    DebugTAC_AI.Log("TACtical_AI: InstantTech - Removed " + mem.t + " as it was invalidated");
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
                FactionTypesExt factType = KickStart.GetCorpExtended(type);
                FactionSubTypes FST = KickStart.CorpExtToCorp(factType);
                if (baseTeamColors)
                    spec.m_SkinID = GetSkinIDSetForTeam(team, (int)FST);
                else if (skinChaotic)
                    spec.m_SkinID = GetSkinIDRand((int)FST);
                else
                    spec.m_SkinID = GetSkinIDSet((int)FST);

                data.m_BlockSpecs.Add(spec);
            }
            //DebugTAC_AI.Log("TACtical_AI: ExportRawTechToTechData - Exported " + name);

            blockIDs = BTs.ToArray();
            BTs.Clear();
            return data;
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



        private static readonly FieldInfo forceInsert = typeof(ManPop).GetField("m_SpawnedTechs", BindingFlags.NonPublic | BindingFlags.Instance);
        private static WorldPosition GetWorldPos(ManSaveGame.StoredTech tank)
        {
            if (tank.m_WorldPosition == default)
                return WorldPosition.FromScenePosition(tank.GetBackwardsCompatiblePosition());
            return tank.m_WorldPosition;
        }
        internal static TrackedVisible TrackTank(ManSaveGame.StoredTech tank, int ID, bool anchored = false)
        {
            if (ManNetwork.IsNetworked)
            {
                //DebugTAC_AI.Log("TACtical_AI: RawTechLoader[stored](MP) - No such tracking function is finished yet - " + tank.m_TechData.Name);
            }
            TrackedVisible tracked = ManVisible.inst.GetTrackedVisible(ID);
            if (tracked != null)
            {
                tracked.RadarType = anchored ? RadarTypes.Base : RadarTypes.Vehicle;
                //DebugTAC_AI.Log("TACtical_AI: RawTechLoader - Updating Tracked " + tank.m_TechData.Name);
                tracked.SetPos(GetWorldPos(tank));
                return tracked;
            }

            tracked = new TrackedVisible(ID, null, ObjectTypes.Vehicle, anchored ? RadarTypes.Base : RadarTypes.Vehicle);
            tracked.SetPos(GetWorldPos(tank));
            tracked.TeamID = tank.m_TeamID;
            ManVisible.inst.TrackVisible(tracked);
            //DebugTAC_AI.Log("TACtical_AI: RawTechLoader - Tracking " + tank.m_TechData.Name + " ID " + ID);
            return tracked;
        }
        internal static TrackedVisible TrackTank(Tank tank, bool anchored = false)
        {
            if (ManNetwork.IsNetworked)
            {
                //DebugTAC_AI.Log("TACtical_AI: RawTechLoader(MP) - No such tracking function is finished yet - " + tank.name);
            }
            TrackedVisible tracked = ManVisible.inst.GetTrackedVisible(tank.visible.ID);
            if (tracked != null)
            {
                //DebugTAC_AI.Log("TACtical_AI: RawTechLoader - Updating Tracked " + tank.name);
                tracked.SetPos(tank.boundsCentreWorldNoCheck);
                return tracked;
            }

            tracked = new TrackedVisible(tank.visible.ID, tank.visible, ObjectTypes.Vehicle, anchored ? RadarTypes.Base : RadarTypes.Vehicle);
            tracked.SetPos(tank.boundsCentreWorldNoCheck);
            tracked.TeamID = tank.Team;
            ManVisible.inst.TrackVisible(tracked);
            //DebugTAC_AI.Log("TACtical_AI: RawTechLoader - Tracking " + tank.name);
            return tracked;
        }
        internal static void TryForceIntoPop(Tank tank)
        {
            if (tank.Team == -1) // the wild tech pop number
            {
                List<TrackedVisible> visT = (List<TrackedVisible>)forceInsert.GetValue(ManPop.inst);
                visT.Add(TrackTank(tank, tank.IsAnchored));
                forceInsert.SetValue(ManPop.inst, visT);
                //DebugTAC_AI.Log("TACtical_AI: RawTechLoader - Forced " + tank.name + " into population");
            }
        }
        internal static void TryRemoveFromPop(Tank tank)
        {
            if (tank.Team != -1) // the wild tech pop number
            {
                try
                {
                    TrackedVisible tracked = ManVisible.inst.GetTrackedVisible(tank.visible.ID);
                    //ManVisible.inst.StopTrackingVisible(tank.visible.ID);
                    List<TrackedVisible> visT = (List<TrackedVisible>)forceInsert.GetValue(ManPop.inst);
                    visT.Remove(tracked);
                    forceInsert.SetValue(ManPop.inst, visT);
                    DebugTAC_AI.Log("TACtical_AI: RawTechLoader - Removed " + tank.name + " from population");
                }
                catch { }
            }
        }


        // Determination
        public static void TryClearAreaForBase(Vector3 vector3)
        {   //N/A
            // We don't want trees vanishing
            return;
            /*
            int removeCount = 0;
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(vector3, 8, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery })))
            {   // Does not compensate for bases that are 64x64 diagonally!
                if (vis.resdisp.IsNotNull())
                {
                    vis.resdisp.RemoveFromWorld(false);
                    removeCount++;
                }
            }
            DebugTAC_AI.Log("TACtical_AI: removed " + removeCount + " trees around new enemy base setup");*/
        }
        internal static bool GetEnemyBaseSupplies(RawTechTemplate toSpawn)
        {
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
                    if (TempManager.techBases.TryGetValue(type, out RawTechTemplate val))
                        can = !val.environ;
                }
                else if (TempManager.ExternalEnemyTechsAll.Exists(delegate (RawTechTemplate cand) { return cand.techName == mind.Tank.name; }))
                {
                    can = !TempManager.ExternalEnemyTechsAll.Find(delegate (RawTechTemplate cand) { return cand.techName == mind.Tank.name; }).environ;
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
                    if (TempManager.techBases.TryGetValue(type, out RawTechTemplate val))
                        can = val.deployBoltsASAP;
                }
                else if (TempManager.ExternalEnemyTechsAll.Exists(delegate (RawTechTemplate cand) { return cand.techName == mind.Tank.name; }))
                {
                    can = TempManager.ExternalEnemyTechsAll.Find(delegate (RawTechTemplate cand) { return cand.techName == mind.Tank.name; }).deployBoltsASAP;
                }
            }
            catch { }
            return can;
        }
        internal static bool IsBaseTemplateAvailable(SpawnBaseTypes toSpawn)
        {
            if (TempManager.techBases == null)
                DebugTAC_AI.Exception("IsBaseTemplateAvailable - techBases is null.  This should not be possible.");
            return TempManager.techBases.TryGetValue(toSpawn, out _);
        }
        internal static RawTechTemplate GetBaseTemplate(SpawnBaseTypes toSpawn)
        {
            if (TempManager.techBases == null)
            {
                DebugTAC_AI.Exception("TACtical_AI: GetBaseTemplate - techBases IS NULL");
                return null;
            }
            if (TempManager.techBases.TryGetValue(toSpawn, out RawTechTemplate baseT))
            {
                return baseT;
            }
            DebugTAC_AI.Exception("TACtical_AI: GetBaseTemplate - COULD NOT FETCH BaseTemplate FOR ID " + toSpawn + "!");
            return null;
        }


        internal static bool ComparePurposes(HashSet<BasePurpose> purposes, HashSet<BasePurpose> techPurposes, bool cantErad, bool subNeutral, bool searchAttract, bool MPMode)
        {
            if (MPMode && techPurposes.Contains(BasePurpose.MPUnsafe))
            {   // no illegal base in MP
                return false;
            }
            if (!AIGlobals.AllowInfAutominers && techPurposes.Contains(BasePurpose.Autominer))
            {   // no inf mining
                return false;
            }

            if (techPurposes.Count == 0)
                return false;

            bool mobile = techPurposes.Contains(BasePurpose.NotStationary);

            if (!searchAttract && techPurposes.Contains(BasePurpose.AttractTech))
                return false;
            if (techPurposes.Contains(BasePurpose.NoWeapons))
            {
                if (searchAttract)
                    return false;
                if (subNeutral)
                    return true;
            }
            if (cantErad && techPurposes.Contains(BasePurpose.NANI))
                return false;
            if (!KickStart.AllowSniperSpawns && techPurposes.Contains(BasePurpose.Sniper))
                return false;

            if (mobile != purposes.Contains(BasePurpose.NotStationary))
                return false;

            bool valid = true;
            foreach (BasePurpose purpose in purposes)
            {
                switch (purpose)
                {
                    case BasePurpose.AnyNonHQ:
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        break;
                    case BasePurpose.HarvestingNoHQ:
                        if (techPurposes.Contains(BasePurpose.Headquarters))
                            return false;
                        if (!techPurposes.Contains(BasePurpose.Harvesting))
                            return false;
                        break;
                    case BasePurpose.AttractTech:
                    case BasePurpose.NotStationary:
                        break;
                    default:
                        if (!techPurposes.Contains(purpose))
                            valid = false;
                        break;
                }
            }
            return valid;
        }
        
        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(FactionTypesExt faction, FactionLevel bestPlayerFaction, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            try
            {
                // Filters
                //DebugTAC_AI.Log("TACtical_AI: GetEnemyBaseTypes - Fetching with " + faction + " - " + bestPlayerFaction + " - " + terra + " - " + maxGrade + " - " + maxPrice);
                List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.techBases.ToList();
                    UseFactionSubTypes = false;
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        FactionSubTypes FST = KickStart.CorpExtToCorp(faction);
                        canidates = TempManager.techBases.ToList().FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return KickStart.CorpExtToCorp(cand.Value.faction) == FST; }).FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.factionLim <= bestPlayerFaction; });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.techBases.ToList().FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.faction == faction; }).FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.factionLim <= bestPlayerFaction; });
                    }
                }

                bool multiplayer = ManNetwork.IsNetworked;
                bool cantErad = !KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= AIGlobals.MaxEradicatorTechs;
                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand)
                {
                    HashSet<BasePurpose> techPurposes = cand.Value.purposes;
                    return ComparePurposes(new HashSet<BasePurpose> { purpose }, techPurposes, cantErad, subNeutral, searchAttract, multiplayer);
                });

                if (terra == BaseTerrain.Any)
                { 
                    //allow all
                }
                else if (terra == BaseTerrain.AnyNonSea)
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.terrain != BaseTerrain.Sea; });
                }
                else
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.terrain == terra; });
                }

                if (maxGrade != 99 && Singleton.Manager<ManGameMode>.inst.CanEarnXp())
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.IntendedGrade <= maxGrade; });
                }

                if (maxPrice > 0)
                {   
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.startingFunds <= maxPrice; });
                }

                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return RawTechTemplate.JSONToMemoryExternal(cand.Value.savedTech).Count <= AIGlobals.MaxBlockLimitAttract; });
                }
                // finally, remove those which are N/A

                //DebugTAC_AI.Log("TACtical_AI: GetEnemyBaseTypes - Found " + canidates.Count + " options");
                if (!canidates.Any())
                    return FallbackHandler(faction);

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final;
            }
            catch { }
            return FallbackHandler(faction);
        }
        internal static List<SpawnBaseTypes> GetEnemyBaseTypes(FactionTypesExt faction, FactionLevel bestPlayerFaction, HashSet<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            try
            {
                // Filters
                //DebugTAC_AI.Log("TACtical_AI: GetEnemyBaseTypes - Fetching with " + faction + " - " + bestPlayerFaction + " - " + terra + " - " + maxGrade + " - " + maxPrice);
                List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.techBases.ToList().FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.factionLim <= bestPlayerFaction; });
                    UseFactionSubTypes = false;
                }
                else
                {
                    if (UseFactionSubTypes)
                    {
                        FactionSubTypes FST = KickStart.CorpExtToCorp(faction);
                        canidates = TempManager.techBases.ToList().FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return KickStart.CorpExtToCorp(cand.Value.faction) == FST; }).FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.factionLim <= bestPlayerFaction; });
                        UseFactionSubTypes = false;
                    }
                    else
                    {
                        canidates = TempManager.techBases.ToList().FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.faction == faction; }).FindAll
                            (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.factionLim <= bestPlayerFaction; });
                    }
                }

                bool multiplayer = ManNetwork.IsNetworked;
                bool cantErad = !KickStart.EnemyEradicators || SpecialAISpawner.Eradicators.Count >= AIGlobals.MaxEradicatorTechs;
                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand)
                {
                    HashSet<BasePurpose> techPurposes = cand.Value.purposes;
                    return ComparePurposes(purposes, techPurposes, cantErad, subNeutral, searchAttract, multiplayer);
                });


                if (terra == BaseTerrain.Any)
                {
                    //allow all
                }
                else if (terra == BaseTerrain.AnyNonSea)
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.terrain != BaseTerrain.Sea; });
                }
                else
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.terrain == terra; });
                }

                if (maxGrade != 99 && Singleton.Manager<ManGameMode>.inst.CanEarnXp())
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.IntendedGrade <= maxGrade; });
                }
                if (maxPrice > 0)
                {
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return cand.Value.startingFunds <= maxPrice; });
                }

                if (searchAttract)
                {   // prevent laggy techs from entering
                    canidates = canidates.FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return RawTechTemplate.JSONToMemoryExternal(cand.Value.savedTech).Count <= AIGlobals.MaxBlockLimitAttract; });
                }
                // finally, remove those which are N/A

                //DebugTAC_AI.Log("TACtical_AI: GetEnemyBaseTypes - Found " + canidates.Count + " options");
                if (canidates.Count == 0)
                    return FallbackHandler(faction);

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final;
            }
            catch { } // we resort to legacy
            return FallbackHandler(faction);
        }
        private static List<SpawnBaseTypes> fallback = new List<SpawnBaseTypes> { SpawnBaseTypes.NotAvail };
        internal static List<SpawnBaseTypes> FallbackHandler(FactionTypesExt faction)
        {
            try
            {
                // Filters
                List<KeyValuePair<SpawnBaseTypes, RawTechTemplate>> canidates;
                if (faction == FactionTypesExt.NULL)
                {
                    canidates = TempManager.techBases.ToList();
                }
                else
                {
                    FactionSubTypes fallback = KickStart.CorpExtToCorp(faction);
                    canidates = TempManager.techBases.ToList().FindAll
                        (delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand) { return (FactionSubTypes)cand.Value.faction == fallback; });
                }

                canidates = canidates.FindAll(delegate (KeyValuePair<SpawnBaseTypes, RawTechTemplate> cand)
                {
                    if (ManNetwork.IsNetworked && cand.Value.purposes.Contains(BasePurpose.MPUnsafe))
                    {   // no illegal base in MP
                        return false;
                    }
                    if (cand.Value.purposes.Contains(BasePurpose.Fallback))
                    {
                        return true;
                    }
                    return false;
                });

                // finally, remove those which are N/A

                if (!canidates.Any())
                {
                    DebugTAC_AI.Log("TACtical_AI: FallbackHandler - COULD NOT FIND FALLBACK FOR " + KickStart.CorpExtToCorp(faction));
                    return fallback;
                }

                // final list compiling
                List<SpawnBaseTypes> final = new List<SpawnBaseTypes>();

                foreach (KeyValuePair<SpawnBaseTypes, RawTechTemplate> pair in canidates)
                    final.Add(pair.Key);

                final.Shuffle();

                return final;
            }
            catch { } // we resort to legacy
            DebugTAC_AI.Log("TACtical_AI: FallbackHandler(ERROR) - COULD NOT FIND FALLBACK FOR " + KickStart.CorpExtToCorp(faction));
            return fallback;
        }

        internal static SpawnBaseTypes GetEnemyBaseType(FactionTypesExt faction, FactionLevel lvl, BasePurpose purpose, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseTypes(faction, lvl, purpose, terra, searchAttract, maxGrade, maxPrice, subNeutral).GetRandomEntry();

                if (!IsBaseTemplateAvailable(toSpawn))
                    DebugTAC_AI.Exception("TACtical_AI: GetEnemyBaseType - population entry " + toSpawn + " has a null BaseTemplate.  How?");
                return toSpawn;
            }
            catch (Exception e)
            { DebugTAC_AI.Exception("TACtical_AI: GetEnemyBaseType - Population seach FAILED:\n" + e); } // we resort to legacy
            //DebugTAC_AI.Assert(true, "TACtical_AI: GetEnemyBaseType - Population seach FAILED");

            int lowerRANDRange = 1;
            int higherRANDRange = 20;
            if (faction == FactionTypesExt.GSO)
            {
                lowerRANDRange = 1;
                higherRANDRange = 6;
            }
            else if (faction == FactionTypesExt.GC)
            {
                lowerRANDRange = 7;
                higherRANDRange = 10;
            }
            else if (faction == FactionTypesExt.VEN)
            {
                lowerRANDRange = 11;
                higherRANDRange = 14;
            }
            else if (faction == FactionTypesExt.HE)
            {
                lowerRANDRange = 15;
                higherRANDRange = 20;
            }

            return (SpawnBaseTypes)UnityEngine.Random.Range(lowerRANDRange, higherRANDRange);
        }
        internal static SpawnBaseTypes GetEnemyBaseType(FactionTypesExt faction, FactionLevel lvl, HashSet<BasePurpose> purposes, BaseTerrain terra, bool searchAttract = false, int maxGrade = 99, int maxPrice = 0, bool subNeutral = false)
        {
            if (ForceSpawn && !searchAttract)
                return forcedBaseSpawn;

            try
            {
                SpawnBaseTypes toSpawn = GetEnemyBaseTypes(faction, lvl, purposes, terra, searchAttract, maxGrade, maxPrice, subNeutral).GetRandomEntry();
                if (!IsBaseTemplateAvailable(toSpawn))
                    DebugTAC_AI.Exception("TACtical_AI: GetEnemyBaseType - population entry " + toSpawn + " has a null BaseTemplate.  How?");
                return toSpawn;
            }
            catch { }
            DebugTAC_AI.Assert(true, "TACtical_AI: GetEnemyBaseType(multiple purposes) - Population seach FAILED");

            return SpawnBaseTypes.NotAvail;
        }

        // External techs
        internal static RawTechTemplate GetExtEnemyBaseFromName(string Name)
        {
            int nameNum = Name.GetHashCode();
            try
            {
                int lookup = TempManager.ExternalEnemyTechsAll.FindIndex(delegate (RawTechTemplate cand) { return cand.techName.GetHashCode() == nameNum; });
                if (lookup == -1) 
                    return null;
                return TempManager.ExternalEnemyTechsAll[lookup];
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
                int lookup = TempManager.techBases.Values.ToList().FindIndex(delegate (RawTechTemplate cand) { return cand.techName == Name; });
                if (lookup == -1) return SpawnBaseTypes.NotAvail;
                return TempManager.techBases.ElementAt(lookup).Key;
            }
            catch 
            {
                return SpawnBaseTypes.NotAvail;
            }
        }
      
        internal static FactionSubTypes GetMainCorp(SpawnBaseTypes toSpawn)
        {
            return KickStart.CorpExtToCorp(GetBaseTemplate(toSpawn).faction);
        }

        internal static bool IsHQ(SpawnBaseTypes toSpawn)
        {
            if (TempManager.techBases.TryGetValue(toSpawn, out RawTechTemplate baseT))
                return baseT.purposes.Contains(BasePurpose.Headquarters);
            return false;
        }
        internal static bool ContainsPurpose(SpawnBaseTypes toSpawn, BasePurpose purpose)
        {
            if (TempManager.techBases.TryGetValue(toSpawn, out RawTechTemplate baseT))
                return baseT.purposes.Contains(purpose);
            return false;
        }
        private static bool IsRadiusClearOfTechObst(Vector3 pos, float radius)
        {
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, new Bitfield<ObjectTypes>(new ObjectTypes[2] { ObjectTypes.Vehicle, ObjectTypes.Scenery })))
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
            try
            {
                TempManager.techBases.TryGetValue(type, out RawTechTemplate val);
                if (val.purposes.Contains(BasePurpose.Fallback))
                    return true;
                return false;
            }
            catch { } 
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
            HashSet<int> teamsGrabbed = new HashSet<int>();
            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, radius, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Vehicle })))
            {
                if (vis.tank.IsNotNull())
                {
                    Tank tech = vis.tank;
                    if (!teamsGrabbed.Contains(tech.Team) &&!tech.IsNeutral() && !ManSpawn.IsPlayerTeam(tech.Team) && tech.IsAnchored)
                    {
                        teamsGrabbed.Add(tech.Team);
                    }
                }
            }
            return teamsGrabbed.Count;
        }
        internal static int GetEnemyBaseCountForTeam(int Team)
        {
            int baseCount = 0;
            List<Tank> tanks = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
            for (int step = 0; step < tanks.Count; step++)
            {
                Tank tech = tanks.ElementAt(step);
                if (tech.IsAnchored && !tech.IsNeutral() && !ManSpawn.IsPlayerTeam(tech.Team) && tech.IsFriendly(Team))
                    baseCount++;
            }
            return baseCount;
        }


        private static void MakeSureCanExistWithBase(Tank tank)
        {
            if (tank.IsPopulation || !tank.IsFriendly(tank.Team) || tank.Team == ManSpawn.FirstEnemyTeam || tank.Team == ManSpawn.NewEnemyTeam)
            {
                int set = AIGlobals.GetRandomBaseTeam(true);
                DebugTAC_AI.Log("TACtical_AI: Tech " + tank.name + " spawned team " + tank.Team + " that fights against themselves, setting to team " + set + " instead");
                tank.SetTeam(set, false);
                TryRemoveFromPop(tank);
            }
        }
        private static int ReassignToExistingEnemyBaseTeam()
        {
            List<int> enemyBaseTeams = new List<int>();
            foreach (var item in Singleton.Manager<ManTechs>.inst.IterateTechs())
            {
                if (item.IsAnchored && AIGlobals.IsEnemyBaseTeam(item.Team))
                    enemyBaseTeams.Add(item.Team);
            }
            if (!enemyBaseTeams.Any())
                return -1;
            int steppe = UnityEngine.Random.Range(0, enemyBaseTeams.Count - 1);

            return enemyBaseTeams.ElementAt(steppe);
        }


        private static List<TankBlock> blocs = new List<TankBlock>();
        private static List<BlockTypes> types = new List<BlockTypes>();
        internal static void ReconstructConveyorSequencing(Tank tank)
        {
            try
            {
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                    return;

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
                    blocs.Clear();
                    types.Clear();
                }
                else
                    ReconstructConveyorSequencingNoMem(tank);
                // can't fix - any previous design data was not saved!
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: ReconstructConveyorSequencing - error 0");
            }
        }
        private static void ReconstructConveyorSequencingNoMem(Tank tank)
        {
            try
            {
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                    return;

                List<RawBlockMem> mems = new List<RawBlockMem>();
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
                blocs.Clear();
                types.Clear();
                // can't fix - any previous design data was not saved!
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: ReconstructConveyorSequencing - error 0");
            }
        }
        private static void ReconstructConveyorSequencingInternal(Tank tank, List<RawBlockMem> memsConvey, List<BlockTypes> types)
        {
            try
            {
                if (ManNetwork.IsNetworked && !ManNetwork.IsHost)
                    return;

                List<TankBlock> blocs = new List<TankBlock>();
                foreach (TankBlock chain in tank.blockman.IterateBlocks())
                {   // intel
                    if (chain.GetComponent<ModuleItemConveyor>())
                    {
                        blocs.Add(chain);
                        if (!types.Contains(chain.BlockType))
                            types.Add(chain.BlockType);
                    }
                }
                if (memsConvey.Count() == 0)
                    return;

                foreach (TankBlock block in blocs)
                {   // detach
                    try
                    {
                        if (block.IsAttached)
                            tank.blockman.Detach(block, false, false, false);
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: ReconstructConveyorSequencing - error 1");
                    }
                }

                AIERepair.BulkAdding = true;

                int count = memsConvey.Count;
                for (int stepBloc = 0; stepBloc < blocs.Count; stepBloc++)
                {
                    for (int step = 0; step < count; step++)
                    {   // reconstruct
                        try
                        {
                            if (blocs[stepBloc].name == memsConvey[step].t)
                            {
                                TankBlock block = blocs[stepBloc];
                                if (block == null)
                                    continue;

                                if (!AIERepair.AIBlockAttachRequest(tank, memsConvey[step], block, false))
                                {
                                    //DebugTAC_AI.Log("TACtical_AI: ReconstructConveyorSequencing - error 3");
                                }
                                else
                                {
                                    blocs[step].damage.AbortSelfDestruct();
                                    memsConvey.RemoveAt(step);
                                    count--;
                                    step--;
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            DebugTAC_AI.Log("TACtical_AI: ReconstructConveyorSequencing - error 2");
                        }
                    }
                }
                AIERepair.BulkAdding = false;
                // can't fix - any previous design data was not saved!
            }
            catch
            {
                DebugTAC_AI.Log("TACtical_AI: ReconstructConveyorSequencing - error 0");
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
                DebugTAC_AI.Log("TACtical_AI: ForceAllBubblesUp - error");
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
                DebugTAC_AI.Log("TACtical_AI: ChargeAndClean - error");
            }
        }
    }
}
