using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class ModelLoader : ScriptableObject {

    public static Material swbf2Mat = (Material) AssetDatabase.LoadAssetAtPath("Assets/Materials/swbf2.mat", typeof(Material));


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

            string childName = newObject.name + "_segment_" + segCount++;// + "_" + seg.GetMaterialFlags();

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

            //AssetDatabase.CreateAsset(objectMesh, "Assets/Meshes/" + childName + ".asset");
            //AssetDatabase.SaveAssets();
            //AssetDatabase.Refresh();

            MeshFilter filter = childObject.AddComponent<MeshFilter>();
            filter.sharedMesh = objectMesh;
          
            //Handle material
            Texture2D importedTex = TextureLoader.ImportTexture(level, texName);
            //Material tempMat = new Material();

            MeshRenderer childRenderer = childObject.AddComponent<MeshRenderer>();
            
            Material newMat = new Material( swbf2Mat );
            newMat.name = "mat_" + texName;
            newMat.SetInt("_ZWrite", 1);

            childRenderer.sharedMaterial = newMat;

            if (importedTex == null)
            {
                childRenderer.sharedMaterial.color = Color.black;
            }
            else 
            {
                childRenderer.sharedMaterial.mainTexture = importedTex;
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
