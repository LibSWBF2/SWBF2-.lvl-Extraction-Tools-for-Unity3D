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
//using UVec3 = UnityEngine.Vector3;

public class ModelLoader : Loader {

    //Doesn't actually save meshes, not sure where to go with this atm...
    public static bool SaveAssets = false;
    public static Dictionary<string, List<Mesh>> meshDataBase = new Dictionary<string, List<Mesh>>();
    public static void ResetDB() { meshDataBase.Clear(); }


    // Cylinder collision mesh as substitute for cylinder primitive.
    // Perhaps a gameobject with three children, each having a box collider, rotated to 
    // form a 6 sided cylinder would be more performant?  
    private static Mesh cylColl = (Mesh)AssetDatabase.LoadAssetAtPath("Assets/LVLImport/ConversionAssets/CylinderCollider.obj", typeof(Mesh));


    /*
    Extracts static mesh data from array of segments
    */

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
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetVertexBuffer<Vector3>(), positions, dataOffset, true);
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetNormalsBuffer<Vector3>(), normals, dataOffset, true);
            UnityUtils.FillVec2(seg.GetUVBuffer<Vector2>(), texcoords, dataOffset);

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
            ushort[] indexBuffer = seg.GetIndexBuffer();
            UnityUtils.ReverseWinding(indexBuffer);
            mesh.SetTriangles(indexBuffer, i, true, offsets[i]);
            i++;
        }

        return mesh;
    } 


    // Straightforward
    private static bool AddSkeleton(GameObject newObject, Model model, out Dictionary<string, Transform> skeleton)
    {
        LibBone[] hierarchy = model.skeleton;
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



    /*
    Will keep vertex weights separate from static mesh handling until the 
    various edge cases (see git issue about sarlacc, ATTE, and Jabba) regarding weights and
    skeletons are sorted out.
    */

    private static int AddWeights(GameObject obj, Model model, Mesh mesh)
    {
        var segments = (from segment in model.GetSegments() where segment.boneName.Equals("") select segment).ToArray(); 

        int totalLength = (int) segments.Sum(item => item.GetVertexBufferLength());
        int txStatus = segments.Sum(item => item.isPretransformed ? 1 : 0);

        if (txStatus != 0 && txStatus != segments.Length)
        {
            Debug.LogError(String.Format("Model {0} has heterogeneous pretransformation!", model.name));
            return 0;
        }

        byte bonesPerVert = (byte) (txStatus == 0 ? 3 : 1);  
        bool broken = model.isSkeletonBroken;

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


    /*
    Gathers segments by their node in the model's skeleton and creates a non-weighted mesh for each
    node with attached segments.
    */

    public static bool AddStaticMeshes(GameObject newObject, Model model,
                                                    Dictionary<string, Transform> skeleton)
    {
        List<Segment> segments = (from segment in model.GetSegments() where !segment.boneName.Equals("") select segment).ToList();
        Dictionary<string, List<Segment>> segmentMap = new Dictionary<string, List<Segment>>();
        foreach (var segment in segments)
        {
            string boneName = segment.boneName;

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
            renderer.sharedMaterials = (from segment in mappedSegments select MaterialLoader.LoadMaterial(segment.material)).ToArray();
        }

        return true;
    }


    /*
    Finds segments which are not attached to skeleton nodes, ie ones that are skinned,
    and creates a weighted mesh from them. 
    */

    private static bool AddSkinningComponents(GameObject newObject, Model model, Dictionary<string, Transform> skeleton)
    {
        Segment[] skinnedSegments = (from segment in model.GetSegments() where segment.boneName.Equals("") select segment).ToArray();
        Mesh mesh = GetMeshFromSegments(skinnedSegments.ToArray());
        UMaterial[] mats = (from segment in skinnedSegments select MaterialLoader.LoadMaterial(segment.material)).ToArray();

        int skinType = AddWeights(newObject, model, mesh);
        if (skinType == 0)
        {
            //Debug.LogWarning("Failed to add weights....");
        }

        //Below, we handle 
        SkinnedMeshRenderer skinRenderer = newObject.AddComponent<SkinnedMeshRenderer>();
        LibBone[] bonesSWBF = model.skeleton;

        /*
        Set bones
        */
        Transform[] bones = new Transform[bonesSWBF.Length];
        for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
        {
            var curBoneSWBF = bonesSWBF[boneNum];
            bones[boneNum] = skeleton[curBoneSWBF.name];

            //Messy, will fix once skeleton edge cases are sorted out
            //bones[boneNum].SetParent(curBoneSWBF.parentName != null && curBoneSWBF.parentName != "" && !curBoneSWBF.parentName.Equals(curBoneSWBF.name) ? skeleton[curBoneSWBF.parentName] : newObject.transform, false);
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

        return true;
    }




    /*
    Adds skeleton, attaches static meshes/renderers to skeleton bones, and a skinned mesh/renderer to the root
    object if present.
    */

    public static bool AddModelComponents(GameObject newObject, Model model)
    {   
        if (model == null)
        {
            Debug.LogError(String.Format("Failed to load model: {0}", model.name));
            return false;
        }

        if (!AddSkeleton(newObject, model, out Dictionary<string, Transform> skeleton))
        {
            return false;
        }

        AddStaticMeshes(newObject, model, skeleton);
        
        if (model.isSkinned)
        {
            AddSkinningComponents(newObject, model, skeleton);
        }

        return true;
    }

    public static bool AddModelComponents(GameObject newObject, string modelName)
    {
        return AddModelComponents(newObject, container.FindWrapper<Model>(modelName));
    }




    /*
    COLLISION ATTACHMENT FUNCTIONS BELOW
    */


    /*
    Adds collision primitives.  If a set of specific collider names are
    passed (see ClassLoader.LoadGeneralClass), they will be chosen 
    from all the model's colliders.  If not, the model's ordinance
    collision primitives will be used. 
    */ 

    public static bool AddCollisionPrimitives(GameObject newObject, Model model, HashSet<string> colliderNames = null)
    {
        colliderNames = null;
        //Get list of primitives, requested or found.
        List<CollisionPrimitive> prims = new List<CollisionPrimitive>();
        if (colliderNames == null || colliderNames.Count == 0)
        {
            // 1 = Ordinance
            prims = new List<CollisionPrimitive>(model.GetPrimitivesMasked(1));
        }
        else 
        {
            CollisionPrimitive[] allColliderNames = model.GetPrimitivesMasked();
            foreach (var prim in allColliderNames)
            {
                if (colliderNames.Contains(prim.name))
                {
                    prims.Add(prim);
                }
            }
        }
       
        // Instantiate and attach converted primitives
        foreach (var prim in prims) 
        {
            string parentBone = prim.parentName;
            Transform boneTx = null;

            if (parentBone.Equals(""))
            {
                boneTx = newObject.transform;
            }
            else 
            {
                boneTx = UnityUtils.FindChildTransform(newObject.transform, parentBone);
            }

            if (boneTx == null) continue;

            GameObject primObj = new GameObject(prim.name);
            primObj.transform.localPosition = UnityUtils.Vec3FromLibSkel(prim.position);
            primObj.transform.localRotation = UnityUtils.QuatFromLibSkel(prim.rotation);
            primObj.transform.SetParent(boneTx, false);

            switch (prim.primitiveType)
            {
                case 4:
                    BoxCollider boxColl = primObj.AddComponent<BoxCollider>();
                    if (prim.GetCubeDims(out float x, out float y, out float z))
                    {
                        boxColl.size = new Vector3(2.0f*x,2.0f*y,2.0f*z);
                    }
                    break;

                // Instantiate cylinder asset and use in convex mesh collider
                case 2:
                    if (prim.GetCylinderDims(out float r, out float h))
                    {
                        MeshCollider meshColl = primObj.AddComponent<MeshCollider>();
                        meshColl.sharedMesh = cylColl;
                        meshColl.convex = true;
                        primObj.transform.localScale = new UnityEngine.Vector3(r,h,r);
                    }
                    break;
                
                case 1:
                    SphereCollider sphereColl = primObj.AddComponent<SphereCollider>();
                    if (prim.GetSphereRadius(out float rad))
                    {
                        sphereColl.radius = rad;
                    }
                    break;
                
                // This happens, not sure what to make of it, but 
                // all prims of this type have zeroed fields.
                default:
                    Debug.LogWarning(model.name + ": Unknown collision type encountered");
                    break;
            }
        }

        return true;
    }


    /*
    Adds all collision components.  If the model has a CollisionMesh, 
    it is loaded and attached to the root object.  This is problematic,
    as the SWBF2 engine can use concave mesh colliders for non static objects.
    
    After adding the collision mesh, ordinance collision primitives are
    attached.
    */

    public static bool AddCollisionComponents(GameObject newObject, string modelName, HashSet<string> colliderNames)
    {
        if (modelName.Equals("")) return false;

        Model model = null;
        try {
            model = container.FindWrapper<Model>(modelName);
        }
        catch { 
            return false;
        }

        if (model == null) 
        {
            return false;
        }

        CollisionMesh collMesh = null;
        try {
            collMesh = model.GetCollisionMesh();
        }
        catch 
        {
            Debug.LogError(modelName + ": Error in process of CollisionMesh fetch...");
            return false;
        }

        if (collMesh != null)
        {
            ushort[] indBuffer = collMesh.GetIndices();

            try {
                if (indBuffer.Length > 2)
                {
                    Mesh collMeshUnity = new Mesh();
                    collMeshUnity.vertices = UnityUtils.FlipXCoords(collMesh.GetVertices<Vector3>());
                    collMeshUnity.SetTriangles(indBuffer, 0);

                    MeshCollider meshCollider = newObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = collMeshUnity;
                }
            } 
            catch
            {
                Debug.LogError(modelName + ": Error while creating mesh collider...");
            } 
        }

        AddCollisionPrimitives(newObject, model, colliderNames); 
        
        return true;      
    }
}
