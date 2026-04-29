using Sandbox;
using Editor;
using static Sandbox.ClothingContainer;

[Title( "Clothing Dresser" )]
[Category( "Clothing" )]
[Icon( "checkroom", "blue", "white" )]
public sealed class ModelViewerClothingDresser : Component
{
	[Property] SkinnedModelRenderer Source { get; set; }
	[Property] List<Clothing> ClothingList { get ; set; } = new();
	ClothingContainer Container { get; set; } = new ClothingContainer();
	public List<SceneModel> Dressed { get; private set; }

	//Hair Tint
	[Property] Gradient HairTintGradient { get; set; } = new Gradient( new Gradient.ColorFrame( 0.0f, Color.White ), new Gradient.ColorFrame( 0.16f, "#FCC88C" ), new Gradient.ColorFrame(0.34f, "#A57E6A" ), new Gradient.ColorFrame( 0.53f, "#A33900" ), new Gradient.ColorFrame( 0.75f, "#3A271D" ), new Gradient.ColorFrame( 1.0f, "#000000" ) );
	[Property] Color HairTint { get; set; }
	[Property, Range(0,1)] float HairTintValue { get; set; } = 0.4f;

	//Beard Tint
	[Property] Gradient BeardTintGradient { get; set; } = new Gradient( new Gradient.ColorFrame( 0.0f, Color.White ), new Gradient.ColorFrame( 0.16f, "#FCC88C" ), new Gradient.ColorFrame( 0.34f, "#A57E6A" ), new Gradient.ColorFrame( 0.53f, "#A33900" ), new Gradient.ColorFrame( 0.75f, "#3A271D" ), new Gradient.ColorFrame( 1.0f, "#000000" ) );
	[Property] Color BeardTint { get; set; } = Color.White;
	[Property, Range(0,1)] float BeardTintValue { get; set; } = 0.4f;

	protected override void OnStart()
	{
		if ( Source is null )
			return;

		if ( ClothingList is null )
			return;
	
		foreach ( var clothing in ClothingList )
		{
			if ( clothing is null )
				continue;
			var entry = new ClothingEntry( clothing );
			if ( Container.Clothing.Contains( entry ) )
				continue;
			
			Container.Clothing.Add( entry );
		}

		Container.Apply( Source );

		//Find the hair model
		foreach ( var model in GameObject.Children )
		{			
			var mod = model.Components.Get<SkinnedModelRenderer>();

			if( mod is null )
				continue;
			
			if ( model.Name.Contains( "hair" ) || mod.Model.ResourcePath.Contains("hair") && mod.Model.MorphCount <= 1 )
			{
				var hair = model.Components.Get<SkinnedModelRenderer>();
				hair.Tint = HairTint;
			}

			if ( mod.Model.MorphCount >= 1 )
			{
				var beard = model.Components.Get<SkinnedModelRenderer>();
				beard.Tint = BeardTint;
				Log.Info( "Beard Tint: " );
			}
		}
	}
}
