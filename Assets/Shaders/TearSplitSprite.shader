Shader "Free/Tear Split Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Side ("Side", Float) = -1
        _Split ("Split", Range(0,1)) = 0.5
        _EdgeWidth ("Edge Width", Range(0,0.2)) = 0.035
        _EdgeColor ("Edge Color", Color) = (0.55,0.02,0.02,1)
        _Alpha ("Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Side;
            float _Split;
            float _EdgeWidth;
            fixed4 _EdgeColor;
            float _Alpha;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;
                float leftMask = step(input.texcoord.x, _Split);
                float rightMask = step(_Split, input.texcoord.x);
                float mask = _Side < 0 ? leftMask : rightMask;
                clip(mask - 0.001);

                float edge = 1.0 - smoothstep(0.0, max(_EdgeWidth, 0.0001), abs(input.texcoord.x - _Split));
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);
                color.a *= _Alpha;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
