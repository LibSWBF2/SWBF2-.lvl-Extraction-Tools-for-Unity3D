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

/*
I think the proper names here would be Rigid, Static, Soft,
Ordnance, and Terrain respecitvely, not sure that covers it all.
*/

public enum SWBFGameRole : int
{
    Vehicle = 0,
    Building = 1,
    Soldier = 2,
    Ordnance = 3,
    Terrain = 4,
};


/*
CollisionMeshes and Primitives are masked/layered
in the same manner, so they should be united under one
enum.
*/

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
    public static Dictionary<char, ECollisionMaskFlags> CollCharToMask = new Dictionary<char, ECollisionMaskFlags>
    {
        { 'b', ECollisionMaskFlags.Building},
        { 'o', ECollisionMaskFlags.Ordnance},
        { 's', ECollisionMaskFlags.Soldier},
        { 't', ECollisionMaskFlags.Terrain},
        { 'v', ECollisionMaskFlags.Vehicle},
    };


    public static int MapRoleAndMaskToLayer(SWBFGameRole GameRole, ECollisionMaskFlags Mask)
    {
        bool Print = (GameRole == SWBFGameRole.Building && Mask.HasFlag(ECollisionMaskFlags.Soldier));
        
        int Layer = -1;
        
        if (GameRole == SWBFGameRole.Vehicle)
        {
            if (Mask.HasFlag(ECollisionMaskFlags.All))
            {
                Layer = LayerMask.NameToLayer("VehicleAll");
            }
            else if (Mask.HasFlag(ECollisionMaskFlags.Ordnance))
            {
                Layer = LayerMask.NameToLayer("VehicleOrdnance");
            }                   
            else if (Mask.HasFlag(ECollisionMaskFlags.Building))
            {
                Layer = LayerMask.NameToLayer("VehicleBuilding");
            }
            else if (Mask.HasFlag(ECollisionMaskFlags.Soldier))
            {           
                Layer = LayerMask.NameToLayer("VehicleSoldier");
            }
            else if (Mask.HasFlag(ECollisionMaskFlags.Terrain))
            {           
                Layer = LayerMask.NameToLayer("VehicleTerrain");
            }
            else if (Mask.HasFlag(ECollisionMaskFlags.Vehicle))
            {
                Layer = LayerMask.NameToLayer("VehicleVehicle");                    
            }    
            else 
            {
                Layer = LayerMask.NameToLayer("VehicleAll");                
            }            
        }
        else if (GameRole == SWBFGameRole.Building)
        {
            if (Mask.HasFlag(ECollisionMaskFlags.All))
            {
                Layer = LayerMask.NameToLayer("BuildingAll");
            }
            else if (Mask.HasFlag(ECollisionMaskFlags.Ordnance))
            {
                Layer = LayerMask.NameToLayer("BuildingOrdnance");
            }                
            else if (Mask.HasFlag(ECollisionMaskFlags.Soldier))
            {               
                Layer = LayerMask.NameToLayer("BuildingSoldier");
            }
            else if (Mask.HasFlag(ECollisionMaskFlags.Vehicle))
            {
                Layer = LayerMask.NameToLayer("BuildingVehicle");                    
            } 
            else 
            {
                Layer = LayerMask.NameToLayer("BuildingAll");                
            }
        }
        else if (GameRole == SWBFGameRole.Soldier)
        {
            Layer = LayerMask.NameToLayer("SoldierAll");
        }
        else if (GameRole == SWBFGameRole.Terrain)
        {
            Layer = LayerMask.NameToLayer("TerrainAll");
        }
        else if (GameRole == SWBFGameRole.Ordnance)
        {
            Layer = LayerMask.NameToLayer("OrdnanceAll");
        }

        return Layer;
    }


    public ECollisionMaskFlags Mask = ECollisionMaskFlags.All;
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
        Node.transform.localPosition = Coll.Node.transform.localPosition;
        Node.transform.localRotation = Coll.Node.transform.localRotation;
        Node.name = Coll.Node.name;
        
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

    public SWBFGameRole GameRole = SWBFGameRole.Building;


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
    public void ExpandMultiLayerColliders(bool Print = false)
    {
        int EndIndex = Colliders.Count;

        for (int i = 0; i < EndIndex; i++)
        {
            SWBFCollider CurrentCollider = Colliders[i];

            if (Print)
            {
                Debug.LogFormat("EMLC: Collision node: {0} has flags: {1}", CurrentCollider.Node.name, CurrentCollider.Mask.ToString());
            }            

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

                    if (Print)
                    {
                        Debug.LogFormat("\tCreating new collider with mask: {0}", CurrentMask.ToString());
                    }
                }
            }
        }
    }


    // Enable/disable collider
    public void EnableCollider(string Name, bool Status = true)
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            if (Collider.Node.name.Equals(Name, StringComparison.OrdinalIgnoreCase))
            {
                Collider.UnityCollider.enabled = Status;
            }
        }
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
                    Collider.Mask |= Mask;
                }
                return;
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

    // Set same mask for all colliders
    public void SetColliderMaskAll(ECollisionMaskFlags Mask, bool Print = false)
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            if (Print)
                Debug.LogFormat("Setting collider: {0} to mask: {1}", Collider.Node.name, Mask.ToString());
            
            Collider.Mask = Mask;
        }
    }


    // Set Unity Layer from Mask -> Layer mapping on all collider nodes
    public void SetColliderLayerFromMaskAll(bool Print = false)
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            int Layer = SWBFCollider.MapRoleAndMaskToLayer(GameRole, Collider.Mask);

            if (Layer == -1)
            {
                Debug.LogErrorFormat("Model: {2} Failed to map collision Role {0} and Layer {1}", GameRole.ToString(), (int) Collider.Mask, Collider.Node.name);
            }
            else 
            {
                Collider.Node.layer = Layer;
            }
        }
    }


    // Get a list of colliders on nodes with given mask
    public List<Collider> GetCollidersByLayer(ECollisionMaskFlags Mask)
    {
        List<Collider> FoundColliders = new List<Collider>();
        foreach (SWBFCollider Collider in Colliders)
        {
            if (Collider.Mask.HasFlag(Mask) || ((int) Mask) == 0)
            {
                if (Collider.UnityCollider == null)
                {
                    Debug.LogErrorFormat("Unity collider is null!: {0}", Collider.Node.name);
                }
                else 
                {
                    FoundColliders.Add(Collider.UnityCollider);
                }
            }
        }
        return FoundColliders;
    }


    public List<GameObject> GetAllCollisionNodes()
    {
        List<GameObject> Nodes = new List<GameObject>();
        foreach (SWBFCollider Collider in Colliders)
        {
            Nodes.Add(Collider.Node);
        }
        return Nodes;   
    } 


    // Set Mask from node name
    // Probably not necessary, could be useful
    /*
    public void SetColliderLayersFromName(bool Print = false)
    {
        foreach (SWBFCollider Collider in Colliders)
        {
            if (Collider.Node.name.StartsWith("p_"))
            {
                if (Print)
                {
                    Debug.LogFormat("Collision node: {0} has flags: {1}", Collider.Node.name, Collider.Mask.ToString());
                }

                int FirstDashIndex = Collider.Node.name.IndexOf("-", 0, StringComparison.OrdinalIgnoreCase); 
                if (FirstDashIndex == -1)
                {
                    Collider.Mask = ECollisionMaskFlags.All; 
                    
                    continue;   
                }

                int SecondDashIndex = Collider.Node.name.IndexOf("-", FirstDashIndex+1, StringComparison.OrdinalIgnoreCase);

                if (SecondDashIndex == -1)
                {                    
                    Collider.Mask = ECollisionMaskFlags.All;
                }
                else 
                {
                    string CollisionChars = Collider.Node.name.Substring(FirstDashIndex+1, SecondDashIndex - FirstDashIndex - 1);
                    
                    foreach (char CollisionChar in CollisionChars)
                    {
                        if (Print)
                        {
                            Debug.LogFormat("\tOn char: {0}", CollisionChar);
                        }

                        if (SWBFCollider.CollCharToMask.TryGetValue(CollisionChar, out ECollisionMaskFlags FoundMask))
                        {
                            if (Collider.Mask == ECollisionMaskFlags.All)
                            {
                                Collider.Mask = FoundMask;
                            }
                            else 
                            {
                                Collider.Mask &= FoundMask;
                            }
                        }
                    }
                }
            }
        }
    }
    */
}