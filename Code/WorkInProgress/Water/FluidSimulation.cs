using System;

namespace Facepunch;
public class FluidSimulation : Component, Component.ExecuteInEditor
{
    [Property] public BBox SimulationBounds { get; set; } = new BBox( new Vector3( -256, -256, 0 ), new Vector3( 256, 256, 0 ) );
    internal ComputeShader _compute;
    internal Texture[] _textures;
    internal bool _pingPong = false;

    /// <summary>
    /// The fluid texture
    ///
    /// rg: velocity
    /// b: density
    /// a: pressure
    /// </summary>
    public Texture Texture { get => _textures[_pingPong ? 0 : 1]; }

    /// <summary>
    /// Layers of the fluid simulation
    /// </summary>
    public enum Layers
    {
        Velocity,
        Density,
        Pressure,
        Total
    };

    public enum Stages
    {
        Advect,
        Divergence,
        Jacobi,
        SubtractGradient,
        Total
    }

    protected override void DrawGizmos()
    {
        Gizmo.Draw.LineThickness = 2;
        Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
        Gizmo.Draw.LineBBox( SimulationBounds );
    }

    protected override void OnEnabled()
    {
        _textures = new Texture[2];
        _textures[0] = Texture.CreateArray( 512, 512, (int)Layers.Total, ImageFormat.RGBA16161616F ).WithUAVBinding().Finish();
        _textures[1] = Texture.CreateArray( 512, 512, (int)Layers.Total, ImageFormat.RGBA16161616F ).WithUAVBinding().Finish();

        _compute = new ComputeShader( "fluid_simulator_cs" );
    }

    protected override void OnDisabled()
    {
        _textures[0]?.Dispose();
        _textures[1]?.Dispose();
        _textures = null;
        _compute = null;
    }
    protected override void OnFixedUpdate()
    {
        UpdateObjects();
        Update();
    }

    public struct ObjectInLiquid
    {
        public Vector2 Position;
        public Vector2	Velocity;
    }

    public void UpdateObjects()
    {
        List<ObjectInLiquid> objectsInLiquid = new();
        foreach( var obj in Scene.GetAllComponents<Sandbox.PlayerController>() )
        {
            Log.Info( obj.Transform.World.Position );

            var l = new ObjectInLiquid();
            l.Position = 256 - ( obj.Transform.World.Position / 2 );
            l.Velocity = -(obj.Velocity / 4);
            //l.Radius = 20.0f;

            objectsInLiquid.Add(l);
        }

        _compute.Attributes.SetData( "ObjectsInLiquid", objectsInLiquid );
        _compute.Attributes.Set( "NumObjectsInLiquid", objectsInLiquid.Count() );
    }

    public void Update()
    {
        _compute.Attributes.Set( "GridSize", new Vector2( Texture.Width, Texture.Height ) );
        _compute.Attributes.Set( "TimeStep", Time.Delta );
        _compute.Attributes.Set( "Time", Time.Delta );
        _compute.Attributes.Set( "Viscosity", 0.0f );
        _compute.Attributes.Set( "Density", 1000.0f );
        _compute.Attributes.Set( "Pressure", 1.0f );
        _compute.Attributes.Set("FlowMap", Texture.Load(FileSystem.Mounted, $"/materials/water/flowmap_temp_test.jpg"));

        foreach ( Stages stage in Enum.GetValues( typeof( Stages ) ) )
        {
            var textureIn  = _textures[_pingPong ? 1 : 0];
            var textureOut = _textures[_pingPong ? 0 : 1];

            _compute.Attributes.Set( "CellBufferIn", textureIn );
            _compute.Attributes.Set( "CellBufferOut", textureOut );

            _compute.Attributes.SetComboEnum( "D_STAGE", stage );
            _compute.Dispatch( Texture.Width, Texture.Height, 1 );
            
            _pingPong = !_pingPong;
        }

    }
}
