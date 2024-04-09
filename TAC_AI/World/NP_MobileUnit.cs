using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;

namespace TAC_AI.World
{
    public class NP_MobileUnit : NP_TechUnit
    {
        public float MoveSpeed { get; internal set; } = -1;
        public bool IsAirborne { get; internal set; } = false;
        public readonly bool isFounder = false;

        public NP_MobileUnit(ManSaveGame.StoredTech techIn, NP_Presence_Automatic team, FactionSubTypes FST) :
            base(techIn, team, FST, ManEnemyWorld.MobileHealthMulti)
        {
            isFounder = tech.m_TechData.IsTeamFounder();
            ManEnemyWorld.GetExpectedSpeedAsync(this);
        }
        public static float GetSpeedLegacy(ManSaveGame.StoredTech tech, FactionSubTypes faction)
        {
            float MoveSpeed = 0;
            if (!tech.m_TechData.CheckIsAnchored() && !tech.m_TechData.Name.Contains(" " + RawTechLoader.turretChar))
            {
                MoveSpeed = 25;
                if (ManEnemyWorld.corpSpeeds.TryGetValue(faction, out float sped))
                    MoveSpeed = sped;
            }
            return MoveSpeed;
        }


        public override bool Exists()
        {
            return teamInst.IsValidAndRegistered() && teamInst.EMUs.Contains(this);
        }

        public override float GetSpeed()
        {
            if (MoveSpeed > 0)
                return MoveSpeed;
            return 0;
        }
        public override float GetEvasion()
        {
            return MoveSpeed * ManEnemyWorld.MobileSpeedToEvasion;
            return ManEnemyWorld.BaseEvasion;
        }

        internal override void MovementSceneDelta(float timeDelta)
        {
            trackedVis.StopTracking();
            Vector3 actualPosScene = PosScene;
            Vector3 posDelta = (trackedVis.GetWorldPosition().ScenePosition - actualPosScene) + (UnityEngine.Random.insideUnitCircle * MoveSpeed * timeDelta).ToVector3XZ();
            Vector3 Max = Vector3.one * 64;
            Vector3 finalPos = posDelta.Clamp(-Max, Max) + actualPosScene;
            trackedVis.SetPos(finalPos);
            //DebugTAC_AI.Log("MovementSceneDelta for " + Name + " with val " + finalPos);
        }

        /// <summary>
        /// Deal damage to this Tech
        /// </summary>
        /// <param name="dealt"></param>
        /// <returns>True if tech destroyed</returns>
        public override bool RecieveDamage(int Dealt)
        {
            //ManEnemyWorld.GetTeam(tech.m_TeamID).SetAttackMode(tilePos);
            if (MaxShield > 0)
            {
                Shield -= Dealt;
                if (Shield <= 0)
                {
                    Health += Shield;
                    Shield = 0;
                }
                NP_Presence_Automatic.ReportCombat("Tech " + Name + " has received " + Dealt + " damage | Health " + Health
                    + " | Shield " + Shield);
            }
            else
            {
                Health -= Dealt;
                NP_Presence_Automatic.ReportCombat("Tech " + Name + " has received " + Dealt + " damage | Health " + Health);
            }
            return Health < 0;
        }
    }
}
