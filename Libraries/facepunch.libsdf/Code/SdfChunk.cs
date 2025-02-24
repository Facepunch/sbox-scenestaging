using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox.Sdf;

internal static class Static
{
	private static Texture _sWhite3D;

	public static Texture White3D => _sWhite3D ??= new Texture3DBuilder()
		.WithName( "White 3D" )
		.WithSize( 1, 1, 1 )
		.WithFormat( ImageFormat.I8 )
		.WithData( new byte[] { 255 } )
		.Finish();

	private const int MaxPooledMeshes = 256;

	private static List<Mesh> _sMeshPool { get; } = new();

	public static Mesh RentMesh( Material mat )
	{
		if ( _sMeshPool.Count == 0 )
		{
			return new Mesh( mat );
		}

		var last = _sMeshPool[^1];
		_sMeshPool.RemoveAt( _sMeshPool.Count - 1 );

		last.Material = mat;

		return last;
	}

	public static void ReturnMesh( Mesh mesh )
	{
		if ( _sMeshPool.Count >= MaxPooledMeshes )
		{
			return;
		}

		_sMeshPool.Add( mesh );
	}
}

public interface IMeshWriter
{
	bool IsEmpty { get; }
	void ApplyTo( Mesh mesh );
}

public record struct MeshDescription( IMeshWriter Writer, Material Material );

