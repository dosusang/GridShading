#ifndef POINT_LIGHT_INCLUDED
#define POINT_LIGHT_INCLUDED

#define MAX_TO_POINTS 256
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#ifndef SHADER_API_GLES3
CBUFFER_START(TOPointLights)
#endif
half _IfEnableVoxelPointLights;
half4 _TOPointPosRange[MAX_TO_POINTS];
half4 _TOPointColor[MAX_TO_POINTS];
half4 _TPVoxelCenter;
half4 _TPVoxelSize;
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif

TEXTURE2D_FLOAT(_VoxelIdxMap);  SAMPLER(sampler_VoxelIdxMap);

half TOPointDistanceAttenuation(half distanceSqr, half2 distanceAttenuation)
{
    half lightAtten = rcp(distanceSqr);
    half factor = distanceSqr * distanceAttenuation.x;
    half smoothFactor = saturate(1.0h - factor * factor);
    smoothFactor = smoothFactor * smoothFactor;
    return lightAtten * smoothFactor;
}

Light GetTOPointLight(uint idx, half3 positionWS)
{
    Light l;
    l.color = _TOPointColor[idx].xyz;
    half3 vec = _TOPointPosRange[idx].xyz - positionWS;
    l.direction = normalize(vec);

    half distSqr = max(dot(vec, vec), 0.01);
    half2 attenuation = half2(_TOPointPosRange[idx].w, _TOPointColor[idx].w);

    half atten = TOPointDistanceAttenuation(distSqr, attenuation.xy);
    
    l.distanceAttenuation = atten;
    l.shadowAttenuation = 1;
    return l;
}

uint GetCount(half4 sample)
{
    uint x = sample.a * 256;
    return clamp(0, 7, x);
}

uint MyRound(half x)
{
    return (x + 0.5);
}
            
void ShadingTOPointLight(inout half3 color, BRDFData brdfData, InputData input)
{
    if(_IfEnableVoxelPointLights < 0.1) return;
    // return;
    int yIdx = (input.positionWS.y - _TPVoxelCenter.y) / (_TPVoxelSize.y * 0.25h) + 2;
    yIdx = clamp(0, 3, yIdx);
    // color.r = yIdx / 4.0;
    // return;
    
    half2 uv =  ((input.positionWS.xz - _TPVoxelCenter.xz)) / (_TPVoxelSize.xz) + 0.5h;
    uv = uv * 0.5h;
    uv += half2(0.5h, 0.5h) * half2(yIdx / 2, yIdx % 2);
    half4 idxMapSample = SAMPLE_TEXTURE2D(_VoxelIdxMap, sampler_VoxelIdxMap, uv);
    uint count = GetCount(idxMapSample);
    // color.rgb = input.normalWS;
    // color.rgb = idxMapSample;
    // return;
    //
    // color.rgb = half3(uv, 0);
    // return;
    
    // color.rgb = (half)count / 8;
    // return;

    uint idx[7];
    idx[0] = MyRound(idxMapSample.r * 65536.0f) & 0x00FF;
    idx[1] = idxMapSample.r * 256.0f;
    idx[2] = MyRound(idxMapSample.g * 65536.0f) & 0x00FF;
    idx[3] = idxMapSample.g * 256.0f;
    idx[4] = MyRound(idxMapSample.b * 65536.0f) & 0x00FF;
    idx[5] = idxMapSample.b * 256.0f;
    idx[6] = MyRound(idxMapSample.a * 65536.0f) & 0x00FF;
    for(uint i = 0; i < count; i++)
    {
        Light l = GetTOPointLight(idx[i], input.positionWS);
        color.rgb += LightingPhysicallyBased(brdfData, l, input.normalWS, input.viewDirectionWS);
    }
}


#endif

