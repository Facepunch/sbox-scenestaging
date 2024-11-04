
MODES
{
    Default();
}

COMMON
{
    #include "common/shared.hlsl" // This should always be the first include in COMMON
}

CS
{
    //-------------------------------------------------------------------------

    Texture2DArray  <float4> CellBuffer       < Attribute("CellBufferIn"); > ;
    RWTexture2DArray<float4> CellBufferOut    < Attribute("CellBufferOut"); > ;

    SamplerState samplerLinear < Filter( BILINEAR ); AddressU( MIRROR ); AddressV( MIRROR ); >;

    class Cell
    {
        float2 Velocity;
        float Pressure;
        float Divergence;
        float2 UVOffset;
        float Smoke;

        static Cell Sample( float2 vPosition )
        {
            Cell cell;
            cell.Velocity   = CellBuffer.SampleLevel( samplerLinear, float3( vPosition, 0 ), 0 ).xy;
            cell.Pressure   = CellBuffer.SampleLevel( samplerLinear, float3( vPosition, 0 ), 0 ).z;
            cell.Divergence = CellBuffer.SampleLevel( samplerLinear, float3( vPosition, 0 ), 0 ).w;
            cell.UVOffset   = CellBuffer.SampleLevel( samplerLinear, float3( vPosition, 1 ), 0 ).xy;
            cell.Smoke      = CellBuffer.SampleLevel( samplerLinear, float3( vPosition, 1 ), 0 ).z;
            return cell;
        }

        static Cell Fetch( uint2 vPosition )
        {
            Cell cell;
            cell.Velocity   = CellBuffer[ float3( vPosition, 0 ) ].xy;
            cell.Pressure   = CellBuffer[ float3( vPosition, 0 ) ].z;
            cell.Divergence = CellBuffer[ float3( vPosition, 0 ) ].w;
            cell.UVOffset   = CellBuffer[ float3( vPosition, 1 ) ].xy;
            cell.Smoke      = CellBuffer[ float3( vPosition, 1 ) ].z;
            return cell;
        }

        void Store( float2 vPosition )
        {
            CellBufferOut[ uint3( vPosition, 0 ) ] = clamp( float4( Velocity, Pressure, Divergence ), -65534.0f, 65534.0f );
            CellBufferOut[ uint3( vPosition, 1 ) ] = clamp( float4( UVOffset, Smoke, 0 ), -65534.0f, 65534.0f );
        }

        static void GetNeighbors(uint2 vPosition, out Cell left, out Cell right, out Cell top, out Cell bottom)
        {
            left    = Cell::Fetch(vPosition - uint2(1, 0));
            right   = Cell::Fetch(vPosition + uint2(1, 0));
            top     = Cell::Fetch(vPosition - uint2(0, 1));
            bottom  = Cell::Fetch(vPosition + uint2(0, 1));
        }

        static void SampleNeighbors(uint2 vPosition, out Cell left, out Cell right, out Cell top, out Cell bottom, float2 Offset = 1.0f)
        {
            float2 vUV = ( vPosition + 0.5f ) / GridSize;
            float2 texelSize = Offset / GridSize;
            left    = Cell::Sample(vUV - float2(texelSize.x, 0));
            right   = Cell::Sample(vUV + float2(texelSize.x, 0));
            top     = Cell::Sample(vUV + float2(0, texelSize.y));
            bottom  = Cell::Sample(vUV - float2(0, texelSize.y));
        }
    };

    //-------------------------------------------------------------------------

    float2 GridSize  < Attribute("GridSize"); >;
    float  TimeStep  < Attribute("TimeStep"); >;
    float  Time      < Attribute("Time"); >;
    float  Viscosity < Attribute("Viscosity"); >;
    float  Density   < Attribute("Density"); >;

    Texture2D<float4> FlowMap < Attribute("FlowMap"); >;

    //-------------------------------------------------------------------------
    // Objects
    //-------------------------------------------------------------------------
    #define MAX_OBJECTS 10
    cbuffer ObjectsInLiquid
    {
        float4 VelocityPosition[MAX_OBJECTS];
    };

    int NumObjectsInLiquid < Attribute("NumObjectsInLiquid"); >;

    class RippleObject
    {
        float2 Velocity;
        float2 Position;
        float  Radius;

        static RippleObject From( int index )
        {
            RippleObject obj;
            obj.Position = VelocityPosition[index].xy;
            obj.Velocity = VelocityPosition[index].zw;
            obj.Radius   = 5.0f;
            return obj;
        }

        static int NumObjects()
        {
            return NumObjectsInLiquid.x;
        }
    };

    //-------------------------------------------------------------------------

    DynamicCombo( D_STAGE, 0..4, Sys( All ) );

    //-------------------------------------------------------------------------

    void ApplyForces(uint2 vPosition, inout Cell cell)
    {
        float2 velocityToAdd = 0;

        //FlowMap
        if( length(cell.Velocity) < 0.5f )
        velocityToAdd = ( FlowMap.SampleLevel(samplerLinear, vPosition / GridSize, 0).rg - 0.5f ) * 0.0005;

        if( FlowMap.SampleLevel(samplerLinear, vPosition / GridSize, 0).a < 0.5 )
            cell.Pressure = 0;

        //
        // Add forces from moving objects
        //
        for (int i = 0; i < RippleObject::NumObjects(); i++)
        {
            RippleObject obj = RippleObject::From(i);

            if (distance(vPosition, obj.Position) < obj.Radius)
            {
                velocityToAdd -= obj.Velocity * TimeStep * 0.1;
                cell.Smoke += length( obj.Velocity ) * TimeStep * 0.01;
            }
        }

        cell.Velocity += velocityToAdd * 100;

        //cell.Smoke += length(velocityToAdd) * abs(sin(Time )) * 0.1;

        // Falloff
        float flFalloff = 0.25;
        cell.Smoke *= 1.0 - TimeStep * flFalloff;
        cell.Pressure *= 1.0 - TimeStep * flFalloff;

        cell.UVOffset = lerp(cell.UVOffset, vPosition/GridSize, TimeStep);

        // Not related to forces but showcasing stuff
        {
            // Pretend there's a wall
            if( vPosition.x > 225 && vPosition.x < 270 && vPosition.y > 225 && vPosition.y < 270 )
            {
                cell.Velocity = 0;
                cell.Pressure = 0;
                cell.Divergence = 0;
                cell.Smoke = 0;
            }

            // And a sprinkler on top
            if( distance(vPosition, float2( 300, 250 ) ) < 2 )
            {
                cell.Velocity = 0;
                cell.Pressure = sin( Time * 10 ) * 20;
                cell.Divergence = 0;
                cell.Smoke = 1;
            }
        }

        return;
        // Reset - TEST
        if ((int)(Time) % 10 == 0)
        {
            cell.Velocity = 0;
            cell.Pressure = 0;
            cell.Divergence = 0;
            cell.Smoke = 0;
            cell.UVOffset = vPosition / GridSize;
        }
    }

    //-------------------------------------------------------------------------

    void Advect( uint2 vPosition, inout Cell cell )
    {
        float2 pos = ( ( vPosition + 0.5 ) + ( cell.Velocity * TimeStep  ) ) / GridSize;
        cell.Velocity = Cell::Sample( pos ).Velocity;
    }

    void AdvectDye( uint2 vPosition, inout Cell cell )
    {
        float2 pos = ( ( vPosition + 0.5 ) + ( cell.Velocity * TimeStep  ) ) / GridSize;
        cell.Smoke = Cell::Sample( pos ).Smoke ;
        cell.UVOffset = Cell::Sample( pos ).UVOffset;
    }

    void Divergence( uint2 vPosition, inout Cell cell )
    {
        Cell L, R, T, B;
        Cell::SampleNeighbors( vPosition, L, R, T, B );

        cell.Divergence = ( R.Velocity.x - L.Velocity.x + T.Velocity.y - B.Velocity.y ) * 0.5f;
    }
    
    void Pressure(uint2 vPosition, inout Cell cell)
    {
        Cell L, R, T, B;
        Cell::SampleNeighbors(vPosition, L, R, T, B );

        float pressure = ( L.Pressure + R.Pressure + B.Pressure + T.Pressure - cell.Divergence) * 0.25f;
        cell.Pressure = pressure;
    }
    
    void Gradient(uint2 vPosition, inout Cell cell)
    {
        Cell L, R, T, B;
        Cell::SampleNeighbors(vPosition, L, R, T, B );

        cell.Velocity -= float2( R.Pressure - L.Pressure, T.Pressure - B.Pressure ) * ( 1.0 - 0.8 );
        
    }
    
    void VorticityConfinement(uint2 vPosition, inout Cell cell)
    {
        Cell L, R, T, B;
        Cell::SampleNeighbors(vPosition, L, R, T, B);

        float2 force = float2( (T.Divergence) - (B.Divergence),  (L.Divergence) - (R.Divergence) ) * 0.5;
        force /= max( length(force), 0.00001 );
        force *= cell.Divergence;

        cell.Velocity += force * 0.5;
    }

    //-------------------------------------------------------------------------

    [numthreads(8, 8, 1)] 
    void MainCs(uint3 vThreadId : SV_DispatchThreadID)
    {
        Cell cell = Cell::Fetch( vThreadId.xy );

        cell.Velocity *= 1.0;

        #if D_STAGE == 0
            VorticityConfinement( vThreadId.xy, cell );
        #elif D_STAGE == 1
            ApplyForces( vThreadId.xy, cell );
            Divergence( vThreadId.xy, cell );
        #elif D_STAGE == 2
            Pressure( vThreadId.xy, cell );
        #elif D_STAGE == 3
            Gradient( vThreadId.xy, cell );
        #elif D_STAGE == 4
            Advect( vThreadId.xy, cell );
            AdvectDye( vThreadId.xy, cell );
        #endif

        cell.Store( vThreadId.xy );
    }
}