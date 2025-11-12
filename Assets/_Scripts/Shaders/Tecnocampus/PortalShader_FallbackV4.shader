Shader "Tecnocampus/PortalShader_FallbackV4"
{
    Properties
    {
        _MainTex ("Other Portal RT", 2D) = "black" {}
        _MaskTex ("Mask texture", 2D) = "white" {}
        _Cutout  ("Cutout", Range(0.0, 1.0)) = 0.5

        _LinkedPortalValid ("Linked Portal Valid (0|1)", Float) = 0.0

        _FallbackTex ("Fallback Noise", 2D) = "gray" {}
        _FallbackColor ("Fallback Tint", Color) = (0.20, 0.55, 1.0, 1.0)
        _FallbackSpeed ("Fallback Scroll Speed", Float) = 0.8
        _FallbackIntensity ("Fallback Intensity", Range(0,2)) = 1.0

        _RotationSpeed ("Rotation Speed", Range(-20,20)) = 3.0

        _Open        ("Open Amount", Range(0,1)) = 0.0
        _Link        ("Link Amount", Range(0,1)) = 0.0
        _EdgeFeather ("Edge Feather", Range(0.001,0.25)) = 0.06
        _EdgeBoost   ("Edge Boost", Range(0,5)) = 1.0
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
            float  _RotationSpeed;

            float  _Open;
            float  _Link;
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

            float2 rotate2D(float2 p, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(c*p.x - s*p.y, s*p.x + c*p.y);
            }

            fixed4 FallbackEffect(float2 uv)
            {
                float t = _Time.y;

                float2 c = uv * 2.0 - 1.0;
                float ang = t * _RotationSpeed;
                float2 rc = rotate2D(c, ang);
                float2 suv = rc * 0.5 + 0.5;

                float2 uv1 = suv * 1.35 + float2(  t * _FallbackSpeed, -t * _FallbackSpeed * 0.7);
                float2 uv2 = rotate2D(suv, 0.5) * 1.8 + float2(-t * _FallbackSpeed * 0.45,  t * _FallbackSpeed * 0.35);

                fixed3 a = tex2D(_FallbackTex, uv1).rgb;
                fixed3 b = tex2D(_FallbackTex, uv2).rgb;
                float mixv = 0.5 + 0.5 * sin(t * 1.73);
                fixed3 col = lerp(a, b, mixv);

                return fixed4(col * _FallbackColor.rgb * _FallbackIntensity, 1.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 maskCol = tex2D(_MaskTex, i.uv);
                if (maskCol.a < _Cutout) clip(-1);

                float2 centered = i.uv * 2.0 - 1.0;
                float r = length(centered);

                // Outer aperture
                float openMask = smoothstep(_Open, _Open - _EdgeFeather, r);
                if (openMask <= 0.001) clip(-1);

                // Inner link aperture
                float linkMask = smoothstep(_Link, _Link - _EdgeFeather, r);

                fixed4 fb = FallbackEffect(i.uv);
                float2 uvRT = (i.screenPos.xy / i.screenPos.w);
                fixed4 rt = tex2D(_MainTex, uvRT);

                float gate = step(0.5, _LinkedPortalValid);
                float showRT = linkMask * gate;

                fixed4 col = lerp(fb, rt, showRT);

                float ringOpen = 1.0 - saturate((r - _Open) / max(_EdgeFeather * 0.5, 0.001));
                float ringLink = 1.0 - saturate((r - _Link) / max(_EdgeFeather * 0.5, 0.001));
                col.rgb += _FallbackColor.rgb * (ringOpen + ringLink) * _EdgeBoost * 0.12;

                col.rgb *= openMask;

                return col;
            }
            ENDCG
        }
    }
}