/// <summary>
/// Base class for chunks in a <see cref="SdfWorld{TWorld,TChunk,TResource,TChunkKey,TArray,TSdf}"/>.
/// Each chunk contains an SDF for a sub-region of one specific volume / layer resource.
/// </summary>
/// <typeparam name="TWorld">Non-abstract world type</typeparam>
/// <typeparam name="TChunk">Non-abstract chunk type</typeparam>
/// <typeparam name="TResource">Volume / layer resource</typeparam>
/// <typeparam name="TChunkKey">Integer coordinates used to index a chunk</typeparam>
/// <typeparam name="TArray">Type of <see cref="SdfArray{TSdf}"/> used to contain samples</typeparam>
/// <typeparam name="TSdf">Interface for SDF shapes used to make modifications</typeparam>
public abstract partial class SdfChunk<TWorld, TChunk, TResource, TChunkKey, TArray, TSdf> : Component, Component.ExecuteInEditor
	where TWorld : SdfWorld<TWorld, TChunk, TResource, TChunkKey, TArray, TSdf>
	where TChunk : SdfChunk<TWorld, TChunk, TResource, TChunkKey, TArray, TSdf>, new()
	where TResource : SdfResource<TResource>
	where TChunkKey : struct
	where TArray : SdfArray<TSdf>, new()
	where TSdf : ISdf<TSdf>
{
	/// <summary>
	/// Array storing SDF samples for this chunk.
	/// </summary>
	protected TArray Data { get; private set; }

	/// <summary>
	/// World that owns this chunk.
	/// </summary>
	public TWorld World { get; private set; }

	/// <summary>
	/// Volume or layer resource controlling the rendering and collision of this chunk.
	/// </summary>
	public TResource Resource { get; private set; }

	/// <summary>
	/// Position index of this chunk in the world.
	/// </summary>
	public TChunkKey Key { get; private set; }

	/// <summary>
	/// If this chunk has collision, the generated physics mesh for this chunk.
	/// </summary>
	public PhysicsShape Shape { get; set; }

	/// <summary>
	/// If this chunk is rendered, the scene object containing the generated mesh.
	/// </summary>
	public SceneObject Renderer { get; private set; }

	private float _opacity = 1f;

	public float Opacity
	{
		get => _opacity;
		set
		{
			_opacity = value;
			UpdateOpacity();
		}
	}

	public abstract Vector3 ChunkPosition { get; }

	private readonly List<Mesh> _usedMeshes = new();

	internal void Init( TWorld world, TResource resource, TChunkKey key )
	{
		World = world;
		Resource = resource;
		Key = key;

		Opacity = world.Opacity;

		Flags |= ComponentFlags.Hidden | ComponentFlags.NotNetworked | ComponentFlags.NotSaved;

		Data = new TArray();
		Data.Init( resource.Quality );

		OnInit();
	}

	/// <summary>
	/// Called after the chunk is added to the <see cref="World"/>.
	/// </summary>
	protected virtual void OnInit()
	{

	}

	/// <summary>
	/// Sets every sample in this chunk's SDF to solid or empty.
	/// </summary>
	/// <param name="solid">Solidity to set each sample to.</param>
	public Task ClearAsync( bool solid )
	{
		Data.Clear( solid );
		return GameTask.CompletedTask;
	}

	/// <summary>
	/// Add a world-space shape to this chunk.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">Shape to add</param>
	/// <returns>True if any geometry was modified</returns>
	public Task<bool> AddAsync<T>( T sdf )
		where T : TSdf
	{
		return OnAddAsync( sdf );
	}

	/// <summary>
	/// Subtract a world-space shape from this chunk.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">Shape to subtract</param>
	/// <returns>True if any geometry was modified</returns>
	public Task<bool> SubtractAsync<T>( T sdf )
		where T : TSdf
	{
		return OnSubtractAsync( sdf );
	}

	public Task<bool> RebuildAsync( IEnumerable<ChunkModification<TSdf>> modifications )
	{
		return OnRebuildAsync( modifications );
	}

	protected abstract Task<bool> OnAddAsync<T>( T sdf )
		where T : TSdf;
	protected abstract Task<bool> OnSubtractAsync<T>( T sdf )
		where T : TSdf;
	protected abstract Task<bool> OnRebuildAsync( IEnumerable<ChunkModification<TSdf>> modifications );

	internal async Task UpdateMesh()
	{
		await OnUpdateMeshAsync();

		if ( Renderer == null || Resource.ReferencedTextures is not { Count: > 0 } ) return;

		await GameTask.MainThread();

		foreach ( var reference in Resource.ReferencedTextures )
		{
			var matching = World.GetChunk( reference.Source, Key );
			UpdateLayerTexture( reference.TargetAttribute, reference.Source, matching );
		}
	}

	internal void UpdateLayerTexture( TResource resource, TChunk source )
	{
		if ( Renderer == null || Resource.ReferencedTextures is not { Count: > 0 } ) return;

		foreach ( var reference in Resource.ReferencedTextures )
		{
			if ( reference.Source != resource ) continue;
			UpdateLayerTexture( reference.TargetAttribute, reference.Source, source );
		}
	}

	internal void UpdateLayerTexture( string targetAttribute, TResource resource, TChunk source )
	{
		ThreadSafe.AssertIsMainThread();

		if ( source != null )
		{
			if ( resource != source.Resource )
			{
				Log.Warning( $"Source chunk is using the wrong layer or volume resource" );
				return;
			}

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if ( resource.Quality.ChunkSize != Resource.Quality.ChunkSize )
			{
				Log.Warning( $"Layer {Resource.ResourceName} references {resource.ResourceName} " +
					$"as a texture source, but their chunk sizes don't match" );
				return;
			}

			Renderer.Attributes.Set( targetAttribute, source.Data.Texture );
		}
		else
		{
			Renderer.Attributes.Set( targetAttribute, Data.Dimensions == 3 ? Static.White3D : Texture.White );
		}

		var quality = resource.Quality;
		var arraySize = quality.ChunkResolution + SdfArray<TSdf>.Margin * 2 + 1;

		var margin = (SdfArray<TSdf>.Margin + 0.5f) / arraySize;
		var scale = 1f / quality.ChunkSize;
		var size = 1f - (SdfArray<TSdf>.Margin * 2 + 1f) / arraySize;

		var texParams = new Vector4( margin, margin, scale * size, quality.MaxDistance * 2f );

		Renderer.Attributes.Set( $"{targetAttribute}_Params", texParams );
	}

	/// <summary>
	/// Implements updating the render / collision meshes of this chunk.
	/// </summary>
	/// <returns>Task that completes when the meshes have finished updating.</returns>
	protected abstract Task OnUpdateMeshAsync();

	/// <summary>
	/// Asynchronously updates the collision shape to the defined mesh.
	/// </summary>
	/// <param name="vertices">Collision mesh vertices</param>
	/// <param name="indices">Collision mesh indices</param>
	protected async Task UpdateCollisionMeshAsync( List<Vector3> vertices, List<int> indices )
	{
		await GameTask.MainThread();

		if ( !IsValid ) return;

		UpdateCollisionMesh( vertices, indices );
	}

	protected async Task UpdateRenderMeshesAsync( params MeshDescription[] meshes )
	{
		await GameTask.MainThread();

		if ( !IsValid ) return;

		UpdateRenderMeshes( meshes );
	}

	/// <summary>
	/// Updates the collision shape to the defined mesh. Must be called on the main thread.
	/// </summary>
	/// <param name="vertices">Collision mesh vertices</param>
	/// <param name="indices">Collision mesh indices</param>
	protected void UpdateCollisionMesh( List<Vector3> vertices, List<int> indices )
	{
		if ( !World.HasPhysics )
		{
			return;
		}

		ThreadSafe.AssertIsMainThread();

		if ( indices.Count == 0 )
		{
			Shape?.Remove();
			Shape = null;
		}
		else
		{
			var tags = Resource.SplitCollisionTags;

			if ( !Shape.IsValid() )
			{
				Shape = World.AddMeshShape( vertices, indices );

				foreach ( var tag in tags ) Shape.Tags.Add( tag );
			}
			else
			{
				Shape.UpdateMesh( vertices, indices );
			}

			Shape.EnableSolidCollisions = Active;
		}
	}

	/// <summary>
	/// Updates this chunk's model to use the given set of meshes. Must be called on the main thread.
	/// </summary>
	/// <param name="meshes">Set of meshes this model should use</param>
	private void UpdateRenderMeshes( params MeshDescription[] meshes )
	{
		meshes = meshes.Where( x => x.Material != null && !x.Writer.IsEmpty ).ToArray();

		ThreadSafe.AssertIsMainThread();

		var meshCountChanged = meshes.Length != _usedMeshes.Count;

		if ( meshCountChanged )
		{
			foreach ( var mesh in _usedMeshes )
			{
				Static.ReturnMesh( mesh );
			}

			_usedMeshes.Clear();

			foreach ( var mesh in meshes )
			{
				_usedMeshes.Add( Static.RentMesh( mesh.Material ) );
			}
		}
		else
		{
			for ( var i = 0; i < meshes.Length; ++i )
			{
				_usedMeshes[i].Material = meshes[i].Material;
			}
		}

		for ( var i = 0; i < meshes.Length; ++i )
		{
			meshes[i].Writer.ApplyTo( _usedMeshes[i] );
		}

		if ( !meshCountChanged )
		{
			return;
		}

		if ( _usedMeshes.Count == 0 )
		{
			Renderer?.Delete();
			Renderer = null;
			return;
		}

		var model = new ModelBuilder()
			.AddMeshes( _usedMeshes.ToArray() )
			.Create();

		if ( !Renderer.IsValid() )
		{
			Renderer = new SceneObject( Scene.SceneWorld, model )
			{
				Batchable = Resource.ReferencedTextures is not { Count: > 0 }
			};

			foreach ( var tag in World.Tags )
			{
				Renderer.Tags.Add( tag );
			}
		}

		Renderer.Model = model;

		UpdateTransform();
		UpdateOpacity();
	}

	internal void UpdateTransform()
	{
		if ( !Renderer.IsValid() )
			return;

		Renderer.Transform = World.Transform.World;
		Renderer.Position = World.Transform.World.PointToWorld( ChunkPosition );
	}

	private void UpdateOpacity()
	{
		if ( Renderer is not { } renderer ) return;

		var value = Opacity;

		renderer.ColorTint = Color.White.WithAlpha( value );
		renderer.RenderingEnabled = value > 0f;
		Renderer.Flags.CastShadows = value >= 1f;
	}

	protected override void OnEnabled()
	{
		if ( Shape is { } shape )
		{
			shape.EnableSolidCollisions = true;
		}

		UpdateTransform();
		UpdateOpacity();
	}

	protected override void OnDisabled()
	{
		if ( Renderer is { } renderer )
		{
			renderer.RenderingEnabled = false;
		}

		if ( Shape is { } shape )
		{
			shape.EnableSolidCollisions = false;
		}
	}

	protected override void OnDestroy()
	{
		Data.Dispose();

		Renderer?.Delete();
		Renderer = null;

		Shape?.Remove();
		Shape = null;

		foreach ( var usedMesh in _usedMeshes )
		{
			Static.ReturnMesh( usedMesh );
		}

		_usedMeshes.Clear();

		base.OnDestroy();
	}
}
