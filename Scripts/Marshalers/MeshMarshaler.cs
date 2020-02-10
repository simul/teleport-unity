using System;
using System.Runtime.InteropServices;

namespace teleport
{
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
            int byteOffset = Marshal.SizeOf<Int64>();

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
            //4 64-bit ints, and 7 pointers.
            return 88;
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

            //Write primitive arrays.
            Marshal.WriteInt64(ptr, byteOffset, mesh.primitiveArrayAmount);
            byteOffset += Marshal.SizeOf(mesh.primitiveArrayAmount);

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.primitiveArrayAmount));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.primitiveArrayAmount; i++)
                {
                    Marshal.StructureToPtr(mesh.primitiveArrays[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.primitiveArrays[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            //Write accessors.
            Marshal.WriteInt64(ptr, byteOffset, mesh.accessorAmount);
            byteOffset += Marshal.SizeOf(mesh.accessorAmount);

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.accessorAmount));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.accessorAmount; i++)
                {
                    Marshal.StructureToPtr(mesh.accessorIDs[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.accessorIDs[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.accessorAmount));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.accessorAmount; i++)
                {
                    Marshal.StructureToPtr(mesh.accessors[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.accessors[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            //Write buffer views.
            Marshal.WriteInt64(ptr, byteOffset, mesh.bufferViewAmount);
            byteOffset += Marshal.SizeOf(mesh.bufferViewAmount);

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.bufferViewAmount));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.bufferViewAmount; i++)
                {
                    Marshal.StructureToPtr(mesh.bufferViewIDs[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.bufferViewIDs[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.bufferViewAmount));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.bufferViewAmount; i++)
                {
                    Marshal.StructureToPtr(mesh.bufferViews[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.bufferViews[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            //Write buffers.
            Marshal.WriteInt64(ptr, byteOffset, mesh.bufferAmount);
            byteOffset += Marshal.SizeOf(mesh.bufferAmount);

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.bufferAmount));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.bufferAmount; i++)
                {
                    Marshal.StructureToPtr(mesh.bufferIDs[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.bufferIDs[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            {
                IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.PrimitiveArray>() * mesh.bufferAmount));
                int arrayByteOffset = 0;
                for(int i = 0; i < mesh.bufferAmount; i++)
                {
                    Marshal.StructureToPtr(mesh.buffers[i], arrayPtr + arrayByteOffset, false);
                    arrayByteOffset += Marshal.SizeOf(mesh.buffers[i]);
                }
                Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
                byteOffset += Marshal.SizeOf<IntPtr>();
            }

            return ptr;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            throw new NotImplementedException();
        }
    }
}