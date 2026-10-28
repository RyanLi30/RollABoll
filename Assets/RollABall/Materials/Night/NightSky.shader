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
   float noise(float2 p) { float2 f=frac(p),c=floor(p);f=f*f*(3-2*f);return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y); }
   float fbm(float2 p) { float n=0,a=.5;for(int k=0;k<5;k++){n+=noise(p)*a;p=p*2.07+float2(9.2,3.7);a*=.5;}return n; }
   half4 frag(v2f i):SV_Target
   {
    float3 d=normalize(i.direction);
    float horizon=pow(1-saturate(abs(d.y)),4);
    float3 color=lerp(_Tint.rgb*.55,float3(.085,.135,.20),horizon);
    float2 sky=float2(atan2(d.x,d.z),asin(d.y));
    float2 cloudUV=sky*float2(3.8,8)+float2(_Time.y*.001,0);
    float cloud=fbm(cloudUV+fbm(cloudUV*.45)*1.6);
    float veil=smoothstep(.37,.71,cloud)*smoothstep(-.03,.22,d.y);
    color+=veil*float3(.026,.041,.068);
    color+=pow(saturate(1-abs(d.y-.12)*4),3)*fbm(sky*float2(2,14))*float3(.025,.033,.048);
    float2 uv=sky*145,cell=floor(uv),pt=frac(uv)-.5;
    float star=smoothstep(.065,0,length(pt))*step(.989,hash(cell))*smoothstep(.09,.42,d.y)*(1-veil*.85);
    color+=star*lerp(float3(.35,.45,.7),float3(.7,.58,.4),hash(cell+8));
    float3 moonDir=normalize(float3(.42,.36,1));
    float md=distance(d,moonDir);
    float disc=smoothstep(.051,.0495,md);
    float2 lunar=(d.xy-moonDir.xy)*37;
    float surface=.55+.35*fbm(lunar*3)+.1*noise(lunar*15);
    float crater=pow(saturate(noise(lunar*6)),3);
    color+=disc*surface*(1-crater*.32)*float3(.65,.73,.83);
    color+=exp(-md*23)*float3(.018,.033,.060)*(1-veil*.7);
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
