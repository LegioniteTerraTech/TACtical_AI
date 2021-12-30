using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI.World
{
    public class EnemyTechUnit
    {
        public ManSaveGame.StoredTech tech;
        public FactionTypesExt Faction = FactionTypesExt.GSO;
        public IntVector2 tilePos;
        public long Health = 0;
        public long MaxHealth = 0;
        public float MoveSpeed = 0;
        public int AttackPower = 0;
        public bool isMoving = false;
        public bool isArmed = false;
        public bool canHarvest = false;

        public EnemyTechUnit(IntVector2 tilePosition, ManSaveGame.StoredTech techIn)
        {
            tilePos = tilePosition;
            tech = techIn;
        }

        /// <summary>
        /// Deal damage to this Tech
        /// </summary>
        /// <param name="dealt"></param>
        /// <returns>If tech destroyed</returns>
        public bool TakeDamage(int Dealt)
        {
            EnemyWorldManager.GetTeam(tech.m_TeamID).SetEvent(tilePos);
            Health -= Dealt;
            Debug.Log("TACtical_AI: EnemyPresence - Enemy " + tech.m_TechData.Name + " has received " + Dealt + " damage | Health " + Health);
            return Health < 0;
        }
    }
}
