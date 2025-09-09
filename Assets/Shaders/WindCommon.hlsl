#ifndef WINDCOMMON_HLSL
#define WINDCOMMON_HLSL

#include "common/Bindless.hlsl"

float4 WindStrengthFreqMulHighStrength < Attribute( "WindStrengthFreq" ); Default4( 0.25, 0.03, 0.003, 0.25 ); >;
float3 WindDir < Attribute( "WindDir" ); Default3( 0.707, 0.707, 0.0 ); >;
float WindGustSpeed < Attribute( "WindGustSpeed" ); Default1( 0.4 ); >;
float WindGustStrength < Attribute( "WindGustStrength" ); Default1( 0.2 ); >;
float WindVariation < Attribute( "WindVariation" ); Default1( 0.5 ); >;
#define WindTime g_flTime

static const float WIND_MICRO_FREQUENCY = 2.0;
static const float WIND_MICRO_STRENGTH = 0.05;
static const float WIND_TURBULENCE = 1.0;

class Wind
{
    static float SampleNoise(float3 worldPos, float3 windDir, float frequency)
    {
        // Sample noise function (Perlin or Simplex) for wind effect
        // This is a placeholder; replace with actual noise sampling code
        return sin(dot(worldPos, windDir) * frequency + WindTime) * 0.5 + 0.5;
    }

    // Main wind displacement function - applies primary, secondary and micro-detail wind effects
    static float3 GetWindDisplacement(float3 worldPos, float windStrengthMultiplier, float stiffness)
    {
        // Extract wind parameters
        float primaryStrength = WindStrengthFreqMulHighStrength.x * windStrengthMultiplier;
        float primaryFreq = WindStrengthFreqMulHighStrength.y;
        float secondaryStrength = WindStrengthFreqMulHighStrength.z * windStrengthMultiplier;
        float highFreqStrength = WindStrengthFreqMulHighStrength.w * windStrengthMultiplier;
        
        // Apply stiffness scaling to reduce wind effect
        primaryStrength *= (1.0 - stiffness * 0.8);
        secondaryStrength *= (1.0 - stiffness * 0.8);
        highFreqStrength *= (1.0 - stiffness * 0.9);
        
        // Calculate normalized wind direction
        float3 windDirection = normalize(WindDir);
        
        // Primary wind: Large directional gusts
        float3 primaryWind = GetPrimaryWind(worldPos, windDirection, primaryStrength, primaryFreq);
        
        // Secondary wind: Medium-scale movements
        float3 secondaryWind = GetSecondaryWind(worldPos, secondaryStrength);
        
        // High-frequency wind: Small jittering and micro-movements
        float3 microWind = GetMicroWind(worldPos, highFreqStrength);
        
        // Combine all wind effects
        return primaryWind + secondaryWind + microWind;
    }
    
    // Primary wind calculation - main directional gusts
    static float3 GetPrimaryWind(float3 worldPos, float3 windDir, float strength, float frequency)
    {
        // Calculate base wind time with variation for more natural look
        float windTime = WindTime * WindGustSpeed;
        
        // Project position onto wind plane
        float2 posInWindDir = float2(dot(worldPos.xz, windDir.xz), worldPos.y);
        
        // Calculate wind phase with spatial variation
        float phase = posInWindDir.x * frequency + windTime;
        
        // Use Perlin noise for natural-feeling wind gusts
        float noise = SampleNoise(worldPos * 0.01, windDir, WIND_TURBULENCE);
        
        // Wind strength with variation over space
        float spatialVariation = SampleNoise(worldPos * 0.005, windDir, 0.2) * WindVariation;
        
        // Calculate gust using multiple sine waves for more natural movement
        float gust = sin(phase) * 0.5 + 0.5;
        gust += sin(phase * 1.5 + 0.5) * 0.25;
        gust += sin(phase * 0.8 - 0.3) * 0.25;
        gust *= 0.5;  // Normalize
        
        // Add sudden gusts
        gust += pow(noise * 0.5 + 0.5, 2.0) * WindGustStrength;
        
        // Apply spatial variation
        gust *= (1.0 + spatialVariation);
        
        // Calculate final displacement
        float3 displacement = windDir * gust * strength;
        
        // Add slight vertical component for more natural wind
        displacement.y = length( displacement ) * 0.2 * sin(phase * 1.2);
        
        return displacement;
    }
    
    // Secondary wind - medium frequency movements
    static float3 GetSecondaryWind(float3 worldPos, float strength)
    {
        // Use different frequencies for varied movement
        float3 windOffsets = WindTime * float3(0.35, 0.3, 0.4);
        
        // Calculate noise at different frequencies and offsets
        float noise1 = SampleNoise(worldPos * 0.06 + windOffsets.xxx, normalize(float3(1, 0, 1)), 0.4);
        float noise2 = SampleNoise(worldPos * 0.07 + windOffsets.yyy, normalize(float3(-1, 0, 1)), 0.4);
        float noise3 = SampleNoise(worldPos * 0.08 + windOffsets.zzz, normalize(float3(1, 0, -1)), 0.4);
        
        // Combine noises for a more complex movement
        float3 noiseVec = float3(noise1, noise2, noise3) * 2.0 - 1.0;
        
        return noiseVec * strength;
    }
    
    // Micro-wind - high frequency small details
    static float3 GetMicroWind(float3 worldPos, float strength)
    {
        // Fast, small movements
        float3 windOffsets = WindTime * float3(1.5, 1.7, 1.3) * WIND_MICRO_FREQUENCY;
        
        // High-frequency noise for small details
        float noise1 = SampleNoise(worldPos * 0.25 + windOffsets.xxx, float3(1, 0.5, 0), 0.7);
        float noise2 = SampleNoise(worldPos * 0.30 + windOffsets.yyy, float3(0, 0.5, 1), 0.7);
        float noise3 = SampleNoise(worldPos * 0.35 + windOffsets.zzz, float3(1, 0, 1), 0.7);
        
        // Create small rapid movements
        float3 noiseVec = float3(noise1, noise2, noise3) * 2.0 - 1.0;
        
        // Apply micro wind strength
        return noiseVec * strength * WIND_MICRO_STRENGTH;
    }
    
    // Helper function to get wind intensity at a position (useful for audio/visual effects)
    static float GetWindIntensity(float3 worldPos)
    {
        float primaryStrength = WindStrengthFreqMulHighStrength.x;
        float3 displacement = GetWindDisplacement(worldPos, 1.0, 0.0);
        return length(displacement) / primaryStrength;
    }
};

#endif