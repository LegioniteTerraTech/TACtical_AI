﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
//using Harmony;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using SafeSaves;
using UnityEditor;

namespace TAC_AI
{
    class PatchBatch
    {
    }

#if STEAM
    public class KickStartTAC_AI : ModBase
    {
        
        internal static KickStartTAC_AI oInst;

        bool isInit = false;
        public override bool HasEarlyInit()
        {
            return true;
        }

        // IDK what I should init here...
        public override void EarlyInit()
        {
            if (oInst == null)
            {
                oInst = this;
                try
                {
                    KickStart.PatchMod();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("TACtical_AI: Error on patch");
                    DebugTAC_AI.Log(e);
                }
            }
        }
        public IEnumerable<float> InitIterator()
        {
            return (IEnumerable<float>)KickStart.MainOfficialInitIterate();
        }
        public override void Init()
        {
            // We do this check because this mod takes FOREVER to build, so we don't heed every reset
            //   request - the mod is already built to handle that because of Unofficial.
            KickStart.ShouldBeActive = true;
            if (!isInit)
            {
                if (oInst == null)
                    oInst = this;
                KickStart.MainOfficialInit();
                isInit = true;
            }
            else
            {
                KickStart.VALIDATE_MODS();
                SpecialAISpawner.DetermineActiveOnModeType();
                AIECore.TankAIManager.inst.CorrectBlocksList();
            }
        }
        public override void DeInit()
        {
            KickStart.ShouldBeActive = false;
            if (isInit)
            {
                KickStart.DeInitCheck();
                isInit = false;
            }
        }

        public override void Update()
        {
        }
    }
#endif

    internal enum AttractType
    {
        Harvester,
        Invader,
        SpaceInvader,
        Dogfight,
        SpaceBattle,
        NavalWarfare,
        BaseSiege,
        BaseVBase,
        Misc,
    }

    internal static class Patches
    {
#if DEBUG
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("IsEnemy", typeof(int), typeof(int))]
        internal static class TankTeamPatch
        {

            // Leg testing
            private static bool Prefix(ref bool __result, ref int teamID1, ref int teamID2)
            {
                bool team1Player = ManSpawn.IsPlayerTeam(teamID1);
                bool team2Player = ManSpawn.IsPlayerTeam(teamID2);
                if ((team1Player && AIGlobals.IsFriendlyBaseTeam(teamID2)) || (team2Player && AIGlobals.IsFriendlyBaseTeam(teamID1)))
                {
                    __result = false;
                    return false;
                }
                if (DebugRawTechSpawner.DevCheatNoAttackPlayer && (team1Player || team2Player) && !ManNetwork.IsNetworked)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("IsFriendly", typeof(int), typeof(int))]
        internal static class TankTeamPatch2
        {

            private static bool Prefix(ref bool __result, ref int teamID1, ref int teamID2)
            {
                if (DebugRawTechSpawner.DevCheatPlayerEnemyBaseTeam && (ManSpawn.IsPlayerTeam(teamID1) || ManSpawn.IsPlayerTeam(teamID2)) && !ManNetwork.IsNetworked)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
#else
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("IsEnemy", typeof(int), typeof(int))]//
        internal static class TankTeamPatch
        {
            private static bool Prefix(ref bool __result, ref int teamID1, ref int teamID2)
            {
                if ((ManSpawn.IsPlayerTeam(teamID1) && AIGlobals.IsFriendlyBaseTeam(teamID2)) || (ManSpawn.IsPlayerTeam(teamID2) && AIGlobals.IsFriendlyBaseTeam(teamID1)))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
#endif

        /*
        [HarmonyPatch(typeof(LocatorPanel))]
        [HarmonyPatch("Format")]//On icon update
        private static class SendUpdateAIDisp2
        {
            static FieldInfo icon = typeof(LocatorPanel).GetField("m_FactionIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            private static bool Prefix(LocatorPanel __instance, ref Sprite iconSprite)
            {
                if (KickStart.EnableBetterAI)
                {
                    try
                    {

                        //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Triggered!");
                        if (RawTechExporter.lastTech.IsNotNull())
                        {
                            if (RawTechExporter.lastTech.AIState == 1)
                            {
                                if (RawTechExporter.aiIcons.TryGetValue(RawTechExporter.lastTech.DediAI, out Sprite sprite))
                                {
                                    //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Swapping sprite!");
                                    iconSprite = sprite;
                                    return false;
                                }
                            }
                        }
                        
                        //Image cache = (Image)icon.GetValue(__instance);
                        //cache.sprite = Singleton.Manager<ManUI>.inst.GetAICategoryIcon(AICategories.AIHostile);
                        //icon.SetValue(__instance, cache);

                        //DebugTAC_AI.Log("TACtical_AI: SendUpdateAIDisp2 - Caught Update!");
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: SendUpdateAIDisp - failiure on send!");
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ManUI))]
        [HarmonyPatch("GetAICategoryIcon")]//On icon update
        private static class UpdateAIDisplay
        {
            static bool fired = false;
            private static bool Prefix(ManUI __instance, ref Sprite __result)
            {
                //DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - Trigger");
                if (KickStart.EnableBetterAI)
                {
                    try
                    {
                        if (!fired)
                        {
                            DebugTAC_AI.Log("TACtical_AI: UpdateAIDisplay - snapping sprite!"); 
                            Sprite image = __instance.m_SpriteFetcher.GetAICategoryIcon(AICategories.AIHostile);
                            RenderTexture grabTex = RenderTexture.GetTemporary(
                                image.texture.width,
                                image.texture.height,
                                0,
                                RenderTextureFormat.Default,
                                RenderTextureReadWrite.Linear
                                );
                            Graphics.Blit(image.texture, grabTex);
                            RenderTexture grabTex2 = RenderTexture.active;
                            RenderTexture.active = grabTex;

                            Texture2D generated = new Texture2D((int)image.rect.width, (int)image.rect.height);
                            generated.ReadPixels(new Rect(0, 0, (int)grabTex.width, (int)grabTex.height), 0, 0);

                            DebugTAC_AI.Log("TACtical_AI: UpdateModeDisplay - deployed!");
                            FileUtils.SaveTexture(generated, RawTechExporter.BaseDirectory + up + "AI2.png");
                            fired = true;
                        }
                        //image.sprite = spride override
                    }
                    catch
                    {
                        DebugTAC_AI.Log("TACtical_AI: UpdateModeDisplay - failiure on update!");
                    }
                }
                return true;
            }
        }*/


#if !STEAM
        [HarmonyPatch(typeof(ManGameMode))]
        [HarmonyPatch("Awake")]//On Game start
        private static class StartupSpecialAISpawner
        {
            private static void Postfix()
            {
                // Setup aircraft if Population Injector is N/A
                if (!KickStart.isPopInjectorPresent)
                {
                    SpecialAISpawner.Initiate();
                    ManEnemyWorld.LateInitiate();
                }
            }
        }
#endif
    }
}
