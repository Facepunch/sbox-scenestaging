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

    RenderState( DepthWriteEnable, false );
    RenderState( DepthEnable, false );

    Texture2D ColorBuffer < Attribute( "ColorBuffer" ); >;
    
    float Strength< Attribute("Strength"); Default(0.0f); >;
    float Threshold< Attribute("Threshold"); Default(0.0f); >;
    float3 Tint< Attribute("Tint"); Default3(1.0f, 1.0f, 1.0f); >;
    int CompositeMode< Attribute("CompositeMode"); Default(0); >;
	
    
    // Add these functions in the PS section before MainPs:
    float3 SampleBicubicLevel(Texture2D tex,float2 uv, float level)
    {
        SamplerState samp = g_sBilinearClamp;

        float2 texSize;
        tex.GetDimensions(texSize.x, texSize.y);
        texSize /= pow(2, level);
        float2 invTexSize = 1.0 / texSize;

        // Todo: Actually bicubic? barely makes a difference in practice but this is 16x faster
        return tex.SampleLevel(samp, uv, level).rgb;
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
        const float bloomIntensity = Strength * 0.1f;    // Overall bloom strength
        const float falloffScale = Threshold + 0.1f;      // Controls how quickly bloom fades across mips
        const float gammaCorrection = 2.2f;   // Gamma space for color accuracy
        
        [unroll]
        for (int i = 0; i < mipLevels - 1; i++)
        {
            // Sample each mip level with bicubic filtering
            float3 sample = SampleBicubicLevel(ColorBuffer, vScreenUv.xy, i + 1);
            
            // Convert to linear space and apply subtle curve to prevent over-saturation
            float3 bloomSample = max( pow(sample, gammaCorrection), 0 );
            
            // Calculate falloff based on mip level (larger mips = wider blur, less contribution)
            float mipWeight = ( (float)i ) / (float)mipLevels; // Exponential decay for smooth falloff
            mipWeight = pow( mipWeight,  falloffScale * falloffScale ); // Apply falloff scale

            // Accumulate bloom with weight
            vBloomColor.rgb += bloomSample * mipWeight;
        }
        
        // Apply bloom intensity
        vBloomColor.rgb *= bloomIntensity;

        // Apply tint
        vBloomColor.rgb *= Tint;

        // High-quality compositing
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
