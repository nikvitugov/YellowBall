// Текстура, уложенная в мировых единицах: на растянутом квадрате-платформе она не растягивается,
// а повторяется; на наклонной платформе поворачивается вместе с ней (доски идут вдоль платформы).
// Работает и для спрайтов (SpriteRenderer), и для 3D-объектов. Без освещения, как обычные спрайты.
Shader "YellowBall/WorldTiled"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (не используется)", 2D) = "white" {}
        _Tex ("Текстура", 2D) = "white" {}
        _Color ("Оттенок", Color) = (1, 1, 1, 1)
        _TileSize ("Ширина повтора текстуры, юниты", Float) = 2
        [Toggle] _AlongObject ("Поворачивать вместе с объектом", Float) = 1
        [Toggle] _UseSpriteColor ("Подкрашивать цветом спрайта", Float) = 0
    }
    SubShader
    {
        // DisableBatching: нужна матрица каждого объекта (иначе спрайты склеиваются и поворот теряется)
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True"
               "DisableBatching" = "True" "CanUseSpriteAtlas" = "False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Tex;
            float4 _Tex_TexelSize;      // (1/ширина, 1/высота, ширина, высота)
            fixed4 _Color;
            float _TileSize, _AlongObject, _UseSpriteColor;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;

                // направления «вдоль» и «вверх»: мировые оси или оси объекта (та, что ближе к горизонтали, — «вдоль»)
                float2 along = float2(1, 0);
                if (_AlongObject > 0.5)
                {
                    float2 dx = normalize(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)).xy);
                    float2 dy = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)).xy);
                    along = abs(dx.x) >= abs(dy.x) ? dx : dy;
                    if (along.x < 0) along = -along;
                }
                float2 up = float2(-along.y, along.x);

                float aspect = _Tex_TexelSize.z / _Tex_TexelSize.w;   // ширина/высота текстуры
                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.uv = float2(dot(wp.xy, along) / _TileSize, dot(wp.xy, up) / _TileSize * aspect);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // цвет спрайта (SpriteRenderer.color) — по желанию: у платформ в сцене он свой (зелёный/коричневый)
                fixed4 tint = _UseSpriteColor > 0.5 ? i.color : fixed4(1, 1, 1, i.color.a);
                return tex2D(_Tex, i.uv) * _Color * tint;
            }
            ENDCG
        }
    }
}
