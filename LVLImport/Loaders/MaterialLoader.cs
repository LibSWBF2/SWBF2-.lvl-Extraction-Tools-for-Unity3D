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
using LibSWBF2.Enums;

using LibMaterial = LibSWBF2.Wrappers.Material;
using UMaterial = UnityEngine.Material;


public class MaterialLoader : Loader
{
    static Shader DefaultSTDShader         = Shader.Find("ConversionAssets/SWBFStandard");
    static Shader DefaultHDRPShader        = Shader.Find("HDRP/Lit");
    static Shader DefaultTerrainSTDShader  = Shader.Find("ConversionAssets/SWBFTerrain");
    static Shader DefaultTerrainHDRPShader = Shader.Find("HDRP/Lit");

    public static Shader DefaultShader => UseHDRP ? DefaultHDRPShader : DefaultSTDShader;
    public static Shader TerrainShader => UseHDRP ? DefaultTerrainHDRPShader : DefaultTerrainSTDShader;


    public static bool UseHDRP;


    public static MaterialLoader Instance { get; private set; } = null;
    static MaterialLoader()
    {
        Instance = new MaterialLoader();
    }

    private Dictionary<string, UMaterial> materialDataBase = new Dictionary<string, UMaterial>();


    public void ResetDB()
    {
        materialDataBase.Clear();
    }



    public UMaterial LoadMaterial(LibMaterial mat)
    {
        string texName = mat.textures[0];
        MaterialFlags matFlags = mat.materialFlags;

        if (texName == "")
        {
            return new UMaterial(DefaultShader);
        } 
        else 
        {
            string materialName = texName + "_" + ((uint) matFlags).ToString();

            if (!materialDataBase.ContainsKey(materialName))
            {
                UMaterial material = new UMaterial(DefaultShader);

                if (SaveAssets)
                {
                    AssetDatabase.CreateAsset(material, Path.Combine(SaveDirectory, materialName + ".mat")); 
                }

                material.name = materialName;

                if (UseHDRP)
                {
                    if (matFlags.HasFlag(MaterialFlags.Doublesided))
                    {
                        material.SetFloat("_DoubleSidedEnable", 1.0f);
                    }
                    if (matFlags.HasFlag(MaterialFlags.Transparent))
                    {
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }

                    material.SetFloat("_Metallic", 0.0f);
                    material.SetFloat("_Smoothness", 0.0f);

                    Texture2D importedTex = TextureLoader.Instance.ImportTexture(texName);
                    if (importedTex != null)
                    {
                        //material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");
                        material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                        material.mainTexture = importedTex;
                    }
                }
                else
                {
                    material.SetFloat("_Glossiness", 0.0f);

                    if (matFlags.HasFlag(MaterialFlags.Hardedged))
                    {
                        SetRenderMode(ref material, 1);
                    }
                    else if (matFlags.HasFlag(MaterialFlags.Transparent))
                    {
                        SetRenderMode(ref material, 2);
                    }

                    if (matFlags.HasFlag(MaterialFlags.Doublesided))
                    {
                        material.SetInt("_Cull",(int) UnityEngine.Rendering.CullMode.Off);
                    }

                    Texture2D importedTex = TextureLoader.Instance.ImportTexture(texName);
                    if (importedTex != null)
                    {
                        material.mainTexture = importedTex;
                    }

                    if (matFlags.HasFlag(MaterialFlags.Glow))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetTexture("_EmissionMap", importedTex);
                        material.SetColor("_EmissionColor", Color.white);
                    }
                }

                materialDataBase[materialName] = material;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return materialDataBase[materialName];
        }
    }

    /*From https://answers.unity.com/questions/1004666/change-material-rendering-mode-in-runtime.html */
    static void SetRenderMode(ref UMaterial standardShaderMaterial, int blendMode)
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
}
