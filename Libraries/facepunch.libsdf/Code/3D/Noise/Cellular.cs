using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox.Sdf.Noise
{
	public record struct CellularNoiseSdf3D( int Seed, Vector3 CellSize, float DistanceOffset, Vector3 InvCellSize ) : ISdf3D
	{
		public CellularNoiseSdf3D( int seed, Vector3 cellSize, float distanceOffset )
		: this( seed, cellSize, distanceOffset, new Vector3( 1f / cellSize.x, 1f / cellSize.y, 1f / cellSize.z ) )
		{
		}

		public BBox? Bounds => null;

		public float this[ Vector3 pos ]
		{
			get
			{
				var localPos = pos * InvCellSize;
				var cell = (
					X: (int)MathF.Floor( localPos.x ),
					Y: (int)MathF.Floor( localPos.y ),
					Z: (int)MathF.Floor( localPos.z ));

				var cellPos = new Vector3( cell.X, cell.Y, cell.Z ) * CellSize;
				var cellLocalPos = pos - cellPos;

				var minDistSq = float.PositiveInfinity;

				foreach ( var offset in PointOffsets )
				{
					var feature = GetFeature( cell.X + offset.X, cell.Y + offset.Y, cell.Z + offset.Z ) + new Vector3( offset.X, offset.Y, offset.Z ) * CellSize;
					var distSq = (feature - cellLocalPos).LengthSquared;

					minDistSq = Math.Min( minDistSq, distSq );
				}

				return MathF.Sqrt( minDistSq ) - DistanceOffset;
			}
		}

		Vector3 GetFeature( int x, int y, int z )
		{
			var hashX = HashCode.Combine( Seed, x, y, z );
			var hashY = HashCode.Combine( z, Seed, x, y );
			var hashZ = HashCode.Combine( y, z, Seed, x );

			return new Vector3( (hashX & 0xffff) / 65536f, (hashY & 0xffff) / 65536f, (hashZ & 0xffff) / 65536f ) * CellSize;
		}

		private static (int X, int Y, int Z)[] PointOffsets { get; } = Enumerable.Range( -1, 3 ).SelectMany( z =>
			Enumerable.Range( -1, 3 ).SelectMany( y => Enumerable.Range( -1, 3 ).Select( x => (x, y, z) ) ) ).ToArray();
		
		async Task ISdf3D.SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			var localBounds = new BBox( 0f, new Vector3( outputSize.X, outputSize.Y, outputSize.Z ) );
			var bounds = localBounds.Transform( transform );
			var cellBounds = new BBox( bounds.Mins * InvCellSize, bounds.Maxs * InvCellSize );

			var cellMin = (
				X: (int)MathF.Floor( cellBounds.Mins.x ) - 1,
				Y: (int)MathF.Floor( cellBounds.Mins.y ) - 1,
				Z: (int)MathF.Floor( cellBounds.Mins.z ) - 1);

			var cellMax = (
				X: (int) MathF.Floor( cellBounds.Maxs.x ) + 2,
				Y: (int) MathF.Floor( cellBounds.Maxs.y ) + 2,
				Z: (int) MathF.Floor( cellBounds.Maxs.z ) + 2);

			var cellCounts = (
				X: cellMax.X - cellMin.X,
				Y: cellMax.Y - cellMin.Y,
				Z: cellMax.Z - cellMin.Z);

			var features = ArrayPool<Vector3>.Shared.Rent( cellCounts.X * cellCounts.Y * cellCounts.Z );

			try
			{
				for ( var cellZ = 0; cellZ < cellCounts.Z; ++cellZ )
				{
					for ( var cellY = 0; cellY < cellCounts.Y; ++cellY )
					{
						for ( int cellX = 0, index = cellY * cellCounts.X + cellZ * cellCounts.X * cellCounts.Y; cellX < cellCounts.X; ++cellX, ++index )
						{
							features[index] = GetFeature( cellX + cellMin.X, cellY + cellMin.Y, cellZ + cellMin.Z );
						}
					}
				}

				var cellSize = CellSize;
				var invCellSize = InvCellSize;
				var distanceOffset = DistanceOffset;

				await GameTask.WorkerThread();

				for ( var z = 0; z < outputSize.Z; ++z )
				{
					for ( var y = 0; y < outputSize.Y; ++y )
					{
						for ( int x = 0, index = (y + z * outputSize.Y) * outputSize.X; x < outputSize.X; ++x, ++index )
						{
							var pos = transform.PointToWorld( new Vector3( x, y, z ) );
							var localPos = pos * invCellSize;
							var cell = (
								X: (int)MathF.Floor( localPos.x ),
								Y: (int)MathF.Floor( localPos.y ),
								Z: (int)MathF.Floor( localPos.z ));

							var cellPos = new Vector3( cell.X, cell.Y, cell.Z ) * cellSize;
							var cellLocalPos = pos - cellPos;

							var minDistSq = float.PositiveInfinity;

							foreach ( var offset in PointOffsets )
							{
								var featureCell = (
									X: cell.X + offset.X - cellMin.X,
									Y: cell.Y + offset.Y - cellMin.Y,
									Z: cell.Z + offset.Z - cellMin.Z);
								var featureIndex = featureCell.X + featureCell.Y * cellCounts.X + featureCell.Z * cellCounts.X * cellCounts.Y;

								var feature = features[featureIndex] + new Vector3( offset.X, offset.Y, offset.Z ) * cellSize;
								var distSq = (feature - cellLocalPos).LengthSquared;

								minDistSq = Math.Min( minDistSq, distSq );
							}

							output[index] = MathF.Sqrt( minDistSq ) - distanceOffset;
						}
					}
				}
			}
			finally
			{
				ArrayPool<Vector3>.Shared.Return( features );
			}
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			writer.Write( Seed );
			writer.Write( CellSize );
			writer.Write( DistanceOffset );
		}

		public static CellularNoiseSdf3D ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new CellularNoiseSdf3D( reader.Read<int>(), reader.Read<Vector3>(), reader.Read<float>() );
		}
	}
}
