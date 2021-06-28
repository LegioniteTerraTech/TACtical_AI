using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI.AI
{
    public class BlockMemory
    {   // Save the blocks!
        public Vector3 CachePos = Vector3.zero;
        public OrthoRotation CacheRot;
        public BlockTypes blockType = BlockTypes.GSOAIController_111;
    }
}
