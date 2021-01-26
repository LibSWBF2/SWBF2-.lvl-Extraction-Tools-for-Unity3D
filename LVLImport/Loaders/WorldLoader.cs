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

using LibTerrain = LibSWBF2.Wrappers.Terrain;
using UMaterial = UnityEngine.Material;



public class WorldLoader : Loader {



    public static bool TerrainAsMesh = false;


    //Imports all worlds for now...
    public static void ImportWorlds(Level level)
    {
        bool LoadedTerrain = false;

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
                    case "prop":
                    case "building":
                    case "destructablebuilding":
                    case "armedbuilding":
                        obj = ClassLoader.LoadGeneralClass(entityClassName);
                        break;

                    default:
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

                obj.transform.rotation = UnityUtils.QuatFromLibWorld(inst.GetRotation());
                obj.transform.position = UnityUtils.Vec3FromLibWorld(inst.GetPosition());
                obj.transform.parent = worldRoot.transform;
            }
        
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

            var lights = ImportLights(world.GetLights());
            foreach (var light in lights)
            {
                light.transform.parent = worldRoot.transform;
            }
        }

        TryImportSkyDome(level);

        AssetDatabase.Refresh();
    }



    private static GameObject ImportTerrainAsMesh(LibTerrain terrain)
    {
        Mesh terrainMesh = new Mesh();
        terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        terrainMesh.vertices = UnityUtils.FloatToVec3Array(terrain.GetPositionsBuffer(), false);
        terrainMesh.normals = UnityUtils.FloatToVec3Array(terrain.GetNormalsBuffer(), false);
        terrainMesh.triangles = Array.ConvertAll(terrain.GetIndexBuffer(), s => ((int) s));
        terrainMesh.RecalculateNormals();

        GameObject terrainObj = new GameObject("Terrain");

        MeshFilter filter = terrainObj.AddComponent<MeshFilter>();
        filter.sharedMesh = terrainMesh;

        MeshRenderer renderer = terrainObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = new UMaterial(Shader.Find("ConversionAssets/TerrainTest"));

        int j = 0;
        foreach (string texName in terrain.GetTextureNames())
        {
            Texture2D tex = TextureLoader.ImportTexture(texName);
            string layerTexName = "_LayerXXTex".Replace("XX",j.ToString());

            if (tex != null)
            {
                renderer.sharedMaterial.SetTexture(layerTexName, tex);  
            }

            if (++j > 3) break;
        }


        terrain.GetBlendMap(out uint blendDim, out uint numLayers, out byte[] blendMapRaw);  
        Texture2D blendTex = new Texture2D((int) blendDim, (int) blendDim);
        Color[] colors = blendTex.GetPixels(0);

        for (int w = 0; w < blendDim; w++)
        {
            for (int h = 0; h < blendDim; h++)
            {
                Color col = Color.black;
                int baseIndex = (int) (numLayers * (w * blendDim + h));

                for (int z = 0; z < (int) numLayers && z < 4; z++)
                {
                    col[z] = ((float) blendMapRaw[baseIndex + z]) / 255.0f;  
                }

                colors[(blendDim - w - 1) * blendDim + h] = col;
            }
        }

        blendTex.SetPixels(colors,0);
        blendTex.Apply();

        renderer.sharedMaterial.SetTexture("_BlendMap", blendTex);

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
        terData.SetHoles(0,0,holes);
        

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
    private static List<GameObject> ImportLights(LibSWBF2.Wrappers.Light[] lights)
    {
        List<GameObject> lightObjects = new List<GameObject>();

        foreach (var light in lights) 
        {
            GameObject lightObj = new GameObject();

            lightObj.transform.rotation = UnityUtils.QuatFromLibLGT(light.rotation);

            light.position.Z *= -1.0f;
            light.position.Y += .2f;
            lightObj.transform.position = UnityUtils.Vec3FromLibWorld(light.position);

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
                Debug.LogWarning("Cant handle light type for " + light.name + " yet");
                continue;
            }

            lightObjects.Add(lightObj);
        }

        return lightObjects;
    }
        

    private static void TryImportSkyDome(Level level)
    {
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
                        Debug.LogError("Skydome model creation failed...");
                        continue;
                    }

                    MaterialLoader.PatchMaterial(ref newObj, "skydome");
                }
            } catch {
                Debug.LogWarning("Didn't find obvious sky model...");
                continue;
            }

            if (newObj != null){
                newObj.transform.localScale = new UnityEngine.Vector3(300,300,300);
            }
        }
    }
}
