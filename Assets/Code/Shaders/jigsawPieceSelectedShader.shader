Shader "Custom/jigsawPieceSelectedShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _SelectColor ("SelectColor", Color) = (1,1,1,1)
        _SideColor ("Side Color", Color) = (0.937,0.345,0.129,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Outline ("Outline", Range(0, 20)) = 0.89
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
		Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        LOD 200
     
        Stencil
	    {
	        Ref 1
	        Comp always
	        Pass replace
	    }
           
        // Blend SrcAlpha OneMinusSrcAlpha
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert // alpha:fade
        #pragma target 3.0
		
        sampler2D _MainTex;
        
        struct Input
        {
            float2 uv_MainTex;
            float3 vertexNormal;
        };
		
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _SideColor;
        
        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)
		
        // Forward vertex normals to the surface shader
        void vert (inout appdata_full v, out Input o) {
              UNITY_INITIALIZE_OUTPUT(Input,o);
              o.vertexNormal = v.normal;
        }
        
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
		
            // Check if the normal is pointing upwards
            if (IN.vertexNormal.y > 0.5f)
            {
                o.Albedo = c.rgb;
            }
            else
            {
                // Apply the side color
                o.Albedo = _SideColor;
            }
            
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    	
		// Outline Pass
		Pass 
		{
		    Stencil
		    {
		        Ref 1
		        Comp notequal
		    }
		    ZWrite Off
		    ZTest Always
		    // ... your outline shader code here ...
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#pragma target 3.0
		
			struct VertexInput 
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};
		
			struct VertexOutput 
			{
				float4 pos : SV_POSITION;
			};
		
			uniform float _Outline;
			uniform float4 _SelectColor;
		
			VertexOutput vert (VertexInput v) 
			{
			    VertexOutput o;

			    // Get the object's center in world space
			    float4 objCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));

			    // Calculate distance from camera to object center
			    float dist = distance(_WorldSpaceCameraPos, objCenter.xyz) / _ScreenParams.g;

			    // Calculate scale factor based on distance and outline factor
			    float scaleFactor = 1.0f + (_Outline * dist);

				//
			    float4 pos = float4(v.vertex.xyz * scaleFactor, 1.0f);

			    // Transform to clip space
			    o.pos = UnityObjectToClipPos(pos);

			    return o;
			}
		
			float4 frag(VertexOutput i) : COLOR 
			{
				return fixed4(_SelectColor.rgb, 1);
			}
			ENDCG
			
			Cull Front
		}
    }
    FallBack "Diffuse"
}
