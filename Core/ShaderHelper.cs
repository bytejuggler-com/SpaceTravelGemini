using System.Numerics;
using Raylib_cs;

namespace SpaceTravel.Core;

public static class ShaderHelper
{
    public const string DefaultVertexShader = @"#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec4 vertexColor;

out vec2 fragTexCoord;
out vec4 fragColor;

uniform mat4 mvp;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
";

    public static Shader LoadFragmentShader(string fragmentSource)
    {
        return Raylib.LoadShaderFromMemory(null, fragmentSource);
    }

    public static void SetFloat(Shader shader, int loc, float value)
    {
        if (loc >= 0)
            Raylib.SetShaderValue(shader, loc, value, ShaderUniformDataType.Float);
    }

    public static void SetInt(Shader shader, int loc, int value)
    {
        if (loc >= 0)
            Raylib.SetShaderValue(shader, loc, value, ShaderUniformDataType.Int);
    }

    public static void SetVec2(Shader shader, int loc, Vector2 value)
    {
        if (loc >= 0)
            Raylib.SetShaderValue(shader, loc, value, ShaderUniformDataType.Vec2);
    }

    public static void SetVec3(Shader shader, int loc, Vector3 value)
    {
        if (loc >= 0)
            Raylib.SetShaderValue(shader, loc, value, ShaderUniformDataType.Vec3);
    }

    public static void SetVec4(Shader shader, int loc, Vector4 value)
    {
        if (loc >= 0)
            Raylib.SetShaderValue(shader, loc, value, ShaderUniformDataType.Vec4);
    }

    /// <summary>
    /// Draws a fullscreen quad covering the active render target so fragment shaders receive normalized fragTexCoord in [0, 1].
    /// </summary>
    public static void DrawFullscreenShaderQuad(Shader shader, Texture2D dummyTexture, int width, int height)
    {
        Raylib.BeginShaderMode(shader);
        Rectangle src = new(0, 0, dummyTexture.Width, dummyTexture.Height);
        Rectangle dst = new(0, 0, width, height);
        Raylib.DrawTexturePro(dummyTexture, src, dst, Vector2.Zero, 0f, Color.White);
        Raylib.EndShaderMode();
    }
}
