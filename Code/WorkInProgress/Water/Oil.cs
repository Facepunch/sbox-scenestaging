using System;

namespace Facepunch;
public class Oil : Component, Component.ExecuteInEditor
{
    Model Model { get; set; }
    public SceneObject SceneObject { get; private set; }

    private Model CreateTessellatedPlane( int Tessellation, float Size )
    {
        var material = Material.FromShader( "oil" );
        var mesh = new Mesh( material );

        // Calculate the number of vertices and the step between them
        int verticesPerLine = Tessellation + 1;
        float halfSize = Size / 2.0f;
        float step = Size / Tessellation;

        // Create vertex buffer
        var vertices = new List<Vertex>();
        for ( int i = 0; i < verticesPerLine; i++ )
        {
            for ( int j = 0; j < verticesPerLine; j++ )
            {
                float x = -halfSize + j * step;
                float y = -halfSize + i * step;
                var position = new Vector3( x, y, 0 );

                // Perspective correction
                position *= position.DistanceSquared( Vector3.Zero );

                // Edge vertices are moved to infinity
                if ( i == 0 || i == Tessellation || j == 0 || j == Tessellation )
                {
                    position.x *= 1000;
                    position.y *= 1000;
                }

                vertices.Add( new Vertex( position, Vector3.Up, Vector3.Forward, new Vector4( x / Size, y / Size, 0, 0 ) ) );
            }
        }
        mesh.CreateVertexBuffer<Vertex>( vertices.Count, Vertex.Layout, vertices.ToArray() );

        // Create index buffer
        var indices = new List<int>();
        for ( int i = 0; i < Tessellation; i++ )
        {
            for ( int j = 0; j < Tessellation; j++ )
            {
                int start = i * verticesPerLine + j;
                indices.Add( start );
                indices.Add( start + 1 );
                indices.Add( start + verticesPerLine );

                indices.Add( start + verticesPerLine );
                indices.Add( start + 1 );
                indices.Add( start + verticesPerLine + 1 );
            }
        }
        mesh.CreateIndexBuffer( indices.Count, indices.ToArray() );

        // Set bounds
        mesh.Bounds = BBox.FromHeightAndRadius( 50, 50000 );

        var modelBuilder = new ModelBuilder();
        return modelBuilder.AddMesh( mesh ).Create();
    }


    protected override void OnEnabled()
    {
        Model = CreateTessellatedPlane( 200, 20 );

        SceneObject = new SceneObject( Scene.SceneWorld, Model, Transform.World );
        SceneObject.SetComponentSource( this );
        SceneObject.Flags.CastShadows = false;
        SceneObject.RenderingEnabled = true;

        Transform.OnTransformChanged = OnTransformChanged;
    }

    protected override void OnDisabled()
    {
        SceneObject?.Delete();
        SceneObject = null;
        Model = null;
    }

    public void OnTransformChanged()
    {
        SceneObject.Transform = Transform.World;
    }

    protected override void OnFixedUpdate()
    {
        var fluidSimulator = GameObject.Components.Get<FluidSimulation>();

        if(fluidSimulator == null)
            return;

        // Matrix.FromTransform is internal, no way to pass Transform directly
        var matrix = new Matrix();

        matrix = Matrix.CreateScale( 1.0f / fluidSimulator.SimulationBounds.Size ) * Matrix.CreateRotation( Transform.World.Rotation ) * Matrix.CreateTranslation( ( Transform.World.Position + fluidSimulator.SimulationBounds.Center + ( fluidSimulator.SimulationBounds.Size * 0.5f ) ) / fluidSimulator.SimulationBounds.Size );

        SceneObject.Attributes.Set( "FluidTexture", fluidSimulator.Texture );
        SceneObject.Attributes.SetCombo("D_FLUID_SIMULATION", 1);
        SceneObject.Attributes.Set("PositionToBounds", matrix );
    }


}