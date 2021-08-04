using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using TAC_AI.AI.Enemy;
using UnityEngine;

namespace TAC_AI
{
    public class ReverseCache : Module
	{   // For blocks edited externally - we need to destroy them from affecting the pool of related blocks

		private static FieldInfo minerOp = typeof(ModuleItemProducer).GetField("m_OperationMode", BindingFlags.NonPublic | BindingFlags.Instance);
		private static FieldInfo mineOut = typeof(ModuleItemProducer).GetField("m_CompatibleChunkTypes", BindingFlags.NonPublic | BindingFlags.Instance);

		private bool cached = false;
		private ChunkTypes[] chunks;
		private ModuleItemProducer.OperateConditionFlags flags;
		public void SaveComponents()
		{   // can't have this cycling again can we?
			if (!cached)
			{
				chunks = (ChunkTypes[])mineOut.GetValue(GetComponent<ModuleItemProducer>());
				flags = (ModuleItemProducer.OperateConditionFlags)minerOp.GetValue(GetComponent<ModuleItemProducer>());
				minerOp.SetValue(GetComponent<ModuleItemProducer>(), ModuleItemProducer.OperateConditionFlags.Anchored);
				mineOut.SetValue(GetComponent<ModuleItemProducer>(), RBases.TryGetBiomeResource(transform.position));

				cached = true;
				Debug.Log("TACtical_AI: Saved " + name);
			}
		}
		public void LoadNow()
		{   // can't have this cycling again can we?
			mineOut.SetValue(GetComponent<ModuleItemProducer>(), chunks);
			minerOp.SetValue(GetComponent<ModuleItemProducer>(), flags);
			Debug.Log("TACtical_AI: Loaded " + name);
			DestroyImmediate(this);
		}
    }
}
