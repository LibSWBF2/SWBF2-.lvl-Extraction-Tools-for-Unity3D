using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
//using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class ModelLoader : ScriptableObject {

    public static Dictionary<string, Material> materialDataBase = new Dictionary<string, Material>();
    public static Material defaultMaterial = new Material(Shader.Find("Standard"));



    public static Material GetMaterial(Level level, string texName, uint matFlags)
    {
        if (texName == "")
        {
            return defaultMaterial;
        } 
        else 
        {
            string materialName = texName + "_" + matFlags.ToString();

            if (!materialDataBase.ContainsKey(materialName))
            {
                Material material = new Material(defaultMaterial);
                material.name = materialName;

                if (MaterialsUtils.IsCutout(matFlags))
                {
                    MaterialsUtils.SetRenderMode(ref material, 1);
                }
                else if (MaterialsUtils.IsTransparent(matFlags))
                {
                    MaterialsUtils.SetRenderMode(ref material, 3);
                }

                Texture2D importedTex = TextureLoader.ImportTexture(level, texName);
                if (importedTex != null)
                {
                    material.mainTexture = importedTex;
                }

                materialDataBase[materialName] = material;
            }

            return materialDataBase[materialName];
        }
    }


    public static GameObject GameObjectFromModel(Level level, Model model)
    {
        GameObject newObject  = new GameObject();
        MeshRenderer renderer = newObject.AddComponent<MeshRenderer>();
        Mesh mesh             = new Mesh();
        MeshFilter filter     = newObject.AddComponent<MeshFilter>();
        newObject.isStatic    = true;

        try {
            newObject.name = model.Name;
        } 
        catch (Exception e)
        {
        	Debug.Log("Exception in gameobj from model: " + e.ToString());
            DestroyImmediate(newObject);
            return null;
        }

        Segment[] segments = model.GetSegments(); 

        Material[] mats = new Material[segments.Length];

        mesh.subMeshCount = segments.Length;


        int totalLength = 0;
        foreach (Segment seg in segments)
        {
            totalLength += (int) seg.GetVertexBufferLength();
        }

        Vector3[] positions = new Vector3[totalLength];
        Vector3[] normals = new Vector3[totalLength];
        Vector2[] texcoords = new Vector2[totalLength];
        int[] offsets = new int[segments.Length];


        int dataOffset = 0;

        for (int i = 0; i < segments.Length; i++)
        {
            Segment seg = segments[i];


            // Handle material data
            string texName = seg.GetMaterialTexName();
            uint matFlags = seg.GetMaterialFlags();
            mats[i] = GetMaterial(level, texName, matFlags);


            // Handle vertex data
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetVertexBuffer(), positions, dataOffset, false);
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetNormalsBuffer(), normals, dataOffset, false);
            UnityUtils.FillVec2(seg.GetUVBuffer(), texcoords, dataOffset);

            offsets[i] = dataOffset;

            dataOffset += (int) seg.GetVertexBufferLength();
        }

        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0,texcoords);

        renderer.sharedMaterials = mats;


        int j = 0;
        foreach (Segment seg in segments)
        {
            mesh.SetTriangles(seg.GetIndexBuffer(), j, true, offsets[j]);
            j++;
        }

        filter.sharedMesh = mesh;


        CollisionMesh collMesh = model.GetCollisionMesh();
        uint[] indBuffer = collMesh.GetIndices();

        try {

            if (indBuffer.Length > 2)
            {
                Mesh collMeshUnity = new Mesh();
                collMeshUnity.vertices = UnityUtils.FloatToVec3Array(collMesh.GetVertices(), false);
                
                collMeshUnity.SetIndexBufferParams(indBuffer.Length, IndexFormat.UInt32);
                collMeshUnity.SetIndexBufferData(indBuffer, 0, 0, indBuffer.Length);

                MeshCollider meshCollider = newObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = collMeshUnity;
            }
        } 
        catch (Exception e)
        {
            Debug.Log(e.ToString() + " while creating mesh collider...");
        }

        return newObject;      
    }

    //[MenuItem("SWBF2/Import Models", false, 1)]
    public static void ImportModels(Level level)
    {
        Model[] models = level.GetModels();
        
        foreach (Model model in models)
        {
            if (model.Name.Contains("LOWD")) continue;
            GameObject newObject = ModelLoader.GameObjectFromModel(level, model);
        } 
    }
}
