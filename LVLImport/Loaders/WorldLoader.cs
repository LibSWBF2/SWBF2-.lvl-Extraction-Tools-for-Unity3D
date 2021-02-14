using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Enums;
using LibSWBF2.Utils;

using LibTerrain = LibSWBF2.Wrappers.Terrain;
using UMaterial = UnityEngine.Material;
using LibVec3 = LibSWBF2.Types.Vector3;
using ULight = UnityEngine.Light;



public class WorldLoader : Loader {

    public static bool TerrainAsMesh = false;

    private static Dictionary<string, GameObject> loadedSkydomes;

    static WorldLoader()
    {
        loadedSkydomes = new Dictionary<string, GameObject>();
    }

    public static void Reset()
    {
        loadedSkydomes = new Dictionary<string, GameObject>();
    }


    public static void ImportWorld(World world)
    {
        bool LoadedTerrain = false;

    	GameObject worldRoot = new GameObject(world.name);


        //Instances
        GameObject instancesRoot = new GameObject("Instances");
        instancesRoot.transform.parent = worldRoot.transform;
        foreach (GameObject instanceObject in ImportInstances(world.GetInstances()))
        {
            instanceObject.transform.parent = instancesRoot.transform;
        }
        

        //Terrain
        var terrain = world.GetTerrain();
        if (terrain != null && !LoadedTerrain)
        {
            GameObject terrainGameObject;
            if (TerrainAsMesh)
            {
                terrainGameObject = ImportTerrainAsMesh(terrain);
            }
            else 
            {
                terrainGameObject = ImportTerrain(terrain);
            }

            terrainGameObject.transform.parent = worldRoot.transform;
            LoadedTerrain = true;
        }


        //Lighting
        var lightingRoots = ImportLights(container.FindConfig(ConfigType.Lighting, world.name)); 
        foreach (var lightingRoot in lightingRoots)
        {
            lightingRoot.transform.parent = worldRoot.transform;
        }


        //Regions
        var regionsRoot = ImportRegions(world.GetRegions());
        regionsRoot.transform.parent = worldRoot.transform;


        //Skydome, check if already loaded first
        if (!loadedSkydomes.ContainsKey(world.skydomeName))
        {
            var skyRoot = ImportSkydome(container.FindConfig(ConfigType.Skydome, world.skydomeName));
            if (skyRoot != null)
            {
                skyRoot.transform.parent = worldRoot.transform;
            }

            loadedSkydomes[world.skydomeName] = skyRoot;
        }
    }


    private static List<GameObject> ImportInstances(Instance[] instances)
    {
        List<GameObject> instanceObjects = new List<GameObject>();
        foreach (Instance inst in instances)
        {
            string entityClassName = inst.entityClassName;
            string baseName = ClassLoader.GetBaseClassName(entityClassName);

            GameObject instanceObject = null;

            switch (baseName)
            {
                case "door":
                case "animatedprop":                  
                case "prop":
                case "building":
                case "destructablebuilding":
                case "armedbuilding":
                case "animatedbuilding":
                case "commandpost":
                    instanceObject = ClassLoader.LoadGeneralClass(entityClassName);
                    break;

                default:
                    break; 
            }

            if (instanceObject == null)
            {
                continue;
            }

            if (!inst.name.Equals(""))
            {
                instanceObject.name = inst.name;
            }

            instanceObject.transform.rotation = UnityUtils.QuatFromLibWorld(inst.rotation);
            instanceObject.transform.position = UnityUtils.Vec3FromLibWorld(inst.position);
            instanceObject.transform.localScale = new Vector3(-1.0f,1.0f,1.0f);
            instanceObjects.Add(instanceObject);
        }

        return instanceObjects;
    }



