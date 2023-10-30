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
        private static bool CanGrabFromInventory(EnemyMind mind)
        {
            return (KickStart.EnemiesHaveCreativeInventory || mind.AllowInvBlocks || KickStart.AllowEnemiesToStartBases) && (mind.CommanderSmarts >= EnemySmarts.Smrt || mind.BuildAssist);
        }

        private static bool PreRepairPrep(Tank tank, EnemyMind mind)
        {
            List<TankBlock> cBlocks = tank.blockman.IterateBlocks().ToList();
            if (mind.TechMemor.IsNull())
            {
                DebugTAC_AI.LogError("TACtical_AI: EnemyRepairLerp called with no valid EnemyDesignMemory!!!");
                mind.AIControl.InsureTechMemor("PreRepairPrep", true);
                return false;
            }
            int savedBCount = mind.TechMemor.IterateReturnContents().Count;
            int cBCount = cBlocks.Count;
            //DebugTAC_AI.Log("TACtical_AI: saved " + savedBCount + " vs remaining " + cBCount);
            if (savedBCount < cBCount)
            {
                DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + ":  New blocks were added without " +
                    "being saved before building.  Was the player messing with the Tech?");
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
        private static bool EnemyRepairLerp(Tank tank, EnemyMind mind, bool canUseInventory, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing)
        {
            bool hardest = KickStart.EnemyBlockDropChance == 0;
            //DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + ":  Trying to repair");

            if (mind.TechMemor.TryAttachExistingBlockFromListInst(ref typesMissing, ref fBlocks, hardest))
                return true;

            if (canUseInventory)
            {
                //DebugTAC_AI.Log("TACtical AI: EnemyRepairLerp - trying to fix from inventory);
                RawTechLoader.ResetSkinIDSet();
                if (mind.TechMemor.TrySpawnAndAttachBlockFromListWithSkinInst(ref typesMissing, false, true))
                    return true;
            }
            return false;
        }
        private static bool QueueEnemyRepairLerp(Tank tank, EnemyMind mind, bool canUseInventory, ref List<TankBlock> fBlocks, ref List<BlockTypes> typesMissing, bool overrideChecker = false)
        {
            if (ManNetwork.IsNetworked)
                return EnemyRepairLerp(tank, mind, canUseInventory, ref fBlocks, ref typesMissing);
            bool hardest = KickStart.EnemyBlockDropChance == 0;
            //DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + ":  Trying to repair");

            //int attachAttempts = fBlocks.Count();
            //DebugTAC_AI.Log("TACtical AI: EnemyRepairLerp - Found " + attachAttempts + " loose blocks to use");

            if (mind.TechMemor.TryAttachExistingBlockFromList(ref typesMissing, ref fBlocks, hardest))
                return true;

            if (canUseInventory)
            {
                //DebugTAC_AI.Log("TACtical AI: EnemyRepairLerp - trying to fix from inventory);
                RawTechLoader.ResetSkinIDSet();
                if (mind.TechMemor.TrySpawnAndAttachBlockFromListWithSkin(ref typesMissing, false, true))
                    return true;
            }
            return false;
        }

        internal static bool EnemyInstaRepair(Tank tank, EnemyMind mind, int RepairAttempts = 0)
        {
            if (!KickStart.AISelfRepair)
                return true;
            bool success = false;

            try
            {
                if (mind.TechMemor.SystemsCheck() && PreRepairPrep(tank, mind))
                {
                    mind.TechMemor.RushAttachOpIfNeeded();
                    if (RepairAttempts == 0)
                        RepairAttempts = mind.TechMemor.IterateReturnContents().Count();

                    //bool hardest = KickStart.EnemyBlockDropChance == 0;
                    List<TankBlock> fBlocks = mind.TechMemor.FindBlocksNearbyTank();
                    List<BlockTypes> typesMissing = mind.TechMemor.GetMissingBlockTypes();

                    RawTechLoader.ResetSkinIDSet();
                    bool canGrabFromInv = CanGrabFromInventory(mind);
                    while (RepairAttempts > 0)
                    {
                        bool worked = EnemyRepairLerp(tank, mind, canGrabFromInv, ref fBlocks, ref typesMissing);
                        if (!worked)
                            break;
                        if (!mind.TechMemor.SystemsCheck())
                        {
                            success = true;
                            break;
                        }
                        RepairAttempts--;
                    }
                    mind.TechMemor.UpdateMissingBlockTypes(typesMissing);
                }
                else
                {
                    DebugTAC_AI.Log("Stopped repairing for " + tank.name + " - reason: sysCheck - " + mind.TechMemor.SystemsCheck()
                        + " | PreRepair lerp: " + PreRepairPrep(tank, mind));
                }
            }
            catch { } // it failed - [patch later]
            return success;
        }
        internal static bool EnemyRepairStepper(TankAIHelper thisInst, Tank tank, EnemyMind mind, bool Super = false)
        {
            /*
            DebugTAC_AI.Log("TACtical_AI: Enemy AI " + tank.name + ": - EnemyRepairStepper "
                + mind.TechMemor.blockIntegrityDirty + " | " + thisInst.PendingSystemsCheck);
            */
            if (!(bool)mind.TechMemor)
            {
                DebugTAC_AI.Assert("TACtical_AI: Enemy AI " + tank.name + ":  Tried to call EnemyRepairStepper when TechMemor is NULL");
                return false;
            }
            if (mind.TechMemor.ReserveSuperGrabs > 0)
            {
                thisInst.PendingDamageCheck = !EnemyInstaRepair(tank, mind, mind.TechMemor.ReserveSuperGrabs);
                mind.TechMemor.ReserveSuperGrabs = 0;
            }
            else if (thisInst.RepairStepperClock <= 0)
            {
                //thisInst.AttemptedRepairs = 0;
                float prevVal = thisInst.RepairStepperClock;
                if (AIGlobals.TurboAICheat)
                {
                    thisInst.RepairStepperClock = 0;
                    thisInst.TechMemor.ReserveSuperGrabs = 5 * KickStart.AIClockPeriod;
                }
                else if (tank.IsAnchored)
                {   // Enemy bases must be allowed to build or they will not work!
                    if (mind.AIControl.Provoked == 0)
                    {
                        if (!Super)
                            thisInst.RepairStepperClock = AIERepair.bDelaySafe;
                        else
                            thisInst.RepairStepperClock = AIERepair.bDelaySafe / 2;
                    }
                    else
                    {
                        if (!Super)
                            thisInst.RepairStepperClock = AIERepair.bDelayCombat;
                        else
                            thisInst.RepairStepperClock = AIERepair.bDelayCombat / 2;
                    }
                }
                else
                {
                    if (!KickStart.AISelfRepair)
                        return false;
                    if (mind.AIControl.Provoked == 0)
                    {
                        if (!Super)
                            thisInst.RepairStepperClock = AIERepair.eDelaySafe / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                        else
                            thisInst.RepairStepperClock = (AIERepair.eDelaySafe / 4) / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                    }
                    else
                    {
                        if (!Super)
                            thisInst.RepairStepperClock = AIERepair.eDelayCombat / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                        else
                            thisInst.RepairStepperClock = (AIERepair.eDelayCombat / 4) / Mathf.Max((int)mind.CommanderSmarts + 1, 1);
                    }
                }
                if (thisInst.PendingDamageCheck) //&& thisInst.AttemptedRepairs == 0)
                {
                    try
                    {
                        if (thisInst.RepairStepperClock < 1)
                            thisInst.RepairStepperClock = 1;
                        int initialBlockCount = tank.blockman.blockCount;
                        float OverdueTime = Mathf.Abs(prevVal / thisInst.RepairStepperClock);
                        if (OverdueTime >= 2)
                        {
                            int blocksToAdd = Mathf.CeilToInt(OverdueTime);
                            thisInst.PendingDamageCheck = !EnemyInstaRepair(tank, mind, blocksToAdd);
                            thisInst.RepairStepperClock -= (OverdueTime - blocksToAdd) * thisInst.RepairStepperClock;
                        }
                        else if (mind.TechMemor.SystemsCheck() && PreRepairPrep(tank, mind))
                        {   // Cheaper to check twice than to use GetMissingBlockTypes when not needed.
                            thisInst.RepairStepperClock -= OverdueTime * thisInst.RepairStepperClock;
                            mind.TechMemor.RushAttachOpIfNeeded();
                            //bool hardest = KickStart.EnemyBlockDropChance == 0;
                            List<TankBlock> fBlocks = mind.TechMemor.FindBlocksNearbyTank();
                            List<BlockTypes> typesMissing = mind.TechMemor.GetMissingBlockTypes();
                            if (ManNetwork.IsNetworked)
                            {
                                if (EnemyRepairLerp(tank, mind, CanGrabFromInventory(mind), ref fBlocks, ref typesMissing))
                                {
                                    thisInst.PendingDamageCheck = mind.TechMemor.SystemsCheck();
                                }
                                else
                                {
                                    DebugTAC_AI.Log("Stopped repairing for " + tank.name + " - reason: sysCheck - " + mind.TechMemor.SystemsCheck()
                                        + " | PreRepair lerp: " + PreRepairPrep(tank, mind));
                                    thisInst.PendingDamageCheck = false; // cannot repair as invalid block 
                                }
                            }
                            else
                            {
                                QueueEnemyRepairLerp(tank, mind, CanGrabFromInventory(mind), ref fBlocks, ref typesMissing);
                                thisInst.PendingDamageCheck = mind.TechMemor.SystemsCheck();
                            }
                            mind.TechMemor.UpdateMissingBlockTypes(typesMissing);
                            //thisInst.AttemptedRepairs = 1;
                        }
                        else
                        {
                            DebugTAC_AI.Log("Stopped repairing for " + tank.name + " - reason: sysCheck - " + mind.TechMemor.SystemsCheck()
                                + " | PreRepair lerp: " + PreRepairPrep(tank, mind));
                            thisInst.PendingDamageCheck = false;
                        }
                        

                        if (!thisInst.PendingDamageCheck)
                        {
                            if (mind.StartedAnchored)
                            {
                                mind.AIControl.AdjustAnchors();
                                mind.TechMemor.MakeMinersMineUnlimited();
                            }
                            thisInst.FinishedRepairEvent.Send(thisInst);
                            if (mind.TechMemor.IsDesignComplete())
                            {
                                DebugTAC_AI.Log("TACtical_AI: EnemyRepairStepper - Done for " + tank.name + ": Job Finished.");
                            }
                            else
                            {
                                if (mind.TechMemor.ranOutOfParts)
                                    DebugTAC_AI.Log("TACtical_AI: EnemyRepairStepper - Unfinished for " + tank.name + ": No more funds.");
                                else
                                {
                                    DebugTAC_AI.Log("TACtical_AI: EnemyRepairStepper - Unfinished for " + tank.name 
                                        + ": Floating or invalid blocks in memory.  Purging...");
                                    mind.TechMemor.SaveTech();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        DebugTAC_AI.Log("TACtical_AI: EnemyRepairStepper(Main) - Error on handling Enemy AI " + tank.name + ":  " + e);
                    }
                }
            }
            else
                thisInst.RepairStepperClock -= KickStart.AIClockPeriod;

            if (mind.TechMemor.ReserveSuperGrabs < 0)
                mind.TechMemor.ReserveSuperGrabs = 0;
            thisInst.UpdateDamageThreshold();
            return thisInst.PendingDamageCheck;
        }


        // EXPERIMENTAL - AI-Based new Tech building
        public static bool EnemyNewTechConstruction(TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {
            if (thisInst.PendingDamageCheck)// && thisInst.AttemptedRepairs == 0)
            {
                thisInst.PendingDamageCheck = !EnemyInstaRepair(tank, mind, mind.TechMemor.IterateReturnContents().Count() + 10);
            }
            else
            {
                if (thisInst.RepairStepperClock == 1)
                {
                    //thisInst.AttemptedRepairs = 0;
                    thisInst.RepairStepperClock = 0;
                }
                else if (thisInst.RepairStepperClock == 0)
                    thisInst.RepairStepperClock = 20;
                else
                    thisInst.RepairStepperClock--;
            }
            return thisInst.PendingDamageCheck;
        }
    }
}
