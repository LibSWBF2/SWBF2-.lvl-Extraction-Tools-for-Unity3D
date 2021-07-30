using System;
using System.Collections.Generic;

using UnityEngine;
#if !LVLIMPORT_NO_EDITOR
using UnityEditor;
#endif

using LibSWBF2.Wrappers;

using UVector3 = UnityEngine.Vector3;

public class ClassLoader : Loader
{
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



    public IEnumerator<LoadStatus> ImportClassBatch(Level[] levels)
    {
        float ZOffset = 0.0f;

        foreach (Level lvl in levels)
        {
            Transform LevelRoot = new GameObject(lvl.Name).transform;
            EntityClass[] Classes = lvl.Get<EntityClass>();
            UVector3 SpawnOffset = Vector3.zero;
            float RootZOffset = 0.0f;

            for (int i = 0; i < Classes.Length; i++)
            {
                GameObject ecObj = LoadGeneralClass(Classes[i].Name);

                if (ecObj != null)
                {
                    yield return new LoadStatus(i / (float)Classes.Length, lvl.Name + ": " + ecObj.name);

                    ecObj.transform.parent = LevelRoot;

                    var extents = UnityUtils.GetMaxBounds(ecObj).extents;

                    RootZOffset = Math.Max(RootZOffset, extents.z);

                    SpawnOffset += new Vector3(extents.x, 0, 0);
                    ecObj.transform.localPosition = SpawnOffset;
                    SpawnOffset += new Vector3(extents.x, 0, 0);
                }
            }

            LevelRoot.localPosition = new Vector3(0.0f, 0.0f, RootZOffset + ZOffset);
            ZOffset += RootZOffset * 2.0f;
        }
    }



    public void DeleteAll()
    {
    }




    public string GetBaseClassName(string name)
    {
        var ecWrapper = container.Get<EntityClass>(name);

        if (ecWrapper == null)
        {
            return "";
        }

        return ecWrapper.BaseClassName;
    }


    static bool IsStaticObjectClass(EntityClass ec)
    {
        switch (ec.BaseClassName)
        {
            case "door":
            case "animatedprop":
            case "animatedbuilding":
            case "powerupstation":
            case "soldier":
            case "walkerdroid":
            case "vehicle":
            case "walker":
            case "commandwalker":
            case "hover":
            case "flyer":
            case "cannon":
            case "repair":
            case "remote":
            case "grenade":
            case "detonator":
            case "mine":
            case "launcher":
            case "missile":
            case "bullet":
            case "bolt":
            case "beam":
            case "droid":
            case "melee":
            case "explosion":
            case "cloth":
                return false;
            default:
                return true;
        }
    }

    public static EntityClass GetRootClass(EntityClass cl)
    {
        if (cl == null) return null;

        EntityClass parentClass = cl.BaseClass;
        if (parentClass == null)
        {
            return cl;
        }
        return GetRootClass(parentClass);
    }

    public GameObject Instantiate(ISWBFProperties instOrClass, string instName)
    {
        //TODO: caching

        GameObject obj = null;
        SWBFModel ModelMapping = null;

        if (instOrClass.GetProperty("GeometryName", out string geometryName))
        {
            if (!instOrClass.GetProperty("OverrideTexture", out string overrideTexture))
            {
                overrideTexture = null;
            }

            obj = ModelLoader.Instance.GetGameObjectFromModel(geometryName, overrideTexture);

            if (obj == null)
            {
                Debug.LogWarningFormat("Failed to load model {1} used by object {0}", instName, geometryName);
                return new GameObject(instName);
            }
            else
            {
                obj.name = instName;
            }

            ModelMapping = ModelLoader.Instance.GetModelMapping(obj, geometryName);

            EntityClass odf = instOrClass.GetType() == typeof(Instance) ? ((Instance)instOrClass).EntityClass : ((EntityClass)instOrClass);
            EntityClass root = GetRootClass(odf);
            if (IsStaticObjectClass(root))
            {
                obj.isStatic = true;
                foreach (var tx in UnityUtils.GetChildTransforms(obj.transform))
                {
                    tx.gameObject.isStatic = true;
                }

                // If class is not static, colliders will be wrangled in MonoBehaviour.
                // Though some props/other statics may have collisions set in ODF
                if (ModelMapping != null)
                {
                    ModelMapping.ExpandMultiLayerColliders();
                    ModelMapping.SetColliderLayerFromMaskAll();
                }
            }
            else
            {
                ModelMapping.ConvexifyMeshColliders();
            }
        }
        else
        {
            obj = new GameObject(instName);
        }

        return obj;
    }

