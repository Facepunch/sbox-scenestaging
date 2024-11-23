using System;

namespace Facepunch;

[Title( "FFT Water Wave" )]
[Hide]
public class FFTWaterWave : Component, Component.ExecuteInEditor
{
    internal ComputeShader _compute;
    internal bool _pingPong = false;

    public Texture H0Texture;
    public Texture HtTexture;
    public Texture FFTIntermediary;
    public Texture HeightTexture;
    public Texture DisplacementMap;
    public Texture NormalMap;

    //----------------------------------------------------------------------------------------------------

    enum Passes
    {
        Phase,                  // Updates the wave phases over time.
        Spectrum,               // Calculates the wave spectrum in the frequency domain.
        Horizontal,             // Performs the horizontal (row-wise) FFT.
        Vertical,                // Performs the vertical (column-wise) FFT.
        ComputeDisplacementNormal   // Computes the displacement and normal maps.
    };

    public struct OceanParameters
    {
        public Vector4 WindDirection;    // Wind direction vector (WindDirection.xy)
        public float   WindSpeed;       // Wind speed scalar
        public float   WaveAmplitude;   // Overall amplitude of the waves
        public float   PatchSize;       // Size of the ocean patch
        public float   ChoppyScale;     // Scale of wave choppiness
        public uint    FFTSize;         // Size of the FFT grid (e.g., 256)
        public uint    NumButterflies;  // Number of stages in the FFT (log2(FFTSize))

        public OceanParameters()
        {
            WindDirection = new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
            WindSpeed = 10.0f;
            WaveAmplitude = 10.0f;
            PatchSize = 1000.0f;
            ChoppyScale = 10.0f;
            FFTSize = 256;
            NumButterflies = (uint)Math.Log(FFTSize, 2);
        }
    };

    public int GridSize = 256;
    protected override void OnEnabled()
    {
        _compute = new ComputeShader( "fft_wave_simulation_cs" );

        H0Texture = Texture.Create(GridSize, GridSize, ImageFormat.RG1616F).WithName("H0Texture").WithUAVBinding().Finish();
        HtTexture = Texture.Create(GridSize, GridSize, ImageFormat.RG1616F).WithName("HtTexture").WithUAVBinding().Finish();
        FFTIntermediary = Texture.Create(GridSize, GridSize, ImageFormat.RG1616F).WithName("FFTIntermediary").Finish();
        HeightTexture = Texture.Create(GridSize, GridSize, ImageFormat.R32F).WithName("HeightTexture").WithUAVBinding().Finish();
        DisplacementMap = Texture.Create(GridSize, GridSize, ImageFormat.RG3232F).WithName("DisplacementMap").WithUAVBinding().Finish();
        NormalMap = Texture.Create(GridSize, GridSize, ImageFormat.RGBA8888).WithName("NormalMap").WithUAVBinding().Finish();
    }

    protected override void OnDisabled()
    {
        _compute = null;
    }
    protected override void OnUpdate()
    {
        UpdateFFT();
    }

    public void UpdateFFT()
    {
        OceanParameters c = new()
        {
            /*Time            = Time.Now,
            WindDirection   = this.WindDirection,
            WindSpeed       = this.WindSpeed,
            WaveAmplitude   = this.WaveAmplitude,
            GridSize        = 256*/
        };
        
        _compute.Attributes.SetData( "OceanParameters", c );

        _compute.Attributes.Set("Time", Time.Now);
        
        _compute.Attributes.Set("H0Texture", H0Texture);
        _compute.Attributes.Set("HtTexture", HtTexture);
        _compute.Attributes.Set("FFTIntermediary", FFTIntermediary );
        _compute.Attributes.Set("HeightTexture", HeightTexture);
        _compute.Attributes.Set("DisplacementMap", DisplacementMap);
        _compute.Attributes.Set("NormalMap", NormalMap);

        foreach ( Passes pass in Enum.GetValues( typeof( Passes ) ) )
        {
            _compute.Attributes.SetComboEnum( "D_PASS", pass );
            _compute.Dispatch( GridSize, GridSize, 1 );
            
            _pingPong = !_pingPong;
        }
    }
}
