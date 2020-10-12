using System;
using System.Runtime.InteropServices;

public class TransformAnimationMarshaler : ICustomMarshaler
{
	private static TransformAnimationMarshaler staticInstance;

	public static ICustomMarshaler GetInstance(string cookie)
	{
		if(staticInstance == null) staticInstance = new TransformAnimationMarshaler();
		return staticInstance;
	}

	public void CleanUpManagedData(object managedObj)
	{ }

	public void CleanUpNativeData(IntPtr pNativeData)
	{
		//Free array.
		Marshal.FreeCoTaskMem(Marshal.ReadIntPtr(pNativeData, Marshal.SizeOf<Int64>()));

		//Free animation.
		Marshal.FreeCoTaskMem(pNativeData);
	}

	public int GetNativeDataSize()
	{
		//16 = 8 + 8 == Marshal.SizeOf<Int64>() + Marshal.SizeOf<IntPtr>()
		return 16;
	}

	public IntPtr MarshalManagedToNative(object managedObj)
	{
		avs.TransformAnimation animation = (avs.TransformAnimation)managedObj;

		IntPtr ptr = Marshal.AllocCoTaskMem(GetNativeDataSize());
		if(ptr == IntPtr.Zero)
		{
			throw new Exception("Could not allocate memory.");
		}

		int byteOffset = 0;

		Marshal.WriteInt64(ptr, animation.boneAmount);
		byteOffset += Marshal.SizeOf(animation.boneAmount);

		IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.TransformKeyframe>() * animation.boneAmount));
		int arrayByteOffset = 0;
		foreach(avs.TransformKeyframe boneKeyframe in animation.boneKeyframes)
		{
			Marshal.StructureToPtr(boneKeyframe, arrayPtr + arrayByteOffset, false);
			arrayByteOffset += Marshal.SizeOf(boneKeyframe);
		}
		Marshal.WriteIntPtr(ptr, byteOffset, arrayPtr);
		byteOffset += Marshal.SizeOf<IntPtr>();

		return ptr;
	}

	public object MarshalNativeToManaged(IntPtr pNativeData)
	{
		throw new NotImplementedException();
	}
}
