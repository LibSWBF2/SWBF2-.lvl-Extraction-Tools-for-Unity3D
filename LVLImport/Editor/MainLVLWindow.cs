using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using System.Linq;

using UnityEngine;
using UnityEditor;
using LibSWBF2;
using LibSWBF2.Wrappers;
using LibSWBF2.Enums;


public class LVLImportWindow : EditorWindow {

    bool currentlyLoading, startLoading;

    bool startLoadWorlds, startLoadClasses;

    static bool terrainAsMesh = false;
    static bool saveTextures, saveMaterials, saveModels, saveAnims, saveObjects, saveWorld;

    static string matFolder = "Materials";
    static string texFolder = "Textures";
    static string modelsFolder = "Models";
    static string animsFolder = "Animations";
    static string objectsFolder = "Objects";
    static string worldFolder = "World";

    static string savePathPrefix = "Assets/LVLImport/"; 

    Container container;

    static List<string> filesToLoad = new List<string>();
    List<SWBF2Handle> fileHandles = new List<SWBF2Handle>();




    [MenuItem("SWBF2/Import .lvl", false, 10)]
    static void Init()
    {
        LVLImportWindow window = (LVLImportWindow)EditorWindow.GetWindow(typeof(LVLImportWindow));
        window.Show();
    }



    private string AddFileOption(string fileName, bool closeable = false)
    {
        string path = null;
        GUILayout.BeginHorizontal();
        
        string tempPath = EditorGUILayout.TextField("", fileName);

        if (tempPath != null && String.Compare(tempPath,"") != 0 && String.Compare(fileName,tempPath) != 0)
        {
           path = tempPath;
        }

        if (GUILayout.Button("Browse",GUILayout.Width(55)))
        {
            path = EditorUtility.OpenFilePanel("Select lvl file(s)", "", "lvl,zaabin");
            path = String.Compare(path,"") == 0 ? null : path;
        }

        if (closeable)
        {
            if (GUILayout.Button("X",GUILayout.Width(30)))
            {
                path = "removed";
            }
        }

        GUILayout.EndHorizontal();

        return path;
    }


    private void AddSaveOption(string type, ref bool initStatus, ref string initVal)
    {
        EditorGUIUtility.labelWidth = 150;
        GUILayout.BeginHorizontal();
        initStatus = EditorGUILayout.Toggle(new GUIContent("Save " + type, ""), initStatus);

        if (initStatus)
        {
            EditorGUIUtility.labelWidth = 105;
            initVal = EditorGUILayout.TextField(savePathPrefix, initVal,  GUILayout.ExpandWidth(true));
        }
        GUILayout.EndHorizontal();
    }



    private void AddSpaces(int numSpaces = 1)
    {
        for (int i = 0; i < numSpaces; i++)
        {
            EditorGUILayout.Space();
        }
    }


    void Update()
    {
    	Repaint();
    }



