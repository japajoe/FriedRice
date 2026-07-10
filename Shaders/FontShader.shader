Shader "FriedRice/FontShader"
{
    Properties
    {
        _MainTex ("Font Atlas (R8)", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
                fixed4 color    : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect; // x: xMin, y: yMin, z: xMax, w: yMax

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convert projections to absolute screen space pixel coordinates
                float2 pixelPos = i.screenPos.xy / i.screenPos.w;
                pixelPos.x *= _ScreenParams.x;
                
                // Invert the pixel Y position because Unity's ScreenPos starts at the bottom-left (0,0), 
                pixelPos.y = _ScreenParams.y - (pixelPos.y * _ScreenParams.y);

                float clipWidth = _ClipRect.z - _ClipRect.x;
                float clipHeight = _ClipRect.w - _ClipRect.y;

                if (clipWidth > 0.0 && clipHeight > 0.0)
                {
                    if (pixelPos.x < _ClipRect.x || pixelPos.y < _ClipRect.y || 
                        pixelPos.x > _ClipRect.z || pixelPos.y > _ClipRect.w)
                    {
                        discard;
                    }
                }

                // Sample the single channel byte
                fixed alpha = tex2D(_MainTex, i.uv).r;
                return fixed4(i.color.rgb, alpha * i.color.a);
            }
            ENDCG
        }
    }
}