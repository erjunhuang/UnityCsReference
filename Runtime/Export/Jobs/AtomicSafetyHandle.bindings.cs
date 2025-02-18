// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Text;
using System.Diagnostics;
using UnityEngine.Bindings;
using UnityEngine.Scripting;
using Unity.Jobs;

namespace Unity.Collections.LowLevel.Unsafe
{
    public enum EnforceJobResult
    {
        AllJobsAlreadySynced = 0,
        DidSyncRunningJobs = 1,
        HandleWasAlreadyDeallocated = 2,
    }

    public enum AtomicSafetyErrorType
    {
        Deallocated = 0,         // access on main thread after deallocation
        DeallocatedFromJob = 1,  // access from job after deallocation
        NotAllocatedFromJob = 2, // Access from job prior to assignment
    }

    // AtomicSafetyHandle is used by the C# job system to provide validation and full safety
    // for read / write permissions to access the buffers represented by each handle.
    // Each AtomicSafetyHandle represents a single container.
    // Since all Native containers are written using structs,
    // it also provides checks against destroying a container
    // and accessing from another struct pointing to the same buffer.

    [UsedByNativeCode]
    [NativeHeader("Runtime/Jobs/AtomicSafetyHandle.h")]
    [NativeHeader("Runtime/Jobs/JobsDebugger.h")]
    public struct AtomicSafetyHandle
    {
        internal const int Read = 1 << 0;
        internal const int Write = 1 << 1;
        internal const int Dispose = 1 << 2;

        internal const int ReadCheck = ~(Write | Dispose);
        internal const int WriteCheck = ~(Read | Dispose);
        internal const int DisposeCheck = ~(Read | Write);
        internal const int ReadWriteDisposeCheck = ~(Read | Write | Dispose);

        [NativeDisableUnsafePtrRestriction]
        internal IntPtr versionNode;
        internal int version;
        internal int staticSafetyId;

        // Creates a new AtomicSafetyHandle that is valid until Release is called.
        [ThreadSafe]
        public static extern AtomicSafetyHandle Create();

        [ThreadSafe]
        public static extern AtomicSafetyHandle GetTempUnsafePtrSliceHandle();

        [ThreadSafe]
        public static extern AtomicSafetyHandle GetTempMemoryHandle();
        [ThreadSafe]
        public static extern bool IsTempMemoryHandle(AtomicSafetyHandle handle);

        // Releases a previously Created AtomicSafetyHandle.
        // You must call CheckDeallocateAndThrow before calling Release.
        [ThreadSafe]
        public static extern void Release(AtomicSafetyHandle handle);

        [ThreadSafe]
        public static extern bool IsDefaultValue(in AtomicSafetyHandle handle);

        // Marks the AtomicSafetyHandle so that it cannot be disposed of.
        [ThreadSafe]
        public static extern void PrepareUndisposable(ref AtomicSafetyHandle handle);

        // Switches the AtomicSafetyHandle to the secondary version number.
        [ThreadSafe]
        public static extern void UseSecondaryVersion(ref AtomicSafetyHandle handle);

        // Switches the AtomicSafetyHandle to the secondary version number.
        [ThreadSafe]
        public static extern void SetAllowSecondaryVersionWriting(AtomicSafetyHandle handle, bool allowWriting);

        [ThreadSafe]
        public static extern void SetBumpSecondaryVersionOnScheduleWrite(AtomicSafetyHandle handle, bool value);

        [NativeMethod(IsThreadSafe = true, IsFreeFunction = true)]
        public static extern void SetAllowReadOrWriteAccess(AtomicSafetyHandle handle, bool allowReadWriteAccess);

        [NativeMethod(IsThreadSafe = true, IsFreeFunction = true)]
        public static extern bool GetAllowReadOrWriteAccess(AtomicSafetyHandle handle);


