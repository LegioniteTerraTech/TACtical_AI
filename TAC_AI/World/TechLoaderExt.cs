
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;
using Snapshots;
using System.Reflection;
using HarmonyLib;

namespace TAC_AI.World
{
    /// <summary>
    /// Makes the TechSelector usable for Mods, and for general other case usages
    /// </summary>
    public class TechLoaderExt
    {
        private static FieldInfo GetUI = typeof(UISnapshotsPanelHUD).GetField("m_SnapshotViewModel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        public static bool IsSelectingTech => OnSelectedTechCallback != null;
        private static Action<Snapshot> OnSelectedTechCallback = null;
        public static bool IsSelectingFolder => OnSelectedFolderCallback != null;
        private static Action<string> OnSelectedFolderCallback = null;
        internal static bool QueuedOpen = false;

        private static VMSnapshotPanel UIControl = null;
        private static UISnapshotsPanelHUD UIPanel = null;
        private static Snapshot prevTech = default;
        private static string prevFolder = string.Empty;
        private static void OnHudOpened(UIHUDElement ele)
        {
            if (ele && ele.HudElementType == ManHUD.HUDElementType.TechLoader)
            {
                if (!QueuedOpen)
                    OnFinishedUsing();
            }
        }
        private static void OnHudClosed(UIHUDElement ele)
        {
            if (ele && ele.HudElementType == ManHUD.HUDElementType.TechLoader)
            {
                OnFinishedUsing();
            }
        }

        private static bool SetSelectedSnapshotFolder()
        {
            if (!QueuedOpen && OnFolderSelect())
                return false;
            return true;
        }
        private static void Insure()
        {
            if (UIControl)
                return;
            try
            {
                ManHUD.inst.InitialiseHudElement(ManHUD.HUDElementType.TechLoader);
                KickStart.harmonyInstance.MassPatchAllWithin(typeof(TechSelectorExtender), "TerraTechModExt2");
                var snapP = AccessTools.Method(typeof(VMSnapshotPanel), "SetSelectedSnapshotFolder", new Type[] { typeof(string) });
                if (snapP == null)
                    throw new NullReferenceException("VMSnapshotPanel.SetSelectedSnapshotFolder");
                var folder = typeof(TechLoaderExt).GetMethod("SetSelectedSnapshotFolder", BindingFlags.NonPublic | BindingFlags.Static);
                if (folder == null)
                    throw new NullReferenceException("TechLoaderExt.SetSelectedSnapshotFolder");
                var fetch = new HarmonyMethod(folder);
                if (fetch == null)
                    throw new NullReferenceException("HarmonyMethod");
                KickStart.harmonyInstance.Patch(snapP, fetch);
                try
                {
                    UIPanel = (UISnapshotsPanelHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.TechLoader);
                    if (!UIPanel)
                        throw new NullReferenceException("Could not get UISnapshotPanel after attempting " +
                            "to initialise it");
                }
                catch (Exception e)
                {
                    DebugTAC_AI.Log("Failed, trying loading it then - " + e);
                    ManHUD.inst.ShowHudElement(ManHUD.HUDElementType.TechLoader);
                    UIPanel = (UISnapshotsPanelHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.TechLoader);
                    if (!UIPanel)
                        throw new NullReferenceException("Could not get UISnapshotPanel after attempting " +
                            "to initialise it(2)");
                    ManHUD.inst.HideHudElement(ManHUD.HUDElementType.TechLoader);
                }
                UIControl = (VMSnapshotPanel)GetUI.GetValue(UIPanel);
                ManHUD.inst.OnShowHUDElementEvent.Subscribe(OnHudOpened);
                ManHUD.inst.OnExpandHUDElementEvent.Subscribe(OnHudOpened);
                ManHUD.inst.OnHideHUDElementEvent.Subscribe(OnHudClosed);
                ManHUD.inst.OnCollapseHUDElementEvent.Subscribe(OnHudClosed);
            }
            catch (Exception e)
            {
                throw new Exception("TechSelectorExt.Insure() failed to init:", e);
            }
        }
        private static Snapshot selectedTech => UIControl.m_Selected?.Value.m_Snapshot;
        private static string selectedFolder => UIControl.m_SelectedFolderName.Value;
        internal static bool OnSnapPlacement()
        {
            if (IsSelectingTech)
            {
                OnSelectedTechCallback.Invoke(selectedTech);
                OnFinishedUsing(false);
            }
            return false;
        }
        internal static bool OnFolderSelect()
        {
            if (IsSelectingFolder)
            {
                OnSelectedFolderCallback.Invoke(selectedFolder);
                OnFinishedUsing(false);
            }
            return false;
        }
        internal static bool WarnInvalidTechData()
        {
            if (UIControl.m_Selected?.Value.m_Snapshot != null)
            {
                if (UIControl.m_Selected.Value.m_ValidData.HasMissingBlocksPlace)
                {
                    UIControl.m_Warnings.Add(new VMSnapshotPanel.WarningData
                    {
                        m_WarningType = PlacementSelection.InvalidType.General,
                        m_Text = Localisation.inst.GetLocalisedString(
                            LocalisationEnums.StringBanks.TechPlacement, 1,
                            Array.Empty<Localisation.GlyphInfo>())
                    });
                }
            }
            else
            {
                UIControl.m_Warnings.Add(new VMSnapshotPanel.WarningData
                {
                    m_WarningType = PlacementSelection.InvalidType.General,
                    m_Text = "Tech NULL",
                });
            }
            return false;
        }
        internal static bool WarnNoSpawnTechsInFolderMode()
        {
            UIControl.m_Warnings.Add(new VMSnapshotPanel.WarningData
            {
                m_WarningType = PlacementSelection.InvalidType.General,
                m_Text = "Select a Folder",
            });
            return false;
        }

        private static void OnFinishedUsing(bool sendNull = true)
        {
            if (!UIControl)
                throw new NullReferenceException("UIControl NULL");
            try
            {
                if (!ManHUD.inst.IsHudElementVisible(ManHUD.HUDElementType.TechLoader))
                    return;
                if (OnSelectedFolderCallback != null)
                {
                    DebugTAC_AI.Log("Started TechSelectorExt.AssignFolder() request");
                    UIControl.m_SwapOptionVisible.Value = true;
                    UIControl.m_PlaceOptionVisible.Value = true;
                    if (sendNull)
                        OnSelectedFolderCallback(null);
                    UIControl.m_SelectedFolderName.Value = prevFolder;
                    DebugTAC_AI.Log("Finished TechSelectorExt.AssignFolder() request");
                }
                if (OnSelectedTechCallback != null)
                {
                    DebugTAC_AI.Log("Started TechSelectorExt.Open() request");
                    UIControl.m_SwapOptionVisible.Value = true;
                    UIControl.m_PlaceOptionVisible.Value = true;
                    if (sendNull)
                        OnSelectedTechCallback(null);
                    for (int i = 0; i < UIControl.m_Snapshots.Count; i++)
                    {
                        if (UIControl.m_Snapshots[i].m_Snapshot == prevTech)
                        {
                            UIControl.SetSelectedSnapshot(i);
                            break;
                        }
                    }
                    DebugTAC_AI.Log("Finished TechSelectorExt.Open() request");
                }
                OnSelectedFolderCallback = null;
                OnSelectedTechCallback = null;
                ManHUD.inst.HideHudElement(ManHUD.HUDElementType.TechLoader);
            }
            catch { }
        }
        /// <summary>
        /// To use the Tech Loader UI. 
        /// Note it returns null if the tech selected is NULL or the window is reopened for a different purpose.
        /// </summary>
        /// <param name="CallbackOnSelected">To invoke</param>
        /// <param name="PrevSnap">The last snapshot you want this to default to</param>
        /// <param name="overrideRequest">If you don't want an exception thrown if there is already a request on the UI</param>
        /// <exception cref="InvalidOperationException">If there is already another TechLoader UI operation in progress</exception>
        public static void Open(Action<Snapshot> CallbackOnSelected, Snapshot PrevSnap, bool UsePlaceInstead, bool overrideRequest = false)
        {
            Insure();
            int error = 0;
            try
            {
                if (overrideRequest)
                {
                    bool didOverride = false;
                    error = 1;
                    if (IsSelectingFolder)
                    {
                        DebugTAC_AI.Log("Overrided previous TechSelectorExt.AssignFolder() request");
                        didOverride = true;
                    }
                    error = 2;
                    if (IsSelectingTech && CallbackOnSelected != OnSelectedTechCallback)
                    {
                        DebugTAC_AI.Log("Overrided previous TechSelectorExt.Open() request");
                        didOverride = true;
                    }
                    error = 3;
                    if (didOverride)
                        OnFinishedUsing();
                }
                else
                {
                    if (IsSelectingFolder)
                        throw new InvalidOperationException("TechSelectorExt.Open() cannot be called because " +
                            "another folder selection operation is already in progress!");
                    if (IsSelectingTech && CallbackOnSelected != OnSelectedTechCallback)
                        throw new InvalidOperationException("TechSelectorExt.Open() cannot be called because " +
                            "another tech selection operation is already in progress!");
                }
                QueuedOpen = true;
                try
                {
                    error = 4;
                    if (ManHUD.inst.IsHudElementVisible(ManHUD.HUDElementType.TechLoader))
                    {
                        /*
                        DebugTAC_AI.Log("TechSelectorExt.Open was called while TechLoader was already open, sending a fake" +
                            "ManHUD.inst.OnHideHUDElementEvent to fool button to still be clickable");
                        ManHUD.inst.OnHideHUDElementEvent.Send(ManHUD.inst.GetHudElement(ManHUD.HUDElementType.TechLoader));
                        */
                        ManHUD.inst.HideHudElement(ManHUD.HUDElementType.TechLoader);
                    }
                    error = 5;
                    if (!IsSelectingTech)
                        prevTech = UIControl.m_Selected.Value.m_Snapshot;
                    error = 6;
                    if (!ManHUD.inst.IsHudElementVisible(ManHUD.HUDElementType.TechLoader))
                    {
                        DebugTAC_AI.Log("TechSelectorExt.Open - opening UI");
                        ManHUD.inst.ShowHudElement(ManHUD.HUDElementType.TechLoader);
                    }
                    error = 7;
                    OnSelectedTechCallback = CallbackOnSelected;
                    error = 8;
                    if (UsePlaceInstead)
                        UIControl.m_SwapOptionVisible.Value = false;
                    else
                        UIControl.m_PlaceOptionVisible.Value = false;
                    error = 9;
                    if (PrevSnap != null)
                    {
                        try
                        {
                            for (int i = 0; i < UIControl.m_Snapshots.Count; i++)
                            {
                                if (UIControl.m_Snapshots[i].m_Snapshot == PrevSnap)
                                {
                                    UIControl.SetSelectedSnapshot(i);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                    error = 10;
                }
                finally
                {
                    QueuedOpen = false;
                }
            }
            catch (Exception e)
            {
                UIControl.m_SwapOptionVisible.Value = true;
                throw new Exception("Error - " + error, e);
            }
        }
        /// <summary>
        /// To use the Tech Loader UI. 
        /// Note it returns null if the folder selected is NULL or the window is reopened for a different purpose.
        /// </summary>
        /// <param name="CallbackOnSelected">To invoke</param>
        /// <param name="PrevFolder">The last folder you want this to default to</param>
        /// <param name="overrideRequest">If you don't want an exception thrown if there is already a request on the UI</param>
        /// <exception cref="InvalidOperationException">If there is already another TechLoader UI operation in progress</exception>
        public static void AssignFolder(Action<string> CallbackOnSelected, string PrevFolder, bool overrideRequest = false)
        {
            Insure();
            if (!ManSnapshots.inst.SupportsFolders())
                return;
            try
            {
                if (overrideRequest)
                {
                    bool didOverride = false;
                    if (IsSelectingTech)
                    {
                        DebugTAC_AI.Log("Overrided previous TechSelectorExt.Open() request");
                        didOverride = true;
                    }
                    if (IsSelectingFolder && CallbackOnSelected != OnSelectedFolderCallback)
                    {
                        DebugTAC_AI.Log("Overrided previous TechSelectorExt.AssignFolder() request");
                        didOverride = true;
                    }
                    if (didOverride)
                        OnFinishedUsing();
                }
                else
                {
                    if (IsSelectingTech)
                        throw new InvalidOperationException("TechSelectorExt.AssignFolder() cannot be called because " +
                            "another tech selection operation is already in progress!");
                    if (IsSelectingFolder && CallbackOnSelected != OnSelectedFolderCallback)
                        throw new InvalidOperationException("TechSelectorExt.AssignFolder() cannot be called because " +
                            "another folder selection operation is already in progress!");
                }
                QueuedOpen = true;
                try
                {
                    if (ManHUD.inst.IsHudElementVisible(ManHUD.HUDElementType.TechLoader))
                    {
                        /*
                        DebugTAC_AI.Log("TechSelectorExt.Open was called while TechLoader was already open, sending a fake" +
                            "ManHUD.inst.OnHideHUDElementEvent to fool button to still be clickable");
                        ManHUD.inst.OnHideHUDElementEvent.Send(ManHUD.inst.GetHudElement(ManHUD.HUDElementType.TechLoader));
                        */
                        ManHUD.inst.HideHudElement(ManHUD.HUDElementType.TechLoader);
                    }
                    if (!IsSelectingFolder)
                        prevFolder = UIControl.m_SelectedFolderName.Value;
                    if (!ManHUD.inst.IsHudElementVisible(ManHUD.HUDElementType.TechLoader))
                    {
                        DebugTAC_AI.Log("TechSelectorExt.Open - opening UI");
                        ManHUD.inst.ShowHudElement(ManHUD.HUDElementType.TechLoader);
                    }
                    OnSelectedFolderCallback = CallbackOnSelected;
                    UIControl.m_SwapOptionVisible.Value = false;
                    UIControl.m_PlaceOptionVisible.Value = false;
                    try
                    {
                        if (!PrevFolder.NullOrEmpty() && UIControl.m_SnapshotFolderCache.Exists(x => x.Name == PrevFolder))
                            UIControl.m_SelectedFolderName.Value = PrevFolder;
                    }
                    catch { }
                }
                finally
                {
                    QueuedOpen = false;
                }
            }
            catch (Exception e)
            {
                UIControl.m_SwapOptionVisible.Value = true;
                UIControl.m_PlaceOptionVisible.Value = true;
                throw e;
            }
        }

        public static IEnumerator<SnapshotLiveData> IterateFolder(string folderName)
        {
            Insure();
            if (!ManSnapshots.inst.SupportsFolders())
                yield break;
            SnapshotFolderLiveData folder = UIControl.m_SnapshotFolderCache.Find(x => x.Name == folderName);
            if (!folder.Name.NullOrEmpty())
            {
                foreach (var item in folder.Snapshots)
                {
                    yield return item;
                }
            }
        }

        public static void AddTechToFolder(string techName, string folderName)
        {
            Insure();
            if (!ManSnapshots.inst.SupportsFolders())
                return;
            SnapshotLiveData tech = UIControl.m_Snapshots.List.FirstOrDefault(x => x.m_Loaded && x.m_Snapshot.m_Name.Value == techName);
            if (tech != null && UIControl.m_SnapshotFolderCache.Exists(x => x.Name == folderName))
                ManSnapshots.inst.SetFolder(tech, folderName);
        }
    }


    internal class TechSelectorExtender
    {
        internal static class VMSnapshotPanelPatches
        {
            internal static Type target = typeof(VMSnapshotPanel);
            /*
            [HarmonyPatch(new Type[] { typeof(string) })]
            private static bool SetSelectedSnapshotFolder_Prefix(VMSnapshotPanel __instance)
            {
                if (!TechLoaderExt.QueuedOpen && TechLoaderExt.OnFolderSelect())
                    return false;
                return true;
            }*/
            private static bool Place_Prefix(VMSnapshotPanel __instance)
            {
                if (!TechLoaderExt.QueuedOpen && TechLoaderExt.OnSnapPlacement())
                    return false;
                return true;
            }
            private static bool UpdateWarnings_Prefix(VMSnapshotPanel __instance)
            {
                if (TechLoaderExt.IsSelectingTech)
                {
                    TechLoaderExt.WarnInvalidTechData();
                    return false;
                }
                else if (TechLoaderExt.IsSelectingFolder)
                {
                    TechLoaderExt.WarnNoSpawnTechsInFolderMode();
                    return false;
                }
                return true;
            }
        }
    }
}
