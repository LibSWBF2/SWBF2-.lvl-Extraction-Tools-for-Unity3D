using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;
#if !LVLIMPORT_NO_EDITOR
using UnityEditor;
#endif


public class TextureLoader : Loader
{

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

    public Texture2D ImportUITexture(string name, bool printError=true) => ImportTexture(name, true, printError);
    public Texture2D ImportTexture(string name, bool mirror=false, bool printError=true) 
    {
        if (texDataBase.ContainsKey(name))
        {
            return texDataBase[name];
        }

        var tex = container.Get<LibSWBF2.Wrappers.Texture>(name);

        if (tex != null && tex.Height * tex.Width > 0)
        {
            Texture2D newTexture = new Texture2D(tex.Width, tex.Height, TextureFormat.RGBA32, false);
            newTexture.name = tex.Name;

            byte[] data = tex.GetBytesRGBA();
            data = mirror ? MirrorVertically(data, tex.Width, tex.Height, 4) : data;

            newTexture.LoadRawTextureData(data);
            newTexture.Apply();

#if !LVLIMPORT_NO_EDITOR
            string texPath = SaveDirectory + "/" + name + ".png";
            if (SaveAssets)
            {
                File.WriteAllBytes(texPath, newTexture.EncodeToPNG());
                // TODO: figure out how to save texture assets in an AssetEditing block without
                // lost refs after save/light bake...
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.Default);
                newTexture = (Texture2D) AssetDatabase.LoadAssetAtPath(texPath, typeof(Texture2D));
            }
#endif
            texDataBase[name] = newTexture;
            return newTexture;
        }
        else if (printError)
        {
            Debug.LogWarning($"Texture '{name}' failed to load!");
        }

        return null;
    }

    public Texture2DArray ImportTextures(string[] names, out float[] xDims, bool mirror = false)
    {
        int maxWidth = 0;
        int maxHeight = 0;
        xDims = new float[names.Length];

        LibSWBF2.Wrappers.Texture[] libTextures = new LibSWBF2.Wrappers.Texture[names.Length];
        for (int i = 0; i < names.Length; ++i)
        {
            libTextures[i] = container.Get<LibSWBF2.Wrappers.Texture>(names[i]);
            maxWidth = Mathf.Max(maxWidth, libTextures[i].Width);
            maxHeight = Mathf.Max(maxHeight, libTextures[i].Height);
        }

        byte[] buffer = new byte[maxWidth * maxHeight * 4];

        Texture2DArray textures = new Texture2DArray(maxWidth, maxHeight, names.Length, TextureFormat.RGBA32, false);
        for (int i = 0; i < names.Length; ++i)
        {
            var tex = libTextures[i];
            xDims[i] = tex.Width;

            if (tex.Width < textures.width || tex.Height < textures.height)
            {
                byte[] data = tex.GetBytesRGBA();

                for (int row = 0; row < maxHeight; ++row)
                {
                    int dstWidth = textures.width * 4;
                    int dstStartIdx = row * textures.width * 4;
                    if (row < tex.Height)
                    {
                        int srcWidth = tex.Width * 4;
                        int srcStartIdx = row * tex.Width * 4;

                        Array.Copy(data, srcStartIdx, buffer, dstStartIdx, srcWidth);
                        for (int x = dstWidth - srcWidth; x < dstWidth; ++x)
                        {
                            // fill remaining collumns with 0
                            buffer[dstStartIdx + x] = 0;
                        }
                    }
                    else
                    {
                        for (int x = 0; x < dstWidth; ++x)
                        {
                            // fill remaining rows with 0
                            buffer[dstStartIdx + x] = 0;
                        }
                    }
                }
            }
            else
            {
                buffer = tex.GetBytesRGBA();
            }

            buffer = mirror ? MirrorVertically(buffer, tex.Width, tex.Height, 4) : buffer;
            textures.SetPixelData(buffer, 0, i);
        }
        textures.Apply();
        return textures;
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
