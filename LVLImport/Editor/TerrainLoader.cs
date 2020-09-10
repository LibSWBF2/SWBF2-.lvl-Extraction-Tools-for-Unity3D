using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class TerrainLoader : ScriptableObject {

    public static void ImportTerrain(Level level) {

        LibSWBF2.Wrappers.Terrain terrain = level.GetTerrain();

        //Read heightmap
        float[] heightsRaw = terrain.Heights;
        int dim = terrain.width;
        
        TerrainData terData = new TerrainData();
        terData.heightmapResolution = terrain.width + 1;
        terData.size = new Vector3(terrain.width, 15, dim);
        terData.baseMapResolution = 1024;
        terData.SetDetailResolution(1024, 8);

        float[,] heights = new float[terrain.width,terrain.height];
        for (int x = 0; x < terrain.width; x++){
            for (int y = 0; y < terrain.height; y++){
                heights[x,y] = heightsRaw[x * terrain.width + y];
            }
        }

        terData.SetHeights(0, 0, heights);


        //Get list of textures used
        List<Texture2D> terTextures = new List<Texture2D>();
        int realNumLayers = 0;
        foreach (string texName in terrain.TextureNames)
        {
            Texture2D tex = TextureLoader.ImportTexture(level,texName);
            if (tex == null)
            {
                Debug.Log("Couldnt find texture: " + texName);
            }
            else 
            {
            	Debug.Log("adding texture " + texName);
                terTextures.Add(tex);  
                realNumLayers++;              
            }
        }

        byte[] splatMapRaw = terrain.GetBlendMap(out int blendDim, out int numLayers);  

        numLayers = realNumLayers;


        //Assign layers
        TerrainLayer[] terrainLayers = new TerrainLayer[numLayers];
        
        for (int i = 0; i < numLayers; i++)
        {
        	TerrainLayer newLayer = new TerrainLayer();
            newLayer.diffuseTexture = terTextures[i];
            terrainLayers[i] = newLayer;
        }
        terData.SetTerrainLayersRegisterUndo(terrainLayers,"Undo");


        //Read splatmap
        float[,,] splatMap = new float[blendDim, blendDim, numLayers];

        Debug.Log("Terrain data length " + blendDim + " with " + numLayers + " layers");

        for (int y = 0; y < blendDim; y++)
        {
            for (int x = 0; x < blendDim; x++)
            {
                int baseIndex = numLayers * (y * blendDim + x);
                for (int z = 0; z < numLayers; z++)
                {
                    splatMap[x,y,z] = ((float) splatMapRaw[baseIndex + z]) / 255.0f;    
                }
            }
        }
        terData.alphamapResolution = blendDim;
        terData.SetAlphamaps(0, 0, splatMap);
        terData.SetBaseMapDirty();


        //Save terrain/create gameobj
        GameObject terrainObj = UnityEngine.Terrain.CreateTerrainGameObject(terData);
        //PrefabUtility.SaveAsPrefabAsset(terrainObj, Application.dataPath + "/Terrain/terrain.prefab");
        //AssetDatabase.Refresh();

        Debug.Log("Terrain Imported!");
    }
}
