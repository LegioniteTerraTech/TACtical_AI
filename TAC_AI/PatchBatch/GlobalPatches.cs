using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace TAC_AI
{
    internal class GlobalPatches : MassPatcher
    {
        internal static class ModuleItemHolderBeamPatches
        {
            internal static Type target = typeof(ModuleItemHolderBeam);
            //Allow disabling of physics on mobile bases
            /// <summary>
            /// PatchModuleItemHolderBeamForStatic
            /// </summary>
            private static bool UpdateFloat_Prefix(ModuleItemHolderBeam __instance, ref Visible item)
            {
                return false;
            }
        }

    }
}

