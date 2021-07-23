using System;
using System.IO;
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


    Dictionary<string, SWBFModel> ModelMappingDB = new Dictionary<string, SWBFModel>();
    Dictionary<string, GameObject> ModelDB = new Dictionary<string, GameObject>();

    GameObject ModelDBRoot = new GameObject("ModelDBRoot");


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
        ModelDB.Clear();
        GameObject.Destroy(ModelDBRoot);
        ModelDBRoot = new GameObject("ModelDBRoot");

        ModelMappingDB.Clear();
    }

    // Cylinder collision mesh as substitute for cylinder primitive.
    // Perhaps a gameobject with three children, each having a box collider, rotated to 
    // form a 6 sided cylinder would be more performant?  
    public readonly static Mesh CylinderCollision = Resources.Load<Mesh>("CylinderCollider");



    public SWBFModel GetSWBFModelMapping(GameObject Root, string ModelName)
    {
        if (ModelMappingDB.ContainsKey(ModelName))
        {
            return new SWBFModel(ModelMappingDB[ModelName], Root);
        }

        return null;
    }


    // Until we get the mapping class ready
    public Model GetModelWrapper(string name)
    {
        Model model = null;
        try {
            model = container.Get<Model>(name);
        }
        catch { }
        return model; 
    }

    // Used for quick fx testing
    public List<UMaterial> GetNeededMaterials(Model model)
    {
        List<UMaterial> mats = new List<UMaterial>();
        foreach (Segment seg in model.GetSegments())
        {
            mats.Add(MaterialLoader.Instance.LoadMaterial(seg.Material, ""));
        }
        return mats;
    }

    // '' 
    public Mesh GetFirstMesh(Model model)
    {
        return GetMeshFromSegments(model.GetSegments());
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

    private List<SWBFSegment> AddStaticMeshes(GameObject newObject, Model model, Dictionary<string, Transform> skeleton, bool shadowSensitive, bool unlit)
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

        List<SWBFSegment> SWBFSegments = new List<SWBFSegment>(); 

        foreach (string boneName in segmentMap.Keys)
        {
            GameObject boneObj = skeleton[boneName].gameObject;
            List<Segment> mappedSegments = segmentMap[boneName];

            for (int i = 0; i < mappedSegments.Count; i++)
            {
                SWBFSegments.Add(new SWBFSegment(i, boneObj, mappedSegments[i].Tag));
            }

            MeshFilter filter = boneObj.AddComponent<MeshFilter>();
            filter.sharedMesh = GetMeshFromSegments(mappedSegments.ToArray(), model.Name + "_" + boneName);

            MeshRenderer renderer = boneObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = (from segment in mappedSegments select MaterialLoader.Instance.LoadMaterial(segment.Material, null, unlit)).ToArray();
            renderer.shadowCastingMode = shadowSensitive ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadowSensitive;
        }

        return SWBFSegments;
    }


    /*
    Finds segments which are not attached to skeleton nodes, ie ones that are skinned,
    and creates a weighted mesh from them. 
    */

    private List<SWBFSegment> AddSkinningComponents(GameObject newObject, Model model, Dictionary<string, Transform> skeleton, string overrideTexture)
    {
        Segment[] skinnedSegments = (from segment in model.GetSegments() where segment.BoneName.Equals("") select segment).ToArray();
        
        List<SWBFSegment> SkinnedSWBFSegments = new List<SWBFSegment>();
        for (int i = 0; i < skinnedSegments.Length; i++)
        {
            SkinnedSWBFSegments.Add(new SWBFSegment(i, newObject, skinnedSegments[i].Tag, true));
        }


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

        return SkinnedSWBFSegments;
    }




    /*
    Adds skeleton, attaches static meshes/renderers to skeleton bones, and a skinned mesh/renderer to the root
    object if present.
    */

    /*
    public bool AttachModelComponents(GameObject ModelObj, string modelName, string overrideTexture, bool shadowSensitive=true, bool unlit=false)
    {   
        Model model = container.Get<LibSWBF2.Wrappers.Model>(modelName);

        if (model == null)
        {
            return null;
        }

        if (!ModelDB.ContainsKey(model.Name))
        {
            SWBFModel ModelMapping = new SWBFModel(ModelObj);

            if (!AddSkeleton(ModelObj, model, out Dictionary<string, Transform> skeleton))
            {
                return null;
            }

            List<SWBFSegment> Segments = AddStaticMeshes(ModelObj, model, skeleton, shadowSensitive, unlit);
            ModelMapping.AddSegments(Segments);

            if (model.IsSkinned)
            {
                List<SWBFSegment> SkinnedSegments = AddSkinningComponents(ModelObj, model, skeleton, overrideTexture);
                ModelMapping.AddSegments(SkinnedSegments);
            }

            List<SWBFCollider> Colliders = AddCollision(ModelObj, model);
            ModelMapping.AddColliders(Colliders);

            ModelDB[model.Name] = ModelObj;
            ModelObj.transform.SetParent(ModelDBRoot.transform);
            ModelObj.SetActive(false);

            ModelMappingDB[model.Name] = ModelMapping;
        }

        GameObject Copy = GameObject.Instantiate(ModelDB[model.Name]);
        Copy.SetActive(true);


        SkinnedMeshRenderer CopySMR = Copy.GetComponent<SkinnedMeshRenderer>();
        if (CopySMR != null)
        {
            SkinnedMeshRenderer ObjSMR = ModelObj.GetComponent<SkinnedMeshRenderer>();
            if (ObjSMR == null)
            {
                ObjSMR = ModelObj.AddComponent<SkinnedMeshRenderer>();
            }

            ObjSMR.bones = UnityUtils.RemapTransforms(CopySMR.bones, ModelObj);
            ObjSMR.sharedMesh = CopySMR.sharedMesh;
            ObjSMR.sharedMaterials = CopySMR.sharedMaterials;
        }

        MeshRenderer CopyMR = Copy.GetComponent<MeshRenderer>();
        MeshFilter CopyFilter = Copy.GetComponent<MeshFilter>();
        if (CopyMR != null && CopyFilter != null)
        {
            MeshRenderer ObjMR = ModelObj.GetComponent<MeshRenderer>();
            if (ObjMR == null)
            {
                ObjMR = ModelObj.AddComponent<MeshRenderer>();
            }

            MeshFilter ObjFilter = ModelObj.GetComponent<MeshFilter>();
            if (ObjFilter == null)
            {
                ObjFilter = ModelObj.AddComponent<MeshFilter>();
            }

            ObjFilter.sharedMesh = CopyFilter.sharedMesh;

            ObjMR.sharedMaterials = CopyMR.sharedMaterials;
            ObjMR.shadowCastingMode = CopyMR.shadowCastingMode;
            ObjMR.receiveShadows = CopyMR.shadowSensitive;
        }

        return Copy;
    }
    */


    public GameObject GetGameObjectFromModel(string modelName, string overrideTexture, bool shadowSensitive=true, bool unlit=false)
    {   
        Model model = container.Get<LibSWBF2.Wrappers.Model>(modelName);

        if (model == null)
        {
            return null;
        }

        if (!ModelDB.ContainsKey(model.Name))
        {
            GameObject ModelObj = new GameObject(model.Name);
            SWBFModel ModelMapping = new SWBFModel(ModelObj);

            if (!AddSkeleton(ModelObj, model, out Dictionary<string, Transform> skeleton))
            {
                return null;
            }

            List<SWBFSegment> Segments = AddStaticMeshes(ModelObj, model, skeleton, shadowSensitive, unlit);
            ModelMapping.AddSegments(Segments);

            if (model.IsSkinned)
            {
                List<SWBFSegment> SkinnedSegments = AddSkinningComponents(ModelObj, model, skeleton, overrideTexture);
                ModelMapping.AddSegments(SkinnedSegments);
            }

            List<SWBFCollider> Colliders = AddCollision(ModelObj, model);
            ModelMapping.AddColliders(Colliders);

            ModelDB[model.Name] = ModelObj;
            ModelObj.transform.SetParent(ModelDBRoot.transform);
            ModelObj.SetActive(false);

            ModelMappingDB[model.Name] = ModelMapping;
        }

        GameObject Copy = GameObject.Instantiate(ModelDB[model.Name]);
        Copy.SetActive(true);

        return Copy;
    }


    public SWBFModel GetModelMapping(GameObject Root, string ModelName)
    {
        if (ModelMappingDB.ContainsKey(ModelName))
        {
            return new SWBFModel(ModelMappingDB[ModelName], Root);
        }

        return null;
    }


    /*
    public SWBFModel AttachModelToGameObject(GameObject Root, string ModelName)
    {
        GameObject DupObj = GetGameObjectFromModel(ModelName);

        if (DupObj.)
        {

        }   

        SWBFModel ModelMapping = GetModelMapping(Root, ModelName);
    }
    */



    /*
    COLLISION ATTACHMENT FUNCTIONS BELOW
    */


    /*
    Adds collision primitives.  If a set of specific collider names are
    passed (see ClassLoader.LoadGeneralClass), they will be chosen 
    from all the model's colliders.  If not, the model's ordinance
    collision primitives will be used. 
    */ 

    public List<SWBFCollider> AddCollision(GameObject newObject, Model model)
    {
        // Contains all primitives and collision mesh
        List<SWBFCollider> SWBFColliders = new List<SWBFCollider>();

        //Get list of primitives, requested or found.
        CollisionPrimitive[] prims = model.GetPrimitivesMasked();
       
        // Instantiate and attach converted primitives
        foreach (CollisionPrimitive prim in prims) 
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

                    SWBFColliders.Add(new SWBFCollider(prim.MaskFlags, SWBFColliderType.Cube, primObj));

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
                    
                        SWBFColliders.Add(new SWBFCollider(prim.MaskFlags, SWBFColliderType.Cylinder, primObj));
                    }
                    break;
                
                case ECollisionPrimitiveType.Sphere:
                    SphereCollider sphereColl = primObj.AddComponent<SphereCollider>();
                    if (prim.GetSphereRadius(out float rad))
                    {
                        sphereColl.radius = rad;
                    }
                    sphereColl.sharedMaterial = PhyMat;

                    SWBFColliders.Add(new SWBFCollider(prim.MaskFlags, SWBFColliderType.Sphere, primObj));

                    break;
                
                // This happens, not sure what to make of it, but 
                // all prims of this type have zeroed fields.
                default:
                    Debug.LogWarning(model.Name + ": Unknown collision type encountered");
                    break;
            }
        }

        // Get CollisionMesh if present, add to root and collider list
        CollisionMesh collMesh = null;
        try {
            collMesh = model.GetCollisionMesh();
        }
        catch 
        {
            Debug.LogWarning(model.Name + ": Error in process of CollisionMesh fetch...");
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

                    GameObject MeshColliderObj = new GameObject("collisionmesh");

                    MeshCollider meshCollider = MeshColliderObj.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = collMeshUnity;
                    meshCollider.sharedMaterial = PhyMat;
                    MeshColliderObj.transform.SetParent(newObject.transform);

                    SWBFColliders.Add(new SWBFCollider(collMesh.MaskFlags, SWBFColliderType.Mesh, MeshColliderObj));

                    ApplyUnityLayer(MeshColliderObj, collMesh.MaskFlags);
                }
            } 
            catch
            {
                Debug.LogWarning(model.Name + ": Error while creating mesh collider...");
            }
        }

        return SWBFColliders;
    }


  

    void ApplyUnityLayer(GameObject obj, ECollisionMaskFlags flags)
    {
        // If the object's name has a keyword in it, change flags to the keyword's layer
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
