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


public enum SWBFColliderType : int 
{
    Mesh = 0,
    Sphere = 1,
    Cylinder = 2,
    Cube = 3,
};


// Simple class for mapping SWBF collision
// concepts to Unity, mainly Mask -> Layer mapping
public class SWBFCollider
{
    public ECollisionMaskFlags Mask;
    public GameObject Node;
    public Collider UnityCollider;
    public SWBFColliderType CollisionType;

    public SWBFCollider(ECollisionMaskFlags _Mask, SWBFColliderType CollType, GameObject node)
    {
        Node = node;
        Mask = _Mask;
        CollisionType = CollType;
        UnityCollider = node.GetComponent<Collider>();
    }

    // Create a mapping on the child node of TargetRoot with the same name
    // as Coll's node.  New mapping has same metadata as Coll
    public SWBFCollider(SWBFCollider Coll, GameObject TargetRoot)
    {
        Node = UnityUtils.FindChildTransform(TargetRoot.transform, Coll.Node.name).gameObject;
        UnityCollider = Node.GetComponent<Collider>();

        Mask = Coll.Mask;
        CollisionType = Coll.CollisionType;
    }

    // Duplicate Coll and set parent to Coll's parent
    public SWBFCollider(SWBFCollider Coll)
    {
        Node = GameObject.Instantiate(Coll.Node);
        Node.transform.SetParent(Coll.Node.transform.parent);
        UnityCollider = Node.GetComponent<Collider>();

        Mask = Coll.Mask;
        CollisionType = Coll.CollisionType;
    }
}

// Simple mapping from Segment to GameObject + submesh/material index
public class SWBFSegment
{
    public readonly int Index; // submesh/material index
    public readonly GameObject Node;
    public readonly string Tag = "";
    public readonly bool IsSkinned = false; 

    public SWBFSegment(int index, GameObject node, string tag, bool isSkinned = false)
    {
        Index = index;
        Node = node;
        Tag = tag;
        IsSkinned = isSkinned;
    }

    // Create a mapping on the child node of TargetObject with the same name
    // as Segment's node (unless Segment is skinned).  
    // New mapping has same metadata as Segment
    public SWBFSegment(SWBFSegment Segment, GameObject TargetObject)
    {
        if (Segment.IsSkinned)
        {
            Node = TargetObject;
        }
        else 
        {
            Transform NodeTx = UnityUtils.FindChildTransform(TargetObject.transform, Segment.Node.name);
            if (NodeTx != null)
            {
                Node = NodeTx.gameObject;
            }            
        }

        Index = Segment.Index;
        Tag = Segment.Tag;
        IsSkinned = Segment.IsSkinned;
    }
}

// Class for mapping SWBF model concepts to Unity objects
public class SWBFModel
{
    GameObject Root;

    List<SWBFSegment> Segments;
    List<SWBFCollider> Colliders;


    public SWBFModel(GameObject root)
    {
        Root = root;
        Colliders = new List<SWBFCollider>();
        Segments = new List<SWBFSegment>();
    }


    public SWBFModel(SWBFModel Model, GameObject TargetObject)
    {
        Root = TargetObject;

        Segments = new List<SWBFSegment>();
        foreach (SWBFSegment Segment in Model.Segments)
        {
            Segments.Add(new SWBFSegment(Segment, Root));
        }

        Colliders = new List<SWBFCollider>();
        foreach (SWBFCollider SWBFColl in Model.Colliders)
        {
            Colliders.Add(new SWBFCollider(SWBFColl, Root));
        }
    }

    public void AddSegments(List<SWBFSegment> SWBFSegments)
    {
        foreach (SWBFSegment Segm in SWBFSegments)
        {
            Segments.Add(Segm);
        }
    }

    public void AddColliders(List<SWBFCollider> SWBFColliders)
    {
        foreach (SWBFCollider Coll in SWBFColliders)
        {
            Colliders.Add(Coll);
        }
    }


    // Get all segments with tag, useful for override_texturing
    // and ODF referenced textures e.g. wheel nodes
    public List<SWBFSegment> GetSegmentsWithTag(string Tag)
    {
        List<SWBFSegment> TaggedSegments = new List<SWBFSegment>();
        foreach (SWBFSegment Segment in Segments)
        {
            if (Segment.Tag.Equals(Tag, StringComparison.OrdinalIgnoreCase))
            {
                TaggedSegments.Add(Segment);
            }
        }
        return TaggedSegments;
    }


