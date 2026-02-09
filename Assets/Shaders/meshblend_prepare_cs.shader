// Mesh Blend Prepare Compute Shader
// Edge detection with LDS tile caching for fast neighbor region comparison

MODES
{
	Default();
}

FEATURES
{
}

COMMON
{
	#include "system.fxc"
}

CS
{
	#include "common.fxc"

	Texture2D<float2> InputMask < Attribute( "InputMask" ); >;
	RWTexture2D<uint2> OutputEdgeMap < Attribute( "OutputEdgeMap" ); >;

	#define INVALID_EDGE uint2( 0xFFFFFFFF, 0xFFFFFFFF )
	#define ID_EPSILON 0.001

	// LDS tile: 8x8 threads + 1px border on each side = 10x10
	#define TILE_BORDER 1
	#define TILE_SIZE 10
	
	groupshared float g_Cache[TILE_SIZE * TILE_SIZE];

	uint CoordToCache( int2 coord )
	{
		coord = clamp( coord, int2( 0, 0 ), int2( TILE_SIZE - 1, TILE_SIZE - 1 ) );
		return coord.y * TILE_SIZE + coord.x;
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 threadId : SV_DispatchThreadID, uint3 groupId : SV_GroupID, uint3 localId : SV_GroupThreadID )
	{
		uint2 texSize;
		InputMask.GetDimensions( texSize.x, texSize.y );
		
		// Preload padded tile into LDS
		int2 tileOrigin = int2( groupId.xy ) * 8 - TILE_BORDER;
		
		for ( uint y = localId.y; y < TILE_SIZE; y += 8 )
		{
			for ( uint x = localId.x; x < TILE_SIZE; x += 8 )
			{
				int2 texel = clamp( tileOrigin + int2( x, y ), int2( 0, 0 ), int2( texSize ) - int2( 1, 1 ) );
				float regionId = InputMask.Load( int3( texel, 0 ) ).r;
				g_Cache[CoordToCache( int2( x, y ) )] = regionId;
			}
		}
		
		GroupMemoryBarrierWithGroupSync();
		
		if ( any( threadId.xy >= texSize ) )
			return;
		
		int2 localPos = int2( 1, 1 ) + int2( localId.xy );
		float regionId = g_Cache[CoordToCache( localPos )];
		
		if ( regionId <= ID_EPSILON )
		{
			OutputEdgeMap[threadId.xy] = INVALID_EDGE;
			return;
		}
		
		// Cardinal directions first, then diagonals
		int2 neighborDirs[8];
		neighborDirs[0] = int2( -1, 0 );
		neighborDirs[1] = int2( 1, 0 );
		neighborDirs[2] = int2( 0, -1 );
		neighborDirs[3] = int2( 0, 1 );
		neighborDirs[4] = int2( -1, -1 );
		neighborDirs[5] = int2( -1, 1 );
		neighborDirs[6] = int2( 1, -1 );
		neighborDirs[7] = int2( 1, 1 );

		for ( uint i = 0; i < 8; i++ )
		{
			int2 neighborLocal = localPos + neighborDirs[i];
			float neighborId = g_Cache[CoordToCache( neighborLocal )];
			
			if ( abs( neighborId - regionId ) > ID_EPSILON && neighborId > ID_EPSILON )
			{
				int2 neighborPixel = int2( threadId.xy ) + neighborDirs[i];
				neighborPixel = clamp( neighborPixel, int2( 0, 0 ), int2( texSize ) - int2( 1, 1 ) );
				OutputEdgeMap[threadId.xy] = uint2( neighborPixel );
				return;
			}
		}
		
		OutputEdgeMap[threadId.xy] = INVALID_EDGE;
	}
}
