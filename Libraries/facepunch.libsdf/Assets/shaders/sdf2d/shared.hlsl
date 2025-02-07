#ifndef SDF2D_SHARED_H
#define SDF2D_SHARED_H

#define STRING( A ) #A

#define CreateSdfLayerTexture( attribName ) \
    CreateTexture2D( g_t##attribName ) < \
        Attribute( #attribName ); \
        SrgbRead( false ); \
        Filter( BILINEAR ); \
        AddressU( CLAMP ); \
        AddressV( CLAMP ); \
    >; \
    float4 g_fl##attribName##_Params < \
        Default4( 0.0, 0.0, 1.0, 1.0 ); \
        Attribute( STRING( attribName##_Params ) ); \
    >

#define SdfLayerTex( attribName, positionOs ) \
    ((Tex2D( g_t##attribName, positionOs.xy * g_fl##attribName##_Params.z + g_fl##attribName##_Params.xx ) - 0.5) * g_fl##attribName##_Params.w)

#endif
