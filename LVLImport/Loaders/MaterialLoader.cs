using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;

using LibMaterial = LibSWBF2.Wrappers.Material;
using UMaterial = UnityEngine.Material;
using LibBone = LibSWBF2.Wrappers.Bone;


public class MaterialLoader : Loader {

    public static Dictionary<string, UMaterial> materialDataBase = new Dictionary<string, UMaterial>();
    public static UMaterial defaultMaterial = new UMaterial(Shader.Find("Standard"));
    public static bool SaveAssets = false;

    private static string matsFolder;


    public static void SetSaveDirectory(string path)
    {
        matsFolder = path;  
        if (!Directory.Exists(matsFolder))
        {
            Directory.CreateDirectory(matsFolder);
        }
    }




    public static void ResetDB()
    {
        materialDataBase.Clear();
    }


    public static UMaterial LoadMaterial(LibMaterial mat)
    {
        string texName = mat.textures[0];
        uint matFlags = mat.materialFlags;

        if (texName == "")
        {
            return defaultMaterial;
        } 
        else 
        {
            string materialName = texName + "_" + matFlags.ToString();

            if (!materialDataBase.ContainsKey(materialName))
            {
                UMaterial material = new UMaterial(defaultMaterial);

                if (SaveAssets)
                {
                    AssetDatabase.CreateAsset(material, Path.Combine(matsFolder, materialName + ".mat")); 
                }

                material.name = materialName;
                material.SetFloat("_Glossiness", 0.0f);

                if (IsCutout(matFlags))
                {
                    SetRenderMode(ref material, 1);
                }
                else if (IsTransparent(matFlags))
                {
                    SetRenderMode(ref material, 2);
                }

                Texture2D importedTex = TextureLoader.ImportTexture(texName);
                if (importedTex != null)
                {
                    material.mainTexture = importedTex;
                }

                if (IsEmissive(matFlags))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetTexture("_EmissionMap", importedTex);
                    material.SetColor("_EmissionColor", Color.white);
                }         

                materialDataBase[materialName] = material;
            }

            return materialDataBase[materialName];
        }
    }



    public static void PatchMaterial(GameObject obj, string patchType="")
    {
        var renderer = obj.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            foreach (UMaterial mat in renderer.sharedMaterials)
            {
                if (patchType.Equals("skydome"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", mat.GetTexture("_MainTex"));
                    mat.SetColor("_EmissionColor", Color.white);
                }
            }
        }
        else
        {
            Debug.Log("Can't patch skydome mat, renderer is null...");
        }
    }


    /*From https://answers.unity.com/questions/1004666/change-material-rendering-mode-in-runtime.html */
    public static void SetRenderMode(ref UMaterial standardShaderMaterial, int blendMode)
    {
        switch (blendMode)
        {
            case 0: //opaque
                standardShaderMaterial.SetFloat("_Mode",(float) blendMode);
                standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                standardShaderMaterial.SetInt("_ZWrite", 1);
                standardShaderMaterial.DisableKeyword("_ALPHATEST_ON");
                standardShaderMaterial.DisableKeyword("_ALPHABLEND_ON");
                standardShaderMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                standardShaderMaterial.renderQueue = -1;
                break;
            case 1: //cutout
                standardShaderMaterial.SetFloat("_Mode",(float) blendMode);
                standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                standardShaderMaterial.SetInt("_ZWrite", 1);
                standardShaderMaterial.EnableKeyword("_ALPHATEST_ON");
                standardShaderMaterial.DisableKeyword("_ALPHABLEND_ON");
                standardShaderMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                standardShaderMaterial.renderQueue = 2450;
                break;
            case 2: //fade
                standardShaderMaterial.SetFloat("_Mode",(float) blendMode);
                standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                standardShaderMaterial.SetInt("_ZWrite", 0);
                standardShaderMaterial.DisableKeyword("_ALPHATEST_ON");
                standardShaderMaterial.EnableKeyword("_ALPHABLEND_ON");
                standardShaderMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                standardShaderMaterial.renderQueue = 3000;
                break;
            case 3: //transparent
                standardShaderMaterial.SetFloat("_Mode",(float) blendMode);
                standardShaderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                standardShaderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                standardShaderMaterial.SetInt("_ZWrite", 0);
                standardShaderMaterial.DisableKeyword("_ALPHATEST_ON");
                standardShaderMaterial.DisableKeyword("_ALPHABLEND_ON");
                standardShaderMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                standardShaderMaterial.renderQueue = 3000;
                break;
        }
    }


    public static bool IsEmissive(uint flags)
    {
        return (flags & (uint)16) != 0; 
    }

    public static bool IsTransparent(uint flags)
    {
        return (flags & (uint)4) != 0;
    }

    public static bool IsScrolling(uint flags)
    {
        return (flags & (uint)16777216) != 0;
    }

    public static bool IsCutout(uint flags)
    {
        return (flags & (uint)2) != 0;
    }  


}
