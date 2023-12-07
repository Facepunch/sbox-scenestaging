//
// Lots of hard coded constants and random functions that make up Rust's terrain.
//

// packed as ( mult, start, dist );
// this stuff is meters, so make sure to do * 39
static const float3 layerUVMIX[8] = {
    float3( 0.33, 5, 100 ),
    float3( 0.05, 15, 50 ),
    float3( 0.1, 5, 50 ),
    float3( 0.125, 10, 50 ),
    float3( 0.1, 10, 150 ),
    float3( 0.2, 5, 150 ),
    float3( 0.75, 5, 200 ),
    float3( 0.25, 5, 75 )
};

static const float layerFactor[8] = { 16, 4.5, 8, 4.5, 4.5, 4.5, 4.5, 4.5 };
static const float layerFalloff[8] = { 16, 16, 4, 16, 16, 16, 16, 16 };
static const float layerUV[8] = { 1.0 / 2048.0, 1.0 / 2048.0, 1.0 / 2048.0, 1.0 / 2048.0, 1.0 / 2048.0, 1.0 / 2048.0, 1.0 / 2048.0, 1.0 / 2048.0 };

static const float4 layerAridColor[8] = 
{
    float4( 0.8, 0.7775281, 0.71910113, 1 ), // Dirt
    float4( 0.8742, 0.90356845, 0.93, 1 ),
    float4( 0.70980394, 0.6536111, 0.5749412, 1 ),
    float4( 0.85, 0.7567085, 0.61455, 1 ),
    float4( 0.74, 0.728377, 0.5850262, 1 ),
    float4( 0.8, 0.7267974, 0.5803922, 1 ),
    float4( 0.8509804, 0.7333465, 0.57951766, 1 ),
    float4( 0.85, 0.7416667, 0.63750005, 1 ),
};

static const float4 layerTemperateColor[8] = 
{
    float4( 0.7, 0.6845133, 0.6597345, 1 ),
    float4( 0.8742, 0.90356845, 0.93, 1 ),
    float4( 0.65882355, 0.64828235, 0.60611767, 1 ),
    float4( 0.6509804, 0.61418587, 0.56635296, 1 ),
    float4( 0.47843137, 0.5803922, 0.3764706, 1 ),
    float4( 0.68, 0.68, 0.5448193, 1 ),
    float4( 0.72, 0.6829715, 0.60480005, 1 ),
    float4( 0.64, 0.61908495, 0.5939869, 1 ),
};

static const float4 layerTundraColor[8] = 
{
    float4( 0.773, 0.739761, 0.739761, 1 ),
    float4( 0.8742, 0.90356845, 0.93, 1 ),
    float4( 0.5, 0.49019608, 0.4509804, 1 ),
    float4( 0.65, 0.6047468, 0.5224683, 1 ),
    float4( 0.62, 0.5783009, 0.372, 1 ),
    float4( 0.6, 0.5228571, 0.36, 1 ),
    float4( 0.7, 0.6488764, 0.5623596, 1 ),
    float4( 0.6, 0.5746988, 0.53855425, 1 ),
};

static const float4 layerArcticColor[8] = 
{
    float4( 0.70409805, 0.73915684, 0.745, 1 ),
    float4( 0.8742, 0.90356845, 0.93, 1 ),
    float4( 0.530689, 0.57733774, 0.589, 1 ),
    float4( 0.6365, 0.661625, 0.67, 1 ),
    float4( 0.7588235, 0.8051961, 0.84313726, 1 ),
    float4( 0.851, 0.84657484, 0.784622, 1 ),
    float4( 0.558, 0.58425003, 0.6, 1 ),
    float4( 0.65, 0.65, 0.65, 1 ),
};