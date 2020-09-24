using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class ModelLoader : ScriptableObject {

    public static Dictionary<string, Material> materialDataBase = new Dictionary<string, Material>();
    public static Material defaultMaterial = new Material(Shader.Find("Standard"));


    public static GameObject GameObjectFromModel(Level level, Model model)
    {
        GameObject newObject = new GameObject();
        newObject.isStatic   = true;
        newObject.AddComponent<MeshRenderer>();

        Segment[] segments;

        try {
            newObject.name = model.Name;
        } 
        catch (Exception e)
        {
        	Debug.Log("Exception in gameobj from model: " + e.ToString());
            DestroyImmediate(newObject);
            return null;
        }

        segments = model.GetSegments(); 


        int segCount = 0;
        foreach (Segment seg in segments)
        {
            string texName = seg.GetMaterialTexName();

            if (texName == "")
            {
                continue;
            }

            uint matFlags = seg.GetMaterialFlags();
            string materialName = texName + "_" + matFlags.ToString();

            string childName = newObject.name + "_segment_" + segCount++;

            //Handle mesh
            Vector3[] vertexBuffer = UnityUtils.FloatToVec3Array(seg.GetVertexBuffer()); 
            Vector2[] UVs = UnityUtils.FloatToVec2Array(seg.GetUVBuffer());
            Vector3[] normalsBuffer = UnityUtils.FloatToVec3Array(seg.GetNormalsBuffer());
            int[] indexBuffer = seg.GetIndexBuffer();

            GameObject childObject = new GameObject();

            Mesh objectMesh = new Mesh();
            objectMesh.SetVertices(vertexBuffer);
            objectMesh.SetUVs(0,UVs);
            objectMesh.SetNormals(normalsBuffer);
            objectMesh.SetIndices(indexBuffer, MeshTopology.Triangles, 0);

            MeshFilter filter = childObject.AddComponent<MeshFilter>();
            filter.sharedMesh = objectMesh;
          
            //Handle material
            Texture2D importedTex = TextureLoader.ImportTexture(level, texName);
            MeshRenderer childRenderer = childObject.AddComponent<MeshRenderer>();
            Material material;

            if (materialDataBase.ContainsKey(materialName))
            {
                material = materialDataBase[materialName];
            }
            else
            {
                material = new Material(defaultMaterial);
                material.name = materialName;
                materialDataBase[materialName] = material;
            }

            
            if (MaterialsUtils.IsCutout(matFlags))
            {
                MaterialsUtils.SetRenderMode(ref material, 1);
            }
            else if (MaterialsUtils.IsTransparent(matFlags))
            {
                MaterialsUtils.SetRenderMode(ref material, 3);
            }
            

            childRenderer.sharedMaterial = material;

            if (importedTex == null)
            {
                childRenderer.sharedMaterial.color = Color.black;
            }
            else 
            {
                childRenderer.sharedMaterial.mainTexture = importedTex;
            }

            childObject.transform.SetParent(newObject.transform);
            childObject.name = childName;
        }  

        
        CollisionMesh collMesh = model.GetCollisionMesh();
        int[] indBuffer = collMesh.GetIndices();

        try {

            if (indBuffer.Length > 2)
            {
                Mesh mesh = new Mesh();
                mesh.vertices = UnityUtils.FloatToVec3Array(collMesh.GetVertices());
                mesh.triangles = indBuffer;

                MeshCollider meshCollider = newObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
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
        
        //int i = 0;
        foreach (Model model in models)
        {
            if (model.Name.Contains("LOWD")) continue;
            GameObject newObject = ModelLoader.GameObjectFromModel(level, model);
        } 
    }
}
