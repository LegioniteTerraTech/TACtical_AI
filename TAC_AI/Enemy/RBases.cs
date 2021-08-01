using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI.AI.Enemy
{
    public static class RBases
    {
        public static List<EnemyBaseFunder> EnemyBases = new List<EnemyBaseFunder>();

        public static EnemyBaseFunder GetTeamFunder(int Team)
        {
            List<EnemyBaseFunder> baseFunders = EnemyBases.FindAll(delegate (EnemyBaseFunder cand) { return cand.Team == Team; });
            if (baseFunders.Count == 0)
            {
                //Debug.Log("TACtical_AI: " + Team + " CALLED GetTeamFunds WITH NO BASE!!!");
                return null;
            }
            if (baseFunders.Count > 1)
            {
                //Debug.Log("TACtical_AI: " + Team + " has " + baseFunders.Count + " bases on scene. The richest will be selected.");
                EnemyBaseFunder funder = null;
                int highestFunds = 0;
                foreach (EnemyBaseFunder funds in baseFunders)
                {
                    if (highestFunds < funds.BuildBucks)
                    {
                        highestFunds = funds.BuildBucks;
                        funder = funds;
                    }
                }
                return funder;
            }
            return baseFunders.First();
        }
        public static int GetTeamFunds(int Team)
        {
            EnemyBaseFunder funder = GetTeamFunder(Team);
            if (funder.IsNull())
            {
                return 0;
            }
            return funder.BuildBucks;
        }
        public static bool PurchasePossible(BlockTypes bloc, int Team)
        {
            if (Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true) <= GetTeamFunds(Team))
                return true;
            return false;
        }
        public static bool TryMakePurchase(BlockTypes bloc, int Team)
        {
            if (PurchasePossible(bloc, Team))
            {
                var funds = GetTeamFunder(Team);
                funds.SetBuildBucks(funds.BuildBucks - Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(bloc, true));
                return true;
            }
            return false;
        }
        public static int GetEnemyHQCount()
        {
            return EnemyBases.FindAll(delegate (EnemyBaseFunder funds) { return funds.isHQ; }).Count;
        }

        public class EnemyBaseFunder : MonoBehaviour
        {
            public Tank Tank;
            public int Team { get { return Tank.Team; } }
            public int BuildBucks { get { return buildBucks; } }
            private int buildBucks = 5000;
            public bool isHQ = false;

            public void Initiate(Tank tank)
            {
                Tank = tank;
                tank.TankRecycledEvent.Subscribe(OnRecycle);
                EnemyBases.Add(this);
            }
            public void OnRecycle(Tank tank)
            {
                tank.TankRecycledEvent.Unsubscribe(OnRecycle);
                EnemyBases.Remove(this);
                Destroy(this);
            }
            public void AddBuildBucks(int toAdd)
            {
                SetBuildBucks(buildBucks + toAdd);
            }
            public void SetBuildBucks(int newVal, bool noNameChange = false)
            {
                if (!noNameChange)
                {
                    StringBuilder nameActual = new StringBuilder();
                    char lastIn = 'n';
                    bool doingBB = false;
                    foreach (char ch in name)
                    {
                        if (!doingBB)
                        {
                            if (ch == '¥' && lastIn == '¥')
                            {
                                nameActual.Remove(nameActual.Length - 2, 2);
                                doingBB = true;
                            }
                            else
                                nameActual.Append(ch);
                            lastIn = ch;
                        }
                    }
                    if (newVal == -1)
                    {
                        nameActual.Append(" ¥¥ Inf");
                    }
                    else
                        nameActual.Append(" ¥¥" + newVal);
                    Tank.SetName(nameActual.ToString());
                }
                buildBucks = newVal;
            }
            public int GetBuildBucksFromName(string name)
            {
                StringBuilder funds = new StringBuilder();
                char lastIn = 'n';
                bool doingBB = false;
                foreach (char ch in name)
                {
                    if (!doingBB)
                    {
                        if (ch == '¥' && lastIn == '¥')
                        {
                            doingBB = true;
                        }
                        lastIn = ch;
                    }
                    else    // Get the base's "saved" funds
                    {
                        funds.Append(ch);
                    }
                }
                if (!doingBB)
                    return 0;
                string Funds = funds.ToString();
                if (Funds == " Inf")
                {
                    return -1;
                }
                bool worked = int.TryParse(Funds, out int Output);
                if (!worked)
                {
                    Debug.Log("TACtical_AI: BuildBucks corrupted for tech " + name + ", returning 0");
                    return 0;
                }
                return Output;
            }
            public string GetActualName(string name)
            {
                StringBuilder nameActual = new StringBuilder();
                char lastIn = 'n';
                foreach (char ch in name)
                {
                    if (ch == '¥' && lastIn == '¥')
                    {
                        nameActual.Remove(nameActual.Length - 2, 2);
                        break;
                    }
                    else
                        nameActual.Append(ch);
                    lastIn = ch;
                }
                return nameActual.ToString();
            }
        }

        public static bool SetupBaseAI(AIECore.TankAIHelper thisInst, Tank tank, EnemyMind mind)
        {   // iterate through EVERY BASE dammit
            string name = tank.name;
            bool DidFire = false;
            if (name == "INSTANTIATED_BASE")
            {   //It's a base spawned by this mod
                tank.Anchors.TryAnchorAll(true);
                DidFire = true;
            }
            if (tank.GetComponent<Templates.BookmarkBuilder>())
            {
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                //mind.InvertBullyPriority = true;
                //mind.EvilCommander = EnemyHandling.Starship;
                mind.CommanderAttack = EnemyAttack.Grudge;
                //mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.MissionTrigger;
                var builder = tank.GetComponent<Templates.BookmarkBuilder>();
                mind.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                if (mind.TechMemor.IsNull())
                {
                    mind.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                    mind.TechMemor.Initiate();
                }
                mind.TechMemor.SetupForNewTechConstruction(thisInst, builder.blueprint);
                tank.MainCorps = new List<FactionSubTypes> { builder.faction };
                if (builder.faction != FactionSubTypes.NULL)
                {
                    tank.MainCorps = new List<FactionSubTypes> { builder.faction };
                    mind.MainFaction = builder.faction;
                    //Debug.Log("TACtical_AI: Tech " + tank.name + " set faction " + tank.GetMainCorp().ToString());
                }
                AIERepair.Turboconstruct(tank, mind.TechMemor, true);

                RCore.BlockSetEnemyHandling(tank, mind, true);
                RCore.RandomSetMindAttack(mind, tank);

                if (builder.unprovoked)
                {
                    mind.CommanderMind = EnemyAttitude.SubNeutral;
                }

                UnityEngine.Object.DestroyImmediate(builder);
                DidFire = true;
                //Debug.Log("TACtical_AI: Tech " + tank.name + " is ready to roll!  " + mind.EvilCommander.ToString() + " based enemy with attitude " + mind.CommanderAttack.ToString() + " | Mind " + mind.CommanderMind.ToString() + " | Smarts " + mind.CommanderSmarts.ToString() + " inbound!");
            }
            if (name.Contains(" ¥¥"))
            {
                if (name.Contains("#"))
                {
                    //It's not a base
                    StringBuilder nameNew = new StringBuilder();
                    char lastIn = 'n';
                    foreach (char ch in name)
                    {
                        if (ch == '¥' && lastIn == '¥')
                        {
                            nameNew.Remove(nameNew.Length - 2, 2);
                            break;
                        }
                        else
                            nameNew.Append(ch);
                        lastIn = ch;
                    }
                    nameNew.Append(" Minion");
                    tank.SetName(nameNew.ToString());
                    // it's a minion of the base
                }
                else
                {
                    var funds = tank.gameObject.AddComponent<EnemyBaseFunder>();
                    funds.Initiate(tank);
                    funds.SetBuildBucks(funds.GetBuildBucksFromName(name), true);
                    mind.TechMemor = tank.gameObject.GetComponent<AIERepair.DesignMemory>();
                    if (mind.TechMemor.IsNull())
                    {
                        mind.TechMemor = tank.gameObject.AddComponent<AIERepair.DesignMemory>();
                        mind.TechMemor.Initiate();
                    }
                    Templates.SpawnBaseTypes type = Templates.RawTechLoader.GetEnemyBaseTypeFromName(funds.GetActualName(name));
                    Debug.Log("TACtical_AI: " + funds.GetActualName(name) + " |type " + type.ToString());
                    SetupBaseType(type, mind);
                    mind.TechMemor.SetupForNewTechConstruction(thisInst, Templates.RawTechLoader.GetBlueprint(type));
                    mind.TechMemor.unlimitedParts = Templates.RawTechLoader.GetEnemyBaseSupplies(type);
                    tank.MainCorps.Add(Templates.RawTechLoader.GetMainCorp(type));
                    tank.Anchors.TryAnchorAll(true);
                    DidFire = true;
                }
            }

            return DidFire;
        }

        public static void SetupBaseType(SpawnBaseTypes type, EnemyMind mind)
        {   // iterate through EVERY BASE dammit
            if (RawTechLoader.IsHQ(type))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.InvertBullyPriority = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderAttack = EnemyAttack.Bully;
                mind.CommanderMind = EnemyAttitude.Homing;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
            else if (RawTechLoader.ContainsPurpose(type, BasePurpose.Harvesting))
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EnemyAttack.Grudge;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
            else
            {
                mind.StartedAnchored = true;
                mind.AllowInvBlocks = true;
                mind.AllowRepairsOnFly = true;
                mind.EvilCommander = EnemyHandling.Stationary;
                mind.CommanderMind = EnemyAttitude.Default;
                mind.CommanderSmarts = EnemySmarts.IntAIligent;
                mind.CommanderAttack = EnemyAttack.Spyper;
                mind.CommanderBolts = EnemyBolts.AtFull;
            }
        }
    }
}
