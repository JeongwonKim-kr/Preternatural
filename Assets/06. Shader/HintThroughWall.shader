Shader "Custom/HintThroughWall"
{
    Properties
    {
        _Color ("Hint Color", Color) = (0.35, 0.35, 0.35, 0.7)
        _Brightness ("Brightness", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "3D Through Wall"

            // Render only when the object's depth is
            // behind something already rendered.
            ZTest Greater

            ZWrite Off

            Cull Back

            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };

            fixed4 _Color;
            float _Brightness;


            // =====================================================
            // VERTEX
            // =====================================================

            v2f vert(appdata v)
            {
                v2f o;

                // Actual 3D object position
                o.vertex =
                    UnityObjectToClipPos(v.vertex);

                // Actual 3D world-space normal
                o.worldNormal =
                    UnityObjectToWorldNormal(v.normal);

                return o;
            }


            // =====================================================
            // FRAGMENT
            // =====================================================

            fixed4 frag(v2f i) : SV_Target
            {
                // Normalize the object's 3D normal.
                float3 normal =
                    normalize(i.worldNormal);

                // Main directional light.
                float3 lightDirection =
                    normalize(
                        _WorldSpaceLightPos0.xyz
                    );

                // Basic 3D lighting.
                float lighting =
                    max(
                        0.25,
                        dot(
                            normal,
                            lightDirection
                        )
                    );

                // Grey color with simple 3D shading.
                float3 finalColor =
                    _Color.rgb *
                    lighting *
                    _Brightness;

                return fixed4(
                    finalColor,
                    _Color.a
                );
            }

            ENDCG
        }
    }
}