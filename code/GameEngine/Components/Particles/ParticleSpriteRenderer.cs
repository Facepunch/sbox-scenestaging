using Sandbox;
using Sandbox.Utility;
using System.Collections.Generic;

public sealed class ParticleSpriteRenderer : BaseComponent, BaseComponent.ExecuteInEditor
{
	SpriteSceneObject _so;
	[Property] public Texture Texture { get; set; } = Texture.White;
	[Property, Range( 0, 2 )] public float Scale { get; set; } = 1.0f;
	[Property] public bool Additive { get; set; }


	public override void OnEnabled()
	{
		_so = new SpriteSceneObject( Scene.SceneWorld );
		_so.Transform = Transform.World;
	}

	public override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnPreRender()
	{
		if ( _so is null ) return;
		if ( !TryGetComponent( out ParticleEffect effect ) ) return;


		_so.Transform = Transform.World.WithRotation( Rotation.Identity );
		_so.Material = Material.FromShader( "code/shaders/sprite.vfx" );
		_so.Flags.CastShadows = true;
		_so.Texture = Texture;

		var left = Camera.Main.Rotation.Left;
		var up = Camera.Main.Rotation.Up;

		if ( Additive ) _so.Attributes.SetCombo( "D_BLEND", 1 );
		else _so.Attributes.SetCombo( "D_BLEND", 0 );

		_so.Clear();

		foreach( var p in effect.Particles )
		{
			var rot = p.Angles.ToRotation();

			_so.DrawSprite( p.Position, p.Size * Scale, p.Angles, p.Color );
		}
	}
}