    // Set Mask for Collider with ColliderName, but don't apply Unity Layer yet 
    public void SetColliderMask(string ColliderName, ECollisionMaskFlags Mask)
    {
        bool IsMesh = ColliderName.Equals("CollisionMesh", StringComparison.OrdinalIgnoreCase);

        foreach (SWBFCollider Collider in Colliders)
        {
            if ((IsMesh && Collider.CollisionType == SWBFColliderType.Mesh) || 
                Collider.Node.name.Equals(ColliderName, StringComparison.OrdinalIgnoreCase))
            {
                if (Collider.Mask == ECollisionMaskFlags.All)
                {
                    Collider.Mask = Mask;
                }
                else 
                {
                    Collider.Mask &= Mask;
                }
                return;
            }
        }
    }


    // Does the model use only primitives for a particular Mask?
    public bool IsCollisionLayerOnlyPrimitive(ECollisionMaskFlags Mask)
    {
        foreach (SWBFCollider SWBFColl in Colliders)
        {
            if (SWBFColl.Mask.HasFlag(Mask) &&
                SWBFColl.CollisionType == SWBFColliderType.Mesh)
            {
                return false;
            }
        }
        return true;
    }


    // Make collision mesh convex
    public void ConvexifyMeshColliders()
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            if (Collider.CollisionType == SWBFColliderType.Mesh)
            {
                MeshCollider MC = Collider.UnityCollider as MeshCollider;
                MC.convex = true;
            }
        }
    }


    // Delete collision mesh
    public void StripMeshCollider()
    {
        for (int i = 0; i < Colliders.Count; i++)
        {
            SWBFCollider Collider = Colliders[i];

            if (Collider.CollisionType == SWBFColliderType.Mesh)
            {
                Component.DestroyImmediate(Collider.UnityCollider);
                GameObject.DestroyImmediate(Collider.Node);
                
                Colliders.RemoveAt(i);

                i--;
            }
        }
    }


    // Enable/disable the collision mesh
    public void SetMeshCollider(bool Enabled)
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            if (Collider.CollisionType == SWBFColliderType.Mesh)
            {
                Collider.UnityCollider.enabled = Enabled;
                return;
            }
        }
    }


    // Delete collider node if it doesn't cover Mask
    public void StripAllCollidersExcept(ECollisionMaskFlags Mask)
    {
        int StopIndex = Colliders.Count;
        
        for (int i = 0; i < StopIndex; i++)
        {
            SWBFCollider Collider = Colliders[i];

            if (!Collider.Mask.HasFlag(Mask))
            {
                Component.Destroy(Collider.UnityCollider);
                
                if (Collider.Node != Root)
                {
                    GameObject.Destroy(Collider.Node);
                }

                Colliders.RemoveAt(i);
            }

            StopIndex = Colliders.Count;
        } 
    }


    // Enable or Disable all colliders that don't cover Mask
    public void SetAllCollidersExcept(ECollisionMaskFlags Mask, bool Enabled)
    {
        for (int i = 0; i < Colliders.Count; i++)
        {
            SWBFCollider Collider = Colliders[i];

            Debug.LogFormat("Collider {0} has flags: {1}", Collider.Node.name, Collider.Mask.ToString());

            if (!Collider.Mask.HasFlag(Mask))
            {
                Collider.UnityCollider.enabled = Enabled;
            }
        } 
    }


    // In case we decide to duplicate colliders to cover multilayering,
    // this will create new colliders for each layer covered.
    public void ExpandMultiLayerColliders()
    {
        int EndIndex = Colliders.Count;

        for (int i = 0; i < EndIndex; i++)
        {
            SWBFCollider CurrentCollider = Colliders[i];

            if (CurrentCollider.Mask == ECollisionMaskFlags.All) continue;

            ECollisionMaskFlags OriginalFlags = CurrentCollider.Mask;

            bool CreateNew = false;
            for (int MaskIter = 1; MaskIter < 32; MaskIter *= 2)
            {
                ECollisionMaskFlags CurrentMask = (ECollisionMaskFlags) MaskIter;
                if (OriginalFlags.HasFlag(CurrentMask))
                {
                    if (CreateNew)
                    {
                        CurrentCollider = new SWBFCollider(CurrentCollider);
                        Colliders.Add(CurrentCollider);
                    }

                    CurrentCollider.Mask = CurrentMask;
                    CreateNew = true;
                }
            }
        }
    }


    // Bypass Mask -> Unity Layer mapping and set all collider nodes to a 
    // particular Unity Layer. 
    public void SetColliderLayerAll(int Layer)
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            Collider.Node.layer = Layer;
        }
    }

    // Set Unity Layer from Mask -> Layer mapping on all collider nodes
    public void SetColliderLayerFromMaskAll()
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            // Set Unity layer from Mask
        }
    }


    public bool ApplyUnityLayer(ECollisionMaskFlags Mask)
    {
        // Dk yet;
        return false;
    }
}