    void OnGUI()
    {
        GUI.enabled = !currentlyLoading;

        EditorGUIUtility.labelWidth = 150;
        GUILayout.Label("Import Settings", EditorStyles.boldLabel);

        for (int i = 0; i < filesToLoad.Count; i++)
        {
            string newPath = AddFileOption(filesToLoad[i],true);
            if (String.Compare(newPath,"removed") == 0)
            {
                filesToLoad.RemoveAt(i);
                i--;
            }
            else if (newPath != null)
            {
                filesToLoad[i] = newPath;
            }
        }

        string pathAdded = AddFileOption("Browse for lvl file(s) or enter path");
        if (pathAdded != null)
        {
            filesToLoad.Add(pathAdded);
        }

        AddSpaces(5);

        terrainAsMesh = EditorGUILayout.Toggle(new GUIContent("Import Terrain as Mesh", ""), terrainAsMesh);


        if (saveTextures || saveModels || saveMaterials || saveAnims || saveObjects)
        {
            AddSpaces(5);
            EditorGUIUtility.labelWidth = 150;
            savePathPrefix = EditorGUILayout.TextField("Save Path Prefix", savePathPrefix,  GUILayout.ExpandWidth(true));
            AddSpaces(2);
        }

        if (!savePathPrefix.StartsWith("Assets"))
        {
            savePathPrefix = "Assets/LVLImport";
            Debug.LogError("Save Path Prefix must start with \"Assets/\"!");
        }

        AddSaveOption("Textures", ref saveTextures, ref texFolder);
        AddSaveOption("Materials", ref saveMaterials, ref matFolder);
        AddSaveOption("Models", ref saveModels, ref modelsFolder);
        AddSaveOption("Animations", ref saveAnims, ref animsFolder);
        AddSaveOption("Objects", ref saveObjects, ref objectsFolder);
        AddSaveOption("World", ref saveWorld, ref worldFolder);


        saveTextures = saveMaterials ? true : saveTextures;

        if (saveModels)
        {
            saveTextures = true;
            saveMaterials = true;
        }

        if (saveObjects)
        {
            saveTextures = true;
            saveMaterials = true;
            saveAnims = true;
            saveModels = true;
        }        

        AddSpaces(5);

        WorldLoader.UseHDRP = GUILayout.Toggle(WorldLoader.UseHDRP, "Use HDRP");
        GUILayout.BeginHorizontal();
       
        startLoadWorlds = GUILayout.Button("Import Worlds",GUILayout.Width(100)) ? true : currentlyLoading && startLoadWorlds;
        startLoadClasses = GUILayout.Button("Import Objects",GUILayout.Width(100)) ? true : currentlyLoading && startLoadClasses;
        GUILayout.EndHorizontal();

        GUI.enabled = true;

        startLoading = (startLoadClasses || startLoadWorlds) && !currentlyLoading;

        if (startLoading)
        {
            container = new Container();
            WorldLoader.UseHDRP = true;

            fileHandles = new List<SWBF2Handle>();
            foreach (string path in filesToLoad)
            {
                fileHandles.Add(container.AddLevel(path));
            }

            container.LoadLevels();
            currentlyLoading = true;
            startLoading = false;
        }

        if (currentlyLoading)
        {
            for (int i = 0; i < filesToLoad.Count; i++)
            {
                SWBF2Handle handle = fileHandles[i];
                float progress = container.GetProgress(handle);
                EditorGUI.ProgressBar(new Rect(3, 250 + 30 * i, position.width - 6, 20), progress, filesToLoad[i]);
            }

            if (container.IsDone())
            {
                currentlyLoading = false;

                Loader.ResetAllLoaders();
                Loader.SetGlobalContainer(container);

                WorldLoader.Instance.TerrainAsMesh = terrainAsMesh;

                if (saveTextures){ TextureLoader.Instance.SetSave(savePathPrefix,texFolder); }
                if (saveMaterials) { MaterialLoader.Instance.SetSave(savePathPrefix,matFolder); }
                if (saveModels) { ModelLoader.Instance.SetSave(savePathPrefix,modelsFolder); }
                if (saveAnims) { AnimationLoader.Instance.SetSave(savePathPrefix,animsFolder); }
                if (saveObjects) { ClassLoader.Instance.SetSave(savePathPrefix,objectsFolder); }
                if (saveWorld) { WorldLoader.Instance.SetSave(savePathPrefix, worldFolder); }


                UnityEngine.Vector3 offset = new UnityEngine.Vector3(0,0,0); 


                foreach (SWBF2Handle handle in fileHandles)
                {
                    Level level = container.GetLevel(handle);
                    if (level == null)
                    {
                        continue;
                    }

                    if (startLoadWorlds)
                    {
                        foreach (World world in level.GetWrappers<World>())
                        {
                            WorldLoader.Instance.ImportWorld(world);
                        }
                    }

                    AssetDatabase.Refresh();


                    UnityEngine.Vector3 spawnLoc = new UnityEngine.Vector3(0,0,0); 
                    
                    if (startLoadClasses)
                    {
                        string levelName = level.Name;
                        GameObject root = new GameObject(levelName == null ? "objects" : levelName.Replace(".lvl",""));
                        root.transform.localPosition = offset;

                        List<GameObject> importedObjs = new List<GameObject>();

                        try
                        {
                            //AssetDatabase.StartAssetEditing();

                            foreach (var ec in level.GetWrappers<EntityClass>())
                            {
                                GameObject newClass = ClassLoader.Instance.LoadGeneralClass(ec.name);
                                if (newClass != null)
                                {
                                    importedObjs.Add(newClass);
                                }
                            }
                        }
                        finally
                        {
                            //AssetDatabase.StopAssetEditing();
                            //AssetDatabase.Refresh();
                        }

                        foreach (GameObject newClass in importedObjs)
                        {
                            if (newClass != null)
                            {
                                newClass.transform.SetParent(root.transform, false);

                                float xExtent = UnityUtils.GetMaxBounds(newClass).extents.x;
                                spawnLoc += new Vector3(xExtent,0,0);
                                newClass.transform.localPosition = spawnLoc;
                                spawnLoc += new Vector3(xExtent,0,0);
                            }
                        }

                        offset += new UnityEngine.Vector3(0,0,20);
                    }
                }

                container.Delete();
            }
        }
    }
}
