using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using TAC_AI.Templates;

namespace TAC_AI
{
    /// <summary>
    /// This is a VERY big mod.
    ///   We must make it look big like it is.
    /// </summary>
    public static class CustomAttract
    {
        private static readonly FieldInfo state = typeof(ModeAttract).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo spawnNum = typeof(ModeAttract).GetField("spawnIndex", BindingFlags.NonPublic | BindingFlags.Instance),
            rTime = typeof(ModeAttract).GetField("resetAtTime", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static List<SpecialAttract.AttractInfo> Attracts = null;

        internal static List<SpecialAttract.AttractInfo> InitAttracts => new List<SpecialAttract.AttractInfo>()
        {
            new SpecialAttract.AttractInfo(AttractType.BaseVBase.ToString(), 1.25f, null, StartBaseVBase),
            new SpecialAttract.AttractInfo(AttractType.Dogfight.ToString(), 5.25f, null, StartDogfight),
            new SpecialAttract.AttractInfo(AttractType.Harvester.ToString(), 3.75f, null, StartHarvester, null, true),
            new SpecialAttract.AttractInfo(AttractType.BaseSiege.ToString(), 2.25f, null, StartBaseSiege, EndBaseSiege),
            new SpecialAttract.AttractInfo(AttractType.Invader.ToString(), 0.75f, null, StartInvader),
            new SpecialAttract.AttractInfo(AttractType.Misc.ToString(), 0.65f, null, StartMisc),
            new SpecialAttract.AttractInfo(AttractType.NavalWarfare.ToString(), 3.25f, PreStartNaval, StartNaval),
            new SpecialAttract.AttractInfo(AttractType.SpaceBattle.ToString(), 3.25f, null, StartSpaceBattle),
            new SpecialAttract.AttractInfo(AttractType.SpaceInvader.ToString(), 3.25f, null, StartSpaceInvader),
        };
        public static bool StartInvader(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Any);
            return true;
        }

        public static bool StartSpaceInvader(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Space);
            return true;
        }

        public static bool StartHarvester(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            var tanksToConsider = SpecialAttract.GetRandomStartingPositions();

            RawTechPopParams RTF = RawTechPopParams.Default;
            RTF.Purposes = new HashSet<BasePurpose> { BasePurpose.NotStationary, BasePurpose.Harvesting };

            RawTechLoader.TrySpawnSpecificTech(tanksToConsider[0], Vector3.forward, team1, RTF);
            Tank first = ManTechs.inst.IterateTechs().FirstOrDefault();
            RTF.Purposes = new HashSet<BasePurpose> { BasePurpose.Harvesting, BasePurpose.HasReceivers };
            RawTechLoader.TrySpawnSpecificTech(spawn, Vector3.forward, team1, RTF);
            rTime.SetValue(__instance, Time.time + __instance.resetTime);
            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
            SpecialAttract.SetupTechCam(first);
            return false;
        }

        public static bool StartDogfight(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            var tanksToConsider = SpecialAttract.GetRandomStartingPositions();

            for (int step = 0; 3 > step; step++)
            {
                Vector3 position = tanksToConsider[step] + (Vector3.up * 32);
                Vector3 ForeVec = (spawn - tanksToConsider[step]).normalized;
                if (!RawTechLoader.SpawnAttractTech(position - (ForeVec * 12) + (Vector3.up * (16 * step)), ForeVec, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Air))
                    DebugTAC_AI.Log(KickStart.ModID + ": ThrowCoolAIInAttract(Dogfight) - error ~ could not find Tech");
            }
            rTime.SetValue(__instance, Time.time + __instance.resetTime);
            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
            SpecialAttract.SetupTechCam();
            return false;
        }

        public static bool StartSpaceBattle(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            var tanksToConsider = SpecialAttract.GetRandomStartingPositions();

            for (int step = 0; 3 > step; step++)
            {
                Vector3 position = tanksToConsider[step] + (Vector3.up * 14);
                if (RawTechLoader.SpawnAttractTech(position, (spawn - tanksToConsider[step]).normalized, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Space))
                    DebugTAC_AI.Log(KickStart.ModID + ": ThrowCoolAIInAttract(SpaceBattle) - error ~ could not find Tech");
            }
            rTime.SetValue(__instance, Time.time + __instance.resetTime);
            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
            return false;
        }

        public static bool PreStartNaval(ModeAttract __instance)
        {
            if (KickStart.isWaterModPresent)
            {
                KickStart.retryForBote++;
                Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);
                Vector3 offset = Vector3.zero;
                offset.x = -50.0f;
                offset.z = 100.0f;
                //offset.x = -240.0f;
                //offset.z = 442.0f;
                BiomeMap edited = __instance.spawns[0].biomeMap;
                Singleton.Manager<ManWorld>.inst.SeedString = "68unRTyXMrX93DH";
                Singleton.Manager<ManGameMode>.inst.RegenerateWorld(edited, __instance.spawns[1].cameraSpawn.forward, Quaternion.LookRotation(__instance.spawns[1].cameraSpawn.forward, Vector3.up));
                Singleton.Manager<ManTimeOfDay>.inst.EnableSkyDome(enable: true);
                Singleton.Manager<ManTimeOfDay>.inst.EnableTimeProgression(enable: false);
                Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(UnityEngine.Random.Range(8, 18), 0, 0);
                KickStart.SpecialAttractPos = offset;
                Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);

