using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox.Sdf;

/// <summary>
/// Represents chunks in a <see cref="Sdf3DWorld"/>.
/// Each chunk contains an SDF for a sub-region of one specific volume.
/// </summary>
[Hide]
public partial class Sdf3DChunk : SdfChunk<Sdf3DWorld, Sdf3DChunk, Sdf3DVolume, (int X, int Y, int Z), Sdf3DArray, ISdf3D>
{
	public override Vector3 ChunkPosition
	{
		get
		{
			var quality = Resource.Quality;
			return new Vector3( Key.X * quality.ChunkSize, Key.Y * quality.ChunkSize, Key.Z * quality.ChunkSize );
		}
	}

	private TranslatedSdf3D<T> ToLocal<T>( in T sdf )
		where T : ISdf3D
	{
		return sdf.Translate( new Vector3( Key.X, Key.Y, Key.Z ) * -Resource.Quality.ChunkSize );
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

	protected override Task<bool> OnRebuildAsync( IEnumerable<ChunkModification<ISdf3D>> modifications )
	{
		return Data.RebuildAsync( modifications.Select( x => x with { Sdf = ToLocal( x.Sdf ) } ));
	}

	/// <inheritdoc />
	protected override async Task OnUpdateMeshAsync()
	{
		var enableRenderMesh = Resource.Material != null;
		var enableCollisionMesh = Resource.HasCollision;

		if ( !enableRenderMesh && !enableCollisionMesh )
		{
			return;
		}

		using var writer = Sdf3DMeshWriter.Rent();

		await Data.WriteToAsync( writer, Resource );

		if ( !IsValid ) return;

		var renderTask = Task.CompletedTask;
		var collisionTask = Task.CompletedTask;

		if ( enableRenderMesh )
		{
			renderTask = UpdateRenderMeshesAsync( new MeshDescription( writer, Resource.Material ) );
		}

		if ( enableCollisionMesh )
		{
			var scale = WorldScale.x;
			var offset = new Vector3( Key.X, Key.Y, Key.Z ) * Resource.Quality.ChunkSize;

			collisionTask = GameTask.RunInThreadAsync( async () =>
			{
				// ReSharper disable AccessToDisposedClosure
				var vertices = writer.VertexPositions;

				for ( var i = 0; i < vertices.Count; ++i )
				{
					vertices[i] += offset;
					vertices[i] *= scale;
				}

				await UpdateCollisionMeshAsync( writer.VertexPositions, writer.Indices );
				// ReSharper restore AccessToDisposedClosure
			} );
		}

		await GameTask.WhenAll( renderTask, collisionTask );
	}
}
