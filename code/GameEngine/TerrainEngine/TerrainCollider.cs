using Sandbox.Diagnostics;

namespace Sandbox.TerrainEngine;

/// <summary>
/// Creates a static collider matching the shape of a sibling <see cref="Terrain"/>.
/// </summary>
[Title( "Terrain Collider" )]
[Category( "Physics" )]
[Icon( "terrain" )]
public class TerrainCollider : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property] public Surface Surface { get; set; }

	PhysicsBody _body;

	public override void OnEnabled()
	{
		Assert.True( _body == null );
		Assert.NotNull( Scene, "Tried to create physics object but no scene" );
		Assert.NotNull( Scene.PhysicsWorld, "Tried to create physics object but no physics world" );

		_body = new PhysicsBody( Scene.PhysicsWorld );

		_body.UseController = false;
		_body.BodyType = PhysicsBodyType.Static;
		_body.GameObject = GameObject;
		_body.GravityEnabled = false;
		_body.Transform = Transform.World;

		GenerateCollisionMesh();
	}

	public override void OnDisabled()
	{
		_body.Remove();
		_body = null;
	}

	void GenerateCollisionMesh()
	{
		var terrain = GetComponent<Terrain>( false ); // Terrain component might be disabled
		if ( terrain == null )
		{
			Log.Warning( $"No TerrainComponent found on {this}" );
			return;
		}

		var data = terrain.TerrainData;
		var sizeScale = terrain.TerrainResolutionInInches;
		var heightScale = terrain.MaxHeightInInches / ushort.MaxValue;
		//var shape = _body.AddHeightFieldShape( data.HeightMap, data.HeightMapWidth, data.HeightMapHeight, sizeScale, heightScale );
		//shape.Tags.Add( "solid" );
	}
}
