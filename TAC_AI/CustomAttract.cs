using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI
{
    /// <summary>
    /// This is a VERY big mod.
    ///   We must make it look big like it is.
    /// </summary>
    internal static class CustomAttract
    {
        private static readonly FieldInfo spawnNum = typeof(ModeAttract).GetField("spawnIndex", BindingFlags.NonPublic | BindingFlags.Instance),
            rTime = typeof(ModeAttract).GetField("resetAtTime", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Dictionary<AttractType, float> weightedAttracts = new Dictionary<AttractType, float> {
            { AttractType.BaseVBase,        1.25f },
            { AttractType.Dogfight,         5.25f },
            { AttractType.Harvester,        3.75f },
            { AttractType.BaseSiege,        2.25f },
            { AttractType.Invader,          0.75f },
            { AttractType.Misc,             0.65f },
            { AttractType.NavalWarfare,     3.25f },
            { AttractType.SpaceBattle,      3.25f },
            { AttractType.SpaceInvader,     1.2f },
        };

        internal static AttractType WeightedDetermineRAND()
        {
            float maxAttractVal = 0;
            foreach (var item in weightedAttracts)
            {
                maxAttractVal += item.Value;
            }
            float select = UnityEngine.Random.Range(0, maxAttractVal);
            foreach (var item in weightedAttracts)
            {
                select -= item.Value;
                if (select <= 0)
                    return item.Key;
            }
            return AttractType.Misc;
        }

        internal static void CheckShouldRestart(ModeAttract __instance)
        {
            FieldInfo state = typeof(ModeAttract).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
            int mode = (int)state.GetValue(__instance);
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Equals))
            {
                UILoadingScreenHints.SuppressNextHint = true;
                Singleton.Manager<ManUI>.inst.FadeToBlack();
                state.SetValue(__instance, 3);
            }
            if (mode == 2)
            {
                if (KickStart.SpecialAttractNum == AttractType.Harvester)
                {
                    bool restart = false;
                    List<Tank> active = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
                    foreach (Tank tonk in active)
                    {
                        if ((tonk.boundsCentreWorldNoCheck - Singleton.cameraTrans.position).magnitude > 125)
                            restart = true;
                    }
                    if (restart == true)
                    {
                        UILoadingScreenHints.SuppressNextHint = true;
                        Singleton.Manager<ManUI>.inst.FadeToBlack();
                        state.SetValue(__instance, 3);
                    }
                }
                else
                {
                    bool restart = true;
                    List<Tank> active = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();
                    foreach (Tank tonk in active)
                    {
                        if (tonk.Weapons.GetFirstWeapon().IsNotNull())
                        {
                            foreach (Tank tonk2 in active)
                            {
                                if (tonk.IsEnemy(tonk2.Team))
                                    restart = false;
                            }
                        }
                        if (tonk.IsSleeping)
                        {
                            foreach (TankBlock block in tonk.blockman.IterateBlocks())
                            {
                                block.damage.SelfDestruct(0.5f);
                            }
                            tonk.blockman.Disintegrate(true, false);
                        }
                    }
                    if (restart == true)
                    {
                        UILoadingScreenHints.SuppressNextHint = true;
                        Singleton.Manager<ManUI>.inst.FadeToBlack();
                        state.SetValue(__instance, 3);
                    }
                }
            }
            if (UseFollowCam)
            {
                if (FollowTech)
                {
                    if (!FollowTech.visible.isActive)
                    {
                        TankCamera instCam = CameraManager.inst.GetCamera<TankCamera>();
                        Tank nextTech = null;
                        foreach (var item in ManTechs.inst.IterateTechs())
                        {
                            if (item.blockman.blockCount > 5)
                            {
                                nextTech = item;
                                break;
                            }
                        }
                        if (nextTech)
                        {
                            FollowTech = nextTech;
                            instCam.ManualZoom(FollowTech.GetCheapBounds() * 1.5f);
                            instCam.SetFollowTech(FollowTech);
                        }
                    }
                    else
                    {
                        if (FollowTech.blockman.blockCount < 6)
                            FollowTech = null;
                        //var help = FollowTech.GetComponent<AI.AIECore.TankAIHelper>();
                        //if (help && help.gr)
                    }
                }
                else
                {
                    TankCamera instCam = CameraManager.inst.GetCamera<TankCamera>();
                    Tank nextTech = null;
                    foreach (var item in ManTechs.inst.IterateTechs())
                    {
                        if (!nextTech && item.blockman.blockCount > 5)
                        {
                            nextTech = item;
                            break;
                        }
                    }
                    if (nextTech)
                    {
                        FollowTech = nextTech;
                        instCam.ManualZoom(FollowTech.GetCheapBounds() * 1.5f);
                        instCam.SetFollowTech(FollowTech);
                    }
                }
            }
        }
        private static bool UseFollowCam = false;
        private static Tank FollowTech;

        internal static bool SetupTerrain(ModeAttract __instance)
        {
            // Testing
            UseFollowCam = false;
            CameraManager.inst.Switch(CameraManager.inst.GetCamera<FramingCamera>());
            bool caseOverride = true;
            AttractType outNum = AttractType.Harvester;

#if DEBUG
                caseOverride = true;
#else
            caseOverride = false;
#endif

            if (UnityEngine.Random.Range(1, 100) > 20 || KickStart.retryForBote == 1 || caseOverride)
            {
                DebugTAC_AI.Log("TACtical_AI: Ooop - the special threshold has been met");
                KickStart.SpecialAttract = true;
                if (KickStart.retryForBote == 1)
                    outNum = AttractType.NavalWarfare;
                else if (!caseOverride)
                    outNum = WeightedDetermineRAND();
                KickStart.SpecialAttractNum = outNum;

                if (KickStart.SpecialAttractNum == AttractType.NavalWarfare)
                {   // Naval Brawl
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
                    else
                    {
                        KickStart.retryForBote = 0;
                        outNum = WeightedDetermineRAND();
                    }
                }
            }
            else
            {
                KickStart.SpecialAttract = false;
            }
            int spawnIndex = (int)spawnNum.GetValue(__instance);
            Singleton.Manager<ManWorld>.inst.SeedString = null;
            Singleton.Manager<ManGameMode>.inst.RegenerateWorld(__instance.spawns[spawnIndex].biomeMap, __instance.spawns[spawnIndex].cameraSpawn.position, __instance.spawns[spawnIndex].cameraSpawn.orientation);
            Singleton.Manager<ManTimeOfDay>.inst.EnableSkyDome(enable: true);
            Singleton.Manager<ManTimeOfDay>.inst.EnableTimeProgression(enable: false);
            Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(UnityEngine.Random.Range(8, 18), 0, 0);//11
            return false;
        }
        private static void SetupTechCam(ModeAttract __instance, Tank target = null)
        {
            UseFollowCam = true;
            //Vector3 frameCamPos = CameraManager.inst.GetCamera<FramingCamera>().transform.position;
            //Quaternion frameCamRot = CameraManager.inst.GetCamera<FramingCamera>().transform.rotation;
            TankCamera instCam = CameraManager.inst.GetCamera<TankCamera>();
            CameraManager.inst.Switch(instCam);
            if (target)
            {
                FollowTech = target;
            }
            else
            {
                FollowTech = null;
                foreach (var item in ManTechs.inst.IterateTechs())
                {
                    if (!FollowTech)
                        FollowTech = item;
                    if (item.rbody)
                    {
                        item.rbody.velocity += item.rootBlockTrans.forward * 45;
                    }
                }
            }
            if (FollowTech)
            {
                instCam.ManualZoom(FollowTech.GetCheapBounds() * 1.5f);
                //instCam.SetFollowSpringStrength(0.05f);
                instCam.SetFollowTech(FollowTech);
                Quaternion look = Quaternion.LookRotation(FollowTech.trans.forward);
                CameraManager.inst.ResetCamera(FollowTech.trans.position + (look * new Vector3(-12, 5, 0)), look);
            }
        }

        // TECH COMBAT
        internal static bool SetupTechsStart(ModeAttract __instance)
        {
            try
            {
                if (KickStart.SpecialAttract)
                {
                    int spawnIndex = (int)spawnNum.GetValue(__instance);
                    Vector3 spawn = __instance.spawns[spawnIndex].vehicleSpawnCentre.position;
                    Singleton.Manager<ManWorld>.inst.GetTerrainHeight(spawn, out float height);
                    spawn.y = height;

                    List<Vector3> tanksToConsider = new List<Vector3>();

                    int numToSpawn = 3;
                    float rad = 360f / (float)numToSpawn;
                    for (int step = 0; step < numToSpawn; step++)
                    {
                        Quaternion rotation = Quaternion.AngleAxis(UnityEngine.Random.value * 360f, Vector3.up);
                        Vector3 offset = Quaternion.Euler(0f, (float)step * rad, 0f) * Vector3.forward * 16;
                        tanksToConsider.Add(__instance.spawns[spawnIndex].vehicleSpawnCentre.position + offset);
                    }

                    AttractType randNum = KickStart.SpecialAttractNum;
                    DebugTAC_AI.Log("TACtical_AI: Pre-Setup for attract type " + randNum.ToString());
                    int team1 = AIGlobals.GetRandomEnemyBaseTeam();
                    int team2 = AIGlobals.GetRandomEnemyBaseTeam();

                    switch (randNum)
                    {
                        case AttractType.SpaceInvader: // space invader

                            RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Space);
                            break;

                        case AttractType.Harvester: // Peaceful harvesting
                            RawTechLoader.SpawnSpecificTech(tanksToConsider[0], Vector3.forward, team1, new List<BasePurpose> { BasePurpose.NotStationary, BasePurpose.Harvesting });
                            Tank first = ManTechs.inst.IterateTechs().FirstOrDefault();
                            RawTechLoader.SpawnSpecificTech(spawn, Vector3.forward, team1, new List<BasePurpose> { BasePurpose.Harvesting, BasePurpose.HasReceivers });
                            rTime.SetValue(__instance, Time.time + __instance.resetTime);
                            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                            //SetupTechCam(__instance, first);
                            return false;

                        case AttractType.Dogfight: // Aircraft fight
                            for (int step = 0; numToSpawn > step; step++)
                            {
                                Vector3 position = tanksToConsider[step] + (Vector3.up * 48);
                                if (!RawTechLoader.SpawnAttractTech(position,  -(spawn - tanksToConsider[step]).normalized, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Air))
                                    DebugTAC_AI.Log("TACtical_AI: ThrowCoolAIInAttract(Dogfight) - error ~ could not find Tech");
                            }
                            rTime.SetValue(__instance, Time.time + __instance.resetTime);
                            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                            SetupTechCam(__instance);
                            return false;

                        case AttractType.SpaceBattle: // Airship assault
                            for (int step = 0; numToSpawn > step; step++)
                            {
                                Vector3 position = tanksToConsider[step] + (Vector3.up * 14);
                                if (RawTechLoader.SpawnAttractTech(position, (spawn - tanksToConsider[step]).normalized, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Space))
                                    DebugTAC_AI.Log("TACtical_AI: ThrowCoolAIInAttract(SpaceBattle) - error ~ could not find Tech");
                            }
                            rTime.SetValue(__instance, Time.time + __instance.resetTime);
                            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                            return false;

                        case AttractType.NavalWarfare: // Naval Brawl
                            if (KickStart.isWaterModPresent)
                            {
                                Camera.main.transform.position = KickStart.SpecialAttractPos;
                                int removed = 0;
                                foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(KickStart.SpecialAttractPos, 2500, new Bitfield<ObjectTypes>(new ObjectTypes[1] { ObjectTypes.Scenery })))
                                {
                                    if (vis.resdisp.IsNotNull() && vis.centrePosition.y < -25)
                                    {
                                        vis.resdisp.RemoveFromWorld(false, true, true, true);
                                        removed++;
                                    }
                                }
                                DebugTAC_AI.Log("TACtical_AI: removed " + removed);
                                for (int step = 0; numToSpawn > step; step++)
                                {
                                    Vector3 offset = Quaternion.Euler(0f, (float)step * rad, 0f) * Vector3.forward * 16;
                                    Vector3 posSea = KickStart.SpecialAttractPos + offset;

                                    Vector3 forward = (KickStart.SpecialAttractPos - posSea).normalized;
                                    Vector3 position = posSea;// - (forward * 10);
                                    position = AI.Movement.AIEPathing.ForceOffsetToSea(position);

                                    if (!RawTechLoader.SpawnAttractTech(position, forward, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Sea))
                                        RawTechLoader.SpawnAttractTech(position,  forward, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.Space);
                                }
                                //Debug.Log("TACtical_AI: cam is at " + Singleton.Manager<CameraManager>.inst.ca);
                                Singleton.Manager<CameraManager>.inst.ResetCamera(KickStart.SpecialAttractPos, Quaternion.LookRotation(Vector3.forward));
                                Singleton.cameraTrans.position = KickStart.SpecialAttractPos;
                                Singleton.cameraTrans.rotation = Quaternion.LookRotation(Vector3.forward);
                                rTime.SetValue(__instance, Time.time + __instance.resetTime);
                                spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                                return false;
                            }
                            else
                                RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Land);
                            break;

                        case AttractType.BaseSiege: // HQ Siege
                            RawTechLoader.SpawnAttractTech(spawn,  Vector3.forward, team1, BaseTerrain.Land, purpose: BasePurpose.Headquarters);
                            break;

                        case AttractType.BaseVBase: // BaseVBase - Broken ATM

                            RawTechLoader.SpawnAttractTech(spawn + (Vector3.forward * 50),-Vector3.forward, team1, BaseTerrain.Land, purpose: BasePurpose.TechProduction);
                            RawTechLoader.SpawnAttractTech(spawn + (Vector3.forward * 25), -Vector3.forward, team1, BaseTerrain.Land);
                            RawTechLoader.SpawnAttractTech(spawn - (Vector3.forward * 50),  Vector3.forward, team2, BaseTerrain.Land, purpose: BasePurpose.TechProduction);
                            RawTechLoader.SpawnAttractTech(spawn - (Vector3.forward * 25),  Vector3.forward, team2, BaseTerrain.Land);
                            rTime.SetValue(__instance, Time.time + __instance.resetTime);
                            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                            return false;

                        case AttractType.Misc: // pending
                            for (int step = 0; numToSpawn > step; step++)
                            {
                                Vector3 position = tanksToConsider[step] + (Vector3.up * 10);

                                if (RawTechLoader.SpawnAttractTech(position, (spawn - tanksToConsider[step]).normalized, AIGlobals.GetRandomEnemyBaseTeam(), BaseTerrain.AnyNonSea))
                                    DebugTAC_AI.Log("TACtical_AI: ThrowCoolAIInAttract(Misc) - error ~ could not find Tech");
                            }
                            RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Air);
                            rTime.SetValue(__instance, Time.time + __instance.resetTime);
                            spawnIndex = (spawnIndex + 1) % __instance.spawns.Length;
                            return false;

                        default: //AttractType.Invader: - Land battle invoker
                            RawTechLoader.SpawnAttractTech(spawn, Vector3.forward, team1, BaseTerrain.Land);
                            break;
                    }
                }
            }
            catch { }
            return true;
        }
        internal static void SetupTechsEnd(ModeAttract __instance)
        {
            try
            {
                if (KickStart.SpecialAttract)
                {
                    int TechCount = Singleton.Manager<ManTechs>.inst.CurrentTechs.Count();
                    List<Tank> tanksToConsider = Singleton.Manager<ManTechs>.inst.CurrentTechs.ToList();

                    AttractType randNum = KickStart.SpecialAttractNum;
                    if (randNum == AttractType.Harvester)
                    {   // Peaceful harvesting
                    }
                    else if (randNum == AttractType.Dogfight)
                    {   // Aircraft fight
                    }
                    else if (randNum == AttractType.SpaceBattle)
                    {   // Airship assault
                    }
                    else if (randNum == AttractType.NavalWarfare)
                    {   // Naval Brawl
                    }
                    else if (randNum == AttractType.BaseSiege)
                    {   // HQ Siege
                        foreach (Tank tech in Singleton.Manager<ManTechs>.inst.CurrentTechs)
                        {
                            tech.SetTeam(4114);
                        }
                        int teamBase = AIGlobals.GetRandomEnemyBaseTeam();
                        Singleton.Manager<ManTechs>.inst.CurrentTechs.First().SetTeam(teamBase);
                        Singleton.Manager<ManTechs>.inst.CurrentTechs.ElementAtOrDefault(1).SetTeam(teamBase);
                    }
                    else if (randNum == AttractType.Misc)
                    {   // pending
                    }
                    else // AttractType.Invader
                    {   // Land battle invoker
                    }

                    //Debug.Log("TACtical_AI: Post-Setup for attract type " + KickStart.SpecialAttractNum.ToString());
                }
            }
            catch { }
        }
    }
}
