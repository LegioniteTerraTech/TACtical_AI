using System;
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

        public static implicit operator Transform(ModuleHarvestReciever yes)
        {
            return yes.trans;
        }

        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
            trans = transform;
            holder = gameObject.GetComponent<ModuleItemHolder>();
        }
        public void OnAttach()
        {
            AI.AIECore.Depots.Add(this);
            tank = transform.root.GetComponent<Tank>();
        }
        public void OnDetach()
        {
            tank = null;
            AI.AIECore.Depots.Remove(this);
        }
    }
}
