Shader "UI/PreternaturalTVSignalGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captured Frame", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SignalTime ("Signal Time", Float) = 0
        _TearStrength ("Horizontal Tear", Range(0,0.1)) = 0.04
        _ChromaticOffset ("Chromatic Offset", Range(0,0.04)) = 0.012
        _StaticAlpha ("Static Alpha", Range(0,1)) = 0.35
        _DropoutAlpha ("Dropout Alpha", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _SignalTime;
            float _TearStrength;
            float _ChromaticOffset;
            float _StaticAlpha;
            float _DropoutAlpha;

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float frame = floor(_SignalTime * 24.0);
                float band = floor(input.uv.y * 28.0);
                float bandNoise = Hash21(float2(band, frame));
                float tearGate = step(0.62, bandNoise);
                float tear = (bandNoise * 2.0 - 1.0) * _TearStrength * tearGate;
                float wobble = sin(input.uv.y * 145.0 + _SignalTime * 31.0)
                    * _TearStrength * 0.08;

                float2 uv = input.uv + float2(tear + wobble, 0.0);
                float red = tex2D(_MainTex, uv + float2(_ChromaticOffset, 0.0)).r;
                float green = tex2D(_MainTex, uv).g;
                float blue = tex2D(_MainTex, uv - float2(_ChromaticOffset, 0.0)).b;
                float alpha = tex2D(_MainTex, uv).a;
                fixed3 signal = fixed3(red, green, blue);

                float grain = Hash21(floor(input.uv * float2(640.0, 360.0)) + frame);
                signal = lerp(signal, grain.xxx, _StaticAlpha);

                float scanline = 0.86 + 0.14 * sin(input.uv.y * 1080.0 * 3.14159265);
                signal *= scanline;

                float dropoutBand = step(0.94,
                    Hash21(float2(floor(input.uv.y * 55.0), floor(frame * 0.5) + 81.0)));
                signal *= 1.0 - dropoutBand * _DropoutAlpha;

                return fixed4(signal, alpha) * input.color;
            }
            ENDCG
        }
    }
}
