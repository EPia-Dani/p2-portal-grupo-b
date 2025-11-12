Shader "Tecnocampus/PortalShader_Fallback"
{
    Properties
    {
        _MainTex ("Other Portal RT", 2D) = "black" {}
        _MaskTex ("Mask texture", 2D) = "white" {}
        _Cutout  ("Cutout", Range(0.0, 1.0)) = 0.5

        // Fallback shown when there is no linked portal / no RT
        _LinkedPortalValid ("Linked Portal Valid (0|1)", Float) = 0.0
        _FallbackTex ("Fallback Base (noise/flow)", 2D) = "gray" {}
        _FallbackColor ("Fallback Tint", Color) = (0.20, 0.55, 1.0, 1.0) // Portal-ish
        _FallbackSpeed ("Fallback Speed", Float) = 0.5
        _FallbackIntensity ("Fallback Intensity", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque" }
        Lighting Off
        Cull Back
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _FallbackTex;

            float4 _MainTex_TexelSize;     // not used, but kept for completeness
            float4 _MaskTex_TexelSize;
            float4 _FallbackTex_TexelSize;

            float  _Cutout;
            float  _LinkedPortalValid;
            float4 _FallbackColor;
            float  _FallbackSpeed;
            float  _FallbackIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv        : TEXCOORD0;
                float4 pos       : SV_POSITION;
                float4 screenPos : TEXCOORD1; // for RT sampling
                float3 viewPos   : TEXCOORD2; // for radial falloff
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                // view-space position to compute distance from center
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                float4 view  = mul(UNITY_MATRIX_V, world);
                o.viewPos = view.xyz;
                return o;
            }

            // Simple 2x layered panner + radial mask to evoke Portal's idle surface
            fixed4 FallbackEffect(float2 uv, float3 viewPos)
            {
                float t = _Time.y * _FallbackSpeed;

                // Two pans with slight rotation
                float2 uv1 = uv * 1.3 + float2( t, -t);
                float2 uv2 = mul(float2x2(0.866, -0.5, 0.5, 0.866), uv * 1.8) + float2(-t*0.6, t*0.4);

                fixed3 a = tex2D(_FallbackTex, uv1).rgb;
                fixed3 b = tex2D(_FallbackTex, uv2).rgb;

                // Interference
                float swirl = saturate( (dot(a,b) + a.r + b.g) * 0.5 );
                fixed3 col = lerp(a, b, 0.5 + 0.5*sin(t*1.7)) * (0.6 + 0.4*swirl);

                // Radial falloff from center (screen-space approximate using view depth)
                // Use mesh UV as portal-local coords: assume portal quad has UV 0..1
                float2 centered = uv * 2.0 - 1.0;      // -1..1
                float  r = length(centered);
                float  edge = smoothstep(1.0, 0.6, r); // strong towards edges

                fixed3 finalRGB = col * _FallbackColor.rgb * _FallbackIntensity * edge;
                return fixed4(finalRGB, 1.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1) Cut by mask
                fixed4 maskCol = tex2D(_MaskTex, i.uv);
                if (maskCol.a < _Cutout)
                    clip(-1);

                // 2) Choose source
                if (_LinkedPortalValid >= 0.5)
                {
                    // Sample other portal RT by screen coords
                    float2 uvRT = (i.screenPos.xy / i.screenPos.w);
                    return tex2D(_MainTex, uvRT);
                }
                else
                {
                    return FallbackEffect(i.uv, i.viewPos);
                }
            }
            ENDCG
        }
    }
}
