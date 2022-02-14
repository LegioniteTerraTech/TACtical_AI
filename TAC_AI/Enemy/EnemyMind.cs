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
    {   // Where the brain is handled for enemies
        // ESSENTIALS
        public Tank Tank;
        public AIECore.TankAIHelper AIControl;
        public EnemyOperationsController EnemyOpsController;
        public AIERepair.DesignMemory TechMemor;

        // Set on spawn
        public EnemyHandling EvilCommander = EnemyHandling.Wheeled; // What kind of vehicle is this Enemy?
        public EnemyAttitude CommanderMind = EnemyAttitude.Default; // What the Enemy does if there's no threats around.
        public EnemyAttack CommanderAttack = EnemyAttack.Circle;    // The way the Enemy acts if there's a threat.
        public EnemySmarts CommanderSmarts = EnemySmarts.Default;   // The extent the Enemy will be "self-aware"
        public EnemyBolts CommanderBolts = EnemyBolts.Default;      // When the Enemy should press X.

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
        public int Range = KickStart.DefaultEnemyRange;// Aggro range
        public int TargetLockDuration = 0;      // Updates to wait before target swatching
        public Vector3 HoldPos = Vector3.zero;  // For stationary techs like Wingnut who must hold ground

        internal bool queueRemove = false;
        internal const float MaxRangeFireAll = 125;
        internal const int BaseFounderRange = 60;
        internal const float SpyperMaxRange = 450;
        internal const float SpacingRange = 8;
        internal const float SpacingRangeAir = 32;
        internal const int ScanDelay = 20;
        internal const int PestererSwitchDelay = 500;
        internal const int ProvokeTime = 200;

        internal int BoltsQueued = 0;

        public void Initiate()
        {
            //Debug.Log("TACtical_AI: Launching Enemy AI for " + Tank.name);
            Tank = gameObject.GetComponent<Tank>();
            AIControl = gameObject.GetComponent<AIECore.TankAIHelper>();
            Tank.DamageEvent.Subscribe(OnHit);
            Tank.AttachEvent.Subscribe(OnBlockAdd);
            Tank.DetachEvent.Subscribe(OnBlockLoss);
        }
        public void Refresh()
        {
            if (GetComponents<EnemyMind>().Count() > 1)
                Debug.Log("TACtical_AI: ASSERT: THERE IS MORE THAN ONE EnemyMind ON " + Tank.name + "!!!");

            //Debug.Log("TACtical_AI: Refreshing Enemy AI for " + Tank.name);
            queueRemove = false;
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
            queueRemove = true;
            if (gameObject.GetComponent<AIERepair.DesignMemory>().IsNotNull())
                gameObject.GetComponent<AIERepair.DesignMemory>().Remove();
            Tank.DamageEvent.Unsubscribe(OnHit);
            Tank.AttachEvent.Unsubscribe(OnBlockAdd);
            Tank.DetachEvent.Unsubscribe(OnBlockLoss);
            AIControl.MovementController.UpdateEnemyMind(null);
            DestroyImmediate(this);
        }

        public static void OnBlockAdd(TankBlock blockAdd, Tank tonk)
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
            if (dingus.Damage > 100)
            {
                Hurt = true;
                Provoked = ProvokeTime;
                AIControl.FIRE_NOW = true;
                try
                {
                    if ((bool)dingus.SourceTank)
                    {
                        GetRevengeOn(dingus.SourceTank.visible);
                        if (CommanderSmarts == EnemySmarts.IntAIligent && RBases.GetTeamBaseCount(Tank.Team) > 0)
                            RBases.RequestFocusFire(Tank, dingus.SourceTank.visible);
                    }
                }
                catch { }//cant always get dingus source
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
                mind.Provoked = ProvokeTime;
                mind.AIControl.PendingSystemsCheck = true;
                if (mind.BoltsQueued == 0 && ManNetwork.IsHost)
                {   // do NOT destroy blocks on split Techs!
                    if (!blockLoss.GetComponent<ModuleTechController>())
                    {
                        if ((bool)mind.TechMemor)
                        {   // cannot self-destruct timer cabs or death
                            if (mind.TechMemor.ChanceGrabBackBlock(blockLoss))
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

        public void GetRevengeOn(Visible target = null, bool forced = false)
        {
            try
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
                    if (Range == KickStart.DefaultEnemyRange)
                    {
                        Range = 500;
                    }
                    PursuingTarget = true;
                    Invoke("EndAggro", 2);
                }
            }
            catch { }
        }
        public void EndAggro()
        {
            try
            {
                if (PursuingTarget)
                {
                    if (Tank.blockman.IterateBlockComponents<ModuleItemHolderBeam>().Count() != 0)
                        CommanderMind = EnemyAttitude.Miner;
                    if (Range == 500)
                    {
                        Range = KickStart.DefaultEnemyRange;
                    }
                    PursuingTarget = false;
                }
            }
            catch { }
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
            if (CommanderAttack == EnemyAttack.Spyper) inRange = SpyperMaxRange;
            else if (inRange <= 0) inRange = Range;
            float TargetRange = inRange;
            Vector3 scanCenter = Tank.boundsCentreWorldNoCheck;

            if (target != null)
            {
                if (!target.isActive || (target.tank.boundsCentreWorldNoCheck - scanCenter).magnitude > TargetRange)
                {
                    Debug.Log("Target lost");
                    target = null;
                }
                else if (TargetLockDuration >= 0)
                {
                    TargetLockDuration -= KickStart.AIClockPeriod;
                    return target;
                }
            }

            if (PursuingTarget) // Carry on chasing the target
                return target;

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
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
                        if (dist < TargetRange)
                        {
                            target = cTank.visible;
                        }
                    }
                }
                TargetLockDuration = PestererSwitchDelay;
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
                            float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
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
                            float dist = (cTank.boundsCentreWorldNoCheck - Tank.boundsCentreWorldNoCheck).magnitude;
                            if (cTank.blockman.blockCount < BlockCount && dist < TargetRange)
                            {
                                BlockCount = cTank.blockman.blockCount;
                                target = cTank.visible;
                            }
                        }
                    }
                }
                TargetLockDuration = ScanDelay;
            }
            else
            {
                TargetLockDuration = ScanDelay;
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
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                    {
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
                        if (dist < TargetRange)
                        {
                            TargetRange = dist;
                            target = cTank.visible;
                        }
                        else if (pos > 1 && dist < TargRange2)
                        {
                            TargRange2 = dist;
                            target2 = cTank.visible;
                        }
                        else if (pos > 2 && dist < TargRange3)
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

            if (PursuingTarget) // Carry on chasing the target
                return target;

            // We begin the search
            if (CommanderAttack == EnemyAttack.Spyper) inRange = SpyperMaxRange;
            else if (inRange <= 0) inRange = 500;
            float TargetRange = inRange;
            Vector3 scanCenter = Tank.boundsCentreWorldNoCheck;

            if (target != null)
            {
                if ((target.tank.boundsCentreWorldNoCheck - scanCenter).magnitude > TargetRange)
                    target = null;
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
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
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
                            float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
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
                            float dist = (cTank.boundsCentreWorldNoCheck - Tank.boundsCentreWorldNoCheck).magnitude;
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
                for (int step = 0; step < launchCount; step++)
                {
                    Tank cTank = techs.ElementAt(step);
                    if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                    {
                        float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
                        if (dist < TargetRange)
                        {
                            TargetRange = dist;
                            target = cTank.visible;
                        }
                        else if (pos > 1 && dist < TargRange2)
                        {
                            TargRange2 = dist;
                            target2 = cTank.visible;
                        }
                        else if (pos > 2 && dist < TargRange3)
                        {
                            TargRange3 = dist;
                            target3 = cTank.visible;
                        }
                    }
                }
                if (pos >= 3)
                    return target3;
                if (pos == 2)
                    return target2;
            }
            TargetLockDuration = ScanDelay;
            return target;
        }
    }
}
