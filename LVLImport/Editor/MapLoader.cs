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

    public static UnityEngine.Vector4 Vec3FromLib(LibSWBF2.Types.Vector4 vec)
    {
        UnityEngine.Vector4 newVec = new UnityEngine.Vector4();
        newVec.x = vec.X;
        newVec.y = vec.Y;
        newVec.z = vec.Z;
        newVec.w = vec.W;
        return newVec;
    }

    [MenuItem("SWBF2/Import Map", false, 1)]
    public static void ImportMap()
    {
        LibSWBF2.Logging.Logger.SetLogLevel(ELogType.Warning);
        LibSWBF2.Logging.Logger.OnLog += (LoggerEntry logEntry) => 
        {
            Debug.Log(logEntry.ToString());
        };

        Debug.Log("Loading... This might take a while...");
        //Level level = Level.FromFile(@"/home/will/Desktop/geo1.lvl");
        //Level level = Level.FromFile(@"/home/will/.wine32bit/drive_c/Program Files/Steam/steamapps/common/Star Wars Battlefront II/GameData/data/_lvl_pc/pol/pol1.lvl");
        Level level = Level.FromFile(@"/Users/will/Desktop/geo1.lvl");
        //Level level = Level.FromFile(@"/Users/will/Desktop/terrainblendinglvls/TST_Tex3_Tex2_Blended.lvl");
        //Level level = Level.FromFile(@"/Users/will/Desktop/terrainblendinglvls/TST_Square_Tex1_Tex2_Blended.lvl");

        Debug.Log("Read lvl file!");

        World[] worlds = level.GetWorlds();
        
        int i = 0;

        foreach (World world in worlds)
        {
            Debug.Log("On world number " + i++);
            Instance[] instances = world.GetInstances();

            foreach (Instance inst in instances)
            {
                Model model = null;
                try {
                    model = level.GetModel(inst.Name);
                    //string tstname = model.Name;
                } catch (Exception e){
                    Debug.Log("Model not found: " + inst.Name);
                    continue;
                }

                if (model != null)
                {
                    GameObject newObj = ModelLoader.GameObjectFromModel(level, model);

                    if (newObj != null)
                    {
                        newObj.transform.position = MapLoader.Vec3FromLib(inst.GetPosition());
                        newObj.transform.rotation = MapLoader.QuatFromLib(inst.GetRotation());
                    }
                }
                else 
                {
                	//Debug.Log("Model not found!");
                }
            }
        }
           

        TerrainLoader.ImportTerrain(level);


        //Basic lights
        foreach (var light in level.GetLights()) 
        {
            switch (light.lightType) 
            {
                GameObject lightObj = new GameObject();
                lightObj.transform.position = Vec3FromLib(light.position);
                lightObj.transform.rotation = Vec4FromLib(light.rotation);

                UnityEngine.Light lightComp = lightObj.AddComponent<UnityEngine.Light>();

                case LightType.Omni:
                    lightComp.color = Vec3FromLib(light.color);
                    lightComp.type  = UnityEngine.LightType.Point;
                    lightComp.range = light.range; 
                    break;

                case LightType.Spot:
                    lightComp.type      = UnityEngine.LightType.Spot;
                    lightComp.spotAngle = light.spotAngles.X * Mathf.Rad2Deg;
                    break;

                default:
                    DestroyImmediate(newObject);
                    break;
            }
        }
    }
}
