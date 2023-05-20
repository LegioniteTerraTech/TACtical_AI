using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;
using TAC_AI.AI.Enemy.EnemyOperations;


namespace TAC_AI.AI.Enemy
{
    public class EnemyMind : MonoBehaviour
    {   // Where the brain is handled for enemies (and mayber non-player allies)
        // ESSENTIALS
        public Tank Tank;
        public AIECore.TankAIHelper AIControl;
        public EnemyOperationsController EnemyOpsController;
        public AIERepair.DesignMemory TechMemor => AIControl.TechMemor;

        // Set on spawn
        private EnemyHandling evilCommander = EnemyHandling.Wheeled; // What kind of vehicle is this Enemy?
        public EnemyHandling EvilCommander 
        { 
            get => evilCommander;
            set 
            {
                switch (value)
                {
                    case EnemyHandling.Chopper:
                    case EnemyHandling.Airplane:
                        AIControl.DriverType = AIDriverType.Pilot;
                        break;
                    case EnemyHandling.Starship:
                        AIControl.DriverType = AIDriverType.Astronaut;
                        break;
                    case EnemyHandling.Naval:
                        AIControl.DriverType = AIDriverType.Sailor;
                        break;
                    case EnemyHandling.SuicideMissile:
                        AIControl.DriverType = AIDriverType.Pilot;
                        break;
                    case EnemyHandling.Stationary:
                        AIControl.DriverType = AIDriverType.Stationary;
                        break;
                    default:
                        AIControl.DriverType = AIDriverType.Tank;
                        break;
                }
                evilCommander = value;
            }
        }
        public EnemyAttitude CommanderMind = EnemyAttitude.Default; // What the Enemy does if there's no threats around.
        public EAttackMode CommanderAttack
        {     // The way the Enemy acts if there's a threat.
            get => AIControl.AttackMode;
            set { AIControl.AttackMode = value; }
        }
        public EnemySmarts CommanderSmarts = EnemySmarts.Default;   // The extent the Enemy will be "self-aware"
        public EnemyBolts CommanderBolts = EnemyBolts.Default;      // When the Enemy should press X.
        public EnemyStanding CommanderAlignment = EnemyStanding.Enemy;// How to handle attacks

        public FactionTypesExt MainFaction = FactionTypesExt.GSO;   // Extra for determining mentality on auto-generation
        public bool StartedAnchored = false;    // Do we stay anchored?
        public bool AllowRepairsOnFly = false;  // If we are feeling extra evil
        public bool InvertBullyPriority = false;// Shoot the big techs instead
        public bool AllowInvBlocks = false;     // Can this tech spawn blocks from inventory?
        public bool LikelyMelee        // Can we melee?
        {     // The way the Enemy acts if there's a threat.
            get => AIControl.FullMelee;
            set { AIControl.FullMelee = value; }
        }

        public bool SolarsAvail = false;        // Do we currently have solar panels
        public bool Hurt = false;               // Are we damaged?
        public float MaxCombatRange// Aggro range
        {
            get => AIControl.MaxCombatRange;
            set => AIControl.AILimitSettings.ChaseRange = value;
        }
        public float MinCombatRange
        {
            get => AIControl.MinCombatRange;
            set => AIControl.AILimitSettings.CombatRange = value;
        }
        public Vector3 sceneStationaryPos = Vector3.zero;  // For stationary techs like Wingnut who must hold ground

        internal bool BuildAssist = false;

        internal int BoltsQueued = 0;


        public bool AttackPlayer => CommanderAlignment == EnemyStanding.Enemy;
        public bool AttackAny => CommanderAlignment < EnemyStanding.SubNeutral;


