// Mesh Blend Expand Compute Shader
// Jump Flood Algorithm: propagates nearest cross-region edge per pixel

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

	Texture2D<uint2> InputEdgeMap < Attribute( "InputEdgeMap" ); >;
	Texture2D<float2> Mask < Attribute( "Mask" ); >;
	RWTexture2D<uint2> OutputEdgeMap < Attribute( "OutputEdgeMap" ); >;
	int StepSize < Attribute( "StepSize" ); Default( 32 ); >;

	#define INVALID_EDGE uint2( 0xFFFFFFFF, 0xFFFFFFFF )
	#define ID_EPSILON 0.001

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 threadId : SV_DispatchThreadID )
	{
		uint2 texSize;
		InputEdgeMap.GetDimensions( texSize.x, texSize.y );
		
		if ( any( threadId.xy >= texSize ) )
			return;
		
		float currentRegionId = Mask.Load( int3( threadId.xy, 0 ) ).r;
		
		if ( currentRegionId <= ID_EPSILON )
		{
			OutputEdgeMap[threadId.xy] = INVALID_EDGE;
			return;
		}
		
		uint2 nearestEdge = InputEdgeMap.Load( int3( threadId.xy, 0 ) );
		float bestDistSq = 1e10;
		
		if ( all( nearestEdge != INVALID_EDGE ) )
		{
			float2 toEdge = float2( threadId.xy ) - float2( nearestEdge );
			bestDistSq = dot( toEdge, toEdge );
		}
		
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
			int2 samplePos = int2( threadId.xy ) + neighborDirs[i] * StepSize;
			samplePos = clamp( samplePos, int2( 0, 0 ), int2( texSize ) - int2( 1, 1 ) );
			
			uint2 candidateEdge = InputEdgeMap.Load( int3( samplePos, 0 ) );
			
			if ( any( candidateEdge == INVALID_EDGE.x ) )
				continue;
			
			float candidateRegionId = Mask.Load( int3( int2( candidateEdge ), 0 ) ).r;
			
			if ( abs( candidateRegionId - currentRegionId ) > ID_EPSILON )
			{
				float2 toCandidate = float2( threadId.xy ) - float2( candidateEdge );
				float candidateDistSq = dot( toCandidate, toCandidate );
				
				if ( candidateDistSq < bestDistSq )
				{
					bestDistSq = candidateDistSq;
					nearestEdge = candidateEdge;
				}
			}
		}
		
		OutputEdgeMap[threadId.xy] = nearestEdge;
	}
}
