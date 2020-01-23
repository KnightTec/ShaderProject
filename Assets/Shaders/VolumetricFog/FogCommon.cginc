
float henyeyGreensteinPhaseFunction(float3 pointPosition, float3 lightDirection, float3 camPos, float g) 
{
	float3 viewDir = normalize(pointPosition - camPos);
	float theta = dot(lightDirection, viewDir);
	float phase = 1 - g * g;
	phase /= 4 * pow(1 + g * g - 2 * g * theta, 1.5);
	return phase;
}
float rayleighPhaseFunction(float3 pointPosition, float3 lightDirection, float3 camPos)
{
	float3 viewDir = normalize(pointPosition - camPos);
	float theta = dot(lightDirection, viewDir);
	// 3/16
	return 0.1875 * (1 + theta * theta); 
}

// clamp to prevent overflow / underflow
#define EXP(arg) exp(clamp(arg, -50, 50))