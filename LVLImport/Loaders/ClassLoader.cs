using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Utils;


public class ClassLoader : ScriptableObject {

    public static Dictionary<string, GameObject> classObjectDatabase = new Dictionary<string, GameObject>();

    public const uint GEOMETRYNAME = 1204317002;
    public const uint ATTACHODF = 2849035403;
    public const uint ATTACHTOHARDPOINT = 1005041674;
    public const uint ANIMATIONNAME = 2555738718;
    public const uint ANIMATION = 3779456605;


    private static Transform FindChildTransform(Transform trans, string childName)
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
            var duplicate = Instantiate(classObjectDatabase[name]);
            duplicate.transform.localPosition = Vector3.zero;
            duplicate.transform.localRotation = Quaternion.identity;
            return duplicate;
        }

        var ecWrapper = CentralLoader.GetEntityClass(name);
        if (ecWrapper == null)
        {
            Debug.Log(String.Format("\tERROR: Failed to load door object: {0}", name));
            return null;
        }

        if (!ecWrapper.GetOverriddenProperties(out uint[] properties, out string[] values))
        {
            Debug.Log(String.Format("\tERROR: Failed to load door object: {0}", name));
            return null;
        }


        GameObject obj = new GameObject(name);

        string currentAnimationSet = "";

        for (int i = 0; i < properties.Length; i++)
        {
            uint property = properties[i];
            string propertyValue = values[i];

            switch (property)
            {
                case ANIMATIONNAME:

                    currentAnimationSet = propertyValue;
                    break;

                case ANIMATION:

                    AnimationClip animClip = AnimationLoader.LoadAnimationClip(currentAnimationSet, propertyValue, obj.transform);

                    if (animClip == null)
                    {
                        Debug.Log(String.Format("\tERROR: Failed to load animation clip {0}", propertyValue));
                    }
                    else 
                    {
                        Animation anim = obj.GetComponent<Animation>();

                        if (anim == null)
                        {
                            anim = obj.AddComponent<Animation>();
                        }

                        anim.AddClip(animClip, animClip.name);
                        anim.wrapMode = WrapMode.Once;
                    }
                    break;

                case GEOMETRYNAME:

                    if (!ModelLoader.AddModelComponents(ref obj, propertyValue))
                    {
                        Debug.Log(String.Format("\tERROR: Failed to load model used by: {0}", name));
                        return obj;
                    }
                    break;

                default:
                    break;
            }
        }
        
        obj.AddComponent<Door>();
        classObjectDatabase[name] = obj;
        return obj;
    }




    public static GameObject LoadBaseClass_Prop(string name)
    {
        Debug.Log(String.Format("Loading prop: {0}...", name));

        if (classObjectDatabase.ContainsKey(name))
        {
            var duplicate = Instantiate(classObjectDatabase[name]);
            duplicate.transform.localPosition = Vector3.zero;
            duplicate.transform.localRotation = Quaternion.identity;
            return duplicate;
        }

        var ecWrapper = CentralLoader.GetEntityClass(name);
        if (ecWrapper == null)
        {
            Debug.Log(String.Format("\tERROR: Failed to load prop object: {0}", name));
            return null;
        }

        if (!ecWrapper.GetOverriddenProperties(out uint[] properties, out string[] values))
        {
            Debug.Log(String.Format("\tERROR: Failed to load door object: {0}", name));
            return null;
        }

        GameObject obj = new GameObject(name);
        GameObject lastAttached = null;

        for (int i = 0; i < properties.Length; i++)
        {
            uint property = properties[i];
            string propertyValue = values[i];


            switch (property)
            {
                case GEOMETRYNAME:

                    if (!ModelLoader.AddModelComponents(ref obj, ecWrapper.GetProperty("GeometryName")))
                    {
                        Debug.Log(String.Format("\tERROR: Failed to load model used by: {0}", name));
                    }
                    break;

                case ATTACHODF:
                    lastAttached = LoadBaseClass_Door(propertyValue);
                    break;

                case ATTACHTOHARDPOINT:

                    if (lastAttached == null)
                    {
                        break;
                    }

                    var childTx = FindChildTransform(obj.transform, propertyValue);

                    if (childTx == null)
                    {
                        Debug.Log("\tERROR: Couldnt find hardpoint: " + propertyValue);
                        lastAttached.transform.SetParent(obj.transform, false);
                    }
                    else 
                    {
                        lastAttached.transform.SetParent(childTx, false);
                    }

                    break;

                default:
                    break;
            }
        }
        
        classObjectDatabase[name] = obj;
        return obj;
    }
}
