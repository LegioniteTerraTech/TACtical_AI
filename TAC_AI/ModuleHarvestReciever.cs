using System;
using TAC_AI.AI;
using UnityEngine;
using TerraTechETCUtil;

namespace TAC_AI
{
    internal class ModuleHarvestReciever : MonoBehaviour
    {
        TankBlock TankBlock;
        // Returns the position of itself in the world as a point the AI can pathfind to
        public Tank tank;
        public Transform trans;
        public ModuleItemHolder holder;
        private bool DockingRequested = false;

        public static implicit operator Transform(ModuleHarvestReciever yes)
        {
            return yes.trans;
        }

        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            Invoke("DelayedSub", 0.001f);
            trans = transform;
            holder = gameObject.GetComponent<ModuleItemHolder>();
            TankBlock.SubToBlockAttachConnected(null, OnDetach);
            if (TankBlock.tank)
                OnAttach();
        }
        public void DelayedSub()
        {
            TankBlock.SubToBlockAttachConnected(OnAttach, null);
        }
        public void OnAttach()
        {
            if (holder)
            {
                if (holder.Acceptance.HasFlag(ModuleItemHolder.AcceptFlags.Blocks))
                {
                    //DebugTAC_AI.Log("Block " + name + " is a Block Receiver");
                    AIECore.BlockHandlers.Add(this);
                }
                if (holder.Acceptance.HasFlag(ModuleItemHolder.AcceptFlags.Chunks))
                {
                    //DebugTAC_AI.Log("Block " + name + " is a Chunk Receiver");
                    AIECore.Depots.Add(this);
                }
            }
            tank = transform.root.GetComponent<Tank>();
            DockingRequested = false;
        }
        public void OnDetach()
        {
            tank = null;
            if (holder)
            {
                if (holder.Acceptance.HasFlag(ModuleItemHolder.AcceptFlags.Blocks))
                {
                    AIECore.BlockHandlers.Remove(this);
                }
                if (holder.Acceptance.HasFlag(ModuleItemHolder.AcceptFlags.Chunks))
                {
                    AIECore.Depots.Remove(this);
                }
            }
            DockingRequested = false;
        }
        public void RequestDocking(TankAIHelper Approaching)
        {
            if (!DockingRequested)
            {
                if (tank == null)
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": Tried to request docking to a charger that was not attached to anything");
                    return;
                }
                DockingRequested = true;
                Invoke("StopDocking", 2);
                tank.GetHelperInsured().SlowForApproacher(Approaching);
            }
        }
        private void StopDocking()
        {
            if (DockingRequested)
            {
                DockingRequested = false;
            }
        }
    }
}