    private static GameObject ImportTerrainAsMesh(LibTerrain terrain)
    {
        Mesh terrainMesh = new Mesh();
        terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        terrainMesh.vertices = terrain.GetPositionsBuffer<Vector3>();
        terrainMesh.triangles = Array.ConvertAll(terrain.GetIndexBuffer(), s => ((int) s));
        terrainMesh.RecalculateNormals();

        GameObject terrainObj = new GameObject("Terrain");

        MeshFilter filter = terrainObj.AddComponent<MeshFilter>();
        filter.sharedMesh = terrainMesh;

        MeshRenderer renderer = terrainObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = new UMaterial(Shader.Find("ConversionAssets/TerrainTest"));

        int i = 0;
        foreach (string texName in terrain.layerTextures)
        {
            Texture2D tex = TextureLoader.ImportTexture(texName);
            string layerTexName = "_LayerXXTex".Replace("XX",i.ToString());

            if (tex != null)
            {
                renderer.sharedMaterial.SetTexture(layerTexName, tex);  
            }
            i++;
        }

        terrain.GetBlendMap(out uint blendDim, out uint numLayers, out byte[] blendMapRaw);  
        
        for (i = 0; i < 4; i++)
        {
            Texture2D blendTex = new Texture2D((int) blendDim, (int) blendDim);
            Color[] colors = blendTex.GetPixels(0);

            for (int w = 0; w < blendDim; w++)
            {
                for (int h = 0; h < blendDim; h++)
                {
                    Color col = Color.black;
                    int baseIndex = (int) (numLayers * (w * blendDim + h));
                    int offset = i * 4;

                    for (int z = 0; z < 4; z++)
                    {
                        if (offset + z < numLayers)
                        {
                            col[z] = ((float) blendMapRaw[baseIndex + offset + z]) / 255.0f;  
                        }
                    }

                    colors[(blendDim - w - 1) * blendDim + h] = col;
                }
            } 

            blendTex.SetPixels(colors,0);
            blendTex.Apply();

            renderer.sharedMaterial.SetTexture("_BlendMap" + i.ToString(), blendTex);
        }

        terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
        float bound = (float) (dim * dimScale);
        renderer.sharedMaterial.SetFloat("_XBound", bound);
        renderer.sharedMaterial.SetFloat("_ZBound", bound);


        terrainObj.transform.localScale = new UnityEngine.Vector3(1.0f,1.0f,-1.0f);
        return terrainObj;
    }





    private static GameObject ImportTerrain(LibTerrain terrain)
    {
        //Read heightmap
        terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
        float floor = terrain.heightLowerBound;
        float ceiling = terrain.heightUpperBound;
        
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
        terData.SetHoles(0,0,holes);
        

        //Get list of textures used
        List<Texture2D> terTextures = new List<Texture2D>();
        foreach (string texName in terrain.layerTextures)
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


        //Read blendmap
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

        return terrainObj;
    }



