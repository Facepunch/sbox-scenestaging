namespace Sandbox;

[Title( "Bloom (Scene Staging)" )]
[Category( "Post Processing" )]
[Icon( "exposure" )]
public class Bloom2 : PostProcess, Component.ExecuteInEditor
{
    [Property] public SceneCamera.BloomAccessor.BloomMode Mode { get; set; }

    [Range( 0, 10 )]
    [Property] public float Strength { get; set; } = 1.0f;

    [Range( 0, 2 )]
    [Property] public float Threshold { get; set; } = 0.5f;

    [Range( 0, 5 )]
    [Property] public float ThresholdWidth { get; set; }
    [Property] public Curve BloomCurve { get; set; } = new Curve( new Curve.Frame( 0.0f, 0.5f ), new Curve.Frame( 1.0f, 1.0f ) );
    [Property] public Gradient BloomColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0.0f, Color.White ), new Gradient.ColorFrame( 1.0f, Color.White ) );

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

    protected override void OnUpdate()
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
        Commands.Set( "CompositeMode", (int)Mode );

        var material = Material.FromShader( "postprocess_bloom_staging" );

        Commands.GrabFrameTexture( "ColorBuffer", true );
        Commands.Blit( material );
    }
}