using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TAC_AI
{
    public static class IAIFollowableExt
    {
        public static bool IsNotNull(this IAIFollowable AIF)
        {
            return AIF?.tank != null;
        }
    }
    public interface IAIFollowable
    {
        Vector3 position { get; }
        Tank tank { get; }
    }
}
