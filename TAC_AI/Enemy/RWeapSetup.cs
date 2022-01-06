using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace TAC_AI.AI.Enemy
{
    // Sets up all important AI statistics based on AI core
    internal static class RWeapSetup
    {
        private static FieldInfo deals = typeof(WeaponRound).GetField("m_Damage", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo bDPS = typeof(BeamWeapon).GetField("m_DamagePerSecond", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo burn = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);

        private const int SmolTechThreshold = 24;
        private const int EradDamage = 1750;

        private const int SnipeVelo = 140;
        private const int RangedRange = 75;

        private const int CircleRange = 170;
        private const int MinCircleSpeed = 140;

        public static EnemyAttack GetAttackStrat(Tank tank)
        {
            
            if (KickStart.isTweakTechPresent && tank.blockman.IterateBlocks().Count() <= SmolTechThreshold)
            {   // Small Techs should play it mobile
                return EnemyAttack.Circle;
            }

            EnemyAttack attack = EnemyAttack.Grudge;
            int strongWeaps = 0; // Weapons with damage surpassing 1750
            int rangedWeaps = 0; // Weapons with velocity >= 140 or can shoot further than 75
            int circleWeaps = 0; // Weapons with horizontal aiming range >= 170
            int fastWeaps = 0;   // Weapons with horizontal aiming speed >= 200
            int meleeWeaps = 0;  // Drill, Tesla or Flamethrowers
            Vector3 weaponsAngleBias = Vector3.zero;
            int count = 0;

            foreach (ModuleWeapon weap in tank.blockman.IterateBlockComponents<ModuleWeapon>())
            {
                count++;
                var fD = weap.GetComponent<FireData>();
                var gA = weap.GetComponentsInChildren<GimbalAimer>();
                bool CircleAiming = false;
                if (gA.Count() > 0)
                {
                    foreach (GimbalAimer aim in gA)
                    {
                        float rotRange = Mathf.Max(aim.rotationLimits) - Mathf.Min(aim.rotationLimits);
                        if (rotRange >= CircleRange || rotRange.Approximately(0))
                        {
                            circleWeaps++;
                            CircleAiming = true;
                            break;
                        }
                    }
                }
                if (!CircleAiming)
                {
                    weaponsAngleBias += tank.rootBlockTrans.InverseTransformVector(weap.transform.forward);
                }

                if ((bool)fD)
                {
                    if ((bool)fD.m_BulletPrefab)
                    {
                        var round = fD.m_BulletPrefab.GetComponent<WeaponRound>();
                        if ((bool)round)
                        {
                            if ((int)deals.GetValue(round) >= EradDamage)
                                strongWeaps++;
                        }
                        var fDS = weap.GetComponent<FireDataShotgun>();
                        if ((bool)fDS)
                        {
                            if (fDS.m_ShotMaxRange > RangedRange)
                                rangedWeaps++;
                            if (fDS.m_ShotMaxRange > 16)
                            {
                                if (weap.m_RotateSpeed > MinCircleSpeed)
                                    fastWeaps++;
                            }
                            else
                                meleeWeaps++;
                        }
                        else
                        {
                            var Lzr = fD.m_BulletPrefab.GetComponent<LaserProjectile>();
                            var Mle = fD.m_BulletPrefab.GetComponent<MissileProjectile>();
                            var jet = fD.m_BulletPrefab.GetComponentInChildren<BoosterJet>();

                            if ((bool)Mle && (bool)jet)
                            {
                                if ((float)burn.GetValue(jet) > 0.005f)
                                    rangedWeaps++;
                                else if (fD.m_MuzzleVelocity > SnipeVelo)
                                    rangedWeaps++;
                            }
                            else
                            {
                                if (fD.m_MuzzleVelocity > SnipeVelo || ((bool)Lzr && fD.m_MuzzleVelocity >= 95))
                                    rangedWeaps++;
                                if (fD.m_MuzzleVelocity > 75)
                                {
                                    if (weap.m_RotateSpeed > MinCircleSpeed)
                                        fastWeaps++;
                                }
                                else
                                    meleeWeaps++;
                            }
                        }
                    }
                }
                else
                {
                    var fDL = weap.GetComponentInChildren<BeamWeapon>();
                    if ((bool)fDL)
                    {
                        if ((int)bDPS.GetValue(fDL) >= EradDamage)
                            strongWeaps++;
                        if (fDL.Range > RangedRange)
                            rangedWeaps++;
                        if (fDL.Range > 16 && weap.m_RotateSpeed > MinCircleSpeed)
                            fastWeaps++;
                        else
                            meleeWeaps++;
                    }
                    else
                    {
                        // Assume drill, tesla, or flamethrower
                        meleeWeaps++;
                    }
                }
            }
            List<KeyValuePair<int, int>> sortList = new List<KeyValuePair<int, int>>
            {
                new KeyValuePair<int, int>(circleWeaps, 2),
                new KeyValuePair<int, int>(fastWeaps, 3),
                new KeyValuePair<int, int>(strongWeaps, 0),
                new KeyValuePair<int, int>(rangedWeaps, 1),
                new KeyValuePair<int, int>(meleeWeaps, 4)
            };

            sortList = sortList.OrderBy(x => x.Key).ToList();
            bool isStrong = false;
            bool isRanged = false;
            bool isRaider = false;
            bool isFast = false;
            bool isMelee = false;
            bool Forwards = (weaponsAngleBias / count).z > 0.7f;

            switch (sortList.ElementAt(4).Value) {
                case 0:
                    isStrong = true;
                    break;
                case 1:
                    isRanged = true;
                    break;
                case 2:
                    isRaider = true;
                    break;
                case 3:
                    isFast = true;
                    break;
                case 4:
                    isMelee = true;
                    break;
            }
            switch (sortList.ElementAt(3).Value)
            {
                case 0:
                    isStrong = true;
                    break;
                case 1:
                    isRanged = true;
                    break;
                case 2:
                    isRaider = true;
                    break;
                case 3:
                    isFast = true;
                    break;
                case 4:
                    isMelee = true;
                    break;
            }
            Debug.Log("TACtical_AI: Enemy AI " + tank.name + " Combat type " + sortList.ElementAt(0).Value + " | " + sortList.ElementAt(1).Value);

            // Because we want the combat to not be irritating, circle should only be used if the player has target leading
            if (!Forwards && (KickStart.isTweakTechPresent || KickStart.isWeaponAimModPresent))
                attack = EnemyAttack.Circle;
            else if (isFast && (isRaider || isStrong))
                attack = EnemyAttack.Pesterer;
            else if ((isStrong || Forwards) && isRanged)
                attack = EnemyAttack.Spyper;
            else if (isStrong && (isMelee || isFast || Forwards))
                attack = EnemyAttack.Bully;

            return attack;
        }
    }
}
