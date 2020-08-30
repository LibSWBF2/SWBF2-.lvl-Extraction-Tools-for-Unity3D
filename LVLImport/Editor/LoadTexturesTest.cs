using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class TextureLoader : ScriptableObject {

    public static Dictionary<string, Texture2D> texDataBase = new Dictionary<string, Texture2D>();
    public static bool imported = false;

    public static Texture2D ImportTexture(Level level, string name) {

        string texturePath = Application.dataPath + "/Textures/" + Regex.Replace(name, @"\s+", "") + ".png";

        if (File.Exists(texturePath) && reuse)
        {

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
            File.WriteAllBytes(texturePath, tex.EncodeToPNG());
        }
        else 
        {
            Debug.Log("Texture Import Failed!");
            return null;
        }
        
    }
}
