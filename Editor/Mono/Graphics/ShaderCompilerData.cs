// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using System.Runtime.InteropServices;

namespace UnityEditor.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    [UsedByNativeCode]
    public struct ShaderSnippetData
    {
        private ShaderType m_ShaderType;
        private PassType m_PassType;
        private string m_PassName;
        private PassIdentifier m_PassIdentifier;

        public ShaderType shaderType
        {
            get { return m_ShaderType; }
        }

        public PassType passType
        {
            get { return m_PassType; }
        }

        public string passName
        {
            get { return m_PassName; }
        }

        public PassIdentifier pass { get { return m_PassIdentifier; } }
    }

    [StructLayout(LayoutKind.Sequential)]
    [UsedByNativeCode]
    public struct ShaderCompilerData
    {
        public ShaderKeywordSet shaderKeywordSet;
        public PlatformKeywordSet platformKeywordSet; // C++ side is PlatformCapKeywords
        private ShaderRequirements m_ShaderRequirements;
        private GraphicsTier m_GraphicsTier;
        private ShaderCompilerPlatform m_ShaderCompilerPlatform;

        public ShaderRequirements shaderRequirements
        {
            get { return m_ShaderRequirements; }
        }

        public GraphicsTier graphicsTier
        {
            get { return m_GraphicsTier; }
        }

        public ShaderCompilerPlatform shaderCompilerPlatform
        {
            get { return m_ShaderCompilerPlatform; }
        }
    }

    public enum ShaderCompilerPlatform
    {
        None            = 0, // For non initialized variable.
        D3D             = 4, // Direct3D 11 (FL10.0 and up) and Direct3D 12, compiled with MS D3DCompiler
        GLES20          = 5, // OpenGL ES 2.0 / WebGL 1.0, compiled with MS D3DCompiler + HLSLcc
        GLES3x          = 9, // OpenGL ES 3.0+ / WebGL 2.0, compiled with MS D3DCompiler + HLSLcc
        PS4             = 11, // Sony PS4
        XboxOneD3D11    = 12, // MS XboxOne
        Metal           = 14, // Apple Metal, compiled with MS D3DCompiler + HLSLcc
        OpenGLCore      = 15, // Desktop OpenGL 3+, compiled with MS D3DCompiler + HLSLcc
        Vulkan          = 18, // Vulkan SPIR-V, compiled with MS D3DCompiler + HLSLcc
        Switch          = 19, // Nintendo Switch (NVN)
        XboxOneD3D12    = 20, // Xbox One D3D12
        GameCore        = 21, // Game Core
        PS5             = 23, // PS5
        PS5NGGC         = 24  // PS5 NGGC
    }

    public enum ShaderCompilerMessageSeverity
    {
        Error,
        Warning
    }

    [Flags]
    public enum ShaderRequirements : long
    {
        None                        = 0,
        BaseShaders                 = (1 << 0), // Basic "can have shaders" (SM2.0 level) capability
        Interpolators10             = (1 << 1), // 10 interpolators/varyings
        Interpolators32             = (1 << 2), // 32 interpolators/varyings
        MRT4                        = (1 << 3), // Multiple render targets, at least 4 (ability for fragment shader to output up to 4 colors)
        MRT8                        = (1 << 4), // Multiple render targets, at least 8 (ability for fragment shader to output up to 8 colors)
        Derivatives                 = (1 << 5), // Derivative (ddx/ddy) instructions in the fragment shader
        SampleLOD                   = (1 << 6), // Ability to sample textures in fragment shader with explicit LOD level
        FragCoord                   = (1 << 7), // Pixel position (VPOS/SV_Position/gl_FragCoord) input in fragment shader
        FragClipDepth               = (1 << 8), // Pixel depth (SV_Position.zw/gl_FragCoord.zw) input in fragment shader
        Interpolators15Integers     = (1 << 9), // Integers + Interpolators15. We bundle them together since extremely unlikely a GPU/API will ever exist that only has part of that.
        Texture2DArray              = (1 << 10), // 2DArray textures
        Instancing                  = (1 << 11), // SV_InstanceID shader input
        Geometry                    = (1 << 12), // Geometry shaders
        CubeArray                   = (1 << 13), // Cubemap arrays
        Compute                     = (1 << 14), // Compute shaders
        RandomWrite                 = (1 << 15), // Random-write textures (UAVs) from shader stages
        TessellationCompute         = (1 << 16), // Tessellator hardware, i.e. Metal style
        TessellationShaders         = (1 << 17), // Tessellation shaders, i.e. DX11 style (hull/domain shader stages)
        SparseTexelResident         = (1 << 18), // Sparse textures with sampling instructions that return residency info
        FramebufferFetch            = (1 << 19), // Framebuffer fetch (ability to have in+out fragment shader color params)
        MSAATextureSamples          = (1 << 20), // Access to MSAA'd texture samples in shaders (e.g. HLSL Texture2DMS)
        SetRTArrayIndexFromAnyShader = (1 << 21) // Must support setting the render target array index from any shader and not just the geometry shader
    }

    public enum ShaderType
    {
        Vertex      = 1,
        Fragment    = 2,
        Geometry    = 3,
        Hull        = 4,
        Domain      = 5,
        Surface     = 6,
        RayTracing  = 7,
        Count       = 7
    }
}
