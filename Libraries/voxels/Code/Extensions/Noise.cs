using System;
using Sandbox.Utility;

namespace Voxels;

public static class NoiseExtensions
{
	extension( INoiseField field )
	{
		public void Sample<T>( VoxelSpan<T> dst, BBox domain, Func<Vector3Int, float, T> map )
		{
			var domainScale = domain.Size / dst.Size;
			
			for ( var z = 0; z < dst.Size.z; z++ )
			{
				for ( var y = 0; y < dst.Size.y; y++ )
				{
					for ( var x = 0; x < dst.Size.x; x++ )
					{
						var sample = field.Sample( domain.Mins + new Vector3( x, y, z ) * domainScale );

						dst[x, y, z] = map( new Vector3Int( x, y, z ), sample );
					}
				}
			}
		}
	}
}
