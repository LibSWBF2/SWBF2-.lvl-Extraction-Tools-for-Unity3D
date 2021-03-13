using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;

using LibSWBF2.Wrappers;
using LibSWBF2.Enums;

using LibTerrain = LibSWBF2.Wrappers.Terrain;
using UMaterial = UnityEngine.Material;
using LibVec3 = LibSWBF2.Types.Vector3;
using ULight = UnityEngine.Light;



public class WorldLoader : Loader
{
    public bool ImportTerrain = true;
    public bool TerrainAsMesh = false;
    public static bool UseHDRP;
    public static WorldLoader Instance { get; private set; } = null;


    Dictionary<string, GameObject> loadedSkydomes;
    Dictionary<string, Collider> LoadedRegions;

    string[] BlendUniforms = new string[4] 
    {
        "Texture2D_e354f4a9e36f4302a8feaefa8efd534f",   // Blend0
        "Texture2D_495af9007a884b93af8983df8b78ffb8",   // Blend1
        "Texture2D_174e3d4b45f741aaa690bfad9266cec2",   // Blend2
        "Texture2D_0661f9003d2e44729888f3e8310ba999",   // Blend3
    };


    static WorldLoader()
    {
        Instance = new WorldLoader();
    }

    private WorldLoader()
    {
        loadedSkydomes = new Dictionary<string, GameObject>();
        LoadedRegions = new Dictionary<string, Collider>();
    }

    public void Reset()
    {
        loadedSkydomes.Clear();
        LoadedRegions.Clear();
    }

    public void ImportWorld(World world)
    {
        ImportWorld(world, out _);
    }

    public GameObject ImportWorld(World world, out bool hasTerrain)
    {
        MaterialLoader.UseHDRP = UseHDRP;

        hasTerrain = false;
        GameObject worldRoot = new GameObject(world.Name);

        //Regions - Import before instances, since instances may reference regions
        var regionsRoot = ImportRegions(world.GetRegions());
        regionsRoot.transform.parent = worldRoot.transform;

        //Instances
        GameObject instancesRoot = new GameObject("Instances");
        instancesRoot.transform.parent = worldRoot.transform;

        List<GameObject> instances = ImportInstances(world.GetInstances());
        foreach (GameObject instanceObject in instances)
        {
            instanceObject.transform.parent = instancesRoot.transform;
        }
        
        //Terrain
        if (ImportTerrain)
        {
            var terrain = world.GetTerrain();
            if (terrain != null && !hasTerrain)
            {
                GameObject terrainGameObject;
                if (TerrainAsMesh)
                {
                    if (UseHDRP)
                    {
                        terrainGameObject = ImportTerrainAsMeshHDRP(terrain);
                    }
                    else
                    {
                        terrainGameObject = ImportTerrainAsMesh(terrain, world.Name);
                    }
                }
                else 
                {
                    terrainGameObject = ImportTerrainAsUnity(terrain, world.Name);
                }

                terrainGameObject.transform.parent = worldRoot.transform;
                hasTerrain = true;
            }
        }


        //Lighting
        var lightingRoots = ImportLights(container.FindConfig(ConfigType.Lighting, world.Name)); 
        foreach (var lightingRoot in lightingRoots)
        {
            lightingRoot.transform.parent = worldRoot.transform;
        }


        //Skydome, check if already loaded first
        if (!loadedSkydomes.ContainsKey(world.SkydomeName))
        {
            var skyRoot = ImportSkydome(container.FindConfig(ConfigType.Skydome, world.SkydomeName));
            if (skyRoot != null)
            {
                skyRoot.transform.parent = worldRoot.transform;
            }

            loadedSkydomes[world.SkydomeName] = skyRoot;
        }

        return worldRoot;
    }

    public Collider GetRegion(string regionName)
    {
        if (LoadedRegions.TryGetValue(regionName, out Collider region))
        {
            return region;
        }
        return null;
    }

    private List<GameObject> ImportInstances(Instance[] instances)
    {
        List<GameObject> instanceObjects = new List<GameObject>();

        foreach (Instance inst in instances)
        {
            string entityClassName = inst.EntityClassName;
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
                    //instanceObject = ClassLoader.Instance.LoadGeneralClass(entityClassName,true);
                    instanceObject = ClassLoader.Instance.LoadInstance(inst);
                    break;

                default:
                    continue;
            }

            instanceObject.transform.rotation = UnityUtils.QuatFromLibWorld(inst.Rotation);
            instanceObject.transform.position = UnityUtils.Vec3FromLibWorld(inst.Position);
            instanceObject.transform.localScale = new Vector3(1.0f,1.0f,1.0f);
            instanceObjects.Add(instanceObject);
        }

