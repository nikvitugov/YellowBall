// Полупрозрачная копия модели для шлейфа PlayerMotionBlur.
// Без освещения, без записи глубины: сам игрок (непрозрачный) перекрывает копии,
// поэтому размытие видно только позади него.
Shader "YellowLight/MotionTrail"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.92, 0.45, 0.5)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed _Cutoff;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - _Cutoff); // та же «вырезка», что у материала героя (Cutout)
                fixed4 c = tex * _Color;
                c.a = _Color.a;
                return c;
            }
            ENDCG
        }
    }
}
