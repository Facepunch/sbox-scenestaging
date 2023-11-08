using Editor;
using Sandbox;
using Sandbox.Utility;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Serialization;

public class Particle
{
	public Vector3 Position;
	public Vector3 Size;
	public Vector3 Velocity;
	public Angles Angles;
	public Color Color;
	public float BornTime;
	public float DeathTime;
	public float Radius;
}

public sealed class SpriteRenderer : BaseComponent, BaseComponent.ExecuteInEditor
{
	SpriteSceneObject _so;
	[Property] public Texture Texture { get; set; } = Texture.White;
	[Property] public Vector2 Size { get; set; } = 10.0f;
	[Property, Range( 0, 2 )] public float Scale { get; set; } = 1.0f;
	[Property, Range( 0, 2 )] public float Speed { get; set; } = 1.0f;
	[Property] public bool Additive { get; set; }


	public override void Update()
	{

	}

	public void Terminate( Particle p )
	{

	}

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
}

class SpriteSceneObject : SceneDynamicObject
{
	public Texture Texture 
	{
		set
		{
			Attributes.Set( "BaseTexture", value );
			Attributes.Set( "BaseTextureSheet", value.SequenceData );
		}
	}

	public SpriteSceneObject( SceneWorld world ) : base( world )
	{
		RenderLayer = SceneRenderLayer.Default;
	}

	//public override void RenderSceneObject()
	//{

		//Graphics.SetupLighting( this, Graphics.Attributes );
		//Graphics.Draw( Vertices, Vertices.Count, Material, Attributes, Graphics.PrimitiveType.Points );
	//}

	public void DrawSprite( in Vector3 center, in Vector2 size, in Angles angles, in Color color )
	{
		var v = new Vertex( center, new Vector4( size.x, size.y, 0, 0 ), color );

		v.Normal.x = angles.pitch;
		v.Normal.y = angles.yaw;
		v.Normal.z = angles.roll;

		AddVertex( v );


			//Vertices.Add( v );
		
	}
}
