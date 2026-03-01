#version 330 core

uniform float iTime;
uniform vec2 iResolution;
uniform float iRms;
uniform float iMidrange;
uniform float iTotalDuration;

const vec2 START_POS = vec2(-0.5, 0.0);
const vec2 END_POS = vec2(-0.11973195199391995, 0.6495285294768962);
const float TRANSITION_SECONDS = 10.0;
const float MIN_ITER = 30;
const float ITER_LIMIT = 2000;
const float LOG_2_ZOOM_CONSTANT = (ITER_LIMIT - MIN_ITER) / 16.0; 
const float PI = 3.14159265359;

//vec3 palette( float t ) {
//    vec3 a = vec3(0.5, 0.5, 0.5);
//    vec3 b = vec3(0.5, 0.5, 0.5);
//    vec3 c = vec3(1.0, 1.0, 1.0);
//    vec3 d = vec3(0.0, 0.0, 0.0);
//
//    return a + b*cos( 6.28318*(c*t+d) );
//}


// out vec4 color instead of layout(location = 0) out vec4 color because Avalonia doesn't support it
out vec4 color;

float signum(float num) { return float(num > 0) - float(num < 0); }

float atan2(vec2 theta) {
    const float HALF_PI = PI / 2.0;

    float signX = signum(theta.x);
    float signY = signum(theta.y);

    if (signX == 0 && signY == 0) return 0; // Technically undefined

    float baseAtan = atan(theta.y / theta.x);

    if (signX == 1) return baseAtan; // Quadrant I or IV
    else if (signX == -1) {
        return baseAtan += (signY >= 0) ? PI : -PI; // Quadrant II (+) or Quadrant IV (-)
    } else { // x == 0
        return (signY == 1) ? HALF_PI : -HALF_PI; // y == 0 case already handled earlier
    }
}

void main()
{
    float zoomDurationFactor = mix(0.0, 1.0, iTime / iTotalDuration);
    float zoomFactor = (zoomDurationFactor < 0.5) ? \
            mix(0.0, 16.0, smoothstep(0.0, 0.5, zoomDurationFactor)) : \
            mix(16.0, 0.0, smoothstep(0.5, 1.0, zoomDurationFactor));
    
    
    float magnification = (iResolution.y / 3.0) * pow(2.0, zoomFactor);
    float invMagnification = 1.0 / magnification;
    int maxIterations = int(floor(50 + (LOG_2_ZOOM_CONSTANT * zoomFactor)));
    
    float coordInterpolationFactor = smoothstep(0.0, 4.0, zoomFactor);
    vec2 currCoords = mix(START_POS, END_POS, coordInterpolationFactor);

    float im = ((gl_FragCoord.y - (iResolution.y / 2.0)) * -invMagnification) + currCoords.y;
    float re = ((gl_FragCoord.x - (iResolution.x / 2.0)) * invMagnification) + currCoords.x;
    
    float escapeRadius = 2.0 + (iMidrange * 0.3);

    vec2 z = vec2(0, 0);
    float magnitude;
    int i;

    for (i = 0; i < maxIterations; i++) {
        magnitude = length(z);

        if (magnitude > escapeRadius) {
            i++;
            break;
        }

        vec2 zSquared = pow(z, vec2(2.0, 2.0));
        float updatedZIm = (2.0 * z.x * z.y) + im;
        float updatedZRe = zSquared.x - zSquared.y + re;

        z = vec2(updatedZRe, updatedZIm);
    }

    const float VERY_DARK_GRAY_NORM = 20.0 / 255.0;
    float normIterations = VERY_DARK_GRAY_NORM + (float(i) / float(maxIterations));    
    vec3 iterationColor = (i == maxIterations) ? vec3(0.0, 0.0, 0.0) : vec3(normIterations, normIterations, normIterations);

    color = vec4(iterationColor, smoothstep(0.0, 0.01, iRms));
}