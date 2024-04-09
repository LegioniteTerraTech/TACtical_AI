using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TAC_AI.AI
{
    // Sets up all important AI statistics based on AI core
    internal static class EWeapSetup
    {
        internal static readonly FieldInfo deals = typeof(WeaponRound).GetField("m_Damage", BindingFlags.NonPublic | BindingFlags.Instance),
            bDPS = typeof(BeamWeapon).GetField("m_DamagePerSecond", BindingFlags.NonPublic | BindingFlags.Instance),
            burn = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);

        // Bully-like
        internal const int OHKOCapableDamage = 1750;

        // Spyper AI
        internal const int SnipeVelo = 140;
        internal const int RangedRange = 75;
        internal static HashSet<BlockTypes> ranged = new HashSet<BlockTypes>()
        {
                        BlockTypes.GSOMegatonLong_242,
                        BlockTypes.GSOBigBertha_845,
                        //BlockTypes.VENRPGLauncher_122, // More of a kiting weapon
                        BlockTypes.HE_Mortar_232,
                        BlockTypes.HE_Cannon_Naval_826,
                        BlockTypes.HE_Cruise_Missile_51_121,
                        BlockTypes.HE_CannonBattleship_216,
                        BlockTypes.BF_MissilePod_323,
                        BlockTypes.EXP_Cannon_Repulsor_444,
                        BlockTypes.EXP_MissilePod_424,
        };

        // Circle AI
        internal const int CircleRange = 170;
        internal const int MinCircleSpeed = 140;

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
                    if (ranged.Contains(item.GetComponent<TankBlock>().BlockType))
                        return true;
                }
            }
            return false;
        }

        public static EAttackMode GetAttackStrat(Tank tank, TankAIHelper help)
        {
            bool smolTech = false;
            if (KickStart.isTweakTechPresent && tank.blockman.blockCount <= AIGlobals.SmolTechBlockThreshold)
            {   // Small Techs should play it mobile
                smolTech = true;
            }

            EAttackMode attack = EAttackMode.Chase;
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
            //DebugTAC_AI.Log(KickStart.ModID + ": Enemy AI " + tank.name + " Combat type " + sortList.ElementAt(0).Value + " | " + sortList.ElementAt(1).Value);

            // Determine based on Tech Size and driving class:
            // Because we want the combat to not be irritating, circle should only be used if the player has target leading
            switch (help.DediAI)
            {
                case AIType.MTMimic:
                case AIType.MTStatic:
                case AIType.MTTurret: // NEVER use circle on a static defense
                    if (isStrong && (isMelee || isFast || Forwards))
                        attack = EAttackMode.Strong;
                    else if ((isStrong || Forwards) && isRanged && !isMelee)
                        attack = EAttackMode.Ranged;
                    else if (isFast && (isRaider || isStrong))
                        attack = EAttackMode.Random;
                    break;
                case AIType.Aviator:
                    if (help.MovementController is AIControllerAir air)
                    {
                        if (air.FlyStyle == AIControllerAir.FlightType.Helicopter)
                        {   // Try use our height to our advantage
                            if (smolTech)
                            {
                                if (isFast && (isRaider || isStrong))
                                    attack = EAttackMode.Random;
                                else if ((isStrong || Forwards) && isRanged && !isMelee)
                                    attack = EAttackMode.Ranged;
                                else if (isStrong && (isMelee || isFast || Forwards))
                                    attack = EAttackMode.Strong;
                                else if (!Forwards)
                                    attack = EAttackMode.Circle;
                            }
                            else
                            {
                                if ((isStrong || Forwards) && isRanged && !isMelee)
                                    attack = EAttackMode.Ranged;
                                else if (isStrong && (isMelee || isFast || Forwards))
                                    attack = EAttackMode.Strong;
                                else if (isFast && (isRaider || isStrong))
                                    attack = EAttackMode.Random;
                                else if (!Forwards)
                                    attack = EAttackMode.Circle;
                            }
                        }
                        else
                        {   // Try use our height and speed to our advantage
                            if (smolTech)
                            {
                                if (isStrong && (isMelee || isFast || Forwards))
                                    attack = EAttackMode.Strong;
                                else if (isFast && (isRaider || isStrong))
                                    attack = EAttackMode.Random;
                                else if (!Forwards)
                                    attack = EAttackMode.Circle;
                                else if ((isStrong || Forwards) && isRanged)
                                    attack = EAttackMode.Ranged;
                            }
                            else
                            {
                                if (isStrong && (isMelee || isFast || Forwards))
                                    attack = EAttackMode.Strong;
                                else if (isFast && (isRaider || isStrong))
                                    attack = EAttackMode.Random;
                                else if (!Forwards)
                                    attack = EAttackMode.Circle;
                                else if ((isStrong || Forwards) && isRanged)
                                    attack = EAttackMode.Ranged;
                            }
                        }
                    }
                    break;
                case AIType.Astrotech: // Abuse the crab out of our absurd mobility
                    if (smolTech)
                    {
                        if (isFast && (isRaider || isStrong))
                            attack = EAttackMode.Random;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EAttackMode.Ranged;
                        else if (!Forwards)
                            attack = EAttackMode.Circle;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EAttackMode.Strong;
                    }
                    else
                    {
                        if (isStrong && (isMelee || isFast || Forwards))
                        {
                            attack = EAttackMode.Strong;
                        }
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EAttackMode.Ranged; // Most large Spaceships feature a large forwards weapons array
                        else if (isFast && (isRaider || isStrong))
                            attack = EAttackMode.Random;
                        else if (!Forwards)
                            attack = EAttackMode.Circle;
                    }
                    break;
                case AIType.Buccaneer: // Abuse the sea
                    if (smolTech)
                    {
                        if (!Forwards)
                            attack = EAttackMode.Circle;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EAttackMode.Ranged;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EAttackMode.Strong;
                        else if (isFast && (isRaider || isStrong))
                            attack = EAttackMode.Random;
                    }
                    else
                    {
                        if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EAttackMode.Ranged;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EAttackMode.Strong;
                        else if (!Forwards)
                            attack = EAttackMode.Circle;
                        else if (isFast && (isRaider || isStrong))
                            attack = EAttackMode.Random;
                    }
                    break;
                default:    // Likely Ground
                    if (smolTech)
                    {
                        if (isFast && (isRaider || isStrong))
                            attack = EAttackMode.Random;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EAttackMode.Ranged;
                        else if (!Forwards)
                            attack = EAttackMode.Circle;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EAttackMode.Strong;
                    }
                    else
                    {
                        if (!Forwards)
                            attack = EAttackMode.Circle;
                        else if (isFast && (isRaider || isStrong))
                            attack = EAttackMode.Random;
                        else if ((isStrong || Forwards) && isRanged && !isMelee)
                            attack = EAttackMode.Ranged;
                        else if (isStrong && (isMelee || isFast || Forwards))
                            attack = EAttackMode.Strong;
                    }
                    break;
            }
            return attack;
        }
    }
}
