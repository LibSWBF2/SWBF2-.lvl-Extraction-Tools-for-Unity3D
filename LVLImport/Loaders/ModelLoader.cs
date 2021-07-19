using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
#if !LVLIMPORT_NO_EDITOR
using UnityEditor;
#endif

using LibSWBF2.Wrappers;
using LibSWBF2.Enums;

using LibMaterial = LibSWBF2.Wrappers.Material;
using UMaterial = UnityEngine.Material;
using LibBone = LibSWBF2.Wrappers.Bone;


public class ModelLoader : Loader {

    public static ModelLoader Instance { get; private set; } = null;

    public PhysicMaterial PhyMat;

    public Dictionary<ECollisionMaskFlags, int> CollFlagToUnityLayer = new Dictionary<ECollisionMaskFlags, int>
    {
        { ECollisionMaskFlags.All,      0},
        { ECollisionMaskFlags.Building, 0},
        { ECollisionMaskFlags.Flag,     0},
        { ECollisionMaskFlags.Ordnance, 0},
        { ECollisionMaskFlags.Soldier,  0},
        { ECollisionMaskFlags.Terrain,  0},
        { ECollisionMaskFlags.Vehicle,  0},
    };

    // Objects containing a substring in this map (case insensitive), will get their collision flags overriden
    // when collision is applied. Why? Because e.g. in geo1 there are some giant vehicle collisions,
    // that got all their flags set... Idk about other cases at this point.
    Dictionary<string, ECollisionMaskFlags> FlagOverrides = new Dictionary<string, ECollisionMaskFlags>
    {
        { "vehicle", ECollisionMaskFlags.Vehicle }
    };

    static ModelLoader()
    {
        Instance = new ModelLoader();
    }

    public void ResetDB()
    {
        //todox
    }

    // Cylinder collision mesh as substitute for cylinder primitive.
    // Perhaps a gameobject with three children, each having a box collider, rotated to 
    // form a 6 sided cylinder would be more performant?  
    public readonly static Mesh CylinderCollision = Resources.Load<Mesh>("CylinderCollider");



    public Model GetModelWrapper(string name)
    {
        Model model = null;
        try {
            model = container.Get<Model>(name);
        }
        catch { }
        return model; 
    }


    /*
    Extracts static mesh data from array of segments
    */

