using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAC_AI.AI.Movement;
using TAC_AI.AI.Movement.AICores;
using TAC_AI.AI.Enemy.EnemyOperations;
using UnityEngine;


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
        public EnemyHandling EvilCommander = EnemyHandling.Wheeled; // What kind of vehicle is this Enemy?
        public EnemyAttitude CommanderMind = EnemyAttitude.Default; // What the Enemy does if there's no threats around.
        public EnemyAttack CommanderAttack = EnemyAttack.Circle;    // The way the Enemy acts if there's a threat.
        public EnemySmarts CommanderSmarts = EnemySmarts.Default;   // The extent the Enemy will be "self-aware"
        public EnemyBolts CommanderBolts = EnemyBolts.Default;      // When the Enemy should press X.
        public EnemyStanding CommanderAlignment = EnemyStanding.Enemy;// How to handle attacks

        public FactionTypesExt MainFaction = FactionTypesExt.GSO;   // Extra for determining mentality on auto-generation
        public bool StartedAnchored = false;    // Do we stay anchored?
        public bool AllowRepairsOnFly = false;  // If we are feeling extra evil
        public bool InvertBullyPriority = false;// Shoot the big techs instead
        public bool AllowInvBlocks = false;     // Can this tech spawn blocks from inventory?
        public bool LikelyMelee = false;        // Can we melee?

        public bool SolarsAvail = false;        // Do we currently have solar panels
        public int Provoked = 0;           // Were we hit from afar?
        public bool Hurt = false;               // Are we damaged?
        public bool PursuingTarget = false;     // Chasing specified target?
        public int Range = AIGlobals.DefaultEnemyRange;// Aggro range
        public int TargetLockDuration = 0;      // Updates to wait before target swatching
        public Vector3 HoldPos = Vector3.zero;  // For stationary techs like Wingnut who must hold ground

        internal bool BuildAssist = false;

        internal int BoltsQueued = 0;


        public bool AttackPlayer => CommanderAlignment == EnemyStanding.Enemy;
        public bool AttackAny => CommanderAlignment < EnemyStanding.SubNeutral;


        public void Initiate()
        {
            //Debug.Log("TACtical_AI: Launching Enemy AI for " + Tank.name);
            Tank = gameObject.GetComponent<Tank>();
            AIControl = gameObject.GetComponent<AIECore.TankAIHelper>();
            AIControl.FinishedRepairEvent.Subscribe(OnFinishedRepairs);
            Tank.DamageEvent.Subscribe(OnHit);
            Tank.AttachEvent.Subscribe(OnBlockAdd);
            Tank.DetachEvent.Subscribe(OnBlockLoss);
        }
        public void Refresh()
        {
            if (GetComponents<EnemyMind>().Count() > 1)
                Debug.Log("TACtical_AI: ASSERT: THERE IS MORE THAN ONE EnemyMind ON " + Tank.name + "!!!");

            //Debug.Log("TACtical_AI: Refreshing Enemy AI for " + Tank.name);
            EnemyOpsController = new EnemyOperationsController(this);
            AIControl.MovementController.UpdateEnemyMind(this);
            AIControl.AvoidStuff = true;
            PursuingTarget = false;
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
            //Debug.Log("TACtical_AI: Removing Enemy AI for " + Tank.name);
            if (gameObject.GetComponent<AIERepair.DesignMemory>().IsNotNull())
                gameObject.GetComponent<AIERepair.DesignMemory>().Remove();
            Tank.DamageEvent.Unsubscribe(OnHit);
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

        public void OnHit(ManDamage.DamageInfo dingus)
        {
            if (dingus.Damage > 75 && dingus.SourceTank)
            {
                Hurt = true;
                if (Provoked == 0)
                {
                    AIControl.lastEnemy = dingus.SourceTank.visible;
                    GetRevengeOn(AIControl.lastEnemy);
                    if (Tank.IsAnchored || CommanderSmarts > EnemySmarts.Mild)
                    {
                        // Execute remote orders to allied units - Attack that threat!
                        RBases.RequestFocusFire(Tank, AIControl.lastEnemy);
                    }
                }
                Provoked = AIGlobals.ProvokeTime;
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
                mind.AIControl.PendingSystemsCheck = true;
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

        public void OnFinishedRepairs()
        {
            try
            {
                Debug.Log("TACtical_AI: OnFinishedRepair");
                if (TechMemor)
                {
                    Debug.Log("TACtical_AI: TechMemor");
                    if (!TechMemor.ranOutOfParts && Tank.name.Contains('⟰'))
                    {
                        Tank.SetName(Tank.name.Replace(" ⟰", ""));
                        AIControl.AIState = AIAlignment.NonPlayerTech;
                        RCore.RandomizeBrain(AIControl, Tank);
                        Debug.Log("TACtical_AI: (Rechecking blocks) Enemy AI " + Tank.name + " of Team " + Tank.Team + ":  Ready to kick some Tech!");
                        BuildAssist = false;
                    }
                }
            }
            catch { }
        }

        public void GetRevengeOn(Visible target = null, bool forced = false)
        {
            if (!PursuingTarget && (forced || CommanderAttack != EnemyAttack.Coward))
            {
                if (forced)
                {
                    if (CommanderAttack == EnemyAttack.Coward) // no time for chickens
                        CommanderAttack = EnemyAttack.Grudge;

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
                if ((bool)target)
                {
                    if ((bool)target.tank)
                    {
                        AIControl.AvoidStuff = true;
                        AIControl.lastEnemy = target;
                        AIControl.lastDestination = target.tank.boundsCentreWorldNoCheck;
                    }
                }
                if (Range == AIGlobals.DefaultEnemyRange)
                {
                    Range = 500;
                }
                PursuingTarget = true;
            }
        }
        public void EndAggro()
        {
            if (PursuingTarget)
            {
                if (Tank.blockman.IterateBlockComponents<ModuleItemHolderBeam>().Count() != 0)
                    CommanderMind = EnemyAttitude.Miner;
                else if (AIECore.FetchClosestBlockReceiver(Tank.boundsCentreWorldNoCheck, Range, out _, out _, Tank.Team))
                    CommanderMind = EnemyAttitude.Junker;
                if (Range == 500)
                {
                    Range = AIGlobals.DefaultEnemyRange;
                }
                PursuingTarget = false;
            }
        }

        public bool IsInRange(Visible target)
        {
            float inRange;
            switch (CommanderAttack)
            {
                case EnemyAttack.Spyper:
                    inRange = AIGlobals.SpyperMaxRange;
                    break;
                default:
                    inRange = Range;
                    break;
            }
            return (target.tank.boundsCentreWorldNoCheck - Tank.boundsCentreWorldNoCheck).sqrMagnitude <= inRange * inRange;
        }
        public bool InRangeOfTarget()
        {
            return IsInRange(AIControl.lastEnemy);
        }

        /// <summary>
        ///  Gets the enemy position based on current position and AI preferences
        /// </summary>
        /// <param name="inRange">value > 0</param>
        /// <param name="pos">MAX 3</param>
        /// <returns></returns>
        public Visible FindEnemy(float inRange = 0, int pos = 1)
        {
            //if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
            //    return null; // We NO ATTACK
            Visible target = AIControl.lastEnemy;

            // We begin the search
            if (CommanderAttack == EnemyAttack.Spyper) inRange = AIGlobals.SpyperMaxRange;
            else if (inRange <= 0) inRange = Range;
            float TargetRange = inRange * inRange;
            Vector3 scanCenter = Tank.boundsCentreWorldNoCheck;

            if (target?.tank)
            {
                if (!target.isActive || !target.tank.IsEnemy(Tank.Team) || (target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRange)
                {
                    //Debug.Log("Target lost");
                    target = null;
                }
                else if (PursuingTarget) // Carry on chasing the target
                {
                    return target;
                }
                else if (TargetLockDuration >= 0)
                {
                    TargetLockDuration -= KickStart.AIClockPeriod;
                    return target;
                }
            }

            List<Tank> techs = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
            if (CommanderAttack == EnemyAttack.Pesterer)
            {
                int max = techs.Count();
                int launchCount = UnityEngine.Random.Range(0, max);
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (cTank.IsEnemy(Tank.Team) && cTank != Tank && cTank.visible.isActive)
                    {
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                        if (dist < TargetRange)
                        {
                            target = cTank.visible;
                        }
                    }
                }
                TargetLockDuration = AIGlobals.PestererSwitchDelay;
            }
            else if (CommanderAttack == EnemyAttack.Bully)
            {
                int launchCount = techs.Count();
                if (InvertBullyPriority)
                {
                    int BlockCount = 0;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                        {
                            float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                            if (cTank.blockman.blockCount > BlockCount && dist < TargetRange)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                else
                {
                    int BlockCount = 262144;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                        {
                            float dist = (cTank.boundsCentreWorldNoCheck - Tank.boundsCentreWorldNoCheck).sqrMagnitude;
                            if (cTank.blockman.blockCount < BlockCount && dist < TargetRange)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                TargetLockDuration = AIGlobals.ScanDelay;
            }
            else
            {
                TargetLockDuration = AIGlobals.ScanDelay;
                if (CommanderAttack == EnemyAttack.Grudge && target != null)
                {
                    if (target.isActive)
                        return target;
                }
                if (pos == 1)
                    return Tank.Vision.GetFirstVisibleTechIsEnemy(Tank.Team);

                float TargRange2 = TargetRange;
                float TargRange3 = TargetRange;

                Visible target2 = null;
                Visible target3 = null;

                int launchCount = techs.Count();

                Tank cTank;
                float dist;
                int step;
                switch (pos)
                {
                    case 2:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRange)
                                {
                                    if (TargetRange < TargRange2)
                                    {
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRange = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                            }
                        }
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    case 3:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRange)
                                {
                                    if (TargetRange < TargRange2)
                                    {
                                        if (TargRange2 < TargRange3)
                                        {
                                            TargRange3 = dist;
                                            target3 = cTank.visible;
                                        }
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRange = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    if (TargRange2 < TargRange3)
                                    {
                                        TargRange3 = dist;
                                        target3 = cTank.visible;
                                    }
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                                else if (dist < TargRange3)
                                {
                                    TargRange3 = dist;
                                    target3 = cTank.visible;
                                }
                            }
                        }
                        if (pos >= 3 && !(bool)target3)
                            return target3;
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    default:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRange)
                                {
                                    TargetRange = dist;
                                    target = cTank.visible;
                                }
                            }
                        }
                        break;
                }
            }
            /*
            if (target.IsNull())
            {
                Debug.Log("TACtical_AI: Tech " + Tank.name + " Could not find target with FindEnemy, resorting to defaults");
                return Tank.Vision.GetFirstVisibleTechIsEnemy(Tank.Team);
            }
            */
            return target;
        }

        public Visible FindEnemyAir(float inRange = 0, int pos = 1)
        {
            //if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
            //    return null; // We NO ATTACK
            Visible target = AIControl.lastEnemy;

            // We begin the search
            if (CommanderAttack == EnemyAttack.Spyper) inRange = AIGlobals.SpyperMaxRange;
            else if (inRange <= 0) inRange = 500;
            float TargetRange = inRange * inRange;
            Vector3 scanCenter = Tank.boundsCentreWorldNoCheck;

            if (target != null)
            {
                if (!target.isActive || !target.tank.IsEnemy(Tank.Team) || (target.tank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude > TargetRange)
                {
                    //Debug.Log("Target lost");
                    target = null;
                }
                else if (PursuingTarget) // Carry on chasing the target
                {
                    return target;
                }
                else if (TargetLockDuration >= 0)
                {
                    TargetLockDuration -= KickStart.AIClockPeriod;
                    return target;
                }
            }
            float altitudeHigh = -256;

            List<Tank> techs = AIECore.TankAIManager.GetTargetTanks(Tank.Team);
            if (CommanderAttack == EnemyAttack.Pesterer)
            {
                scanCenter = AircraftUtils.ForeAiming(Tank.visible);
                int launchCount = techs.Count();
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                    {
                        if (altitudeHigh < cTank.boundsCentreWorldNoCheck.y)
                        {   // Priority is other aircraft
                            if (AIEPathing.AboveHeightFromGround(cTank.boundsCentreWorldNoCheck))
                                altitudeHigh = AIEPathing.OffsetFromGroundA(cTank.boundsCentreWorldNoCheck, AIControl).y;
                            else
                                altitudeHigh = cTank.boundsCentreWorldNoCheck.y;
                        }
                        else
                            continue;
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                        if (dist < TargetRange)
                        {
                            TargetRange = dist;
                            target = cTank.visible;
                        }
                    }
                }
            }
            else if (CommanderAttack == EnemyAttack.Bully)
            {
                int launchCount = techs.Count();
                if (InvertBullyPriority)
                {
                    altitudeHigh = 2199;
                    int BlockCount = 0;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                        {
                            if (altitudeHigh > cTank.boundsCentreWorldNoCheck.y)
                            {   // Priority is bases or lowest target
                                if (!AIEPathing.AboveHeightFromGround(cTank.boundsCentreWorldNoCheck))
                                    altitudeHigh = AIEPathing.OffsetFromGroundA(cTank.boundsCentreWorldNoCheck, AIControl).y;
                                else
                                    altitudeHigh = cTank.boundsCentreWorldNoCheck.y;
                            }
                            else
                                continue;
                            float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                            if (cTank.blockman.blockCount > BlockCount && dist < TargetRange)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                else
                {
                    int BlockCount = 262144;
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                        {
                            if (altitudeHigh < cTank.boundsCentreWorldNoCheck.y)
                            {   // Priority is other aircraft
                                if (AIEPathing.AboveHeightFromGround(cTank.boundsCentreWorldNoCheck))
                                    altitudeHigh = AIEPathing.OffsetFromGroundA(cTank.boundsCentreWorldNoCheck, AIControl).y;
                                else
                                    altitudeHigh = cTank.boundsCentreWorldNoCheck.y;
                            }
                            else
                                continue;
                            float dist = (cTank.boundsCentreWorldNoCheck - Tank.boundsCentreWorldNoCheck).sqrMagnitude;
                            if (cTank.blockman.blockCount < BlockCount && dist < TargetRange)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
            }
            else
            {
                if (CommanderAttack == EnemyAttack.Grudge && target != null)
                {
                    if (target.isActive)
                        return target;
                }
                float TargRange2 = TargetRange;
                float TargRange3 = TargetRange;

                Visible target2 = null;
                Visible target3 = null;

                int launchCount = techs.Count();
                Tank cTank;
                float dist;
                int step;
                switch (pos)
                {
                    case 2:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRange)
                                {
                                    if (TargetRange < TargRange2)
                                    {
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRange = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                            }
                        }
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    case 3:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRange)
                                {
                                    if (TargetRange < TargRange2)
                                    {
                                        if (TargRange2 < TargRange3)
                                        {
                                            TargRange3 = dist;
                                            target3 = cTank.visible;
                                        }
                                        TargRange2 = dist;
                                        target2 = cTank.visible;
                                    }
                                    TargetRange = dist;
                                    target = cTank.visible;
                                }
                                else if (dist < TargRange2)
                                {
                                    if (TargRange2 < TargRange3)
                                    {
                                        TargRange3 = dist;
                                        target3 = cTank.visible;
                                    }
                                    TargRange2 = dist;
                                    target2 = cTank.visible;
                                }
                                else if (dist < TargRange3)
                                {
                                    TargRange3 = dist;
                                    target3 = cTank.visible;
                                }
                            }
                        }
                        if (pos >= 3 && !(bool)target3)
                            return target3;
                        if (pos == 2 && !(bool)target2)
                            return target2;
                        break;
                    default:
                        for (step = 0; step < launchCount; step++)
                        {
                            cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                            {
                                dist = (cTank.boundsCentreWorldNoCheck - scanCenter).sqrMagnitude;
                                if (dist < TargetRange)
                                {
                                    TargetRange = dist;
                                    target = cTank.visible;
                                }
                            }
                        }
                        break;
                }
                TargetLockDuration = AIGlobals.ScanDelay;
            }
            return target;
        }


    }
}
