using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.InteropServices.WindowsRuntime;

using UnityEngine;

using LibSWBF2.Types;
using LibSWBF2.Wrappers;

using UVector3 = UnityEngine.Vector3;


public static class UnityUtils {

    /*
    Credits: https://gamedev.stackexchange.com/a/183962/138651
    Gets bounding box of gameobject and all children.
    */

    public static Bounds GetMaxBounds(GameObject g) {
        var renderers = g.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(g.transform.position, UVector3.zero);
        var b = renderers[0].bounds;
        foreach (Renderer r in renderers) {
           b.Encapsulate(r.bounds);
        }
        
        return b;
    }


    /*
    Recursively descends transform hierarchy in search of childName.
    */

    public static Transform FindChildTransform(Transform trans, string childName)
    {
        for (int j = 0; j < trans.childCount; j++)
        {
            var curTransform = trans.GetChild(j);

            if (curTransform.name.Equals(childName))
            {
                return curTransform;
            }

            var t = FindChildTransform(curTransform, childName);

            if (t != null)
            {
                return t;
            }
        }

        return null;
    }

    //Reversed libSWBF2 GLM transform decomp + munge conversion leads to this mess (I think)
	public static UnityEngine.Quaternion QuatFromLibWorld(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(-vec.Z, vec.W, -vec.X, vec.Y);
    }
    //''
    public static UVector3 Vec3FromLibWorld(LibSWBF2.Types.Vector3 vec)
    {
        return new UVector3(vec.X,vec.Y,-vec.Z);
    }


    //Skeleton transform conversion to match vertex data handedness flip
    public static UnityEngine.Quaternion QuatFromLibSkel(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(-vec.X, vec.Y, vec.Z, -vec.W);
    }

    //''
    public static UVector3 Vec3FromLibSkel(LibSWBF2.Types.Vector3 vec)
    {
        return new UVector3(-vec.X, vec.Y, vec.Z);
    }


    // For some reason lights don't get the same mungetime conversion as all other world objects do
    public static UnityEngine.Quaternion QuatFromLibLGT(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(vec.Y, vec.Z, vec.W, vec.X);   
    }



    public static UnityEngine.Color ColorFromLib(LibSWBF2.Types.Vector3 vec)
    {
        return new UnityEngine.Color(vec.X,vec.Y,vec.Z);
    }

    public static UnityEngine.Vector4 Vec4FromLib(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Vector4(vec.X,vec.Y,-vec.Z,vec.W);
    }

    

    /*
    The following 4 functions will be replaced by templated getters for each
    wrapper if I can find good documentation on low-level Unity vector structure +
    alignment. A basic test on Mac worked, but more are needed. 
    */

    public static UVector3[] FloatToVec3Array(float[] floats, bool flipX=true)
    {
        UVector3[] vectors = new UVector3[floats.Length / 3];
        for (int i = 0; i < floats.Length; i+=3)
        {
            vectors[i / 3] = new UVector3(flipX ? 0.0f - floats[i] : floats[i],floats[i+1],floats[i+2]);
        }
        return vectors;
    }


    public static UnityEngine.Vector2[] FloatToVec2Array(float[] floats)
    {
        UnityEngine.Vector2[] vectors = new UnityEngine.Vector2[floats.Length / 2];
        for (int i = 0; i < floats.Length; i+=2)
        {
            vectors[i / 2] = new UnityEngine.Vector2(floats[i],floats[i+1]);
        }
        return vectors;
    }


    public static void ConvertSpaceAndFillVec3(float[] floats, UVector3[] vectors, int offset=0, bool flipX = true)
    {
        if (floats != null){
            for (int i = 0; i < floats.Length; i+=3)
            {
                vectors[i / 3 + offset] = new UVector3(flipX ? (0.0f - floats[i]) : floats[i],floats[i+1],floats[i+2]);
            }
        } 
    }

    public static void FillVec2(float[] floats, UnityEngine.Vector2[] vectors, int offset=0)
    {
        if (floats != null){
            for (int i = 0; i < floats.Length; i+=2)
            {
                vectors[i / 2 + offset] = new UnityEngine.Vector2(floats[i],floats[i+1]);
            }
        } 
    }


    /*
    Reverse triangle ordering.
    Necessary when handedness of vertex data is flipped.
    */

    public static ushort[] ReverseWinding(ushort[] indices)
    {
        ushort temp;
        for (int i = 0; i < indices.Length; i+=3)
        {
            temp = indices[i];
            indices[i] = indices[i+2];
            indices[i+2] = temp;
        }
        return indices;
    }


    /*
    Only parameter of note is fix.  This is used for broken skeletons (only example being Jabba from tat3.lvl), where 
    bone weight indices are off by 1.
    */

    public static void FillBoneWeights(VertexWeight[] vws, BoneWeight1[] boneWeights, int offset, int fix = 0)
    {
        if (vws != null)
        {
            for (int i = 0; i < vws.Length; i++)
            {
                int windex = (int) vws[i].index;
                float wvalue = vws[i].weight;

                boneWeights[offset + i].boneIndex = windex + fix;
                boneWeights[offset + i].weight = wvalue;                                   
            }
        }
    }
}


