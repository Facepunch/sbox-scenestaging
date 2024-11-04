MODES
{
    Default();
}

CS
{
    #include "common.fxc"

    //--------------------------------------------------------------------------------------
    // Passes enumeration
    //--------------------------------------------------------------------------------------
    enum Passes
    {
        InitializeH0 = 0,
        UpdateHt = 1,
        FFTX = 2,
        FFTY = 3,
        ComputeDisplacementNormal = 4
    };

    //--------------------------------------------------------------------------------------
    // Constant buffer to pass parameters from CPU to GPU
    //--------------------------------------------------------------------------------------
    cbuffer OceanParameters
    {
        float4 windDirection;    // Wind direction vector (windDirection.xy)
        float   windSpeed;       // Wind speed scalar
        float   waveAmplitude;   // Overall amplitude of the waves
        float   patchSize;       // Size of the ocean patch
        float   time;            // Elapsed time
        float   choppyScale;     // Scale of wave choppiness
        uint    fftSize;         // Size of the FFT grid (e.g., 256)
        uint    numButterflies;  // Number of stages in the FFT (log2(fftSize))
        uint    D_PASS;          // Current pass identifier
        uint    fftStage;        // Current FFT stage
    };

    //--------------------------------------------------------------------------------------
    // Resource declarations
    //--------------------------------------------------------------------------------------

    // Textures for storing frequency domain data
    RWTexture2D<float2>     h0Texture        < Attribute( "h0Texture" ); >;        // Holds initial frequency domain heights h0(k)
    RWTexture2D<float2>     htTexture        < Attribute( "h1Texture" ); >;        // Holds time-updated frequency domain heights h(k, t)

    // Textures for FFT input and output
    Texture2D<float2>       fftInputTexture  < Attribute( "fftInputTexture" ); >;  // Input texture for FFT
    RWTexture2D<float2>     fftOutputTexture < Attribute( "fftOutputTexture" ); >; // Output texture for FFT

    // Textures for spatial domain data
    RWTexture2D<float>      heightTexture    < Attribute( "heightTexture" ); >;    // Holds height map in spatial domain
    RWTexture2D<float2>     displacementMap  < Attribute( "displacementMap" ); >;  // Holds displacement map
    RWTexture2D<float4>     normalMap        < Attribute( "normalMap" ); >;        // Holds normal map

    //--------------------------------------------------------------------------------------
    // Constants
    //--------------------------------------------------------------------------------------
    #define PI 3.14159265359f

    //--------------------------------------------------------------------------------------
    // Helper functions
    //--------------------------------------------------------------------------------------

    //
    // Compute the Phillips spectrum value
    //
    float ComputePhillipsSpectrum(float2 k, float2 windDir, float windSpd, float amplitude)
    {
        float k_length = length(k);
        if (k_length < 0.0001f)
            return 0.0f;

        float k_dot_w = dot(normalize(k), normalize(windDir));
        float k_dot_w2 = k_dot_w * k_dot_w;

        float L = windSpd * windSpd / 9.81f; // 9.81 is gravity
        float L2 = L * L;

        float k_length2 = k_length * k_length;
        float k_length4 = k_length2 * k_length2;

        float phillips = amplitude * exp(-1.0f / (k_length2 * L2)) / k_length4 * k_dot_w2;

        // Suppress waves going opposite to wind direction
        if (k_dot_w < 0.0f)
            phillips *= 0.0f;

        return phillips;
    }

    
    //
    // Generate a Gaussian random number based on thread ID
    //
    float2 GenerateGaussianRandom(uint2 seed)
    {
        // Simple hash function to generate pseudo-random numbers
        uint n = seed.x + seed.y * 57u;
        n = (n << 13u) ^ n;
        uint nn = n * (n * n * 15731u + 789221u) + 1376312589u;
        float rand1 = 1.0f - (float(nn & 0x7fffffffu) / 1073741824.0f);

        // Generate second random number
        n = n * 16807u;
        nn = n * (n * n * 15731u + 789221u) + 1376312589u;
        float rand2 = 1.0f - (float(nn & 0x7fffffffu) / 1073741824.0f);

        // Box-Muller transform to generate Gaussian random numbers
        float radius = sqrt(-2.0f * log(rand1 + 1e-6f)); // Add small value to avoid log(0)
        float theta = 2.0f * PI * rand2;

        return float2(radius * cos(theta), radius * sin(theta));
    }
    
    //--------------------------------------------------------------------------------------
    // Pass functions
    //--------------------------------------------------------------------------------------

    void InitializeH0Pass(uint2 dispatchThreadID, uint N)
    {
        // Calculate wave vector k
        float2 k = ((dispatchThreadID.xy - N / 2.0f) * (2.0f * PI / patchSize));

        // Compute the Phillips spectrum value
        float phillips = ComputePhillipsSpectrum(k, windDirection.xy, windSpeed, waveAmplitude);

        // Generate a random complex number with Gaussian distribution
        float2 gaussianRandom = GenerateGaussianRandom(dispatchThreadID.xy);

        // Compute initial height in frequency domain
        float2 h0 = sqrt(phillips / 2.0f) * gaussianRandom;

        // Store h0 in a texture for later use
        h0Texture[dispatchThreadID.xy] = h0;
    }

    void UpdateHtPass(uint2 dispatchThreadID, uint N)
    {
        // Retrieve wave vector k
        float2 k = ((dispatchThreadID.xy - N / 2.0f) * (2.0f * PI / patchSize));

        // Retrieve initial height h0(k)
        float2 h0 = h0Texture[dispatchThreadID.xy];

        // Compute angular frequency omega(k)
        float k_length = length(k);
        float omega = sqrt(9.81f * k_length);

        // Compute time-dependent height h(k, t)
        float cos_omega_t = cos(omega * time);
        float sin_omega_t = sin(omega * time);

        float2 ht;
        ht.x = h0.x * cos_omega_t - h0.y * sin_omega_t;
        ht.y = h0.y * cos_omega_t + h0.x * sin_omega_t;

        // Store ht in a texture for the FFT
        htTexture[dispatchThreadID.xy] = ht;
    }

    void FFTXPass(uint2 dispatchThreadID, uint N)
    {
        // FFT in X direction
        uint stage = fftStage;

        uint butterflySpan = 1 << stage;
        uint butterflyStep = butterflySpan << 1;

        uint id = dispatchThreadID.x;
        uint2 uv = dispatchThreadID.xy;

        uint butterflyOffset = id % butterflyStep;
        uint butterflyIndex = butterflyOffset % butterflySpan;

        int partnerOffset = (butterflyOffset < butterflySpan) ? int(butterflySpan) : -int(butterflySpan);
        int partnerID = int(id) + partnerOffset;

        // Ensure partnerID is within bounds
        if (partnerID < 0 || partnerID >= N)
            return;

        // Read inputs from previous stage
        float2 a = fftInputTexture[uv];
        float2 b = fftInputTexture[uint2(partnerID, uv.y)];

        // Compute twiddle factor
        float angle = -PI * float(butterflyIndex) / float(butterflySpan);
        float2 twiddle = float2(cos(angle), sin(angle));

        // Perform butterfly computation
        float2 t;
        t.x = b.x * twiddle.x - b.y * twiddle.y;
        t.y = b.x * twiddle.y + b.y * twiddle.x;

        // Store outputs for next stage
        fftOutputTexture[uv] = a + t;
        fftOutputTexture[uint2(partnerID, uv.y)] = a - t;
    }

    void FFTYPass(uint2 dispatchThreadID, uint N)
    {
        // FFT in Y direction
        uint stage = fftStage;

        uint butterflySpan = 1 << stage;
        uint butterflyStep = butterflySpan << 1;

        uint id = dispatchThreadID.y;
        uint2 uv = dispatchThreadID.xy;

        uint butterflyOffset = id % butterflyStep;
        uint butterflyIndex = butterflyOffset % butterflySpan;

        int partnerOffset = (butterflyOffset < butterflySpan) ? int(butterflySpan) : -int(butterflySpan);
        int partnerID = int(id) + partnerOffset;

        // Ensure partnerID is within bounds
        if (partnerID < 0 || partnerID >= N)
            return;

        // Read inputs from previous stage
        float2 a = fftInputTexture[uv];
        float2 b = fftInputTexture[uint2(uv.x, partnerID)];

        // Compute twiddle factor
        float angle = -PI * float(butterflyIndex) / float(butterflySpan);
        float2 twiddle = float2(cos(angle), sin(angle));

        // Perform butterfly computation
        float2 t;
        t.x = b.x * twiddle.x - b.y * twiddle.y;
        t.y = b.x * twiddle.y + b.y * twiddle.x;

        // Store outputs for next stage
        fftOutputTexture[uv] = a + t;
        fftOutputTexture[uint2(uv.x, partnerID)] = a - t;
    }

    void ComputeDisplacementNormalPass(uint2 dispatchThreadID, uint N)
    {
        // Retrieve height field h(x)
        float height = heightTexture[dispatchThreadID.xy];

        // Compute indices with wrapping for edge cases
        uint xPlus1 = (dispatchThreadID.x + 1) % N;
        uint yPlus1 = (dispatchThreadID.y + 1) % N;

        // Compute partial derivatives for normals
        float height_x1 = heightTexture[uint2(xPlus1, dispatchThreadID.y)];
        float height_y1 = heightTexture[uint2(dispatchThreadID.x, yPlus1)];

        float dx = height_x1 - height;
        float dy = height_y1 - height;

        // Compute normal vector
        float3 normal = normalize(float3(-dx, -dy, 1.0f));

        // Compute displacement for choppy waves
        float2 displacement = choppyScale * float2(dx, dy);

        // Store results
        normalMap[dispatchThreadID.xy] = float4(normal, 1.0f);
        displacementMap[dispatchThreadID.xy] = displacement;
    }

    //--------------------------------------------------------------------------------------
    // Main compute shader function
    //--------------------------------------------------------------------------------------
    [numthreads(8, 8, 1)]
    void MainCs(uint2 dispatchThreadID : SV_DispatchThreadID, uint2 groupThreadID : SV_GroupThreadID)
    {
        uint N = fftSize;

        if (dispatchThreadID.x >= N || dispatchThreadID.y >= N)
            return;

        if (D_PASS == Passes::InitializeH0)
        {
            InitializeH0Pass(dispatchThreadID, N);
        }
        else if (D_PASS == Passes::UpdateHt)
        {
            UpdateHtPass(dispatchThreadID, N);
        }
        else if (D_PASS == Passes::FFTX)
        {
            FFTXPass(dispatchThreadID, N);
        }
        else if (D_PASS == Passes::FFTY)
        {
            FFTYPass(dispatchThreadID, N);
        }
        else if (D_PASS == Passes::ComputeDisplacementNormal)
        {
            ComputeDisplacementNormalPass(dispatchThreadID, N);
        }
    }

}