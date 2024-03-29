#if !LVLIMPORT_NO_EDITOR

using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Debug = System.Diagnostics.Debug;
using UDebug = UnityEngine.Debug;

using System.Linq;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Wrappers;
using LibSWBF2.Enums;


public class LVLImportWindow : EditorWindow {

    bool currentlyLoading, startLoading;

    bool startLoadWorlds, startLoadClasses, startLoadEffects;

    static bool terrainAsMesh = true;
    static bool saveTextures, saveMaterials, saveModels, saveAnims, saveObjects, saveWorld, saveEffects;

    static string matFolder = "Materials";
    static string texFolder = "Textures";
    static string modelsFolder = "Models";
    static string animsFolder = "Animations";
    static string objectsFolder = "Objects";
    static string worldFolder = "World";
    static string effectsFolder = "Effects";

    static string savePathPrefix = "Assets/LVLImport/"; 

    static string effectToImport = "";

    static Container container;

    static List<string> filesToLoad = new List<string>();
    static List<LibSWBF2.SWBF2Handle> fileHandles = new List<LibSWBF2.SWBF2Handle>();

    IEnumerator<LoadStatus> CurrentLoad = null;


    enum ImporterAction
    {
        ImportClasses,
        ImportEffects,
        ImportWorlds,
        None
    }

    static ImporterAction CurrentAction = ImporterAction.None;


    enum ImporterState
    {
        Configuring,
        Loading,
        Importing,
    } 

    static ImporterState CurrentState = ImporterState.Configuring;




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
        GUILayout.BeginHorizontal();
        initStatus = EditorGUILayout.Toggle(new GUIContent("Save " + type, ""), initStatus);

        if (initStatus)
        {
            EditorGUILayout.LabelField(savePathPrefix);
            initVal = EditorGUILayout.TextField("", initVal, GUILayout.ExpandWidth(false));
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




    private ImporterAction ExecStateConfiguration()
    {
        EditorGUIUtility.labelWidth = 200;
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

        terrainAsMesh = EditorGUILayout.Toggle(new GUIContent("Import Terrain Mesh as a Mesh", "Import SWBF Terrain data as a Mesh.  Produces the most accurate geometry, but the custom material/shader that is used will probably not export cleanly to other engines/formats."), terrainAsMesh);
        terrainAsMesh = !EditorGUILayout.Toggle(new GUIContent("Import Terrain Mesh as Terrain", "Import SWBF Terrain data as a Terrain Object.  Not as geometrically accurate as a Mesh, especially if the terrain has holes, but the material will export to other engines/formats more cleanly."), !terrainAsMesh);




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
            UDebug.LogError("Save Path Prefix must start with \"Assets/\"!");
        }

        savePathPrefix = savePathPrefix.EndsWith("/") ? savePathPrefix : savePathPrefix + "/";

        AddSaveOption("Textures", ref saveTextures, ref texFolder);
        AddSaveOption("Materials", ref saveMaterials, ref matFolder);
        AddSaveOption("Models", ref saveModels, ref modelsFolder);
        AddSaveOption("Animations", ref saveAnims, ref animsFolder);
        AddSaveOption("Objects", ref saveObjects, ref objectsFolder);
        AddSaveOption("World", ref saveWorld, ref worldFolder);
        
        // AddSaveOption("Effects", ref saveEffects, ref effectsFolder);


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

        if (saveEffects)
        {
            //saveTextures = true;
            effectToImport = EditorGUILayout.TextField("", effectToImport);
        }       

        AddSpaces(5);


        GUILayout.BeginHorizontal();

        ImporterAction result = ImporterAction.None;

        if (GUILayout.Button("Import Worlds",GUILayout.Width(100)))
        {
            result = ImporterAction.ImportWorlds;
        }
        else if (GUILayout.Button("Import Objects",GUILayout.Width(100)))
        {
            result = ImporterAction.ImportClasses;
        }
        /*
        else if (GUILayout.Button("Import Effects",GUILayout.Width(100)))
        {
            result = ImporterAction.ImportEffects;
        }
        */

        GUILayout.EndHorizontal();

        return result;
    }



