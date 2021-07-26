﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using UnityEngine;
using TAC_AI.AI.Enemy;
using TAC_AI.AI;

namespace TAC_AI.Templates
{
    public class RawTechExporter : MonoBehaviour
    {
        public static GameObject inst;
        public static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 100);   // the "window"
        public static bool isOpen;

        public static string DLLDirectory;
        public static string BaseDirectory;
        public static string RawTechsDirectory;

        // GUI
        public static void Initiate()
        {
            inst = new GameObject();
            inst.AddComponent<RawTechExporter>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIRawDisplay>();
            GUIWindow.SetActive(false);
            SetupWorkingDirectories();
        }
        internal class GUIRawDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isOpen)
                {
                    HotWindow = GUI.Window(846321, HotWindow, GUIHandler, "<b>Save Current Tech</b>");
                }
            }
        }
        public int TimeStep = 0;
        public void Update()
        {
            CheckKeyCombo();
            if (TimeStep > 30)
            {
                if (Singleton.Manager<ManPauseGame>.inst.IsPaused)
                {
                    LaunchSubMenu();
                }
                else
                {
                    CloseSubMenu();
                }
                TimeStep = 0;
            }
            TimeStep++;
        }
        public void CheckKeyCombo()
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftAlt))
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    float timeDelay = Time.time;
                    Debug.Log("TACtical_AI: Reloading All Raw Enemy Techs!");
                    TempManager.ValidateAndAddAllExternalTechs();
                    timeDelay = Time.time - timeDelay;
                    Debug.Log("TACtical_AI: Done in " + timeDelay + " seconds");
                }
            }
        }

        private static void GUIHandler(int ID)
        {
            if (GUI.Button(new Rect(20, 40, 160, 40), "<b>SAVE RAW</b>"))
            {
                SaveTechToRawJSON(Singleton.playerTank);
            }
            GUI.DragWindow();
        }
        public static void LaunchSubMenu()
        {
            if (Singleton.playerTank.IsNull() || !KickStart.EnableBetterAI)
            {
                //Debug.Log("TACtical_AI: TANK IS NULL!");
                CloseSubMenu();
                return;
            }
            //Debug.Log("TACtical_AI: Opened RawJSON menu");
            isOpen = true;
            GUIWindow.SetActive(true);
        }
        public static void CloseSubMenu()
        {
            if (isOpen)
            {
                isOpen = false;
                GUIWindow.SetActive(false);
                //Debug.Log("TACtical_AI: Closed RawJSON menu");
            }
        }


        // Setup
        public static void SetupWorkingDirectories()
        {
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            di = di.Parent; // off of this DLL
            DLLDirectory = di.ToString();
            di = di.Parent; // out of the DLL folder
            di = di.Parent; // out of QMods
            BaseDirectory = di.ToString();
            RawTechsDirectory = di.ToString() + "\\Raw Techs";
            Debug.Log("TACtical_AI: DLL folder is at: " + DLLDirectory);
            Debug.Log("TACtical_AI: Raw Techs is at: " + RawTechsDirectory);
            ValidateEnemyFolder();
        }


        // Operations
        public static List<BaseTemplate> LoadAllEnemyTechs()
        {
            List<string> names = GetTechNameList();
            List<BaseTemplate> temps = new List<BaseTemplate>();
            foreach (string name in names)
            {

                BuilderExternal ext = LoadEnemyTech(name);
                BaseTemplate temp = new BaseTemplate();

                temp.techName = ext.Name;
                temp.savedTech = ext.Blueprint;
                temp.startingFunds = 0;
                FactionSubTypes MainCorp = Singleton.Manager<ManSpawn>.inst.GetCorporation(AIERepair.JSONToFirstBlock(ext.Blueprint));
                temp.purposes = GetHandler(ext.Blueprint, MainCorp, out BaseTerrain terra, out int minCorpGrade);
                temp.IntendedGrade = minCorpGrade;
                temp.faction = MainCorp;
                temp.terrain = terra;

                temps.Add(temp);
                Debug.Log("TACtical_AI: Deployed " + name + " as an enemy tech.");
            }
            return temps;
        }
        internal static List<string> GetTechNameList(string altDirectoryFromBaseDirectory = null)
        {
            string search;
            if (altDirectoryFromBaseDirectory == null)
                search = RawTechsDirectory + "\\Enemies";
            else
                search = BaseDirectory + "\\" + altDirectoryFromBaseDirectory;
            List<string> toClean = Directory.GetFiles(search).ToList();
            List<string> Cleaned = new List<string>();
            foreach (string cleaning in toClean)
            {
                if (!GetNameJSON(cleaning, out string output))
                    continue;
                Cleaned.Add(output);
            }
            return Cleaned;
        }
        private static bool GetNameJSON(string FolderDirectory, out string output)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == '\\')
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }
            if (!final.ToString().Contains(".JSON"))
            {
                output = null;
                return false;
            }
            final.Remove(final.Length - 5, 5);// remove ".JSON"
            output = final.ToString();
            return true;
        }
        private static FieldInfo forceVal = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
        public static List<BasePurpose> GetHandler(string blueprint, FactionSubTypes factionType, out BaseTerrain terra, out int minCorpGrade)
        {
            List<TankBlock> blocs = new List<TankBlock>();
            List<BlockMemory> mems = AIERepair.DesignMemory.JSONToTechExternal(blueprint);
            foreach (BlockMemory mem in mems)
            {
                blocs.Add(Singleton.Manager<ManSpawn>.inst.GetBlockPrefab((BlockTypes)Enum.Parse(typeof(BlockTypes), mem.t)));
            };

            bool isFlying = false;
            bool isFlyingDirectionForwards = true;

            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            int FoilCount = 0;
            int MovingFoilCount = 0;

            int modCollectCount = 0;
            int modEXPLODECount = 0;
            int modBoostCount = 0;
            int modHoverCount = 0;
            int modGyroCount = 0;
            int modWheelCount = 0;
            int modAGCount = 0;
            int modGunCount = 0;
            int modDrillCount = 0;
            minCorpGrade = 0;
            foreach (BlockMemory mem in mems)
            {
                TankBlock bloc = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab((BlockTypes)Enum.Parse(typeof(BlockTypes), mem.t));
                if (bloc.GetComponent<ModuleItemHolder>())
                    modCollectCount++;
                if (bloc.GetComponent<ModuleDetachableLink>())
                    modEXPLODECount++;
                if (bloc.GetComponent<ModuleBooster>())
                {
                    var module = bloc.GetComponent<ModuleBooster>();
                    List<FanJet> jets = module.GetComponentsInChildren<FanJet>().ToList();
                    foreach (FanJet jet in jets)
                    {
                        if (jet.spinDelta <= 10)
                        {
                            Quaternion quat = new OrthoRotation(mem.r);
                            biasDirection -= quat * (jet.EffectorForwards * jet.force);
                        }
                    }
                    List<BoosterJet> boosts = module.GetComponentsInChildren<BoosterJet>().ToList();
                    foreach (BoosterJet boost in boosts)
                    {
                        //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                        boostBiasDirection -=  boost.LocalBoostDirection * (float)forceVal.GetValue(boost);
                    }
                    modBoostCount++;
                }
                if (bloc.GetComponent<ModuleHover>())
                    modHoverCount++;
                if (bloc.GetComponent<ModuleGyro>())
                    modGyroCount++;
                if (bloc.GetComponent<ModuleWheels>())
                    modWheelCount++;
                if (bloc.GetComponent<ModuleAntiGravityEngine>())
                    modAGCount++;
                if (bloc.GetComponent<ModuleWeaponGun>())
                    modGunCount++;
                if (bloc.GetComponent<ModuleDrill>())
                    modDrillCount++;
                if (Singleton.Manager<ManSpawn>.inst.GetCorporation((BlockTypes)Enum.Parse(typeof(BlockTypes), mem.t)) == factionType)
                {
                    if (bloc.m_Tier > minCorpGrade)
                        minCorpGrade = bloc.m_Tier;
                }
                if (bloc.GetComponent<ModuleWing>())
                { 
                    //Get the slowest spooling one
                    List<ModuleWing.Aerofoil> foils = bloc.GetComponent<ModuleWing>().m_Aerofoils.ToList();
                    FoilCount += foils.Count();
                    foreach (ModuleWing.Aerofoil Afoil in foils)
                    {
                        if (Afoil.flapAngleRangeActual > 0 && Afoil.flapTurnSpeed > 0)
                            MovingFoilCount++;
                    }
                }
            }

            List<BasePurpose> purposes = new List<BasePurpose>();
            if (modEXPLODECount > 0)
                purposes.Add(BasePurpose.TechProduction);
            if (modCollectCount > 0)
                purposes.Add(BasePurpose.Harvesting);

            boostBiasDirection.Normalize();
            biasDirection.Normalize();

            if (biasDirection == Vector3.zero && boostBiasDirection != Vector3.zero)
            {
                isFlying = true;
                if (boostBiasDirection.y > 0.6)
                    isFlyingDirectionForwards = false;
            }
            else if (biasDirection != Vector3.zero)
            {
                isFlying = true;
                if (biasDirection.y > 0.6)
                    isFlyingDirectionForwards = false;
            }

            if (Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(AIERepair.JSONToFirstBlock(blueprint)).GetComponent<ModuleAnchor>())
            {
                terra = BaseTerrain.Land;
                return purposes;
            }
            else if (modBoostCount > 2 && (modHoverCount > 2 || modAGCount > 0))
            {   //Starship
                terra = BaseTerrain.Space;
                purposes.Add(BasePurpose.NotABase);
                return purposes;
            }
            else if (MovingFoilCount > 4 && isFlying && isFlyingDirectionForwards)
            {   // Airplane
                terra = BaseTerrain.Air;
                purposes.Add(BasePurpose.NotABase);
                return purposes;
            }
            else if (modGyroCount > 0 && isFlying && !isFlyingDirectionForwards)
            {   // Chopper
                terra = BaseTerrain.Air;
                purposes.Add(BasePurpose.NotABase);
                return purposes;
            }
            else if (KickStart.isWaterModPresent && FoilCount > 0 && modGyroCount > 0 && modBoostCount > 0 && (modWheelCount < 4 || modHoverCount > 1))
            {   // Naval
                terra = BaseTerrain.Sea;
                purposes.Add(BasePurpose.NotABase);
                return purposes;
            }
            else if (modGunCount < 2 && modDrillCount < 2 && modBoostCount > 0)
            {   // Melee
                terra = BaseTerrain.AnyNonSea;
                purposes.Add(BasePurpose.NotABase);
                return purposes;
            }
            terra = BaseTerrain.Land;
            purposes.Add(BasePurpose.NotABase);
            return purposes;
        }
        private static void ValidateEnemyFolder()
        {
            string destination = RawTechsDirectory + "\\Enemies";
            if (!Directory.Exists(RawTechsDirectory))
            {
                Debug.Log("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    Debug.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                Debug.Log("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    Debug.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
        }


        // JSON Handlers
        public static void SaveTechToRawJSON(Tank tank)
        {
            BuilderExternal builder = new BuilderExternal();
            builder.Name = tank.name;
            builder.Faction = tank.GetMainCorp();
            builder.Blueprint = AI.AIERepair.DesignMemory.TechToJSONExternal(tank);
            builder.InfBlocks = false;
            builder.NonAggressive = false;
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveTechToFile(tank.name, builderJSON);
        }
        public static BuilderExternal LoadTechFromRawJSON(string TechName, string altFolderName = "")
        {
            string loaded = LoadTechFromFile(TechName, altFolderName);
            return JsonUtility.FromJson<BuilderExternal>(loaded);
        }
        internal static BuilderExternal LoadEnemyTech(string TechName)
        {
            string loaded = LoadEnemyTechFromFile(TechName);
            return JsonUtility.FromJson<BuilderExternal>(loaded);
        }


        // Loaders
        private static void SaveTechToFile(string TechName, string RawTechJSON)
        {
            if (!Directory.Exists(RawTechsDirectory))
            {
                Debug.Log("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    Debug.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(RawTechsDirectory + "\\" + TechName + ".JSON", RawTechJSON);
                Debug.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static string LoadTechFromFile(string TechName, string altFolderName)
        {
            string destination;
            if (altFolderName == "")
                destination = RawTechsDirectory;
            else
                destination = BaseDirectory + "\\" + altFolderName;
            try
            {
                string output = File.ReadAllText(destination + "\\" + TechName + ".JSON");
                Debug.Log("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                return output;
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");

                Debug.Log("TACtical_AI: Attempted directory - |" + destination + "\\" + TechName + ".JSON");
                return null;
            }
        }
        private static void SaveEnemyTechToFile(string TechName, string RawBaseTechJSON)
        {
            string destination = RawTechsDirectory + "\\Enemies";
            if (!Directory.Exists(RawTechsDirectory))
            {
                Debug.Log("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    Debug.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                Debug.Log("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    Debug.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(destination + "\\" + TechName + ".JSON", RawBaseTechJSON);
                Debug.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static string LoadEnemyTechFromFile(string TechName)
        {
            string destination = RawTechsDirectory + "\\Enemies";
            if (!Directory.Exists(RawTechsDirectory))
            {
                Debug.Log("TACtical_AI: Generating Raw Techs folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory);
                    Debug.Log("TACtical_AI: Made new Raw Techs folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Raw Techs folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }

            }
            if (!Directory.Exists(destination))
            {
                Debug.Log("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    Debug.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }

            }
            try
            {
                string output = File.ReadAllText(destination + "\\" + TechName + ".JSON");
                Debug.Log("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                return output;
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return null;
            }
        }
    }
}
