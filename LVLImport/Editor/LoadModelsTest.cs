using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class ModelLoader : ScriptableObject {

    static int modelCounter = 0;

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
        GameObject newObject = new GameObject();
        newObject.AddComponent<MeshRenderer>();
        newObject.name = model.Name;

        Segment[] segments = model.GetSegments(); 

        int segCount = 0;
        foreach (Segment seg in segments)
        {
            string texName = seg.GetMaterialTexName();

//          Debug.Log("Segment topology: " + seg.GetTopology());
//          Debug.Log("Num verts: " + seg.GetVertexBuffer().Length / 3);
//          Debug.Log("Index buffer length: " + seg.GetIndexBuffer().Length);

            if (texName == "")// || seg.GetTopology() != 4)
            {
                continue;
            }

            string childName = newObject.name + "segment" + segCount++;

            Vector3[] vertexBuffer = ModelLoader.floatToVec3Array(seg.GetVertexBuffer()); 
            Vector2[] UVs = ModelLoader.floatToVec2Array(seg.GetUVBuffer());
            Vector3[] normalsBuffer = ModelLoader.floatToVec3Array(seg.GetNormalsBuffer());
            int[] indexBuffer = seg.GetIndexBuffer();

            GameObject childObject = new GameObject();

            Mesh objectMesh = new Mesh();
            objectMesh.SetVertices(vertexBuffer);
            objectMesh.SetUVs(0,UVs);
            objectMesh.SetNormals(normalsBuffer);
            objectMesh.SetIndices(indexBuffer, MeshTopology.Triangles, 0);

            MeshFilter filter = childObject.AddComponent<MeshFilter>();
            filter.sharedMesh = objectMesh;
            MeshRenderer childRenderer = childObject.AddComponent<MeshRenderer>();
            childRenderer.material.color = Color.white;
            childObject.transform.SetParent(newObject.transform);
            childObject.name = childName;

            //PrefabUtility.SaveAsPrefabAsset(childObject, Application.dataPath + "/Models/" + childName + ".prefab");
            //AssetDatabase.Refresh();  
        }  

        return newObject;      
    }

    //[MenuItem("SWBF2/Import Models", false, 1)]
    public static void ImportModels(Level level)
    {
        Model[] models = level.GetModels();
        
        int i = 0;
        foreach (Model model in models)
        {
            if (model.Name.Contains("LOWD")) continue;

            GameObject newObject = ModelLoader.GameObjectFromModel(model);

            PrefabUtility.SaveAsPrefabAssetAndConnect(newObject, Application.dataPath + "/Models/" + newObject.name + ".prefab",  InteractionMode.UserAction);
            AssetDatabase.Refresh();  
        } 
    }
}
