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

        foreach (LibSWBF2.Wrappers.Terrain terrain in level.GetTerrains() )
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
	                float h = heightsRaw[x * dim + y];
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
	            Texture2D tex = TextureLoader.ImportTexture(level,texName);
	            if (tex == null)
	            {
	                //Debug.Log("Couldnt find texture: " + texName);
	            }
	            else 
	            {
	            	//Debug.Log("adding texture " + texName);
	                terTextures.Add(tex);  
	            }
	        }

	        terrain.GetBlendMap(out uint blendDim, out uint numLayers, out byte[] blendMapRaw);  


	        //Assign layers
	        TerrainLayer[] terrainLayers = new TerrainLayer[numLayers];
	        
	        for (int i = 0; i < numLayers; i++)
	        {
	        	TerrainLayer newLayer = new TerrainLayer();
	            newLayer.diffuseTexture = terTextures[i];
	            newLayer.tileSize = new Vector2(32,32);
	            terrainLayers[i] = newLayer;
	        }

	        terData.SetTerrainLayersRegisterUndo(terrainLayers,"Undo");


	        //Read splatmap
	        float[,,] blendMap = new float[blendDim, blendDim, numLayers];

	        Debug.Log("Terrain data length " + blendDim + " with " + numLayers + " layers");

	        for (int y = 0; y < blendDim; y++)
	        {
	            for (int x = 0; x < blendDim; x++)
	            {
	                int baseIndex = (int) (numLayers * (y * blendDim + x));
	                for (int z = 0; z < numLayers; z++)
	                {
	                    blendMap[y,x,z] = ((float) blendMapRaw[baseIndex + z]) / 255.0f;    
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

	        Debug.Log("Terrain Imported!");
    	}
    }
}
