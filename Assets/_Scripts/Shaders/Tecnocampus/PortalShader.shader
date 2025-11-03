Shader "Tecnocampus/PortalShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaskTex ("Mask texture", 2D) = "white" {}
        _Cutout  ("Cutout", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque" }

        Lighting Off
        Cull Back
        ZWrite On
        ZTest Less
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex    = UnityObjectToClipPos(v.vertex);
                o.uv        = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float     _Cutout;

            fixed4 frag (v2f i) : SV_Target
            {
                // Máscara por UV (blanco visible, negro recortado)
                fixed4 maskCol = tex2D(_MaskTex, i.uv);
                if (maskCol.a < _Cutout)
                    clip(-1);

                // Muestreo de la RT por coordenadas de pantalla
                i.screenPos /= i.screenPos.w;
                fixed4 col = tex2D(_MainTex, float2(i.screenPos.x, i.screenPos.y));
                return col;
            }
            ENDCG
        }
    }
}