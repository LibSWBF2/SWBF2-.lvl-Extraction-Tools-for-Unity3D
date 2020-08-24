using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibSWBF2.Logging;
using LibSWBF2.Wrappers;

using UnityEngine;
using UnityEditor;


public class ImportTerrainTest : Editor
{
    // Creates a new menu item 'Examples > Create Prefab' in the main menu.
    [MenuItem("Examples/Terrain to Prefab")]
    static void CreatePrefab()
    {

        Logger.SetLogLevel(ELogType.Warning);
        Logger.OnLog += (LoggerEntry logEntry) => 
        {
            Console.WriteLine(logEntry.ToString());
        };

        Console.WriteLine("Loading... This might take a while...");
        //Level level = Level.FromFile(@"/home/will/Desktop/tst1.lvl");
        Level level = Level.FromFile(@"/home/will/.wine32bit/drive_c/Program Files/Steam/steamapps/common/Star Wars Battlefront II/GameData/data/_lvl_pc/geo/geo1.lvl");

        Terrain terrain = level.GetTerrain();

        string printStr = "";

        foreach (var str in terrain.Names)
        {
            printStr += (" " + str);
        }

        Console.WriteLine("Terrain texture names: " + printStr);
        
        /*Console.WriteLine("Indicies: ");

        int[] rawVerts = terrain.Indicies;

        for (int i = 0; i < rawVerts.Length; i+=3){
            if (i % 200 == 0){
                Console.WriteLine(i + ": (" + rawVerts[i] + ", " + rawVerts[i+1] + ", " + rawVerts[i+2] + ")");
            }
        }*/

        Console.WriteLine("Done!");
        
        // Keep track of the currently selected GameObject(s)
        GameObject[] objectArray = Selection.gameObjects;

        // Loop through every GameObject in the array above
        foreach (GameObject gameObject in objectArray)
        {
            // Set the path as within the Assets folder,
            // and name it as the GameObject's name with the .Prefab format
            string localPath = "Assets/" + gameObject.name + ".prefab";

            // Make sure the file name is unique, in case an existing Prefab has the same name.
            localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);

            // Create the new Prefab.
            PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, localPath, InteractionMode.UserAction);
        }
    }

    // Disable the menu item if no selection is in place.
    [MenuItem("Examples/Create Prefab", true)]
    static bool ValidateCreatePrefab()
    {
        return Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject);
    }
}
