using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.World
{
    internal class TileMoveCommand
    {
        public readonly IntVector2 PrevTileCoord;
        public readonly Vector3 posInTile;
        public readonly IntVector2 TargetTileCoord;
        public readonly int ExpectedMoveTurns;
        public readonly Event<TileMoveCommand, Tank> OnTechLoaded;
        public int CurrentTurn;
        public readonly NP_TechUnit ETU;
        /// <summary>
        /// this, Worked, Loaded
        /// </summary>
        public readonly Action<TileMoveCommand, bool, bool> call;

        public TileMoveCommand(NP_TechUnit unit, IntVector2 endTileWorld, int expectedTime, Action<TileMoveCommand, bool, bool> callback)
        {
            PrevTileCoord = unit.tilePos;
            posInTile = unit.WorldPos.TileRelativePos;
            TargetTileCoord = endTileWorld;
            ExpectedMoveTurns = expectedTime;
            ETU = unit;
            call = callback;
            OnTechLoaded = new Event<TileMoveCommand, Tank>();
            CurrentTurn = 0;
        }

        public bool TryGetActiveTank(out Tank tank)
        {
            tank = null;
            var TV = ManVisible.inst.GetTrackedVisible(ETU.ID);
            if (TV != null)
                tank = TV.visible?.tank;
            return tank != null;
        }

        public bool IsValid()
        {
            if (!ETU.Exists())
            {
                //DebugTAC_AI.Log(KickStart.ModID + ": IsValid(ETU) " + ETU.tech.m_TechData.Name + " is INVALID!");
                return false;
            }
            return true;
        }
        public void OnFinished(bool success, bool stillRegistered)
        {
            if (call != null)
                call.Invoke(this, success, stillRegistered);
            if (stillRegistered && TryGetActiveTank(out var actTank) && OnTechLoaded.HasSubscribers())
                OnTechLoaded.Send(this, actTank);
        }
        public Vector3 PosSceneCurTime()
        {
            float percent = (float)CurrentTurn / ExpectedMoveTurns;
            return Vector3.Lerp(ManWorld.inst.TileManager.CalcTileOriginScene(PrevTileCoord),
                ManWorld.inst.TileManager.CalcTileOriginScene(TargetTileCoord), percent) + posInTile;
        }
    }
}
