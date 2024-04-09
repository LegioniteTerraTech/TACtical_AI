using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using TAC_AI.AI;
using TAC_AI.AI.Enemy;
using UnityEngine;
using TerraTechETCUtil;

namespace TAC_AI
{
    internal class ReverseCache : MonoBehaviour
	{   // For blocks edited externally - we need to destroy them from affecting the pool of related blocks

		private static readonly FieldInfo minerOp = typeof(ModuleItemProducer).GetField("m_OperationMode", BindingFlags.NonPublic | BindingFlags.Instance),
		    mineOut = typeof(ModuleItemProducer).GetField("m_CompatibleChunkTypes", BindingFlags.NonPublic | BindingFlags.Instance);

		private bool cached = false;
		private ChunkTypes[] chunks;
		private ModuleItemProducer.OperateConditionFlags flags;
		public void SaveComponents()
		{   // can't have this cycling again can we?
			if (!cached)
			{
				ModuleItemProducer produce = GetComponent<ModuleItemProducer>();
				chunks = (ChunkTypes[])mineOut.GetValue(produce);
				flags = (ModuleItemProducer.OperateConditionFlags)minerOp.GetValue(produce);
				minerOp.SetValue(produce, ModuleItemProducer.OperateConditionFlags.Anchored);
				mineOut.SetValue(produce, RLoadedBases.TryGetBiomeResource(transform.position));

				cached = true;

				//DebugTAC_AI.Log(KickStart.ModID + ": Saved " + name);
                try
                {
                    Tank tank = transform.root.GetComponent<Tank>();
                    TankAIHelper help = tank.GetHelperInsured();
                    tank.FixupAnchors(true);
                    if (tank.IsAnchored)
                        tank.Anchors.UnanchorAll(true);
                    if (!tank.IsAnchored)
                    {
                        help.TryReallyAnchor();
                        if (!tank.IsAnchored)
                        {
                            DebugTAC_AI.Log(KickStart.ModID + ": Anchor is being stubborn");
                            help.TryReallyAnchor();
                        }
                    }
                    GetComponent<TankBlock>().SubToBlockAttachConnected(LoadNow, null);
                }
                catch
                {
                    DebugTAC_AI.Log(KickStart.ModID + ": fired prematurely");
                }
			}
		}
		public void LoadNow()
		{   
			mineOut.SetValue(GetComponent<ModuleItemProducer>(), chunks);
			minerOp.SetValue(GetComponent<ModuleItemProducer>(), flags);
			//DebugTAC_AI.Log(KickStart.ModID + ": Loaded " + name);
			cached = false;
            GetComponent<TankBlock>().UnSubToBlockAttachConnected(LoadNow, null);
            DestroyImmediate(this);
		}
    }
}
