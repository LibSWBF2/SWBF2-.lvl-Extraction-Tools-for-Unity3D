using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class ModelLoader : ScriptableObject {

    //The below 2 methods will be replaced with the NativeArray<T> ones...
    public static Vector3[] floatToVec3Array(float[] floats)
    {
        Vector3[] vectors = new Vector3[floats.Length / 3];
        for (int i = 0; i < floats.Length; i+=3)
        {
            vectors[i / 3] = new Vector3(floats[i],floats[i+1],floats[i+2]);
        }
        return vectors;
    }

    public static Vector2[] floatToVec2Array(float[] floats)
    {
        Vector2[] vectors = new Vector2[floats.Length / 2];
        for (int i = 0; i < floats.Length; i+=2)
        {
            vectors[i / 2] = new Vector2(floats[i],floats[i+1]);
        }
        return vectors;
    }

    public static GameObject GameObjectFromModel(Model model)
    {
        GameObject newObject = new Gameobject();
        newObject.name = model.GetName();

        Segment[] segments = model.GetSegments(); 

        foreach (Segment seg in segments)
        {
            string texName = seg.GetMaterialTexName();
            if (texName == "")
            {
                continue;
            }

            Vector3[] vertexBuffer = ModelLoader.floatToVec3Array(seg.GetVertexBuffer()); 
            Vector2[] UVs = ModelLoader.floatToVec2Array(seg.GetUVBuffer());
            Vector3[] normalsBuffer = ModelLoader.floatToVec3Array(seg.GetNormalsBuffer());
            int[] indexBuffer = seg.GetIndexBuffer();

            Mesh objectMesh = new Mesh();

            objectMesh.SetVertices(vertexBuffer);
            objectMesh.SetUVs(0,UVs);
            objectMesh.SetNormals(normalsBuffer);
            objectMesh.SetIndicies(indexBuffer, MeshTopology.Triangles);

            GameObject childObject = new Gameobject();
            childObject.AddComponent<Mesh>(objectMesh);

            childObject.transform.SetParent(newObject.transform);
        }  

        return newObject;      
    }




    [MenuItem("SWBF2/Import Models", false, 1)]
    public static void ImportModels(Level level)
    {
        Model[] models = level.GetModels();
        
        foreach (Model model in models)
        {
            if (model.Name.Contains("LOWD") || model.GetTopology != 4) continue;

            GameObject newObject = ModelLoader.GameObjectFromModel(model);

            Object prefab = EditorUtility.CreateEmptyPrefab("Assets/Models/"+newObject.name+".prefab");
            EditorUtility.ReplacePrefab(newObject, prefab, ReplacePrefabOptions.ConnectToPrefab);

                   
        } 

        
    }
}
