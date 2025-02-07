#ifndef SDF3D_SHARED_H
#define SDF3D_SHARED_H

#define STRING( A ) #A

#define CreateSdfLayerTexture( attribName ) \
    CreateTexture3D( g_t##attribName ) < \
        Attribute( #attribName ); \
        SrgbRead( false ); \
        Filter( BILINEAR ); \
        AddressU( CLAMP ); \
        AddressV( CLAMP ); \
        AddressW( CLAMP ); \
    >; \
    float4 g_fl##attribName##_Params < \
        Default4( 0.0, 0.0, 1.0, 1.0 ); \
        Attribute( STRING( attribName##_Params ) ); \
    >

#define SdfLayerTex( attribName, positionOs ) \
    ((Tex3D( g_t##attribName, positionOs.xyz * g_fl##attribName##_Params.z + g_fl##attribName##_Params.xxx ) - 0.5) * g_fl##attribName##_Params.w)

#endif