    /*
    Temporary solution until I get to recording the default properties of
    each base class.  That'll come with ODF -> Scriptable Object conversion.
    This loads the most relevant dependencies of a given ODF. 
    */

    public GameObject LoadGeneralClass(string name, bool tryMakeStatic = false)
    {
        if (name == null || name == "") return null;

        //Check if ODF already loaded
        if (classObjectDatabase.ContainsKey(name))
        {
            GameObject duplicate = null;

            GameObject original = classObjectDatabase[name];

            if (original == null) return null;

#if !LVLIMPORT_NO_EDITOR
            if (SaveAssets)
            {
                duplicate = PrefabUtility.InstantiatePrefab(original) as GameObject;
            }
            else
#endif
            {
                duplicate = UnityEngine.Object.Instantiate(original);
            }

            if (duplicate == null)
            {
                return null;
            }

            duplicate.transform.localPosition = Vector3.zero;
            duplicate.transform.localRotation = Quaternion.identity;
            duplicate.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            return duplicate;
        }

        var ecWrapper = container.Get<EntityClass>(name);
        if (ecWrapper == null)
        {
            Debug.LogWarningFormat("Object class: {0} not defined in loaded levels...", name);
            return null;
        }

        uint[] properties;
        string[] values;

        try {
            ecWrapper.GetAllProperties(out properties, out values);
        } catch
        {
            Debug.LogWarningFormat("Failed to load object class: {0}", name);
            return null;
        }

        if (properties == null || values == null)
        {
            return null;
        }


        GameObject obj = null;

        GameObject lastAttached = null;
        string lastAttachedName = "";

        HashSet<string> ordinanceColliders = new HashSet<string>();

        string currentAnimationSet = "";

        int geomNameIndex;
        string geometryName = "";

        geomNameIndex = Array.IndexOf<uint>(properties, GEOMETRYNAME);

        if (geomNameIndex != -1)
        {
            geometryName = values[geomNameIndex];

            int overrideTexIndex = Array.IndexOf<uint>(properties, LibSWBF2.Utils.HashUtils.GetFNV("OverrideTexture"));

            try {
                obj = ModelLoader.Instance.GetGameObjectFromModel(geometryName, overrideTexIndex != -1 ? values[overrideTexIndex] : null);
                if (obj == null)
                {
                    obj = new GameObject();
                }
                obj.name = name;

                if (tryMakeStatic && IsStaticObjectClass(ecWrapper))
                {
                    obj.isStatic = true;
                    foreach (var tx in UnityUtils.GetChildTransforms(obj.transform))
                    {
                        tx.gameObject.isStatic = true;
                    }
                }
            }
            catch 
            {
                Debug.LogWarningFormat("Failed to load model {1} used by object {0}", name, geometryName);
                return obj;
            }
        }
        else
        {
            obj = new GameObject(name);
        }


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
                    if (clips == null)
                    {
                        break;
                    }

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
     
                case ATTACHODF:
                    lastAttachedName = propertyValue; //LoadGeneralClass(propertyValue);
                    break;

                // TODO: Hardpoint children are frequently missing...
                case ATTACHTOHARDPOINT:

                    lastAttached = LoadGeneralClass(lastAttachedName, tryMakeStatic);
                    if (lastAttached == null) break;


                    var childTx = UnityUtils.FindChildTransform(obj.transform, propertyValue);

                    if (childTx == null)
                    {
                        Debug.LogWarningFormat("{0}: Couldn't find hardpoint {1}", name, propertyValue);
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
                // case ORDNANCECOLLISION:
                //    ordinanceColliders.Add(propertyValue);
                //    break;

                default:
                    break;
            }
        }

#if !LVLIMPORT_NO_EDITOR
        if (SaveAssets)
        {
            // This breaks when called inside an AssetEditing block...
            classObjectDatabase[name] = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, SaveDirectory + "/" + obj.name + ".prefab", InteractionMode.UserAction);
        }
        else
#endif
        {
            classObjectDatabase[name] = obj;
        }


        return obj;

    }


    
    public void DeleteAndClearDB()
    {
        foreach (string objname in classObjectDatabase.Keys)
        {
            try
            {
#if !LVLIMPORT_NO_EDITOR
                PrefabUtility.UnpackPrefabInstance(classObjectDatabase[objname], PrefabUnpackMode.Completely, InteractionMode.UserAction);
#endif
                UnityEngine.Object.DestroyImmediate(classObjectDatabase[objname]);
            }catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }
        classObjectDatabase.Clear();
    }
}
