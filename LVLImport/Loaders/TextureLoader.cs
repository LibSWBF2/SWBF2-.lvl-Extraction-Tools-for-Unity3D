using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class TextureLoader : Loader {

    public static Dictionary<string, Texture2D> texDataBase = new Dictionary<string, Texture2D>();
    public static bool imported = false;


    public static bool SaveAssets = false;

    public static void ResetDB()
    {
        texDataBase.Clear();
    }

    public static Texture2D ImportTexture(string name, bool reuse=true) 
    {
        string texturePath = "Assets/Textures/" + Regex.Replace(name, @"\s+", "") + ".png";

        if (texDataBase.ContainsKey(name))
        {
            return texDataBase[name];
        }

        if (File.Exists(texturePath))
        {
            Texture2D tex2D = new Texture2D(2,2);
            byte[] texBytes = File.ReadAllBytes(texturePath);
            tex2D.LoadImage(texBytes);

            texDataBase[name] = tex2D;
            return tex2D;
        }


        var tex = container.FindWrapper<LibSWBF2.Wrappers.Texture>(name);

        if (tex != null && tex.width * tex.width > 0)
        {
            Texture2D newTexture = new Texture2D(tex.width,tex.height);
            byte[] data = tex.GetBytesRGBA();

            Color[] colors = newTexture.GetPixels(0);
            for (int i = 0; i < tex.height * tex.width; i++)
            {
                colors[i] = new Color(data[i*4]/255.0f,data[i*4 + 1]/255.0f,data[i*4 + 2]/255.0f,data[i*4 + 3]/255.0f);
            }
            newTexture.SetPixels(colors,0);
            newTexture.Apply();

            if (SaveAssets)
            {
                File.WriteAllBytes(texturePath, newTexture.EncodeToPNG());
            }

            texDataBase[name] = newTexture;
            return newTexture;
        }
        else 
        {
            Debug.LogError(String.Format("Texture: {0} failed to load!", name));
            return null;
        }
    }
}
