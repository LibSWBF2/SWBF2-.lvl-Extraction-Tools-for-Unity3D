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

    public static ClassLoader Instance { get; private set; } = null;

    static ClassLoader()
    {
        Instance = new ClassLoader();
    }

    public Dictionary<string, GameObject> classObjectDatabase = new Dictionary<string, GameObject>();



    public const uint GEOMETRYNAME = 1204317002;
    public const uint ATTACHODF = 2849035403;
    public const uint ATTACHTOHARDPOINT = 1005041674;
    public const uint ANIMATIONNAME = 2555738718;
    public const uint ANIMATION = 3779456605;
    public const uint SOLDIERCOLLISION = 0x5dfdc07f;
    public const uint ORDNANCECOLLISION = 0xfb2bdf07;


    public void ResetDB()
    {
        classObjectDatabase.Clear();
    }


    public void DeleteAll()
    {
    }




    public string GetBaseClassName(string name)
    {
        var ecWrapper = container.FindWrapper<EntityClass>(name);

        if (ecWrapper == null)
        {
            return "";
        }

        return ecWrapper.GetBaseName();
    }


    /*
    Temporary solution until I get to recording the default properties of
    each base class.  That'll come with ODF -> Scriptable Object conversion.
    This loads the most relevant dependencies of a given ODF. 
    */

    public GameObject LoadGeneralClass(string name)
    {
        if (name == null || name == "") return null;

        //Check if ODF already loaded
        if (classObjectDatabase.ContainsKey(name))
        {
            GameObject duplicate = null;
            if (SaveAssets)
            {
                duplicate = PrefabUtility.InstantiatePrefab(classObjectDatabase[name]) as GameObject;
            }
            else 
            {
                duplicate = UnityEngine.Object.Instantiate(classObjectDatabase[name]);     
            }

            if (duplicate == null)
            {
                return null;
            }

            duplicate.transform.localPosition = Vector3.zero;
            duplicate.transform.localRotation = Quaternion.identity;
            duplicate.transform.localScale = new Vector3(1.0f,1.0f,1.0f);
            return duplicate;
        }

        var ecWrapper = container.FindWrapper<EntityClass>(name);
        if (ecWrapper == null)
        {
            Debug.LogError(String.Format("\tObject class: {0} not defined in loaded levels...", name));
            return null;
        }

        uint[] properties;
        string[] values;
        
        try {
            if (!ecWrapper.GetOverriddenProperties(out properties, out values))
            {
                Debug.LogError(String.Format("\tFailed to load object class: {0}", name));
            }
        } catch
        {
            return null;
        }


        GameObject obj = new GameObject(name);

        try 
        {
            AssetDatabase.StartAssetEditing();

            GameObject lastAttached = null;
            string lastAttachedName = "";

            HashSet<string> ordinanceColliders = new HashSet<string>();

            string currentAnimationSet = "";
            string geometryName = "";

            for (int i = 0; i < properties.Length; i++)
            {
                uint property = properties[i];
                string propertyValue = values[i];

                switch (property)
                {
                    // Refers to an animation bank, for now we just get all the bank's clips
                    // and attach them as a legacy Animation component.
                    case ANIMATIONNAME:

                        currentAnimationSet = propertyValue;

                        var clips = AnimationLoader.Instance.LoadAnimationBank(propertyValue, obj.transform);
                        Animation animComponent = obj.GetComponent<Animation>();

                        if (animComponent == null)
                        {
                            animComponent = obj.AddComponent<Animation>();
                        }

                        foreach (var curClip in clips)
                        {
                            animComponent.AddClip(curClip, curClip.name);
                            animComponent.wrapMode = WrapMode.Once;                        
                        }

                        break;

                    // Refers to specific animations for specific purposes (see animatedprop)
                    case ANIMATION:
                        break;

                    case GEOMETRYNAME:

                        geometryName = propertyValue;

                        try {
                            if (!ModelLoader.Instance.AddModelComponents(obj, geometryName))
                            {
                                Debug.LogError(String.Format("\tFailed to load model used by: {0}", name));
                                return obj;
                            }
                        }
                        catch 
                        {
                            return obj;
                        }
                        break;

                    case ATTACHODF:
                        lastAttachedName = propertyValue; //LoadGeneralClass(propertyValue);
                        break;

                    // TODO: Hardpoint children are frequently missing...
                    case ATTACHTOHARDPOINT:

                        lastAttached = LoadGeneralClass(lastAttachedName);
                        if (lastAttached == null) break;


                        var childTx = UnityUtils.FindChildTransform(obj.transform, propertyValue);

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

                    // Some collider primitives don't have proper masks, so their purpose is
                    // listed here.  I think this was a BF1 holdover.  I chose ordinance masking
                    // as it is most accurate.
                    case ORDNANCECOLLISION:
                        ordinanceColliders.Add(propertyValue);
                        break;

                    default:
                        break;
                }
            }

            ModelLoader.Instance.AddCollisionComponents(obj, geometryName, ordinanceColliders);
        }
        finally 
        {
            AssetDatabase.StopAssetEditing();
        }


        if (SaveAssets)
        {
            // SaveAsPrefabAssetAndConnect doesnt return the imported object when inside a Start/StopAssetEditing block.
            // So we import all asset dependencies first, then create/import the prefab 
            classObjectDatabase[name] = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, SaveDirectory + "/" + obj.name + ".prefab", InteractionMode.UserAction);
        }
        else
        {
            classObjectDatabase[name] = obj;
        }

        return obj;
    }
}