    private Mesh GetMeshFromSegments(Segment[] segments, string meshName = "")
    {
        Mesh mesh = new Mesh();

#if !LVLIMPORT_NO_EDITOR
        if (SaveAssets)
        {
            AssetDatabase.CreateAsset(mesh, Path.Combine(SaveDirectory, meshName + ".mesh")); 
        }
#endif
        mesh.subMeshCount = segments.Length;

        int totalLength = (int) segments.Sum(item => item.GetVertexBufferLength());

        Vector3[] positions = new Vector3[totalLength];
        Vector3[] normals = new Vector3[totalLength];
        Vector2[] texcoords = new Vector2[totalLength];
        int[] offsets = new int[segments.Length];

        int dataOffset = 0, i = 0;
        foreach (Segment seg in segments)
        {
            int vBufLength = (int) seg.GetVertexBufferLength();

            UnityUtils.ConvertSpaceAndFillVec3(seg.GetVertexBuffer<Vector3>(), positions, dataOffset);
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetNormalsBuffer<Vector3>(), normals, dataOffset);
            Array.Copy(seg.GetUVBuffer<Vector2>(), 0, texcoords, dataOffset, vBufLength);

            offsets[i++] = dataOffset;
            dataOffset += vBufLength;
        }

        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0,texcoords);

        i = 0;
        foreach (Segment seg in segments)
        {
            mesh.SetTriangles(UnityUtils.ReverseWinding(seg.GetIndexBuffer()), i, true, offsets[i]);
            i++;
        }

        return mesh;
    } 


    // Straightforward
    private bool AddSkeleton(GameObject newObject, Model model, out Dictionary<string, Transform> skeleton)
    {
        skeleton = new Dictionary<string, Transform>();

        ReadOnlyCollection<LibBone> hierarchy = model.Skeleton;
        if (hierarchy == null) return false;

        foreach (var node in hierarchy)
        {
            var nodeTransform = new GameObject(node.Name.ToLower()).transform;
            nodeTransform.localRotation = UnityUtils.QuatFromLibSkel(node.Rotation);
            nodeTransform.localPosition = UnityUtils.Vec3FromLibSkel(node.Location);
            skeleton[node.Name] = nodeTransform;
        }

        foreach (var node in hierarchy)
        {   
            if (node.ParentName.Equals(""))
            {
                skeleton[node.Name].SetParent(newObject.transform, false);
            }
            else 
            {
                skeleton[node.Name].SetParent(skeleton[node.ParentName], false);   
            }
        }

        return true;
    }



    /*
    Will keep vertex weights separate from static mesh handling until the 
    various edge cases (see git issue about sarlacc, ATTE, and Jabba) regarding weights and
    skeletons are sorted out.
    */

    private int AddWeights(GameObject obj, Model model, Mesh mesh)
    {
        var segments = (from segment in model.GetSegments() where segment.BoneName.Equals("") select segment).ToArray(); 

        int totalLength = (int) segments.Sum(item => item.GetVertexBufferLength());
        int txStatus = segments.Sum(item => item.IsPretransformed ? 1 : 0);

        if (txStatus != 0 && txStatus != segments.Length)
        {
            Debug.LogWarningFormat("Model {0} has heterogeneous pretransformation!  Please tell devs about this!!", model.Name);
            return 0;
        }

        byte bonesPerVert = (byte) (txStatus == 0 ? 3 : 1);  
        bool broken = model.IsSkeletonBroken;

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

    bool AddStaticMeshes(GameObject newObject, Model model, Dictionary<string, Transform> skeleton, bool shadowSensitive, bool unlit)
    {
        List<Segment> segments = (from segment in model.GetSegments() where !segment.BoneName.Equals("") select segment).ToList();
        Dictionary<string, List<Segment>> segmentMap = new Dictionary<string, List<Segment>>();
        foreach (var segment in segments)
        {
            string boneName = segment.BoneName;

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
            filter.sharedMesh = GetMeshFromSegments(mappedSegments.ToArray(), model.Name + "_" + boneName);

            MeshRenderer renderer = boneObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = (from segment in mappedSegments select MaterialLoader.Instance.LoadMaterial(segment.Material, null, unlit)).ToArray();
            renderer.shadowCastingMode = shadowSensitive ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadowSensitive;
        }

        return true;
    }


    /*
    Finds segments which are not attached to skeleton nodes, ie ones that are skinned,
    and creates a weighted mesh from them. 
    */

    private bool AddSkinningComponents(GameObject newObject, Model model, Dictionary<string, Transform> skeleton, string overrideTexture)
    {
        Segment[] skinnedSegments = (from segment in model.GetSegments() where segment.BoneName.Equals("") select segment).ToArray();
        Mesh mesh = GetMeshFromSegments(skinnedSegments.ToArray(), model.Name + "_skin");
        UMaterial[] mats = (from segment in skinnedSegments select MaterialLoader.Instance.LoadMaterial(segment.Material, overrideTexture)).ToArray();

        int skinType = AddWeights(newObject, model, mesh);
        if (skinType == 0)
        {
            //Debug.LogWarning("Failed to add weights....");
        }

        //Below, we handle 
        SkinnedMeshRenderer skinRenderer = newObject.AddComponent<SkinnedMeshRenderer>();
        ReadOnlyCollection<LibBone> bonesSWBF = model.Skeleton;

        /*
        Set bones
        */
        Transform[] bones = new Transform[bonesSWBF.Count];
        for (int boneNum = 0; boneNum < bonesSWBF.Count; boneNum++)
        {
            var curBoneSWBF = bonesSWBF[boneNum];
            bones[boneNum] = skeleton[curBoneSWBF.Name];

            //Messy, will fix once skeleton edge cases are sorted out
            //bones[boneNum].SetParent(curBoneSWBF.parentName != null && curBoneSWBF.parentName != "" && !curBoneSWBF.parentName.Equals(curBoneSWBF.Name) ? skeleton[curBoneSWBF.parentName] : newObject.transform, false);
        }

        /*
        Set bindposes...
        */
        Matrix4x4[] bindPoses = new Matrix4x4[bonesSWBF.Count];
        for (int boneNum = 0; boneNum < bonesSWBF.Count; boneNum++)
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

        for (int boneNum = 0; boneNum < bonesSWBF.Count; boneNum++)
        {
            var curBoneSWBF = bonesSWBF[boneNum];
            skeleton[curBoneSWBF.Name].localRotation = UnityUtils.QuatFromLibSkel(curBoneSWBF.Rotation);
            skeleton[curBoneSWBF.Name].localPosition = UnityUtils.Vec3FromLibSkel(curBoneSWBF.Location);
        }

        return true;
    }




    /*
    Adds skeleton, attaches static meshes/renderers to skeleton bones, and a skinned mesh/renderer to the root
    object if present.
    */

    bool AddModelComponents(GameObject newObject, Model model, string overrideTexture, bool shadowSensitive, bool unlit)
    {   
        if (model == null || newObject == null)
        {
            return false;
        }

        if (!AddSkeleton(newObject, model, out Dictionary<string, Transform> skeleton))
        {
            return false;
        }

        AddStaticMeshes(newObject, model, skeleton, shadowSensitive, unlit);
        
        if (model.IsSkinned)
        {
            AddSkinningComponents(newObject, model, skeleton, overrideTexture);
        }

        return true;
    }

    public bool AddModelComponents(GameObject newObject, string modelName, string overrideTexture=null, bool shadowSensitive=true, bool unlit = false)
    {
        return AddModelComponents(newObject, container.Get<Model>(modelName), overrideTexture, shadowSensitive, unlit);
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

    public bool AddCollisionPrimitives(GameObject newObject, Model model, HashSet<string> colliderNames = null)
    {
        //Get list of primitives, requested or found.
        List<CollisionPrimitive> prims = new List<CollisionPrimitive>();
        if (colliderNames == null || colliderNames.Count == 0)
        {
            prims = new List<CollisionPrimitive>(model.GetPrimitivesMasked(ECollisionMaskFlags.Ordnance));
        }
        else 
        {
            CollisionPrimitive[] allColliderNames = model.GetPrimitivesMasked();
            foreach (var prim in allColliderNames)
            {
                if (colliderNames.Contains(prim.Name))
                {
                    prims.Add(prim);
                }
            }
        }
       
        // Instantiate and attach converted primitives
        foreach (var prim in prims) 
        {
            string parentBone = prim.ParentName;
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

            GameObject primObj = new GameObject(prim.Name);
            primObj.transform.localPosition = UnityUtils.Vec3FromLibSkel(prim.Position);
            primObj.transform.localRotation = UnityUtils.QuatFromLibSkel(prim.Rotation);
            primObj.transform.SetParent(boneTx, false);

            ApplyUnityLayer(primObj, prim.MaskFlags);

            switch (prim.PrimitiveType)
            {
                case ECollisionPrimitiveType.Cube:
                    BoxCollider boxColl = primObj.AddComponent<BoxCollider>();
                    if (prim.GetCubeDims(out float x, out float y, out float z))
                    {
                        boxColl.size = new Vector3(2.0f*x,2.0f*y,2.0f*z);
                    }
                    boxColl.sharedMaterial = PhyMat;
                    break;

                // Instantiate cylinder asset and use in convex mesh collider
                case ECollisionPrimitiveType.Cylinder:
                    if (prim.GetCylinderDims(out float r, out float h))
                    {
                        MeshCollider meshColl = primObj.AddComponent<MeshCollider>();
                        meshColl.sharedMesh = CylinderCollision;
                        meshColl.sharedMaterial = PhyMat;
                        meshColl.convex = true;
                        primObj.transform.localScale = new UnityEngine.Vector3(r,h,r);
                    }
                    break;
                
                case ECollisionPrimitiveType.Sphere:
                    SphereCollider sphereColl = primObj.AddComponent<SphereCollider>();
                    if (prim.GetSphereRadius(out float rad))
                    {
                        sphereColl.radius = rad;
                    }
                    sphereColl.sharedMaterial = PhyMat;
                    break;
                
                // This happens, not sure what to make of it, but 
                // all prims of this type have zeroed fields.
                default:
                    Debug.LogWarning(model.Name + ": Unknown collision type encountered");
                    break;
            }
        }

        return true;
    }


    /*
     * NOTE: 'isStatic' of the given GameObject MUST be set beforehand!
     * Adds all collision components.  If the model has a CollisionMesh, 
     * it is loaded and attached to the root object.  This is problematic,
     * as the SWBF2 engine can use concave mesh colliders for non static objects.
     * 
     * After adding the collision mesh, ordinance collision primitives are
     * attached.
    */

    public bool AddCollisionComponents(GameObject newObject, string modelName, HashSet<string> colliderNames)
    {
        if (modelName.Equals("")) return false;

        Model model = null;
        try {
            model = container.Get<Model>(modelName);
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
            Debug.LogWarning(modelName + ": Error in process of CollisionMesh fetch...");
            return false;
        }

        if (collMesh != null)
        {
            ushort[] indBuffer = collMesh.GetIndices();

            try {
                if (indBuffer.Length > 2)
                {
                    Mesh collMeshUnity = new Mesh();
                    
                    Vector3[] positions = collMesh.GetVertices<Vector3>();
                    UnityUtils.ConvertSpaceAndFillVec3(positions,positions,0);
                    collMeshUnity.vertices = positions;
                    collMeshUnity.SetTriangles(indBuffer, 0);

                    MeshCollider meshCollider = newObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = collMeshUnity;
                    meshCollider.sharedMaterial = PhyMat;
                    meshCollider.convex = !newObject.isStatic;
                }
            } 
            catch
            {
                Debug.LogWarning(modelName + ": Error while creating mesh collider...");
            }

            ApplyUnityLayer(newObject, collMesh.MaskFlags);
        }

        return AddCollisionPrimitives(newObject, model, colliderNames);  
    }

    void ApplyUnityLayer(GameObject obj, ECollisionMaskFlags flags)
    {
        foreach (var flagEntry in FlagOverrides)
        {
            if ((!string.IsNullOrEmpty(flagEntry.Key) && obj.name.IndexOf(flagEntry.Key, 0, StringComparison.OrdinalIgnoreCase) != -1))
            {
                flags = flagEntry.Value;
            }
        }

        bool ApplyIfHasFlag(GameObject obj, ECollisionMaskFlags flags, ECollisionMaskFlags flag)
        {
            if (flags.HasFlag(flag))
            {
                if (CollFlagToUnityLayer.TryGetValue(flag, out int layer))
                {
                    obj.layer = layer;
                    return true;
                }
                else
                {
                    Debug.LogWarning($"No corresponding Unity Layer defined for collision flag: {flags} ({(int)flags}), found in object '{obj.name}'");
                }
            }
            return false;
        }


        // Since in Unity, a GameObject can only have ONE layer, but SWBF2 collisions
        // can have multiple collision flags enabled, let's traverse the flags from
        // least to most significant and apply a corresponding layer respectively.

        if (ApplyIfHasFlag(obj, flags, ECollisionMaskFlags.All)) return;
        if (ApplyIfHasFlag(obj, flags, ECollisionMaskFlags.Flag)) return;
        if (ApplyIfHasFlag(obj, flags, ECollisionMaskFlags.Vehicle)) return;
        if (ApplyIfHasFlag(obj, flags, ECollisionMaskFlags.Soldier)) return;
        if (ApplyIfHasFlag(obj, flags, ECollisionMaskFlags.Building)) return;
        if (ApplyIfHasFlag(obj, flags, ECollisionMaskFlags.Ordnance)) return;
        if (ApplyIfHasFlag(obj, flags, ECollisionMaskFlags.Terrain)) return;
    }
}
