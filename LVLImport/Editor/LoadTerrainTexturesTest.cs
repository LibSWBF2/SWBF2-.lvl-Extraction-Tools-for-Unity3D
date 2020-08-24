using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class lvlImportMenu : ScriptableObject {

    [MenuItem("SWBF2/Import Terrain", false, 1)]
    public static void ImportTerrain() {

		LibSWBF2.Logging.Logger.SetLogLevel(ELogType.Warning);
        LibSWBF2.Logging.Logger.OnLog += (LoggerEntry logEntry) => 
        {
            Debug.Log(logEntry.ToString());
        };

        Debug.Log("Loading... This might take a while...");
        Level level = Level.FromFile(@"/Users/will/Desktop/omni.lvl");
        LibSWBF2.Wrappers.Terrain terrain = level.GetTerrain();

        foreach (var str in terrain.Names)
        {
            if (str == ""){
                continue;
            }

            string printStr = "Texture name: " + str;
            if (level.GetTexture(str, out byte[] data, out int width, out int height))
            {
                printStr += (" width: " + width + " height: " + height + " bytes length: " + data.Length);
                Texture2D tex = new Texture2D(width,height);
                Color[] colors = tex.GetPixels(0);
                for (int i = 0; i < height * width; i++)
                {
                	colors[i] = new Color(data[i*4]/255.0f,data[i*4 + 1]/255.0f,data[i*4 + 2]/255.0f,data[i*4 + 3]/255.0f);
                }
                tex.SetPixels(colors,0);
                tex.Apply();
                byte[] pngBytes = tex.EncodeToPNG();
                //UnityEngine.Object.DestroyImmediate(tex);
                File.WriteAllBytes(Application.dataPath + "/Textures/" + Regex.Replace(str, @"\s+", "") + ".png", pngBytes);
            }
            else 
            {
                printStr += " lookup failed.";
            }
            Debug.Log(printStr);
        }

        /*
        TerrainData data = new TerrainData();
        data.heightmapResolution = 16 * 9;

		data.baseMapResolution = 1024;
        data.SetDetailResolution(1024, 8);


        float[] heights = new float[256 * 81];
		*/

        Debug.Log("Done");
    }
}

