using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using TAC_AI.AI;
using UnityEngine.Networking;

namespace TAC_AI
{
    internal static class NetworkHandler
    {
        static NetworkInstanceId Host;
        static bool HostExists = false;

        const TTMsgType AIADVTypeChange = (TTMsgType)4317;
        const TTMsgType AIRetreatRequest = (TTMsgType)4318;
        const TTMsgType AIRTSPosCommand = (TTMsgType)4319;
        const TTMsgType AIRTSPosControl = (TTMsgType)4320;
        const TTMsgType AIRTSAttack = (TTMsgType)4321;


        public class AITypeChangeMessage : MessageBase
        {
            public AITypeChangeMessage() { }
            public AITypeChangeMessage(uint netTechID, AIType AIType)
            {
                this.netTechID = netTechID;
                this.AIType = AIType;
            }
            public override void Deserialize(NetworkReader reader)
            {
                netTechID = reader.ReadUInt32();
                AIType = (AIType)reader.ReadInt32();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(netTechID);
                writer.Write((int)AIType);
            }

            public uint netTechID;
            public AIType AIType;
        }
        public class AIRetreatMessage : MessageBase
        {
            public AIRetreatMessage() { }
            public AIRetreatMessage(int team, bool retreat)
            {
                Team = team;
                Retreat = retreat;
            }
            public override void Deserialize(NetworkReader reader)
            {
                Team = reader.ReadInt32();
                Retreat = reader.ReadBoolean();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(Team);
                writer.Write(Retreat);
            }

            public int Team;
            public bool Retreat;
        }

        public class AIRTSCommandMessage : MessageBase
        {
            public AIRTSCommandMessage() { }
            public AIRTSCommandMessage(uint netTechID, Vector3 PosIn)
            {
                this.netTechID = netTechID;
                this.Position = PosIn;
            }
            public override void Deserialize(NetworkReader reader)
            {
                netTechID = reader.ReadUInt32();
                Position = reader.ReadVector3();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(netTechID);
                writer.Write(Position);
            }

            public uint netTechID;
            public Vector3 Position = Vector3.zero;
        }
        public class AIRTSControlMessage : MessageBase
        {
            public AIRTSControlMessage() { }
            public AIRTSControlMessage(uint netTechID, bool isRTS)
            {
                this.netTechID = netTechID;
                this.RTSControl = isRTS;
            }
            public override void Deserialize(NetworkReader reader)
            {
                netTechID = reader.ReadUInt32();
                RTSControl = reader.ReadBoolean();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(netTechID);
                writer.Write(RTSControl);
            }

            public uint netTechID;
            public bool RTSControl = false;
        }
        public class AIRTSAttackComm : MessageBase
        {
            public AIRTSAttackComm() { }
            public AIRTSAttackComm(uint netTechID, uint netTechIDTarget)
            {
                this.netTechID = netTechID;
                this.targetNetTechID = netTechIDTarget;
            }
            public override void Deserialize(NetworkReader reader)
            {
                netTechID = reader.ReadUInt32();
                targetNetTechID = reader.ReadUInt32();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(netTechID);
                writer.Write(targetNetTechID);
            }

            public uint netTechID;
            public uint targetNetTechID;
        }




        private static int localConnectionID { get { return ManNetwork.inst.Client.connection.connectionId; } }


        // AIRTSCommandMessage
        public static void TryBroadcastRTSCommand(uint netTechID, Vector3 Pos)
        {
            if (HostExists) try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRTSPosCommand, new AIRTSCommandMessage(netTechID, Pos), Host);
                    Debug.Log("Sent new TryBroadcastRTSCommand update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send TryBroadcastRTSCommand!"); }
        }
        public static void OnClientAcceptRTSCommand(NetworkMessage netMsg)
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

        // AIRTSControlMessage
        public static void TryBroadcastRTSControl(uint netTechID, bool isRTS)
        {
            if (HostExists) try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRTSPosControl, new AIRTSControlMessage(netTechID, isRTS), Host);
                    Debug.Log("Sent new TryBroadcastRTSControl update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send TryBroadcastRTSControl!"); }
        }
        public static void OnClientAcceptRTSControl(NetworkMessage netMsg)
        {
            var reader = new AIRTSControlMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.tech.GetComponent<AIECore.TankAIHelper>().isRTSControlled = reader.RTSControl;
                Debug.Log("TACtical_AI: Received new TryBroadcastRTSControl update, RTS control is " + reader.RTSControl);
            }
            catch
            {
                Debug.Log("TACtical_AI: OnClientAcceptRTSControl Receive failiure! Could not decode intake!?");
            }
        }

        // AIRTSAttackComm
        public static void TryBroadcastRTSAttack(uint netTechID, uint TargetNetTechID)
        {
            if (HostExists) try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRTSAttack, new AIRTSAttackComm(netTechID, TargetNetTechID), Host);
                    Debug.Log("Sent new TryBroadcastRTSAttack update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send TryBroadcastRTSAttack!"); }
        }
        public static void OnClientAcceptRTSAttack(NetworkMessage netMsg)
        {
            var reader = new AIRTSAttackComm();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                NetTech targeting = ManNetTechs.inst.FindTech(reader.targetNetTechID);
                find.tech.GetComponent<AIECore.TankAIHelper>().lastEnemy = targeting.tech.visible;
                Debug.Log("TACtical_AI: Received new TryBroadcastRTSAttack update, RTS target is " + targeting.tech.name);
            }
            catch
            {
                Debug.Log("TACtical_AI: OnClientAcceptRTSAttack Receive failiure! Could not decode intake!?");
            }
        }

        // AITypeChangeMessage
        public static void TryBroadcastNewAIState(uint netTechID, AIType AIType)
        {
            if (HostExists)
            {
                try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIADVTypeChange, new AITypeChangeMessage(netTechID, AIType), Host);
                    Debug.Log("Sent new AdvancedAI update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send new AdvancedAI update, shouldn't be too bad in the long run"); }
            }
        }
        public static void OnClientChangeNewAIState(NetworkMessage netMsg)
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
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRetreatRequest, new AIRetreatMessage(team, retreat), Host);
                    Debug.Log("Sent new AdvancedAI update to all");
                }
                catch { Debug.Log("TACtical_AI: Failed to send TryBroadcastNewRetreatState update, shouldn't be too bad in the long run"); }
        }
        public static void OnClientChangeNewRetreatState(NetworkMessage netMsg)
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
