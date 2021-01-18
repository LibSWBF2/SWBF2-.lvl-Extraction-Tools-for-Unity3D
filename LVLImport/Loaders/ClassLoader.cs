using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Utils;


public class ClassLoader : Loader {

    public static Dictionary<string, GameObject> classObjectDatabase = new Dictionary<string, GameObject>();

    public const uint GEOMETRYNAME = 1204317002;
    public const uint ATTACHODF = 2849035403;
    public const uint ATTACHTOHARDPOINT = 1005041674;
    public const uint ANIMATIONNAME = 2555738718;
    public const uint ANIMATION = 3779456605;


    public static void ResetDB()
    {
        classObjectDatabase.Clear();
    }


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
        var ecWrapper = container.FindWrapper<EntityClass>(name);

        if (ecWrapper == null)
        {
            return "";
        }

        return ecWrapper.GetBaseName();
    }



    public static GameObject LoadGeneralClass(string name)
    {
        if (classObjectDatabase.ContainsKey(name))
        {
            var duplicate = Instantiate(classObjectDatabase[name]);
            duplicate.transform.localPosition = Vector3.zero;
            duplicate.transform.localRotation = Quaternion.identity;
            return duplicate;
        }

        var ecWrapper = container.FindWrapper<EntityClass>(name);
        if (ecWrapper == null)
        {
            Debug.LogError(String.Format("\tFailed to load object class: {0}", name));
            return null;
        }

        if (!ecWrapper.GetOverriddenProperties(out uint[] properties, out string[] values))
        {
            Debug.LogError(String.Format("\tFailed to load object class: {0}", name));
            return null;
        }


        GameObject obj = new GameObject(name);
        GameObject lastAttached = null;

        string currentAnimationSet = "";

        for (int i = 0; i < properties.Length; i++)
        {
            uint property = properties[i];
            string propertyValue = values[i];

            switch (property)
            {
                case ANIMATIONNAME:

                    //currentAnimationSet = "imp_walk_atat";
                    currentAnimationSet = propertyValue;
                    break;

                case ANIMATION:

                    //AnimationClip animClip = AnimationLoader.LoadAnimationClip(currentAnimationSet, "death01", obj.transform);
                    AnimationClip animClip = AnimationLoader.LoadAnimationClip(currentAnimationSet, propertyValue, obj.transform);

                    if (animClip == null)
                    {
                        Debug.LogError(String.Format("\tFailed to load animation clip {0}", propertyValue));
                    }
                    else 
                    {
                        Animation anim = obj.GetComponent<Animation>();

                        if (anim == null)
                        {
                            anim = obj.AddComponent<Animation>();
                        }

                        anim.AddClip(animClip, propertyValue);
                        anim.wrapMode = WrapMode.Once;
                    }
                    break;

                case GEOMETRYNAME:

					//if (!ModelLoader.AddModelComponents(ref obj, "imp_walk_atat"))
                    if (!ModelLoader.AddModelComponents(ref obj, propertyValue))
                    {
                        Debug.LogError(String.Format("\tFailed to load model used by: {0}", name));
                        return obj;
                    }
                    break;

                case ATTACHODF:
                    lastAttached = LoadGeneralClass(propertyValue);
                    break;

                case ATTACHTOHARDPOINT:

                    if (lastAttached == null)
                    {
                        break;
                    }

                    var childTx = FindChildTransform(obj.transform, propertyValue);

                    if (childTx == null)
                    {
                        Debug.LogError("\t" + name + ": Couldnt find hardpoint: " + propertyValue);
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
