Shader "Hidden/ResortEmpire/LockedBuildingGrayscale"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            half4 Frag(v2f_img input) : SV_Target
            {
                half4 color = tex2D(_MainTex, input.uv);
                half gray = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                return half4(gray, gray, gray, color.a);
            }
            ENDHLSL
        }
    }
}
