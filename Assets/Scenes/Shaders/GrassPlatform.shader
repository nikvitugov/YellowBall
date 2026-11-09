// Трава для платформ-спрайтов (квадрат, растянутый масштабом).
// Верхняя грань платформы (та, что смотрит вверх — работает и для наклонных, и для повёрнутых)
// получает полосу травы, травинки торчат над краем; ниже — земля.
// Всё в мировых единицах: трава не растягивается, сколько бы ни был растянут квадрат,
// и стыкуется между соседними платформами. Коллайдеры и сцена не меняются.
Shader "YellowLight/GrassPlatform"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (не используется)", 2D) = "white" {}
        _GrassTex ("Полоса травы (RGBA)", 2D) = "white" {}
        _SoilTex ("Земля", 2D) = "gray" {}
        _StripHeight ("Высота полосы травы, юниты", Float) = 0.75
        _Overhang ("Доля полосы над краем платформы", Range(0, 1)) = 0.33
        _TileWidth ("Ширина повтора травы, юниты", Float) = 2.34
        _SoilTileWidth ("Ширина повтора земли, юниты", Float) = 2.34
        _SoilAspect ("Пропорции текстуры земли (ширина/высота)", Float) = 5.85
        _SoilDarken ("Затемнение земли с глубиной", Range(0, 1)) = 0.35
    }
    SubShader
    {
        // DisableBatching: шейдеру нужна матрица каждой платформы
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

            sampler2D _GrassTex;
            sampler2D _SoilTex;
            float _StripHeight, _Overhang, _TileWidth, _SoilTileWidth, _SoilAspect, _SoilDarken;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos   : SV_POSITION;
                float  depth : TEXCOORD0;   // глубина под верхней гранью, юниты (минус — над гранью)
                float  along : TEXCOORD1;   // координата вдоль грани, юниты (общая для всех платформ)
            };

            v2f vert (appdata v)
            {
                // оси объекта в мире (с масштабом)
                float3 ax = mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0));
                float3 ay = mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0));

                // какая из 4 граней смотрит вверх сильнее всех
                // сравниваем направления, а не длины осей: иначе у длинной наклонной платформы «верхом» оказался бы торец
                float3 dx = normalize(ax), dy = normalize(ay);
                float4 up = float4(dx.y, -dx.y, dy.y, -dy.y);
                float2 nLocal; float3 nAxis; float3 tAxis;
                if (up.x >= up.y && up.x >= up.z && up.x >= up.w) { nLocal = float2( 1, 0); nAxis =  ax; tAxis = ay; }
                else if (up.y >= up.z && up.y >= up.w)            { nLocal = float2(-1, 0); nAxis = -ax; tAxis = ay; }
                else if (up.z >= up.w)                            { nLocal = float2(0,  1); nAxis =  ay; tAxis = ax; }
                else                                              { nLocal = float2(0, -1); nAxis = -ay; tAxis = ax; }

                float lenN = length(nAxis);
                float3 n = nAxis / max(lenN, 1e-5);
                float3 t = normalize(tAxis);
                if (t.x < 0) t = -t;                         // трава не должна идти «задом наперёд»

                float s = dot(v.vertex.xy, nLocal);          // -0.5 (низ) .. 0.5 (верх)
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float overhang = _Overhang * _StripHeight;

                v2f o;
                o.depth = (0.5 - s) * lenN;
                if (s > 0.25)                                // вершины верхней грани поднимаем над краем
                {
                    wp += n * overhang;
                    o.depth -= overhang;
                }
                o.along = dot(wp, t);
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float overhang = _Overhang * _StripHeight;

                // земля (только внутри платформы); текстура бесшовная, просто повторяем
                float soilH = _SoilTileWidth / _SoilAspect;
                fixed3 soil = tex2D(_SoilTex, float2(i.along / _SoilTileWidth, -max(i.depth, 0) / soilH)).rgb;
                soil *= lerp(1, 1 - _SoilDarken, saturate(i.depth / 3));
                fixed4 c = fixed4(soil, i.depth >= 0 ? 1 : 0);

                // полоса травы поверх
                float v = 1 - (i.depth + overhang) / _StripHeight;
                fixed4 g = tex2D(_GrassTex, float2(i.along / _TileWidth, saturate(v)));
                g.a *= (v >= 0 && v <= 1) ? 1 : 0;
                c.rgb = lerp(c.rgb, g.rgb, g.a);
                c.a = max(c.a, g.a);

                clip(c.a - 0.01);
                return c;
            }
            ENDCG
        }
    }
}
