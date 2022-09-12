using System;
using System.Runtime.InteropServices;

namespace teleport
{
	using uid = System.UInt64;
	/*
    * avs.Mesh contains C-Style arrays that themselves contain C-Style arrays, which the default marshaler doesn't like.
    * (Mesh.PrimitiveArray[].Attribute[] and Mesh.GeometryBuffer[].byte[]
    * 
    * This marshaler creates pointers for each C-Style array in avs.Mesh, and copies the data into that pointer using the default marshaler.
    */
	public class MeshMarshaler : ICustomMarshaler
    {
        private static MeshMarshaler staticInstance;

        public static ICustomMarshaler GetInstance(string cookie)
        {
            if(staticInstance == null)
            {
                staticInstance = new MeshMarshaler();
            }

            return staticInstance;
        }

        public void CleanUpManagedData(object ManagedObj)
        { }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            int byteOffset = Marshal.SizeOf<IntPtr>() + Marshal.SizeOf<IntPtr>() + Marshal.SizeOf<Int64>();

            //Free pointer for primitive arrays.
            Marshal.FreeCoTaskMem(Marshal.ReadIntPtr(pNativeData, byteOffset));

            //Free pointers for accessors, buffer views, and buffers.
            for(int i = 0; i < 3; i++)
            {
                byteOffset += Marshal.SizeOf<IntPtr>() + Marshal.SizeOf<Int64>();

                Marshal.FreeCoTaskMem(Marshal.ReadIntPtr(pNativeData, byteOffset));
                byteOffset += Marshal.SizeOf<IntPtr>();

                Marshal.FreeCoTaskMem(Marshal.ReadIntPtr(pNativeData, byteOffset));
            }

            //Free mesh itself.
            Marshal.FreeCoTaskMem(pNativeData);
        }

        public int GetNativeDataSize()
        {
            //4 64-bit ints(8), and 9 pointers(8).
            return (4*8+9*8);// 96;
        }

        public IntPtr MarshalManagedToNative(object ManagedObj)
        {
            avs.Mesh mesh = (avs.Mesh)ManagedObj;

            IntPtr ptr = Marshal.AllocCoTaskMem(GetNativeDataSize());
            if(ptr == IntPtr.Zero)
            {
                throw new Exception("Could not allocate memory.");
            }
			
            int byteOffset = 0;

			Marshal.WriteIntPtr(ptr, byteOffset, mesh.name);
			byteOffset += Marshal.SizeOf<IntPtr>();

			Marshal.WriteIntPtr(ptr, byteOffset, mesh.path);
			byteOffset += Marshal.SizeOf<IntPtr>();

			//Write primitive arrays.
			Marshal.WriteInt64(ptr, byteOffset, mesh.numPrimitiveArrays);
            byteOffset += Marshal.SizeOf(mesh.numPrimitiveArrays);
			
			{
				int primitiivearrays_alloc_size = (int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.numPrimitiveArrays);
				IntPtr arrayPtr = Marshal.AllocCoTaskMem(primitiivearrays_alloc_size);
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.numPrimitiveArrays; i++)
                {
                    Marshal.StructureToPtr(mesh.primitiveArrays[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.primitiveArrays[i]);
				}
				if (arrayByteOffset != primitiivearrays_alloc_size)
				{
					UnityEngine.Debug.LogError("arrayByteOffset != primitiivearrays_alloc_size.");
					throw new Exception("Incorrect data size.");
				}
				Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            //Write accessors.
            Marshal.WriteInt64(ptr, byteOffset, mesh.numAccessors);
            byteOffset += Marshal.SizeOf(mesh.numAccessors);

			{
				int accessors_alloc_size = (int)(Marshal.SizeOf<uid>() * mesh.numAccessors);
				IntPtr arrayPtr = Marshal.AllocCoTaskMem(accessors_alloc_size);
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.numAccessors; i++)
                {
                    Marshal.StructureToPtr(mesh.accessorIDs[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.accessorIDs[i]);
				}
				if (arrayByteOffset != accessors_alloc_size)
				{
					UnityEngine.Debug.LogError("arrayByteOffset != accessors_alloc_size.");
					throw new Exception("Incorrect data size.");
				}
				Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

			{
				int accessors_alloc_size = (int)(Marshal.SizeOf<avs.Accessor>() * mesh.numAccessors);
				IntPtr arrayPtr = Marshal.AllocCoTaskMem(accessors_alloc_size);
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.numAccessors; i++)
                {
                    Marshal.StructureToPtr(mesh.accessors[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.accessors[i]);
				}
				if (arrayByteOffset != accessors_alloc_size)
				{
					UnityEngine.Debug.LogError("arrayByteOffset != accessors_alloc_size.");
					throw new Exception("Incorrect data size.");
				}
				Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            //Write buffer views.
            Marshal.WriteInt64(ptr, byteOffset, mesh.numBufferViews);
            byteOffset += Marshal.SizeOf(mesh.numBufferViews);

			{
				int bufferview_alloc_size = (int)(Marshal.SizeOf<uid>() * mesh.numBufferViews);
				IntPtr arrayPtr = Marshal.AllocCoTaskMem(bufferview_alloc_size);
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.numBufferViews; i++)
                {
                    Marshal.StructureToPtr(mesh.bufferViewIDs[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.bufferViewIDs[i]);
				}
				if (arrayByteOffset != bufferview_alloc_size)
				{
					UnityEngine.Debug.LogError("arrayByteOffset != buffer_alloc_size.");
					throw new Exception("Incorrect data size.");
				}
				Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

			{
				int bufferview_alloc_size = (int)(Marshal.SizeOf<avs.BufferView>() * mesh.numBufferViews);
				IntPtr arrayPtr = Marshal.AllocCoTaskMem(bufferview_alloc_size);
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.numBufferViews; i++)
                {
                    Marshal.StructureToPtr(mesh.bufferViews[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.bufferViews[i]);
				}
				if (arrayByteOffset != bufferview_alloc_size)
				{
					UnityEngine.Debug.LogError("arrayByteOffset != buffer_alloc_size.");
					throw new Exception("Incorrect data size.");
				}
				Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            //Write buffers.
            Marshal.WriteInt64(ptr, byteOffset, mesh.numBuffers);
            byteOffset += Marshal.SizeOf(mesh.numBuffers);

            {
				int buffer_alloc_size= (int)(Marshal.SizeOf<uid>() * mesh.numBuffers);

				IntPtr arrayPtr = Marshal.AllocCoTaskMem(buffer_alloc_size);
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.numBuffers; i++)
                {
                    Marshal.StructureToPtr(mesh.bufferIDs[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.bufferIDs[i]);
				}
				if (arrayByteOffset != buffer_alloc_size)
				{
					UnityEngine.Debug.LogError("arrayByteOffset != buffer_alloc_size.");
					throw new Exception("Incorrect data size.");
				}
				Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.GeometryBuffer>() * mesh.numBuffers));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.numBuffers; i++)
                {
                    Marshal.StructureToPtr(mesh.buffers[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.buffers[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }
			if (byteOffset != GetNativeDataSize())
			{
				UnityEngine.Debug.LogError("byteOffset!=GetNativeDataSize.");
				throw new Exception("Incorrect data size.");
			}
            return ptr;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            throw new NotImplementedException();
        }
    }
}