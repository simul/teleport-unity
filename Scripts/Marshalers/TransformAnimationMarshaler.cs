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
		//Free array of bone keyframes, that is located after name and array size.
		Marshal.FreeCoTaskMem(Marshal.ReadIntPtr(pNativeData, Marshal.SizeOf<IntPtr>() + Marshal.SizeOf<Int64>()));

		//Free animation.
		Marshal.FreeCoTaskMem(pNativeData);
	}

	public int GetNativeDataSize()
	{
		//24 = 8 + 8 + 8 == Marshal.SizeOf<IntPtr>() + Marshal.SizeOf<Int64>() + Marshal.SizeOf<IntPtr>()
		return 24;
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

		Marshal.WriteIntPtr(ptr, byteOffset, animation.name);
		byteOffset += Marshal.SizeOf<IntPtr>();

		Marshal.WriteIntPtr(ptr, byteOffset, animation.path);
		byteOffset += Marshal.SizeOf<IntPtr>();

		Marshal.WriteInt64(ptr, byteOffset, animation.boneAmount);
		byteOffset += Marshal.SizeOf<Int64>();

		IntPtr arrayPtr = Marshal.AllocCoTaskMem((int)(Marshal.SizeOf<avs.TransformKeyframeList>() * animation.boneAmount));
		int arrayByteOffset = 0;
		foreach(avs.TransformKeyframeList boneKeyframe in animation.boneKeyframes)
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
