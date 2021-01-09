using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Enums;


public class MapLoader : ScriptableObject {

    //Imports all worlds for now...
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

                    case "dusteffect":
                        break;

                    case "":
                        break;

                    default:
                        Debug.LogWarning(String.Format("\tEncountered unknown base class: {0} subclassed by: {1}", baseName, entityClassName));
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

        ImportTerrain(level);
        ImportLighting(level);
    }


    private static void ImportTerrain(Level level)
    {
        foreach (var terrain in level.GetTerrains())
        {
            //Read heightmap
            terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
            terrain.GetHeightBounds(out float floor, out float ceiling);
            
            TerrainData terData = new TerrainData();
            terData.heightmapResolution = (int) dim + 1;
            terData.size = new Vector3(dim * dimScale, ceiling - floor, dim * dimScale);
            terData.baseMapResolution = 512;
            terData.SetDetailResolution(512, 8);

            float[,] heights = new float[dim,dim];
            bool[,] holes    = new bool[dim,dim];

            for (int x = 0; x < dim; x++)
            {
                for (int y = 0; y < dim; y++)
                {
                    float h = heightsRaw[(dim - 1 - x) * dim + y];
                    heights[x,y] = h < -0.1 ? 0 : h;
                    holes[x,y] = h < -0.1 ? false : true;
                }
            }
            terData.SetHeights(0, 0, heights);
            //terData.SetHoles(0,0,holes);
            

            //Get list of textures used
            List<Texture2D> terTextures = new List<Texture2D>();
            foreach (string texName in terrain.GetTextureNames())
            {
                Texture2D tex = TextureLoader.ImportTexture(texName);
                if (tex != null)
                {
                    terTextures.Add(tex);  
                }
            }

            terrain.GetBlendMap(out uint blendDim, out uint numLayers, out byte[] blendMapRaw);  


            //Assign layers
            TerrainLayer[] terrainLayers = new TerrainLayer[numLayers];
            
            for (int i = 0; i < numLayers && i < terTextures.Count; i++)
            {
                TerrainLayer newLayer = new TerrainLayer();
                newLayer.diffuseTexture = terTextures[i];
                newLayer.tileSize = new Vector2(32,32);
                terrainLayers[i] = newLayer;
            }

            terData.SetTerrainLayersRegisterUndo(terrainLayers,"Undo");


            //Read splatmap
            float[,,] blendMap = new float[blendDim, blendDim, numLayers];

            for (int y = 0; y < blendDim; y++)
            {
                for (int x = 0; x < blendDim; x++)
                {
                    int baseIndex = (int) (numLayers * (y * blendDim + x));
                    for (int z = 0; z < numLayers; z++)
                    {
                        blendMap[blendDim - y - 1,x,z] = ((float) blendMapRaw[baseIndex + z]) / 255.0f;    
                    }
                }
            }

            terData.alphamapResolution = (int) blendDim;
            terData.SetAlphamaps(0, 0, blendMap);
            terData.SetBaseMapDirty();


            //Save terrain/create gameobj
            GameObject terrainObj = UnityEngine.Terrain.CreateTerrainGameObject(terData);
            int dimOffset = -1 * ((int) (dimScale * dim)) / 2;
            terrainObj.transform.position = new Vector3(dimOffset,floor,dimOffset);
            //PrefabUtility.SaveAsPrefabAsset(terrainObj, Application.dataPath + "/Terrain/terrain.prefab");
            //AssetDatabase.Refresh();
        }
    }



    /*
    Lighting -- Still don't know why Z coord has to be reversed + Y coord slightly increased...
    */
    private static void ImportLighting(Level level)
    {
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
