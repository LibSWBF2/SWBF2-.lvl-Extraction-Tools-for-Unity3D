#ifndef BLENDTERRAINLAYERS_INCLUDED
#define BLENDTERRAINLAYERS_INCLUDED

/*
* Texture2DArray can only store textures of same size.
* But the layer textures differ in size, so there will be "smaller" textures inside the array,
* where the remaining pixels are filled with black (see TextureLoader.ImportTextures  <--  note the "S" at the end).
* 
* One workaround is to scale the UV of the affected
* 
* Because Unity does neither support passing float arrays, nor matrix4x4's (possible workaround for a float array length of 16) as parameters,
* we have to use the smallest available float4 parameter multiple times...
*/

void BlendTerrainLayers_float(
    UnitySamplerState ss,
    UnityTexture2DArray layers,
    float numLayers,
    float4 layerTexDims0,
    float4 layerTexDims1,
    float4 layerTexDims2,
    float4 layerTexDims3,
    UnityTexture2D blend0,
    UnityTexture2D blend1,
    UnityTexture2D blend2,
    UnityTexture2D blend3,
    float bound,
    float3 worldPos,
    out float3 output)
{
    float2 modulo = float2(32.0, 32.0);

    float2 gridLoc = abs(worldPos.xz);
    float2 uvTex = (gridLoc % modulo) / modulo;
    float2 bounds = float2(bound / 2.0, bound / 2.0);

    output = float3(0.0, 0.0, 0.0);
    int n = (int)numLayers;

    float4x4 layerTexDims;
    layerTexDims[0] = layerTexDims0;
    layerTexDims[1] = layerTexDims1;
    layerTexDims[2] = layerTexDims2;
    layerTexDims[3] = layerTexDims3;

    // crashes
    //UnityTexture2D blends[4];
    //blends[0] = blend0;
    //blends[1] = blend1;
    //blends[2] = blend2;
    //blends[3] = blend3;

    for (int i = 0; i < n; ++i)
    {
        float uvScale = layerTexDims[i / 4][i % 4];

        float3 layerCol = layerCol = SAMPLE_TEXTURE2D_ARRAY(layers, ss, uvTex * uvScale, i).rgb;

        //int blendIdx = i/ 4;
        float4 blend = float4(0, 0, 0, 0);
        
        if (i < 4)
        {
            blend = SAMPLE_TEXTURE2D(blend0, ss, (worldPos.xz + bounds) / (2.0 * bounds));
        }
        else if (i < 8)
        {
            blend = SAMPLE_TEXTURE2D(blend1, ss, (worldPos.xz + bounds) / (2.0 * bounds));
        }
        else if (i < 12)
        {
            blend = SAMPLE_TEXTURE2D(blend2, ss, (worldPos.xz + bounds) / (2.0 * bounds));
        }
        else if (i < 16)
        {
            blend = SAMPLE_TEXTURE2D(blend3, ss, (worldPos.xz + bounds) / (2.0 * bounds));
        }

        //float lol = (worldPos.xz + bounds) / (2.0 * bounds) * 0.1;
        //output = float3(lol, lol, lol);
        output += layerCol * blend[i % 4];
        //output = layerTexDims[1][0] / layerTexDimMax;
    }
}

#endif // BLENDTERRAINLAYERS_INCLUDED