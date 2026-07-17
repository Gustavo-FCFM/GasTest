Shader "UI/AlwaysOnTop"
{
    // Shader de UI mínimo con ZTest Always: dibuja el gráfico ENCIMA de la
    // geometría 3D (a través de paredes). Lo usa UI_WorldHealthbar para el X-ray
    // de las barras de aliados (un canvas world-space normal se tapa con las
    // paredes; este material lo evita). Soporta color de vértice (para el tinte y
    // el fill de UI) y textura. No hace stencil/máscara — para nameplates alcanza.
    //
    // Si compilás un build, agregá este shader a Project Settings > Graphics >
    // Always Included Shaders (o usalo en algún material) para que no lo quiten.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "IgnoreProjector"  = "True"
            "RenderType"       = "Transparent"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas"= "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color    = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.texcoord) * i.color;
            }
            ENDCG
        }
    }
}