        public void Initiate()
        {
            //DebugTAC_AI.Log("TACtical_AI: Launching Enemy AI for " + Tank.name);
            Tank = gameObject.GetComponent<Tank>();
            AIControl = gameObject.GetComponent<AIECore.TankAIHelper>();
            AIControl.FinishedRepairEvent.Subscribe(OnFinishedRepairs);
            Tank.DamageEvent.Unsubscribe(AIControl.OnHit);
            Tank.DamageEvent.Subscribe(OnHit);
            Tank.AttachEvent.Subscribe(OnBlockAdd);
            Tank.DetachEvent.Subscribe(OnBlockLoss);
        }
        public void Refresh()
        {
            if (GetComponents<EnemyMind>().Count() > 1)
                DebugTAC_AI.Log("TACtical_AI: ASSERT: THERE IS MORE THAN ONE EnemyMind ON " + Tank.name + "!!!");

            //DebugTAC_AI.Log("TACtical_AI: Refreshing Enemy AI for " + Tank.name);
            EnemyOpsController = new EnemyOperationsController(this);
            AIControl.MovementController.UpdateEnemyMind(this);
            AIControl.AvoidStuff = true;
            AIControl.EndPursuit();
            BoltsQueued = 0;
            try
            {
                MainFaction = Tank.GetMainCorpExt();   //Will help determine their Attitude
            }
            catch
            {   // can't always get this 
                MainFaction = FactionTypesExt.GSO;
            }
        }
        public void SetForRemoval()
        {
            //DebugTAC_AI.Log("TACtical_AI: Removing Enemy AI for " + Tank.name);
            Tank.DamageEvent.Unsubscribe(OnHit);
            if (AIControl)
                Tank.DamageEvent.Subscribe(AIControl.OnHit);
            else
                DebugTAC_AI.Assert("NULL AIControl (AIECore.TankAIHelper) in Tech " + Tank.name);
            Tank.AttachEvent.Unsubscribe(OnBlockAdd);
            Tank.DetachEvent.Unsubscribe(OnBlockLoss);
            AIControl.FinishedRepairEvent.Unsubscribe(OnFinishedRepairs);
            AIControl.MovementController.UpdateEnemyMind(null);
            DestroyImmediate(this);
        }

        public void OnBlockAdd(TankBlock blockAdd, Tank tonk)
        {
            try
            {
                if (tonk.FirstUpdateAfterSpawn)
                {
                    if (blockAdd.GetComponent<Damageable>().Health > 0)
                        blockAdd.damage.AbortSelfDestruct();
                }
            }
            catch { }
        }
        public void OnWorldMove(IntVector3 move)
        {
            sceneStationaryPos += move;
        }

        /// <summary>
        /// React when hit by an attack from another Tech. 
        /// Must be resubbed and un-subbed when switching to and from enemy
        /// </summary>
        public void OnHit(ManDamage.DamageInfo dingus)
        {
            if (dingus.SourceTank && dingus.Damage > AIGlobals.DamageAlertThreshold)
            {
                Hurt = true;
                if (AIControl.Provoked == 0)
                {
                    AIControl.lastEnemy = dingus.SourceTank.visible;
                    GetRevengeOn(AIControl.lastEnemyGet);
                    if (Tank.IsAnchored && Tank.GetComponent<RLoadedBases.EnemyBaseFunder>())
                    {
                        // Execute remote orders to allied units - Attack that threat!
                        RLoadedBases.RequestFocusFireNPTs(Tank, AIControl.lastEnemyGet, RequestSeverity.AllHandsOnDeck);
                    }
                    else if (CommanderSmarts > EnemySmarts.Mild)
                    {
                        // Execute remote orders to allied units - Attack that threat!
                        if (AIGlobals.IsNeutralBaseTeam(Tank.Team))
                            RLoadedBases.RequestFocusFireNPTs(Tank, AIControl.lastEnemyGet, RequestSeverity.Warn);
                        else
                            RLoadedBases.RequestFocusFireNPTs(Tank, AIControl.lastEnemyGet, RequestSeverity.ThinkMcFly);
                    }
                }
                AIControl.Provoked = AIGlobals.ProvokeTime;
                AIControl.FIRE_NOW = true;
            }
        }
        public static void OnBlockLoss(TankBlock blockLoss, Tank tonk)
        {
            try
            {
                if (tonk.FirstUpdateAfterSpawn)
                    return;
                var mind = tonk.GetComponent<EnemyMind>();
                mind.AIControl.FIRE_NOW = true;
                mind.Hurt = true;
                mind.AIControl.PendingDamageCheck = true;
                if (mind.BoltsQueued == 0 && ManNetwork.IsHost)
                {   // do NOT destroy blocks on split Techs!
                    if (!blockLoss.GetComponent<ModuleTechController>())
                    {
                        if ((bool)mind.TechMemor)
                        {   // cannot self-destruct timer cabs or death
                            if (mind.TechMemor.ChanceGrabBackBlock())
                                return;// no destroy block
                            mind.ChanceDestroyBlock(blockLoss);
                        }
                        else
                            mind.ChanceDestroyBlock(blockLoss);
                    }
                }
            }
            catch { }
        }
        public void ChanceDestroyBlock(TankBlock blockLoss)
        {
            try
            {
                if (ManLicenses.inst.GetLicense(ManSpawn.inst.GetCorporation(blockLoss.BlockType)).CurrentLevel < ManLicenses.inst.GetBlockTier(blockLoss.BlockType) && !KickStart.AllowOverleveledBlockDrops)
                {
                    if (ManNetwork.IsNetworked)
                        ManLooseBlocks.inst.RequestDespawnBlock(blockLoss, DespawnReason.Host);
                    blockLoss.damage.SelfDestruct(0.6f); // - no get illegal blocks
                }
                else
                {
                    if (UnityEngine.Random.Range(0, 99) >= KickStart.EnemyBlockDropChance)
                    {
                        if (ManNetwork.IsNetworked)
                            ManLooseBlocks.inst.RequestDespawnBlock(blockLoss, DespawnReason.Host);
                        blockLoss.damage.SelfDestruct(0.75f);
                    }
                }
            }
            catch
            {
                if (UnityEngine.Random.Range(0, 99) >= KickStart.EnemyBlockDropChance)
                    blockLoss.damage.SelfDestruct(0.6f);
            }
        }

