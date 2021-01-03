using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Types;
using LibSWBF2.Enums;


public class MapLoader : ScriptableObject {

    public static void ImportMap(Level level)
    {
        World[] worlds = level.GetWorlds();


        foreach (World world in worlds)
        {
        	GameObject worldRoot = new GameObject(world.Name);
            
            Instance[] instances = world.GetInstances();
            foreach (Instance inst in instances)
            {
                string entityClassName = inst.GetEntityClassName();
                string baseName = ClassLoader.GetBaseClassName(entityClassName);

                GameObject obj = null;

                switch (baseName)
                {
                    case "door":
                    case "animatedprop":
                        obj = ClassLoader.LoadBaseClass_Door(entityClassName);
                        break;
                    
                    case "prop":
                    //case "building":
                    case "destructablebuilding":
                    case "armedbuilding":
                        obj = ClassLoader.LoadBaseClass_Prop(entityClassName);
                        break;

                    default:
                        Debug.Log(String.Format("\tERROR: Encountered unknown base class: {0} subclassed by: {1}", baseName, entityClassName));
                        break; 
                }

                if (obj == null)
                {
                    continue;
                }

                if (!inst.Name.Equals(""))
                {
                    obj.name = inst.Name;
                }

                obj.transform.rotation = UnityUtils.QuatFromLib(inst.GetRotation());
                obj.transform.position = UnityUtils.Vec3FromLib(inst.GetPosition());
                obj.transform.parent = worldRoot.transform;
            }
        }
           
        TerrainLoader.ImportTerrain(level);

        /*
        Lighting -- Still don't know why Z coord has to be reversed + Y coord slightly increased...
        */

        Debug.Log("=============================================================");

        foreach (var light in level.GetLights()) 
        {
            GameObject lightObj = new GameObject();

            lightObj.transform.rotation = UnityUtils.QuatFromLibLGT(light.rotation);

            light.position.Z *= -1.0f;
            light.position.Y += .2f;
            lightObj.transform.position = UnityUtils.Vec3FromLib(light.position);

            lightObj.name = light.name;


            UnityEngine.Light lightComp = lightObj.AddComponent<UnityEngine.Light>();
            lightComp.color = UnityUtils.ColorFromLib(light.color);
            lightComp.intensity = 1;

            LibSWBF2.Enums.LightType ltype = light.lightType;

            if (ltype == LibSWBF2.Enums.LightType.Omni)
            {   
                lightComp.type = UnityEngine.LightType.Point;
                lightComp.range = light.range;
                
            }
            else if (ltype == LibSWBF2.Enums.LightType.Spot)
            {
                lightComp.type = UnityEngine.LightType.Spot;
                lightComp.range = light.range;
                lightComp.spotAngle = light.spotAngles.X * Mathf.Rad2Deg;   
            }
            else if (ltype == LibSWBF2.Enums.LightType.Dir)
            {
                lightComp.type = UnityEngine.LightType.Directional;
                lightComp.intensity = 1;
                //lightComp.range = light.range;
                //lightComp.spotAngle = light.spotAngles.X * Mathf.Rad2Deg;   
            }
            else 
            {
                DestroyImmediate(lightObj);
            }
        }
        

        //Basic skybox loading
        foreach (var model in level.GetModels())
        {
            GameObject newObj = null;
            try {
                if (model.Name.Contains("sky")) //best effort
                {
                    newObj = new GameObject(model.Name);

                    if (!ModelLoader.AddModelComponents(ref newObj, model.Name))
                    {
                        DestroyImmediate(newObj);
                        continue;
                    }
                }
            } catch {
                Debug.Log("Couldn't load sky...");
                continue;
            }

            if (newObj != null){
                newObj.transform.localScale = new UnityEngine.Vector3(-200,200,200);
            }
        }
    }
}
