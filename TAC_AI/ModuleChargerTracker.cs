using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI
{
    public class ModuleChargerTracker : Module
    {
        TankBlock TankBlock;
        // Returns the position of itself in the world as a point the AI can pathfind to
        public Tank tank;
        public Transform trans;
        public ModuleItemHolder holder;
        internal float minEnergyAmount = 200;

        public static implicit operator Transform(ModuleChargerTracker yes)
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
            AI.AIECore.Chargers.Add(this);
            tank = transform.root.GetComponent<Tank>();
        }
        public void OnDetach()
        {
            tank = null;
            AI.AIECore.Chargers.Remove(this);
        }
        public bool CanTransferCharge(Tank toChargeTank)
        {
            if (tank == null)
                return false;
            EnergyRegulator.EnergyState energyThis = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);

            EnergyRegulator.EnergyState energyThat = toChargeTank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            float chargeFraction = energyThat.currentAmount / energyThat.storageTotal;

            return energyThis.currentAmount > minEnergyAmount && energyThis.currentAmount / energyThis.storageTotal > chargeFraction;
        }
    }
}
