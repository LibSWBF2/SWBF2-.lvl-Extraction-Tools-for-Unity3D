using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using System.Linq;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Wrappers;


public class LVLImportWindow : EditorWindow {

    bool saveAllPrefabs, importLighting,
    currentlyLoading, startLoading, buttonStatus, importMeshTerrain;


    Container container = new Container();

    
    string importMeshTerrainTip = String.Join(
                                " ",
                                "If toggled, will import and draw the terrain as a mesh",
                                "detached from any terrain component,",
                                "using the heightmap representation only for collision."
                                );
                                
    string importLightingTip = String.Join(
                                " ",
                                "Lighting still has some issues,",
                                "light transforms will have incorrect rotations",
                                "and intensities are hardcoded at an estimate of 10"
                                );

    string saveAllPrefabsTip = String.Join(
                                " ",
                                "Will save all gameobjects, textures, models, and materials",
                                "in Assets/[Prefabs, Textures, Models, Materials] respectively.",
                                "Will drastically increase load times..."
                                );

    List<string> filesToLoad = new List<string>();
    List<uint>   fileHandles = new List<uint>();




    [MenuItem("SWBF2/Import .lvl", false, 10)]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
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
            path = EditorUtility.OpenFilePanel("Select lvl file(s)", "", "lvl");
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



    private void AddSpaces(int numSpaces = 1)
    {
        for (int i = 0; i < numSpaces; i++)
        {
            EditorGUILayout.Space();
        }
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

        saveAllPrefabs = EditorGUILayout.Toggle(new GUIContent("Save All Assets", saveAllPrefabsTip), saveAllPrefabs);
        importLighting = EditorGUILayout.Toggle(new GUIContent("Import Lighting", importMeshTerrainTip), importLighting);
        importMeshTerrain = EditorGUILayout.Toggle(new GUIContent("Import Terrain as Mesh", importMeshTerrainTip), importMeshTerrain);

        AddSpaces(5);
        
        GUILayout.BeginHorizontal();
        startLoading = GUILayout.Button("Import Worlds",GUILayout.Width(100));      
        GUILayout.Button("Import Objects",GUILayout.Width(100));
        GUILayout.EndHorizontal();

        GUI.enabled = true;

        if (startLoading)
        {
            fileHandles = new List<uint>();

            foreach (string path in filesToLoad)
            {
                Debug.Log("Adding " + path);
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
                uint handle = fileHandles[i];
                float progress = container.GetProgress(handle);
                EditorGUI.ProgressBar(new Rect(3, 250 + 30 * i, position.width - 6, 20), progress, filesToLoad[i]);
            }

            if (container.IsDone())
            {
                currentlyLoading = false;
                foreach (uint handle in fileHandles)
                {
                    Level level = container.GetLevel(handle);
                    foreach (World world in level.GetWorlds())
                    {
                        Console.WriteLine("New world: " + world.Name);
                    }
                    MapLoader.ImportMap(level);
                }
            }
        }
    }
}
