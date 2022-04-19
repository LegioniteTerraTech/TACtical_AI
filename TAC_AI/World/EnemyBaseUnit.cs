using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TAC_AI.AI.Enemy;

namespace TAC_AI.World
{
    public class EnemyBaseUnit : EnemyTechUnit
    {
        public readonly EnemyPresence teamInst;
        public int revenue;
        public int BuildBucks { get { return funds; } set { funds = value; SetBuildBucks(funds); } }
        private int funds = 0;
        public bool isDefense = false;
        public bool isSiegeBase = false;
        public bool isHarvestBase = false;
        public bool isTechBuilder = false;

        /// <summary>
        /// If this Tech has a terminal, it can build any tech from the population
        /// </summary>
        public bool HasTerminal = false;

        public void TryPushMoneyToLoadedInstance()
        {
            if (funds == 0)
                return;
            RBases.EnemyBaseFunder EBF = RBases.EnemyBases.Find(delegate (RBases.EnemyBaseFunder cand) { return cand.Team == teamInst.Team && Name == cand.name; });
            if (EBF)
            {
                Debug.Log("TACtical_AI: EnemyBaseUnloaded - Base " + Name + " pushed funds to loaded tech of ID " + EBF.name);
            }
            else
            {
                Debug.LogError("TACtical_AI: EnemyBaseUnloaded - Base " + Name + " failed to update funds");
            }
        }

        public EnemyBaseUnit(IntVector2 tilePosition, ManSaveGame.StoredTech techIn, EnemyPresence team) : base(tilePosition, techIn)
        {
            tilePos = tilePosition;
            teamInst = team;
        }
        public void SetBuildBucks(int newVal)
        {
            StringBuilder nameActual = new StringBuilder();
            char lastIn = 'n';
            bool doingBB = false;
            foreach (char ch in Name)
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
            Name = nameActual.ToString();
        }
        public static string GetActualName(string name)
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

}
