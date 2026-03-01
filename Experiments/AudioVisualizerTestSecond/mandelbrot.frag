#version 330 core

uniform float iTime;
uniform vec2 iResolution;
uniform float iRms;
uniform float iMidrange;
uniform float iTotalDuration;

const vec2 START_POS_MANDELBROT = vec2(-0.5, 0.0);
const vec2 START_POS_JULIA = vec2(0.0, 0.0);

//const vec2 END_POS = vec2(-0.11973195199391995, 0.6495285294768962);
//const vec2 END_POS = vec2(-0.7329251850304637, -0.2161912471140397);
const vec2 END_POS = vec2(-0.6964746125319, -0.356793628703);

const float MIN_ITER = 150;
const float ITER_LIMIT = 2000;
const float PI = 3.14159265359;
const float START_MORPH = 0.49;
const float END_MORPH = 0.51;


// out vec4 color instead of layout(location = 0) out vec4 color because Avalonia doesn't support it
out vec4 color;

void main()
{
    float zoomDurationFactor = mix(0.0, 1.0, iTime / iTotalDuration);
    float zoomFactor = (zoomDurationFactor < 0.5) ? \
            mix(0.0, 16.0, smoothstep(0.0, 0.5, zoomDurationFactor)) : \
            mix(16.0, 0.0, smoothstep(0.5, 1.0, zoomDurationFactor));
    
    
    float magnification = (iResolution.y / 3.0) * pow(2.0, zoomFactor);
    float invMagnification = 1.0 / magnification;
    int maxIterations = int(floor(mix(MIN_ITER, ITER_LIMIT, smoothstep(0.0, 16.0, zoomFactor))));
    
    float coordInterpolationFactor = smoothstep(0.0, 4.0, zoomFactor);
    vec2 currCoords = (zoomDurationFactor < 0.5) ? \
            mix(START_POS_MANDELBROT, END_POS, coordInterpolationFactor) : \
            mix(START_POS_JULIA, END_POS, coordInterpolationFactor);

    float im = ((gl_FragCoord.y - (iResolution.y / 2.0)) * -invMagnification) + currCoords.y;
    float re = ((gl_FragCoord.x - (iResolution.x / 2.0)) * invMagnification) + currCoords.x;
    
    float escapeRadius = 2.0 + iMidrange;

    vec2 z0Mandelbrot = vec2(0.0, 0.0);
    vec2 z0Julia = vec2(re, im);
    vec2 cMandelbrot = vec2(re, im);
    vec2 cJulia = END_POS;
    vec4 toWhiteFlash = vec4(0.0);
    
    vec2 z;
    vec2 c;
    
    if (zoomDurationFactor < START_MORPH) {
        z = z0Mandelbrot;
        c = cMandelbrot;
    } else if (zoomDurationFactor >= START_MORPH && zoomDurationFactor < END_MORPH){
        // Animating z is very flickery so we only animate c and some "practical effects"
        float cTransition = smoothstep(START_MORPH, END_MORPH, zoomDurationFactor);
        toWhiteFlash = vec4(cTransition);
        
        c = mix(cMandelbrot, cJulia, cTransition);
    } else {
        // Fade out to Julia at first
        float morphDuration = END_MORPH - START_MORPH;
        float cTransition = mix(1.0, 0.0, smoothstep(END_MORPH, END_MORPH + morphDuration, zoomDurationFactor));
        toWhiteFlash = vec4(cTransition);
        
        z = z0Julia;
        c = cJulia;
    }
    
    float magnitude;
    int i;

    for (i = 0; i < maxIterations; i++) {
        magnitude = length(z);

        if (magnitude > escapeRadius) {
            i++;
            break;
        }

        vec2 zSquared = pow(z, vec2(2.0, 2.0));
        float updatedZIm = (2.0 * z.x * z.y) + c.y;
        float updatedZRe = zSquared.x - zSquared.y + c.x;

        z = vec2(updatedZRe, updatedZIm);
    }

    const float VERY_DARK_GRAY_NORM = 20.0 / 255.0;
    float normIterations = VERY_DARK_GRAY_NORM + (float(i) / float(maxIterations));    
    vec3 iterationColor = (i == maxIterations) ? vec3(0.0, 0.0, 0.0) : vec3(normIterations, normIterations, normIterations);

    vec4 finalColor = clamp(vec4(iterationColor, smoothstep(0.0, 0.01, iRms)) + toWhiteFlash, 0.0, 1.0);
    
    color = finalColor;
}