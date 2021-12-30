using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.World
{
    public class EnemyBaseUnloaded : EnemyTechUnit
    {
        public readonly EnemyPresence teamInst;
        public int revenue;
        public int Funds { get { return funds; } set { funds = value; SetBuildBucks(funds); } }
        private int funds = 0;
        public bool isSiegeBase = false;
        public bool isHarvestBase = false;
        public bool isTechBuilder = false;

        /// <summary>
        /// If this Tech has a terminal, it can build any tech from the population
        /// </summary>
        public bool HasTerminal = false;

        public EnemyBaseUnloaded(IntVector2 tilePosition, ManSaveGame.StoredTech techIn, EnemyPresence team) : base(tilePosition, techIn)
        {
            tilePos = tilePosition;
            tech = techIn;
            teamInst = team;
        }
        public void SetBuildBucks(int newVal)
        {
            StringBuilder nameActual = new StringBuilder();
            char lastIn = 'n';
            bool doingBB = false;
            foreach (char ch in tech.m_TechData.Name)
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
            tech.m_TechData.Name = nameActual.ToString();
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
