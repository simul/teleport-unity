using System;
using System.Runtime.InteropServices;
using UnityEngine;
using uid = System.UInt64;

namespace avs
{
	public enum LogSeverity
	{
		Never = 0,
		Debug,
		Info,
		Warning,
		Error,
		Critical,
		Num_LogSeverity
	}

	public enum AxesStandard
	{
		NotInitialised = 0,
		RightHanded = 1,
		LeftHanded = 2,
		YVertical = 4,
		EngineeringStyle = 8 | RightHanded,
		GlStyle = 16 | RightHanded,
		UnrealStyle = 32 | LeftHanded,
		UnityStyle = 64 | LeftHanded | YVertical,
	}

	public enum RateControlMode
	{
		RC_CONSTQP = 0, /*< Constant QP mode */
		RC_VBR = 1, /*< Variable bitrate mode */
		RC_CBR = 2, /*< Constant bitrate mode */
		RC_CBR_LOWDELAY_HQ = 3, /*< low-delay CBR, high quality */
		RC_CBR_HQ = 4, /*< CBR, high quality (slower) */
		RC_VBR_HQ = 5 /*< VBR, high quality (slower) */
	};

	public enum VideoCodec
	{
		Any = 0,
		Invalid = 0,
		H264, /*!< H264 */
		HEVC /*!< HEVC (H265) */
	};

	public enum AudioCodec
	{
		Any = 0,
		Invalid = 0,
		PCM
	};
	public static class DataTypes
	{
		private static float copysign(float sizeval, float signval)
		{
			return Mathf.Sign(signval) == 1 ? Mathf.Abs(sizeval) : -Mathf.Abs(sizeval);
		}
		public static Quaternion GetRotation(this Matrix4x4 matrix)
		{
			Quaternion q = new Quaternion();
			q.w = Mathf.Sqrt(Mathf.Max(0, 1 + matrix.m00 + matrix.m11 + matrix.m22)) / 2;
			q.x = Mathf.Sqrt(Mathf.Max(0, 1 + matrix.m00 - matrix.m11 - matrix.m22)) / 2;
			q.y = Mathf.Sqrt(Mathf.Max(0, 1 - matrix.m00 + matrix.m11 - matrix.m22)) / 2;
			q.z = Mathf.Sqrt(Mathf.Max(0, 1 - matrix.m00 - matrix.m11 + matrix.m22)) / 2;
			q.x = copysign(q.x, matrix.m21 - matrix.m12);
			q.y = copysign(q.y, matrix.m02 - matrix.m20);
			q.z = copysign(q.z, matrix.m10 - matrix.m01);
			return q;
		}

		public static Vector3 GetPosition(this Matrix4x4 matrix)
		{
			var x = matrix.m03;
			var y = matrix.m13;
			var z = matrix.m23;

			return new Vector3(x, y, z);
		}

		public static Vector3 GetScale(this Matrix4x4 m)
		{
			var x = Mathf.Sqrt(m.m00 * m.m00 + m.m01 * m.m01 + m.m02 * m.m02);
			var y = Mathf.Sqrt(m.m10 * m.m10 + m.m11 * m.m11 + m.m12 * m.m12);
			var z = Mathf.Sqrt(m.m20 * m.m20 + m.m21 * m.m21 + m.m22 * m.m22);

			return new Vector3(x, y, z);
		}
	}
	//We have to declare our own vector types, as .NET and Unity have different layouts.
	public struct Vector2
	{
		public float x, y;

		public Vector2(float x, float y) => (this.x, this.y) = (x, y);
		public Vector2(System.Numerics.Vector2 netVector) => (x, y) = (netVector.X, netVector.Y);
		public Vector2(UnityEngine.Vector2 unityVector) => (x, y) = (unityVector.x, unityVector.y);

