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

    public static void ResetDB()
    {
        materialDataBase.Clear();
    }


    public static Material GetMaterial(string texName, uint matFlags)
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

                Texture2D importedTex = TextureLoader.ImportTexture(texName);
                if (importedTex != null)
                {
                    material.mainTexture = importedTex;
                }

                materialDataBase[materialName] = material;
            }

            return materialDataBase[materialName];
        }
    }


    public static bool AddModelComponentsHierarchical(ref GameObject newObject, Model model)
    {
        LibSWBF2.Wrappers.Bone[] hierarchy = model.GetSkeleton();
        Dictionary<string, Transform> hierarchyMap = new Dictionary<string, Transform>();

        foreach (var node in hierarchy)
        {
            var nodeTransform = new GameObject(node.name).transform;
            nodeTransform.localRotation = UnityUtils.QuatFromLibSkel(node.rotation);
            nodeTransform.localPosition = UnityUtils.Vec3FromLibSkel(node.location);
            hierarchyMap[node.name] = nodeTransform;
        }

        foreach (var node in hierarchy)
        {   
            if (node.parentName.Equals(""))
            {
                hierarchyMap[node.name].SetParent(newObject.transform, false);
            }
            else 
            {
                hierarchyMap[node.name].SetParent(hierarchyMap[node.parentName], false);   
            }
        }

        Dictionary<string, List<Segment>> segmentMap = new Dictionary<string, List<Segment>>();
        foreach (var segment in model.GetSegments())
        {
            string boneName = segment.GetBone();

            if (boneName.Equals("")) continue;

            if (!segmentMap.ContainsKey(boneName))
            {
                segmentMap[boneName] = new List<Segment>();
            }
            
            segmentMap[boneName].Add(segment);
        }


        foreach (string boneName in segmentMap.Keys)
        {
            GameObject boneObj = hierarchyMap[boneName].gameObject;
            List<Segment> segments = segmentMap[boneName];

            Mesh mesh = new Mesh();
            Material[] mats = new Material[segments.Count];

            mesh.subMeshCount = segments.Count;

            int totalLength = 0;
            foreach (Segment seg in segments)
            {
                totalLength += (int) seg.GetVertexBufferLength();
            }

            Vector3[] positions = new Vector3[totalLength];
            Vector3[] normals = new Vector3[totalLength];
            Vector2[] texcoords = new Vector2[totalLength];
            int[] offsets = new int[segments.Count];

            int dataOffset = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                Segment seg = segments[i];

                // Handle material data
                string texName = seg.GetMaterialTexName();
                uint matFlags = seg.GetMaterialFlags();
                mats[i] = GetMaterial(texName, matFlags);

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

            int j = 0;
            foreach (Segment seg in segments)
            {
                mesh.SetTriangles(seg.GetIndexBuffer(), j, true, offsets[j]);
                j++;
            }

            MeshFilter filter = boneObj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = boneObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = mats;

            boneObj.transform.localScale = new UnityEngine.Vector3(1.0f,1.0f,1.0f);
        }

        return true;
    }



    public static bool AddModelComponents(ref GameObject newObject, string modelName)
    {   
        Model model = CentralLoader.GetModel(modelName);

        if (model == null)
        {
            Debug.Log(String.Format("ERROR: Failed to load model: {0}", modelName));
            return false;
        }

        if (model.IsSkeletalMesh)
        {
            Debug.Log(String.Format("Setting up skinned mesh + renderer for " + model.Name));
        }

        if (model.HasNonTrivialHierarchy && !model.IsSkeletalMesh)
        {
            return AddModelComponentsHierarchical(ref newObject, model);
        }

        Mesh mesh = new Mesh();

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
        BoneWeight1[] weights = new BoneWeight1[model.IsSkeletalMesh ? totalLength * 4 : 0];
        int[] offsets = new int[segments.Length];

        int dataOffset = 0;

        for (int i = 0; i < segments.Length; i++)
        {
            Segment seg = segments[i];

            // Handle material data
            string texName = seg.GetMaterialTexName();
            uint matFlags = seg.GetMaterialFlags();
            mats[i] = GetMaterial(texName, matFlags);

            // Handle vertex data
            var libVerts = seg.GetVertexBuffer();

            UnityUtils.ConvertSpaceAndFillVec3(libVerts, positions, dataOffset, false);
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetNormalsBuffer(), normals, dataOffset, false);
            UnityUtils.FillVec2(seg.GetUVBuffer(), texcoords, dataOffset);

            if (model.IsSkeletalMesh)
            {
                var libWeights = seg.GetVertexWeights(); 
                Debug.Log(String.Format("Model: {2}, Verts length: {0}, Weights length: {1}", libVerts.Length, libWeights.Length, modelName));
                UnityUtils.FillBoneWeights(libWeights, weights, dataOffset * 4, (int) (libWeights.Length / seg.GetVertexBufferLength()));
            }

            offsets[i] = dataOffset;

            dataOffset += (int) seg.GetVertexBufferLength();
        }

        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0,texcoords);

        if (model.IsSkeletalMesh)
        {
            //Debug.Log("Num weights: " + weights.Length.ToString() + " Num verts: " + positions.Length.ToString());

            byte[] bonesPerVertex = new byte[totalLength];
            for (int i = 0; i < totalLength; i++)
            {
                bonesPerVertex[i] = 4;
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

                
                var prim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                prim.name = curBoneSWBF.name;
                var boneTransform = prim.transform; 
                

                //var boneTransform = new GameObject(curBoneSWBF.name).transform;

                boneTransform.localRotation = UnityUtils.QuatFromLibSkel(curBoneSWBF.rotation);
                boneTransform.localPosition = UnityUtils.Vec3FromLibSkel(curBoneSWBF.location);

                transformMap[curBoneSWBF.name] = boneTransform;
            }

            //Debug.Log("\tSetting bind poses");


            /*
            Set bones
            */
            Transform[] bones = new Transform[bonesSWBF.Length];

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                //Debug.Log("\t\tSetting bindpose of " + curBoneSWBF.name + " Parent name = " + curBoneSWBF.parentName);
                bones[boneNum] = transformMap[curBoneSWBF.name];
                bones[boneNum].SetParent(curBoneSWBF.parentName != null && curBoneSWBF.parentName != "" && !curBoneSWBF.parentName.Equals(curBoneSWBF.name) ? transformMap[curBoneSWBF.parentName] : newObject.transform, false);
            }


            /*
            Set bindposes...
            */
            Matrix4x4[] bindPoses = new Matrix4x4[bonesSWBF.Length];

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                bindPoses[boneNum] = bones[boneNum].worldToLocalMatrix * bones[boneNum].parent.localToWorldMatrix;
                //bindPoses[boneNum] = Matrix4x4.identity;
            }



            mesh.bindposes = bindPoses;

            skinRenderer.bones = bones;
            skinRenderer.sharedMesh = mesh;
            skinRenderer.sharedMaterials = mats;

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                transformMap[curBoneSWBF.name].localRotation = UnityUtils.QuatFromLibSkel(curBoneSWBF.rotation);
                transformMap[curBoneSWBF.name].localPosition = UnityUtils.Vec3FromLibSkel(curBoneSWBF.location);
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

        newObject.transform.localScale = new UnityEngine.Vector3(1.0f,1.0f,1.0f);

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
