using System;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI
{
    public class ModuleHarvestReciever : Module
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
        }
        public void DelayedSub()
        {
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
            OnAttach();
        }
        public void OnAttach()
        {
            AIECore.Depots.Add(this);
            tank = transform.root.GetComponent<Tank>();
            DockingRequested = false;
        }
        public void OnDetach()
        {
            tank = null;
            AIECore.Depots.Remove(this);
            DockingRequested = false;
        }
        public void RequestDocking()
        {
            if (!DockingRequested)
            {
                if (tank == null)
                {
                    Debug.Log("TACtical_AI: Tried to request docking to a charger that was not attached to anything");
                    return;
                }
                DockingRequested = true;
                Invoke("StopDocking", 2);
                tank.GetComponent<AIECore.TankAIHelper>().AllowApproach();
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
