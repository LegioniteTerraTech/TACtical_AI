using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI.AI.Enemy
{
    public static class RRepair
    {
        // Timing Information inherited from AIERepair

        private static bool PreRepairPrep(Tank tank, EnemyMind mind, bool overrideChecker = false)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (mind.TechMemor.IsNull())
            {
                Debug.Log("TACtical_AI: EnemyRepairLerp called with no valid EnemyDesignMemory!!!");
                mind.AIControl.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                mind.TechMemor.Initiate();
                return false;
            }
            int savedBCount = mind.TechMemor.ReturnContents().Count;
            int cBCount = cBlocks.Count;
            //Debug.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount && !overrideChecker)
            {
                if (!overrideChecker)
                    Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  INVALID SAVED TECH DESIGN MEMORY!!!");
                mind.TechMemor.SaveTech();
                return false;
            }
            if (savedBCount != cBCount)
            {
                return true;
            }
            return false;
        }

        //COMPLICATED MESS that re-attaches loose blocks for AI techs, does not apply to allied Techs FOR NOW.
        //  Most major operations are called from AIERepair.
        private static bool EnemyRepairLerp(Tank tank, EnemyMind mind, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing, bool overrideChecker = false)
        {
            bool hardest = KickStart.EnemyBlockDropChance == 0;
            //Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Trying to repair");

            //int attachAttempts = fBlocks.Count();
            //Debug.Log("TACtical AI: EnemyRepairLerp - Found " + attachAttempts + " loose blocks to use");

            if (AIERepair.TryAttachExistingBlockFromListInst(tank, mind.TechMemor, ref typesMissing, ref fBlocks, hardest))
                return true;

            if ((KickStart.EnemiesHaveCreativeInventory || mind.AllowInvBlocks || KickStart.AllowEnemiesToStartBases) && mind.CommanderSmarts >= EnemySmarts.Smrt)
            {
                //Debug.Log("TACtical AI: EnemyRepairLerp - trying to fix from inventory);
                RawTechLoader.ResetSkinIDSet();
                if (AIERepair.TrySpawnAndAttachBlockFromListWithSkinInst(tank, mind.TechMemor, ref typesMissing, false, true))
                    return true;
            }
            return false;
        }
        private static bool QueueEnemyRepairLerp(Tank tank, EnemyMind mind, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing, bool overrideChecker = false)
        {
            if (ManNetwork.IsNetworked)
                return EnemyRepairLerp(tank, mind, ref fBlocks, ref typesMissing, overrideChecker);
            bool hardest = KickStart.EnemyBlockDropChance == 0;
            //Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Trying to repair");

            //int attachAttempts = fBlocks.Count();
            //Debug.Log("TACtical AI: EnemyRepairLerp - Found " + attachAttempts + " loose blocks to use");

            if (AIERepair.TryAttachExistingBlockFromList(tank, mind.TechMemor, ref typesMissing, ref fBlocks, hardest))
                return true;

            if ((KickStart.EnemiesHaveCreativeInventory || mind.AllowInvBlocks || KickStart.AllowEnemiesToStartBases) && mind.CommanderSmarts >= EnemySmarts.Smrt)
            {
                //Debug.Log("TACtical AI: EnemyRepairLerp - trying to fix from inventory);
                RawTechLoader.ResetSkinIDSet();
                if (AIERepair.TrySpawnAndAttachBlockFromListWithSkin(tank, mind.TechMemor, ref typesMissing, false, true))
                    return true;
            }
            return false;
        }

        public static bool EnemyInstaRepair(Tank tank, EnemyMind mind, int RepairAttempts = 0)
        {
            if (!KickStart.AllowAISelfRepair)
                return true;
            bool success = false;

            try
            {
                if (AIERepair.SystemsCheck(tank, mind.TechMemor) && PreRepairPrep(tank, mind))
                {
                    mind.TechMemor.RushAttachOpIfNeeded();
                    if (RepairAttempts == 0)
                        RepairAttempts = mind.TechMemor.ReturnContents().Count();

                    //bool hardest = KickStart.EnemyBlockDropChance == 0;
                    List<TankBlock> fBlocks = AIERepair.FindBlocksNearbyTank(tank, mind.TechMemor);
                    List<BlockTypes> typesMissing = mind.TechMemor.GetMissingBlockTypes();

                    RawTechLoader.ResetSkinIDSet();
                    while (RepairAttempts > 0)
                    {
                        bool worked = EnemyRepairLerp(tank, mind, ref fBlocks, ref typesMissing, true);
                        if (!worked)
                            break;
                        if (!AIERepair.SystemsCheck(tank, mind.TechMemor))
                        {
                            success = true;
                            break;
                        }
                        RepairAttempts--;
                    }
                    mind.TechMemor.UpdateMissingBlockTypes(typesMissing);
                }
            }
            catch { } // it failed - [patch later]
            if (ManNetwork.IsNetworked)
                return true;
            return success;
        }
        public static bool EnemyRepairStepper(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind, bool Super = false)
        {
            /*
            Debug.Log("TACtical_AI: Enemy AI " + tank.name + ": - EnemyRepairStepper "
                + mind.TechMemor.blockIntegrityDirty + " | " + thisInst.PendingSystemsCheck);
            */
            if (!(bool)mind.TechMemor)
            {
                Debug.Log("TACtical_AI: Enemy AI " + tank.name + ":  Tried to call EnemyRepairStepper when TechMemor is NULL");
                return false;
            }
            if (mind.TechMemor.ReserveSuperGrabs > 0)
            {
                thisInst.PendingSystemsCheck = !EnemyInstaRepair(tank, mind, mind.TechMemor.ReserveSuperGrabs);
                mind.TechMemor.ReserveSuperGrabs = 0;
            }
            else if (thisInst.repairStepperClock <= 0)
            {
                //thisInst.AttemptedRepairs = 0;
                int prevVal = thisInst.repairStepperClock;
                if (Templates.SpecialAISpawner.CreativeMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.Backspace))
                {
                    thisInst.repairStepperClock = 1;
                    thisInst.TechMemor.ReserveSuperGrabs = 9;
                }
                else if (tank.IsAnchored)
                {   // Enemy bases must be allowed to build or they will not work!
                    if (mind.Provoked == 0)
                    {
                        if (!Super)
                            thisInst.repairStepperClock = AIERepair.bDelaySafe;
                        else
                            thisInst.repairStepperClock = AIERepair.bDelaySafe / 2;
                    }
                    else
                    {
                        if (!Super)
                            thisInst.repairStepperClock = AIERepair.bDelayCombat;
                        else
                            thisInst.repairStepperClock = AIERepair.bDelayCombat / 2;
                    }
                }
                else
                {
                    if (!KickStart.AllowAISelfRepair)
                        return false;
                    if (mind.Provoked == 0)
                    {
                        if (!Super)
                            thisInst.repairStepperClock = AIERepair.eDelaySafe / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                        else
                            thisInst.repairStepperClock = (AIERepair.eDelaySafe / 4) / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                    }
                    else
                    {
                        if (!Super)
                            thisInst.repairStepperClock = AIERepair.eDelayCombat / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                        else
                            thisInst.repairStepperClock = (AIERepair.eDelayCombat / 4) / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                    }
                }
                if (thisInst.PendingSystemsCheck) //&& thisInst.AttemptedRepairs == 0)
                {
                    try
                    {
                        if (thisInst.repairStepperClock < 1)
                            thisInst.repairStepperClock = 1;
                        int initialBlockCount = tank.blockman.blockCount;
                        int OverdueTime = Mathf.Abs(prevVal / thisInst.repairStepperClock);
                        if (OverdueTime > 1)
                        {
                            thisInst.PendingSystemsCheck = !EnemyInstaRepair(tank, mind, OverdueTime);
                        }
                        else if (AIERepair.SystemsCheck(tank, mind.TechMemor) && PreRepairPrep(tank, mind))
                        {   // Cheaper to check twice than to use GetMissingBlockTypes when not needed.
                            mind.TechMemor.RushAttachOpIfNeeded();
                            //bool hardest = KickStart.EnemyBlockDropChance == 0;
                            List<TankBlock> fBlocks = AIERepair.FindBlocksNearbyTank(tank, mind.TechMemor);
                            List<BlockTypes> typesMissing = mind.TechMemor.GetMissingBlockTypes();
                            if (ManNetwork.IsNetworked)
                            {
                                if (EnemyRepairLerp(tank, mind, ref fBlocks, ref typesMissing))
                                {
                                    thisInst.PendingSystemsCheck = AIERepair.SystemsCheck(tank, mind.TechMemor);
                                }
                                else
                                    thisInst.PendingSystemsCheck = false; // cannot repair as invalid block
                            }
                            else
                            {
                                if (QueueEnemyRepairLerp(tank, mind, ref fBlocks, ref typesMissing))
                                    thisInst.PendingSystemsCheck = AIERepair.SystemsCheck(tank, mind.TechMemor);
                                else
                                    thisInst.PendingSystemsCheck = false; // cannot repair as invalid block
                            }
                            mind.TechMemor.UpdateMissingBlockTypes(typesMissing);
                            //thisInst.AttemptedRepairs = 1;
                        }
                        else
                            thisInst.PendingSystemsCheck = false;

                        if (!thisInst.PendingSystemsCheck)
                        {
                            if (mind.StartedAnchored)
                            {
                                mind.AIControl.AdjustAnchors();
                                mind.TechMemor.MakeMinersMineUnlimited();
                            }
                            thisInst.FinishedRepairEvent.Send();
                            if (initialBlockCount != tank.blockman.blockCount)
                            {
                                Debug.Log("TACtical_AI: EnemyRepairStepper - Done for " + tank.name + ": Job Finished.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("TACtical_AI: EnemyRepairStepper(Main) - Error on handling Enemy AI " + tank.name + ":  " + e);
                    }
                }
            }
            else
                thisInst.repairStepperClock -= KickStart.AIClockPeriod;

            if (mind.TechMemor.ReserveSuperGrabs < 0)
                mind.TechMemor.ReserveSuperGrabs = 0;
            return thisInst.PendingSystemsCheck;
        }


        // EXPERIMENTAL - AI-Based new Tech building
        public static bool EnemyNewTechConstruction(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (thisInst.PendingSystemsCheck)// && thisInst.AttemptedRepairs == 0)
            {
                thisInst.PendingSystemsCheck = !EnemyInstaRepair(tank, mind, mind.TechMemor.ReturnContents().Count() + 10);
            }
            else
            {
                if (thisInst.repairStepperClock == 1)
                {
                    //thisInst.AttemptedRepairs = 0;
                    thisInst.repairStepperClock = 0;
                }
                else if (thisInst.repairStepperClock == 0)
                    thisInst.repairStepperClock = 20;
                else
                    thisInst.repairStepperClock--;
            }
            return thisInst.PendingSystemsCheck;
        }
    }
}
