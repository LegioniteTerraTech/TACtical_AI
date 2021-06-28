using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public static class RCore
    {
        public class EnemyMind : MonoBehaviour
        {   // Where the brain is handled for enemies
            // ESSENTIALS
            private Tank Tank;
            private AIECore.TankAIHelper AIControl;
            public AIERepair.DesignMemory TechMemor;

            // Set on spawn
            public EnemyHandling EvilCommander = EnemyHandling.Wheeled;
            public EnemyAttitude CommanderMind = EnemyAttitude.Default;
            public EnemyAttack CommanderAttack = EnemyAttack.Circle;
            public EnemySmarts CommanderSmarts = EnemySmarts.Default;
            public EnemyBolts CommanderBolts = EnemyBolts.Default;

            public FactionSubTypes MainFaction = FactionSubTypes.GSO;
            public bool StartedAnchored = false;
            public bool AllowRepairsOnFly = false;  // If we are feeling extra evil
            public bool InvertBullyPriority = false;// Shoot the big techs instead

            public bool SolarsAvail = false;        // Do whe currently have solar panels
            public bool Provoked = false;           // Were we hit from afar?
            public bool Hurt = false;               // Are we damaged?
            public int Range = 250;                 // Aggro range
            public int TargetLockDuration = 0;
            public Vector3 HoldPos = Vector3.zero;

            internal bool remove = false;

            public void SetForRemoval()
            {
                if (gameObject.GetComponent<EnemyMind>().IsNotNull())
                {
                    Debug.Log("TACtical_AI: Removing Enemy AI for " + Tank.name);
                    remove = true;
                    if (gameObject.GetComponent<AIERepair.DesignMemory>().IsNotNull())
                        gameObject.GetComponent<AIERepair.DesignMemory>().Remove();
                    DestroyImmediate(this);
                }
            }
            public void Initiate()
            {
                remove = false;
                Tank = gameObject.GetComponent<Tank>();
                AIControl = gameObject.GetComponent<AIECore.TankAIHelper>();
                Tank.DamageEvent.Subscribe(OnHit);
                Tank.DetachEvent.Subscribe(OnBlockLoss);
                try
                {
                    MainFaction = Tank.GetMainCorp();   //Will help determine their Attitude
                }
                catch 
                {   // can't always get this 
                    MainFaction = FactionSubTypes.GSO;
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
                }
                catch { }
            }

            /// <summary>
            ///  Gets the enemy position based on 
            /// </summary>
            /// <param name="inRange">value > 0</param>
            /// <param name="pos">MAX 3</param>
            /// <returns></returns>
            public Visible FindEnemy(float inRange = 0, int pos = 1)
            {
                Visible target = AIControl.lastEnemy;
                if (inRange <= 0) inRange = Range;
                float TargetRange = Mathf.Pow(inRange, 2);

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
                            if (cTank.IsEnemy(Tank.Team))
                            {
                                target = cTank.visible;
                            }
                        }
                        TargetLockDuration = 250;
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
                            if (cTank.IsEnemy(Tank.Team))
                            {
                                float dist = (cTank.boundsCentreWorldNoCheck - Tank.boundsCentreWorldNoCheck).sqrMagnitude;
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
                            if (cTank.IsEnemy(Tank.Team))
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
                }
                else
                {
                    float TargRange2 = TargetRange;
                    float TargRange3 = TargetRange;

                    Visible target2 = null;
                    Visible target3 = null;

                    int launchCount = techs.Count();
                    for (int step = 0; step < launchCount; step++)
                    {
                        Tank cTank = techs.ElementAt(step);
                        if (cTank.IsEnemy(Tank.Team))
                        {
                            float dist = (cTank.boundsCentreWorldNoCheck - Tank.boundsCentreWorldNoCheck).sqrMagnitude;
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
                    if (pos > 2)
                        return target3;
                    if (pos > 1)
                        return target2;
                }
                return target;
            }
        }


        public static void RunEvilOperations(AIECore.TankAIHelper thisInst, Tank tank)
        {
            var Mind = tank.gameObject.GetComponent<EnemyMind>();
            if (Mind.IsNull())
            {
                RandomizeBrain(thisInst, tank);
                return;
            }
            if (Mind.remove)
            {
                return;
            }

            RBolts.ManageBolts(thisInst, tank, Mind);
            if (Mind.CommanderAttack == EnemyAttack.Grudge)
            {
                if (thisInst.lastEnemy.IsNull())
                    BGeneral.AidDefend(thisInst, tank);
            }
            else
            {
                BGeneral.AidDefend(thisInst, tank);
            }
            if (Mind.AllowRepairsOnFly)
            {
                bool venPower = false;
                if (Mind.MainFaction == FactionSubTypes.VEN) venPower = true;
                RRepair.EnemyRepairStepper(thisInst, tank, Mind, 50, venPower);// longer while fighting
            }
            if (Mind.EvilCommander != EnemyHandling.Stationary)
            {
                switch (Mind.CommanderAttack)
                {
                    case EnemyAttack.Coward:
                        RGeneral.SelfDefense(thisInst, tank, Mind);
                        break;
                    case EnemyAttack.Spyper:
                        RGeneral.AimAttack(thisInst, tank, Mind);
                        break;
                    default:
                        RGeneral.AidAttack(thisInst, tank, Mind);
                        break;
                }
            }
            switch (Mind.EvilCommander)
            {
                case EnemyHandling.Wheeled:
                    thisInst.PursueThreat = true;
                    RWheeled.TryAttack(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Airplane:
                    //awaiting coding
                    break;
                case EnemyHandling.Chopper:
                    //awaiting coding, Starship but pid
                    thisInst.PursueThreat = true;
                    RStarship.TryAttack(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Starship:
                    thisInst.PursueThreat = true;
                    RStarship.TryAttack(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Naval:
                    thisInst.PursueThreat = true;
                    RNaval.TryAttack(thisInst, tank, Mind);
                    break;
                case EnemyHandling.SuicideMissile:
                    thisInst.PursueThreat = true;
                    RSuicideMissile.RamTillDeath(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Stationary:
                    thisInst.PursueThreat = true;
                    RGeneral.AimAttack(thisInst, tank, Mind);
                    RStation.HoldPosition(thisInst, tank, Mind);
                    break;
                case EnemyHandling.Boss:
                    //awaiting coding, no plans yet for coding
                    thisInst.PursueThreat = true;
                    RStarship.TryAttack(thisInst, tank, Mind);
                    break;
            }
            //CommanderMind is handled in each seperate class
        }
        public static bool SetSmartAIStats(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind toSet)
        {
            bool fired = false;
            var BM = tank.blockman;
            //Determine driving method
            if (tank.IsAnchored)
            {
                toSet.EvilCommander = EnemyHandling.Stationary;
            }
            else if (BM.IterateBlockComponents<ModuleHover>().Count() > 2 || BM.IterateBlockComponents<ModuleAntiGravityEngine>().Count() > 0)
            {
                toSet.EvilCommander = EnemyHandling.Starship;
            }
            else if (BM.IterateBlockComponents<ModuleGyro>().Count() > 1 && BM.IterateBlockComponents<ModuleBooster>().Count() > 0)
            {
                toSet.EvilCommander = EnemyHandling.Naval;
            }
            else if (BM.IterateBlockComponents<ModuleWing>().Count() > 3 && BM.IterateBlockComponents<ModuleBooster>().Count() > 0)
            {
                toSet.EvilCommander = EnemyHandling.Airplane;
            }
            else if (BM.IterateBlockComponents<ModuleWeapon>().Count() < 3 && BM.IterateBlockComponents<ModuleBooster>().Count() > 0)
            {
                toSet.EvilCommander = EnemyHandling.SuicideMissile;
            }
            else
                toSet.EvilCommander = EnemyHandling.Wheeled;


            //Determine Attitude
            if (BM.IterateBlockComponents<ModuleWeaponGun>().Count() < BM.IterateBlockComponents<ModuleDrill>().Count() || toSet.MainFaction == FactionSubTypes.GC)
            {
                // Miner
                toSet.CommanderMind = EnemyAttitude.Miner;
                toSet.CommanderAttack = EnemyAttack.Coward;
                fired = true;
            }
            else if (BM.GetBlockWithID((uint)BlockTypes.HE_CannonBattleship_216).IsNotNull() || BM.GetBlockWithID((uint)BlockTypes.GSOBigBertha_845).IsNotNull())
            {   
                // Artillery
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Spyper;
                fired = true;
            }
            else if (toSet.MainFaction == FactionSubTypes.VEN)
            {
                // Carrier
                toSet.CommanderMind = EnemyAttitude.Default;
                toSet.CommanderAttack = EnemyAttack.Circle;
                fired = true;
            }
            else if (toSet.MainFaction == FactionSubTypes.HE)
            {
                // Carrier
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Grudge;
                fired = true;
            }
            else if (BM.IterateBlockComponents<ModuleWeapon>().Count() > 50)
            {
                // Over-armed
                toSet.CommanderMind = EnemyAttitude.Homing;
                toSet.CommanderAttack = EnemyAttack.Bully;
                fired = true;
            }
            return fired;
        }


        public static void RandomizeBrain(AIECore.TankAIHelper thisInst, Tank tank)
        {
            if (!tank.gameObject.GetComponent<EnemyMind>())
                tank.gameObject.AddComponent<EnemyMind>();
            thisInst.lastPlayer = null;

            var toSet = tank.gameObject.GetComponent<EnemyMind>();
            toSet.HoldPos = tank.boundsCentreWorldNoCheck;
            toSet.Initiate();

            bool isMissionTech = RMission.TryHandleMissionAI(thisInst, tank, toSet);
            if (isMissionTech)
            {
                if (toSet.CommanderSmarts >= EnemySmarts.Smrt)
                {
                    toSet.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                    if (toSet.TechMemor.IsNull())
                        toSet.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                    toSet.TechMemor.Initiate();
                    toSet.TechMemor.SaveTech();
                }
                return;
            }

            if (tank.Anchors.NumAnchored > 0)
                toSet.StartedAnchored = true;
            /*
            try
            {
                ControlSchemeCategory Schemer = tank.control.ActiveScheme.Category;
                switch (Schemer)
                {
                    case ControlSchemeCategory.Car:
                        toSet.EvilCommander = EnemyHandling.Wheeled;
                        break;
                    case ControlSchemeCategory.Aeroplane:
                        toSet.EvilCommander = EnemyHandling.Airplane;
                        break;
                    case ControlSchemeCategory.Helicopter:
                        toSet.EvilCommander = EnemyHandling.Chopper;
                        break;
                    case ControlSchemeCategory.AntiGrav:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Rocket:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    case ControlSchemeCategory.Hovercraft:
                        toSet.EvilCommander = EnemyHandling.Starship;
                        break;
                    default:
                        tank.control.sch
                        string name = tank.control.ActiveScheme.CustomName;
                        if (name == "Ship" || name == "ship" || name == "Naval" || name == "naval" || name == "Boat" || name == "boat")
                        {
                            toSet.EvilCommander = EnemyHandling.Naval;
                        }
                        //Else we just default to Wheeled
                        break;
                }
            }
            catch { }//some population techs are devoid of schemes
            */
            //add Smartness
            int randomNum = UnityEngine.Random.Range(KickStart.LowerDifficulty, KickStart.UpperDifficulty);
            if (randomNum < 35)
                toSet.CommanderSmarts = EnemySmarts.Default;
            else if (randomNum < 60)
                toSet.CommanderSmarts = EnemySmarts.Mild;
            else if (randomNum < 80)
                toSet.CommanderSmarts = EnemySmarts.Meh;
            else if (randomNum < 92)
                toSet.CommanderSmarts = EnemySmarts.Smrt;
            else
                toSet.CommanderSmarts = EnemySmarts.IntAIligent;
            if (randomNum > 98)
            {
                toSet.AllowRepairsOnFly = true;//top 2
                toSet.InvertBullyPriority = true;
            }

            if (toSet.CommanderSmarts > EnemySmarts.Meh)
            {
                toSet.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (toSet.TechMemor.IsNull())
                    toSet.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                toSet.TechMemor.Initiate();
                toSet.TechMemor.SaveTech();
                toSet.CommanderBolts = EnemyBolts.AtFullOnAggro;// allow base function
            }


            bool setEnemy = SetSmartAIStats(thisInst, tank, toSet);
            if (!setEnemy)
            {
                //add Attitude
                int randomNum2 = UnityEngine.Random.Range(1, 4);
                switch (randomNum2)
                {
                    case 1:
                        toSet.CommanderMind = EnemyAttitude.Default;
                        break;
                    case 2:
                        toSet.CommanderMind = EnemyAttitude.Homing;
                        break;
                    case 3:
                        toSet.CommanderMind = EnemyAttitude.Junker;
                        break;
                    case 4:
                        toSet.CommanderMind = EnemyAttitude.Miner;
                        break;
                }
                //add Attack
                int randomNum3 = UnityEngine.Random.Range(1, 6);
                switch (randomNum3)
                {
                    case 1:
                        toSet.CommanderAttack = EnemyAttack.Circle;
                        break;
                    case 2:
                        toSet.CommanderAttack = EnemyAttack.Grudge;
                        break;
                    case 3:
                        toSet.CommanderAttack = EnemyAttack.Coward;
                        break;
                    case 4:
                        toSet.CommanderAttack = EnemyAttack.Bully;
                        break;
                    case 5:
                        toSet.CommanderAttack = EnemyAttack.Pesterer;
                        break;
                    case 6:
                        toSet.CommanderAttack = EnemyAttack.Spyper;
                        break;
                }
            }
            Debug.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  " + toSet.EvilCommander.ToString() + " based enemy with attitude " + toSet.CommanderAttack.ToString() + " | Mind " + toSet.CommanderMind.ToString() + " | Smarts " + toSet.CommanderSmarts.ToString() + " inbound!");
        }

        public static void BeEvil(AIECore.TankAIHelper thisInst, Tank tank)
        {
            //Debug.Log("TACtical_AI: enemy AI active!");
            RunEvilOperations(thisInst, tank);
            ScarePlayer(tank);
        }
        public static void ScarePlayer(Tank tank)
        {
            //Debug.Log("TACtical_AI: enemy AI active!");
            var tonk = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (tonk.IsNotNull())
            {
                bool player = tonk.tank.IsPlayer;
                if (player)
                {
                    Singleton.Manager<ManMusic>.inst.SetDanger(ManMusic.DangerContext.Circumstance.Enemy, tank, tonk.tank);
                }
            }
        }
    }
}
