using System.IO;
using System.Collections.Generic;

using UnityEngine;
#if !LVLIMPORT_NO_EDITOR
using UnityEditor;
#endif

using LibSWBF2.Enums;

using LibMaterial = LibSWBF2.Wrappers.Material;
using UMaterial = UnityEngine.Material;


public class MaterialLoader : Loader
{
    // NOTE: Loading shaders/materials must NOT happen in static constructor,
    // otherwise building a project using this at runtime won't work!

    const string DefaultSTDShader = "ConversionAssets/SWBFStandard";
    const string DefaultTerrainSTDShader = "ConversionAssets/SWBFTerrain";

    UMaterial _DefaultMaterial = null;
    UMaterial _DefaultTerrainMaterial = null;
    UMaterial _DefaultHDRPTransparentMaterial = null;
    UMaterial _DefaultHDRPUnlitMaterial = null;

    UMaterial GetDefaultMaterial()
    {
        if (_DefaultMaterial == null)
        {
            if (UseHDRP)
            {
                _DefaultMaterial = Resources.Load<UMaterial>("HDRPLit");
            }
            else
            {
                _DefaultMaterial = new UMaterial(Shader.Find(DefaultSTDShader));
            }
        }
        return _DefaultMaterial;
    }

    public UMaterial GetDefaultTerrainMaterial()
    {
        if (_DefaultTerrainMaterial == null)
        {
            if (UseHDRP)
            {
                _DefaultTerrainMaterial = Resources.Load<UMaterial>("HDRPTerrain");
            }
            else
            {
                _DefaultTerrainMaterial = new UMaterial(Shader.Find(DefaultTerrainSTDShader));
            }
        }
        return _DefaultTerrainMaterial;
    }

    UMaterial GetDefaultTransparentMaterial()
    {
        if (_DefaultHDRPTransparentMaterial == null)
        {
            _DefaultHDRPTransparentMaterial = Resources.Load<UMaterial>("HDRPTransparent");
        }
        return _DefaultHDRPTransparentMaterial;
    }

    UMaterial GetDefaultUnlitMaterial()
    {
        if (_DefaultHDRPUnlitMaterial == null)
        {
            _DefaultHDRPUnlitMaterial = Resources.Load<UMaterial>("HDRPTransparent");
        }
        return _DefaultHDRPUnlitMaterial;
    }

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



    public UMaterial LoadMaterial(LibMaterial mat, string overrideTexture, bool unlit = false)
    {
        string texName = mat.Textures[0];
        MaterialFlags matFlags = mat.MaterialFlags;

        if (!string.IsNullOrEmpty(overrideTexture))
        {
            texName = overrideTexture;
        }

        if (texName == "")
        {
            return new UMaterial(GetDefaultMaterial());
        } 
        else 
        {
            string materialName = texName + "_" + ((uint) matFlags).ToString();

            if (!materialDataBase.ContainsKey(materialName))
            {
                UMaterial material = null;

                if (UseHDRP)
                {
                    Texture2D importedTex = TextureLoader.Instance.ImportTexture(texName);

                    // glowing materials use the textures alpha channel as glow map
                    // glowing materials can therefore NOT be transparent
                    if (matFlags.HasFlag(MaterialFlags.Glow) && !matFlags.HasFlag(MaterialFlags.Transparent))
                    {
                        material = new UMaterial(unlit ? GetDefaultUnlitMaterial() : GetDefaultMaterial());
                        material.EnableKeyword("_EMISSIVE_MAPPING_BASE");
                        material.EnableKeyword("_EMISSIVE_COLOR_MAP");
                        if (importedTex != null)
                        {
                            material.SetTexture("_EmissiveColorMap", AlphaToGrayscale(importedTex));
                        }
                        material.SetColor("_EmissiveColor", Color.white * 30.0f);
                        material.SetFloat("_EmissiveExposureWeight", 0.0f);
                        //material.SetFloat("_EmissiveIntensity", 7.0f);
                    }
                    else if (matFlags.HasFlag(MaterialFlags.Transparent))
                    {
                        material = new UMaterial(GetDefaultTransparentMaterial());

                        //material.SetFloat("_AlphaCutoffEnable", 1.0f);
                        //material.SetFloat("_SurfaceType", 1.0f);
                        //material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        //material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                        //material.EnableKeyword("ENABLE_ALPHA");
                        //material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");
                        //material.EnableKeyword("_ALPHATEST_ON");
                        //material.EnableKeyword("_ALPHATOMASK_ON");
                        //material.SetFloat("_TransparentZWrite", 1.0f);
                        //material.SetFloat("_EnableBlendModePreserveSpecularLighting", 0.0f);
                        //material.SetFloat("_AlphaDstBlend", 10.0f);
                        //material.SetFloat("_SrcBlend", 1.0f);
                        //material.SetFloat("_DstBlend", 10.0f);
                        //material.SetFloat("_ZWrite", 0.0f);
                        //material.SetFloat("_StencilRefDepth", 0.0f);
                        //material.SetFloat("_StencilRefGBuffer", 22.0f);
                        //material.SetFloat("_StencilRefMV", 32.0f);
                        //material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
                        //material.SetOverrideTag("RenderType", "Transparent");
                        //material.renderQueue = 3000;
                    }
                    else
                    {
                        material = new UMaterial(unlit ? GetDefaultUnlitMaterial() : GetDefaultMaterial());
                    }

                    if (matFlags.HasFlag(MaterialFlags.Doublesided))
                    {
                        material.SetFloat("_DoubleSidedEnable", 1.0f);
                    }

                    material.SetFloat("_Metallic", 0.0f);
                    material.SetFloat("_Smoothness", 0.0f);

                    if (importedTex != null)
                    {
                        //material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");
                        //material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                        material.mainTexture = importedTex;
                    }
                }
                else
                {
                    material = new UMaterial(GetDefaultMaterial());

#if !LVLIMPORT_NO_EDITOR
                    if (SaveAssets)
                    {
                        AssetDatabase.CreateAsset(material, Path.Combine(SaveDirectory, materialName + ".mat"));
                    }
#endif
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

                material.name = materialName;
                materialDataBase[materialName] = material;
            }

#if !LVLIMPORT_NO_EDITOR
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
#endif
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

    static Texture2D AlphaToGrayscale(Texture2D rgba)
    {
        Debug.Assert(rgba.format == UnityEngine.TextureFormat.RGBA32);

        Texture2D grayscale = new Texture2D(rgba.width, rgba.height, UnityEngine.TextureFormat.RGB24, false);
        int numPixels = rgba.width * rgba.height;
        byte[] src = rgba.GetRawTextureData();
        byte[] dst = new byte[numPixels * 3];
        for (int i = 0; i < numPixels; ++i)
        {
            byte alpha = src[(i * 4) + 3];
            dst[(i * 3) + 0] = alpha;
            dst[(i * 3) + 1] = alpha;
            dst[(i * 3) + 2] = alpha;
        }
        grayscale.LoadRawTextureData(dst);
        grayscale.Apply();
        return grayscale;
    }
}
