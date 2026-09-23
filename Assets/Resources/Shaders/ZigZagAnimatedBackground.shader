Shader "ZigZag/Animated Background"
{
    Properties
    {
        _FromTopColor ("Previous Top", Color) = (0.08, 0.03, 0.25, 1)
        _FromMiddleColor ("Previous Middle", Color) = (0.32, 0.08, 0.55, 1)
        _FromBottomColor ("Previous Bottom", Color) = (0.05, 0.02, 0.12, 1)
        _FromAccentColor ("Previous Accent", Color) = (0.05, 0.85, 1, 1)
        _TopColor ("Top Color", Color) = (0.08, 0.03, 0.25, 1)
        _MiddleColor ("Middle Color", Color) = (0.32, 0.08, 0.55, 1)
        _BottomColor ("Bottom Color", Color) = (0.05, 0.02, 0.12, 1)
        _AccentColor ("Accent Color", Color) = (0.05, 0.85, 1, 1)
        _PulseColor ("Event Pulse Color", Color) = (1, 1, 1, 1)
        _MotionSpeed ("Motion Speed", Range(0, 2)) = 0.28
        _Intensity ("Effect Intensity", Range(0, 1)) = 0.35
        _Pulse ("Event Pulse", Range(0, 1)) = 0
        _TransitionProgress ("Transition Progress", Range(0, 1)) = 1
        _TransitionStyle ("Transition Style", Range(0, 19)) = 0
        _TransitionSoftness ("Transition Softness", Range(0.01, 0.25)) = 0.13
        _TransitionCrossFade ("Transition Cross Fade", Range(0, 0.5)) = 0.24
        _BackgroundTime ("Animation Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            fixed4 _FromTopColor, _FromMiddleColor, _FromBottomColor, _FromAccentColor;
            fixed4 _TopColor, _MiddleColor, _BottomColor, _AccentColor, _PulseColor;
            half _MotionSpeed, _Intensity, _Pulse, _TransitionProgress, _TransitionStyle;
            half _TransitionSoftness, _TransitionCrossFade;
            float _BackgroundTime;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                output.screenPosition = ComputeScreenPos(output.position);
                return output;
            }

            half Hash21(half2 coordinates)
            {
                return frac(sin(dot(coordinates, half2(127.1h, 311.7h))) * 43758.5453h);
            }

            half3 PaletteColor(half3 direction, fixed3 top, fixed3 middle, fixed3 bottom,
                fixed3 accent, half time)
            {
                half height01 = saturate(direction.y * 0.5h + 0.5h);
                half longitude = atan2(direction.z, direction.x);
                half3 colour = lerp(bottom, middle, smoothstep(0.02h, 0.55h, height01));
                colour = lerp(colour, top, smoothstep(0.44h, 0.98h, height01));
                half ribbonCenterA = 0.52h + sin(longitude * 1.7h + time) * 0.075h;
                half ribbonCenterB = 0.35h + cos(longitude * 2.3h - time * 0.72h) * 0.055h;
                half ribbonA = pow(saturate(1.0h - abs(height01 - ribbonCenterA) * 8.0h), 3.0h);
                half ribbonB = pow(saturate(1.0h - abs(height01 - ribbonCenterB) * 10.0h), 3.0h);
                colour += accent * (ribbonA * 0.72h + ribbonB * 0.38h) * _Intensity * 0.24h;
                colour *= 0.94h + (sin(time * 0.65h + longitude) * 0.5h + 0.5h) * 0.06h;
                return colour;
            }

            half SmootherStep01(half value)
            {
                value = saturate(value);
                return value * value * value * (value * (value * 6.0h - 15.0h) + 10.0h);
            }

            half TransitionMask(half2 uv, half progress, half style, half time, half softness)
            {
                half2 centered = uv - 0.5h;
                half radius = saturate(length(centered) * 1.4142h);
                half angle = atan2(centered.y, centered.x) / 6.283185h + 0.5h;
                half threshold = 0.5h;
                progress = SmootherStep01(progress);
                softness = clamp(softness, 0.01h, 0.25h);
                half sweep = progress * (1.0h + softness * 2.0h) - softness;

                if (style < 0.5h) return smoothstep(0.0h, 1.0h, progress);
                if (style < 1.5h) threshold = uv.y;
                else if (style < 2.5h) threshold = 1.0h - uv.y;
                else if (style < 3.5h) threshold = uv.x;
                else if (style < 4.5h) threshold = 1.0h - uv.x;
                else if (style < 5.5h) threshold = radius;
                else if (style < 6.5h) threshold = 1.0h - radius;
                else if (style < 7.5h) threshold = (uv.x + uv.y) * 0.5h;
                else if (style < 8.5h) threshold = (uv.x + 1.0h - uv.y) * 0.5h;
                else if (style < 9.5h) threshold = angle;
                else if (style < 10.5h) threshold = saturate(uv.y + sin(uv.x * 8.0h + time * 1.4h) * 0.10h);
                else if (style < 11.5h) threshold = saturate(abs(uv.y - 0.5h) * 2.0h + sin(uv.x * 10.0h - time) * 0.07h);
                else if (style < 12.5h) threshold = saturate(radius * 0.68h + angle * 0.32h);
                else if (style < 13.5h) threshold = saturate(abs(centered.x) + abs(centered.y));
                else if (style < 14.5h) threshold = saturate(frac(uv.y * 7.0h) * 0.62h + floor(uv.y * 7.0h) / 7.0h * 0.38h);
                else if (style < 15.5h)
                {
                    half2 cell = floor(uv * 10.0h);
                    threshold = saturate((fmod(cell.x + cell.y, 2.0h) * 0.22h) + uv.y * 0.78h);
                }
                else if (style < 16.5h) threshold = Hash21(floor(uv * 18.0h));
                else if (style < 17.5h) threshold = saturate(uv.y + sin(uv.x * 14.0h + time * 2.0h) * 0.14h);
                else if (style < 18.5h) threshold = saturate(length(centered * half2(0.72h, 1.0h)) * 1.7h);
                else threshold = saturate(radius + sin(radius * 30.0h - time * 3.0h) * 0.085h);

                return smoothstep(threshold - softness, threshold + softness, sweep);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half3 direction = normalize(input.direction);
                half2 uv = input.screenPosition.xy / max(input.screenPosition.w, 0.0001h);
                half time = (half)_BackgroundTime * _MotionSpeed;
                half3 fromColour = PaletteColor(direction, _FromTopColor.rgb, _FromMiddleColor.rgb,
                    _FromBottomColor.rgb, _FromAccentColor.rgb, time);
                half3 toColour = PaletteColor(direction, _TopColor.rgb, _MiddleColor.rgb,
                    _BottomColor.rgb, _AccentColor.rgb, time);
                half easedProgress = SmootherStep01(_TransitionProgress);
                half spatialTransition = TransitionMask(uv, _TransitionProgress, _TransitionStyle, time,
                    _TransitionSoftness);
                half transition = lerp(spatialTransition, easedProgress, saturate(_TransitionCrossFade));
                half3 colour = lerp(fromColour, toColour, transition);

                half pulseGlow = pow(saturate(1.0h - length(uv - 0.5h) * 1.35h), 2.0h);
                colour += _PulseColor.rgb * pulseGlow * _Pulse * 0.11h;
                colour *= 1.0h + _Pulse * 0.055h;
                return fixed4(saturate(colour), 1.0h);
            }
            ENDCG
        }
    }

    Fallback Off
}
