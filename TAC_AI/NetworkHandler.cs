using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using TAC_AI.World;
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
        const TTMsgType AIEnemyType = (TTMsgType)4322;
        const TTMsgType AIEnemySiege = (TTMsgType)4323;


        public class AITypeChangeMessage : MessageBase
        {
            public AITypeChangeMessage() { }
            public AITypeChangeMessage(uint netTechID, AIType AIType, AIDriverType AIDriving)
            {
                this.netTechID = netTechID;
                this.AIType = AIType;
                this.AIDriving = AIDriving;
            }
            public override void Deserialize(NetworkReader reader)
            {
                netTechID = reader.ReadUInt32();
                AIType = (AIType)reader.ReadInt32();
                AIDriving = (AIDriverType)reader.ReadInt32();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(netTechID);
                writer.Write((int)AIType);
                writer.Write((int)AIDriving);
            }

            public uint netTechID;
            public AIType AIType;
            public AIDriverType AIDriving;
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

        public class AIEnemySet : MessageBase
        {
            public AIEnemySet() { }
            public AIEnemySet(uint netTechID, EnemySmarts EnemyType)
            {
                this.netTechID = netTechID;
                this.enemyType = (int)EnemyType;
            }

            public uint netTechID;
            public int enemyType;
        }

        public class AIEnemyStagedSiege : MessageBase
        {
            public AIEnemyStagedSiege() { }
            public AIEnemyStagedSiege(int team, long totalHP, bool start)
            {
                Team = team;
                MaxHP = totalHP;
                Starting = start;
            }

            public int Team;
            public long MaxHP;
            public bool Starting;
        }



        private static int localConnectionID { get { return ManNetwork.inst.Client.connection.connectionId; } }


        // AIRTSCommandMessage
        public static void TryBroadcastRTSCommand(uint netTechID, Vector3 Pos)
        {
            if (HostExists) try
                {
                    DebugTAC_AI.LogNet("Sent new TryBroadcastRTSCommand update to all");
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRTSPosCommand, new AIRTSCommandMessage(netTechID, Pos), Host);
                }
                catch { DebugTAC_AI.LogNet("TACtical_AI: Failed to send TryBroadcastRTSCommand!"); }
        }
        public static void OnClientAcceptRTSCommand(NetworkMessage netMsg)
        {
            var reader = new AIRTSCommandMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.tech.GetHelperInsured().DirectRTSDest(reader.Position);
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnClientAcceptRTSCommand update, ordering tech " + find.name + " to " + reader.Position);
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnClientAcceptRTSCommand  - Receive failiure! \n Our techs are now desynched!");
            }
        }
        public static void OnServerAcceptRTSCommand(NetworkMessage netMsg)
        {
            var reader = new AIRTSCommandMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.tech.GetHelperInsured().DirectRTSDest(reader.Position);
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnServerAcceptRTSCommand update, ordering tech " + find.name + " to " + reader.Position);
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnServerAcceptRTSCommand  - Receive failiure! \n Our techs are now desynched!");
            }
        }

        // AIRTSControlMessage
        public static void TryBroadcastRTSControl(uint netTechID, bool isRTS)
        {
            if (HostExists) try
                {
                    DebugTAC_AI.LogNet("Sent new TryBroadcastRTSControl update to all");
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRTSPosControl, new AIRTSControlMessage(netTechID, isRTS), Host);
                }
                catch { DebugTAC_AI.LogNet("TACtical_AI: Failed to send TryBroadcastRTSControl!"); }
        }
        public static void OnClientAcceptRTSControl(NetworkMessage netMsg)
        {
            var reader = new AIRTSControlMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.tech.GetHelperInsured().isRTSControlled = reader.RTSControl;
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnClientAcceptRTSControl update,  Tech " + find.name + "'s RTS control is " + reader.RTSControl);
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnClientAcceptRTSControl  - Receive failiure! \n Our techs are now desynched!");
            }
        }
        public static void OnServerAcceptRTSControl(NetworkMessage netMsg)
        {
            var reader = new AIRTSControlMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.tech.GetHelperInsured().isRTSControlled = reader.RTSControl;
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnServerAcceptRTSControl update, Tech " + find.name + "'s RTS control is " + reader.RTSControl);
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnServerAcceptRTSControl  - Receive failiure! \n Our techs are now desynched!");

            }
        }

        // AIRTSAttackComm
        public static void TryBroadcastRTSAttack(uint netTechID, uint TargetNetTechID)
        {
            if (HostExists) try
                {
                    DebugTAC_AI.LogNet("Sent new TryBroadcastRTSAttack update to all");
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRTSAttack, new AIRTSAttackComm(netTechID, TargetNetTechID), Host);
                }
                catch { DebugTAC_AI.LogNet("TACtical_AI: Failed to send TryBroadcastRTSAttack!"); }
        }
        public static void OnClientAcceptRTSAttack(NetworkMessage netMsg)
        {
            var reader = new AIRTSAttackComm();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                NetTech targeting = ManNetTechs.inst.FindTech(reader.targetNetTechID);
                var helper = find.tech.GetHelperInsured();
                helper.lastEnemy = targeting.tech.visible;
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnClientAcceptRTSAttack update,  tech " + find.name + "'s RTS target is " + targeting.tech.name);
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnClientAcceptRTSAttack  - Receive failiure! \n Our techs are now desynched!");
            }
        }
        public static void OnServerAcceptRTSAttack(NetworkMessage netMsg)
        {
            var reader = new AIRTSAttackComm();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                NetTech targeting = ManNetTechs.inst.FindTech(reader.targetNetTechID);
                var helper = find.tech.GetHelperInsured();
                helper.lastEnemy = targeting.tech.visible;
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnServerAcceptRTSAttack update,  tech " + find.name + "'s RTS target is " + targeting.tech.name);
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnServerAcceptRTSAttack  - Receive failiure! \n Our techs are now desynched!");
            }
        }

        // AITypeChangeMessage
        public static void TryBroadcastNewAIState(uint netTechID, AIType AIType, AIDriverType AIDriver)
        {
            if (HostExists)
            {
                try
                {
                    DebugTAC_AI.LogNet("Sent new AdvancedAI update to all");
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIADVTypeChange, new AITypeChangeMessage(netTechID, AIType, AIDriver), Host);
                }
                catch { DebugTAC_AI.LogNet("TACtical_AI: Failed to send new AdvancedAI update, shouldn't be too bad in the long run"); }
            }
        }
        public static void OnClientSetNewAIState(NetworkMessage netMsg)
        {
            var reader = new AITypeChangeMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                var helper = find.tech.GetHelperInsured();
                helper.TrySetAITypeRemote(netMsg.GetSender(), reader.AIType, reader.AIDriving);
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnClientSetNewAIState update, tech " + find.name + " changing to " + helper.DediAI.ToString()
                    + " | Driver: " + helper.DriverType.ToString());
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnClientSetNewAIState - Receive failiure! \n Our techs are now desynched!");
            }
        }
        public static void OnServerSetNewAIState(NetworkMessage netMsg)
        {
            var reader = new AITypeChangeMessage();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                var helper = find.tech.GetHelperInsured();
                helper.TrySetAITypeRemote(netMsg.GetSender(), reader.AIType, reader.AIDriving);
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnServerSetNewAIState update, tech " + find.name + " changing to " + helper.DediAI.ToString() 
                    + " | Driver: " + helper.DriverType.ToString());
            }
            catch
            {
                DebugTAC_AI.Assert(true, "TACtical_AI: OnServerSetNewAIState - Receive failiure! \n Our techs are now desynched!");
            }
        }

        // AIRetreatMessage
        /// <summary>
        /// sent from both clients and server
        /// </summary>
        /// <param name="team"></param>
        /// <param name="retreat"></param>
        public static void TryBroadcastNewRetreatState(int team, bool retreat)
        {
            if (HostExists) try
                {
                    DebugTAC_AI.LogNet("Sent new TryBroadcastNewRetreatState update to all");
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(localConnectionID, AIRetreatRequest, new AIRetreatMessage(team, retreat), Host);
                }
                catch { DebugTAC_AI.LogNet("TACtical_AI: Failed to send TryBroadcastNewRetreatState update, shouldn't be too bad in the long run"); }
        }
        public static void OnClientSetRetreatState(NetworkMessage netMsg)
        {
            var reader = new AIRetreatMessage();
            netMsg.ReadMessage(reader);
            try
            {
                AIECore.TeamRetreat(reader.Team, reader.Retreat);
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnClientSetRetreatState update, changing retreat states of (" + reader.Team + ") to retreat " + reader.Retreat);
            }
            catch
            {
                DebugTAC_AI.LogNet("TACtical_AI: OnClientSetRetreatState - receive failiure! Could not decode intake!?");
            }
        }
        public static void OnServerSetRetreatState(NetworkMessage netMsg)
        {
            var reader = new AIRetreatMessage();
            netMsg.ReadMessage(reader);
            try
            {
                AIECore.TeamRetreat(reader.Team, reader.Retreat);
                DebugTAC_AI.LogNet("TACtical_AI: Received new OnServerSetRetreatState update, changing retreat states of (" + reader.Team +") to retreat " + reader.Retreat);
            }
            catch
            {
                DebugTAC_AI.LogNet("TACtical_AI: OnServerSetRetreatState - receive failiure! Could not decode intake!?");
            }
        }

        // AIEnemyState
        /// <summary>
        /// SERVER SENT
        /// </summary>
        /// <param name="netTechID"></param>
        /// <param name="smartz"></param>
        public static void TryBroadcastNewEnemyState(uint netTechID, EnemySmarts smartz)
        {
            if (HostExists && ManNetwork.IsHost) try
                {
                    DebugTAC_AI.LogNet("Sent new TryBroadcastNewEnemyState update to all");
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptHost(AIRetreatRequest, new AIEnemySet(netTechID, smartz));
                }
                catch { DebugTAC_AI.LogNet("TACtical_AI: Failed to send TryBroadcastNewEnemyState update, shouldn't be too bad in the long run"); }
        }
        public static void OnClientEnemyAISetup(NetworkMessage netMsg)
        {
            var reader = new AIEnemySet();
            netMsg.ReadMessage(reader);
            try
            {
                NetTech find = ManNetTechs.inst.FindTech(reader.netTechID);
                find.GetComponent<EnemyMind>().CommanderSmarts = (EnemySmarts)reader.enemyType;
                DebugTAC_AI.LogNet("TACtical_AI: OnClientEnemyAISetup - Enemy AI's (" + find.name + ") smarts is " + (EnemySmarts)reader.enemyType);
            }
            catch
            {
                DebugTAC_AI.LogNet("TACtical_AI: OnClientEnemyAISetup - receive failiure! Could not decode intake or input was too early!?");
            }
        }
        public static void OnServerEnemyAISetup(NetworkMessage netMsg)
        {
            DebugTAC_AI.Assert(true, "TACtical_AI: OnServerEnemyAISetup should not be sent to host.  This should not be happening.");
        }

        // AIEnemySiege
        public static void TryBroadcastNewEnemySiege(int Team, long HP, bool starting)
        {
            if (HostExists && ManNetwork.IsHost) try
                {
                    DebugTAC_AI.LogNet("Sent new TryBroadcastNewEnemySiege update to all but host");
                    Singleton.Manager<ManNetwork>.inst.SendToAllExceptHost(AIRetreatRequest, new AIEnemyStagedSiege(Team, HP, starting));
                }
                catch { DebugTAC_AI.LogNet("TACtical_AI: Failed to send TryBroadcastNewEnemySiege update, shouldn't be too bad in the long run"); }
        }
        public static void OnClientEnemySiegeUpdate(NetworkMessage netMsg)
        {
            var reader = new AIEnemyStagedSiege();
            netMsg.ReadMessage(reader);
            try
            {
                if (reader.Starting)
                    ManEnemySiege.InitSiegeWarning(reader.Team, reader.MaxHP);
                else
                    ManEnemySiege.EndSiege();
                DebugTAC_AI.LogNet("TACtical_AI: OnClientEnemySiegeUpdate received.  Attacker is " + reader.Team + " | HP: " + reader.MaxHP + " | is starting: " + reader.Starting);
            }
            catch
            {
                DebugTAC_AI.LogNet("TACtical_AI: OnClientEnemySiegeUpdate receive failiure! Could not decode intake or input was too early!?");
            }
        }
        public static void OnServerEnemySiegeUpdate(NetworkMessage netMsg)
        {
            DebugTAC_AI.Assert(true, "TACtical_AI: OnServerEnemySiegeUpdate should not be sent to host.  This should not be happening.");
        }


        public static class Patches
        {
            /// <summary>
            /// Note: Both sides must subscribe to work!
            /// </summary>
            [HarmonyPatch(typeof(NetPlayer), "OnStartClient")]
            static class OnStartClient
            {
                static void Postfix(NetPlayer __instance)
                {
                    // Standard
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIRetreatRequest, new ManNetwork.MessageHandler(OnClientSetRetreatState));
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIADVTypeChange, new ManNetwork.MessageHandler(OnClientSetNewAIState));
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIEnemyType, new ManNetwork.MessageHandler(OnClientEnemyAISetup));

                    // RTS
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIRTSPosCommand, new ManNetwork.MessageHandler(OnClientAcceptRTSCommand));
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIRTSPosControl, new ManNetwork.MessageHandler(OnClientAcceptRTSControl));
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIRTSAttack, new ManNetwork.MessageHandler(OnClientAcceptRTSAttack));
                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, AIEnemySiege, new ManNetwork.MessageHandler(OnClientEnemySiegeUpdate));

                    DebugTAC_AI.Log("Subscribed " + __instance.netId.ToString() + " to AdvancedAI updates from host.");
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartServer")]
            static class OnStartServer
            {
                static void Postfix(NetPlayer __instance)
                {
                    if (!HostExists)
                    {
                        // Standard
                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, AIRetreatRequest, new ManNetwork.MessageHandler(OnServerSetRetreatState));
                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, AIADVTypeChange, new ManNetwork.MessageHandler(OnServerSetNewAIState));
                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, AIEnemyType, new ManNetwork.MessageHandler(OnServerEnemyAISetup));

                        // RTS
                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, AIRTSPosCommand, new ManNetwork.MessageHandler(OnServerAcceptRTSCommand));
                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, AIRTSPosControl, new ManNetwork.MessageHandler(OnServerAcceptRTSControl));
                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, AIRTSAttack, new ManNetwork.MessageHandler(OnServerAcceptRTSAttack));
                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, AIEnemySiege, new ManNetwork.MessageHandler(OnServerEnemySiegeUpdate));

                        DebugTAC_AI.Log("Host started, hooked AdvancedAI update broadcasting to " + __instance.netId.ToString());
                        Host = __instance.netId;
                        HostExists = true;
                    }
                }
            }
        }
    }
}
