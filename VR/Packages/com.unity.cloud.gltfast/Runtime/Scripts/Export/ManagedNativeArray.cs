// SPDX-FileCopyrightText: 2024 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace GLTFast.Export
{

    /// <summary>
    /// Wraps a managed TIn[] in a NativeArray&lt;TOut&gt;without copying memory.
    /// </summary>
    /// <typeparam name="TIn">Type of items in the input array.</typeparam>
    /// <typeparam name="TOut">Type of items in output NativeArray (might differ from input type TIn).</typeparam>
    [Obsolete("This class is going to get sealed or removed from the public API in a future release.")]
    public class ManagedNativeArray<TIn, TOut> : IDisposable
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the managed NativeArray&lt;TOut&gt; and unpins the underlying managed array.
        /// </summary>
        /// <param name="disposing">Indicates whether the method call comes from a Dispose method (its value is true)
        /// or from a finalizer (its value is false).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && m_Pinned)
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
