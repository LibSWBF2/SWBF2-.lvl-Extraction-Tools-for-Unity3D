using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.InteropServices.WindowsRuntime;

using UnityEngine;
using LibSWBF2.Tyes;


static class UnityUtils {

	public static UnityEngine.Quaternion QuatFromLib(LibSWBF2.Types.Vector4 vec)
    {
        UnityEngine.Quaternion newVec = new UnityEngine.Quaternion();
        newVec.x = vec.X;
        newVec.y = vec.Y;
        newVec.z = vec.Z;
        newVec.w = vec.W;
        return newVec;
    }

    public static UnityEngine.Vector3 Vec3FromLib(LibSWBF2.Types.Vector3 vec)
    {
        UnityEngine.Vector3 newVec = new UnityEngine.Vector3();
        newVec.x = vec.X;
        newVec.y = vec.Y;
        newVec.z = vec.Z;
        return newVec;
    }

    public static UnityEngine.Vector4 Vec4FromLib(LibSWBF2.Types.Vector4 vec)
    {
        UnityEngine.Vector4 newVec = new UnityEngine.Vector4();
        newVec.x = vec.X;
        newVec.y = vec.Y;
        newVec.z = vec.Z;
        newVec.w = vec.W;
        return newVec;
    }

    public static Vector3[] floatToVec3Array(float[] floats)
    {
        Vector3[] vectors = new Vector3[floats.Length / 3];
        for (int i = 0; i < floats.Length; i+=3)
        {
            vectors[i / 3] = new Vector3(floats[i],floats[i+1],floats[i+2]);
        }
        return vectors;
    }

    public static Vector2[] floatToVec2Array(float[] floats)
    {
        Vector2[] vectors = new Vector2[floats.Length / 2];
        for (int i = 0; i < floats.Length; i+=2)
        {
            vectors[i / 2] = new Vector2(floats[i],floats[i+1]);
        }
        return vectors;
    }
}


