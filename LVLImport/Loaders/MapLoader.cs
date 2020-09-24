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

    //[MenuItem("SWBF2/Import Map", false, 1)]
    public static void ImportMap(string path)
    {
        LibSWBF2.Logging.Logger.SetLogLevel(ELogType.Warning);
        LibSWBF2.Logging.Logger.OnLog += (LoggerEntry logEntry) => 
        {
            Debug.Log(logEntry.ToString());
        };

        Level level = Level.FromFile(path);

        World[] worlds = level.GetWorlds();
        

        foreach (World world in worlds)
        {
            
            Instance[] instances = world.GetInstances();
            foreach (Instance inst in instances)
            {
                Model model = null;
                string geometryName = inst.GetModelName();
                try {
                    model = level.GetModel(geometryName);
                    string tstname = model.Name;
                } catch (Exception e){
                    //Debug.Log("Model not found: " + inst.Name);
                    continue;
                }

                if (model != null)
                {
                    GameObject newObj = ModelLoader.GameObjectFromModel(level, model);

                    if (newObj != null)
                    {
                        newObj.transform.position = UnityUtils.Vec3FromLib(inst.GetPosition());
                        newObj.transform.rotation = UnityUtils.QuatFromLib(inst.GetRotation());
                    }
                }
                else 
                {
                }
            }
        }
           
        TerrainLoader.ImportTerrain(level);



        /*
        Lighting -- Still don't know why Z coord has to be reversed + Y coord slightly increased...
        */

        foreach (var light in level.GetLights()) 
        {
            GameObject lightObj = new GameObject();
            light.position.Z *= -1.0f;
            lightObj.transform.position = UnityUtils.Vec3FromLib(light.position);
            lightObj.transform.rotation = UnityUtils.QuatFromLib(light.rotation);
            lightObj.name = light.name;

            UnityEngine.Light lightComp = lightObj.AddComponent<UnityEngine.Light>();
            lightComp.color = UnityUtils.ColorFromLib(light.color);
            lightComp.intensity = 3;

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

        RenderSettings.ambientLight = Color.white;

        /*
        Basic skybox loading
        */

        foreach (var model in level.GetModels())
        {
            GameObject newObj = null;
            try {
                if (model.Name.Contains("sky")) //best effort
                {
                    newObj = ModelLoader.GameObjectFromModel(level,model);
                }
            } catch {
                Debug.Log("Couldn't load sky...");
                continue;
            }

            if (newObj != null){
                newObj.transform.localScale = new UnityEngine.Vector3(200,200,200);
            }
        }
    }
}
