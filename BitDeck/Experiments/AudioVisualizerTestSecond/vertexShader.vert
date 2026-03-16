#version 330 core

layout(location = 0) in vec4 position;
layout(location = 1) in vec2 inTexCoords;

out vec2 texCoords;

void main() 
{
    gl_Position = position;
    texCoords = inTexCoords;
}