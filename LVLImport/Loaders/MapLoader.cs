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

        AnimationLoader.LoadAndAttachAnimation(level, "grab", "tat3_bldg_rancor");
        return;


        World[] worlds = level.GetWorlds();


        foreach (World world in worlds)
        {
        	//Debug.Log("On world: " + world.Name);

        	GameObject worldRoot = new GameObject();
        	worldRoot.name = world.Name;
            
            Instance[] instances = world.GetInstances();
            foreach (Instance inst in instances)
            {
                Model model = null;
                var ec = level.GetEntityClass(inst.GetEntityClassName());

                if (ec == null)
                {
                    continue;
                }

                try {
                    string geometryName = ec.GetProperty("GeometryName");
                    model = level.GetModel(geometryName);
                    string tstname = model.Name;
                } catch (Exception e){
                    //Debug.Log("Model not found: " + inst.Name);
                    continue;
                }

                if (model != null)
                {
                    string objectName = inst.Name.Equals("") ? model.Name : inst.Name;
                    GameObject newObj = new GameObject(objectName);

                    if (ModelLoader.AddModelComponents(level, ref newObj, model))
                    {
                        newObj.transform.localScale = new UnityEngine.Vector3(-1.0f,1.0f,1.0f);
                        newObj.transform.rotation = UnityUtils.QuatFromLib(inst.GetRotation());
                        newObj.transform.position = UnityUtils.Vec3FromLib(inst.GetPosition());
                        newObj.transform.parent = worldRoot.transform;



                    }
                    else 
                    {
                        DestroyImmediate(newObj);
                    }
                }
                else 
                {
                }

                var attached = ec.GetProperty("AttachODF");
                if (attached != null && attached != "")
                {
                    var attachedEC = level.GetEntityClass(attached);
                    
                    if (attachedEC != null)
                    {
                        string gName = attachedEC.GetProperty("GeometryName");
                        Model attachedModel = level.GetModel(gName);

                        try {

                            if (gName != "")
                            {
                                GameObject attachedObj = new GameObject(gName);
                                ModelLoader.AddModelComponents(level, ref attachedObj, attachedModel);

                                //attachedObj.transform.localScale = new UnityEngine.Vector3(-1.0f,1.0f,1.0f);
                                //newObj.transform.rotation = UnityUtils.QuatFromLib(inst.GetRotation());
                                //newObj.transform.position = UnityUtils.Vec3FromLib(inst.GetPosition());
                                //attachedObj.transform.parent = worldRoot.transform;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.Log("Failed to load skinned model: " + gName);
                            Debug.Log("Exception string: " + e.ToString());
                        }

                    }   
                }
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

                    if (!ModelLoader.AddModelComponents(level, ref newObj, model))
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
