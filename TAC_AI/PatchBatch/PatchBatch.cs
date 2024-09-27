using System;
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
        
        internal static TerraTechETCUtil.ModDataHandle oInst;

        bool isInit = false;
        public override bool HasEarlyInit()
        {
            return true;
        }

        // IDK what I should init here...
        public override void EarlyInit()
        {
            if (oInst == default)
            {
                oInst = new TerraTechETCUtil.ModDataHandle(KickStart.ModID);
                /*
                try
                {
                    KickStart.PatchMod();
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Error on patch");
                    DebugTAC_AI.Log(e);
                }
                */
            }
        }
        public IEnumerable<float> InitIterator()
        {
            return KickStart.MainOfficialInitIterate();
        }
        public override void Init()
        {
            // We do this check because this mod takes FOREVER to build, so we don't heed every reset
            //   request - the mod is already built to handle that because of Unofficial.
            KickStart.ShouldBeActive = true;
            if (!isInit)
            {
                if (oInst == default)
                    oInst = new TerraTechETCUtil.ModDataHandle(KickStart.ModID);
                try
                {
                    TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit(KickStart.ModID, 
                        KickStart.MainOfficialInit, KickStart.DeInitALL);
                }
                catch { }
                isInit = true;
            }
            else
            {
                KickStart.VALIDATE_MODS();
                SpecialAISpawner.DetermineActiveOnModeType();
                TankAIManager.inst.CorrectBlocksList();
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
            if (ManUI.inst.HasInitialised)
                DebugTAC_AI.DoShowWarnings();
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
        [HarmonyPatch(typeof(ManMusic))]
        [HarmonyPatch("SetDanger", new Type[1] { typeof(ManMusic.DangerContext.Circumstance) })]
        private class AdvancedMenuMusiks
        {
            private static bool Prefix(ManMusic __instance, ManMusic.DangerContext.Circumstance circumstance)
            {
                if (circumstance == ManMusic.DangerContext.Circumstance.Generic)
                {
                    switch (KickStart.factionAttractOST)
                    {
                        case FactionSubTypes.GSO:
                        case FactionSubTypes.GC:
                        case FactionSubTypes.EXP:
                        case FactionSubTypes.VEN:
                        case FactionSubTypes.HE:
                        case FactionSubTypes.BF:
                            __instance.SetDanger(ManMusic.DangerContext.Circumstance.SetPiece, KickStart.factionAttractOST);
                            return false;
                        case FactionSubTypes.NULL:
                        case FactionSubTypes.SPE:
                        default:
                            return true;
                    }
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(ManMusic))]
        [HarmonyPatch("PlayMusicEvent")]
        private class AdvancedMenuMusiks2
        {
            private static void Prefix(ManMusic __instance, ref ManMusic.MusicTypes musicType)
            {
                if (musicType == ManMusic.MusicTypes.Attract)
                {
                    if (KickStart.factionAttractOST == FactionSubTypes.NULL)
                    {
                        KickStart.factionAttractOST = (FactionSubTypes)UnityEngine.Random.Range(1, Enum.GetValues(typeof(FactionSubTypes)).Length);
                        DebugTAC_AI.Log("Attract OST set to " + KickStart.factionAttractOST);
                    }
                    switch (KickStart.factionAttractOST)
                    {
                        case FactionSubTypes.GSO:
                        case FactionSubTypes.GC:
                        case FactionSubTypes.EXP:
                        case FactionSubTypes.VEN:
                        case FactionSubTypes.HE:
                        case FactionSubTypes.BF:
                            musicType = ManMusic.MusicTypes.Main;
                            __instance.EnableSequencing = true;
                            var prof = ManProfile.inst.GetCurrentUser();
                            if (prof != null)
                            {
                                __instance.SetMusicMixerVolume(prof.m_SoundSettings.m_MusicVolume * 0.75f);
                            }
                            break;
                        case FactionSubTypes.NULL:
                        case FactionSubTypes.SPE:
                        default:
                            break;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ModuleItemHolder.Stack))]
        [HarmonyPatch("Take", new Type[] { typeof(Visible), typeof(bool), typeof(int), typeof(bool) })]
        private class TakeDetect
        {
            private static void Prefix(ModuleItemHolder __instance, ref Visible item)
            {
                if (__instance.block?.tank && 
                    __instance.block.tank.Team == ManSpawn.NeutralTeam)
                {
                    if (item.holderStack?.myHolder?.block?.tank != null)
                    {
                        int prevHolderTeam = item.holderStack.myHolder.block.tank.Team;
                        if (prevHolderTeam == ManSpawn.NeutralTeam)
                        {
                            // Do nothing, it is still valid while held!
                        }
                        else if (ManBaseTeams.IsBaseTeamDynamic(prevHolderTeam))
                        {   // Affiliate this resource with the correct team
                            if (!ManBaseTeams.inst.TradingSellOffers.ContainsKey(item.ID))
                            {
                                ManBaseTeams.inst.TradingSellOffers.Add(item.ID, prevHolderTeam);
                                item.RecycledEvent.Subscribe(ManBaseTeams.PickupRecycled);
                            }
                        }
                        else if (ManBaseTeams.inst.TradingSellOffers.ContainsKey(item.ID))
                        {   // it was moved elsewhere, AKA dropped from trading station by some ungodly method
                            ManBaseTeams.inst.TradingSellOffers.Remove(item.ID);
                            item.RecycledEvent.Unsubscribe(ManBaseTeams.PickupRecycled);
                        }
                    }
                }
            }
        }
#if DEBUG
        // DEBUGGGGGGGGGGGGGG
        /*
        [HarmonyPatch(typeof(Debug))]
        [HarmonyPatch("DrawLine", typeof(Vector3), typeof(Vector3), typeof(Color))]
        internal static class OverrrideDRAW
        {
            private static bool Prefix(ref Vector3 start, ref Vector3 end, ref Color color)
            {
                DebugExtUtilities.DrawDirIndicator(start, end, color);
                return false;
            }
        }
        [HarmonyPatch(typeof(Gizmos))]
        [HarmonyPatch("DrawSphere")]
        internal static class OverrrideDRAW2
        {
            private static bool Prefix(ref Vector3 center, ref float radius)
            {
                DebugExtUtilities.DrawDirIndicatorSphere(center, radius, Color.blue);
                return false;
            }
        }
        [HarmonyPatch(typeof(Gizmos))]
        [HarmonyPatch("DrawCube")]
        internal static class OverrrideDRAW3
        {
            private static bool Prefix(ref Vector3 center, ref Vector3 size)
            {
                DebugExtUtilities.DrawDirIndicatorRecPriz(center, size, Color.magenta);
                return false;
            }
        }
        [HarmonyPatch(typeof(Gizmos))]
        [HarmonyPatch("DrawLine")]
        internal static class OverrrideDRAW4
        {
            private static bool Prefix(ref Vector3 from, ref Vector3 to)
            {
                DebugExtUtilities.DrawDirIndicator(from, to, Color.grey);
                return false;
            }
        }*/
#endif
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("IsEnemy", typeof(int), typeof(int))]//
        internal static class TankTeamPatch
        {
            private static bool Prefix(ref bool __result, ref int teamID1, ref int teamID2)
            {
                if (ManBaseTeams.IsUnattackable(teamID1, teamID2))
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
                if (ManBaseTeams.IsTeammate(teamID1, teamID2))
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
        /*
        [HarmonyPatch(typeof(TankControl))]
        [HarmonyPatch("ActiveScheme", methodType: MethodType.Getter)]
        internal static class LetAIUseProperSteering
        {
            private static bool Prefix(TankControl __instance, ref ControlScheme __result)
            {
                if (__instance.Tech.GetHelperInsured().AIDriving)
                {
                    __result = null;
                    return false;
                }
                return true;
            }
        }*/

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

                        //DebugTAC_AI.Log(KickStart.ModID + ": UpdateAIDisplay - Triggered!");
                        if (RawTechExporter.lastTech.IsNotNull())
                        {
                            if (RawTechExporter.lastTech.AIState == 1)
                            {
                                if (RawTechExporter.aiIcons.TryGetValue(RawTechExporter.lastTech.DediAI, out Sprite sprite))
                                {
                                    //DebugTAC_AI.Log(KickStart.ModID + ": UpdateAIDisplay - Swapping sprite!");
                                    iconSprite = sprite;
                                    return false;
                                }
                            }
                        }
                        
                        //Image cache = (Image)icon.GetValue(__instance);
                        //cache.sprite = Singleton.Manager<ManUI>.inst.GetAICategoryIcon(AICategories.AIHostile);
                        //icon.SetValue(__instance, cache);

                        //DebugTAC_AI.Log(KickStart.ModID + ": SendUpdateAIDisp2 - Caught Update!");
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": SendUpdateAIDisp - failiure on send!");
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
                //DebugTAC_AI.Log(KickStart.ModID + ": UpdateAIDisplay - Trigger");
                if (KickStart.EnableBetterAI)
                {
                    try
                    {
                        if (!fired)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": UpdateAIDisplay - snapping sprite!"); 
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

                            DebugTAC_AI.Log(KickStart.ModID + ": UpdateModeDisplay - deployed!");
                            FileUtils.SaveTexture(generated, RawTechExporter.BaseDirectory + up + "AI2.png");
                            fired = true;
                        }
                        //image.sprite = spride override
                    }
                    catch
                    {
                        DebugTAC_AI.Log(KickStart.ModID + ": UpdateModeDisplay - failiure on update!");
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
