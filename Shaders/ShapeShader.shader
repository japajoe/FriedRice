Shader "FriedRice/ShapeShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Global Tint", Color) = (1,1,1,1)
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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
                fixed4 color : COLOR;
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
                float2 pixelPos = i.vertex.xy;
                
                // Flip Y component to convert from Unity's bottom-left to top-left coordinate space
                pixelPos.y = _ScreenParams.y - pixelPos.y;

                float clipWidth = _ClipRect.z;
                float clipHeight = _ClipRect.w;

                if (clipWidth > 0.0 && clipHeight > 0.0)
                {
                    float xMax = _ClipRect.x + _ClipRect.z;
                    float yMax = _ClipRect.y + _ClipRect.w;

                    if (pixelPos.x < _ClipRect.x || pixelPos.y < _ClipRect.y || 
                        pixelPos.x > xMax || pixelPos.y > yMax)
                    {
                        discard;
                    }
                }

                // Sample full RGBA color from the bound shape/white texture
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                // Combine vertex colors, texture channels, and global material tint
                return texColor * i.color * _Color;
            }
            ENDCG
        }
    }
}