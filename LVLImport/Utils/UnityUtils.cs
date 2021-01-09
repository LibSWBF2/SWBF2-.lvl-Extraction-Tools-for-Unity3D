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


static class UnityUtils {

    
    //This worked via matching the .wld file...
	public static UnityEngine.Quaternion QuatFromLib(LibSWBF2.Types.Vector4 vec)
    {
        //return new UnityEngine.Quaternion(vec.Y, -vec.Z, vec.W, -vec.X);
        return new UnityEngine.Quaternion(-vec.X, vec.W, -vec.Z, vec.Y);
    }
    


    //public static UnityEngine.Quaternion QuatFromLib(LibSWBF2.Types.Vector4 vec)
    //{
    //    return new UnityEngine.Quaternion(-vec.Z, vec.Y, -vec.X, vec.W);
    //}




    public static UnityEngine.Quaternion QuatFromLibSkel(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(vec.X, vec.Y, vec.Z, vec.W);
        //return new UnityEngine.Quaternion(-vec.X, vec.W, -vec.Z, vec.Y);
    }

    public static UnityEngine.Vector3 Vec3FromLibSkel(LibSWBF2.Types.Vector3 vec)
    {
        //return new UnityEngine.Vector3(vec.X,vec.Y,vec.Z);
        return new UnityEngine.Vector3(vec.X,vec.Y, vec.Z);
    }




    public static UnityEngine.Quaternion QuatFromLibLGT(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(vec.Y, vec.Z, vec.W, vec.X);   
    }


    public static UnityEngine.Vector3 Vec3FromLib(LibSWBF2.Types.Vector3 vec)
    {
        return new UnityEngine.Vector3(vec.X,vec.Y,-vec.Z);
    }


    public static UnityEngine.Color ColorFromLib(LibSWBF2.Types.Vector3 vec)
    {
        return new UnityEngine.Color(vec.X,vec.Y,vec.Z);
    }


    public static UnityEngine.Vector4 Vec4FromLib(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Vector4(vec.X,vec.Y,-vec.Z,vec.W);
    }


    public static UnityEngine.Vector3[] FloatToVec3Array(float[] floats, bool flipX=true)
    {
        UnityEngine.Vector3[] vectors = new UnityEngine.Vector3[floats.Length / 3];
        for (int i = 0; i < floats.Length; i+=3)
        {
            vectors[i / 3] = new UnityEngine.Vector3(flipX ? -floats[i] : floats[i],floats[i+1],floats[i+2]);
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


    public static void ConvertSpaceAndFillVec3(float[] floats, UnityEngine.Vector3[] vectors, int offset=0, bool convertSpace = true)
    {
        float mult = convertSpace ? -1.0f : 1.0f;
        if (floats != null){
            for (int i = 0; i < floats.Length; i+=3)
            {
                vectors[i / 3 + offset] = new UnityEngine.Vector3(mult * floats[i],floats[i+1],floats[i+2]);
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

    public static void FillBoneWeights(VertexWeight[] vws, BoneWeight1[] boneWeights, int offset)
    {
        if (vws != null)
        {
            //Debug.Log(String.Format("Weights per vert: {0}, VW Buffer length: {1}, Unity BW buffer length: {2}", weightsPerVert, vws.Length, boneWeights.Length));
            /*
            for (int i = 0; i < vws.Length; i+=weightsPerVert)
            {
                for (int j = 0; j < 4; j++)
                {
                    int k = (i / weightsPerVert) * 4 + j + offset;

                    if (j >= weightsPerVert)
                    {
                        boneWeights[k].boneIndex = 0;
                        boneWeights[k].weight = 0.0f;
                    } 
                    else 
                    {
                        int windex = (int) vws[i + j].index;
                        float wvalue = vws[i + j].weight;

                        //Debug.Log(String.Format("\tIndex: {0}, Value: {1}", windex, wvalue));

                        boneWeights[k].boneIndex = windex;
                        boneWeights[k].weight = wvalue;                          
                    }                  
                }
            }
            */

            for (int i = 0; i < vws.Length; i++)
            {
                int windex = (int) vws[i].index;
                float wvalue = vws[i].weight;

                //Debug.Log(String.Format("\tIndex: {0}, Value: {1}", windex, wvalue));

                boneWeights[offset + i].boneIndex = windex;
                boneWeights[offset + i].weight = wvalue;                                   
            }
        }
    }
}


