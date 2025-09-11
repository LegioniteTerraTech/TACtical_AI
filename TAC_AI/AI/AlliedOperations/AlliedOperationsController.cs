using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TAC_AI.AI.AlliedOperations
{
    /// <summary>
    /// Will be implemented later if performance becomes an issue. 
    ///   Currently the main game bottleneck is the physics engine
    /// WARNING - MUST BE A STRUCT WITH NO SAVED ITEMS INSIDE!
    /// </summary>
    internal interface AIOperation
    {
        void Init(TankAIHelper helper);
        void DeInit(TankAIHelper helper);
        void MovementActions(TankAIHelper helper, Tank tank, ref EControlOperatorSet direct);
        void OnSerialize(TankAIHelper helper, Tank tank, bool saving);
    }

    internal class AlliedOperationsController
    {
        private TankAIHelper helper;

        private static Dictionary<AIType, AIOperation> Operations = new Dictionary<AIType, AIOperation>
        {
            //{ AIType.Escort, new BEscort()}
        };

        public AlliedOperationsController(TankAIHelper helper)
        {
            this.helper = helper;
        }

        public void Startup()
        {
        }

        public void Execute()
        {
            EControlOperatorSet direct = helper.GetDirectedControl();
            if (helper.DriverType == AIDriverType.Stationary)
            {
                switch (helper.DediAI)
                {
                    /*
                    case AIType.Assault:
                        // Up your arsenal
                        BBase.HoldSupport(helper, helper.tank, ref direct);
                        BGeneral.AidDefend(helper, helper.tank);
                        break;*/
                    default:
                        // I fight for my friends
                        BBase.HoldProtect(helper, helper.tank, ref direct);
                        BGeneral.AidDefend(helper, helper.tank);
                        break;
                }
            }
            else
            {
                switch (helper.DediAI)
                {
                    case AIType.Escort:
                        switch (helper.DriverType)
                        {
                            case AIDriverType.Tank:
                                // We move to victory
                                BGeneral.AidDefend(helper, helper.tank);
                                BEscort.MotivateMove(helper, helper.tank, ref direct);
                                break;

                            case AIDriverType.Astronaut:
                                // Grace from Space
                                BGeneral.AidDefend(helper, helper.tank);
                                BAstrotech.MotivateSpace(helper, helper.tank, ref direct);
                                break;

                            case AIDriverType.Sailor:
                                // Yarr
                                BGeneral.AidDefend(helper, helper.tank);
                                BBuccaneer.MotivateBote(helper, helper.tank, ref direct);
                                break;

                            case AIDriverType.Pilot:
                                // Fly and doggyfight
                                BAviator.Dogfighting(helper, helper.tank);
                                BAviator.MotivateFly(helper, helper.tank, ref direct);
                                break;

                            case AIDriverType.Stationary:
                                // STAY and guard
                                BGeneral.AidDefend(helper, helper.tank);
                                BBase.HoldSupport(helper, helper.tank, ref direct);
                                break;

                            case AIDriverType.AutoSet:
                                // Set ourselves up automatically
                                DebugTAC_AI.Log(KickStart.ModID + ": AIDriver is set to AutoSet, but this should have been handled beforehand!");
                                DebugTAC_AI.Log(KickStart.ModID + ": RESETTING TO DEFAULTS");
                                helper.SetDriverType(AIDriverType.Tank);
                                break;


                            default:
                                DebugTAC_AI.Log(KickStart.ModID + ": AIDriver is set to an invalid state - " + helper.DriverType);
                                DebugTAC_AI.Log(KickStart.ModID + ": RESETTING TO DEFAULTS");
                                helper.SetDriverType(AIDriverType.Tank);
                                break;
                        }
                        break;
                    case AIType.Assault:
                        // Up your arsenal
                        BAssassin.ShootToDestroy(helper, helper.tank);
                        BAssassin.MotivateKill(helper, helper.tank, ref direct);
                        break;

                    case AIType.Aegis:
                        // I fight for my friends (priority resource techs pending)
                        BGeneral.AidDefend(helper, helper.tank);
                        BAegis.MotivateProtect(helper, helper.tank, ref direct);
                        break;

                    case AIType.Prospector:
                        // We back in the mine
                        BGeneral.SelfDefend(helper, helper.tank);
                        BProspector.MotivateMine(helper, helper.tank, ref direct);
                        break;

                    case AIType.Scrapper:
                        // Grab Scrape and sell
                        BGeneral.SelfDefend(helper, helper.tank);
                        BScrapper.MotivateFind(helper, helper.tank, ref direct);
                        break;

                    case AIType.Energizer:
                        // The thing that keeps going
                        BGeneral.SelfDefend(helper, helper.tank);
                        BEnergizer.MotivateCharge(helper, helper.tank, ref direct);
                        break;

                    case AIType.MTTurret:
                        // Load, Aim,    FIIIIIRRRRRRRRRRRRRRRRRRRRRRRRRRRE!!!
                        BMultiTech.MimicDefend(helper, helper.tank);
                        BMultiTech.MTStatic(helper, helper.tank, ref direct);
                        //EMultiTech.FollowTurretBelow(helper, helper.tank, ref direct);
                        BMultiTech.BeamLockWithinBounds(helper, helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                        break;

                    case AIType.MTStatic:
                        // Defend and sit like good guard dog
                        BMultiTech.MimicDefend(helper, helper.tank);
                        BMultiTech.MTStatic(helper, helper.tank, ref direct);
                        BMultiTech.BeamLockWithinBounds(helper, helper.tank); //lock rigidbody with closest non-MT Tech on build beam
                        break;

                    case AIType.MTMimic:
                        // Copycat
                        BMultiTech.MimicAllClosestAlly(helper, helper.tank, ref direct);
                        break;

                    default:
                        DebugTAC_AI.Log(KickStart.ModID + ": AIType is set to an invalid state - " + helper.DediAI);
                        DebugTAC_AI.Log(KickStart.ModID + ": RESETTING TO DEFAULTS");
                        helper.DediAI = AIType.Escort;
                        break;
                }
            }
            helper.SetDirectedControl(direct);
        }

    }
}
