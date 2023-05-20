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
            AIOrderFetch    4
            AIOrderMine     5
            AIOrderProtect  6
            AIOrderScout    7
        */
        public static CursorChangeHelper.CursorChangeCache Cache;
        public static bool AddedNewCursors = false;
        private static List<Texture2D> CursorTextureCache = new List<Texture2D>(8);
        /// <summary>
        /// Index, (Cursor base texture, Overwritten texture)
        /// </summary>
        private static Dictionary<int, KeyValuePair<Texture2D, Texture2D>> CursorTextureSwapBackup = new Dictionary<int, KeyValuePair<Texture2D, Texture2D>>();
        public static int[] CursorIndexCache = new int[8];

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

                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderAttack", LODLevel, Vector2.zero, 0);  // 1
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderEmpty", LODLevel, Vector2.zero, 1);   // 2
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderMove", LODLevel, Vector2.zero, 2);    // 3
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderSelect", LODLevel, Vector2.zero, 3);  // 4
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderBlock", LODLevel, Vector2.zero, 4);   // 5
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderMine", LODLevel, Vector2.zero, 5);    // 6
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderAegis", LODLevel, Vector2.zero, 6); // 7
                    TryAddNewCursor(cursorTypes, DirectoryTarget, "AIOrderScout", LODLevel, Vector2.zero, 7);   // 8

                    item.m_CursorData = cursorTypes.ToArray();
                }
            }
            catch (Exception e) { DebugTAC_AI.Log("CursorChanger: AddNewCursors - failed to fetch rest of cursor textures " + e); }
            AddedNewCursors = true;
        }


        public static void ChangeMiniIcon(int cacheIndex, IntVector2 toAddOffset, Texture2D toAdd)
        {
            if (AddedNewCursors)
                return;
            try
            {
                if (CursorTextureSwapBackup.TryGetValue(cacheIndex, out KeyValuePair<Texture2D, Texture2D> tex))
                {
                    Texture2D oldTex = tex.Key;
                    if (toAdd != tex.Value)
                    {
                        ApplyTextureDeltaAdditive(cacheIndex, toAddOffset, oldTex, toAdd);
                        CursorTextureSwapBackup.Remove(cacheIndex);
                        CursorTextureSwapBackup.Add(cacheIndex, new KeyValuePair<Texture2D, Texture2D>(oldTex, toAdd));
                    }
                }
                else
                {
                    Texture2D oldTex = CursorTextureCache[cacheIndex];
                    Texture2D backupTex = new Texture2D(oldTex.width, oldTex.height, oldTex.format, oldTex.mipmapCount > 1);
                    Graphics.CopyTexture(oldTex, backupTex);
                    ApplyTextureDeltaAdditive(cacheIndex, toAddOffset, backupTex, toAdd);
                    CursorTextureSwapBackup.Add(cacheIndex, new KeyValuePair<Texture2D, Texture2D>(backupTex, toAdd));
                }
            }
            catch (Exception e) { DebugTAC_AI.Log("CursorChanger: ChangeMiniIcon - failed to change " + e); }
        }
        private static void ApplyTextureDeltaAdditive(int cacheIndex, IntVector2 toAddOffset, Texture2D baseTex, Texture2D toAdd)
        {
            Texture2D toAddTo = CursorTextureCache[cacheIndex];
            if (toAddTo.width != baseTex.width || toAddTo.height != baseTex.height)
                DebugTAC_AI.Exception("CursorChanger: ApplyTextureDeltaAdditive - Mismatch in toAddTo and baseTex dimensions!");
            for (int xStep = 0; xStep < toAddTo.width; xStep++)
            {
                for (int yStep = 0; yStep < toAddTo.height; yStep++)
                {
                    Color applyColor = baseTex.GetPixel(xStep, yStep);
                    Color applyAdd = toAdd.GetPixel(xStep + toAddOffset.x, yStep + toAddOffset.y);
                    if (applyAdd.a > 0.05f)
                    {
                        applyColor.r = Mathf.Clamp01(applyColor.r + applyAdd.r);
                        applyColor.g = Mathf.Clamp01(applyColor.g + applyAdd.g);
                        applyColor.b = Mathf.Clamp01(applyColor.b + applyAdd.b);
                        applyColor.a = Mathf.Clamp01(applyColor.a + applyAdd.a);
                    }
                    CursorTextureCache[cacheIndex].SetPixel(xStep, yStep, applyColor);
                }
            }
            CursorTextureCache[cacheIndex].Apply();
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
                    /*
                    tex = FileUtils.LoadTexture(FI.Find(delegate (FileInfo cand)
                    { return cand.Name == name + lodLevel + ".png"; }).ToString());
                    CursorTextureCache.Add(tex);*/
                    tex = RawTechExporter.FetchTexture(name + lodLevel + ".png");
                    if (tex == null)
                        throw new NullReferenceException();
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

