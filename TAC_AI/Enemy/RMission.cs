using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI.Enemy
{
    public static class RMission
    {
        /// <summary>
        /// N/A until MissionManager exists
        /// </summary>
        public class OnRailsActions : MonoBehaviour
        {   // Will sit on standby for MissionManager
            public Tank Tank;
            public AIECore.TankAIHelper AIControl;
            public int MissionAIID = 0;


            //public MissionManager.Mission Mission;
            public static Event<Tank, OnRailsActions> MissionAIStatus;


            public static void Initiate()
            {
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(TankDestruction);
            }
            public static void TankDestruction(Tank tank, ManDamage.DamageInfo oof)
            {
                var rails = tank.GetComponent<OnRailsActions>();
                if (rails.IsNotNull())
                    MissionAIStatus.Send(tank, rails);
            }

            public void InitiateForTank()
            {
                Tank = gameObject.GetComponent<Tank>();
                AIControl = gameObject.GetComponent<AIECore.TankAIHelper>();
                //Tank.DamageEvent.Subscribe(OnHit);
                //Tank.DetachEvent.Subscribe(OnBlockLoss);
            }
            public void Remove()
            {
                //Tank.DamageEvent.Unsubscribe(OnHit);
                //Tank.DetachEvent.Unsubscribe(OnBlockLoss);
                DestroyImmediate(this);
            }
            public void Reset()
            {
            }
            public static void OnHit(Tank tank, OnRailsActions mAIState)
            {   // compile relivant information here and deliver it to the MissionManager
                MissionAIStatus.Send(tank, mAIState);
            }
            public static void ArrivalAtDest(Tank tank, OnRailsActions mAIState)
            {   // compile relivant information here and deliver it to the MissionManager
                MissionAIStatus.Send(tank, mAIState);
            }
        }
        public static bool SetupMissionAI(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            string name = tank.name;
            bool DidFire = RBases.SetupBaseAI(thisInst, tank, mind);
            if (!DidFire)
            {
                if (name == "Missile Defense")
                {
                    mind.AllowRepairsOnFly = true;
                    mind.EvilCommander = EnemyHandling.Stationary;
                    mind.CommanderAttack = EnemyAttack.Bully;
                    mind.CommanderMind = EnemyAttitude.Homing;
                    mind.CommanderSmarts = EnemySmarts.Mild;
                    DidFire = true;
                }
                else if (name == "Wingnut")
                {
                    mind.AllowRepairsOnFly = true;
                    mind.InvertBullyPriority = true;
                    mind.EvilCommander = EnemyHandling.Stationary;
                    mind.CommanderAttack = EnemyAttack.Bully;
                    mind.CommanderMind = EnemyAttitude.Homing;
                    mind.CommanderSmarts = EnemySmarts.IntAIligent;
                    DidFire = true;
                }
                else if (name == "Enemy HQ")
                {   //Base where enemies spawn from
                    mind.AllowInvBlocks = true;
                    mind.AllowRepairsOnFly = true;
                    mind.InvertBullyPriority = true;
                    mind.EvilCommander = EnemyHandling.Stationary;
                    mind.CommanderAttack = EnemyAttack.Bully;
                    mind.CommanderMind = EnemyAttitude.Homing;
                    mind.CommanderSmarts = EnemySmarts.IntAIligent;
                    mind.CommanderBolts = EnemyBolts.AtFull;
                    DidFire = true;
                }
                else if (name == "Missile #2")
                {
                    mind.AllowRepairsOnFly = true;
                    mind.InvertBullyPriority = true;
                    mind.EvilCommander = EnemyHandling.SuicideMissile;
                    mind.CommanderAttack = EnemyAttack.Grudge;
                    mind.CommanderMind = EnemyAttitude.Homing;
                    mind.CommanderSmarts = EnemySmarts.IntAIligent;
                    mind.CommanderBolts = EnemyBolts.MissionTrigger;
                    DidFire = true;
                }

                else if (name == "TAC InvaderAttract")
                {
                    mind.AllowInvBlocks = true;
                    mind.AllowRepairsOnFly = true;
                    mind.InvertBullyPriority = true;
                    mind.EvilCommander = EnemyHandling.Starship;
                    mind.CommanderAttack = EnemyAttack.Grudge;
                    mind.CommanderMind = EnemyAttitude.Homing;
                    mind.CommanderSmarts = EnemySmarts.IntAIligent;
                    mind.CommanderBolts = EnemyBolts.MissionTrigger;
                    DidFire = true;
                }
                else
                {
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes tree))
                    {
                        if (tree == AITreeType.AITypes.Flee)
                        {   // setup for runner 
                            mind.AllowRepairsOnFly = true;
                            mind.EvilCommander = EnemyHandling.Wheeled;
                            mind.CommanderAttack = EnemyAttack.Coward;
                            mind.CommanderMind = EnemyAttitude.Homing;
                            RCore.AutoSetIntelligence(mind, tank);
                            DidFire = true;
                        }
                        else if (tree == AITreeType.AITypes.ChargeAtSKU)
                        {   // setup for Sumo 
                            mind.AllowRepairsOnFly = false;
                            mind.EvilCommander = EnemyHandling.Wheeled;
                            mind.CommanderAttack = EnemyAttack.Grudge;
                            mind.CommanderMind = EnemyAttitude.Homing;
                            mind.CommanderSmarts = EnemySmarts.IntAIligent;
                            DidFire = true;
                        }
                        else if (tree == AITreeType.AITypes.Invader)
                        {   // setup for Invaders
                            //   Who needs a timer anyways?  Let's just attack when the player gets close.
                            mind.AllowRepairsOnFly = false;
                            RCore.BlockSetEnemyHandling(tank, mind);
                            if (KickStart.Difficulty > 100)// in Soviet GSO, Invader come to you
                            {
                                mind.CommanderAttack = EnemyAttack.Spyper;
                                mind.CommanderMind = EnemyAttitude.Invader;
                            }
                            else
                            {
                                mind.CommanderAttack = EnemyAttack.Grudge;
                                mind.CommanderMind = EnemyAttitude.Invader;
                            }
                            RCore.AutoSetIntelligence(mind, tank);
                            DidFire = true;
                        }
                        else if (tree == AITreeType.AITypes.Specific)
                        {   // setup for idk
                            thisInst.Hibernate = true;
                            DidFire = true;
                        }
                    }
                }
            }

            if (DidFire && mind.CommanderMind == EnemyAttitude.OnRails)
            {
                var rails = tank.GetComponent<OnRailsActions>();
                if (rails.IsNull())
                {
                    rails = tank.gameObject.AddComponent<OnRailsActions>();
                    rails.InitiateForTank();
                }

                rails.Reset();

            }
            else   // remove uneeded module
            {
                var rails = tank.GetComponent<OnRailsActions>();
                if (rails.IsNotNull())
                    rails.Remove();
            }

            return DidFire;
        }

        public static bool MissionHandler(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            return true;
        }
    }
}
