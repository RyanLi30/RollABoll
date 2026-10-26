Shader "RollABall/Night Sky"
{
 Properties { _Tint ("Sky tint", Color) = (0.03,0.045,0.085,1) }
 SubShader
 {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; };
   struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
   float4 _Tint;
   v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o; }
   float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   half4 frag(v2f i):SV_Target
   {
    float3 d=normalize(i.direction);
    float horizon=pow(1-saturate(abs(d.y)),5);
    float3 color=lerp(_Tint.rgb,float3(.065,.095,.16),horizon);
    float2 uv=float2(atan2(d.x,d.z),asin(d.y))*90;
    float2 cell=floor(uv);float2 pt=frac(uv)-.5;
    float star=smoothstep(.045,0,length(pt))*step(.984,hash(cell))*smoothstep(.05,.3,d.y);
    color+=star*float3(.35,.43,.62);
    float3 moonDir=normalize(float3(-.52,.55,1));
    float md=distance(d,moonDir);
    float moon=smoothstep(.066,.063,md);
    float surface=.6+.4*hash(floor(d.xy*150));
    color+=moon*float3(.32,.39,.53)*surface;
    color+=pow(saturate(dot(d,moonDir)),180)*float3(.025,.035,.06);
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