        public void OnFinishedRepairs(AIECore.TankAIHelper unused)
        {
            try
            {
                //DebugTAC_AI.Log("TACtical_AI: OnFinishedRepair");
                if (TechMemor)
                {
                    //DebugTAC_AI.Log("TACtical_AI: TechMemor");
                    if (Tank.name.Contains('⟰'))
                    {
                        Tank.SetName(Tank.name.Replace(" ⟰", ""));
                        RCore.RandomizeBrain(AIControl, Tank);
                        AIControl.AIAlign = AIAlignment.NonPlayer;
                        DebugTAC_AI.Log("TACtical_AI: (Rechecking blocks) Enemy AI " + Tank.name + " of Team " + Tank.Team + ":  Ready to kick some Tech!");
                        BuildAssist = false;
                    }
                }
            }
            catch { }
        }

        public void GetRevengeOn(Visible target = null, bool forced = false)
        {
            if (!AIControl.KeepEnemyFocus && (forced || CommanderAttack != EAttackMode.Safety))
            {
                if (forced)
                {
                    if (CommanderAttack == EAttackMode.Safety) // no time for chickens
                        CommanderAttack = EAttackMode.Chase;

                    switch (CommanderMind)
                    {
                        case EnemyAttitude.Default:
                        case EnemyAttitude.Homing:
                            break;
                        default:
                            CommanderMind = EnemyAttitude.Default;
                            break;
                    }
                }
                AIControl.SetPursuit(target);
            }
        }
        public void EndAggro()
        {
            if (AIControl.KeepEnemyFocus)
            {
                if (Tank.blockman.IterateBlockComponents<ModuleItemHolderBeam>().Count() != 0)
                    CommanderMind = EnemyAttitude.Miner;
                else if (AIECore.FetchClosestBlockReceiver(Tank.boundsCentreWorldNoCheck, MaxCombatRange, out _, out _, Tank.Team))
                    CommanderMind = EnemyAttitude.Junker;
                AIControl.EndPursuit();
            }
        }

        public bool InMaxCombatRangeOfTarget()
        {
            switch (CommanderAttack)
            {
                case EAttackMode.Ranged:
                    return AIControl.InRangeOfTarget(AIGlobals.SpyperMaxCombatRange);
                default:
                    return AIControl.InRangeOfTarget(MaxCombatRange);
            }
        }

    }
}
