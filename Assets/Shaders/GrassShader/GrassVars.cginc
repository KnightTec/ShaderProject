// Tessellation Options
float _TessFactor;
float _MaxDistance;

// Grass Appearance
float  _Ambient;
float4 _GrassColor;
float4 _DryGrassColor;
float4 _GrassBottomColor;
float4 _DryBottomColor;
float _GrassColorOffset;
sampler2D _DryGrassTex;
float4 _DryGrassTex_ST;

// Grass Geometry
sampler2D _HeightTex;
float4 _HeightTex_ST;
float _GrassCutOff;
float _MinGrassHeight;
float _MaxGrassHeight;
float _MaxGrassWidth;

// Wind
sampler2D _WindTex;
float4 _WindTex_ST;
float _WindSpeed;
float _WindDepth;