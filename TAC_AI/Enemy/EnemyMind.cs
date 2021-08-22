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

        public FactionSubTypes MainFaction = FactionSubTypes.GSO;   // Extra for determining mentality on auto-generation
        public bool StartedAnchored = false;    // Do we stay anchored?
        public bool AllowRepairsOnFly = false;  // If we are feeling extra evil
        public bool InvertBullyPriority = false;// Shoot the big techs instead
        public bool AllowInvBlocks = false;     // Can this tech spawn blocks from inventory?
        public bool LikelyMelee = false;        // Can we melee?

        public bool SolarsAvail = false;        // Do we currently have solar panels
        public bool Provoked = false;           // Were we hit from afar?
        public bool Hurt = false;               // Are we damaged?
        public int Range = KickStart.DefaultEnemyRange;// Aggro range
        public int TargetLockDuration = 0;      // For pesterer's random target swatching
        public Vector3 HoldPos = Vector3.zero;  // For stationary techs like Wingnut who must hold ground

        internal bool queueRemove = false;
        internal const float SpyperMaxRange = 450;
        internal const float SpacingRange = 8;
        internal const float SpacingRangeAir = 16;

        internal int BoltsQueued = 0;

        public void Initiate()
        {
            queueRemove = false;
            Tank = gameObject.GetComponent<Tank>();
            //Debug.Log("TACtical_AI: Launching Enemy AI for " + Tank.name);
            AIControl = gameObject.GetComponent<AIECore.TankAIHelper>();
            EnemyOpsController = new EnemyOperationsController(this);
            Tank.DamageEvent.Subscribe(OnHit);
            Tank.DetachEvent.Subscribe(OnBlockLoss);
            AIControl.MovementController.UpdateEnemyMind(this);
            AIControl.AvoidStuff = true;
            try
            {
                MainFaction = Tank.GetMainCorp();   //Will help determine their Attitude
            }
            catch
            {   // can't always get this 
                MainFaction = FactionSubTypes.GSO;
            }
        }
        public void SetForRemoval()
        {
            if (gameObject.GetComponent<EnemyMind>().IsNotNull())
            {
                //Debug.Log("TACtical_AI: Removing Enemy AI for " + Tank.name);
                queueRemove = true;
                if (gameObject.GetComponent<AIERepair.DesignMemory>().IsNotNull())
                    gameObject.GetComponent<AIERepair.DesignMemory>().Remove();
                AIControl.MovementController.UpdateEnemyMind(null);
                DestroyImmediate(this);
            }
        }
        
        public void OnHit(ManDamage.DamageInfo dingus)
        {
            if (dingus.Damage > 100)
            {
                //Tank.visible.KeepAwake();
                Hurt = true;
                Provoked = true;
                AIControl.FIRE_NOW = true;
                try
                {
                    AIControl.lastEnemy = dingus.SourceTank.visible;
                    AIControl.lastDestination = dingus.SourceTank.boundsCentreWorldNoCheck;
                }
                catch { }//cant always get dingus source
            }
        }
        public static void OnBlockLoss(TankBlock blockLoss, Tank tonk)
        {
            try
            {
                var mind = tonk.GetComponent<EnemyMind>();
                mind.AIControl.FIRE_NOW = true;
                mind.Hurt = true;
                mind.AIControl.PendingSystemsCheck = true;
                if (mind.BoltsQueued == 0)
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
                    blockLoss.damage.SelfDestruct(0.6f); // - no get illegal blocks
                }
                else
                {
                    if (UnityEngine.Random.Range(0, 99) >= KickStart.EnemyBlockDropChance)
                    {
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



        /// <summary>
        ///  Gets the enemy position based on current position and AI preferences
        /// </summary>
        /// <param name="inRange">value > 0</param>
        /// <param name="pos">MAX 3</param>
        /// <returns></returns>
        public Visible FindEnemy(float inRange = 0, int pos = 1)
        {
            if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
                return null; // We NO ATTACK
            Visible target = AIControl.lastEnemy;

            if (CommanderAttack == EnemyAttack.Spyper) inRange = SpyperMaxRange;
            else if (inRange <= 0) inRange = Range;
            float TargetRange = inRange;
            Vector3 scanCenter = Tank.boundsCentreWorldNoCheck;

            if (target != null)
            {
                if (!target.isActive || (target.tank.boundsCentreWorldNoCheck - scanCenter).magnitude > TargetRange)
                    target = null;
            }

            List<Tank> techs = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
            if (CommanderAttack == EnemyAttack.Pesterer)
            {
                if (TargetLockDuration <= 0)
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
                    TargetLockDuration = 50;
                }
                else if (target.IsNotNull())
                {
                    if (!target.isActive)
                    {
                        int max = techs.Count();
                        int launchCount = UnityEngine.Random.Range(0, max);
                        for (int step = 0; step < launchCount; step++)
                        {
                            Tank cTank = techs.ElementAt(step);
                            if (cTank.IsEnemy(Tank.Team) && cTank != Tank)
                            {
                                float dist = (cTank.boundsCentreWorldNoCheck - scanCenter).magnitude;
                                if (dist < TargetRange)
                                {
                                    target = cTank.visible;
                                }
                            }
                        }
                        TargetLockDuration = 50;
                    }
                }
                TargetLockDuration--;
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
            if (CommanderMind == EnemyAttitude.SubNeutral && EvilCommander != EnemyHandling.SuicideMissile)
                return null; // We NO ATTACK
            Visible target = AIControl.lastEnemy;

            if (CommanderAttack == EnemyAttack.Spyper) inRange = SpyperMaxRange;
            else if (inRange <= 0) inRange = 500;
            float TargetRange = inRange;
            Vector3 scanCenter = Tank.boundsCentreWorldNoCheck;

            if (target != null)
            {
                if ((target.tank.boundsCentreWorldNoCheck - scanCenter).magnitude > TargetRange)
                    target = null;
            }
            float altitudeHigh = -256;

            List<Tank> techs = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
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
            /*
            if (target.IsNull())
            {
                Debug.Log("TACtical_AI: Tech " + Tank.name + " Could not find target with FindEnemy, resorting to defaults");
                return Tank.Vision.GetFirstVisibleTechIsEnemy(Tank.Team);
            }
            */
            return target;
        }
    }
}
