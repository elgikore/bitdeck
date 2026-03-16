#version 330 core

in vec2 texCoords;
layout(location = 0) out vec4 color;

uniform sampler2D screenTex;

void main()
{
    color = texture(screenTex, texCoords);
}