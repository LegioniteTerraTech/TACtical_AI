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
        private static readonly FieldInfo deals = typeof(WeaponRound).GetField("m_Damage", BindingFlags.NonPublic | BindingFlags.Instance),
            bDPS = typeof(BeamWeapon).GetField("m_DamagePerSecond", BindingFlags.NonPublic | BindingFlags.Instance),
            burn = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);

        // Bully-like
        private const int OHKOCapableDamage = 1750;

        // Spyper AI
        private const int SnipeVelo = 140;
        private const int RangedRange = 75;

        // Circle AI
        private const int CircleRange = 170;
        private const int MinCircleSpeed = 140;

        public static bool HasArtilleryWeapon(BlockManager BM)
        {
            FireData FD;
            foreach (var item in BM.IterateBlockComponents<ModuleWeaponGun>())
            {
                FD = item.GetComponent<FireData>();
                if ((int)item.GetComponent<TankBlock>().BlockType > Enum.GetValues(typeof(BlockTypes)).Length)
                {
                    if (FD && FD.m_MuzzleVelocity >= SnipeVelo)
                    {
                        var bullet = FD.m_BulletPrefab;
                        if (bullet)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    switch (item.GetComponent<TankBlock>().BlockType)
                    {
                        case BlockTypes.GSOMegatonLong_242:
                        case BlockTypes.GSOBigBertha_845:
                        //case BlockTypes.VENRPGLauncher_122: // More of a kiting weapon
                        case BlockTypes.HE_Mortar_232:
                        case BlockTypes.HE_Cannon_Naval_826:
                        case BlockTypes.HE_Cruise_Missile_51_121:
                        case BlockTypes.HE_CannonBattleship_216:
                        case BlockTypes.BF_MissilePod_323:
                        case BlockTypes.EXP_Cannon_Repulsor_444:
                        case BlockTypes.EXP_MissilePod_424:
                            return true;
                    }
                }
            }
            return false;
        }

        public static EnemyAttack GetAttackStrat(Tank tank, EnemyMind mind)
        {
            bool smolTech = false;
            if (KickStart.isTweakTechPresent && tank.blockman.blockCount <= AIGlobals.SmolTechBlockThreshold)
            {   // Small Techs should play it mobile
                smolTech = true;
            }

            EnemyAttack attack = EnemyAttack.Grudge;
            int strongWeaps = 0; // Weapons with damage surpassing 1750
            int rangedWeaps = 0; // Weapons with velocity >= 140 or can shoot further than 75
            int circleWeaps = 0; // Weapons with horizontal aiming range >= 170 and aiming speed of at least 60
            int fastWeaps = 0;   // Weapons with horizontal aiming speed >= 140
            int meleeWeaps = 0;  // Drill, Tesla or Flamethrowers
            Vector3 weaponsAngleBias = Vector3.zero;
            int count = 0;

            // Learn from what weapons we have on our Tech:
            foreach (ModuleWeapon weap in tank.blockman.IterateBlockComponents<ModuleWeapon>())
            {
                count++;
                var fD = weap.GetComponent<FireData>();
                var gA = weap.GetComponentsInChildren<GimbalAimer>();
                bool CircleAiming = false;
                if (gA.Count() > 0 && weap.RotateSpeed >= 60)// Minimum allowed rotation speed for circling
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
                            if ((int)deals.GetValue(round) >= OHKOCapableDamage)
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
                        if ((int)bDPS.GetValue(fDL) >= OHKOCapableDamage)
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

            // Sort based on weapon abilities:
            sortList = sortList.OrderBy(x => x.Key).ToList();
            bool isStrong = false;  // High Alpha weapons
            bool isRanged = false;  // Ranged weapons
            bool isRaider = false;  // Circle weapons
            bool isFast = false;    // weapons that aim fast
            bool isMelee = false;
            bool Forwards = (weaponsAngleBias / count).z > 0.7f;

            // Pick the top two canidates to determine our combat mindset:
            switch (sortList.ElementAt(4).Value)
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
            //Debug.Log("TACtical_AI: Enemy AI " + tank.name + " Combat type " + sortList.ElementAt(0).Value + " | " + sortList.ElementAt(1).Value);

            // Determine based on Tech Size and driving class:
            // Because we want the combat to not be irritating, circle should only be used if the player has target leading
            switch (mind.EvilCommander)
            {
                case EnemyHandling.Stationary: // NEVER use circle on a static defense
                    if (isStrong && (isMelee || isFast || Forwards))
                        attack = EnemyAttack.Bully;
                    else if ((isStrong || Forwards) && isRanged && !isMelee)
                        attack = EnemyAttack.Spyper;
                    else if (isFast && (isRaider || isStrong))
                        attack = EnemyAttack.Pesterer;
                    break;
                case EnemyHandling.Airplane: // Try use our height and speed to our advantage
                    if (smolTech)
                    {
                        if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                        else if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                        else if ((isStrong || Forwards) && isRanged)
                            attack = EnemyAttack.Spyper;
                    }
                    else
                    {
                        if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                        else if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                        else if((isStrong || Forwards) && isRanged)
                            attack = EnemyAttack.Spyper;
                    }
                    break;
                case EnemyHandling.Chopper: // Try use our height to our advantage
                    if (smolTech)
                    {
                        if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                    }
                    else
                    {
                        if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                        else if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                    }
                    break;
                case EnemyHandling.Starship: // Abuse the crab out of our absurd mobility
                    if (smolTech)
                    {
                        if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                    }
                    else
                    {
                        if (isStrong && (isMelee || isFast || Forwards))
                        {
                            attack = EnemyAttack.Bully;
                            mind.InvertBullyPriority = true; // Probably can rip a new one
                        }
                        else if((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper; // Most large Spaceships feature a large forwards weapons array
                        else if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                    }
                    break;
                case EnemyHandling.Naval: // Abuse the sea
                    if (smolTech)
                    {
                        if (!Forwards)
                            attack = EnemyAttack.Circle;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                        else if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                    }
                    else
                    {
                        if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                        else if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                    }
                    break;
                default:    // Likely Ground
                    if (smolTech)
                    {
                        if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper;
                        else if (!Forwards)
                            attack = EnemyAttack.Circle;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                    }
                    else
                    {
                        if (!Forwards)
                            attack = EnemyAttack.Circle;
                        else if (isFast && (isRaider || isStrong))
                            attack = EnemyAttack.Pesterer;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EnemyAttack.Spyper;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EnemyAttack.Bully;
                    }
                    break;
            }
            return attack;
        }
    }
}
