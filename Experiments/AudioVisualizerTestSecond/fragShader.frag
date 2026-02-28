#version 330 core

uniform float iTime;
uniform vec2 iResolution;
uniform float iRms;
uniform float iMidrange;

const float OFFSET_X = 0.0;
const float OFFSET_Y = 0.0;
const int MAX_ITER = 50;
const float PI = 3.14159265359;
const float ESCAPE_RADIUS_ZERO_OR_LESS_POWER = 5.0;
const float ESCAPE_RADIOUS_DEFAULT = 2.0;

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
    float magnification = (iResolution.y / 3.0) * pow(2.0, 0.0);
    float invMagnification = 1.0 / magnification;

    float im = ((gl_FragCoord.y - (iResolution.y / 2.0)) * -invMagnification) + OFFSET_Y;
    float re = ((gl_FragCoord.x - (iResolution.x / 2.0)) * invMagnification) + OFFSET_X;
//    float powerRe = 2.0;
//    float powerIm = 0.0;

    float speedFactor = 0.2 + iRms * 0.8;
    float powerRe = 10.0 * cos(iTime * 0.5 * speedFactor);
    float powerIm = sin(iTime * 0.50 * speedFactor);
    
    // when power >= 1, output 1 for LERP to output radius = 2 (B)
    // when power <= 0, output 0 for LERP to output radius = 5 (A)
    float powerToRadiusClamp = smoothstep(0.0, 1.0, powerRe);  
    float midrangeBias = + mix(iMidrange * 10, iMidrange, powerToRadiusClamp);
    float escapeRadius = mix(ESCAPE_RADIUS_ZERO_OR_LESS_POWER, ESCAPE_RADIOUS_DEFAULT, powerToRadiusClamp) + midrangeBias;

    vec2 z = vec2(re, im);
    float magnitude;
    int i;

    for (i = 0; i < MAX_ITER; i++) {
        magnitude = length(z);

        if (magnitude > escapeRadius) break;

        float rPolar = max(magnitude, 1e-10);

        float theta = atan2(z);
        float rReImPower = pow(rPolar, powerRe) * exp(-powerIm * theta);
        float pReImTheta = (powerRe * theta) + (powerIm * log(rPolar));

        float updatedZIm = (rReImPower * sin(pReImTheta)) + im;
        float updatedZRe = (rReImPower * cos(pReImTheta)) + re;

        z = vec2(updatedZRe, updatedZIm);
    }

    float invNormColorIter = (1 - (float(i) / float(MAX_ITER))) * smoothstep(0.0, 0.01, iRms);

    color = vec4(invNormColorIter, invNormColorIter, invNormColorIter, 1.0);
}