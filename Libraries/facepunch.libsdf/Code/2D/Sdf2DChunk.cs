using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox.Sdf;

/// <summary>
/// Represents chunks in a <see cref="Sdf2DWorld"/>.
/// Each chunk contains an SDF for a sub-region of one specific layer.
/// </summary>
[Hide]
public partial class Sdf2DChunk : SdfChunk<Sdf2DWorld, Sdf2DChunk, Sdf2DLayer, (int X, int Y), Sdf2DArray, ISdf2D>
{
	public override Vector3 ChunkPosition
	{
		get
		{
			var quality = Resource.Quality;
			return new Vector3( Key.X * quality.ChunkSize, Key.Y * quality.ChunkSize );
		}
	}

	private TranslatedSdf2D<T> ToLocal<T>( in T sdf )
		where T : ISdf2D
	{
		return sdf.Translate( new Vector2( Key.X, Key.Y ) * -Resource.Quality.ChunkSize );
	}

	/// <inheritdoc />
	protected override Task<bool> OnAddAsync<T>( T sdf )
	{
		return Data.AddAsync( ToLocal( sdf ) );
	}

	/// <inheritdoc />
	protected override Task<bool> OnSubtractAsync<T>( T sdf )
	{
		return Data.SubtractAsync( ToLocal( sdf ) );
	}

	protected override Task<bool> OnRebuildAsync( IEnumerable<ChunkModification<ISdf2D>> modifications )
	{
		return Data.RebuildAsync( modifications.Select( x => x with { Sdf = ToLocal( x.Sdf ) } ) );
	}

	/// <inheritdoc />
	protected override async Task OnUpdateMeshAsync()
	{
		var enableRenderMesh = (Resource.FrontFaceMaterial ?? Resource.BackFaceMaterial ?? Resource.CutFaceMaterial) is not null;
		var enableCollisionMesh = Resource.HasCollision && World.HasPhysics;

		if ( !IsValid || !enableRenderMesh && !enableCollisionMesh )
		{
			return;
		}

		using var writer = Sdf2DMeshWriter.Rent();

		await GameTask.WorkerThread();

		writer.DebugOffset = ChunkPosition;
		writer.DebugScale = Data.Quality.UnitSize;

		try
		{
			Data.WriteTo( writer, Resource, enableRenderMesh, enableCollisionMesh );
		}
		catch ( Exception e )
		{
			if ( !IsValid ) return;

			Log.Error( e );

			writer.Reset();
		}

		var renderTask = Task.CompletedTask;
		var collisionTask = Task.CompletedTask;

		if ( enableRenderMesh )
		{
			renderTask = UpdateRenderMeshesAsync(
				new MeshDescription( writer.FrontWriter, Resource.FrontFaceMaterial ),
				new MeshDescription( writer.BackWriter, Resource.BackFaceMaterial ),
				new MeshDescription( writer.CutWriter, Resource.CutFaceMaterial ) );
		}

		if ( enableCollisionMesh )
		{
			var scale = WorldScale.x;
			var offset = new Vector3( Key.X, Key.Y ) * Resource.Quality.ChunkSize;

			collisionTask = GameTask.RunInThreadAsync( async () =>
			{
				// ReSharper disable AccessToDisposedClosure
				var vertices = writer.CollisionMesh.Vertices;

				for ( var i = 0; i < vertices.Count; ++i )
				{
					vertices[i] += offset;
					vertices[i] *= scale;
				}

				await UpdateCollisionMeshAsync( writer.CollisionMesh.Vertices, writer.CollisionMesh.Indices );
				// ReSharper restore AccessToDisposedClosure
			} );
		}

		await GameTask.WhenAll( renderTask, collisionTask );
	}
}
