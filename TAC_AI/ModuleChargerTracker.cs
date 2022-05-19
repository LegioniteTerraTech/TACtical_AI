using System;
using TAC_AI.AI;
using UnityEngine;

namespace TAC_AI
{
    public class ModuleChargerTracker : Module
    {
        TankBlock TankBlock;
        // Returns the position of itself in the world as a point the AI can pathfind to
        public Tank tank;
        public Transform trans;
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
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
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
            EnergyRegulator.EnergyState energyThis = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);

            EnergyRegulator.EnergyState energyThat = toChargeTank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            float chargeFraction = (energyThat.storageTotal - energyThat.spareCapacity) / energyThat.storageTotal;

            return (energyThis.storageTotal - energyThis.spareCapacity) > minEnergyAmount && (energyThis.storageTotal - energyThis.spareCapacity) / energyThis.storageTotal > chargeFraction;
        }
        public void RequestDocking(AIECore.TankAIHelper Approaching)
        {
            if (!DockingRequested)
            {
                if (tank == null)
                {
                    DebugTAC_AI.Log("TACtical_AI: Tried to request docking to a charger that was not attached to anything");
                    return;
                }
                DockingRequested = true;
                Invoke("StopDocking", 2);
                tank.GetComponent<AIECore.TankAIHelper>().AllowApproach(Approaching);
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
