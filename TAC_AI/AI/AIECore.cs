using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TAC_AI.AI
{
    public class AIECore
    {
        public static List<Tank> Allies;
        //public static List<ResourceDispenser> Minables;   //Harvest AI coming soon
        public static List<Transform> Minables;   //Harvest AI coming soon
        public static List<Transform> Depots;   //Harvest AI coming soon
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

        public static bool FetchClosestHarvestReceiver(Vector3 tankPos, float MaxScanRange, out Transform finalPos, out Tank theBase)
        {
            bool fired = false;
            theBase = null;
            finalPos = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            foreach (Transform trans in Depots)
            {
                float temp = (trans.position - tankPos).sqrMagnitude;
                if (bestValue > temp && temp != 0)
                {
                    fired = true;
                    theBase = trans.root.GetComponent<Tank>();
                    bestValue = temp;
                    finalPos = trans;
                }
            }
            return fired;
        }

        public static bool FetchClosestResource(Vector3 tankPos, float MaxScanRange, out Vector3 finalPos, out GameObject theResource)
        {
            bool fired = false;
            finalPos = Vector3.zero;
            theResource = null;
            float bestValue = Mathf.Pow(MaxScanRange, 2);// MAX SCAN RANGE
            int run = Minables.Count;
            for ( int step = 0; step < run; step++)
            {
                var trans = Minables.ElementAt(step);
                if (!trans.gameObject.GetComponent<ResourceDispenser>().IsDeactivated)
                {
                    //Debug.Log("TACtical_AI:Skipped over inactive");
                    if (!trans.gameObject.GetComponent<Damageable>().Invulnerable)
                    {
                        //Debug.Log("TACtical_AI: Skipped over invincible");
                        float temp = (trans.position - tankPos).sqrMagnitude;
                        if (bestValue > temp && temp != 0)
                        {
                            theResource = trans.gameObject;
                            fired = true;
                            bestValue = temp;
                            finalPos = trans.position;
                        }
                    }
                }
                else
                {
                    Minables.Remove(trans);//it's invalid and must be destroyed
                    step--;
                    run--;
                }
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
                Minables = new List<Transform>();
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
                if (techCount > 2)
                    moreThan2Allies = true;
                else
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

            private const int DodgeStrength = 60;  //The motivation in trying to move away from a tech in the way
            //250

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
            public float IdealRangeCombat = 50; // The range the AI will linger from the enemy if PursueThreat is true
            public int AnchorAimDampening = 45; // How much do we dampen anchor movements by?


            //AI Allowed types (self-filling)
            public bool isProspectorAvail = false;  //Is there a Prospector-enabled AI on this tech?
            public bool isScrapperAvail = false;    //Is there a Scrapper-enabled AI on this tech?
            public bool isAssassinAvail = false;    //Is there an Assassin-enabled AI on this tech?
            public bool isAegisAvail = false;       //Is there an Aegis-enabled AI on this tech?

            public bool isAstrotechAvail = false;
            public bool isBuccaneerAvail = false;



            //BROKEN - cannot set AI types!
            public bool AutoAnchor = false;      // Should the AI toggle the anchor when it is still?

            // legdev
            internal bool Feedback = true;      // set this to false to get AI feedback testing


            // General AI Handling
            public bool AIState = false;
            public bool updateCA = false; //Collision avoidence
            public int lastMoveAction = 0;
            public int lastWeaponAction = 0;
            internal int FrustrationMeter = 0;
            internal float Urgency = 0;
            internal float UrgencyOverload = 0;
            //internal float Oops = 0;
            internal Vector3 lastDestination;

            //AutoCollection
            internal int anchorAttempts = 0;
            //internal int lastBlockCount = 0;
            internal float lastTechExtents = 0;
            internal float lastRange = 0;
            internal float EstTopSped = 0;
            internal float recentSpeed = 1;
            internal float lastAuxVal = 0;

            internal Visible lastPlayer;
            internal Visible lastEnemy;
            internal Visible Obst;

            internal Tank LastCloseAlly;


            // Resource AI Handling
            internal bool ProceedToBase = false;
            internal bool ProceedToMine = false;
            internal float lastBaseExtremes = 10;
            internal Tank theBase = null;
            internal GameObject theResource = null;

            internal Transform lastBasePos;
            internal Vector3 lastResourcePos;
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
            public float GroundOffsetHeight = 35;    // flote above ground this dist


            //Timestep
            internal int FixedDelayedUpdateClock = 0;
            internal int DirectionalHandoffDelay = 0;
            internal int DelayedUpdateClock = 500;
            internal int DelayedAnchorClock = 0;
            internal int featherClock = 50;
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
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                this.tank = tank;
                tank.AttachEvent.Subscribe(OnAttach);
                tank.DetachEvent.Subscribe(OnDetach);
                tank.TankRecycledEvent.Subscribe(OnRecycle);
                thisInst.AIList = new List<ModuleAIExtension>();
                Singleton.Manager<ManTechs>.inst.TankDestroyedEvent.Subscribe(OnDeathOrRemoval);
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
            public static void OnDeathOrRemoval(Tank tankInfo, ManDamage.DamageInfo damage)
            {
                ResetAll(tankInfo);
            }

            public static void OnRecycle(Tank tank)
            {
                ResetAll(tank);
            }

            public static void ResetAll(Tank tank)
            {
                var thisInst = tank.gameObject.GetComponent<TankAIHelper>();
                thisInst.DediAI = DediAIType.Escort;
                thisInst.lastAIType = AITreeType.AITypes.Idle;
                thisInst.EstTopSped = 1;
                thisInst.recentSpeed = 1;
                thisInst.anchorAttempts = 0;
                thisInst.DelayedAnchorClock = 0;
                thisInst.foundBase = false;
                thisInst.foundGoal = false;
                thisInst.lastBasePos = null;
                thisInst.lastResourcePos = Vector3.zero;
                thisInst.lastPlayer = null;
                thisInst.lastEnemy = null;
                thisInst.LastCloseAlly = null;
                thisInst.theBase = null;
                thisInst.JustUnanchored = false;
                thisInst.AIList.Clear();
                /*
                FieldInfo controlGet = typeof(TankControl).GetField("m_ControlState", BindingFlags.NonPublic | BindingFlags.Instance);
                TankControl.ControlState control3D = (TankControl.ControlState)controlGet.GetValue(tank.control);
                control3D.m_State.m_Beam = false;
                control3D.m_State.m_BoostJets = false;
                control3D.m_State.m_BoostProps = false;
                control3D.m_State.m_Fire = false;
                control3D.m_State.m_InputMovement = Vector3.zero;
                control3D.m_State.m_InputRotation = Vector3.zero;
                controlGet.SetValue(tank.control, control3D);
                */
            }

            public void RefreshAI()
            {
                var thisInst = gameObject.GetComponent<TankAIHelper>();

                FullMelee = false;      // Should the AI ram the enemy?
                AdvancedAI = false;     // Should the AI take combat calculations and retreat if nesseary?
                SecondAvoidence = false;// Should the AI avoid two techs at once?
                OnlyPlayerMT = true;
                SideToThreat = false;

                thisInst.isProspectorAvail = false;
                thisInst.isScrapperAvail = false;
                thisInst.isAstrotechAvail = false;
                thisInst.isBuccaneerAvail = false;
                foreach (ModuleAIExtension AIEx in thisInst.AIList)
                {
                    if (AIEx.Prospector)
                        thisInst.isProspectorAvail = true;

                    if (AIEx.Scrapper)
                        thisInst.isScrapperAvail = true;

                    if (AIEx.Astrotech)
                        thisInst.isAstrotechAvail = true;

                    if (AIEx.Buccaneer)
                        thisInst.isBuccaneerAvail = true;

                    if (AIEx.MeleePreferred)
                        thisInst.FullMelee = true;

                    if (AIEx.AdvAvoidence)
                        thisInst.SecondAvoidence = true;

                    if (AIEx.MTForAll)
                        thisInst.OnlyPlayerMT = false;

                    if (AIEx.SidePreferred)
                        thisInst.SideToThreat = true;

                    if (AIEx.MinCombatRange > IdealRangeCombat)
                        thisInst.IdealRangeCombat = AIEx.MinCombatRange;

                    if (AIEx.MaxCombatRange > RangeToChase)
                        thisInst.RangeToChase = AIEx.MaxCombatRange;
                }
                if (!thisInst.isAegisAvail && DediAI == DediAIType.Aegis)
                    DediAI = DediAIType.Escort;
                if (!thisInst.isAssassinAvail && DediAI == DediAIType.Assault)
                    DediAI = DediAIType.Escort;
                if (!thisInst.isProspectorAvail && DediAI == DediAIType.Prospector)
                    DediAI = DediAIType.Escort;
                if (!thisInst.isScrapperAvail && DediAI == DediAIType.Scrapper)
                    DediAI = DediAIType.Escort;
                Debug.Log("TACtical_AI: Refreshed AI");

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

            public void BetterAI(TankControl thisControl)
            {
                // The interface method for actually handling the tank - note that this fires at a different rate
                //WIP, will have to add other allied Tech avoidence down the line
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
                            lastCloseAlly = AIEPathing.SecondClosestAlly(tank.rbody.position, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal);
                            if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                            {
                                if (lastAuxVal < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    //thisInst.IsLikelyJammed = true;
                                    IntVector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2);
                                    return (targetIn + ProccessedVal2) / 3;
                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                                //thisInst.IsLikelyJammed = true;
                                IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                                return (targetIn + ProccessedVal) / 2;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAlly(tank.rbody.position, out lastAllyDist);
                        //Debug.Log("TACtical_AI: Ally is " + thisInst.lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        if (lastCloseAlly == null)
                            Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //thisInst.IsLikelyJammed = true;
                            IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly);
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
                    Debug.Log("TACtical_AI: AvoidAssist IS NaN!!");
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
                            lastCloseAlly = AIEPathing.SecondClosestAllyPrecision(tank.rbody.position, out Tank lastCloseAlly2, out lastAllyDist, out float lastAuxVal);
                            if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                            {
                                if (lastAuxVal < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                                {
                                    //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name + " and " + lastCloseAlly2.name);
                                    //thisInst.IsLikelyJammed = true;
                                    IntVector3 ProccessedVal2 = GetOtherDir(lastCloseAlly) + GetOtherDir(lastCloseAlly2);
                                    return (targetIn + ProccessedVal2) / 3;
                                }
                                //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                                //thisInst.IsLikelyJammed = true;

                                IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly);
                                return (targetIn + ProccessedVal) / 2;
                            }

                        }
                        lastCloseAlly = AIEPathing.ClosestAllyPrecision(tank.rbody.position, out lastAllyDist);
                        //Debug.Log("TACtical_AI: Ally is " + thisInst.lastAllyDist + " dist away");
                        //Debug.Log("TACtical_AI: Trigger threshold is " + (thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 4) + " dist away");
                        if (lastCloseAlly == null)
                            Debug.Log("TACtical_AI: ALLY IS NULL");
                        if (lastAllyDist < thisInst.lastTechExtents + Extremes(lastCloseAlly.blockBounds.extents) + 12)
                        {
                            //Debug.Log("TACtical_AI: AI " + tank.name + ": Spacing from " + lastCloseAlly.name);
                            //thisInst.IsLikelyJammed = true;
                            IntVector3 ProccessedVal = GetOtherDir(lastCloseAlly);
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
                    Debug.Log("TACtical_AI: AvoidAssistPrecise IS NaN!!");
                return targetIn;
            }


            public void TryHandleObstruction(bool hasMessaged, float dist, bool useRush, bool useGun)
            {
                //Something is in the way - try fetch the scenery to shoot at
                var thisInst = gameObject.GetComponent<TankAIHelper>();

                if (!hasMessaged)
                {
                    Debug.Log("TACtical_AI: AI " + tank.name + ":  Can't move there - something's in the way!");
                }

                thisInst.UrgencyOverload++;
                thisInst.UrgencyOverload++;
                if (thisInst.Urgency > 0)
                    thisInst.Urgency--;
                if (useRush && dist > thisInst.RangeToStopRush * 2)
                {
                    //SCREW IT - GO FULL SPEED WE ARE TOO FAR BEHIND!
                    if (useGun)
                        RemoveObstruction(); 
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 1f;
                    thisInst.Urgency++;
                }
                else if (10 < thisInst.FrustrationMeter)
                {
                    //Try build beaming to clear debris
                    thisInst.FrustrationMeter++;
                    if (21 < thisInst.FrustrationMeter)
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
                    thisInst.FrustrationMeter++;
                    if (useGun)
                        RemoveObstruction();
                    thisInst.forceDrive = true;
                    thisInst.DriveVar = 0.5f;
                }
            }

            public void SettleDown()
            {
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                thisInst.UrgencyOverload = 0;
                thisInst.Urgency = 0;
                thisInst.FrustrationMeter = 0;
                thisInst.Obst = null;
            }


            public Visible GetObstruction() //VERY expensive operation - only use if absoluetely nesseary
            {
                // Get the scenery that's obstructing if there's any (ignores monuments to be fair to Enemy AI)
                LayerMask Filter = Globals.inst.layerScenery.mask;
                float ext = Extremes(tank.blockBounds.extents);
                Physics.SphereCast(tank.rbody.centerOfMass - tank.transform.InverseTransformVector(Vector3.forward * ext), ext, tank.blockman.GetRootBlock().transform.forward, out RaycastHit Pummel, 100, Filter, QueryTriggerInteraction.Ignore);
                Visible Obstruction;
                try
                {
                    var vaildTar = Pummel.collider.transform.parent.parent.parent.gameObject.GetComponent<Visible>();
                    if (vaildTar != null)
                    {
                        Obstruction = vaildTar;
                        //Debug.Log("TACtical_AI: GetObstruction - found " + Obstruction.name);
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


            public void RemoveObstruction()
            {
                // Shoot at the scenery obsticle infront of us
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                if (Obst.IsNull())
                {
                    thisInst.Obst = GetObstruction();
                    thisInst.Urgency++;
                }
                thisInst.FIRE_NOW = true;
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

            public void DelayedUpdate()
            {
                // Dynamic timescaled update that fires when needed, less for slow techs, fast for large techs
                var thisInst = gameObject.GetComponent<TankAIHelper>();
                var aI = tank.AI;

                aI.TryGetCurrentAIType(out thisInst.lastAIType);
                if (thisInst.lastAIType == AITreeType.AITypes.Escort)
                {
                    //Debug.Log("TACtical_AI: AI " + tank.name + ":  Fired DelayedUpdate!");
                    thisInst.updateCA = true;
                    thisInst.Attempt3DNavi = false;
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
                            thisInst.IsMultiTech = true;
                            BGeneral.ResetValues(thisInst);
                            //EMultiTech.FollowTurretBelow(thisInst, tank);
                            BMultiTech.BeamLockWithinBounds(thisInst, tank); //lock rigidbody with closest non-MT Tech on build beam
                            BMultiTech.MimicDefend(thisInst, tank);
                            break;

                        case DediAIType.MTSlave:
                            // Defend and sit like good guard dog
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
                            // Copycat
                            thisInst.lastPlayer = GetPlayerTech();
                            thisInst.IsMultiTech = false;
                            thisInst.Attempt3DNavi = true;
                            BAstrotech.MotivateSpace(thisInst, tank);
                            BGeneral.AidDefend(thisInst, tank);
                            break;

                        default:
                            // It's one of the other showboat AIs(VEN(Air) or TAC(Navy) or Legion(Star)).  Not yet dammit!
                            Debug.Log("TACtical_AI: AI NOT READY YET! - Tougher Enemies doesn't even exist yet hold your horses!");
                            break;
                    }
                }
            }

            public void FixedUpdate()
            {
                //Handler for the improved AI, messy but gets the job done.
                if (KickStart.EnableBetterAI)
                {
                    var thisInst = gameObject.GetComponent<TankAIHelper>();
                    var aI = tank.AI;

                    if (aI.HasAIModules && tank.IsFriendly() && !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                    {   //MP is NOT supported!
                        if (thisInst.AIState == false)
                        {
                            Debug.Log("TACtical_AI: AI " + tank.name + ":  Checked up and good to go!");
                            thisInst.AIState = true;
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
                        if (thisInst.DelayedUpdateClock > 5)//Mathf.Max(25 / thisInst.recentSpeed, 5)
                        {
                            thisInst.recentSpeed = tank.GetForwardSpeed();
                            DelayedUpdate();
                            thisInst.DelayedUpdateClock = 0;
                            if (thisInst.EstTopSped < thisInst.recentSpeed)
                                thisInst.EstTopSped = thisInst.recentSpeed;
                        }
                    }
                    else
                    {
                        thisInst.DriveVar = 0;
                        thisInst.AIState = false;
                    }
                }
                //else
                //    this.Recycle();//Remove big ram consuming module from Techs that don't need it!
            }
        }
    }
}
