using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using TAC_AI.AI;

namespace TAC_AI
{
    internal static class NetworkHandler
    {
        static UnityEngine.Networking.NetworkInstanceId Host;
        static bool HostExists = false;

        const TTMsgType AIADVTypeChange = (TTMsgType)4317;


        public class AITypeChangeMessage : UnityEngine.Networking.MessageBase
        {
            public AITypeChangeMessage() { }
            public AITypeChangeMessage(uint netTechID, AIType AIType)
            {
                this.netTechID = netTechID;
                this.AIType = AIType;
            }
            public override void Deserialize(UnityEngine.Networking.NetworkReader reader)
            {
                Vector2 output = reader.ReadVector2();
                netTechID = (uint)output.x;
                AIType = (AIType)(int)output.y;
            }

            public override void Serialize(UnityEngine.Networking.NetworkWriter writer)
            {
                writer.Write(new Vector2(netTechID, (int)AIType));
            }

            public uint netTechID;
            public AIType AIType;
        }

        public static void TryBroadcastNewAIState(uint netTechID, AIType AIType)
        {
            if (HostExists) try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllClients(AIADVTypeChange, new AITypeChangeMessage(netTechID, AIType), Host);
                    Debug.Log("Sent new AdvancedAI update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send new AdvancedAI update, shouldn't be too bad in the long run"); }
        }
        public static void OnClientChangeNewAIState(UnityEngine.Networking.NetworkMessage netMsg)
        {
            var reader = new AITypeChangeMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.tech.GetComponent<AIECore.TankAIHelper>().TrySetAITypeRemote(netMsg.GetSender(), reader.AIType);
                Debug.Log("TACtical_AI: Received new AdvancedAI update, changing to " + find.tech.GetComponent<AIECore.TankAIHelper>().DediAI.ToString());
            }
            catch
            {
                Debug.Log("TACtical_AI: Receive failiure! Could not decode intake!?");
            }
        }

        public static class Patches
        {
            [HarmonyPatch(typeof(NetPlayer), "OnStartClient")]
            static class OnStartClient
            {
                static void Postfix(NetPlayer __instance)
                {
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIADVTypeChange, new ManNetwork.MessageHandler(OnClientChangeNewAIState));
                    Debug.Log("Subscribed " + __instance.netId.ToString() + " to AdvancedAI updates from host. Sending current techs");
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartServer")]
            static class OnStartServer
            {
                static void Postfix(NetPlayer __instance)
                {
                    if (!HostExists)
                    {
                        Debug.Log("Host started, hooked AdvancedAI update broadcasting to " + __instance.netId.ToString());
                        Host = __instance.netId;
                        HostExists = true;
                    }
                }
            }
        }
    }
}
