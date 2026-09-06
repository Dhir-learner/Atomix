// SPDX-FileCopyrightText: 2023 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine;

namespace GLTFast.Export
{

    /// <summary>
    /// Provides the default material export
    /// </summary>
    public static class MaterialExport
    {

        static IMaterialExport s_MaterialExport;

        /// <summary>
        /// Provides the default material exporter
        /// </summary>
        /// <returns>Default material export</returns>
        /// <exception cref="InvalidOperationException">Is thrown when the default material export couldn't be determined based on the current render pipeline.</exception>
        public static IMaterialExport GetDefaultMaterialExport()
        {
            if (s_MaterialExport == null)
            {
                var renderPipeline = RenderPipelineUtils.RenderPipeline;

                switch (renderPipeline)
                {
                    case RenderPipeline.BuiltIn:
#if UNITY_SHADER_GRAPH && GLTFAST_BUILTIN_SHADER_GRAPH
                        s_MaterialExport = MetaMaterialExportShaderGraphs<
                            StandardMaterialExport,
                            GltfShaderGraphMaterialExporter
                        >.Instance;
#else
                        s_MaterialExport = MetaMaterialExportBuiltIn.Instance;
#endif
                        break;
                    case RenderPipeline.Universal:
// PATCHED: LitMaterialExport.cs is itself compiled only under "USING_URP || USING_HDRP", but this
// branch required only UNITY_SHADER_GRAPH. In a Built-in Render Pipeline project that also has
// Shader Graph installed - which this project is - the type does not exist and the whole package
// fails to compile. The guard now matches the one on the type's own file.
#if UNITY_SHADER_GRAPH && (USING_URP || USING_HDRP)
                        s_MaterialExport = MetaMaterialExportShaderGraphs<
                            LitMaterialExport,
                            GltfShaderGraphMaterialExporter
                        >.Instance;
#else
                        s_MaterialExport = MetaMaterialExportBuiltIn.Instance;
#endif
                        break;
#if USING_HDRP
                    case RenderPipeline.HighDefinition:
                        s_MaterialExport = MetaMaterialExportShaderGraphs<
                            HighDefinitionMaterialExport,
                            GltfHdrpMaterialExporter
                        >.Instance;
                        break;
#endif
                    default:
                        throw new InvalidOperationException($"Could not determine default MaterialExport (render pipeline {renderPipeline})");
                }

            }
            return s_MaterialExport;
        }

        /// <summary>
        /// Adds an ImageExport to the glTF.
        /// No conversions or channel swizzling
        /// </summary>
        /// <param name="gltf">glTF to add the image to.</param>
        /// <param name="imageExport">Texture generator to be added</param>
        /// <param name="textureId">Resulting texture index.</param>
        /// <returns>True if the texture was added, false otherwise.</returns>
        internal static bool AddImageExport(IGltfWritable gltf, ImageExportBase imageExport, out int textureId)
        {
            var imageId = gltf.AddImage(imageExport);
            if (imageId < 0)
            {
                textureId = -1;
                return false;
            }

            var samplerId = gltf.AddSampler(imageExport.FilterMode, imageExport.WrapModeU, imageExport.WrapModeV);
            textureId = gltf.AddTexture(imageId, samplerId);
            return true;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnLoad()
        {
            // Reset static state
            s_MaterialExport = null;
        }
#endif
    }
}
