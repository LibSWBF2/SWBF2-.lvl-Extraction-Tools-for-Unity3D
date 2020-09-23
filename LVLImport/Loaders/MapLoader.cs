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

        //Level level = Level.FromFile(@"/Users/will/Desktop/MLC.lvl");
        //Level level = Level.FromFile(@"/home/will/.wine32bit/drive_c/Program Files/Steam/steamapps/common/Star Wars Battlefront II/GameData/data/_lvl_pc/mus/mus1.lvl");
        //Level level = Level.FromFile(@"/Users/will/Desktop/geo1.lvl");
        //Level level = Level.FromFile(@"/Users/will/Desktop/terrainblendinglvls/TST_Tex3_Tex2_Blended.lvl");
        //Level level = Level.FromFile(@"/Users/will/Desktop/terrainblendinglvls/TST_Square_Tex1_Tex2_Blended.lvl");
        //Level level = Level.FromFile(@"/Volumes/bootable/stockdata/_lvl_pc/fel/fel1.lvl");

        World[] worlds = level.GetWorlds();
        
        int i = 0;

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
            lightComp.intensity = 15;

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
                //lightComp.intensity = 
                //lightComp.range = light.range;
                //lightComp.spotAngle = light.spotAngles.X * Mathf.Rad2Deg;   
            }
            else 
            {
                DestroyImmediate(lightObj);
            }
        }

        /*        
        level.GetGlobalLightingConfig(out LibSWBF2.Types.Vector3 topColor, 
                                      out LibSWBF2.Types.Vector3 bottomColor,
                                      out LibSWBF2.Wrappers.Light l1,
                                      out LibSWBF2.Wrappers.Light l2);
        
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = UnityUtils.ColorFromLib(topColor);
        RenderSettings.ambientGroundColor = UnityUtils.ColorFromLib(bottomColor);
        */
        RenderSettings.ambientLight = Color.white;
        /*
        try {
            Debug.Log("Global light 1: " + l1.name);
        } catch (Exception e){}
        try {
            Debug.Log("Global light 2: " + l2.name);
        } catch (Exception e){}
        */

        /*
        Basic skybox loading
        */

        foreach (var model in level.GetModels())
        {
            GameObject newObj = null;
            try {
                if (model.Name.Contains("sky"))
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
