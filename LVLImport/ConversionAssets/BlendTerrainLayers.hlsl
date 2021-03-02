#ifndef BLENDTERRAINLAYERS_INCLUDED
#define BLENDTERRAINLAYERS_INCLUDED

void BlendTerrainLayers_float(
    UnityTexture2DArray layers,
    UnityTexture2DArray blends,
    UnitySamplerState ss,
    float numLayers,
    float2 uv,
    float3 worldPos,
    out float3 output)
{
    float2 modulo = float2(32.0, 32.0);
    float xbound = 1024.0;
    float zbound = 1024.0;

    float2 gridLoc = worldPos.xz;
    float2 uvTex = (gridLoc % modulo) / modulo;

    output = float3(0.0, 0.0, 0.0);
    int n = (int)numLayers;

    for (int i = 0; i < n; ++i)
    {
        float3 layerCol = SAMPLE_TEXTURE2D_ARRAY(layers, ss, uvTex, i).rgb;

        float2 bounds = float2(xbound / 2.0, zbound / 2.0);
        float4 blend = SAMPLE_TEXTURE2D_ARRAY(blends, ss, (worldPos.xz + bounds) / (2.0 * bounds), i / 4);

        output += layerCol * blend[i % 4];
    }
}

#endif // BLENDTERRAINLAYERS_INCLUDED