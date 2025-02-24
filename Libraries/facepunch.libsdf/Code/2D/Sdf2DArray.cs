using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sandbox.Sdf;

internal record struct Sdf2DArrayData( byte[] Samples, int BaseIndex, int Size, int RowStride )
{
	public byte this[int x, int y]
	{
		get
		{
			if ( x < -1 || x > Size + 1 || y < -1 || y > Size + 1 )
			{
				return 191;
			}

			return Samples[BaseIndex + x + y * RowStride];
		}
	}
}

/// <summary>
/// Array containing raw SDF samples for a <see cref="Sdf2DChunk"/>.
/// </summary>
public partial class Sdf2DArray : SdfArray<ISdf2D>
{
	/// <summary>
	/// Array containing raw SDF samples for a <see cref="Sdf2DChunk"/>.
	/// </summary>
	public Sdf2DArray()
		: base( 2 )
	{
	}

	/// <inheritdoc />
	protected override Texture CreateTexture()
	{
		return new Texture2DBuilder()
			.WithFormat( ImageFormat.I8 )
			.WithSize( ArraySize, ArraySize )
			.WithData( FrontBuffer )
			.WithAnonymous( true )
			.Finish();
	}

	/// <inheritdoc />
	protected override void UpdateTexture( Texture texture )
	{
		texture.Update( FrontBuffer );
	}

	private (int MinX, int MinY, int MaxX, int MaxY) GetSampleRange( Rect bounds )
	{
		var (minX, maxX, _, _) = GetSampleRange( bounds.Left, bounds.Right );
		var (minY, maxY, _, _) = GetSampleRange( bounds.Top, bounds.Bottom );

		return (minX, minY, maxX, maxY);
	}

	/// <inheritdoc />
	public override async Task<bool> AddAsync<T>( T sdf )
	{
		var (minX, minY, maxX, maxY) = GetSampleRange( sdf.Bounds );
		var maxDist = Quality.MaxDistance;

		await GameTask.WorkerThread();

		var changed = false;

		for ( var y = minY; y < maxY; ++y )
		{
			var worldY = (y - Margin) * UnitSize;

			for ( int x = minX, index = minX + y * ArraySize; x < maxX; ++x, ++index )
			{
				var worldX = (x - Margin) * UnitSize;
				var sampled = sdf[new Vector2( worldX, worldY )];

				if ( sampled >= maxDist ) continue;

				var encoded = Encode( sampled );

				var oldValue = BackBuffer[index];
				var newValue = Math.Min( encoded, oldValue );

				BackBuffer[index] = newValue;

				changed |= oldValue != newValue;
			}
		}

		if ( changed )
		{
			SwapBuffers();
			MarkChanged();
		}

		return changed;
	}

	/// <inheritdoc />
	public override async Task<bool> SubtractAsync<T>( T sdf )
	{
		var (minX, minY, maxX, maxY) = GetSampleRange( sdf.Bounds );
		var maxDist = Quality.MaxDistance;

		await GameTask.WorkerThread();

		var changed = false;

		for ( var y = minY; y < maxY; ++y )
		{
			var worldY = (y - Margin) * UnitSize;

			for ( int x = minX, index = minX + y * ArraySize; x < maxX; ++x, ++index )
			{
				var worldX = (x - Margin) * UnitSize;
				var sampled = sdf[new Vector2( worldX, worldY )];

				if ( sampled >= maxDist ) continue;

				var encoded = Encode( sampled );

				var oldValue = BackBuffer[index];
				var newValue = Math.Max( (byte)(byte.MaxValue - encoded), oldValue );

				BackBuffer[index] = newValue;

				changed |= oldValue != newValue;
			}
		}

		if ( changed )
		{
			SwapBuffers();
			MarkChanged();
		}

		return changed;
	}

	public override Task<bool> RebuildAsync( IEnumerable<ChunkModification<ISdf2D>> modifications )
	{
		throw new NotImplementedException();
	}

	internal void WriteTo( Sdf2DMeshWriter writer, Sdf2DLayer layer, bool renderMesh, bool collisionMesh )
	{
		if ( writer.Samples == null || writer.Samples.Length < FrontBuffer.Length )
		{
			writer.Samples = new byte[FrontBuffer.Length];
		}

		Array.Copy( FrontBuffer, writer.Samples, FrontBuffer.Length );

		var resolution = layer.Quality.ChunkResolution;

		var data = new Sdf2DArrayData( writer.Samples, Margin * ArraySize + Margin, resolution, ArraySize );

		writer.Write( data, layer, renderMesh, collisionMesh );
	}
}
