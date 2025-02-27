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
    float Threshold< Attribute("Threshold"); Default(0.0f); >;
    int CompositeMode< Attribute("CompositeMode"); Default(0); >;
	
    
    // Add these functions in the PS section before MainPs:
    float3 SampleBicubicLevel(Texture2D tex,float2 uv, float level)
    {
        SamplerState samp = g_sBilinearClamp;

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

    float3 ScreenHDR(float3 base, float3 blend)
    {
        // Prevent negative contributions by ensuring inputs are non-negative
        base = max(base, 0.0f);
        blend = max(blend, 0.0f);
        
        // HDR-aware Screen: scale the blend contribution and additively combine
        float3 screenTerm = 1.0f - (1.0f - saturate(base)) * (1.0f - saturate(blend));
        
        // For HDR values > 1, preserve excess energy additively
        float3 excessBase = max(base - 1.0f, 0.0f);
        float3 excessBlend = max(blend - 1.0f, 0.0f);
        
        return screenTerm + excessBase + excessBlend;
    }

    float4 MainPs(PixelInput input) : SV_Target0
    {
        float2 vScreenUv = (input.vPositionSs.xy + g_vViewportOffset) / g_vViewportSize;
        
        // Sample base color
        float4 vFinalColor = ColorBuffer.Sample(g_sBilinearMirror, vScreenUv.xy);
        
        // Initialize bloom color
        float4 vBloomColor = float4(0, 0, 0, 0);
        
        // Get texture dimensions and mip levels
        uint width, height, mipLevels;
        ColorBuffer.GetDimensions(0, width, height, mipLevels);
        
        // Bloom contribution parameters
        const float bloomIntensity = Strength;    // Overall bloom strength
        const float falloffScale = 1.0 - ( Threshold * 0.5 );      // Controls how quickly bloom fades across mips
        const float gammaCorrection = 2.2f;   // Gamma space for color accuracy
        
        [unroll]
        for (int i = 0; i < mipLevels - 1; i++)
        {
            // Sample each mip level with bicubic filtering
            float3 sample = SampleBicubicLevel(ColorBuffer, vScreenUv.xy, i + 1);
            
            // Convert to linear space and apply subtle curve to prevent over-saturation
            float3 bloomSample = max( pow(sample, gammaCorrection), 0 );
            
            // Calculate falloff based on mip level (larger mips = wider blur, less contribution)
            float mipWeight = (float)i / (float)mipLevels; // Exponential decay for smooth falloff
            mipWeight *= falloffScale;
            // Accumulate bloom with weight
            vBloomColor.rgb += bloomSample * mipWeight;
        }
        
        // Apply bloom intensity
        vBloomColor.rgb *= bloomIntensity;
        
        // High-quality compositing: additive bloom with soft clamping
        float3 finalColor = 0; 

        {
            if (CompositeMode == 0)
            {
                // Additive compositing (original)
                finalColor = vFinalColor.rgb + vBloomColor.rgb;
            }
            else if (CompositeMode == 1)
            {
                // Screen blending: result = 1 - ((1 - base) * (1 - bloom))
                finalColor = ScreenHDR(vFinalColor.rgb, vBloomColor.rgb);
            }
            else if (CompositeMode == 2)
            {
                // Blur blending: blend base and bloom based on bloom luminance
                float bloomLuminance = Luminance(vBloomColor.rgb);
                finalColor = lerp(vFinalColor.rgb, vBloomColor.rgb, saturate(bloomLuminance));
            }
        }
        
        // Ensure alpha is 1.0 for the final output
        return float4(finalColor, 1.0f);
    }

}
