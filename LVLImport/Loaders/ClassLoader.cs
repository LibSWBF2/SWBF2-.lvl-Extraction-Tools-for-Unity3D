using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class ClassLoader : ScriptableObject {

    public static Dictionary<string, GameObject> classObjectDatabase = new Dictionary<string, GameObject>();


    public static string GetBaseClassName(string name)
    {
        var ecWrapper = CentralLoader.GetEntityClass(name);

        if (ecWrapper == null)
        {
            return "";
        }

        return ecWrapper.GetBaseName();
    }



    public static GameObject LoadBaseClass_Door(string name)
    {
        Debug.Log(String.Format("Loading door: {0}...", name));

        if (classObjectDatabase.ContainsKey(name))
        {
            return Instantiate(classObjectDatabase[name]);
        }

        GameObject obj = null;
        var ecWrapper = CentralLoader.GetEntityClass(name);

        if (ecWrapper == null)
        {
            Debug.Log(String.Format("\tERROR: Failed to load door object: {0}", name));
            return null;
        }


        obj = new GameObject(name);

        if (!ModelLoader.AddModelComponents(ref obj, ecWrapper.GetProperty("GeometryName")))
        {
            return obj;
        }
        
        string animSetName = ecWrapper.GetProperty("AnimationName");
        string animName    = ecWrapper.GetProperty("Animation");

        AnimationClip animClip = AnimationLoader.LoadAnimationClip(animSetName, animName, obj.transform);

        if (animClip == null)
        {
            Debug.Log(String.Format("\tERROR: Failed to load animation clip {0}", animName));
        }
        else 
        {
            Animation anim = obj.AddComponent<Animation>();
            anim.AddClip(animClip, animClip.name);
            anim.wrapMode = WrapMode.Once;

            obj.AddComponent<Door>();        
        }

        return obj;
    }




    public static GameObject LoadBaseClass_Prop(string name)
    {
        Debug.Log(String.Format("Loading prop: {0}...", name));

        if (classObjectDatabase.ContainsKey(name))
        {
            return Instantiate(classObjectDatabase[name]);
        }

        GameObject obj = null;

        var ecWrapper = CentralLoader.GetEntityClass(name);

        if (ecWrapper == null)
        {
            Debug.Log(String.Format("\tERROR: Failed to load prop object: {0}", name));
            return null;
        }

        obj = new GameObject(name);

        if (!ModelLoader.AddModelComponents(ref obj, ecWrapper.GetProperty("GeometryName")))
        {
            Debug.Log(String.Format("\tERROR: Failed to load model used by: {0}", name));
        }
        
        classObjectDatabase[name] = obj;
        return obj;
    }
}
