using Sandbox;

[Title( "Text Renderer" )]
[Category( "Rendering" )]
[Icon( "font_download" )]
public sealed class TextRenderer : BaseComponent, BaseComponent.ExecuteInEditor
{
	SceneObject _so;

	[Property] public Color Color { get; set; } = Color.White;
	[Property,Range( 0, 2 )] public float Scale { get; set; } = 1.0f;
	[Property,Range( 1, 128 )] public float FontSize { get; set; } = 32.0f;
	[Property] public string FontFamily { get; set; } = "Poppins";
	[Property] public string Text { get; set; } = "Hello! ❤";

	// TODO - alignment
	// TODO - rect size

	public override void OnEnabled()
	{
		_so = new TextSceneObject( Scene.SceneWorld );
		_so.Transform = Transform.World;
	}

	public override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnPreRender()
	{
		if ( _so is TextSceneObject so )
		{
			so.Transform = Transform.World.WithScale( Transform.Scale.x * Scale );
			so.ColorTint = Color;
			so.Text = Text;
			so.FontFamily = FontFamily;
			so.FontSize = FontSize;
		}
	}
}


file class TextSceneObject : SceneCustomObject
{
	public float FontSize { get; set; } = 32;
	public string FontFamily { get; set; } = "Poppins";
	public string Text { get; set; } = "Text";

	public TextSceneObject( SceneWorld world ) : base( world )
	{
		RenderLayer = SceneRenderLayer.Default;
	}

	public override void RenderSceneObject()
	{
		var textFlags = TextFlag.DontClip | TextFlag.Center;

		Graphics.Attributes.SetCombo( "D_WORLDPANEL", 1 );

		// Set a dummy WorldMat matrix so that ScenePanelObject doesn't break the transforms.
		Matrix mat = Matrix.CreateRotation( Rotation.From( 0, -90, 90 ) );
		Graphics.Attributes.Set( "WorldMat", mat );

		Graphics.DrawText( new Rect( 0 ), Text, ColorTint, FontFamily, FontSize, 800.0f, textFlags );
	}
}
