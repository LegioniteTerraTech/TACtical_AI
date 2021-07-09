﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TAC_AI.AI
{
    /*
        Summary of functions: Handles Allied AI, and enemy AI if TougherEnemies is installed or overriden to do so.
        
        AI Control is handled with 4 trees: 
          AI Set Types and Attitudes - Tells the AI which Designator to utilize
            VVVVVV
          AI Action Designator - Handles the destinations the AI should drive to and how to do it (Classes like BEscort or RWheeled)
            VVVVVV
          AI Movement Director - Tells the AI how to navigate safely and avoid obsticles along the way
            VVVVVV
          AI Movement Maintainer - Makes the AI drive to the director's coordinates

          As such, it's important to note that:
            AI Set Types and Attitudes - fires on change and on spawn/load
            Action Designators - must fire constantly (but can be slowed) to maintain consistant operation
            Movement Directors - are the major CPU bottlenecks of this mod
            Movement Maintainers - must be fired every Update to prevent AIs from bugging out on drive/fire operations

        Important to note that this Allied AI will not fire Explosive Bolts under any cirumstances. 
            The player should do that on their own accord as Explosive Bolts cost resources to make.
    */
    public class AIECore
    {
        public static List<Tank> Allies;
        //public static List<ResourceDispenser> Minables;
        public static List<Visible> Minables;
        public static List<Transform> Depots;
        public static bool moreThan2Allies;
        private static int lastTechCount = 0;

        public enum DediAIType
        {   //like the old plans, we make the AI do stuff
            // COMBAT
            Escort,     // Good ol' player defender                     (Classic player defense numbnut)
            Assault,    // Run off and attack the enemies on your radar (Runs off (beyond radar range!) to attack enemies)
            Aegis,      // Protects the nearest non-player allied Tech  (Follows nearest ally, will chase enemy some distance)

            // RESOURCES
            Prospector, // Harvest Chunks and return them to base       (Returns chunks when full to nearest receiver)
            Scrapper,   // Grab loose blocks but avoid combat           (Return to nearest base when threatened)
            Energizer,  // Charges up and/or heals other techs          (Return to nearest base when out of power)

            // MISC        (MultiTech) - BuildBeam disabled, will fire at any angle.
            MTTurret,   // Only turns to aim at enemy                   
            MTSlave,    // Does not move on own but does shoot back     
            MTMimic,    // Copies the actions of the closest non-MT Tech in relation     

            // ADVANCED    (REQUIRES TOUGHER ENEMIES TO USE!)           (can't just do the same without the enemies attacking these ways as well...)
            Aviator,    // Flies aircraft, death from above, nuff said  (Flies above ground, by the player and keeps distance) [unload distance will break!]
            Buccaneer,  // Sails ships amongst ye seas~                 (Avoids terrain above water level)
            Astrotech,  // Flies hoverships and kicks Tech              (Follows player a certain distance above ground level and can follow into the sky)
        }
        //All of their operating ranges are ultimately determined by the Tech's biggest provided vision/radar range.

        public static float Extremes(Vector3 input)
        {
            return Mathf.Max(Mathf.Max(input.x, input.y), input.z);
        }

        public static bool FetchClosestHarvestReceiver(Vector3 tankPos, float MaxScanRange, out Transform finalPos, out Tank theBase, int team = -2)
        {
            if (team == -2)
                team = Singleton.Manager<ManPlayer>.inst.PlayerTeam;
            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            foreach (Transform trans in Depots)
            {
                float temp = (trans.position - tankPos).sqrMagnitude;
                if (bestValue > temp && temp != 0 && trans.root.GetComponent<Tank>().Team == team)
                {
                    fired = true;
                    theBase = trans.root.GetComponent<Tank>();
                    bestValue = temp;
                    finalPos = trans;
                }
            }
            return fired;
        }

        public static bool FetchClosestResource(Vector3 tankPos, float MaxScanRange, out Visible theResource)
        {
            bool fired = false;
            theResource = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            int run = Minables.Count;
            for (int step = 0; step < run; step++)
            {
                var trans = Minables.ElementAt(step);
                if (!trans.gameObject.GetComponent<ResourceDispenser>().IsDeactivated)
                {
                    //Debug.Log("TACtical_AI:Skipped over inactive");
                    if (!trans.gameObject.GetComponent<Damageable>().Invulnerable)
                    {
                        //Debug.Log("TACtical_AI: Skipped over invincible");
                        float temp = (trans.centrePosition - tankPos).sqrMagnitude;
                        if (bestValue > temp && temp != 0)
                        {
                            theResource = trans;
                            fired = true;
                            bestValue = temp;
                        }
                        continue;
                    }
                }
                Minables.Remove(trans);//it's invalid and must be destroyed
                step--;
                run--;
            }
            return fired;
        }

        /*
        // Under Construction!
        public bool FetchLooseBlocks(Vector3 tankPos, float MaxScanRange, out Vector3 finalPos)
        {
            bool fired = false;
            finalPos = Vector3.zero;
            float bestValue = MaxScanRange;// MAX SCAN RANGE
            foreach (Transform trans in Visible.)
            {
                float temp = (trans.position - tankPos).sqrMagnitude;
                if (bestValue > temp && temp != 0)
                {
                    fired = true;
                    bestValue = temp;
                    finalPos = trans.position;
                }
            }
            return fired;
        }
        */

        public static bool AIMessage(bool hasMessaged, string message)
        {
            if (!hasMessaged)
            {
                hasMessaged = true;
                Debug.Log(message);
            }
            return hasMessaged;
        }

        public class TankAIManager : MonoBehaviour
        {
            public static void Initiate()
            {
                new GameObject("AIManager").AddComponent<TankAIManager>();
                Allies = new List<Tank>();
                Minables = new List<Visible>();
                Depots = new List<Transform>();
                Debug.Log("TACtical_AI: Created AIECore Manager.");
            }

            public static void FetchAllAllies()
            {
                Allies = new List<Tank>();
                int AllyCount = 0;
                var allTechs = Singleton.Manager<ManTechs>.inst;
                int techCount = allTechs.CurrentTechs.Count();
                List<Tank> techs = allTechs.CurrentTechs.ToList();
                moreThan2Allies = false;
                try
                {
                    for (int stepper = 0; techCount > stepper; stepper++)
                    {
                        if (techs.ElementAt(stepper).IsFriendly() && !techs.ElementAt(stepper).gameObject.GetComponent<TankAIHelper>().IsMultiTech)
                        {   //Exclude MTs from this event
                            Allies.Add(techs.ElementAt(stepper));
                            Debug.Log("TACtical_AI: Added " + Allies.ElementAt(AllyCount));
                            AllyCount++;
                        }
                    }
                    Debug.Log("TACtical_AI: Fetched allied tech list for AIs...");
                    if (AllyCount > 2)
                        moreThan2Allies = true;
                }
                catch  (Exception e)
                {
                    Debug.Log("TACtical_AI: Error on fetchlist");
                    Debug.Log(e);
                }
                if (AllyCount > 2)
                    moreThan2Allies = true;
                else
                    moreThan2Allies = false;
            }


            private void Update()
            {
                if (Singleton.Manager<ManTechs>.inst.Count != lastTechCount)
                {
                    lastTechCount = Singleton.Manager<ManTechs>.inst.Count;
                    FetchAllAllies();
                }
            }
        }


        public class TankAIHelper : MonoBehaviour
        {
            public Tank tank;
            public AITreeType.AITypes lastAIType;
            //Tweaks (controlled by Module)
            public DediAIType DediAI = DediAIType.Escort;    // Will we swap the Escort AI for the Harvest AI for this Tech?

            public List<ModuleAIExtension> AIList;
            public AIERepair.DesignMemory TechMemor;
            public AIEAirborne.AirAssistance Pilot;

            internal const int DodgeStrength = 60;  //The motivation in trying to move away from a tech in the way
                                                   //250
            
            public bool Hibernate = false;      // Disable the AI to make way for Default AI

            public bool IsMultiTech = false;    // Should the other AIs ignore collision with this Tech?
            public bool PursueThreat = true;    // Should the AI chase the enemy?
            public bool RequestBuildBeam = true;// Should the AI Auto-BuildBeam on flip?

            public bool FullMelee = false;      // Should the AI ram the enemy?
            public bool OnlyPlayerMT = false;   // Should the AI only follow player movement while in MT mode?
            public bool AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
            public bool SecondAvoidence = false;// Should the AI avoid two techs at once?
            public bool SideToThreat = false;   // Should the AI circle the enemy?

            public float RangeToChase = 100;    // How far should we pursue the enemy?
            public float RangeToStopRush = 50;  // The range the AI will linger from the player
            public float IdealRangeCombat = 25; // The range the AI will linger from the enemy if PursueThreat is true
            public int AnchorAimDampening = 45; // How much do we dampen anchor movements by?


            //AI Allowed types (self-filling)
            //   I'll convert these to flags later
            public bool isProspectorAvail = false;  //Is there a Prospector-enabled AI on this tech?
            public bool isScrapperAvail = false;    //Is there a Scrapper-enabled AI on this tech?
            public bool isAssassinAvail = false;    //Is there an Assassin-enabled AI on this tech?
            public bool isAegisAvail = false;       //Is there an Aegis-enabled AI on this tech?

            public bool isAviatorAvail = false;
            public bool isAstrotechAvail = false;
            public bool isBuccaneerAvail = false;



            //BROKEN - cannot set AI types!
            public bool AutoAnchor = false;      // Should the AI toggle the anchor when it is still?

            // legdev
            internal bool Feedback = true;      // set this to false to get AI feedback testing


            // General AI Handling
            public int OverrideAim = 0; // 0 is off, 1 is enemy, 2 is obsticle

            public int AIState = 0;
            public bool updateCA = false; //Collision avoidence
            public bool allowAutoRepair = false;
            public bool useInventory = false;
            public bool useAirControls = false;
            public int lastMoveAction = 0;
            public int lastWeaponAction = 0;
            internal int FrustrationMeter = 0;
            internal float Urgency = 0;
            internal float UrgencyOverload = 0;
            public bool PendingSystemsCheck;    // Is this tech damaged?
            public int AttemptedRepairs = 0;    // How many times have we tried fix
            public float DamageThreshold = 0;   // How much damage have we taken? (100 is total destruction)
            //internal float Oops = 0;
            internal Vector3 lastDestination = Vector3.zero;

            //AutoCollection
            internal int anchorAttempts = 0;
            internal float lastTechExtents = 0;
            internal float lastRange = 0;
            internal float EstTopSped = 0;
            internal float recentSpeed = 1;
            internal float lastAuxVal = 0;

            internal Visible lastPlayer;
            internal Visible lastEnemy;
            internal Transform Obst;

            internal Tank LastCloseAlly;


            // Resource AI Handling
            internal bool ProceedToBase = false;
            internal bool ProceedToMine = false;
            internal float lastBaseExtremes = 10;
            internal Tank theBase = null;
            internal Visible theResource = null;

            internal Transform lastBasePos;
            internal bool foundBase = false;
            internal bool foundGoal = false;

            // MultiTech AI Handling
            internal bool MTLockedToTechBeam = false;
            internal bool MTMimicHostAvail = false;
            internal Vector3 MTOffsetPos = Vector3.zero;
            internal Vector3 MTOffsetRot = Vector3.forward;
            internal Vector3 MTOffsetRotUp = Vector3.up;

            //  !!ADVANCED!!
            internal bool Attempt3DNavi = false;
            internal Vector3 Navi3DDirect = Vector3.zero;
            internal Vector3 Navi3DUp = Vector3.zero;
            public float GroundOffsetHeight = 35;    // flote above ground this dist


            //Timestep
            internal int FixedDelayedUpdateClock = 0;
            internal int DirectionalHandoffDelay = 0;
            internal int DelayedUpdateClock = 500;
            internal int DelayedAnchorClock = 0;
            internal int featherClock = 50;
            internal int repairClock = 0;
            internal int beamClock = 0;
            //internal int LastBuildClock = 0;
            internal int ActionPause = 0;
            internal int unanchorCountdown = 0;

            //Drive Direction Handlers
            /// <summary> Do we steer to target destination? </summary>
            internal bool Steer = false;

            /// <summary> Drive direction </summary>
            internal EDriveType DriveDir = EDriveType.Neutral;

            /// <summary> Drive AWAY from target </summary>
            internal bool AdviseAway = false;


            //Finals
            internal float MinimumRad = 0;
            internal float DriveVar = 0;
            //internal bool IsLikelyJammed = false;
            internal bool Yield = false;
            internal bool PivotOnly = false;
            internal bool ProceedToObjective = false;
            internal bool MoveFromObjective = false;
            internal bool DANGER = false;
            internal bool AvoidStuff = true;
            internal bool FIRE_NOW = false;
            internal bool BOOST = false;
            internal bool featherBoost = false;
            internal bool forceBeam = false;
            internal bool forceDrive = false;
            internal bool areWeFull = false;
            internal bool Retreat = false;

            internal bool JustUnanchored = false;


            public void Subscribe(Tank tank)
            {
                Vector3 _ = tank.boundsCentreWorld;
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                this.tank = tank;
                tank.AttachEvent.Subscribe(OnAttach);
                tank.DetachEvent.Subscribe(OnDetach);
                tank.TankRecycledEvent.Subscribe(OnRecycle);
                thisInst.AIList = new List<ModuleAIExtension>();
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(OnDeathOrRemoval);
                Singleton.Manager<ManTechs>.inst.TankPostSpawnEvent.Subscribe(OnSpawn);
            }

            public static void OnAttach(TankBlock newBlock, Tank tank)
            {
                var thisInst = tank.gameObject.GetComponent<TankAIHelper>();
                thisInst.EstTopSped = 1;
                //thisInst.LastBuildClock = 0;
            }
            public static void OnDetach(TankBlock newBlock, Tank tank)
            {
                var thisInst = tank.gameObject.GetComponent<TankAIHelper>();
                thisInst.EstTopSped = 1;
                thisInst.recentSpeed = 1;
            }

            public static void OnSpawn(Tank tankInfo)
            {
                //Debug.Log("TACtical_AI: Allied AI " + tankInfo.name + ":  Called OnSpawn");
                if (tankInfo.gameObject.GetComponent<TankAIHelper>().AIState != 0)
                ResetAll(tankInfo);
            }
            public static void OnDeathOrRemoval(Tank tankInfo, ManDamage.DamageInfo damage)
            {
                //Debug.Log("TACtical_AI: Allied AI " + tankInfo.name + ":  Called OnDeathOrRemoval");
                tankInfo.gameObject.GetComponent<TankAIHelper>().DediAI = DediAIType.Escort;
                ResetAll(tankInfo);
            }
            public static void OnRecycle(Tank tank)
            {
                //Debug.Log("TACtical_AI: Allied AI " + tank.name + ":  Called OnRecycle");
                tank.gameObject.GetComponent<TankAIHelper>().DediAI = DediAIType.Escort;
                ResetAll(tank);
            }


            public static void ResetAll(Tank tank)
            {
                var thisInst = tank.gameObject.GetComponent<TankAIHelper>();
                thisInst.Hibernate = false;
                thisInst.AIState = 0;
                thisInst.lastAIType = AITreeType.AITypes.Idle;
                thisInst.EstTopSped = 1;
                thisInst.recentSpeed = 1;
                thisInst.anchorAttempts = 0;
                thisInst.DelayedAnchorClock = 0;
                thisInst.foundBase = false;
                thisInst.foundGoal = false;
                thisInst.useInventory = false;
                thisInst.lastBasePos = null;
                thisInst.lastPlayer = null;
                thisInst.lastEnemy = null;
                thisInst.LastCloseAlly = null;
                thisInst.theBase = null;
                thisInst.JustUnanchored = false;
                var Mind = tank.gameObject.GetComponent<Enemy.RCore.EnemyMind>();
                if (Mind.IsNotNull())
                    Mind.SetForRemoval();
                var Mem = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (Mem.IsNotNull())
                    Mem.Remove();
                var Pilot = tank.gameObject.GetComponent<AIEAirborne.AirAssistance>();
                if (Pilot.IsNotNull())
                    Pilot.Recycle();

                FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
                control3D.m_State.m_Beam = false;
                control3D.m_State.m_BoostJets = false;
                control3D.m_State.m_BoostProps = false;
                control3D.m_State.m_Fire = false;
                control3D.m_State.m_InputMovement = Vector3.zero;
                control3D.m_State.m_InputRotation = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);
            }

            public void RefreshAI()
            {
                FullMelee = false;      // Should the AI ram the enemy?
                AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
                SecondAvoidence = false;// Should the AI avoid two techs at once?
                OnlyPlayerMT = true;
                SideToThreat = false;
                useInventory = false;

                isProspectorAvail = false;
                isScrapperAvail = false;

                isAstrotechAvail = false;
                isAviatorAvail = false;
                isBuccaneerAvail = false;

                FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
                control3D.m_State.m_Beam = false;
                control3D.m_State.m_BoostJets = false;
                control3D.m_State.m_BoostProps = false;
                control3D.m_State.m_Fire = false;
                control3D.m_State.m_InputMovement = Vector3.zero;
                control3D.m_State.m_InputRotation = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);

                AIList.Clear();
                List<ModuleAIBot> AIs = tank.blockman.IterateBlockComponents<ModuleAIBot>().ToList();
                foreach (ModuleAIBot bot in AIs)
                {
                    var AIE = bot.gameObject.GetComponent<ModuleAIExtension>();
                    if (AIE.IsNotNull())
                        AIList.Add(AIE);
                }
                //Debug.Log("TACtical_AI: AI list for Tech " + tank.name + " has " + AIList.Count() + " entries");

                foreach (ModuleAIExtension AIEx in AIList)
                {
                    if (AIEx.Prospector)
                        isProspectorAvail = true;

                    if (AIEx.Scrapper)
                        isScrapperAvail = true;

                    if (AIEx.Astrotech)
                        isAstrotechAvail = true;

                    if (AIEx.Aviator)
                        isAviatorAvail = true;

                    if (AIEx.Buccaneer)
                        isBuccaneerAvail = true;

                    if (AIEx.MeleePreferred)
                        FullMelee = true;

                    if (AIEx.AdvAvoidence)
                        SecondAvoidence = true;

                    if (AIEx.MTForAll)
                        OnlyPlayerMT = false;

                    if (AIEx.SidePreferred)
                        SideToThreat = true;

                    if (AIEx.MinCombatRange > IdealRangeCombat)
                        IdealRangeCombat = AIEx.MinCombatRange;

                    if (AIEx.MaxCombatRange > RangeToChase)
                        RangeToChase = AIEx.MaxCombatRange;

                    if (AIEx.SelfRepairAI)
                        allowAutoRepair = true;
                }
                // REMOVE any AI states that have been removed!!!
                if (!isAegisAvail && DediAI == DediAIType.Aegis)
                    DediAI = DediAIType.Escort;
                if (!isAssassinAvail && DediAI == DediAIType.Assault)
                    DediAI = DediAIType.Escort;
                if (!isProspectorAvail && DediAI == DediAIType.Prospector)
                    DediAI = DediAIType.Escort;
                if (!isScrapperAvail && DediAI == DediAIType.Scrapper)
                    DediAI = DediAIType.Escort;
                if (!isAstrotechAvail && DediAI == DediAIType.Astrotech)
                    DediAI = DediAIType.Escort;
                if (!isAviatorAvail)
                {
                    if (DediAI == DediAIType.Aviator)
                        DediAI = DediAIType.Escort;
                    if (gameObject.GetComponent<AIEAirborne.AirAssistance>().IsNotNull())
                        gameObject.GetComponent<AIEAirborne.AirAssistance>().Recycle();
                }
                else if (DediAI == DediAIType.Aviator)
                {
                    TestForFlyingAIRequirement();
                }
                if (allowAutoRepair)
                {
                    if (TechMemor.IsNull())
                        tank.gameObject.AddComponent<AIERepair.DesignMemory>().Initiate();
                }
                else
                {
                    if (TechMemor.IsNotNull())
                        TechMemor.Remove();
                }

                //Debug.Log("TACtical_AI: Refreshed AI");
            }

            public void ForceAllAIsToEscort()
            {
                //broken ATM, needed to return AI mode back to Escort on unanchor as unanchoring causes it to go to idle
                foreach (ModuleAIBot AIbot in tank.blockman.IterateBlockComponents<ModuleAIBot>())
                {
                    AIbot.m_AITypesEnabled.SetValue(false, 1);
                    AIbot.m_AITypesEnabled.SetValue(true, 2);
                    Debug.Log("TACtical_AI: set an AI to escort");
                }
            }


            /// <summary>
            /// Main controller for ALL AI
            /// </summary>
            /// <param name="thisControl"></param>
            public void BetterAI(TankControl thisControl)
            {
                // The interface method for actually handling the tank - note that this fires at a different rate
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                thisInst.lastTechExtents = Extremes(tank.blockBounds.extents);

                AIEBeam.BeamDirector(thisControl, thisInst, tank);
                if (thisInst.updateCA)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Fired CollisionAvoidUpdate!");
                    AIEWeapons.WeaponDirector(thisControl, thisInst, tank);
                    AIEDrive.DriveDirector(thisControl, thisInst, tank);
                }
                AIEWeapons.WeaponMaintainer(thisControl, thisInst, tank);
                AIEDrive.DriveMaintainer(thisControl, thisInst, tank);
            }
            
            /// <summary>
            /// Gets the opposite direction of the target tech, accounting for size
            /// </summary>
            /// <param name="targetToAvoid"></param>
            /// <returns></returns>
            public Vector3 GetOtherDir(Tank targetToAvoid)
            {
                //What actually does the avoidence
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                //Debug.Log("TACtical_AI: GetOtherDir");
                Vector3 inputOffset = tank.transform.position - targetToAvoid.transform.position;
                float inputSpacing = Extremes(targetToAvoid.blockBounds.extents) + thisInst.lastTechExtents + DodgeStrength;
                Vector3 Final = (inputOffset.normalized * inputSpacing) + tank.transform.position;
                return Final;
            }


            // Collision Avoidence
            public Vector3 AvoidAssist(Vector3 targetIn)
            {
                //The method to determine if we should avoid an ally nearby while navigating to the target
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                //thisInst.IsLikelyJammed = false;
                if (thisInst.AvoidStuff)
                {
                    try
                    {
                        Tank lastCloseAlly;
                        float lastAllyDist;
                        if (thisInst.SecondAvoidence && moreThan2Allies)// MORE processing power
                        {
                            lastCloseAlly = AIEPathing.SecondClosestAlly(tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal);
                            if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                            {
                                if (lastAuxVal < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    //thisInst.IsLikelyJammed = true;
                                    IntVector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst2, thisInst.AdvancedAI);
                                    if (obst2)
                                        return (targetIn + ProccessedVal2) / 4;
                                    else
                                        return (targetIn + ProccessedVal2) / 3;

                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                                //thisInst.IsLikelyJammed = true;
                                IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 3;
                                else
                                    return (targetIn + ProccessedVal) / 2;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAlly(tank.boundsCentreWorldNoCheck, out lastAllyDist);
                        //Debug.Log("TACtical_AI: Ally is " + thisInst.lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        if (lastCloseAlly == null)
                            Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //thisInst.IsLikelyJammed = true;
                            IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 3;
                            else
                                return (targetIn + ProccessedVal) / 2;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Crash on Avoid " + e);
                        return targetIn;
                    }
                }
                if (targetIn.IsNaN())
                {
                    Debug.Log("TACtical_AI: AvoidAssist IS NaN!!");
                    TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }
            public Vector3 AvoidAssistPrecise(Vector3 targetIn)
            {
                //The method to determine if we should avoid an ally nearby while navigating to the target
                //  MORE DEMANDING THAN THE ABOVE!
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                if (thisInst.AvoidStuff)
                {
                    try
                    {
                        Tank lastCloseAlly;
                        float lastAllyDist;
                        if (thisInst.SecondAvoidence && moreThan2Allies)// MORE processing power
                        {
                            lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal);
                            if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                            {
                                if (lastAuxVal < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    IntVector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst2, thisInst.AdvancedAI);
                                    if (obst2)
                                        return (targetIn + ProccessedVal2) / 4;
                                    else
                                        return (targetIn + ProccessedVal2) / 3;
                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);

                                IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                                if (obst)
                                    return (targetIn + ProccessedVal) / 3;
                                else
                                    return (targetIn + ProccessedVal) / 2;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAllyPrecision(tank.boundsCentreWorldNoCheck, out lastAllyDist);
                        //Debug.Log("TACtical_AI: Ally is " + thisInst.lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        if (lastCloseAlly == null)
                            Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly) + AIEPathing.ObstDodgeOffset(tank, thisInst, targetIn, out bool obst, thisInst.AdvancedAI);
                            if (obst)
                                return (targetIn + ProccessedVal) / 3;
                            else
                                return (targetIn + ProccessedVal) / 2;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: Crash on Avoid " + e);
                        return targetIn;
                    }
                }
                if (targetIn.IsNaN())
                {
                    Debug.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                    TankAIManager.FetchAllAllies();
                }
                return targetIn;
            }


            // Obstruction Management
            public void TryHandleObstruction(bool hasMessaged, float dist, bool useRush, bool useGun)
            {
                //Something is in the way - try fetch the scenery to shoot at
                var thisInst = gameObject.GetComponent<TankAIHelper>();

                Debug.Log("TACtical_AI: AI " + tank.name + ":  Obstructed");
                if (!hasMessaged)
                {
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  Can't move there - something's in the way!");
                }

                thisInst.UrgencyOverload += KickStart.AIClockPeriod / 2;
                if (thisInst.Urgency > 0)
                    thisInst.Urgency += KickStart.AIClockPeriod / 5;
                if (useRush && dist > thisInst.RangeToStopRush * 2)
                {
                    //SCREW IT - GO FULL SPEED WE ARE TOO FAR BEHIND!
                    if (useGun)
                        RemoveObstruction(); 
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 1f;
                    thisInst.Urgency += KickStart.AIClockPeriod / 5;
                }
                else if (10 < thisInst.FrustrationMeter)
                {
                    //Try build beaming to clear debris
                    thisInst.FrustrationMeter += KickStart.AIClockPeriod / 5;
                    if (30 < thisInst.FrustrationMeter)
                    {
                        thisInst.FrustrationMeter = 0;
                    }
                    else if (15 < thisInst.FrustrationMeter)
                    {
                        thisInst.forceDrive = true;
                        thisInst.DriveVar = -1;
                    }
                    else
                        thisInst.forceBeam = true;
                }
                else
                {
                    //Shoot the freaking tree
                    thisInst.FrustrationMeter += KickStart.AIClockPeriod / 5;
                    if (useGun)
                        RemoveObstruction();
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 0.5f;
                }
            }
            /*
            public Transform GetObstruction() //VERY expensive operation - only use if absoluetely nesseary
            {
                // Get the scenery that's obstructing if there's any (ignores monuments to be fair to Enemy AI)
                LayerMask Filter = Globals.inst.layerScenery.mask;
                float ext = Extremes(tank.blockBounds.extents);
                Physics.SphereCast(tank.rbody.centerOfMass - tank.transform.InverseTransformVector(Vector3.forward * ext), ext, tank.blockman.GetRootBlock().transform.forward, out RaycastHit Pummel, 100, Filter, QueryTriggerInteraction.Ignore);
                Transform Obstruction;
                try
                {
                    var vaildTar = Pummel.collider.transform.parent.parent.parent.gameObject.transform;
                    if (vaildTar != null)
                    {
                        Obstruction = vaildTar;
                        Debug.Log("TACtical_AI: GetObstruction - found " + Obstruction.name);
                        return Obstruction;
                    }
                    Debug.Log("TACtical_AI: GetObstruction - Expected Scenery but got " + Pummel.collider.transform.parent.parent.parent.gameObject.name + " instead");
                    //avoid _GameManager - crashes can happen
                    //Debug.Log("TACtical_AI: GetObstruction - host gameobject " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(Pummel.collider.transform.root.gameObject, Pummel.collider.transform.root.gameObject.name));

                }
                catch
                {
                    //Debug.Log("TACtical_AI: GetObstruction - DID NOT HIT ANYTHING");
                }
                return null;
            }
            */
            public Transform GetObstruction()
            {
                List<Visible> ObstList = AIEPathing.ObstructionAwareness(tank.boundsCentreWorldNoCheck, this);
                int bestStep = 0;
                float bestValue = 500;
                int steps = ObstList.Count;
                if (steps <= 0)
                {
                    Debug.Log("TACtical_AI: GetObstruction - DID NOT HIT ANYTHING");
                    return null;
                }
                for (int stepper = 0; steps > stepper; stepper++)
                {
                    float temp = Mathf.Clamp((ObstList.ElementAt(stepper).centrePosition - tank.boundsCentreWorldNoCheck).sqrMagnitude - ObstList.ElementAt(stepper).Radius, 0, 500);
                    if (bestValue > temp && temp != 0)
                    {
                        bestStep = stepper;
                        bestValue = temp;
                    }
                }
                Debug.Log("TACtical_AI: GetObstruction - found " + ObstList.ElementAt(bestStep).name);
                return ObstList.ElementAt(bestStep).trans;
            }
            public void RemoveObstruction()
            {
                // Shoot at the scenery obsticle infront of us
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                if (Obst == null)
                {
                    thisInst.Obst = GetObstruction();
                    thisInst.Urgency += KickStart.AIClockPeriod / 5;
                }
                thisInst.FIRE_NOW = true;
            }
            public void SettleDown()
            {
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                thisInst.UrgencyOverload = 0;
                thisInst.Urgency = 0;
                thisInst.FrustrationMeter = 0;
                thisInst.Obst = null;
            }


            public void TestForFlyingAIRequirement()
            {
                if (AIState == 1 && DediAI == DediAIType.Aviator)
                {
                    if (Pilot == null)
                    {
                        Pilot = AIEAirborne.AirAssistance.Initiate(tank, this);
                    }
                }
                else if (AIState == 2 && gameObject.GetComponent<Enemy.RCore.EnemyMind>().IsNotNull())
                {
                    var enemy = gameObject.GetComponent<Enemy.RCore.EnemyMind>();
                    if (enemy.EvilCommander == Enemy.EnemyHandling.Chopper || enemy.EvilCommander == Enemy.EnemyHandling.Airplane)
                    {
                        if (Pilot == null)
                        {
                            Pilot = AIEAirborne.AirAssistance.Initiate(tank, this, enemy);
                        }
                    }
                }
                else
                {
                    if (Pilot != null)
                    {
                        Pilot.Recycle();
                    }
                    else
                    {
                        if (tank.GetComponent<AIEAirborne.AirAssistance>())
                        {
                            Debug.Log("TACtical_AI: Allied AI " + tank.name + ":  DID NOT HAVE PILOT SET UP CORRECTLY!!! [TestForFlyingAIRequirement]");
                            tank.GetComponent<AIEAirborne.AirAssistance>().Recycle();
                        }
                    }
                }
            }

            public bool IsTechMoving(float minSpeed)
            {
                if (tank.rbody.IsNull())
                    return false;
                return tank.rbody.velocity.sqrMagnitude > minSpeed * minSpeed;
            }
            public Visible GetPlayerTech()
            {
                foreach (Visible thatTech in tank.Vision.IterateVisibles(ObjectTypes.Vehicle))
                {
                    if (thatTech.tank.PlayerFocused)
                        return thatTech;
                }
                return lastPlayer;
            }

            public void RunAlliedOperations()
            {
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                var aI = tank.AI;

                aI.TryGetCurrentAIType(out thisInst.lastAIType);
                if (thisInst.lastAIType == AITreeType.AITypes.Escort)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Fired DelayedUpdate!");
                    thisInst.Attempt3DNavi = false;

                    //thisInst.updateCA = true;
                    if (thisInst.ActionPause > 0)
                        thisInst.ActionPause--;
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  current mode " + thisInst.DediAI.ToString());
                    switch (thisInst.DediAI)
                    {
                        case DediAIType.Escort:
                            // We move to victory
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.foundGoal = false;
                            //thisInst.IsMultiTech = false;// Disabled so that on tech split it can be set automatically
                            BEscort.MotivateMove(thisInst, tank);
                            BGeneral.AidDefend(thisInst, tank);
                            break;

                        case DediAIType.Assault:
                            // Up your arsenal
                            thisInst.IsMultiTech = false;
                            thisInst.foundGoal = false;
                            //EAssassin.MotivateKill(thisInst, tank);
                            //EAssassin.ShootToDestroy(thisInst, tank);
                            Debug.Log("TACtical_AI: AI NOT READY YET!");
                            break;

                        case DediAIType.Aegis:
                            // I fight for my friends (priority resource techs)
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.foundGoal = false;
                            thisInst.IsMultiTech = false;
                            //EAegis.MotivateProtect(thisInst, tank);
                            BGeneral.AidDefend(thisInst, tank);
                            Debug.Log("TACtical_AI: AI NOT READY YET!");
                            break;

                        case DediAIType.Prospector:
                            // We back in the mine
                            thisInst.IsMultiTech = false;
                            BProspector.MotivateMine(thisInst, tank);
                            BGeneral.SelfDefend(thisInst, tank);
                            break;

                        case DediAIType.Scrapper:
                            // Grab Scrape and sell
                            thisInst.IsMultiTech = false;
                            thisInst.foundGoal = false;
                            //EScrapper.MotivateFind(thisInst, tank);
                            BGeneral.SelfDefend(thisInst, tank);
                            Debug.Log("TACtical_AI: AI NOT READY YET!");
                            break;

                        case DediAIType.Energizer:
                            // The thing that keeps going
                            thisInst.IsMultiTech = false;
                            //EEnergizer.MotivateCharge(thisInst, tank);
                            BGeneral.SelfDefend(thisInst, tank);
                            Debug.Log("TACtical_AI: AI NOT READY YET!");
                            break;

                        case DediAIType.MTTurret:
                            // Load, Aim,    FIIIIIRRRRRRRRRRRRRRRRRRRRRRRRRRRE!!!
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.IsMultiTech = true;
                            BGeneral.ResetValues(thisInst);
                            //EMultiTech.FollowTurretBelow(thisInst, tank);
                            BMultiTech.BeamLockWithinBounds(thisInst, tank); //lock rigidbody with closest non-MT Tech on build beam
                            BMultiTech.MimicDefend(thisInst, tank);
                            break;

                        case DediAIType.MTSlave:
                            // Defend and sit like good guard dog
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.IsMultiTech = true;
                            BGeneral.ResetValues(thisInst);
                            BMultiTech.BeamLockWithinBounds(thisInst, tank); //lock rigidbody with closest non-MT Tech on build beam
                            BMultiTech.MimicDefend(thisInst, tank);
                            break;

                        case DediAIType.MTMimic:
                            // Copycat
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.IsMultiTech = true;
                            thisInst.Attempt3DNavi = true;
                            BGeneral.ResetValues(thisInst);
                            BMultiTech.MimicClosestAlly(thisInst, tank);
                            break;

                        case DediAIType.Astrotech:
                            // Grace from Space
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.IsMultiTech = false;
                            thisInst.Attempt3DNavi = true;
                            BAstrotech.MotivateSpace(thisInst, tank);
                            BGeneral.AidDefend(thisInst, tank);
                            break;

                        case DediAIType.Aviator:
                            // Fly and doggyfight
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.IsMultiTech = false;
                            thisInst.Attempt3DNavi = false;
                            BAviator.MotivateFly(thisInst, tank);
                            BAviator.Dogfighting(thisInst, tank);
                            break;

                        default:
                            // It's one of the other showboat AIs(VEN(Air) or TAC(Navy) or Legion(Star)).  Not yet dammit!
                            Debug.Log("TACtical_AI: AI NOT READY YET! - Tougher Enemies doesn't even exist yet hold your horses!");
                            break;
                    }
                    AIEDrive.DetermineCombat(thisInst);
                }
            }

            /// <summary>
            /// Extension for TougherEnemies to toggle
            /// </summary>
            public void TryRunEnemyOperations()
            {
                //BEGIN THE PAIN!
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                //thisInst.updateCA = true;
                if (thisInst.ActionPause > 0)
                    thisInst.ActionPause--;
                Enemy.RCore.BeEvil(thisInst, tank);
            }

            public void DelayedUpdate()
            {   //OBSOLETE until further notice
                // Dynamic timescaled update that fires when needed, less for slow techs, fast for large techs
            }

            public void FixedUpdate()
            {
                //Handler for the improved AI, messy but gets the job done.
                if (KickStart.EnableBetterAI && !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                {
                    var thisInst = gameObject.GetComponent<TankAIHelper>();
                    var aI = tank.AI;


                    if (tank.IsFriendly() && aI.CheckAIAvailable())
                    {   //MP is NOT supported!
                        //Player-Allied AI
                        if (thisInst.AIState != 1)
                        {
                            ResetAll(thisInst.tank);
                            thisInst.AIState = 1;
                            RefreshAI();
                            Debug.Log("TACtical_AI: Allied AI " + tank.name + ":  Checked up and good to go!");
                        }

                        thisInst.FixedDelayedUpdateClock++;
                        if (thisInst.FixedDelayedUpdateClock > KickStart.AIDodgeCheapness)
                        {
                            thisInst.updateCA = true;
                            thisInst.FixedDelayedUpdateClock = 0;
                        }
                        else
                            thisInst.updateCA = false;

                        if (thisInst.recentSpeed < 1)
                            thisInst.recentSpeed = 1;
                        thisInst.DelayedUpdateClock++;
                        if (thisInst.DelayedUpdateClock > KickStart.AIClockPeriod)//Mathf.Max(25 / thisInst.recentSpeed, 5)
                        {
                            thisInst.recentSpeed = tank.GetForwardSpeed();
                            RunAlliedOperations();
                            thisInst.DelayedUpdateClock = 0;
                            if (thisInst.EstTopSped < thisInst.recentSpeed)
                                thisInst.EstTopSped = thisInst.recentSpeed;
                        }
                    }
                    else if ((KickStart.testEnemyAI || KickStart.isTougherEnemiesPresent) && KickStart.enablePainMode && tank.IsEnemy())
                    {   //MP is NOT supported!
                        //Enemy AI
                        if (thisInst.AIState != 2)
                        {
                            ResetAll(thisInst.tank);
                            thisInst.AIState = 2;
                            Enemy.RCore.RandomizeBrain(thisInst, tank);
                            Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Ready to kick some Tech!");
                        }
                        if (!thisInst.Hibernate)
                        {
                            thisInst.FixedDelayedUpdateClock++;
                            if (thisInst.FixedDelayedUpdateClock > KickStart.AIDodgeCheapness)
                            {
                                thisInst.updateCA = true;
                                thisInst.FixedDelayedUpdateClock = 0;
                            }
                            else
                                thisInst.updateCA = false;

                            if (thisInst.recentSpeed < 1)
                                thisInst.recentSpeed = 1;
                            thisInst.DelayedUpdateClock++;
                            if (thisInst.DelayedUpdateClock > KickStart.AIClockPeriod)
                            {
                                thisInst.recentSpeed = tank.GetForwardSpeed();
                                TryRunEnemyOperations();
                                thisInst.DelayedUpdateClock = 0;
                                if (thisInst.EstTopSped < thisInst.recentSpeed)
                                    thisInst.EstTopSped = thisInst.recentSpeed;
                            }
                        }
                    }
                    else
                    {   // Static tech
                        thisInst.DriveVar = 0;
                        if (thisInst.AIState > 0)
                        {   // Reset and ready for static tech
                            ResetAll(thisInst.tank);
                            thisInst.AIState = 0;
                        }
                    }
                }
                //else
                //    this.Recycle();//Remove big ram consuming module from Techs that don't need it!
            }
        }
    }
}
