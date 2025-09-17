using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TAC_AI.Templates;

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
            public TankAIHelper AIControl;
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
                AIControl = gameObject.GetComponent<TankAIHelper>();
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

        internal static bool SpecificNameCases(TankAIHelper helper, Tank tank, EnemyMind mind)
        {   // Handle specific enemy names to tailor the AI into working order
            int name = tank.name.GetHashCode();
            bool DidFire = false;

            if (name == "Missile Defense".GetHashCode())
            {   // The GSO Missile Turret
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EAttackMode.Strong;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.Mild;
                DidFire = true;
            }
            else if (name == "Wing-nut".GetHashCode())
            {   // Wing-nut mission
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EAttackMode.Strong;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                DidFire = true;
            }
            else if (name == "Spider King".GetHashCode())
            {   // Spider King mission
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EAttackMode.Strong;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                DidFire = true;
            }
            else if (name == "Fly".GetHashCode())
            {   // Spider King mission
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Starship;
                mind.CommanderAttack = EAttackMode.Random;
                mind.CommanderMind = EnemyAttitude.Homing;
                RCore.AutoSetIntelligence(mind);
                DidFire = true;
            }
            else if (name == "Enemy HQ".GetHashCode())
            {   //Base where enemies spawn from
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EAttackMode.Strong;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.AtFull;
                DidFire = true;
            }
            else if (name == "Missile #2".GetHashCode())
            {
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.SuicideMissile;
                mind.CommanderAttack = EAttackMode.Chase;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.MissionTrigger;
                DidFire = true;
            }
            else if (name == "DPS Target".GetHashCode())
            {   // R&D Target
                mind.AIControl.RunState = AIRunState.Default;
                mind.StartedAnchored = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EAttackMode.Safety;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.Default;
                mind.CommanderBolts = EnemyBolts.MissionTrigger;
                DidFire = true;
            }
            /*
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
            */

            return DidFire;
        }

        internal static bool SetupBaseOrMissionAI(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            string name = tank.name;
            // Don't worry the bases are sorted based on if they are valid or not
            bool DidFire = RLoadedBases.SetupBaseAI(helper, tank, mind);
            if (!(bool)tank)
                return true;
            tank.AI.TryGetCurrentAIType(out AITreeType.AITypes tree1);
            DebugTAC_AI.LogAISetup(KickStart.ModID + ": AI " + tank.name + ":  AI Tree is " + tree1.ToString());

            if (!DidFire)
            {
                if (name.Contains('Ω'))
                {   // Base host NPC
                    mind.CommanderMind = EnemyAttitude.NPCBaseHost;
                    DidFire = true;
                }
                else if (name.Contains('⦲'))
                {   // Boss
                    mind.CommanderMind = EnemyAttitude.Boss;
                    DidFire = true;
                }
                else if (SpecificNameCases(helper, tank, mind))
                {
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
                            mind.CommanderAttack = EAttackMode.Safety;
                            mind.CommanderMind = EnemyAttitude.Homing;
                            RCore.AutoSetIntelligence(mind);
                            DidFire = true;
                        }
                        else if (tree == AITreeType.AITypes.ChargeAtSKU)
                        {   // setup for Sumo 
                            mind.AllowRepairsOnFly = false;
                            mind.EvilCommander = EnemyHandling.Wheeled;
                            mind.CommanderAttack = EAttackMode.Chase;
                            mind.CommanderMind = EnemyAttitude.Homing;
                            mind.CommanderSmarts = EnemySmarts.IntAIligent;
                            DidFire = true;
                        }
                        else if (tree == AITreeType.AITypes.Invader)
                        {   // setup for Invaders
                            //   Who needs a timer anyways?  Let's just attack when the player gets close.
                            mind.AllowRepairsOnFly = false;
                            RCore.GetOrCalculateEnemyHandling(tank, mind);
                            if (KickStart.Difficulty > 100)// in Soviet GSO, Invader come to you
                            {
                                mind.CommanderAttack = EAttackMode.Ranged;
                                mind.CommanderMind = EnemyAttitude.Invader;
                            }
                            else
                            {
                                mind.CommanderAttack = EAttackMode.Chase;
                                mind.CommanderMind = EnemyAttitude.Invader;
                            }
                            RCore.AutoSetIntelligence(mind);
                            DidFire = true;
                        }
                        else if (tree == AITreeType.AITypes.Specific || tree == AITreeType.AITypes.FacePlayer)
                        {   // setup for idk
                            helper.RunState = AIRunState.Default;
                            DidFire = true;
                        }
                    }
                }
            }

            if (name.Contains('⟰'))
            {   // Spawned as a Tech Fragment
                mind.BuildAssist = true;
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

        public static void MissionHandler(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            var AISettings = tank.GetComponent<AIBookmarker>();
            if (AISettings.IsNotNull())
            {
                mind.EvilCommander = AISettings.commander;
                mind.CommanderAttack = AISettings.attack;
                mind.CommanderBolts = AISettings.bolts;
                mind.CommanderMind = AISettings.attitude;
                mind.CommanderSmarts = AISettings.smarts;
                UnityEngine.Object.DestroyImmediate(AISettings);
            }
            return;
        }
        public static bool ADVMissionHandler(TankAIHelper helper, Tank tank, EnemyMind mind)
        {
            return true;
        }
    }
}
