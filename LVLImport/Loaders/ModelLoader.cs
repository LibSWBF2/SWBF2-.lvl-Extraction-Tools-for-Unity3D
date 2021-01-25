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

public class ModelLoader : Loader {

    public static bool SaveAssets = false;
    public static Dictionary<string, List<Mesh>> meshDataBase = new Dictionary<string, List<Mesh>>();


    public static void ResetDB()
    {
        meshDataBase.Clear();
    }


    private static Mesh GetMeshFromSegments(Segment[] segments)
    {
        Mesh mesh = new Mesh();
        mesh.subMeshCount = segments.Length;

        int totalLength = (int) segments.Sum(item => item.GetVertexBufferLength());

        Vector3[] positions = new Vector3[totalLength];
        Vector3[] normals = new Vector3[totalLength];
        Vector2[] texcoords = new Vector2[totalLength];
        int[] offsets = new int[segments.Length];

        int dataOffset = 0, i = 0;
        foreach (Segment seg in segments)
        {
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetVertexBuffer(), positions, dataOffset, true);
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetNormalsBuffer(), normals, dataOffset, true);
            UnityUtils.FillVec2(seg.GetUVBuffer(), texcoords, dataOffset);

            offsets[i++] = dataOffset;
            dataOffset += (int) seg.GetVertexBufferLength();
        }

        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0,texcoords);
        
        i = 0;
        foreach (Segment seg in segments)
        {
            //Reverse winding order since we flip handedness of model data
            int[] rewound = UnityUtils.ReverseWinding(seg.GetIndexBuffer());
            mesh.SetTriangles(rewound, i, true, offsets[i]);
            i++;
        }

        return mesh;
    } 





    private static bool AddHierarchy(ref GameObject newObject, Model model, out Dictionary<string, Transform> skeleton)
    {
        LibBone[] hierarchy = model.GetSkeleton();
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

        skeleton = hierarchyMap;
        return true;
    }



    private static int AddWeights(ref GameObject obj, Model model, ref Mesh mesh, bool broken = false)
    {
        var segments = (from segment in model.GetSegments() where segment.GetBone().Equals("") select segment).ToArray(); 

        int totalLength = (int) segments.Sum(item => item.GetVertexBufferLength());
        int txStatus = segments.Sum(item => item.IsPretransformed() ? 1 : 0);

        if (txStatus != 0 && txStatus != segments.Length)
        {
            Debug.LogError(String.Format("Model {0} has heterogeneous pretransformation!", model.Name));
            return 0;
        }

        byte bonesPerVert = (byte) (txStatus == 0 ? 3 : 1);  

        BoneWeight1[] weights = new BoneWeight1[totalLength * bonesPerVert];

        int dataOffset = 0;
        foreach (Segment seg in segments)
        {           
            UnityUtils.FillBoneWeights(seg.GetVertexWeights(), weights, dataOffset, broken ? -1 : 0);            
            dataOffset += (int) seg.GetVertexBufferLength() * bonesPerVert;
        }
        var weightsArray = new NativeArray<BoneWeight1>(weights, Allocator.Temp);

        byte[] bonesPerVertex = Enumerable.Repeat<byte>(bonesPerVert, totalLength).ToArray();
        var bonesPerVertexArray = new NativeArray<byte>(bonesPerVertex, Allocator.Temp);

        mesh.SetBoneWeights(bonesPerVertexArray, weightsArray);

        return (int) bonesPerVert;
    }




    public static bool AddModelComponentsHierarchical(ref GameObject newObject, List<Segment> segments,
                                                    Dictionary<string, Transform> skeleton)
    {

        Dictionary<string, List<Segment>> segmentMap = new Dictionary<string, List<Segment>>();
        foreach (var segment in segments)
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
            GameObject boneObj = skeleton[boneName].gameObject;
            List<Segment> mappedSegments = segmentMap[boneName];

            MeshFilter filter = boneObj.AddComponent<MeshFilter>();
            filter.sharedMesh = GetMeshFromSegments(mappedSegments.ToArray());

            MeshRenderer renderer = boneObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = (from segment in mappedSegments select MaterialLoader.LoadMaterial(segment.GetMaterial())).ToArray();
        }

        return true;
    }


    public static bool AddModelComponents(ref GameObject newObject, string modelName)
    {
        return AddModelComponents(ref newObject, container.FindWrapper<Model>(modelName));
    }



    public static bool AddModelComponents(ref GameObject newObject, Model model)
    {   
        if (model == null)
        {
            Debug.LogError(String.Format("Failed to load model: {0}", model.Name));
            return false;
        }

        if (!AddHierarchy(ref newObject, model, out Dictionary<string, Transform> skeleton))
        {
            return false;
        }

        //if (model.HasNonTrivialHierarchy && !model.IsSkeletalMesh)
        //{
        //    return AddModelComponentsHierarchical(ref newObject, (from segment in model.GetSegments() where !seg.GetBone().Equals("")).ToList(), skeleton);
        //}

        AddModelComponentsHierarchical(ref newObject, (from segment in model.GetSegments() where !segment.GetBone().Equals("") select segment).ToList(), skeleton);
        
        if (!model.IsSkeletalMesh) return true;


        Segment[] segments = (from segment in model.GetSegments() where segment.GetBone().Equals("") select segment).ToArray(); 
        Mesh mesh = GetMeshFromSegments(segments);
        UMaterial[] mats = (from segment in segments select MaterialLoader.LoadMaterial(segment.GetMaterial())).ToArray();

        if (model.IsSkeletalMesh)
        {
            int skinType = AddWeights(ref newObject, model, ref mesh, model.IsSkeletonBroken);
            if (skinType == 0)
            {
                //Debug.LogWarning("Failed to add weights....");
            }

            SkinnedMeshRenderer skinRenderer = newObject.AddComponent<SkinnedMeshRenderer>();
            LibBone[] bonesSWBF = model.GetSkeleton();

            /*
            Set bones
            */
            Transform[] bones = new Transform[bonesSWBF.Length];
            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                //Debug.Log("\t\tSetting bindpose of " + curBoneSWBF.name + " Parent name = " + curBoneSWBF.parentName);
                bones[boneNum] = skeleton[curBoneSWBF.name];
                bones[boneNum].SetParent(curBoneSWBF.parentName != null && curBoneSWBF.parentName != "" && !curBoneSWBF.parentName.Equals(curBoneSWBF.name) ? skeleton[curBoneSWBF.parentName] : newObject.transform, false);
            }

            /*
            Set bindposes...
            */
            Matrix4x4[] bindPoses = new Matrix4x4[bonesSWBF.Length];
            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                if (skinType == 1)
                {
                    //For pretransformed skins...
                    bindPoses[boneNum] = Matrix4x4.identity;
                }
                else 
                {
                    bindPoses[boneNum] = bones[boneNum].worldToLocalMatrix * bones[0].parent.localToWorldMatrix;
                }
                //But what works for sarlacctentacle?
            }

            mesh.bindposes = bindPoses;

            skinRenderer.bones = bones;
            skinRenderer.sharedMesh = mesh;
            skinRenderer.sharedMaterials = mats;

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                skeleton[curBoneSWBF.name].localRotation = UnityUtils.QuatFromLibSkel(curBoneSWBF.rotation);
                skeleton[curBoneSWBF.name].localPosition = UnityUtils.Vec3FromLibSkel(curBoneSWBF.location);
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
