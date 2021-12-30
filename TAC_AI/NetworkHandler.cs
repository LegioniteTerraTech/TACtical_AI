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
        const TTMsgType AIRetreatRequest = (TTMsgType)4318;
        const TTMsgType AIRTSPosCommand = (TTMsgType)4319;


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
        public class AIRetreatMessage : UnityEngine.Networking.MessageBase
        {
            public AIRetreatMessage() { }
            public AIRetreatMessage(int team, bool retreat)
            {
                Team = team;
                Retreat = retreat;
            }
            public override void Deserialize(UnityEngine.Networking.NetworkReader reader)
            {
                Vector2 vec = reader.ReadVector2();
                Team = (int)vec.x;
                Retreat = vec.y > 0;
            }

            public override void Serialize(UnityEngine.Networking.NetworkWriter writer)
            {
                writer.Write(new Vector2(Team, Retreat ? 2 : 0));
            }

            public int Team;
            public bool Retreat;
        }

        public class AIRTSCommandMessage : UnityEngine.Networking.MessageBase
        {
            public AIRTSCommandMessage() { }
            public AIRTSCommandMessage(uint netTechID, Vector3 PosIn)
            {
                this.netTechID = netTechID;
                this.Position = PosIn;
            }
            public override void Deserialize(UnityEngine.Networking.NetworkReader reader)
            {
                Vector4 output = reader.ReadVector4();
                Position.x = output.x;
                Position.y = output.y;
                Position.z = output.z;
                netTechID = (uint)output.w;
            }

            public override void Serialize(UnityEngine.Networking.NetworkWriter writer)
            {
                writer.Write(new Vector4(Position.x, Position.y, Position.z, netTechID));
            }

            public uint netTechID;
            public Vector3 Position = Vector3.zero;
        }

        // AIRTSCommandMessage

        public static void TryBroadcastRTSCommand(uint netTechID, Vector3 Pos)
        {
            if (HostExists) try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllClients(AIRTSPosCommand, new AIRTSCommandMessage(netTechID, Pos), Host);
                    Debug.Log("Sent new TryBroadcastRTSCommand update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send TryBroadcastRTSCommand!"); }
        }
        public static void OnClientAcceptRTSCommand(UnityEngine.Networking.NetworkMessage netMsg)
        {
            var reader = new AIRTSCommandMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.tech.GetComponent<AIECore.TankAIHelper>().DirectRTSDest(reader.Position);
                Debug.Log("TACtical_AI: Received new TryBroadcastRTSCommand update, ordering to " + reader.Position);
            }
            catch
            {
                Debug.Log("TACtical_AI: OnClientAcceptRTSCommand Receive failiure! Could not decode intake!?");
            }
        }

        // AITypeChangeMessage
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

        // AIRetreatMessage
        public static void TryBroadcastNewRetreatState(int team, bool retreat)
        {
            if (HostExists) try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllClients(AIRetreatRequest, new AIRetreatMessage(team, retreat), Host);
                    Debug.Log("Sent new AdvancedAI update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send TryBroadcastNewRetreatState update, shouldn't be too bad in the long run"); }
        }
        public static void OnClientChangeNewRetreatState(UnityEngine.Networking.NetworkMessage netMsg)
        {
            var reader = new AIRetreatMessage();
            netMsg.ReadMessage(reader);
            try
            {
                AIECore.TeamRetreat(reader.Team, reader.Retreat);
                //Debug.Log("TACtical_AI: Received new AdvancedAI update, changing retreat states");
            }
            catch
            {
                Debug.Log("TACtical_AI: Retreat receive failiure! Could not decode intake!?");
            }
        }

        public static class Patches
        {
            [HarmonyPatch(typeof(NetPlayer), "OnStartClient")]
            static class OnStartClient
            {
                static void Postfix(NetPlayer __instance)
                {
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIRTSPosCommand, new ManNetwork.MessageHandler(OnClientAcceptRTSCommand));
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIRetreatRequest, new ManNetwork.MessageHandler(OnClientChangeNewRetreatState));
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
