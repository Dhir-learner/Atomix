// SPDX-FileCopyrightText: 2024 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GLTFast.Schema
{
    /// <summary>
    /// Specifier for an accessor’s type
    /// </summary>
    /// <seealso href="https://www.khronos.org/registry/glTF/specs/2.0/glTF-2.0.html#accessor-data-types"/>
    public enum GltfAccessorAttributeType : byte
    {
        // Names are identical to glTF specified strings, that's why
        // inconsistent names are ignored.
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// Unknown/undefined type
        /// </summary>
        Undefined,

        /// <summary>
        /// Scalar. single value.
        /// </summary>
        SCALAR,
        /// <summary>
        /// Two component vector
        /// </summary>
        VEC2,
        /// <summary>
        /// Three component vector
        /// </summary>
        VEC3,
        /// <summary>
        /// Four component vector
        /// </summary>
        VEC4,
        /// <summary>
        /// 2x2 matrix (4 values)
        /// </summary>
        MAT2,
        /// <summary>
        /// 3x3 matrix (9 values)
        /// </summary>
        MAT3,
        /// <summary>
        /// 4x4 matrix (16 values)
        /// </summary>
        MAT4
        // ReSharper restore InconsistentNaming
    }
}
