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
        InitializeH0,
        UpdateHt,
        FFTX,
        FFTY,
        ComputeDisplacementNormal
    };
    
    DynamicCombo(D_PASS, 0..4, Sys(All));

    //--------------------------------------------------------------------------------------
    // Constant buffer to pass parameters from CPU to GPU
    //--------------------------------------------------------------------------------------
    cbuffer OceanParameters
    {
        float4 WindDirection;    // Wind direction vector (WindDirection.xy)
        float   WindSpeed;       // Wind speed scalar
        float   WaveAmplitude;   // Overall amplitude of the waves
        float   PatchSize;       // Size of the ocean patch
        float   ChoppyScale;     // Scale of wave choppiness
        uint    FFTSize;         // Size of the FFT grid (e.g., 256)
        uint    NumButterflies;  // Number of stages in the FFT (log2(FFTSize))
    };

    float   Time < Attribute( "Time" ); >;

    //--------------------------------------------------------------------------------------
    // Resource declarations
    //--------------------------------------------------------------------------------------

    // Textures for storing frequency domain data
    RWTexture2D<float2>     h0Texture        < Attribute( "H0Texture" ); >;        // Holds initial frequency domain heights h0(k)
    RWTexture2D<float2>     htTexture        < Attribute( "HtTexture" ); >;        // Holds time-updated frequency domain heights h(k, t)

    // Textures for FFT input and output
    RWTexture2D<float2>     FFTIntermediary < Attribute( "FFTIntermediary" ); >; // Output texture for FFT

    // Textures for spatial domain data
    RWTexture2D<float>      heightTexture    < Attribute( "HeightTexture" ); >;    // Holds height map in spatial domain
    RWTexture2D<float2>     displacementMap  < Attribute( "DisplacementMap" ); >;  // Holds displacement map
    RWTexture2D<float4>     normalMap        < Attribute( "NormalMap" ); >;        // Holds normal map

    //--------------------------------------------------------------------------------------
    // Constants
    //--------------------------------------------------------------------------------------
    #define PI 3.14159265359f
    #define THREAD_GROUP_SIZE_X 256

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
        //if (k_dot_w < 0.0f)
       //     phillips *= 0.0f;

        return phillips;
    }
    
    //
    // Generate a Gaussian random number based on thread ID
    //
    float2 GenerateGaussianRandom(uint2 seed)
    {
        uint n = seed.x + seed.y * 57u;
        n = (n << 13u) ^ n;
        uint nn = n * (n * n * 15731u + 789221u) + 1376312589u;
        float rand1 = 1.0f - (float(nn & 0x7fffffffu) / 1073741824.0f);

        // Generate second random number
        n = n * 16807u;
        nn = n * (n * n * 15731u + 789221u) + 1376312589u;
        float rand2 = 1.0f - (float(nn & 0x7fffffffu) / 1073741824.0f);

        // Box-Muller transform to generate Gaussian random numbers
        float radius = sqrt(-2.0f * log(max(rand1, 1e-6f))); // Use max to avoid log(0)
        float theta = 2.0f * PI * rand2;

        return float2(radius * cos(theta), radius * sin(theta));
    }

    //
    // Reverse bits for FFT
    //
    uint ReverseBits(uint x, uint n)
    {
        uint result = 0;
        for (uint i = 0; i < n; ++i)
        {
            uint bit = (x >> i) & 1;
            result |= bit << (n - i - 1);
        }
        return result;
    }

    //
    // Complex exponential function
    //
    float2 ComplexExp(float theta)
    {
        return float2(cos(theta), sin(theta));
    }
    
    //--------------------------------------------------------------------------------------
    // Complex number operations
    //--------------------------------------------------------------------------------------
    float2 ComplexMul(float2 a, float2 b)
    {
        return float2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
    }

    //--------------------------------------------------------------------------------------
    // Pass functions
    //--------------------------------------------------------------------------------------

    void InitializeH0Pass(uint2 dispatchThreadID)
    {
        uint N = FFTSize;
        // Calculate wave vector k
        float2 k = ((dispatchThreadID.xy - N / 2.0f) * (2.0f * PI / PatchSize));

        // Compute the Phillips spectrum value
        float phillips = ComputePhillipsSpectrum(k, WindDirection.xy, WindSpeed, WaveAmplitude);

        // Generate a random complex number with Gaussian distribution
        float2 gaussianRandom = GenerateGaussianRandom(dispatchThreadID.xy);

        // Compute initial height in frequency domain
        float2 h0 = sqrt(phillips / 2.0f) * gaussianRandom;

        // Store h0 in a texture for later use
        h0Texture[dispatchThreadID.xy] = h0;
    }

    void UpdateHtPass(uint2 dispatchThreadID)
    {
        uint N = FFTSize;
        // Retrieve wave vector k
        float2 k = ((dispatchThreadID.xy - N / 2.0f) * (2.0f * PI / PatchSize));

        // Retrieve initial height h0(k)
        float2 h0 = h0Texture[dispatchThreadID.xy];
        float2 h0_conj = float2(h0.x, -h0.y);

        // Compute angular frequency omega(k)
        float k_length = length(k);
        float omega = sqrt(9.81f * k_length);

        // Compute time-dependent height h(k, t)
        float cos_omega_t = cos(omega * Time);
        float sin_omega_t = sin(omega * Time);

        float2 ht = h0 * float2(cos_omega_t, sin_omega_t) + h0_conj * float2(cos_omega_t, -sin_omega_t);

        // Store ht in a texture for the FFT
        htTexture[dispatchThreadID.xy] = ht;
    }

    //--------------------------------------------------------------------------------------
    // FFT Passes: Performs FFT along the X-axis and Y-axis
    //--------------------------------------------------------------------------------------

    groupshared float2 sharedData[THREAD_GROUP_SIZE_X]; // Adjust size based on maximum thread group size

    void FFTPass(uint2 DTid : SV_DispatchThreadID, uint2 GTid : SV_GroupThreadID)
    {
        uint N = FFTSize;
        int dir = (D_PASS == Passes::FFTX) ? 1 : -1;

        if (GTid.x >= N)
            return;

        // Load data into shared memory with bit reversal
        uint index = GTid.x;
        uint reversedIndex = ReverseBits(index, NumButterflies);
        float2 value;

        if (D_PASS == Passes::FFTX) // Horizontal pass
        {
            value = htTexture.Load(int3(reversedIndex, DTid.y, 0));
        }
        else // Vertical pass
        {
            value = FFTIntermediary.Load(int3(DTid.x, reversedIndex, 0));
        }

        sharedData[GTid.x] = value;

        GroupMemoryBarrierWithGroupSync();

        // FFT computation using butterfly operations
        for (uint s = 1; s <= NumButterflies; s++)
        {
            uint m = 1 << s;
            uint m2 = m >> 1;
            float theta = dir * PI / m2;

            for (uint k = GTid.x; k < N; k += THREAD_GROUP_SIZE_X)
            {
                uint i = (k % m);
                if (i < m2)
                {
                    uint index1 = k;
                    uint index2 = k + m2;

                    float2 t = ComplexMul(sharedData[index2], ComplexExp(-theta * i));
                    float2 u = sharedData[index1];

                    sharedData[index1] = u + t;
                    sharedData[index2] = u - t;
                }
            }
            GroupMemoryBarrierWithGroupSync();
        }

        // Write back results
        if (D_PASS == Passes::FFTX) // Horizontal pass
        {
            FFTIntermediary[uint2(DTid.x, DTid.y)] = sharedData[GTid.x];
        }
        else // Vertical pass
        {
            htTexture[uint2(DTid.x, DTid.y)] = sharedData[GTid.x];
        }
    }


    //--------------------------------------------------------------------------------------
    // Main compute shader function
    //--------------------------------------------------------------------------------------
    [numthreads(THREAD_GROUP_SIZE_X, 1, 1)]
    void MainCs(uint2 DTid : SV_DispatchThreadID, uint2 GTid : SV_GroupThreadID)
    {
        if (D_PASS == Passes::InitializeH0)
        {
            InitializeH0Pass(DTid);
        }
        else if (D_PASS == Passes::UpdateHt)
        {
            UpdateHtPass(DTid);
        }
        else if (D_PASS == Passes::FFTX)
        {
            FFTPass(DTid, GTid);
        }
        else if (D_PASS == Passes::FFTY)
        {
            FFTPass(DTid, GTid);
        }
    }
}