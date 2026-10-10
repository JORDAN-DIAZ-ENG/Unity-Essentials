// Draws a renderer as a flat silhouette in _FlashColor. Used by Juice.Flash, which
// swaps it in for a renderer's own materials for the length of a hit flash.
//
// The pass carries no LightMode tag, so the built-in pipeline draws it as a normal
// pass and URP draws it as SRPDefaultUnlit: one shader covers both pipelines.
// Sprite alpha comes from _MainTex, which SpriteRenderer fills in itself; meshes
// see the white default and draw solid.
Shader "Hidden/Essential/Juice/Flash"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" }

        Pass
        {
            Cull Off
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _FlashColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed alpha : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.alpha = input.color.a;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, input.uv).a * input.alpha * _FlashColor.a;
                return fixed4(_FlashColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