        return instanceObjects;
    }



    private GameObject ImportTerrainAsMesh(LibTerrain terrain, string name)
    {
        Mesh terrainMesh = new Mesh();
        if (SaveAssets)
        {
            AssetDatabase.CreateAsset(terrainMesh, Path.Combine(SaveDirectory, name + "_terrain.mesh"));
        }

        terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        terrainMesh.vertices = terrain.GetPositionsBuffer<Vector3>();
        terrainMesh.triangles = Array.ConvertAll(terrain.GetIndexBuffer(), s => ((int) s));
        terrainMesh.RecalculateNormals();

        GameObject terrainObj = new GameObject("Terrain");
        terrainObj.isStatic = true;

        MeshFilter filter = terrainObj.AddComponent<MeshFilter>();
        filter.sharedMesh = terrainMesh;
       
        UMaterial terrainMat = new UMaterial(MaterialLoader.DefaultTerrainSTDMaterial);
        if (SaveAssets)
        {
            AssetDatabase.CreateAsset(terrainMat, Path.Combine(SaveDirectory, name + "_terrain.mat"));
        }

        MeshRenderer renderer = terrainObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMat;

        int i = 0;
        foreach (string texName in terrain.LayerTextures)
        {
            Texture2D tex = TextureLoader.Instance.ImportTexture(texName);
            string layerTexName = "_LayerXXTex".Replace("XX", i.ToString());

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

            if (SaveAssets)
            {
                string blendSlicePath = SaveDirectory + "/blendmap_slice_" + i.ToString() + ".png";
                File.WriteAllBytes(blendSlicePath, blendTex.EncodeToPNG());
                AssetDatabase.ImportAsset(blendSlicePath, ImportAssetOptions.Default);
                blendTex = (Texture2D) AssetDatabase.LoadAssetAtPath(blendSlicePath, typeof(Texture2D));
            }

            renderer.sharedMaterial.SetTexture("_BlendMap" + i.ToString(), blendTex);
        }

        terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
        float bound = (float) (dim * dimScale);
        renderer.sharedMaterial.SetFloat("_XBound", bound);
        renderer.sharedMaterial.SetFloat("_ZBound", bound);

        if (SaveAssets)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(terrainObj, SaveDirectory + "/" + name + "_terrain.prefab", InteractionMode.UserAction);
        }

        terrainObj.transform.localScale = new UnityEngine.Vector3(1.0f,1.0f,-1.0f);
        return terrainObj;
    }


    public GameObject ImportTerrainAsMeshHDRP(LibTerrain terrain)
    {
        Mesh terrainMesh = new Mesh();

        terrainMesh.indexFormat = IndexFormat.UInt32;
        terrainMesh.vertices = terrain.GetPositionsBuffer<Vector3>();
        terrainMesh.triangles = Array.ConvertAll(terrain.GetIndexBuffer(), s => ((int)s));
        terrainMesh.RecalculateNormals();

        GameObject terrainObj = new GameObject("Terrain");
        terrainObj.isStatic = true;

        MeshFilter filter = terrainObj.AddComponent<MeshFilter>();
        filter.sharedMesh = terrainMesh;

        UMaterial terrainMat = new UMaterial(MaterialLoader.DefaultTerrainHDRPMaterial);

        MeshRenderer renderer = terrainObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMat;

        string[] layerNames = new string[terrain.LayerTextures.Count];
        terrain.LayerTextures.CopyTo(layerNames, 0);
        Texture2DArray layers = TextureLoader.Instance.ImportTextures(layerNames, out float[] xAbsDims);
        terrainMat.SetTexture("Texture2DArray_7458b9063e46411289f9d5f3dc012ed7", layers);
        terrainMat.SetFloat("Vector1_665d2bac01fe4570bbe0622def4f5bce", layers.depth);

        terrain.GetBlendMap(out uint blendDim, out uint numLayers, out byte[] blendMapRaw);
        for (int i = 0; i < 4; i++)
        {
            Texture2D blendTex = new Texture2D((int)blendDim, (int)blendDim);

            Color[] colors = blendTex.GetPixels(0);

            for (int w = 0; w < blendDim; w++)
            {
                for (int h = 0; h < blendDim; h++)
                {
                    Color col = Color.black;
                    int baseIndex = (int)(numLayers * (w * blendDim + h));
                    int offset = i * 4;

                    for (int z = 0; z < 4; z++)
                    {
                        if (offset + z < numLayers)
                        {
                            col[z] = ((float)blendMapRaw[baseIndex + offset + z]) / 255.0f;
                        }
                    }

                    colors[(blendDim - w - 1) * blendDim + h] = col;
                }
            }

            blendTex.SetPixels(colors, 0);
            blendTex.Apply();

            renderer.sharedMaterial.SetTexture(BlendUniforms[i], blendTex);
        }

        terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
        float bound = (float)(dim * dimScale);
        renderer.sharedMaterial.SetFloat("Vector1_49103558bb1244ff8ac124e1bd984b90", bound);

        Vector4[] layerTexDims = new Vector4[4];
        float absDimMax = 0.0f;
        Debug.Assert(xAbsDims.Length <= 16);
        for (int i = 0; i < xAbsDims.Length; ++i)
        {
            absDimMax = Mathf.Max(absDimMax, xAbsDims[i]);
        }
        for (int i = 0; i < xAbsDims.Length; ++i)
        {
            layerTexDims[i / 4][i % 4] = xAbsDims[i] / absDimMax;
        }

        renderer.sharedMaterial.SetVector("Vector4_1e6425e6507a4b929dc007ed28cce2a1", layerTexDims[0]);
        renderer.sharedMaterial.SetVector("Vector4_bb80c51fa149447d9ecd026c6a02f191", layerTexDims[1]);
        renderer.sharedMaterial.SetVector("Vector4_0d4ad4ff048e46a8aa2f1787736e6b9f", layerTexDims[2]);
        renderer.sharedMaterial.SetVector("Vector4_bbab58e8dc3648e2951866e087bc80dd", layerTexDims[3]);

        terrainObj.transform.localScale = new UnityEngine.Vector3(1.0f, 1.0f, -1.0f);
        return terrainObj;
    }


    private GameObject ImportTerrainAsUnity(LibTerrain terrain, string name)
    {
        //Read heightmap
        terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
        float floor = terrain.HeightLowerBound;
        float ceiling = terrain.HeightUpperBound;
        
        TerrainData terData = new TerrainData();

        if (SaveAssets)
        {
            AssetDatabase.CreateAsset(terData, SaveDirectory + "/" + name + "_terrain_data.asset");
        }

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
        foreach (string texName in terrain.LayerTextures)
        {
            Texture2D tex = TextureLoader.Instance.ImportTexture(texName);
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
        
        if (SaveAssets)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(terrainObj, SaveDirectory + "/" + name + "_terrain.prefab", InteractionMode.UserAction);
        }

        return terrainObj;
    }



    /*
    Lighting -- Still don't know why Z coord has to be reversed + Y coord slightly increased...
    */
    public List<GameObject> ImportLights(Config lightingConfig, bool SetAmbient=false)
    {
        List<GameObject> lightObjects = new List<GameObject>();

        if (lightingConfig == null) return lightObjects;


        GameObject globalLightsRoot = new GameObject("GlobalLights");
        lightObjects.Add(globalLightsRoot);

        string light1Name = "", light2Name = "";
        Field globalLighting = lightingConfig.GetField("GlobalLights");
        if (globalLighting != null)
        {
            Scope gl = globalLighting.Scope;
            light1Name = gl.GetField("Light1").GetString();
            light2Name = gl.GetField("Light2").GetString();

            Color topColor = UnityUtils.ColorFromLib(gl.GetField("Top").GetVec3(), true);
            Color bottomColor = UnityUtils.ColorFromLib(gl.GetField("Bottom").GetVec3(), true);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientGroundColor = bottomColor;
            RenderSettings.ambientSkyColor = topColor;
        }


        Field[] lightFields = lightingConfig.GetFields("Light");

        GameObject localLightsRoot = new GameObject("LocalLights");
        lightObjects.Add(localLightsRoot);

        bool sunFound = false;
        foreach (Field light in lightFields) 
        {
            string lightName = light.GetString();
            Scope sl = light.Scope;

            bool IsGlobal = string.Equals(lightName, light1Name, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(lightName, light2Name, StringComparison.OrdinalIgnoreCase);


            GameObject lightObj = new GameObject(lightName);

            lightObj.transform.rotation = UnityUtils.QuatFromLibLGT(sl.GetVec4("Rotation"));

            LibVec3 lightPos = sl.GetVec3("Position");

            lightPos.Z *= -1.0f;
            lightPos.Y += .2f;
            lightObj.transform.position = UnityUtils.Vec3FromLibWorld(lightPos);

            float ltype = sl.GetFloat("Type");
            float range = sl.GetFloat("Range");


            if (UseHDRP)
            {
                HDAdditionalLightData lightComp = null;

                if (ltype == 2.0f)
                {
                    lightComp = lightObj.AddHDLight(HDLightTypeAndShape.Point);
                    lightComp.intensity = 10000.0f;
                }
                else if (ltype == 3.0f)
                {
                    lightComp = lightObj.AddHDLight(HDLightTypeAndShape.ConeSpot);
                    lightComp.intensity = 10000.0f;
                }
                else if (ltype == 1.0f)
                {
                    lightComp = lightObj.AddHDLight(HDLightTypeAndShape.Directional);
                    lightComp.intensity = 400000.0f;
                }

                lightComp.EnableColorTemperature(false);
                lightComp.color = UnityUtils.ColorFromLib(sl.GetVec3("Color"));
                if (!sunFound)
                {
                    lightComp.EnableShadows(true);
                    lightComp.shadowUpdateMode = ShadowUpdateMode.EveryFrame;
                    sunFound = true;
                }
            }
            else
            {
                ULight lightComp = lightObj.AddComponent<ULight>();
                lightComp.color = UnityUtils.ColorFromLib(sl.GetVec3("Color"));

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
                    lightComp.spotAngle = sl.GetVec2("Cone").X * Mathf.Rad2Deg;   
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
                    Debug.LogWarning("Cant handle light type for " + light.Name + " yet");
                    continue;
                }

                lightComp.shadows = LightShadows.Soft;
                lightComp.lightmapBakeType = LightmapBakeType.Realtime;
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


    public GameObject ImportSkydome(Config skydomeConfig)
    {
        if (skydomeConfig == null) return null;


        GameObject skyRoot = new GameObject("Skydome");

        //Import dome
        GameObject domeRoot = new GameObject("Dome");
        domeRoot.transform.parent = skyRoot.transform;
        
        Field domeInfo = skydomeConfig.GetField("DomeInfo");
        if (domeInfo != null)
        {
            Scope sDi = domeInfo.Scope;

            //Havent decided re this yet
            Color ambient = UnityUtils.ColorFromLib(sDi.GetVec3("Ambient"));

            Field[] domeModelFields = sDi.GetFields("DomeModel");
            foreach (Field domeModelField in domeModelFields)
            {
                Scope sD = domeModelField.Scope;
                string geometryName = sD.GetString("Geometry");
                GameObject domeModelObj = new GameObject(geometryName);

                ModelLoader.Instance.AddModelComponents(domeModelObj, geometryName, false, true);

                if (SaveAssets)
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(domeModelObj, SaveDirectory + "/dome_model_" + geometryName + ".prefab", InteractionMode.UserAction);
                }

                domeModelObj.transform.localScale = new Vector3(-300,300,300);
                domeModelObj.transform.parent = domeRoot.transform;
            }
        }


        //Import dome objects, one of each for now
        GameObject domeObjectsRoot = new GameObject("SkyObjects");
        domeObjectsRoot.transform.parent = skyRoot.transform;

        Field[] domeObjectFields = skydomeConfig.GetFields("SkyObject");
        foreach (Field domeObjectField in domeObjectFields)
        {
            string geometryName = domeObjectField.Scope.GetString("Geometry");
            GameObject domeObject = new GameObject(geometryName);

            ModelLoader.Instance.AddModelComponents(domeObject, geometryName, false);

            if (SaveAssets)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(domeObject, SaveDirectory + "/dome_object_" + geometryName + ".prefab", InteractionMode.UserAction);
            }

            domeObject.transform.parent = domeObjectsRoot.transform;
            domeObject.transform.localPosition = new Vector3(0, domeObjectField.Scope.GetVec2("Height").X, 0);
        }

        return skyRoot;
    }
    


    public GameObject ImportRegions(Region[] regions)
    {
        GameObject regionsRoot = new GameObject("Regions");
        foreach (Region region in regions)
        {
            GameObject regionObj = new GameObject(region.Name);
            regionObj.transform.position = UnityUtils.Vec3FromLibWorld(region.Position);
            regionObj.transform.rotation = UnityUtils.QuatFromLibWorld(region.Rotation);

            LibVec3 sz = region.Size;

            Collider collider = null;
            if (region.Type == "box")
            {
                BoxCollider coll = regionObj.AddComponent<BoxCollider>();
                coll.size = new Vector3(sz.X, sz.Y, sz.Z);
                collider = coll;
            }
            else if (region.Type == "sphere")
            {
                SphereCollider coll = regionObj.AddComponent<SphereCollider>();
                coll.radius = sz.X;
                collider = coll;
            }
            else if (region.Type == "cylinder")
            {
                MeshCollider coll = regionObj.AddComponent<MeshCollider>();
                coll.convex = true;
                coll.sharedMesh = ModelLoader.CylinderCollision;
                regionObj.transform.localScale = new Vector3(sz.X, sz.Y, sz.Z);
                collider = coll;
            }
            else
            {
                throw new Exception(string.Format("IMPLEMENT '{0}'!", region.Type));
            }

            collider.isTrigger = true;
            regionObj.transform.parent = regionsRoot.transform;

            if (!LoadedRegions.ContainsKey(region.Name))
            {
                LoadedRegions.Add(region.Name, collider);
            }
        }

        return regionsRoot;
    }
}
