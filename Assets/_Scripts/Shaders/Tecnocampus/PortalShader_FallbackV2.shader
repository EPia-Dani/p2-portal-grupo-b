Shader "Tecnocampus/PortalShader_FallbackV2"
{
    Properties
    {
        _MainTex ("Other Portal RT", 2D) = "black" {}
        _MaskTex ("Mask texture", 2D) = "white" {}
        _Cutout  ("Cutout", Range(0.0, 1.0)) = 0.5

        _LinkedPortalValid ("Linked Portal Valid (0|1)", Float) = 0.0

        // Fallback visuals
        _FallbackTex ("Fallback Noise", 2D) = "gray" {}
        _FallbackColor ("Fallback Tint", Color) = (0.20, 0.55, 1.0, 1.0)
        _FallbackSpeed ("Fallback Speed", Float) = 0.8
        _FallbackIntensity ("Fallback Intensity", Range(0,2)) = 1.0

        // Swirl + aperture controls
        _SwirlStrength ("Swirl Strength", Range(0,3)) = 1.2
        _SwirlSpeed    ("Swirl Speed",    Range(0,10)) = 3.0
        _Open          ("Open Amount",    Range(0,1))  = 0.0   // 0 closed, 1 fully open
        _EdgeFeather   ("Open Feather",   Range(0.001,0.2)) = 0.05
        _EdgeBoost     ("Edge Boost",     Range(0,5))  = 1.0
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

            float  _Cutout;
            float  _LinkedPortalValid;

            float4 _FallbackColor;
            float  _FallbackSpeed;
            float  _FallbackIntensity;

            float  _SwirlStrength;
            float  _SwirlSpeed;
            float  _Open;
            float  _EdgeFeather;
            float  _EdgeBoost;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv        : TEXCOORD0;
                float4 pos       : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            float2 rotate(float2 p, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(c*p.x - s*p.y, s*p.x + c*p.y);
            }

            fixed4 FallbackEffect(float2 uv)
            {
                // Centered coords -1..1
                float2 c = uv * 2.0 - 1.0;
                float r = length(c);
                float t = _Time.y;

                // Swirl angle reduces toward the edge to avoid UV tearing
                float swirlAngle = _SwirlStrength * (1.0 - saturate(r)) * (0.6 + 0.4*sin(t * _SwirlSpeed));
                float2 cuv = rotate(c, swirlAngle);

                // Back to 0..1 after swirl
                float2 suv = cuv * 0.5 + 0.5;

                // Layered flow
                float2 uv1 = suv * 1.35 + float2(  t * _FallbackSpeed, -t * _FallbackSpeed * 0.7);
                float2 uv2 = rotate(suv, 0.5) * 1.8 + float2(-t * _FallbackSpeed * 0.45,  t * _FallbackSpeed * 0.35);

                fixed3 a = tex2D(_FallbackTex, uv1).rgb;
                fixed3 b = tex2D(_FallbackTex, uv2).rgb;
                float mixv = 0.5 + 0.5 * sin(t * 1.73);
                fixed3 col = lerp(a, b, mixv);

                // Radial emphasis and tint
                float edge = smoothstep(1.2, 0.0, r);        // more intense near center
                fixed3 finalRGB = col * _FallbackColor.rgb * _FallbackIntensity * edge;

                // Add a subtle edge ring during opening
                float ring = saturate((r - _Open) / max(_EdgeFeather * 0.5, 0.001));
                ring = 1.0 - ring; // peak at aperture edge
                finalRGB += _FallbackColor.rgb * ring * _EdgeBoost * 0.15;

                return fixed4(finalRGB, 1.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1) Mask by UV
                fixed4 maskCol = tex2D(_MaskTex, i.uv);
                if (maskCol.a < _Cutout) clip(-1);

                // 2) If linked portal exists, sample RT
                if (_LinkedPortalValid >= 0.5)
                {
                    float2 uvRT = (i.screenPos.xy / i.screenPos.w);
                    return tex2D(_MainTex, uvRT);
                }

                // 3) Fallback with opening from center
                // Aperture: show only inside radius = _Open, with feather
                float2 centered = i.uv * 2.0 - 1.0;
                float r = length(centered);
                float a = smoothstep(_Open + _EdgeFeather, _Open, r); // 1 inside, 0 outside

                // Clip outside for a clean hole
                if (a <= 0.001) clip(-1);

                fixed4 fb = FallbackEffect(i.uv);

                // Feather the edge
                fb.rgb *= a;

                return fb;
            }
            ENDCG
        }
    }
}
