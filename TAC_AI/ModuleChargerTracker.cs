using System;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI
{
    internal class ModuleChargerTracker : MonoBehaviour, IAIFollowable
    {
        TankBlock TankBlock;
        // Returns the position of itself in the world as a point the AI can pathfind to
        public Tank tank { get; private set; }
        public Transform trans;
        public Vector3 position => trans.position;
        internal float minEnergyAmount = 200;
        private bool DockingRequested = false;

        public static implicit operator Transform(ModuleChargerTracker yes)
        {
            return yes.trans;
        }

        public void OnPool()
        {
            if (TankBlock)
                return;
            TankBlock = gameObject.GetComponent<TankBlock>();
            trans = transform;
            Invoke("DelayedSub", 0.001f);
        }
        public void DelayedSub()
        {
            TankBlock.AttachedEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachingEvent.Subscribe(new Action(OnDetach));
            if (TankBlock.tank)
                OnAttach();
        }
        public void OnAttach()
        {
            AIECore.Chargers.Add(this);
            tank = transform.root.GetComponent<Tank>();
            DockingRequested = false;
        }
        public void OnDetach()
        {
            tank = null;
            AIECore.Chargers.Remove(this);
            DockingRequested = false;
        }
        public bool CanTransferCharge(Tank toChargeTank)
        {
            if (tank == null)
                return false;
            TechEnergy.EnergyState energyThis = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);

            TechEnergy.EnergyState energyThat = toChargeTank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            float chargeFraction = (energyThat.storageTotal - energyThat.spareCapacity) / energyThat.storageTotal;

            return (energyThis.storageTotal - energyThis.spareCapacity) > minEnergyAmount && (energyThis.storageTotal - energyThis.spareCapacity) / energyThis.storageTotal > chargeFraction;
        }
        public void RequestDocking(TankAIHelper requesterHelper)
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
                tank.GetHelperInsured().SlowForApproacher(requesterHelper);
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
