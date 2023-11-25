using Sandbox;
using Sandbox.Diagnostics;

namespace Sandbox.TerrainEngine;

/// <summary>
/// Creates a static collider matching the shape of a sibling <see cref="TerrainComponent"/>.
/// </summary>
[Title( "Terrain Collider" )]
[Category( "Physics" )]
[Icon( "terrain" )]
public class TerrainColliderComponent : BaseComponent, BaseComponent.ExecuteInEditor
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

		Log.Warning( $"{this} disabled cause it hangs for 20 seconds to add the massive collision mesh." );
		// GenerateCollisionMesh();
	}

	public override void OnDisabled()
	{
		_body.Remove();
		_body = null;
	}

	/// <summary>
	/// Horrible, takes 20 seconds to add and takes 1.5GB of memory.
	/// </summary>
	void GenerateCollisionMesh()
	{
		var terrain = GetComponent<TerrainComponent>( false ); // Terrain component might be disabled
		if ( terrain == null )
		{
			Log.Warning( $"No TerrainComponent found on {this}" );
			return;
		}

		/*var heightScale = terrain.MaxHeightInInches;
		var scale = terrain.TerrainResolutionInInches;

		var pixels = terrain.HeightMap.GetPixels();
		var width = terrain.HeightMap.Width;
		var height = terrain.HeightMap.Height;

		var min = Vector3.Zero;
		var max = Vector3.Zero;

		var verticies = new Vector3[width * height];
		int[] indices = new int[(width - 1) * (height - 1) * 6];

		// verticies
		for ( int y = 0; y < height; y++ )
		{
			for ( int x = 0; x < width; x++ )
			{
				var color = pixels[ y * width + ( width - x - 1 ) ]; // FIXME: why do I need to do this backwards
				var h = (float)color.b / 255.0f;

				// are we stupid rust heightmap
				if ( terrain.IsRustHeightmap )
				{
					h = (color.b * 256 + color.r) / 256.0f * 2 / 255.0f;
				}

				verticies[y * width + x] = new Vector3( x * scale - width * scale / 2, y * scale - height * scale / 2, h * heightScale );
				min = Vector3.Min( verticies[y * width + x], min );
				max = Vector3.Max( verticies[y * width + x], max );
			}
		}

		int index = 0;
		for ( int y = 0; y < height - 1; y++ )
		{
			for ( int x = 0; x < width - 1; x++ )
			{
				// Each quad requires two triangles
				int bottomLeft = y * width + x;
				int topLeft = (y + 1) * width + x;
				int bottomRight = bottomLeft + 1;
				int topRight = topLeft + 1;

				// Triangle 1
				indices[index++] = bottomLeft;
				indices[index++] = topRight;
				indices[index++] = topLeft;

				// Triangle 2
				indices[index++] = bottomLeft;
				indices[index++] = bottomRight;
				indices[index++] = topRight;
			}
		}

		var shape = _body.AddMeshShape( verticies, indices );
		shape.AddTag( "solid" );
		shape.IsTrigger = false;*/
	}
}
