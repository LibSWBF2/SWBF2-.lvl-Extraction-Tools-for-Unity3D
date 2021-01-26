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

                if (MaterialsUtils.IsCutout(matFlags))
                {
                    MaterialsUtils.SetRenderMode(ref material, 1);
                }
                else if (MaterialsUtils.IsTransparent(matFlags))
                {
                    MaterialsUtils.SetRenderMode(ref material, 3);
                }

                Texture2D importedTex = TextureLoader.ImportTexture(texName);
                if (importedTex != null)
                {
                    material.mainTexture = importedTex;
                }

                if (MaterialsUtils.IsEmissive(matFlags))
                {
                    material.SetTexture("_EmissionMap", importedTex);
                    material.SetColor("_EmissionColor", Color.white);
                }         

                materialDataBase[materialName] = material;
            }

            return materialDataBase[materialName];
        }
    }



    public static void PatchMaterial(ref GameObject obj, string patchType="")
    {
        var renderer = obj.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            foreach (UMaterial mat in renderer.sharedMaterials)
            {
                if (patchType.Equals("skydome"))
                {
                    mat.SetTexture("_EmissionMap", mat.GetTexture("_EmissionMap"));
                    mat.SetColor("_EmissionColor", Color.white);
                }
            }
        }
    }
}