        // Performs CheckWriteAndThrow and then bumps the secondary version.
        // This allows for example a NativeArray that becomes invalid if the Length of a List
        // is changed to be invalidated, while the NativeList handle itself remains valid.
        [ThreadSafe(ThrowsException = true)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static extern void CheckWriteAndBumpSecondaryVersion(AtomicSafetyHandle handle);

        // For debugging purposes in unit tests we need to sometimes just sync
        // all jobs against an handle before shutting down.
        [ThreadSafe]
        public static extern EnforceJobResult EnforceAllBufferJobsHaveCompleted(AtomicSafetyHandle handle);

        // Waits for all jobs running against this AtomicSafetyHandle to complete,
        // then releases the atomic safetyhandle.
        // e.g. You are forced to delete some memory right now,
        // irregardless of any potential jobs that are still running.
        // (It is up to you to still give an error message.)
        // This is unusual behaviour, normally we would simply throw an exception
        // and thus simply not perform the Disposing.
        // But sometimes you just need to delete memory right away, for example Mesh.EndWriteVertices().
        [ThreadSafe]
        public static extern EnforceJobResult EnforceAllBufferJobsHaveCompletedAndRelease(AtomicSafetyHandle handle);

        // Waits for all jobs running against this AtomicSafetyHandle to complete,
        // Then marks the atomic safety handle to no longer be readable or writable.
        // Thus the only thing you can do with it is dispose it.
        [ThreadSafe]
        public static extern EnforceJobResult EnforceAllBufferJobsHaveCompletedAndDisableReadWrite(AtomicSafetyHandle handle);

        // Same as CheckReadAndThrow but the early out has already been performed in the call site for performance reasons.
        [ThreadSafe(ThrowsException = true)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static extern void CheckReadAndThrowNoEarlyOut(AtomicSafetyHandle handle);

        // Same as CheckWriteAndThrow but the early out has already been performed in the call site for performance reasons.
        [ThreadSafe(ThrowsException = true)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static extern void CheckWriteAndThrowNoEarlyOut(AtomicSafetyHandle handle);

        // Checks if the handle can be deallocated.
        // If not (already destroyed, job currently accessing the data) throws an exception.
        [ThreadSafe(ThrowsException = true)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static extern void CheckDeallocateAndThrow(AtomicSafetyHandle handle);

        [ThreadSafe(ThrowsException = true)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static extern void CheckGetSecondaryDataPointerAndThrow(AtomicSafetyHandle handle);

        [ThreadSafe]
        public static unsafe extern int GetReaderArray(AtomicSafetyHandle handle, int maxCount, IntPtr output);

        [ThreadSafe]
        public static extern JobHandle GetWriter(AtomicSafetyHandle handle);

        // Checks if the handle can be read from
        // If not (already destroyed, job currently writing to the data) throws an exception.
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static unsafe void CheckReadAndThrow(AtomicSafetyHandle handle)
        {
            var versionPtr = (int*)handle.versionNode;
            if (handle.version != ((*versionPtr) & ReadCheck))
                CheckReadAndThrowNoEarlyOut(handle);
        }

        // Checks if the handle can be written to
        // If not (already destroyed, job currently reading or writing to the data) throws an exception.
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static unsafe void CheckWriteAndThrow(AtomicSafetyHandle handle)
        {
            var versionPtr = (int*)handle.versionNode;
            if (handle.version != ((*versionPtr) & WriteCheck))
                CheckWriteAndThrowNoEarlyOut(handle);
        }

        // When the handle is of a non-default value, checks whether it is still valid
        // If not (already destroyed) throws an exception.
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static unsafe void ValidateNonDefaultHandle(in AtomicSafetyHandle handle)
        {
            if (!IsDefaultValue(handle))
            {
                CheckExistsAndThrow(handle);
            }
        }

        // When the handle is of a non-default value, checks whether it is still valid
        // If not valid, return false
        public static unsafe bool IsValidNonDefaultHandle(in AtomicSafetyHandle handle)
        {
            if (!IsDefaultValue(handle))
            {
                return IsHandleValid(handle);
            }

            return false;
        }


        // Checks if the handle is still valid.
        // If not (already destroyed) throws an exception.
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static unsafe void CheckExistsAndThrow(in AtomicSafetyHandle handle)
        {
            var versionPtr = (int*)handle.versionNode;
            if (handle.version != ((*versionPtr) & ReadWriteDisposeCheck))
                throw new ObjectDisposedException("The NativeArray has been disposed, it is not allowed to access it");
        }

        // Checks if the handle is still valid and returns true if it is, false otherwise
        public static unsafe bool IsHandleValid(in AtomicSafetyHandle handle)
        {
            var versionPtr = (int*)handle.versionNode;
            if (handle.version != ((*versionPtr) & ReadWriteDisposeCheck))
                return false;

            return true;
        }


        [ThreadSafe]
        public static extern string GetReaderName(AtomicSafetyHandle handle, int readerIndex);

        [ThreadSafe]
        public static extern string GetWriterName(AtomicSafetyHandle handle);

        [ThreadSafe]
        public static unsafe extern int NewStaticSafetyId(byte* ownerTypeNameBytes, int byteCount);

        public static unsafe int NewStaticSafetyId<T>()
        {
            var ownerTypeName = typeof(T).ToString();
            var bytes = Encoding.UTF8.GetBytes(ownerTypeName);
            fixed(byte* pBytes = bytes)
            {
                return NewStaticSafetyId(pBytes, bytes.Length);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [NativeThrows, ThreadSafe]
        public static unsafe extern void SetCustomErrorMessage(int staticSafetyId, AtomicSafetyErrorType errorType, byte* messageBytes, int byteCount);
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static unsafe void SetStaticSafetyId(ref AtomicSafetyHandle handle, int staticSafetyId)
        {
            handle.staticSafetyId = staticSafetyId;
        }
    }
}

