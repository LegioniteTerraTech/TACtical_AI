using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;
using TerraTechETCUtil;

namespace TAC_AI
{
    public class CursorChanger : MonoBehaviour
    {
        // static FieldInfo existingCursors = typeof(MousePointer).GetField("m_CursorDataSets", BindingFlags.NonPublic | BindingFlags.Instance);

        /*
            Default,
            OverGrabbable,
            HoldingGrabbable,
            Painting,
            SkinPainting,
            SkinPaintingOverPaintable,
            SkinTechPainting,
            SkinTechPaintingOverPaintable,
            Disabled
            // NEW
            AIOrderAttack   0
            AIOrderEmpty    1
            AIOrderMove     2
            AIOrderSelect   3
            AIOrderFetch    4
            AIOrderMine     5
            AIOrderProtect  6
            AIOrderScout    7
        */
        public static CursorChangeHelper.CursorChangeCache Cache;
        public static bool AddedNewCursors = false;
        public static CursorChangeHelper.CursorChangeCache CursorIndexCache => Cache.CursorIndexCache;

        public static void AddNewCursors()
        {
            if (AddedNewCursors)
                return;
            if (ResourcesHelper.TryGetModContainer("Advanced AI", out ModContainer MC))
            {
                Cache = CursorChangeHelper.GetCursorChangeCache(RawTechExporter.DLLDirectory, "AI_Icons", MC,
                    new KeyValuePair<string, bool>("AIOrderAttack", false),
                    new KeyValuePair<string, bool>("AIOrderEmpty", false),
                    new KeyValuePair<string, bool>("AIOrderMove", false),
                    new KeyValuePair<string, bool>("AIOrderSelect", false),
                    new KeyValuePair<string, bool>("AIOrderBlock", false),
                    new KeyValuePair<string, bool>("AIOrderSelect", false),
                    new KeyValuePair<string, bool>("AIOrderMine", false),
                    new KeyValuePair<string, bool>("AIOrderAegis", false),
                    new KeyValuePair<string, bool>("AIOrderScout", false)
                    );
            }
            else
                DebugTAC_AI.Assert(true, "CursorChanger: AddNewCursors - Could not find ModContainer for Advanced AI!");

            AddedNewCursors = true;
        }
    }
}

