Shader "ZigZag/GhostCar"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Original Material Color", Color) = (1, 1, 1, 1)
        _RimColor ("Neutral Rim", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.28
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _FaceAlpha ("Face Alpha Factor", Range(0, 1)) = 0.42
        _RimAlpha ("Rim Alpha Factor", Range(0, 2)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _RimColor;
            fixed _Opacity;
            fixed _RimStrength;
            fixed _RimPower;
            fixed _FaceAlpha;
            fixed _RimAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDirection : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.viewDirection = _WorldSpaceCameraPos.xyz - worldPosition;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, input.uv);
                float3 normal = normalize(input.worldNormal);
                float3 viewDirection = normalize(input.viewDirection);
                float rim = pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower);

                fixed3 color = textureColor.rgb * _Color.rgb;
                color = lerp(color, _RimColor.rgb, rim * _RimStrength);
                fixed alpha = saturate(_Opacity * (_FaceAlpha + rim * _RimAlpha) * textureColor.a);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
