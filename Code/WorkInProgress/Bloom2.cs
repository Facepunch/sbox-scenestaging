namespace Sandbox;

[Title( "Bloom (Scene Staging)" )]
[Category( "Post Processing" )]
[Icon( "exposure" )]
public class Bloom2 : PostProcess, Component.ExecuteInEditor
{
    [Property, MakeDirty] public SceneCamera.BloomAccessor.BloomMode Mode { get; set; }

    [Range( 0, 10 )]
    [Property, MakeDirty] public float Strength { get; set; } = 1.0f;

    [Range( 0, 2 )]
    [Property, MakeDirty] public float Threshold { get; set; } = 1.0f;
    [Property, MakeDirty, Range( 1.0f, 2.2f) ] public float Gamma { get; set; } = 2.2f;
    [Property, MakeDirty] public Color Tint { get; set; } = Color.White;


    Rendering.CommandList Commands;
    protected override void OnEnabled()
    {
        Commands = new Rendering.CommandList( "Bloom" );
        Camera.AddCommandList( Commands, Rendering.Stage.BeforePostProcess, 1 );
        OnDirty();
    }

    protected override void OnDisabled()
    {
        Camera.RemoveCommandList( Commands );
        Commands = null;
    }

    protected override void OnDirty()
    {
        Rebuild();
    }

    void Rebuild()
    {
        if ( Commands is null )
            return;
        Commands.Reset();

        if ( Strength == 0.0f )
            return;


        Commands.Set( "Threshold", Threshold );
        Commands.Set( "Strength", Strength );
        Commands.Set( "Gamma", Gamma );
        Commands.Set( "CompositeMode", (int)Mode );
        Commands.Set( "Tint", Tint );

        var material = Material.FromShader( "postprocess_bloom_staging" );

        Commands.GrabFrameTexture( "ColorBuffer", true );
        Commands.Blit( material );
    }
}