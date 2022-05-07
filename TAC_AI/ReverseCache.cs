using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using TAC_AI.AI.Enemy;
using UnityEngine;

namespace TAC_AI
{
    public class ReverseCache : MonoBehaviour
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
				mineOut.SetValue(produce, RBases.TryGetBiomeResource(transform.position));

				cached = true;

				//Debug.Log("TACtical_AI: Saved " + name);
                try
                {
                    Tank tank = transform.root.GetComponent<Tank>();
                    tank.FixupAnchors();
                    if (tank.IsAnchored)
                        tank.Anchors.UnanchorAll(true);
                    if (!tank.IsAnchored)
                    {
                        DebugTAC_AI.Log("TACtical_AI: Anchor is being stubborn");
                        tank.TryToggleTechAnchor();
                        if (!tank.IsAnchored)
                        {
                            DebugTAC_AI.Log("TACtical_AI: Anchor is being stubborn 2");
                            tank.TryToggleTechAnchor();
                        }
                    }
                    if (!tank.IsAnchored)
                        tank.Anchors.TryAnchorAll(true);
                    if (!tank.IsAnchored)
                        tank.TryToggleTechAnchor();
                    if (!tank.IsAnchored)
                    {
                        tank.Anchors.RetryAnchorOnBeam = true;
                        tank.TryToggleTechAnchor();
                    }
                    GetComponent<TankBlock>().AttachEvent.Subscribe(LoadNow);
                }
                catch
                {
                    DebugTAC_AI.Log("TACtical_AI: fired prematurely");
                }
			}
		}
		public void LoadNow()
		{   
			mineOut.SetValue(GetComponent<ModuleItemProducer>(), chunks);
			minerOp.SetValue(GetComponent<ModuleItemProducer>(), flags);
			//Debug.Log("TACtical_AI: Loaded " + name);
			cached = false;
            GetComponent<TankBlock>().AttachEvent.Unsubscribe(LoadNow);
            DestroyImmediate(this);
		}
    }
}
