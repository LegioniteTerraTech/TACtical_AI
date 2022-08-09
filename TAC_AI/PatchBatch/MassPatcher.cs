using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace TAC_AI
{
    internal class MassPatcher
    {
        internal static string modName => KickStart.ModName;
        internal static Harmony harmonyInst => KickStart.harmonyInstance;
        internal static string harmonyID => harmonyInst.Id;
        internal static bool IsUnstable = false;

        public static void CheckIfUnstable()
        {
            IsUnstable = SKU.DisplayVersion.Count(x => x == '.') > 2;
            Debug.Log(modName + ": Is " + SKU.DisplayVersion + " an Unstable? - " + IsUnstable);
        }

        internal static bool MassPatchAll()
        {
            try
            {
                CheckIfUnstable();
                MassPatchAllWithin(typeof(GlobalPatches));
                return true;
            }
            catch (Exception e)
            {
                Debug.Log(modName + ": FAILED ON ALL PATCH ATTEMPTS - CASCADE FAILIURE " + e);
            }
            return false;
        }
        internal static bool MassUnPatchAll()
        {
            try
            {
                MassUnPatchAllWithin(typeof(GlobalPatches));

                return true;
            }
            catch (Exception e)
            {
                Debug.Log(modName + ": FAILED ON ALL UN-PATCH ATTEMPTS - CASCADE FAILIURE " + e);
            }
            return false;
        }

        internal static bool MassPatchAllWithin(Type ToPatch)
        {
            try
            {
                Type[] types = ToPatch.GetNestedTypes(BindingFlags.Static | BindingFlags.NonPublic);
                if (types == null)
                {
                    Debug.Log(modName + ": FAILED TO patch " + ToPatch.Name + " - There's no nested classes?");
                    return false;
                }
                foreach (var typeCase in types)
                {
                    try
                    {
                        Type patcherType;
                        try
                        {
                            patcherType = (Type)typeCase.GetField("target", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                        }
                        catch
                        {
                            Debug.Log(modName + ": FAILED TO patch " + typeCase.Name + " of " + ToPatch.Name + " - There must be a declared target type in a field \"target\"");
                            continue;
                        }
                        MethodInfo[] methods = typeCase.GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
                        if (methods == null)
                        {
                            Debug.Log(modName + ": FAILED TO patch " + typeCase.Name + " of " + ToPatch.Name + " - There are no methods to patch?");
                            continue;
                        }
                        //Debug.Log("MethodCount: " + methods.Length);
                        Dictionary<string, MassPatcherTemplate> methodsToPatch = new Dictionary<string, MassPatcherTemplate>();
                        foreach (var item in methods)
                        {
                            int underscore = item.Name.LastIndexOf('_');
                            if (underscore == -1)
                            {
                                //Debug.Log("No Underscore");
                                continue;
                            }
                            bool StableOnly = item.Name.EndsWith("1");
                            bool UnstableOnly = item.Name.EndsWith("0");
                            string nameNoDivider = UnstableOnly || StableOnly ? item.Name.Substring(0, item.Name.Length - 1) : item.Name;
                            string patcherMethod = nameNoDivider.Substring(0, underscore);
                            string patchingExecution = nameNoDivider.Substring(underscore + 1, nameNoDivider.Length - 1 - underscore);
                            if (!methodsToPatch.TryGetValue(patcherMethod, out MassPatcherTemplate MPT))
                            {
                                //Debug.Log("Patching " + patcherMethod);
                                MPT = new MassPatcherTemplate
                                {
                                    fullName = item.Name,
                                };
                                methodsToPatch.Add(patcherMethod, MPT);
                            }
                            //Debug.Log("patchingExecution " + patchingExecution);
                            if (UnstableOnly)
                            {   // It's clearly an unstable handler
                                if (IsUnstable)
                                {
                                    switch (patchingExecution)
                                    {
                                        case "Prefix":
                                            MPT.prefix = new HarmonyMethod(AccessTools.Method(typeCase, item.Name));
                                            break;
                                        case "Postfix":
                                            MPT.postfix = new HarmonyMethod(AccessTools.Method(typeCase, item.Name));
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                            else if (StableOnly)
                            {
                                if (!IsUnstable)
                                {
                                    switch (patchingExecution)
                                    {
                                        case "Prefix":
                                            MPT.prefix = new HarmonyMethod(AccessTools.Method(typeCase, item.Name));
                                            break;
                                        case "Postfix":
                                            MPT.postfix = new HarmonyMethod(AccessTools.Method(typeCase, item.Name));
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                switch (patchingExecution)
                                {
                                    case "Prefix":
                                        if (MPT.prefix == null)
                                            MPT.prefix = new HarmonyMethod(AccessTools.Method(typeCase, item.Name));
                                        break;
                                    case "Postfix":
                                        if (MPT.postfix == null)
                                            MPT.postfix = new HarmonyMethod(AccessTools.Method(typeCase, item.Name));
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }

                        foreach (var item in methodsToPatch)
                        {
                            try
                            {
                                if (item.Value.prefix != null || item.Value.postfix != null)
                                {
                                    MethodInfo methodCase = AccessTools.Method(patcherType, item.Key);
                                    harmonyInst.Patch(methodCase, item.Value.prefix, item.Value.postfix);
                                    Debug.Log(modName + ": (" + item.Value.fullName + ") Patched " + item.Key + " of " + ToPatch.Name);//+ "  prefix: " + (item.Value.prefix != null) + "  postfix: " + (item.Value.postfix != null)
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.Log(modName + ": (" + item.Value.fullName + ") Failure on patch of " + ToPatch.Name + " in type - " + typeCase.Name + " - " + e.Message);
                                return false;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log(modName + ": Failed to handle patch of " + ToPatch.Name + " in type - " + typeCase.Name + " - " + e.Message);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(modName + ": FAILED TO patch " + ToPatch.Name + " - " + e);
            }
            Debug.Log(modName + ": Mass patched " + ToPatch.Name);
            return true;
        }
        internal static bool MassUnPatchAllWithin(Type ToPatch)
        {
            try
            {
                Type[] types = ToPatch.GetNestedTypes(BindingFlags.Static | BindingFlags.NonPublic);
                if (types == null)
                {
                    Debug.Log(modName + ": FAILED TO patch " + ToPatch.Name + " - There's no nested classes?");
                    return false;
                }
                foreach (var typeCase in types)
                {
                    try
                    {
                        Type patcherType;
                        try
                        {
                            patcherType = (Type)typeCase.GetField("target", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                        }
                        catch
                        {
                            Debug.Log(modName + ": FAILED TO un-patch " + typeCase.Name + " of " + ToPatch.Name + " - There must be a declared target type in a field \"target\"");
                            continue;
                        }
                        MethodInfo[] methods = typeCase.GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
                        if (methods == null)
                        {
                            Debug.Log(modName + ": FAILED TO un-patch " + typeCase.Name + " of " + ToPatch.Name + " - There are no methods to patch?");
                            continue;
                        }
                        List<string> methodsToUnpatch = new List<string>();
                        foreach (var item in methods)
                        {
                            int underscore = item.Name.LastIndexOf('_');
                            if (underscore == -1)
                                continue;
                            bool divider = item.Name.EndsWith("0");
                            string nameNoDivider = divider ? item.Name.Substring(0, item.Name.Length - 1) : item.Name;
                            string patcherMethod = nameNoDivider.Substring(0, underscore);
                            string patchingExecution = nameNoDivider.Substring(underscore + 1, nameNoDivider.Length - 1 - underscore);
                            if (!methodsToUnpatch.Contains(patcherMethod))
                            {
                                methodsToUnpatch.Add(patcherMethod);
                            }
                        }

                        foreach (var item in methodsToUnpatch)
                        {
                            MethodInfo methodCase = AccessTools.Method(patcherType, item);
                            harmonyInst.Unpatch(methodCase, HarmonyPatchType.All, harmonyID);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log(modName + ": Failed to handle un-patch of " + ToPatch.Name + " in type - " + typeCase.Name + " - " + e.Message);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(modName + ": FAILED TO un-patch " + ToPatch.Name + " - " + e);
            }
            Debug.Log(modName + ": Mass un-patched " + ToPatch.Name);
            return true;
        }

        internal class MassPatcherTemplate
        {
            internal string fullName = null;
            internal HarmonyMethod prefix = null;
            internal HarmonyMethod postfix = null;
        }
    }
}
