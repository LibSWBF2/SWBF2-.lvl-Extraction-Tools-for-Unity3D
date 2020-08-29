using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class TerrainLoader : ScriptableObject {

    [MenuItem("SWBF2/Import Terrain", false, 1)]
    public static void ImportTerrain(Level level) {

        LibSWBF2.Wrappers.Terrain terrain = level.GetTerrain();

        foreach (var str in terrain.Names)
        {
            if (str == ""){
                continue;
            }

            if (level.GetTexture(str, out byte[] data, out int width, out int height))
            {
                Texture2D tex = new Texture2D(width,height);
                Color[] colors = tex.GetPixels(0);
                for (int i = 0; i < height * width; i++)
                {
                    colors[i] = new Color(data[i*4]/255.0f,data[i*4 + 1]/255.0f,data[i*4 + 2]/255.0f,data[i*4 + 3]/255.0f);
                }
                tex.SetPixels(colors,0);
                tex.Apply();
                byte[] pngBytes = tex.EncodeToPNG();
                File.WriteAllBytes(Application.dataPath + "/Textures/" + Regex.Replace(str, @"\s+", "") + ".png", pngBytes);
            }
            else 
            {
                Debug.Log("Terrain Import Failed!");
                return;
            }
        }

        float[] heightsRaw = terrain.Heights;
        int dim = terrain.width;
        
        TerrainData terData = new TerrainData();
        terData.heightmapResolution = terrain.width + 1;
        terData.size = new Vector3(terrain.width, 15, dim);
        terData.baseMapResolution = 1024;
        terData.SetDetailResolution(1024, 8);

        float[,] heights = new float[terrain.width,terrain.height];
        for (int x = 0; x < terrain.width; x++){
            for (int y = 0; y < terrain.height; y++){
                heights[x,y] = heightsRaw[x * terrain.width + y];
            }
        }

        terData.SetHeights(0, 0, heights);

        GameObject terrainObj = UnityEngine.Terrain.CreateTerrainGameObject(terData);
        PrefabUtility.SaveAsPrefabAsset(terrainObj, Application.dataPath + "/Terrain/terrain.prefab");
        
        AssetDatabase.Refresh();

        Debug.Log("Terrain Imported!");
    }
}
