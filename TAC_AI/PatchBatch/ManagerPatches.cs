using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Reflection;
using TerraTechETCUtil;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.Templates;
using TAC_AI.World;
using HarmonyLib;

namespace TAC_AI
{
    internal class ManagerPatches
    {
        internal static class ManSpawnPatches
        {
            internal static Type target = typeof(ManSpawn);
            //DelayedLoadRequest
            private static void OnDLCLoadComplete_Postfix(ManSpawn __instance)
            {
                ManPlayerRTS.DelayedInitiate();
            }
        }

        internal static class ManLooseBlocksPatches
        {
            internal static Type target = typeof(ManLooseBlocks);

            //AITechLivesMatter
            private static bool OnServerAttachBlockRequest_Prefix(ManLooseBlocks __instance, ref NetworkMessage netMsg)
            {
                if (AIERepair.NonPlayerAttachAllow)
                {
                    BlockAttachedMessage BAM = netMsg.ReadMessage<BlockAttachedMessage>();
                    NetTech NetT = NetworkServer.FindLocalObject(BAM.m_TechNetId).GetComponent<NetTech>();
                    TankBlock canidate = ManLooseBlocks.inst.FindTankBlock(BAM.m_BlockPoolID);
                    bool attached;
                    if (NetT == null)
                    {
                        DebugTAC_AI.Log("TACtical_AI: BlockAttachNetworkOverrideServer - NetTech is NULL!");
                    }
                    else if (canidate == null)
                    {
                        DebugTAC_AI.Log("TACtical_AI: BlockAttachNetworkOverrideServer - BLOCK IS NULL!");
                    }
                    else
                    {
                        Tank tank = NetT.tech;
                        NetBlock netBlock = canidate.netBlock;
                        if (netBlock.IsNull())
                        {
                            DebugTAC_AI.Log("TACtical_AI: BlockAttachNetworkOverrideServer - NetBlock could not be found on AI block attach attempt!");
                        }
                        else
                        {
                            OrthoRotation OR = new OrthoRotation((OrthoRotation.r)BAM.m_BlockOrthoRotation);
                            attached = tank.blockman.AddBlockToTech(canidate, BAM.m_BlockPosition, OR);
                            if (attached)
                            {
                                Singleton.Manager<ManNetwork>.inst.ServerNetBlockAttachedToTech.Send(tank, netBlock, canidate);
                                tank.GetHelperInsured().dirty = true;

                                Singleton.Manager<ManNetwork>.inst.SendToAllExceptHost(TTMsgType.BlockAttach, BAM);
                                if (netBlock.block != null)
                                {
                                    netBlock.Disconnect();
                                }
                                if (Singleton.Manager<ManNetwork>.inst.IsServer)
                                {
                                    netBlock.RemoveClientAuthority();
                                    NetworkServer.UnSpawn(netBlock.gameObject);
                                }
                                netBlock.transform.Recycle(worldPosStays: false);
                            }
                        }
                    }
                    return false;
                }
                return true;
            }
        }

        internal static class ManTechsPatches
        {
            internal static Type target = typeof(ManTechs);

            //PatchTankToHelpAI
            private static void RegisterTank_Postfix(ManTechs __instance, ref Tank t)
            {
                //DebugTAC_AI.Log("TACtical_AI: Patched Tank OnPool(TankAIHelper & TimeTank)");
                t.GetHelperInsured();
            }
        }

        internal static class ManNetworkPatches
        {
            internal static Type target = typeof(ManNetwork);
            // Multi-Player
            //WarnJoiningPlayersOfScaryAI
            private static void AddPlayer_Postfix(ManNetwork __instance)
            {
                // Setup aircraft if Population Injector is N/A
                try
                {
                    if (ManNetwork.IsHost && KickStart.EnableBetterAI)
                        AIECore.TankAIManager.inst.Invoke("WarnPlayers", 16);
                }
                catch { }
            }
        }
    }
}
