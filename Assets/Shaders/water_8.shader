//
// this is a feasibility test, don't take this as final
//

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	ToolsVis( S_MODE_TOOLS_VIS );
}

COMMON
{
	#define S_TRANSLUCENT 1
	#define S_SPECULAR 1
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		// Add your vertex manipulation functions here
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"
    
    RenderState( BlendEnable, true );
    RenderState( SrcBlend, SRC_ALPHA );
    RenderState( DstBlend, INV_SRC_ALPHA );
    RenderState( BlendOp, ADD );
    RenderState( SrcBlendAlpha, ONE );
    RenderState( DstBlendAlpha, INV_SRC_ALPHA );
    RenderState( BlendOpAlpha, ADD );

	BoolAttribute( bWantsFBCopyTexture, true );

	CreateTexture2D( g_tFrameBufferCopyTexture ) < Attribute("FrameBufferCopyTexture");   SrgbRead( false ); Filter(MIN_MAG_MIP_LINEAR);    AddressU( MIRROR );     AddressV( MIRROR ); > ;    

	float4 SurfaceColor < UiType(Color); Default4(0.0, 0.5, 0.6, 0.5); UiGroup("Water"); > ;
	float4 DepthColor < UiType(Color); Default4(1.0, 0.0, 0.0, 1); UiGroup("Water"); > ;
	float MaxDepth < Default(256.0f); Range(0.0f, 256); UiGroup("Water"); > ;
	float Thickness < Default(1.0f); Range(0.0f, 1); UiGroup("Water"); > ;
	float Refraction < Default(1.0f); Range(0.0f, 1); UiGroup("Water"); > ;

	float BigWaveSize < Default(1.0f); Range(0.0f, 10); UiGroup("Big Wave"); > ;
	float BigWaveScale < Default(1.0f); Range(0.0f, 100); UiGroup("Big Wave"); > ;
	float BigWaveTime < Default(1.0f); Range(0.0f, 20); UiGroup("Big Wave"); > ;

	float SmallWaveSize < Default(0.4f); Range(0.0f, 10); UiGroup("Small Wave"); > ;
	float SmallWaveScale < Default(10.0f); Range(0.0f, 100); UiGroup("Small Wave"); > ;
	float SmallWaveTime < Default(13.0f); Range(0.0f, 20); UiGroup("Small Wave"); > ;

	float FlowSize < Default(1.0f); Range(0.0f, 20); UiGroup("Flow"); > ;
	float FlowSpeed < Default(0.01f); Range(0.0f, 1); UiGroup("Flow"); > ;

	float NormalScale < Default(0.5f); Range(0.0f, 2); UiGroup("Tweaks"); > ;
	float TextureScale < Default(128f); Range(0.0f, 1024); UiGroup("Tweaks"); > ;

	CreateInputTexture2D( EdgeFoam, Linear, 8, "", "", "Edge Foam", Default4( 0.5, 0.5, 1.0, 1 ) );
	CreateTexture2D( g_tEdgeFoam ) < Channel( RGBA, Box( EdgeFoam ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

	float4 MainPs( PixelInput i ) : SV_Target
	{
		float3 worldPos = g_vCameraPositionWs + i.vPositionWithOffsetWs;
		float distanceFromEye = length( i.vPositionWithOffsetWs );
		float3 depthPos = Depth::GetWorldPosition( i.vPositionSs.xy );

		float depth = distance( depthPos, float3( depthPos.x, depthPos.y, worldPos.z ) );
		float eyedepth = distance( depthPos, worldPos );

		float3 op = worldPos;

		float textureScale = TextureScale;
		float moveSpeed = textureScale / 512;


		/// large bounce
		{
			worldPos.x += sin( (op.x / BigWaveScale) + (g_flTime * BigWaveTime) ) * BigWaveSize;
			worldPos.y += cos( (op.y / BigWaveScale) + (g_flTime * BigWaveTime) ) * BigWaveSize;
		}

		/// small bounce
		{
			worldPos.x += sin( (op.x / SmallWaveScale) + g_flTime * SmallWaveTime ) * SmallWaveSize;
			worldPos.y += cos( (op.y / SmallWaveScale) + g_flTime * SmallWaveTime ) * SmallWaveSize;
		}

		i.vTextureCoords.x = worldPos.x / textureScale;
		i.vTextureCoords.y = worldPos.y / textureScale; 

		float2 foamuv = i.vTextureCoords;
		float3 delta = op - worldPos;

		i.vTextureCoords.x += sin( g_flTime * FlowSpeed / FlowSize ) * FlowSize* moveSpeed;
		i.vTextureCoords.y += cos( g_flTime * FlowSpeed / FlowSize ) * FlowSize * moveSpeed;

		Material m = Material::From( i );

		i.vTextureCoords = i.vTextureCoords.xy * 0.6;

		i.vTextureCoords.x -= sin( g_flTime * moveSpeed * FlowSpeed / FlowSize ) * FlowSize* moveSpeed;
		i.vTextureCoords.y -= cos( g_flTime * moveSpeed * FlowSpeed / FlowSize ) * FlowSize* moveSpeed;

		Material m2 = Material::From( i );

		float normalSize = 0.1;

		m.Normal.x += (delta.x * normalSize) - normalSize * 0.5f;
		m.Normal.y += (delta.y * normalSize) - normalSize * 0.5f;

		m = Material::lerp( m, m2, 0.5 );

		//
		// Adds a "distant" layer, which is larger to hide tiling
		//
		{
			i.vTextureCoords *= 0.05;
			i.vTextureCoords.x += g_flTime * 0.01 * moveSpeed;
			Material mm = Material::From( i );

			float l = saturate( length( i.vPositionWithOffsetWs ) / 2048 );
			l = clamp( l, 0.0, 1 );
			l = pow( l, 1 );
			m = Material::lerp( m, mm, l );
		}

		float3 surface = m.Albedo;

		// should we add surface normal to this?
		float fres = saturate( 1 - dot( normalize( g_vCameraPositionWs - worldPos ), m.Normal ) );
		fres = pow( fres, 5 ); // Schlick Fresnel which is just about equivalent to water BRDF with roughness 0

		// refraction
		{
			float2 uv = i.vPositionSs.xy;
			uv += m.Normal.xy * clamp( eyedepth / MaxDepth, 0, 1 ) * Refraction * 500 * pow(  1-fres, 1 );

			float3 rdepthPos = Depth::GetWorldPosition( uv );
			float rdepth = distance( rdepthPos, float3( rdepthPos.x, rdepthPos.y, worldPos.z ) );
			float reyedepth = distance( rdepthPos, worldPos );

            float3 vRefractionColor = Tex2DLevel( g_tFrameBufferCopyTexture, uv * g_vInvViewportSize * g_vFrameBufferCopyInvSizeAndUvScale.zw, 0 ).rgb;

			m.Albedo = vRefractionColor.rgb;
			m.Albedo.rgb = lerp( m.Albedo.rgb, DepthColor.rgb, clamp( reyedepth / MaxDepth, 0, 1 ) * DepthColor.a );
		}

		// get flatter the further away
		{
			float lval = distanceFromEye / 2048;
			lval = clamp( lval, 0, 0.66 );

			m.Normal = normalize( lerp( m.Normal, float3( 0, 0, 1 ), lval ) ); 
		}

		// get flatter the more you're looking down
		
		{
			float lval = saturate( 1-fres );
			lval = pow( lval, 0.5 );
			lval = clamp( lval, 0, 0.8 );

			m.Normal = normalize( lerp( m.Normal, float3( 0, 0, 1 ), lval ) ); 
		}

		// scale normals, artist adjustment
		{
			m.Normal = lerp( m.Normal, float3( 0, 0, 1 ), 1 - NormalScale );
		}

        // Reflection
        {
            const float3 vRayWs = CalculateCameraToPositionDirWs( m.WorldPosition );

            float3 vReflectWs = reflect(vRayWs, m.Normal);
            float2 vPositionSs = i.vPositionSs.xy;

            TraceResult trace = ScreenSpace::Trace(m.WorldPosition, vReflectWs, vPositionSs, 64);

            // Composite result
            {
				float2 uv = saturate( trace.HitClipSpace.xy * g_vFrameBufferCopyInvSizeAndUvScale.zw );
                float3 vReflectionColor = g_tFrameBufferCopyTexture.SampleLevel(g_tFrameBufferCopyTexture_sampler, uv, 0).rgb;

                // Calculate derivatives
                float2 dx = ddx(trace.HitClipSpace.xy);
                float2 dy = ddy(trace.HitClipSpace.xy);

                // Sample neighboring texels for anti-aliasing
                float3 vReflectionColor_dx = g_tFrameBufferCopyTexture.SampleLevel(g_tFrameBufferCopyTexture_sampler, uv + dx, 0).rgb;
                float3 vReflectionColor_dy = g_tFrameBufferCopyTexture.SampleLevel(g_tFrameBufferCopyTexture_sampler, uv + dy, 0).rgb;
                float3 vReflectionColor_dxy = g_tFrameBufferCopyTexture.SampleLevel(g_tFrameBufferCopyTexture_sampler, uv + dx + dy, 0).rgb;

                // Average the colors
                float3 vReflectionColorAA = (vReflectionColor + vReflectionColor_dx + vReflectionColor_dy + vReflectionColor_dxy) / 4.0;

                // Apply the reflection color with anti-aliasing

				if ( trace.Confidence > 0 ) m.Roughness = 1; // Cut off specular from this texel
                m.Albedo.rgb = lerp(m.Albedo.rgb, vReflectionColorAA, trace.Confidence * fres);
            }
        }


		surface = lerp( surface, SurfaceColor.rgb, SurfaceColor.a );		
		m.Albedo.rgb = lerp( m.Albedo.rgb, surface, fres );

		m.Opacity = saturate( depth / 3 );

		// edge foam. you can kind of see what I was going for, but there's
		// no real good way of detecting the shore
		//if ( false )
		{
			float4 foam = Tex2D( g_tEdgeFoam, foamuv / 3 );

			float edge = saturate( 1-(depth / 10) ) * foam.r;
			edge = pow( edge, 10 );
			edge = saturate( edge * 500 );
			m.Albedo.rgb = lerp( m.Albedo.rgb, foam, saturate( edge * 500 ) );
			m.Metalness *= 1-edge;

			m.Opacity += edge;
			m.Opacity *= saturate( depth / 0.1 );
		}

		return ShadingModelStandard::Shade( i, m );
	}
}
