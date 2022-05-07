using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;
using TAC_AI.Templates;

namespace TAC_AI
{
    public class CursorChanger : MonoBehaviour
    {
        static FieldInfo existingCursors = typeof(MousePointer).GetField("m_CursorDataSets", BindingFlags.NonPublic | BindingFlags.Instance);

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
        */
        public static bool AddedNewCursors = false;
        private static List<Texture2D> CursorTextureCache = new List<Texture2D>();
        public static int[] CursorIndexCache = new int[4];

        public static void AddNewCursors()
        {
            if (AddedNewCursors)
                return;
            MousePointer MP = FindObjectOfType<MousePointer>();
            DebugTAC_AI.Assert(!MP, "CursorChanger: AddNewCursors - THE CURSOR DOES NOT EXIST!");
            string DirectoryTarget = RawTechExporter.DLLDirectory + RawTechExporter.up + "AI_Icons";
            DebugTAC_AI.LogDevOnly("CursorChanger: AddNewCursors - Path: " + DirectoryTarget);
            try
            {
                int LODLevel = 0;
                MousePointer.CursorDataSet[] cursorLODs = (MousePointer.CursorDataSet[])existingCursors.GetValue(MP);
                foreach (var item in cursorLODs)
                {
                    List<MousePointer.CursorData> cursorTypes = item.m_CursorData.ToList();

                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderAttack", LODLevel, Vector2.zero, 0);// 1
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderEmpty", LODLevel, Vector2.zero, 1);// 2
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderMove", LODLevel, Vector2.zero, 2);// 3
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderSelect", LODLevel, Vector2.zero, 3);// 4
                    
                    item.m_CursorData = cursorTypes.ToArray();
                }
            }
            catch (Exception e) { DebugTAC_AI.Log("CursorChanger: AddNewCursors - failed to fetch rest of cursor textures " + e); }
            AddedNewCursors = true;
        }

        private static void TryAddNewCursor(List<MousePointer.CursorData> lodInst, string DLLDirectory, string name, int lodLevel, Vector2 center, int cacheIndex)
        {
            DebugTAC_AI.Info("CursorChanger: AddNewCursors - " + DLLDirectory + " for " + name + " " + lodLevel + " " + center);
            try
            {
                List<FileInfo> FI = new DirectoryInfo(DLLDirectory).GetFiles().ToList();
                Texture2D tex;
                try
                {
                    tex = FileUtils.LoadTexture(FI.Find(delegate (FileInfo cand)
                    { return cand.Name == name + lodLevel + ".png"; }).ToString());
                    CursorTextureCache.Add(tex);
                }
                catch
                {
                    DebugTAC_AI.Info("CursorChanger: AddNewCursors - failed to fetch cursor texture LOD " + lodLevel + " for " + name);
                    tex = FileUtils.LoadTexture(FI.Find(delegate (FileInfo cand)
                    { return cand.Name == name + "1.png"; }).ToString());
                    CursorTextureCache.Add(tex);
                }
                MousePointer.CursorData CD = new MousePointer.CursorData
                {
                    m_Hotspot = center * tex.width,
                    m_Texture = tex,
                };
                lodInst.Add(CD);
                CursorIndexCache[cacheIndex] = lodInst.IndexOf(CD);
                DebugTAC_AI.Info(name + " center: " + CD.m_Hotspot.x + "|" + CD.m_Hotspot.y);
            }
            catch { DebugTAC_AI.Assert(true, "CursorChanger: AddNewCursors - failed to fetch cursor texture " + name); }
        }

    }
}

