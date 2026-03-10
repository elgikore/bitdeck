#version 400 core
#extension GL_ARB_gpu_shader_fp64 : require

layout(location = 0) out vec4 color;

uniform float iTime;
uniform vec2 iResolution;
uniform float iRms;
uniform float iMidrange;
uniform float iTotalDuration;

const dvec2 START_POS_MANDELBROT = dvec2(-0.5, 0.0);
const dvec2 START_POS_JULIA = dvec2(0.0, 0.0);

//const dvec2 END_POS = dvec2(-0.11973198284749387, 0.6495285447896464);
const dvec2 END_POS = dvec2(-0.7329625513044067, -0.21616389160711513);
//const dvec2 END_POS = dvec2(-0.6964746125319, -0.356793628703);
//const dvec2 END_POS = dvec2(-0.76006598207706, -0.08049577394589);
//const dvec2 END_POS = dvec2(-0.76290645735314, -0.08273390579021);
//const dvec2 END_POS = dvec2(-0.03347354159771, -0.77327276891843);

const int MIN_ITER = 200;
const int ITER_LIMIT = 2600;
const float PI = 3.14159265359;
const float START_MORPH = 0.495;
const float END_MORPH = 0.505;


vec3 dmgColorPaletteFromIdx(int idx) {
    const vec3 DMG_PALETTE_LUT[4] = vec3[4](
        vec3(8.0 / 255.0, 24.0 / 255.0, 32.0 / 255.0),
        vec3(52.0 / 255.0, 104.0 / 255.0, 86.0 / 255.0),
        vec3(136.0 / 255.0, 192.0 / 255.0, 112.0 / 255.0),
        vec3(224.0 / 255.0, 248.0 / 255.0, 208.0 / 255.0)
    );

    return DMG_PALETTE_LUT[int(idx)];
}

float bayer4x4(vec2 pixelCoord) {
    int x = int(pixelCoord.x) & 3; // & 3 == % 4 (bitwise works for powers of 2)
    int y = int(pixelCoord.y) & 3;
    int idx = y * 4 + x; // 2d -> 1d coord. (4 means that there are 4 cols. in 4x4)

    const float BAYER_4X4_MATRIX[16] = float[16](
        // Normalized to 16.0
        0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
        12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
        3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
        15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0
    );

    return BAYER_4X4_MATRIX[idx];
}

// https://dev.thi.ng/gradients/
vec3 cyclingDmgColor(int iter, int maxIter, vec2 fragCoord) {
    const vec3 A = vec3(0.28, 0.28, 0.28);
    const vec3 B = vec3(0.633, 0.633, 0.633);
    const vec3 C = vec3(2.0, 2.0, 2.0);
    const vec3 D = vec3(-0.672, -0.338, 0.0);
    const vec3 GRAYSCALE_CONST = vec3(0.299, 0.587, 0.114);
    const float VERY_DARK_GRAY = 30.0 / 255.0; // So that it doesn't fall to inner color
    const float VERY_LIGHT_GRAY = 240.0 / 255.0; // So that it isn't too bright
    const int LUT_LENGTH = 4;
    
    float normIteration = float(iter) / float(maxIter);
    vec3 cycler = vec3(iTime * 0.8);
    vec3 colored = vec3(A) + vec3(B) * cos(2 * PI * (C * normIteration + D) + cycler);
    float grayscale = clamp(dot(colored, GRAYSCALE_CONST), VERY_DARK_GRAY, VERY_LIGHT_GRAY);
    
    // Bayer dithering https://spencerszabados.github.io/blog/2022/bayer-dithering/
    // It uses 255 * formula to scale back from normalized (c-1) to represent the discrete grayscale (255 colors)
    // but since we use 4 colors, 3 * formula => 3 and /(4-1) (c=4) cancels out
    float bayerValueFromCoord = bayer4x4(fragCoord);

    // pixel(x,y) in the original is normalized color, but we get the color from normalized iteration, so it still works
    // also we are using GB palette, it makes things easier
    float ditherValue = bayerValueFromCoord + ((LUT_LENGTH - 1) * (grayscale));
    int lutIdx = int(floor(ditherValue));
    
    return (iter == maxIter) ? dmgColorPaletteFromIdx(0) : dmgColorPaletteFromIdx(lutIdx);
}



void main()
{
    float zoomDurationFactor = mix(0.0, 1.0, iTime / iTotalDuration);
    float zoomFactor = (zoomDurationFactor < 0.5) ? \
            mix(0.0, 40.0, smoothstep(0.0, 0.5, zoomDurationFactor)) : \
            mix(40.0, 0.0, smoothstep(0.5, 1.0, zoomDurationFactor));
    
    float magnification = (iResolution.y / 3.0) * pow(2.0, zoomFactor);
    float invMagnification = 1.0 / magnification;
    int maxIterations = int(floor(mix(MIN_ITER, ITER_LIMIT, smoothstep(0.0, 16.0, zoomFactor))));
    
    float coordInterpolationFactor = smoothstep(0.0, 4.0, zoomFactor);
    dvec2 currCoords = (zoomDurationFactor < 0.5) ? \
            mix(START_POS_MANDELBROT, END_POS, coordInterpolationFactor) : \
            mix(START_POS_JULIA, END_POS, coordInterpolationFactor);

    double im = ((gl_FragCoord.y - (iResolution.y / 2.0)) * -invMagnification) + currCoords.y;
    double re = ((gl_FragCoord.x - (iResolution.x / 2.0)) * invMagnification) + currCoords.x;
    
    double escapeRadius = 4.0 + (4.0 * iMidrange);

    dvec2 z0Mandelbrot = dvec2(0.0, 0.0);
    dvec2 z0Julia = dvec2(re, im);
    dvec2 cMandelbrot = dvec2(re, im);
    dvec2 cJulia = END_POS;
    dvec4 toWhiteFlash = dvec4(0.0);
    float cTransition = 0.0;
    
    dvec2 z;
    dvec2 c;
    
    if (zoomDurationFactor < START_MORPH) {
        z = z0Mandelbrot;
        c = cMandelbrot;
    } else if (zoomDurationFactor >= START_MORPH && zoomDurationFactor < END_MORPH){
        // Animating z is very flickery so we only animate c and some "practical effects"
        // 0.50 instead of END_MORPH so that it can hold the flash for a bit longer
        cTransition = smoothstep(START_MORPH, 0.50, zoomDurationFactor);
        
        c = mix(cMandelbrot, cJulia, cTransition);
    } else {
        // Fade out to Julia at first
        float morphDuration = END_MORPH - START_MORPH;
        cTransition = mix(1.0, 0.0, smoothstep(END_MORPH, END_MORPH + morphDuration, zoomDurationFactor));
        
        z = z0Julia;
        c = cJulia;
    }
    
    double magnitude;
    int i;

    for (i = 0; i < maxIterations; i++) {
        magnitude = dot(z, z);

        if (magnitude > escapeRadius) {
            i++;
            break;
        }

        dvec2 zSquared = z * z;
        double updatedZIm = (2.0 * z.x * z.y) + c.y;
        double updatedZRe = zSquared.x - zSquared.y + c.x;

        z = dvec2(updatedZRe, updatedZIm);
    }
    
    vec3 iterationColor = cyclingDmgColor(i, maxIterations, gl_FragCoord.xy);
    vec4 finalColor = mix(vec4(iterationColor, smoothstep(0.0, 0.01, iRms)), vec4(dmgColorPaletteFromIdx(3), 1.0), cTransition);
    
    color = vec4(finalColor);
}