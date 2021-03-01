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

    public Texture2D ImportUITexture(string name) => ImportTexture(name, true);
    public Texture2D ImportTexture(string name, bool mirror=false) 
    {
        if (texDataBase.ContainsKey(name))
        {
            return texDataBase[name];
        }

        var tex = container.FindWrapper<LibSWBF2.Wrappers.Texture>(name);

        if (tex != null && tex.width * tex.width > 0)
        {
            Texture2D newTexture = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            newTexture.name = tex.name;
            byte[] data = mirror ? MirrorVertically(tex.GetBytesRGBA(), tex.width, tex.height, 4) : tex.GetBytesRGBA();
            newTexture.LoadRawTextureData(data);
            newTexture.Apply();

            if (SaveAssets)
            {
                File.WriteAllBytes(SaveDirectory + "/" + name + ".png", newTexture.EncodeToPNG());
            }

            texDataBase[name] = newTexture;
            return newTexture;
        }
        else 
        {
            Debug.LogWarningFormat("Texture: {0} failed to load!", name);
            return null;
        }
    }


    static byte[] MirrorVertically(byte[] data, int width, int height, int stride)
    {
        int byteWidth = width * stride;
        byte[] mirrored = new byte[data.Length];
        for (int rowIdx = 0; rowIdx < height; ++rowIdx)
        {
            int rowReverseIdx = height - rowIdx - 1;
            Array.Copy(data, rowReverseIdx * byteWidth, mirrored, rowIdx * byteWidth, byteWidth);
        }
        return mirrored;
    }
}