                return false;
            }
            return true;
        }

        public static bool StartNaval(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            var tanksToConsider = SpecialAttract.GetRandomStartingPositions();

            if (KickStart.isWaterModPresent)
            {
                Camera.main.transform.position = KickStart.SpecialAttractPos;
                int removed = 0;
                foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(KickStart.SpecialAttractPos, 2500, AIGlobals.sceneryBitMask))
                {
                    if (vis.resdisp.IsNotNull() && vis.centrePosition.y < -25)
                    {
                        vis.resdisp.RemoveFromWorld(false, true, true, true);
                        removed++;
                    }
                }
                DebugTAC_AI.Log(KickStart.ModID + ": removed " + removed);
                int numToSpawn = 3;
                float rad = 360f / (float)numToSpawn;
                for (int step = 0; numToSpawn > step; step++)
                {
                    Vector3 offset = Quaternion.Euler(0f, (float)step * rad, 0f) * Vector3.forward * 16;
                    Vector3 posSea = KickStart.SpecialAttractPos + offset;

                    Vector3 forward = (KickStart.SpecialAttractPos - posSea).normalized;
                    Vector3 position = posSea;// - (forward * 10);
                    position = AI.Movement.AIEPathing.SnapOffsetToSea(position);

                    if (!RawTechLoader.SpawnAttractTech(position, forward, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Sea))
                        RawTechLoader.SpawnAttractTech(position, forward, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Space);
                }
                //DebugTAC_AI.Log(KickStart.ModID + ": cam is at " + Singleton.Manager<CameraManager>.inst.ca);
                Singleton.Manager<CameraManager>.inst.ResetCamera(KickStart.SpecialAttractPos, Quaternion.LookRotation(Vector3.forward));
                Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);
                rTime.SetValue(__instance, Time.time + __instance.resetTime);
                spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                return false;
            }
            else
                RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Land);
            return true;
        }

        public static bool StartBaseSiege(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            var tanksToConsider = SpecialAttract.GetRandomStartingPositions();

            RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Land, purpose: BasePurpose.AnyNonHQ);
            return true;
        }

        public static void EndBaseSiege(ModeAttract __instance)
        {
            foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
            {
                tech.SetTeam(4114);
            }
            int teamBase = AIGlobals.GetRandomEnemyBaseTeam();
            Singleton.Manager<ManTechs>.inst.CurrentTechs.FirstOrDefault().SetTeam(teamBase);
            Singleton.Manager<ManTechs>.inst.CurrentTechs.ElementAtOrDefault(1).SetTeam(teamBase);
        }

        public static bool StartBaseVBase(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            int team2 = AIGlobals.GetRandomEnemyBaseTeam();
            var tanksToConsider = SpecialAttract.GetRandomStartingPositions();

            RawTechLoader.SpawnAttractTech(spawn + (Vector3.forward * 50), -Vector3.forward, team1, BaseTerrain.Land, purpose: BasePurpose.TechProduction);
            RawTechLoader.SpawnAttractTech(spawn + (Vector3.forward * 25), -Vector3.forward, team1, BaseTerrain.Land);
            RawTechLoader.SpawnAttractTech(spawn - (Vector3.forward * 50), Vector3.forward, team2, BaseTerrain.Land, purpose: BasePurpose.TechProduction);
            RawTechLoader.SpawnAttractTech(spawn - (Vector3.forward * 25), Vector3.forward, team2, BaseTerrain.Land);
            rTime.SetValue(__instance, Time.time + __instance.resetTime);
            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
            return false;
        }
        public static bool StartMisc(ModeAttract __instance)
        {
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
            int team1 = AIGlobals.GetRandomEnemyBaseTeam();
            int team2 = AIGlobals.GetRandomEnemyBaseTeam();
            var tanksToConsider = SpecialAttract.GetRandomStartingPositions();

            for (int step = 0; 3 > step; step++)
            {
                Vector3 position = tanksToConsider[step] + (Vector3.up * 10);

                if (RawTechLoader.SpawnAttractTech(position, (spawn - tanksToConsider[step]).normalized, 
                    AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.AnyNonSea))
                    DebugTAC_AI.Log(KickStart.ModID + ": ThrowCoolAIInAttract(Misc) - error ~ could not find Tech");
            }
            RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Air);
            rTime.SetValue(__instance, Time.time + __instance.resetTime);
            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
            return false;
        }


    }
}
