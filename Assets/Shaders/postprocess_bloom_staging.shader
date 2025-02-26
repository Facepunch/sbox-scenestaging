HEADER
{
    Description = "Bloom post-process shader";
    DevShader = true;
}

MODES
{
    Default();
    Forward();
}

FEATURES
{
}

COMMON
{
    #include "postprocess/shared.hlsl"
}

struct VertexInput
{
    float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
    float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
    float2 vTexCoord : TEXCOORD0;

	// VS only
	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs		: SV_Position;
	#endif

	// PS only
	#if ( ( PROGRAM == VFX_PROGRAM_PS ) )
		float4 vPositionSs		: SV_Position;
	#endif
};

VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        
        o.vPositionPs = float4(i.vPositionOs.xy, 0.0f, 1.0f);
        o.vTexCoord = i.vTexCoord;
        return o;
    }
}

PS
{
    #include "postprocess/common.hlsl"
    #include "postprocess/functions.hlsl"
    #include "procedural.hlsl"

    RenderState( DepthWriteEnable, false );
    RenderState( DepthEnable, false );

    Texture2D ColorBuffer < Attribute( "ColorBuffer" ); >;
    
    float Strength< Attribute("Strength"); Default(0.0f); >;
	
    // Add these functions in the PS section before MainPs:
    float3 SampleTextureBicubic(Texture2D tex, SamplerState samp, float2 uv, float level)
    {
        float2 texSize;
        tex.GetDimensions(texSize.x, texSize.y);
        texSize /= pow(2, level);
        float2 invTexSize = 1.0 / texSize;
        
        // Calculate texture coordinates
        float2 texel = uv * texSize;
        float2 fraction = frac(texel);
        texel = floor(texel) * invTexSize;
        
        // Cubic weights
        float2 w0 = fraction * (-0.5 + fraction * (1.0 - 0.5 * fraction));
        float2 w1 = 1.0 + fraction * fraction * (-2.5 + 1.5 * fraction);
        float2 w2 = fraction * (0.5 + fraction * (2.0 - 1.5 * fraction));
        float2 w3 = fraction * fraction * (-0.5 + 0.5 * fraction);
        
        // Calculate texture coordinates for sampling
        float2 s0 = texel + (-1.0 * invTexSize);
        float2 s1 = texel +   0.0 * invTexSize;
        float2 s2 = texel +   1.0 * invTexSize;
        float2 s3 = texel +   2.0 * invTexSize;
        
        // Sample the texture at 16 points
        float3 row0 = 
            tex.SampleLevel(samp, float2(s0.x, s0.y), level).rgb * w0.x * w0.y +
            tex.SampleLevel(samp, float2(s1.x, s0.y), level).rgb * w1.x * w0.y +
            tex.SampleLevel(samp, float2(s2.x, s0.y), level).rgb * w2.x * w0.y +
            tex.SampleLevel(samp, float2(s3.x, s0.y), level).rgb * w3.x * w0.y;
            
        float3 row1 = 
            tex.SampleLevel(samp, float2(s0.x, s1.y), level).rgb * w0.x * w1.y +
            tex.SampleLevel(samp, float2(s1.x, s1.y), level).rgb * w1.x * w1.y +
            tex.SampleLevel(samp, float2(s2.x, s1.y), level).rgb * w2.x * w1.y +
            tex.SampleLevel(samp, float2(s3.x, s1.y), level).rgb * w3.x * w1.y;
            
        float3 row2 = 
            tex.SampleLevel(samp, float2(s0.x, s2.y), level).rgb * w0.x * w2.y +
            tex.SampleLevel(samp, float2(s1.x, s2.y), level).rgb * w1.x * w2.y +
            tex.SampleLevel(samp, float2(s2.x, s2.y), level).rgb * w2.x * w2.y +
            tex.SampleLevel(samp, float2(s3.x, s2.y), level).rgb * w3.x * w2.y;
            
        float3 row3 = 
            tex.SampleLevel(samp, float2(s0.x, s3.y), level).rgb * w0.x * w3.y +
            tex.SampleLevel(samp, float2(s1.x, s3.y), level).rgb * w1.x * w3.y +
            tex.SampleLevel(samp, float2(s2.x, s3.y), level).rgb * w2.x * w3.y +
            tex.SampleLevel(samp, float2(s3.x, s3.y), level).rgb * w3.x * w3.y;
            
        return row0 + row1 + row2 + row3;
    }

    float4 MainPs( PixelInput input ) : SV_Target0
    {
        float2 vScreenUv = input.vPositionSs.xy / g_vViewportSize;
        
        // Sample base color
        float4 vFinalColor = ColorBuffer.Sample( g_sBilinearMirror, vScreenUv.xy );
        
        // Use more mip levels for better bloom falloff
        float4 vBloomTaps[6] = 
        {
            float4(SampleTextureBicubic(ColorBuffer, g_sBilinearMirror, vScreenUv.xy, 2  ), 1.0), // Higher frequency detail
            float4(SampleTextureBicubic(ColorBuffer, g_sBilinearMirror, vScreenUv.xy, 3  ), 1.0),
            float4(SampleTextureBicubic(ColorBuffer, g_sBilinearMirror, vScreenUv.xy, 4  ), 1.0),
            float4(SampleTextureBicubic(ColorBuffer, g_sBilinearMirror, vScreenUv.xy, 5  ), 1.0),
            float4(SampleTextureBicubic(ColorBuffer, g_sBilinearMirror, vScreenUv.xy, 6  ), 1.0),
            float4(SampleTextureBicubic(ColorBuffer, g_sBilinearMirror, vScreenUv.xy, 7  ), 1.0)  // Very wide bloom
        };

    
        // Energy-conserving weights that sum to ~1.0
        // Each mip level covers 4x more area, so we adjust weights accordingly
        float bloomWeights[6] = 
        { 
            0.35f,  // Fine detail
            0.25f,  // Medium detail
            0.18f,  // Regular bloom
            0.12f,  // Wide bloom
            0.07f,  // Very wide bloom
            0.03f   // Ultra-wide bloom
        };
        
        // Combine bloom samples with careful color grading
        float4 vBloomColor = float4(0, 0, 0, 0);
        
        [unroll]
        for(int i = 0; i < 6; i++)
        {
            // Apply subtle curve to each bloom sample to prevent over-saturation
            float3 bloomSample = pow( vBloomTaps[i].rgb, 1.0 / 2.2 );
            bloomSample = max(0, bloomSample - 0.004); // Remove dark noise
            bloomSample *= bloomSample; // Quadratic falloff
            vBloomColor.rgb += bloomSample * bloomWeights[i];
        }
        
        // Softly clamp bloom intensity to prevent excessive values
        vBloomColor.rgb = 1.0 - exp(-vBloomColor.rgb * Strength);
        
        // Blend bloom additively with scene color
        vFinalColor.rgb += vBloomColor.rgb;
        
        // Ensure we don't lose energy in very bright areas
        vFinalColor.rgb = max(vFinalColor.rgb, vBloomColor.rgb * 0.5);
        
        return float4(vFinalColor.rgb, 1.0f);
    }
}
