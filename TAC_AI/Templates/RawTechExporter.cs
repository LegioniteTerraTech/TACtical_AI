using System;
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
    public class AIBookmarker : MonoBehaviour
    {   // External AI-setting interface - used to set Tech AI state externally
        public EnemyHandling commander = EnemyHandling.Wheeled;
        public EnemyAttack attack = EnemyAttack.Circle;
        public EnemyAttitude attitude = EnemyAttitude.Default;
        public EnemySmarts smarts = EnemySmarts.Default;
        public EnemyBolts bolts = EnemyBolts.Default;
    }

    /// <summary>
    /// Don't try bothering with anything sneaky with this - it's built against illegal blocks and block rotations.
    /// </summary>
    public class BuilderExternal
    {   // External builder interface - use to save Techs externally
        public string Name = "unset";
        public string Blueprint;
        public bool InfBlocks;
        public bool IsAnchored;
        public FactionTypesExt Faction;
        public bool NonAggressive = false;
        public bool Eradicator = false;
        public int Cost = 0;
    }

    public class RawTechExporter : MonoBehaviour
    {
        public static GameObject inst;
        public static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        public static bool isOpen;
        public static bool pendingInGameReload;
        public static string up = "\\";

        public static bool ExportJSONInsteadOfRAWTECH = false;

        public static string DLLDirectory;
        public static string BaseDirectory;
        public static string RawTechsDirectory;

        // AI Icons
        public static Dictionary<AIType, Sprite> aiIcons;
        public static Dictionary<EnemySmarts, Sprite> aiIconsEnemy;
        public static AIECore.TankAIHelper lastTech;

        // GUI
        public static void Initiate()
        {
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX)
            {
                up = "/";
            }
            inst = new GameObject();
            inst.AddComponent<RawTechExporter>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIRawDisplay>();
            GUIWindow.SetActive(false);
            SetupWorkingDirectories();
            aiIcons = new Dictionary<AIType, Sprite> {
                {AIType.Escort,  LoadSprite("AI_Tank.png") },
                {AIType.MTMimic,  LoadSprite("AI_MT.png") },
                {AIType.MTSlave,  LoadSprite("AI_MT.png") },
                {AIType.MTTurret,  LoadSprite("AI_MT.png") },
                {AIType.Aegis,  LoadSprite("AI_Aegis.png") },
                {AIType.Assault,  LoadSprite("AI_Assault.png") },
                {AIType.Prospector,  LoadSprite("AI_Harvest.png") },
                {AIType.Aviator,  LoadSprite("AI_Pilot.png") },
                {AIType.Buccaneer,  LoadSprite("AI_Ship.png") },
                {AIType.Astrotech,  LoadSprite("AI_Space.png") },
            };
            aiIconsEnemy = new Dictionary<EnemySmarts, Sprite> {
                {EnemySmarts.Mild,  LoadSprite("E_Mild.png") },
                {EnemySmarts.Meh,  LoadSprite("E_Meh.png") },
                {EnemySmarts.Smrt,  LoadSprite("E_Smrt.png") },
                {EnemySmarts.IntAIligent,  LoadSprite("E_Intel.png") },
            };

#if DEBUG
            ExportJSONInsteadOfRAWTECH = true;
#endif
        }
        internal class GUIRawDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isOpen)
                {
                    HotWindow = GUI.Window(846321, HotWindow, GUIHandler, "<b>RAW Tech Saving</b>");
                }
            }
        }
        public int TimeStep = 0;
        public void Update()
        {
            CheckKeyCombos();
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
        public void CheckKeyCombos()
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
                    if (!SpecialAISpawner.thisActive)
                        pendingInGameReload = true;
                }
                if (Input.GetKeyDown(KeyCode.B))
                {
                    Invoke("DelayedReloadRawTechLoader", 0);
                }
            }
        }
        public void DelayedReloadRawTechLoader()
        {
            float timeDelay = Time.time;
            Debug.Log("TACtical_AI: Rebuilding Raw Tech Loader!");
            AIERepair.ConstructErrorBlocksList();
            TempManager.ValidateAndAddAllExternalTechs();
            timeDelay = Time.time - timeDelay;
            Debug.Log("TACtical_AI: Done in " + timeDelay + " seconds");
        }
        public static void Reload()
        {
            if (pendingInGameReload)
            {
                float timeDelay = Time.time;
                Debug.Log("TACtical_AI: Reloading All Raw Enemy Techs (Ingame)!");
                TempManager.ValidateAndAddAllExternalTechs();
                timeDelay = Time.time - timeDelay;
                Debug.Log("TACtical_AI: Done in " + timeDelay + " seconds");
                pendingInGameReload = false;
            }
        }
        public static void ReloadExternal()
        {
            float timeDelay = Time.time;
            Debug.Log("TACtical_AI: Reloading All Raw Enemy Techs!");
            TempManager.ValidateAndAddAllExternalTechs();
            timeDelay = Time.time - timeDelay;
            Debug.Log("TACtical_AI: Done in " + timeDelay + " seconds");
            pendingInGameReload = false;
        }

        private static void GUIHandler(int ID)
        {
            bool snapsAvail = SnapsAvailable();
            if (GUI.Button(new Rect(20, 30, 160, 40), new GUIContent("<b>SAVE CURRENT</b>", "Save current Tech to the Raw Techs directory")))
            {
                SaveTechToRawJSON(Singleton.playerTank);
            }
            if (GUI.Button(new Rect(20, 70, 160, 40), new GUIContent("<b>+ ENEMY POP</b>", "Save current Tech to Raw Enemies pop in eLocal.")))
            {
                SaveEnemyTechToRawJSON(Singleton.playerTank);
            }
            if (GUI.Button(new Rect(20, 110, 160, 40), new GUIContent("<b>+ ALL SNAPS</b>", snapsAvail ? "Save ALL snapshots to Raw Enemies pop in eBulk." : "Open the snapshots menu at least once first!")))
            {
                SaveEnemyTechsToRawBLK();
            }
            GUI.Label(new Rect(20, 160, 150, 40), GUI.tooltip);
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
            RawTechsDirectory = di.ToString() + up + "Raw Techs";
#if DEBUG
            Debug.Log("TACtical_AI: DLL folder is at: " + DLLDirectory);
            Debug.Log("TACtical_AI: Raw Techs is at: " + RawTechsDirectory);
#endif
            ValidateEnemyFolder();
        }


        // Operations
        public static List<BaseTemplate> LoadAllEnemyTechs()
        {
            List<string> Dirs = GetALLDirectoriesInFolder(RawTechsDirectory + up + "Enemies");
            List<BaseTemplate> temps = new List<BaseTemplate>();
            List<string> names = new List<string>();
            Debug.Log("TACtical_AI: LoadAllEnemyTechs - Total directories found in Enemies Folder: " + Dirs.Count());
            foreach (string Dir in Dirs)
            {
                names = GetTechNameListDir(Dir);
                Debug.Log("TACtical_AI: LoadAllEnemyTechs - Total RAW Techs found in " + GetNameDirectory(Dir) + ": " + names.Count());
                foreach (string name in names)
                {
                    try
                    {
                        BuilderExternal ext = LoadEnemyTech(name, Dir);
                        BaseTemplate temp = new BaseTemplate();

                        temp.techName = ext.Name;
                        temp.savedTech = ext.Blueprint;
                        temp.startingFunds = ValidateCost(ext.Blueprint, ext.Cost);
                        FactionTypesExt MainCorp = KickStart.GetCorpExtended(AIERepair.JSONToFirstBlock(ext.Blueprint));
                        temp.purposes = GetHandler(ext.Blueprint, MainCorp, ext.IsAnchored, out BaseTerrain terra, out int minCorpGrade);
                        temp.IntendedGrade = minCorpGrade;
                        temp.faction = MainCorp;
                        temp.terrain = terra;

                        temps.Add(temp);
                        Debug.Log("TACtical_AI: Deployed " + name + " as an enemy tech, grade " + minCorpGrade + " " + MainCorp.ToString() + ", of BB Cost " + temp.startingFunds + ".");
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: Could not deploy " + name + " as an enemy tech!  Corrupted BuilderExternal(Or tech too small)!!");
                    }
                }
            }
            return temps;
        }
        internal static List<string> GetAllNames(string directory)
        {
            List<string> Cleaned = new List<string>();
            List<string> Dirs = GetALLDirectoriesInFolder(directory);
            Dirs.Add(directory); 
            
            foreach (string Dir in Dirs)
            {
                Cleaned.AddRange(GetTechNameList(Dir));
            }

            return Cleaned;
        }
        internal static List<string> GetTechNameListDir(string directory, bool ExcludeJSON = false)
        {
            List<string> toClean = Directory.GetFiles(directory).ToList();
            List<string> Cleaned = CleanNames(toClean, ExcludeJSON);
            return Cleaned;
        }
        internal static List<string> GetTechNameList(string altDirectoryFromBaseDirectory = null)
        {
            string search;
            List<string> Cleaned;
            if (altDirectoryFromBaseDirectory == null)
            {
                search = RawTechsDirectory + up + "Enemies";
            }
            else
            {
                search = BaseDirectory + up + altDirectoryFromBaseDirectory;
            }
            List<string> toClean = Directory.GetFiles(search).ToList();
            Cleaned = CleanNames(toClean, false);
            return Cleaned;
        }
        internal static int GetTechCounts()
        {
            List<string> Dirs = GetALLDirectoriesInFolder(RawTechsDirectory + up + "Enemies");
            int techCount = 0;
            foreach (string Dir in Dirs)
            {
                techCount += GetTechNameListDir(Dir).Count;
            }
            return techCount;
        }

        private static List<string> CleanNames(List<string> FolderDirectories, bool excludeJSON)
        {
            List<string> Cleaned = new List<string>();
            foreach (string cleaning in FolderDirectories)
            {
                if (!GetNameJSON(cleaning, out string output, excludeJSON))
                    continue;
                Cleaned.Add(output);
            }
            return Cleaned;
        }
        private static bool GetNameJSON(string FolderDirectory, out string output, bool excludeJSON)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == up.ToCharArray()[0])
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }
            if (!final.ToString().Contains(".RAWTECH"))
            {
                if (!final.ToString().Contains(".JSON") && !excludeJSON)
                {
                    output = null;
                    return false;
                }
                else
                    final.Remove(final.Length - 5, 5);// remove ".JSON"
            }
            else
                final.Remove(final.Length - 8, 8);// remove ".RAWTECH"
            
            output = final.ToString();
            return true;
        }
        private static FieldInfo forceVal = typeof(BoosterJet).GetField("m_Force", BindingFlags.NonPublic | BindingFlags.Instance);
        public static List<BasePurpose> GetHandler(string blueprint, FactionTypesExt factionType, bool Anchored, out BaseTerrain terra, out int minCorpGrade)
        {
            List<TankBlock> blocs = new List<TankBlock>();
            List<BlockMemory> mems = AIERepair.DesignMemory.JSONToMemoryExternal(blueprint);
            if (mems.Count < 1)
            {
                Debug.Log("TACtical_AI: TECH IS NULL!  SKIPPING!");
                minCorpGrade = 99;
                terra = BaseTerrain.AnyNonSea;
                return new List<BasePurpose>();
            }

            List<BasePurpose> purposes = new List<BasePurpose>();
            //foreach (BlockMemory mem in mems)
            //{
            //    blocs.Add(Singleton.Manager<ManSpawn>.inst.GetBlockPrefab((BlockTypes)Enum.Parse(typeof(BlockTypes), mem.t)));
            //}

            bool isFlying = false;
            bool isFlyingDirectionForwards = true;

            Vector3 biasDirection = Vector3.zero;
            Vector3 boostBiasDirection = Vector3.zero;

            int FoilCount = 0;
            int MovingFoilCount = 0;

            int modControlCount = 0;
            int modCollectCount = 0;
            int modEXPLODECount = 0;
            int modBoostCount = 0;
            int modHoverCount = 0;
            int modGyroCount = 0;
            int modWheelCount = 0;
            int modAGCount = 0;
            int modDangerCount = 0;
            int modGunCount = 0;
            int modDrillCount = 0;
            minCorpGrade = 0;

            BlockUnlockTable blockList = Singleton.Manager<ManLicenses>.inst.GetBlockUnlockTable();
            int gradeM = blockList.GetMaxGrade(KickStart.CorpExtToCorp(factionType));
            //Debug.Log("TACtical_AI: GetHandler - " + Singleton.Manager<ManLicenses>.inst.m_UnlockTable.GetAllBlocksInTier(1, factionType, false).Count());
            foreach (BlockMemory blocRaw in mems)
            {
                BlockTypes type = AIERepair.StringToBlockType(blocRaw.t);
                TankBlock bloc = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(type);
                if (bloc.IsNull())
                    continue;

                if (bloc.GetComponent<ModuleTechController>())
                    modControlCount++;
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
                            Quaternion quat = new OrthoRotation(blocRaw.r);
                            biasDirection -= quat * (jet.EffectorForwards * jet.force);
                        }
                    }
                    List<BoosterJet> boosts = module.GetComponentsInChildren<BoosterJet>().ToList();
                    foreach (BoosterJet boost in boosts)
                    {
                        //We have to get the total thrust in here accounted for as well because the only way we CAN boost is ALL boosters firing!
                        boostBiasDirection -= boost.LocalBoostDirection * (float)forceVal.GetValue(boost);
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
                if (bloc.GetComponent<ModuleWeapon>())
                    modDangerCount++;
                if (bloc.GetComponent<ModuleWeaponGun>())
                    modGunCount++;
                if (bloc.GetComponent<ModuleDrill>())
                    modDrillCount++;

                try
                {
                    /*
                    BlockUnlockTable blockList = Singleton.Manager<ManLicenses>.inst.GetBlockUnlockTable();
                    int gradeM = blockList.GetMaxGrade(factionType);
                    for (int step = 0; step > gradeM; step++)
                    {
                        if (blockList.GetInitialBlocksInTier(step, factionType).Contains(type))
                        {
                            if (step > minCorpGrade)
                                minCorpGrade = step;
                            break;
                        }
                    }*/

                    int tier = Singleton.Manager<ManLicenses>.inst.m_UnlockTable.GetBlockTier(type, true);
                    if (KickStart.GetCorpExtended(type) == factionType)
                    {
                        if (tier > minCorpGrade)
                        {
                            minCorpGrade = tier;
                        }
                    }
                    else
                    {
                        if (tier - 1 > minCorpGrade)
                        {
                            if (tier > gradeM)
                                minCorpGrade = gradeM - 1;
                            else
                                minCorpGrade = tier - 1;
                        }
                    }
                }
                catch
                {
                    //Debug.Log("TACtical_AI: GetHandler - error");
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
                blocs.Add(bloc);
            }

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

            if (modDangerCount > modControlCount)
                purposes.Add(BasePurpose.NoWeapons);

            terra = BaseTerrain.Land;
            if (Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(AIERepair.StringToBlockType(mems.ElementAt(0).t)).GetComponent<ModuleAnchor>())
            {
                Debug.Log("TACtical_AI: Purposes: Anchored (static)");
                return purposes;
            }
            else if (modBoostCount > 2 && (modHoverCount > 2 || modAGCount > 0))
            {   //Starship
                terra = BaseTerrain.Space;
            }
            else if (MovingFoilCount > 4 && isFlying && isFlyingDirectionForwards)
            {   // Airplane
                terra = BaseTerrain.Air;
            }
            else if (modGyroCount > 0 && isFlying && !isFlyingDirectionForwards)
            {   // Chopper
                terra = BaseTerrain.Air;
            }
            else if (KickStart.isWaterModPresent && FoilCount > 0 && modGyroCount > 0 && modBoostCount > 0 && (modWheelCount < 4 || modHoverCount > 1))
            {   // Naval
                terra = BaseTerrain.Sea;
            }
            else if (modGunCount < 2 && modDrillCount < 2 && modBoostCount > 0)
            {   // Melee
                terra = BaseTerrain.AnyNonSea;
            }

            if (!Anchored)
                purposes.Add(BasePurpose.NotStationary);

            if (mems.Count >= KickStart.LethalTechSize || modGunCount > 48 || modHoverCount > 18)
            {
                purposes.Add(BasePurpose.NANI);
            }

            string purposesList = "None.";
            if (purposes.Count > 0)
            {
                purposesList = "";
                foreach (BasePurpose purp in purposes)
                {
                    purposesList += purp.ToString() + "|";
                }
            }

            Debug.Log("TACtical_AI: Terrain: " + terra.ToString() + " - Purposes: " + purposesList);

            return purposes;
        }
        public static int ValidateCost(string blueprint, int ExistingCost)
        {
            if (ExistingCost <= 0)
                ExistingCost = GetBBCost(AIERepair.DesignMemory.JSONToMemoryExternal(blueprint));
            if (ExistingCost <= 0)
            {
                Debug.Log("TACtical_AI: ValidateCost - Invalid tech cost encountered ~ could not handle!");
                ExistingCost = 0;
            }

            return ExistingCost;
        }

        private static void ValidateEnemyFolder()
        {
            string destination = RawTechsDirectory + up + "Enemies";
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

        // Snapshot to RawTech
        public static bool SnapsAvailable()
        {
            try
            {
                List<Binding.SnapshotLiveData> Snaps = ManSnapshots.inst.m_Snapshots.ToList();
                if (Snaps.Count > 0)
                    return true;
            }
            catch
            {
                // error
                return false;
            }
            // No snaps loaded.
            return false;
        }
        public static void SaveEnemyTechsToRawBLK()
        {
            List<Binding.SnapshotLiveData> Snaps = ManSnapshots.inst.m_Snapshots.ToList();
            if (Snaps.Count == 0)
                return;
            foreach (Binding.SnapshotLiveData snap in Snaps)
            {
                try
                {
                    SaveEnemyTechToRawJSONBLK(snap.m_Snapshot.techData);
                }
                catch
                {
                    Debug.Log("TACtical_AI: SaveEnemyTechsToRawBLK - Export Failure reported!");
                    try
                    {
                        Debug.Log("TACtical_AI: Error tech name " + snap.m_Snapshot.techData.Name);
                    }
                    catch
                    {
                        Debug.Log("TACtical_AI: SaveEnemyTechsToRawBLK - COULD NOT FETCH TECHDATA!!!");
                    }
                }
            }
            ReloadExternal();
        }


        // JSON Handlers
        public static void SaveTechToRawJSON(Tank tank)
        {
            BuilderExternal builder = new BuilderExternal();
            builder.Name = tank.name;
            builder.Faction = GetTopCorp(tank);
            builder.Blueprint = AIERepair.DesignMemory.TechToJSONExternal(tank);
            builder.InfBlocks = false;
            builder.IsAnchored = tank.IsAnchored;
            builder.NonAggressive = !IsLethal(tank);
            builder.Eradicator = tank.blockman.IterateBlocks().Count() >= KickStart.LethalTechSize || tank.blockman.IterateBlockComponents<ModuleWeaponGun>().Count() > 48 || tank.blockman.IterateBlockComponents<ModuleHover>().Count() > 18;
            builder.Cost = GetBBCost(tank);
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveTechToFile(tank.name, builderJSON);
        }
        public static void SaveEnemyTechToRawJSON(Tank tank)
        {
            BuilderExternal builder = new BuilderExternal();
            builder.Name = tank.name;
            builder.Faction = GetTopCorp(tank);
            builder.Blueprint = AIERepair.DesignMemory.TechToJSONExternal(tank);
            builder.InfBlocks = false;
            builder.IsAnchored = tank.IsAnchored;
            builder.NonAggressive = !IsLethal(tank);
            builder.Eradicator = tank.blockman.IterateBlocks().Count() >= KickStart.LethalTechSize || tank.blockman.IterateBlockComponents<ModuleWeaponGun>().Count() > 48 || tank.blockman.IterateBlockComponents<ModuleHover>().Count() > 18;
            builder.Cost = GetBBCost(tank);
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveEnemyTechToFile(tank.name, builderJSON);
            ReloadExternal();
        }
        public static void SaveEnemyTechToRawJSONBLK(TechData tank)
        {
            BuilderExternal builder = new BuilderExternal();
            string bluep = BlockSpecToJSONExternal(tank.m_BlockSpecs, out int blockCount, out bool lethal, out int hoveCount, out int weapGCount);
            builder.Name = tank.Name;
            builder.Faction = tank.GetMainCorpExt();
            builder.Blueprint = bluep;
            builder.InfBlocks = false;
            builder.IsAnchored = tank.CheckIsAnchored();
            builder.NonAggressive = lethal;
            builder.Eradicator = blockCount >= KickStart.LethalTechSize || weapGCount > 48 || hoveCount > 18;
            builder.Cost = tank.GetValue();
            string builderJSON = JsonUtility.ToJson(builder, true);
            SaveEnemyTechToFileBLK(tank.Name, builderJSON);
        }
        public static BuilderExternal LoadTechFromRawJSON(string TechName, string altFolderName = "")
        {
            string loaded = LoadTechFromFile(TechName, altFolderName);
            return JsonUtility.FromJson<BuilderExternal>(loaded);
        }
        internal static BuilderExternal LoadEnemyTech(string TechName, string altDirect = "")
        {
            string loaded = LoadEnemyTechFromFile(TechName, altDirect);
            return JsonUtility.FromJson<BuilderExternal>(loaded);
        }
        internal static int GetBBCost(Tank tank)
        {
            int output = 0;
            foreach (TankBlock block in tank.blockman.IterateBlocks())
            {
                output += Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice(block.BlockType);
            }
            return output;
        }
        internal static int GetBBCost(List<BlockMemory> mem)
        {
            int output = 0;
            foreach (BlockMemory block in mem)
            {
                try
                {
                    output += Singleton.Manager<RecipeManager>.inst.GetBlockBuyPrice((BlockTypes)Enum.Parse(typeof(BlockTypes), block.t));
                }
                catch { }
            }
            return output;
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
                if (ExportJSONInsteadOfRAWTECH)
                {
                    File.WriteAllText(RawTechsDirectory + up + TechName + ".JSON", RawTechJSON);
                    Debug.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
                }
                else
                {
                    File.WriteAllText(RawTechsDirectory + up + TechName + ".RAWTECH", RawTechJSON);
                    Debug.Log("TACtical_AI: Saved RawTech.RAWTECH for " + TechName + " successfully.");
                }
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
                destination = BaseDirectory + up + altFolderName;
            try
            {
                string output;
                if (File.Exists(destination + up + TechName + ".JSON"))
                {
                    output = File.ReadAllText(destination + up + TechName + ".JSON");
                    Debug.Log("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                }
                else
                {
                    output = File.ReadAllText(destination + up + TechName + ".RAWTECH");
                    Debug.Log("TACtical_AI: Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                }
                return output;
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");

                Debug.Log("TACtical_AI: Attempted directory - |" + destination + up + TechName + ".JSON");
                return null;
            }
        }
        private static void SaveEnemyTechToFile(string TechName, string RawBaseTechJSON)
        {
            string destination = RawTechsDirectory + up + "Enemies" + up + "eLocal";
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
            if (!Directory.Exists(RawTechsDirectory + up + "Enemies"))
            {
                Debug.Log("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory + up + "Enemies");
                    Debug.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                Debug.Log("TACtical_AI: Generating eLocal folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    Debug.Log("TACtical_AI: Made new eLocal folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new eLocal folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(destination + up + TechName + ".JSON", RawBaseTechJSON);
                Debug.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static void SaveEnemyTechToFileBLK(string TechName, string RawBaseTechJSON)
        {
            string destination = RawTechsDirectory + up + "Enemies" + up + "eBulk";
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
            if (!Directory.Exists(RawTechsDirectory + up + "Enemies"))
            {
                Debug.Log("TACtical_AI: Generating Enemies folder.");
                try
                {
                    Directory.CreateDirectory(RawTechsDirectory + up + "Enemies");
                    Debug.Log("TACtical_AI: Made new Enemies folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new Enemies folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            if (!Directory.Exists(destination))
            {
                Debug.Log("TACtical_AI: Generating eBulk folder.");
                try
                {
                    Directory.CreateDirectory(destination);
                    Debug.Log("TACtical_AI: Made new eBulk folder successfully.");
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not create new eBulk folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }

            }
            try
            {
                File.WriteAllText(destination + up + TechName + ".JSON", RawBaseTechJSON);
                Debug.Log("TACtical_AI: Saved RawTech.JSON for " + TechName + " successfully.");
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not edit RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }
        private static string LoadEnemyTechFromFile(string TechName, string AltDirectory = "")
        {
            string destination;
            if (AltDirectory == "")
            {
                destination = RawTechsDirectory + up + "Enemies";
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
                    string output;
                    if (File.Exists(destination + up + TechName + ".JSON"))
                    {
                        output = File.ReadAllText(destination + up + TechName + ".JSON");
                        Debug.Log("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                    }
                    else
                    {
                        output = File.ReadAllText(destination + up + TechName + ".RAWTECH");
                        Debug.Log("TACtical_AI: Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                    }
                    return output;
                }
                catch
                {
                    Debug.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                    return null;
                }
            }
            else
            {
                destination = AltDirectory;
                try
                {
                    string output;
                    if (File.Exists(destination + up + TechName + ".JSON"))
                    {
                        output = File.ReadAllText(destination + up + TechName + ".JSON");
                        Debug.Log("TACtical_AI: Loaded RawTech.JSON for " + TechName + " successfully.");
                    }
                    else
                    {
                        output = File.ReadAllText(destination + up + TechName + ".RAWTECH");
                        Debug.Log("TACtical_AI: Loaded RawTech.RAWTECH for " + TechName + " successfully.");
                    }
                    return output;
                }
                catch { }
                Debug.Log("TACtical_AI: Could not read RawTech.JSON for " + TechName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return null;
            }
        }


        // Utilities
        public static string BlockSpecToJSONExternal(List<TankPreset.BlockSpec> specs, out int blockCount, out bool lethal, out int hoveCount, out int weapGCount)
        {   // Saving a Tech from the BlockMemory
            blockCount = 0;
            int ctrlCount = 0;
            int weapCount = 0;
            weapGCount = 0;
            hoveCount = 0;
            lethal = false;
            if (specs.Count == 0)
                return null;
            List<BlockMemory> mem = new List<BlockMemory>();
            foreach (TankPreset.BlockSpec spec in specs)
            {
                BlockMemory mem1 = new BlockMemory();
                mem1.t = spec.block;
                mem1.p = spec.position;
                mem1.r = new OrthoRotation(spec.orthoRotation).rot;

                TankBlock block = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(spec.GetBlockType());
                if (block.GetComponent<ModuleHover>())
                    hoveCount++;
                if (block.GetComponent<ModuleWeapon>())
                    weapCount++;
                if (block.GetComponent<ModuleWeaponGun>())
                    weapGCount++;
                if (block.GetComponent<ModuleTechController>())
                    ctrlCount++;
                mem.Add(mem1);
            }

            lethal =  weapCount > ctrlCount;
            blockCount = mem.Count;

            StringBuilder JSONTechRAW = new StringBuilder();
            JSONTechRAW.Append(JsonUtility.ToJson(mem.First()));
            for (int step = 1; step < mem.Count; step++)
            {
                JSONTechRAW.Append("|");
                JSONTechRAW.Append(JsonUtility.ToJson(mem.ElementAt(step)));
            }
            string JSONTechRAWout = JSONTechRAW.ToString();
            StringBuilder JSONTech = new StringBuilder();
            foreach (char ch in JSONTechRAWout)
            {
                if (ch == '"')
                {
                    JSONTech.Append(ch);
                }
                else
                    JSONTech.Append(ch);
            }
            //Debug.Log("TACtical_AI: " + JSONTech.ToString());
            return JSONTech.ToString();
        }
        private static Sprite LoadSprite(string pngName)
        {
            string destination = DLLDirectory + up + "AI_Icons" + up + pngName;
            try
            {
                Texture2D tex = FileUtils.LoadTexture(destination);
                Sprite output = Sprite.Create(tex, new Rect(0,0, tex.width, tex.height), Vector2.zero);
                Debug.Log("TACtical_AI: Loaded Icon " + pngName + " successfully.");
                return output;
            }
            catch
            {
                Debug.Log("TACtical_AI: Could not load Icon " + pngName + "!  \n   File is missing!");
                return null;
            }
        }
        private static string GetNameDirectory(string FolderDirectory)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == up.ToCharArray()[0])
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }

            return final.ToString();
        }
        private static string GetDirectoryPathOnly(string FolderDirectory)
        {
            StringBuilder final = new StringBuilder();
            int offsetRemove = 0;
            foreach (char ch in FolderDirectory)
            {
                if (ch == up.ToCharArray()[0])
                {
                    offsetRemove = 1;
                }
                else
                    offsetRemove++;
                final.Append(ch);
            }
            final.Remove(final.Length - offsetRemove, offsetRemove);
            return final.ToString();
        }
        private static List<string> GetALLDirectoriesInFolder(string directory)
        {   // 
            List<string> final = new List<string>();
            List<string> Dirs = Directory.GetDirectories(directory).ToList();
            final.Add(directory);
            foreach (string Dir in Dirs)
            {
                final.Add(Dir);
                final.AddRange(GetALLDirectoriesInFolder(Dir));
            }
            final = final.Distinct().ToList();
            return final;
        }
        private static FactionTypesExt GetTopCorp(Tank tank)
        {   // 
            FactionTypesExt final = tank.GetMainCorpExt();
            if (!(bool)Singleton.Manager<ManLicenses>.inst)
                return final;
            int corps = Enum.GetNames(typeof(FactionTypesExt)).Length;
            int[] corpCounts = new int[corps];

            foreach (TankBlock block in tank.blockman.IterateBlocks())
            {
                corpCounts[(int)KickStart.GetCorpExtended(block.BlockType)]++;
            }
            int blockCounts = 0;
            int bestCorpIndex = 0;
            for (int step = 0; step < corps; step++)
            {
                int num = corpCounts[step];
                if (num > blockCounts)
                {
                    bestCorpIndex = step;
                    blockCounts = num;
                }
            }
            final = (FactionTypesExt)bestCorpIndex;
            return final;
        }
        private static bool IsLethal(Tank tank)
        {   // 
            return tank.blockman.IterateBlockComponents<ModuleWeapon>().Count() > tank.blockman.IterateBlockComponents<ModuleTechController>().Count();
        }
    }
}
