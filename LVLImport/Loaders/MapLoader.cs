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
    public static void ImportMap()
    {
        LibSWBF2.Logging.Logger.SetLogLevel(ELogType.Warning);
        LibSWBF2.Logging.Logger.OnLog += (LoggerEntry logEntry) => 
        {
            Debug.Log(logEntry.ToString());
        };

        Level level;
        string fileName = EditorUtility.OpenFilePanelWithFilters("Open LVL File", "", new string[] { "SWBF2 LVL File", "lvl" });
        
        FileInfo file = new FileInfo(fileName);

        if (file.Exists) {
            
            level = Level.FromFile(fileName);

            if (level == null) {
                EditorUtility.DisplayDialog("Error", "Error while opening " + file.FullName, "ok");
                return;
            }
        }
        else {
            EditorUtility.DisplayDialog("Not found!", fileName + " could not be found!", "ok");
            return;
        }

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
            Debug.Log("On world number " + i++);
            Instance[] instances = world.GetInstances();

            foreach (Instance inst in instances)
            {
                Model model = null;
                string geometryName = inst.GetModelName();
                try {
                    model = level.GetModel(geometryName);
                    string tstname = model.Name;
                } catch (Exception e){
                    Debug.Log("Model not found: " + inst.Name);
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
                    //Debug.Log("Model not found!");
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
            lightComp.color = new Color(light.color.X, light.color.Y, light.color.Z);
            lightComp.intensity = 20;

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
            else 
            {
                DestroyImmediate(lightObj);
            }
        }

        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;


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
