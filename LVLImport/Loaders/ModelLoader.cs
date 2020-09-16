using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class ModelLoader : ScriptableObject {

    //static Material swbf2Mat = (Material) AssetDatabase.LoadAssetAtPath("Assets/Materials/swbf2.mat", typeof(Material));

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

    public static GameObject GameObjectFromModel(Level level, Model model)
    {
        GameObject newObject = new GameObject();
        newObject.AddComponent<MeshRenderer>();

        Segment[] segments;

        try {
            newObject.name = model.Name;
        } catch (Exception e)
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

			//Debug.Log("Segment topology: " + seg.GetTopology());
			//Debug.Log("Num verts: " + seg.GetVertexBuffer().Length / 3);
			//Debug.Log("Index buffer length: " + seg.GetIndexBuffer().Length);

            if (texName == "")
            {
                continue;
            }

            string childName = newObject.name + "_segment_" + segCount++;

            //Handle mesh
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

            //AssetDatabase.CreateAsset(objectMesh, "Assets/Meshes/" + childName + ".asset");
            //AssetDatabase.SaveAssets();
            //AssetDatabase.Refresh();

            MeshFilter filter = childObject.AddComponent<MeshFilter>();
            filter.sharedMesh = objectMesh;
          
            //Handle material
            Texture2D importedTex = TextureLoader.ImportTexture(level, texName);
            //Material tempMat = new Material();

            MeshRenderer childRenderer = childObject.AddComponent<MeshRenderer>();
            //childRenderer.material = tempMat;

            if (importedTex == null)
            {
                childRenderer.material.color = Color.black;
            }
            else 
            {
                childRenderer.material.mainTexture = importedTex;
            }

            //AssetDatabase.CreateAsset(tempMat, "Assets/Materials/" + childName + "_mat.mat");
            //AssetDatabase.SaveAssets();
            //AssetDatabase.Refresh();

            childObject.transform.SetParent(newObject.transform);
            childObject.name = childName;

            //PrefabUtility.SaveAsPrefabAsset(childObject, Application.dataPath + "/Models/" + childName + ".prefab");
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

           //if (i++ > 10) return;

            if (model.Name.Contains("LOWD")) continue;

            GameObject newObject = ModelLoader.GameObjectFromModel(level, model);

            //PrefabUtility.SaveAsPrefabAssetAndConnect(newObject, "Assets/Models/" + newObject.name + ".prefab",  InteractionMode.UserAction);
            //AssetDatabase.SaveAssets();
            //AssetDatabase.Refresh();  
        } 
    }
}
