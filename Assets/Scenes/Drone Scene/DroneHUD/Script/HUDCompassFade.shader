Shader "UI/HUDCompassFade_Fixed"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScrollX ("Scroll X", Float) = 0
        _ViewWidth ("View Width", Range(0.01, 1)) = 1
        _FadeWidth ("Fade Width", Range(0, 0.5)) = 0.15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _ScrollX;
            float _FadeWidth;
            float _ViewWidth; // <--- Added this variable

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. MASK: Calculated using the screen UVs (0 to 1)
                // This ensures the fade stays at the edges of the UI element
                float leftFade = smoothstep(0.0, _FadeWidth, i.uv.x);
                float rightFade = smoothstep(1.0, 1.0 - _FadeWidth, i.uv.x);
                float mask = leftFade * rightFade;

                // 2. TEXTURE: Calculated using the View Width
                // We multiply by _ViewWidth to "zoom in" or "squash" the texture
                float2 textureUV = i.uv;
                textureUV.x = (textureUV.x * _ViewWidth) + _ScrollX;

                // 3. Combine
                fixed4 col = tex2D(_MainTex, textureUV);
                col.a *= mask;

                return col;
            }
            ENDCG
        }
    }
}