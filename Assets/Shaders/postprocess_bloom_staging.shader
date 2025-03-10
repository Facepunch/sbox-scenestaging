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
    float Gamma< Attribute("Gamma"); Default(2.2f); >;
    float3 Tint< Attribute("Tint"); Default3(1.0f, 1.0f, 1.0f); >;
    int CompositeMode< Attribute("CompositeMode"); Default(0); >;
        
    float3 SampleBiquadraticLevel(Texture2D tex, float2 uv, int level)
    {
        SamplerState samp = g_sBilinearClamp;
        
        // Get accurate dimensions for the specified mip level
        float2 texSize;
        tex.GetDimensions(texSize.x, texSize.y);
        texSize /= pow(2, level);
        float2 invTexSize = 1.0 / texSize;
        
        // Calculate texel position and fractional offset
        float2 texelPos = uv * texSize;
        float2 fracOffset = frac(texelPos);
        float2 baseTexel = floor(texelPos);
        
        // Pre-calculate offsets for our 4 samples
        // These sample positions are chosen to approximate a biquadratic filter
        const float2 offset1 = float2(-1.0, -1.0) / 3.0;
        const float2 offset2 = float2( 1.0, -1.0) / 3.0;
        const float2 offset3 = float2(-1.0,  1.0) / 3.0;
        const float2 offset4 = float2( 1.0,  1.0) / 3.0;
        
        // Calculate sample positions
        float2 samplePos1 = (baseTexel + 0.5 + offset1 + fracOffset) * invTexSize;
        float2 samplePos2 = (baseTexel + 0.5 + offset2 + fracOffset) * invTexSize;
        float2 samplePos3 = (baseTexel + 0.5 + offset3 + fracOffset) * invTexSize;
        float2 samplePos4 = (baseTexel + 0.5 + offset4 + fracOffset) * invTexSize;
        
        // Sample texture with weights
        // The hardware bilinear filtering already gives us 4 texels per sample
        float3 color = 0;
        color += tex.SampleLevel(samp, samplePos1, level).rgb * 0.25;
        color += tex.SampleLevel(samp, samplePos2, level).rgb * 0.25;
        color += tex.SampleLevel(samp, samplePos3, level).rgb * 0.25;
        color += tex.SampleLevel(samp, samplePos4, level).rgb * 0.25;
        return color;
        
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
        const float bloomIntensity = pow(Strength * 0.1, 2);    // Overall bloom strength
        const float falloffScale = Threshold - 2;      // Controls how quickly bloom fades across mips
        const float gammaCorrection = Gamma;   // Gamma space for color accuracy
        
        [unroll]
        for (int i = 0; i < mipLevels - 1; i++)
        {
            // Sample each mip level with biquadratic filtering
            float3 sample = SampleBiquadraticLevel(ColorBuffer, vScreenUv.xy, i + 1);
            
            // Convert to linear space and apply subtle curve to prevent over-saturation
            float3 bloomSample = max( pow(sample, gammaCorrection), 0 );
            
            // Calculate falloff based on mip level (larger mips = wider blur, less contribution)
            float mipWeight = exp2(-falloffScale * i);

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
