using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
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



    public static bool AddModelComponents(Level level, ref GameObject newObject, Model model)
    {
        Mesh mesh = new Mesh();

        if (model.IsSkeletalMesh)
        {
            Debug.Log("Setting up skinned mesh + renderer for " + model.Name);
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
        BoneWeight1[] weights = new BoneWeight1[model.IsSkeletalMesh ? totalLength : 0];
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

            if (model.IsSkeletalMesh)
            {
                UnityUtils.FillBoneWeights(seg.GetVertexWeights(), weights, dataOffset);
            }

            offsets[i] = dataOffset;

            dataOffset += (int) seg.GetVertexBufferLength();
        }

        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0,texcoords);

        if (model.IsSkeletalMesh)
        {
            Debug.Log("Num weights: " + weights.Length.ToString() + " Num verts: " + positions.Length.ToString());

            byte[] bonesPerVertex = new byte[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                bonesPerVertex[i] = 1;
            }

            var bonesPerVertexArray = new NativeArray<byte>(bonesPerVertex, Allocator.Temp);
            var weightsArray = new NativeArray<BoneWeight1>(weights, Allocator.Temp);

            mesh.SetBoneWeights(bonesPerVertexArray, weightsArray);
        }

        int j = 0;
        foreach (Segment seg in segments)
        {
            mesh.SetTriangles(seg.GetIndexBuffer(), j, true, offsets[j]);
            j++;
        }

        if (model.IsSkeletalMesh)
        {
            SkinnedMeshRenderer skinRenderer = newObject.AddComponent<SkinnedMeshRenderer>();

            LibSWBF2.Wrappers.Bone[] bonesSWBF = model.GetSkeleton();
            Dictionary<string, Transform> transformMap = new Dictionary<string, Transform>();

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                var boneTransform = new GameObject(curBoneSWBF.name).transform;

                //boneTransform.localRotation = UnityUtils.QuatFromLib(curBoneSWBF.rotation);
                //boneTransform.localPosition = UnityUtils.Vec3FromLib(curBoneSWBF.location);

                transformMap[curBoneSWBF.name] = boneTransform;
            }

            Debug.Log("\tSetting bind poses");

            Transform[] bones = new Transform[bonesSWBF.Length];
            Matrix4x4[] bindPoses = new Matrix4x4[bonesSWBF.Length];

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];

                Debug.Log("\t\tSetting bindpose of " + curBoneSWBF.name + " Parent name = " + curBoneSWBF.parentName);

                bones[boneNum] = transformMap[curBoneSWBF.name];
                bones[boneNum].SetParent(curBoneSWBF.parentName != null && curBoneSWBF.parentName != "" && !curBoneSWBF.parentName.Equals(curBoneSWBF.name) ? transformMap[curBoneSWBF.parentName] : newObject.transform, false);

                bindPoses[boneNum] = bones[boneNum].worldToLocalMatrix * bones[boneNum].parent.localToWorldMatrix;
            }

            mesh.bindposes = bindPoses;

            skinRenderer.bones = bones;
            skinRenderer.sharedMesh = mesh;
            skinRenderer.sharedMaterials = mats;

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                transformMap[curBoneSWBF.name].localRotation = UnityUtils.QuatFromLib(curBoneSWBF.rotation);
                transformMap[curBoneSWBF.name].localPosition = UnityUtils.Vec3FromLib(curBoneSWBF.location);
            }
        }
        else
        {
            MeshFilter filter = newObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = newObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = mats;
        }


        CollisionMesh collMesh = model.GetCollisionMesh();
        if (collMesh != null)
        {
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
                return false;
            }            
        }

        return true;      
    }


    public static void ImportModels(Level level)
    {
        Model[] models = level.GetModels();
        
        foreach (Model model in models)
        {
            if (model.Name.Contains("LOWD")) continue;
            //GameObject newObject = ModelLoader.GameObjectFromModel(level, model);
        } 
    }
}