		// We should implement a hash code. The GetHashCode method provides this hash code for algorithms that need quick checks of object equality.
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + x.GetHashCode();
				hash = hash * 23 + y.GetHashCode();
				return hash;
			}
		}
		public override bool Equals(object obj)
		{
			if (!(obj is Vector2))
			{
				return false;
			}
			return (this == ((Vector2)obj));
		}

		public static implicit operator Vector2(System.Numerics.Vector2 netVector) => new Vector2(netVector.X, netVector.Y);
		public static implicit operator Vector2(UnityEngine.Vector2 unityVector) => new Vector2(unityVector.x, unityVector.y);

		public static implicit operator System.Numerics.Vector2(Vector2 vector) => new System.Numerics.Vector2(vector.x, vector.y);
		public static implicit operator UnityEngine.Vector2(Vector2 vector) => new UnityEngine.Vector2(vector.x, vector.y);
		public static bool operator ==(Vector2 lhs, Vector2 rhs)
		{
			return lhs.x == rhs.x && lhs.y == rhs.y ;
		}

		public static bool operator !=(Vector2 lhs, Vector2 rhs)
		{
			return lhs.x != rhs.x || lhs.y != rhs.y ;
		}
	}

	public struct Vector3
	{
		public float x, y, z;

		public Vector3(float x, float y, float z) => (this.x, this.y, this.z) = (x, y, z);
		public Vector3(System.Numerics.Vector3 netVector) => (x, y, z) = (netVector.X, netVector.Y, netVector.Z);
		public Vector3(UnityEngine.Vector3 unityVector) => (x, y, z) = (unityVector.x, unityVector.y, unityVector.z);

		// We should implement a hash code. The GetHashCode method provides this hash code for algorithms that need quick checks of object equality.
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + x.GetHashCode();
				hash = hash * 23 + y.GetHashCode();
				hash = hash * 23 + z.GetHashCode();
				return hash;
			}
		}
		public override bool Equals(object obj)
		{
			if (!(obj is Vector3))
			{
				return false;
			}
			return (this==((Vector3)obj));
		}

		public override string ToString()
		{
			return string.Format("({0:0.0}, {1:0.0}, {2:0.0})", x, y, z);
		}

		public static implicit operator Vector3(System.Numerics.Vector3 netVector) => new Vector3(netVector.X, netVector.Y, netVector.Z);
		public static implicit operator Vector3(UnityEngine.Vector3 unityVector) => new Vector3(unityVector.x, unityVector.y, unityVector.z);
		public static implicit operator Vector3(UnityEngine.Color unityColour) => new Vector3(unityColour.r, unityColour.g, unityColour.b);

		public static implicit operator System.Numerics.Vector3(Vector3 vector) => new System.Numerics.Vector3(vector.x, vector.y, vector.z);
		public static implicit operator UnityEngine.Vector3(Vector3 vector) => new UnityEngine.Vector3(vector.x, vector.y, vector.z);
		public static implicit operator UnityEngine.Color(Vector3 vector) => new UnityEngine.Color(vector.x, vector.y, vector.z);

		public static bool operator ==(Vector3 lhs, Vector3 rhs)
		{
			return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
		}

		public static bool operator !=(Vector3 lhs, Vector3 rhs)
		{
			return lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Vector4
	{
		public float x, y, z, w;

		public Vector4(float x, float y, float z, float w) => (this.x, this.y, this.z, this.w) = (x, y, z, w);
		public Vector4(System.Numerics.Vector4 netVector) => (x, y, z, w) = (netVector.X, netVector.Y, netVector.Z, netVector.W);
		public Vector4(UnityEngine.Vector4 unityVector) => (x, y, z, w) = (unityVector.x, unityVector.y, unityVector.z, unityVector.w);


		// We should implement a hash code. The GetHashCode method provides this hash code for algorithms that need quick checks of object equality.
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + x.GetHashCode();
				hash = hash * 23 + y.GetHashCode();
				hash = hash * 23 + z.GetHashCode();
				hash = hash * 23 + w.GetHashCode();
				return hash;
			}
		}
		public override bool Equals(object obj)
		{
			if (!(obj is Vector4))
			{
				return false;
			}
			return (this == ((Vector4)obj));
		}
		public override string ToString()
		{
			return string.Format("({0:0.0}, {1:0.0}, {2:0.0}, {3:0.0})", x, y, z, w);
		}

		public static implicit operator Vector4(System.Numerics.Vector4 netVector) => new Vector4(netVector.X, netVector.Y, netVector.Z, netVector.W);
		public static implicit operator Vector4(UnityEngine.Vector4 unityVector) => new Vector4(unityVector.x, unityVector.y, unityVector.z, unityVector.w);
		public static implicit operator Vector4(UnityEngine.Color unityColour) => new Vector4(unityColour.r, unityColour.g, unityColour.b, unityColour.a);
		public static implicit operator Vector4(UnityEngine.Quaternion quaternion) => new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);

		public static implicit operator System.Numerics.Vector4(Vector4 vector) => new System.Numerics.Vector4(vector.x, vector.y, vector.z, vector.w);
		public static implicit operator UnityEngine.Vector4(Vector4 vector) => new UnityEngine.Vector4(vector.x, vector.y, vector.z, vector.w);
		public static implicit operator UnityEngine.Color(Vector4 vector) => new UnityEngine.Color(vector.x, vector.y, vector.z, vector.w);
		public static implicit operator UnityEngine.Quaternion(Vector4 vector) => new UnityEngine.Quaternion(vector.x, vector.y, vector.z, vector.w);

		public static bool operator ==(Vector4 lhs, Vector4 rhs)
		{
			return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z && lhs.w == rhs.w;
		}

		public static bool operator !=(Vector4 lhs, Vector4 rhs)
		{
			return lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z || lhs.w != rhs.w;
		}
	}
	
	[StructLayout(LayoutKind.Sequential)]
	public class Transform
	{
		public Vector3 position = new Vector3(0, 0, 0);
		public Vector4 rotation = new Vector4(0, 0, 0, 1);
		public Vector3 scale = new Vector3(1, 1, 1);

		//Converts a UnityEngine transform to a libavstream transform using the local transform data.
		public static Transform FromLocalUnityTransform(UnityEngine.Transform unityTransform)
		{
			Transform transform = new Transform();
			transform.position = unityTransform.localPosition;
			transform.rotation = unityTransform.localRotation;
			transform.scale = unityTransform.localScale;
			return transform;
		}

		//Converts a UnityEngine transform to a libavstream transform using the global transform data.
		public static Transform FromGlobalUnityTransform(UnityEngine.Transform unityTransform)
		{
			Transform transform = new Transform();
			transform.position = unityTransform.position;
			transform.rotation = unityTransform.rotation;
			transform.scale = unityTransform.lossyScale;
			return transform;
		}

		//Converts a UnityEngine Matrix4x4 to a libavstream transform 
		public static implicit operator Transform(UnityEngine.Matrix4x4 m)
		{
			Transform transform = new Transform();
			transform.position = m.GetPosition();
			transform.rotation = m.GetRotation();
			transform.scale = m.GetScale();
			return transform;
		}
	}

	public struct Mat4x4
	{
		//[Row, Column]
		float m00, m01, m02, m03;
		float m10, m11, m12, m13;
		float m20, m21, m22, m23;
		float m30, m31, m32, m33;

		public Mat4x4(float m00, float m01, float m02, float m03,
						float m10, float m11, float m12, float m13,
						float m20, float m21, float m22, float m23,
						float m30, float m31, float m32, float m33)
		{
			this.m00 = m00;
			this.m01 = m01;
			this.m02 = m02;
			this.m03 = m03;

			this.m10 = m10;
			this.m11 = m11;
			this.m12 = m12;
			this.m13 = m13;

			this.m20 = m20;
			this.m21 = m21;
			this.m22 = m22;
			this.m23 = m23;

			this.m30 = m30;
			this.m31 = m31;
			this.m32 = m32;
			this.m33 = m33;
		}

		public static implicit operator Mat4x4(Matrix4x4 matrix)
		{
			return new Mat4x4
			(
				matrix.m00, matrix.m01, matrix.m02, matrix.m03,
				matrix.m10, matrix.m11, matrix.m12, matrix.m13,
				matrix.m20, matrix.m21, matrix.m22, matrix.m23,
				matrix.m30, matrix.m31, matrix.m32, matrix.m33
			);
		}
	}

	public struct InputState
	{
		public UInt32 buttonsPressed;
		public float trackpadAxisX;
		public float trackpadAxisY;
		public float joystickAxisX;
		public float joystickAxisY;
	}

	public struct HeadPose
	{
		public Vector4 orientation;
		public Vector3 position;
	}

	public struct MovementUpdate
	{
		public long timestamp;
		public bool isGlobal;

		public uid nodeID;
		public avs.Vector3 position;
		public avs.Vector4 rotation;

		public avs.Vector3 velocity;
		public avs.Vector3 angularVelocityAxis;
		public float angularVelocityAngle;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct SceneCapture2DTagData
	{
		public uint id;
		public Transform cameraTransform;
	};

	[StructLayout(LayoutKind.Sequential)]
	public struct SceneCaptureCubeTagData
	{
		public uint id;
		public Transform cameraTransform;
		public uint lightCount;
		[MarshalAs(UnmanagedType.ByValArray)]
		public LightData[] lights;
	};

	[StructLayout(LayoutKind.Sequential)]
	public struct LightData
	{
		public Transform worldTransform;
		public Vector4 color;
		public float range;
		public float spotAngle;
		public LightType lightType;
		public Matrix4x4 shadowViewMatrix;
		public Matrix4x4 shadowProjectionMatrix;
        public Vector2Int texturePosition;
        public int textureSize;
        public uint uid;
	};
}

namespace SCServer
{
	/*! Graphics API device handle type. */
	public enum GraphicsDeviceType
	{
		Invalid = 0,
		Direct3D11 = 1,
		Direct3D12 = 2,
		OpenGL = 3,
		Vulkan = 4
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct VideoEncodeParams
	{
		public Int32 encodeWidth;
		public Int32 encodeHeight;
		public GraphicsDeviceType deviceType;
		public IntPtr deviceHandle;
		public IntPtr inputSurfaceResource;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct AudioParams
	{
		public avs.AudioCodec codec;
		public UInt32 sampleRate;
		public UInt32 bitsPerSample;
		public UInt32 numChannels;
	}
}