    private bool ExecStateLoading()
    {
        Debug.Assert(CurrentState == ImporterState.Loading);
        for (int i = 0; i < filesToLoad.Count; i++)
        {
            LibSWBF2.SWBF2Handle handle = fileHandles[i];
            float progress = container.GetProgress(handle);
            EditorGUI.ProgressBar(new Rect(3, 250 + 30 * i, position.width - 6, 20), progress, filesToLoad[i]);
        }

        return container.IsDone();
    }


    private bool ExecStateImporting()
    {
        Debug.Assert(CurrentState == ImporterState.Importing);

        if (CurrentLoad != null)
        {
            if (!CurrentLoad.MoveNext())
            {
                return true;
            }
            else 
            {
                var status = CurrentLoad.Current;
                EditorGUI.ProgressBar(new Rect(3, 250 + 0, position.width - 6, 20), status.Progress, status.CurrentTask);
                return false;
            }
        }
        else 
        {
            return true;
        }
    }


    void TransitionToLoading()
    {
        Debug.Assert(CurrentState == ImporterState.Configuring);
        CurrentState = ImporterState.Loading;

        container = new Container();

        fileHandles = new List<LibSWBF2.SWBF2Handle>();
        foreach (string path in filesToLoad)
        {
            fileHandles.Add((LibSWBF2.SWBF2Handle) container.AddLevel(path));
        }

        container.LoadLevels();
    }



    void TransitionToImporting()
    {
        Debug.Assert(CurrentState == ImporterState.Loading);
        CurrentState = ImporterState.Importing;

        Loader.ResetAllLoaders();
        Loader.SetGlobalContainer(container);

        WorldLoader.Instance.TerrainAsMesh = terrainAsMesh;

        if (saveTextures){ TextureLoader.Instance.SetSave(savePathPrefix,texFolder); }
        if (saveMaterials) { MaterialLoader.Instance.SetSave(savePathPrefix,matFolder); }
        if (saveModels) { ModelLoader.Instance.SetSave(savePathPrefix,modelsFolder); }
        if (saveAnims) { AnimationLoader.Instance.SetSave(savePathPrefix,animsFolder); }
        if (saveObjects) { ClassLoader.Instance.SetSave(savePathPrefix,objectsFolder); }
        if (saveWorld) { WorldLoader.Instance.SetSave(savePathPrefix, worldFolder); }

        if (saveEffects) { EffectsLoader.Instance.SetSave(savePathPrefix, effectsFolder); }


        List<Level> levels = new List<Level>();

        foreach (var handle in fileHandles)
        {
            Level l = container.GetLevel(handle);
            if (l != null)
            {
                levels.Add(l);    
            }
        }

        if (CurrentAction == ImporterAction.ImportWorlds)
        {
            CurrentLoad = WorldLoader.Instance.ImportWorldBatch(levels.ToArray());
        }
        else if (CurrentAction == ImporterAction.ImportClasses)
        {
            CurrentLoad = ClassLoader.Instance.ImportClassBatch(levels.ToArray());
        }
        else 
        {
            EffectsLoader.Instance.ImportEffects(new string[] {effectToImport});
            //EffectsLoader.Instance.ImportEffects(new string[] {"com_sfx_ord_flame", "com_sfx_weap_rad_exp_md", "com_sfx_weap_rad_exp_sm", "com_sfx_explosion_xl"});
        }
    }


    void TransitionToConfiguration()
    {
        CurrentState = ImporterState.Configuring;
        CurrentAction = ImporterAction.None;
        if (container != null)
        {
            container.Delete();
        }
        container = null;
    }




    void OnGUI()
    {
        if (CurrentState == ImporterState.Configuring)
        {
            CurrentAction = ExecStateConfiguration();
            if (CurrentAction != ImporterAction.None)
            {
                TransitionToLoading();
            }
        }
        else if (CurrentState == ImporterState.Loading)
        {
            if (ExecStateLoading())
            {
                TransitionToImporting();
            }
            else 
            {

            }
        }
        else if (CurrentState == ImporterState.Importing)
        {
            if (ExecStateImporting())
            {
                ModelLoader.Instance.DeleteGODB();
                TransitionToConfiguration();
            }
        }
    }
}

#endif


