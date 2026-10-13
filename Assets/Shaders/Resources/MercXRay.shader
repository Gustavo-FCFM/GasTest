// ============================================================
// Mercenaries/XRay
//
// Silueta de un personaje DETRÁS de las paredes: se dibuja solo donde algo lo tapa
// (ZTest Greater) y no escribe profundidad, así que lo que está a la vista se ve normal y
// lo que está tapado se ve como una mancha de color. La usa TeamReveal (la Marca del
// cazador del Explorador).
//
// El Offset la acerca un pelito a la cámara: sin eso, la cara de adelante del cuerpo y la
// de la silueta empatan en profundidad y el color parpadea encima del personaje visible.
//
// Vive en Resources para entrar en la build aunque ningún material lo use: TeamReveal lo
// carga por nombre (Resources.Load("MercXRay")).
// ============================================================
Shader "Mercenaries/XRay"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.35, 0.2, 0.55)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+50" }

        Pass
        {
            Name "XRay"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite Off
            ZTest Greater
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
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
