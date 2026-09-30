// ============================================================
// Mercenaries/Outline
//
// Contorno de "casco invertido": la malla se infla hacia afuera por sus normales y se
// dibujan solo sus caras de ATRÁS (Cull Front). El cuerpo real tapa todo el centro, y
// lo único que se ve es el borde que sobresale: el contorno.
//
// El inflado va en METROS de mundo (_Width), no en unidades del modelo: así mide lo
// mismo aunque el .fbx venga escalado.
//
// Vive en una carpeta Resources para que entre en la build aunque ningún material lo
// use: DeadAllyHighlighter lo carga por nombre (Resources.Load("MercOutline")).
// ============================================================
Shader "Mercenaries/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1, 0.45, 1)
        _Width ("Width (m)", Float) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * _Width;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