    /*
    Lighting -- Still don't know why Z coord has to be reversed + Y coord slightly increased...
    */
    private static List<GameObject> ImportLights(Config lightingConfig, bool SetAmbient=false)
    {
        List<GameObject> lightObjects = new List<GameObject>();

        if (lightingConfig == null) return lightObjects;


        GameObject globalLightsRoot = new GameObject("GlobalLights");
        lightObjects.Add(globalLightsRoot);

        string light1Name = "", light2Name = "";
        Config globalLighting = lightingConfig.GetChildConfig("GlobalLights");
        if (globalLighting != null)
        {
            light1Name = globalLighting.GetString("Light1");
            light2Name = globalLighting.GetString("Light2");

            Color topColor = UnityUtils.ColorFromLib(globalLighting.GetVec3("Top"), true);
            Color bottomColor = UnityUtils.ColorFromLib(globalLighting.GetVec3("Bottom"), true);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientGroundColor = bottomColor;
            RenderSettings.ambientSkyColor = topColor;
        }



        List<string> lightNames = lightingConfig.GetStrings("Light");
        List<Config> lightConfigs = lightingConfig.GetChildConfigs("Light");

        GameObject localLightsRoot = new GameObject("LocalLights");
        lightObjects.Add(localLightsRoot);

        int i = 0;
        foreach (Config light in lightConfigs) 
        {
            string lightName = lightNames[i++];

            bool IsGlobal = String.Equals(lightName, light1Name, StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(lightName, light2Name, StringComparison.OrdinalIgnoreCase);


            GameObject lightObj = new GameObject(lightName);

            lightObj.transform.rotation = UnityUtils.QuatFromLibLGT(light.GetVec4("Rotation"));

            LibVec3 lightPos = light.GetVec3("Position");

            lightPos.Z *= -1.0f;
            lightPos.Y += .2f;
            lightObj.transform.position = UnityUtils.Vec3FromLibWorld(lightPos);


            ULight lightComp = lightObj.AddComponent<ULight>();
            lightComp.color = UnityUtils.ColorFromLib(light.GetVec3("Color"));

            float ltype = light.GetFloat("Type");
            float range = light.GetFloat("Range");

            if (ltype == 2.0f)
            {   
                lightComp.type = UnityEngine.LightType.Point;
                lightComp.range = range;
                lightComp.intensity = 4.0f;           
            }
            else if (ltype == 3.0f)
            {
                lightComp.type = UnityEngine.LightType.Spot;
                lightComp.range = range;
                lightComp.spotAngle = light.GetVec2("Cone").X * Mathf.Rad2Deg;   
                lightComp.intensity = IsGlobal ? 2.0f : 0.5f;
            }
            else if (ltype == 1.0f)
            {
                lightComp.type = UnityEngine.LightType.Directional;
                lightComp.intensity = IsGlobal ? 1.0f : 0.3f;
                //lightComp.range = light.range;
                //lightComp.spotAngle = light.spotAngles.X * Mathf.Rad2Deg;   
            }
            else 
            {
                Debug.LogWarning("Cant handle light type for " + light.name + " yet");
                continue;
            }

            if (IsGlobal)
            {
                lightObj.transform.SetParent(globalLightsRoot.transform,false);
            }
            else
            {
                lightObj.transform.SetParent(localLightsRoot.transform,false);
            }

        }

        return lightObjects;
    }


    private static GameObject ImportSkydome(Config skydomeConfig)
    {
        if (skydomeConfig == null) return null;


        GameObject skyRoot = new GameObject("Skydome");

        //Import dome
        GameObject domeRoot = new GameObject("Dome");
        domeRoot.transform.parent = skyRoot.transform;
        
        Config domeInfo = skydomeConfig.GetChildConfig("DomeInfo");
        if (domeInfo != null)
        {
            //Havent decided re this yet
            Color ambient = UnityUtils.ColorFromLib(domeInfo.GetVec3("Ambient"));

            List<Config> domeModelConfigs = domeInfo.GetChildConfigs("DomeModel");
            foreach (Config domeModelConfig in domeModelConfigs)
            {
                string geometryName = domeModelConfig.GetString("Geometry");
                GameObject domeModelObj = new GameObject(geometryName);

                ModelLoader.AddModelComponents(domeModelObj, geometryName);
                try {
                    MaterialLoader.PatchMaterial(domeModelObj.transform.GetChild(0).gameObject, "skydome");
                } catch {}

                domeModelObj.transform.localScale = new Vector3(-300,300,300);
                domeModelObj.transform.parent = domeRoot.transform;
            }
        }


        //Import dome objects, one of each for now
        GameObject domeObjectsRoot = new GameObject("SkyObjects");
        domeObjectsRoot.transform.parent = skyRoot.transform;

        List<Config> domeObjectConfigs = skydomeConfig.GetChildConfigs("SkyObject");
        foreach (Config domeObjectConfig in domeObjectConfigs)
        {
            string geometryName = domeObjectConfig.GetString("Geometry");
            GameObject domeObject = new GameObject(geometryName);

            ModelLoader.AddModelComponents(domeObject, geometryName);

            domeObject.transform.parent = domeObjectsRoot.transform;
            domeObject.transform.localPosition = new Vector3(0, domeObjectConfig.GetVec2("Height").X, 0);
        }

        return skyRoot;
    }
    


    private static GameObject ImportRegions(Region[] regions)
    {
        GameObject regionsRoot = new GameObject("Regions");
        foreach (Region region in regions)
        {
            GameObject regionObj = new GameObject(region.name);
            regionObj.transform.position = UnityUtils.Vec3FromLibWorld(region.position);
            regionObj.transform.rotation = UnityUtils.QuatFromLibWorld(region.rotation);

            LibVec3 sz = region.size;

            //if (region.type.Equals("box"))
            //{
            BoxCollider coll = regionObj.AddComponent<BoxCollider>();
            coll.size = new Vector3(sz.X,sz.Y,sz.Z);
            coll.isTrigger = true;
            //}
            //else 
            //{
            //    CapsuleCollider coll = region.AddComponent<CapsuleCollider>();
            //    coll.height = new Vector3(sz.X,sz.Y,sz.Z);
            //}

            regionObj.transform.parent = regionsRoot.transform;
        }

        return regionsRoot;
    }
}
