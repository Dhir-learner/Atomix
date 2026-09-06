// SPDX-FileCopyrightText: 2025 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace GLTFast
{

    /// <summary>
    /// Wraps a managed TIn[] in a NativeArray&lt;TOut&gt;without copying memory.
    /// Use <see cref="ReadOnlyNativeArrayFromManagedArray{T}"/> for internal development instead.
    /// </summary>
    /// <typeparam name="TIn">Type of items in an input array.</typeparam>
    /// <typeparam name="TOut">Type of items in output NativeArray (might differ from input type TIn).</typeparam>
    [Obsolete("This is going to be removed from the public API in a future release. " +
        "For internal development, refer to ReadOnlyNativeArrayFromManagedArray<T>.")]
    public sealed class ManagedNativeArray<TIn, TOut> : IDisposable
        where TIn : unmanaged
        where TOut : unmanaged
    {

        NativeArray<TOut> m_NativeArray;
        GCHandle m_BufferHandle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_SafetyHandle;
#endif
        readonly bool m_Pinned;

        /// <summary>
        /// Wraps a managed TIn[] in a NativeArray&lt;TOut&gt;without copying memory.
        /// </summary>
        /// <param name="original">The original TIn[] to convert into a NativeArray&lt;TOut&gt;</param>
        public unsafe ManagedNativeArray(TIn[] original)
        {
            if (original != null)
            {
                m_BufferHandle = GCHandle.Alloc(original, GCHandleType.Pinned);
                fixed (void* bufferAddress = &original[0])
                {
                    m_NativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<TOut>(bufferAddress, original.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_SafetyHandle = AtomicSafetyHandle.Create();
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(array: ref m_NativeArray, m_SafetyHandle);
#endif
                }

                m_Pinned = true;
            }
            else
            {
                m_NativeArray = new NativeArray<TOut>();
            }
        }

        /// <summary>
        /// Points to the managed NativeArray&lt;TOut&gt;.
        /// </summary>
        public NativeArray<TOut> nativeArray => m_NativeArray;

        /// <summary>
        /// Disposes the managed NativeArray&lt;TOut&gt;.
        /// </summary>
        public void Dispose()
        {
            if (m_Pinned)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_SafetyHandle);
                AtomicSafetyHandle.Release(m_SafetyHandle);
#endif
                m_BufferHandle.Free();
            }
        }
    }
}
