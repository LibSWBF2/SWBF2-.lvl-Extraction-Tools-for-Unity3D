using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class TextureLoader : Loader {

    public static TextureLoader Instance { get; private set; } = null;

    private Dictionary<string, Texture2D> texDataBase = new Dictionary<string, Texture2D>();


    static TextureLoader()
    {
        Instance = new TextureLoader();
    }


    public void ResetDB()
    {
        texDataBase.Clear();
    }



    public Texture2D ImportTexture(string name) 
    {
        string texPath = SaveDirectory + "/" + name + ".png";

        if (texDataBase.ContainsKey(name))
        {
            return texDataBase[name];
        }

        var tex = container.FindWrapper<LibSWBF2.Wrappers.Texture>(name);

        if (tex != null && tex.height * tex.width > 0)
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
                File.WriteAllBytes(texPath, newTexture.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.Default);
                newTexture = (Texture2D) AssetDatabase.LoadAssetAtPath(texPath, typeof(Texture2D));
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
