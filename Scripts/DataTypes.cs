using System.Runtime.InteropServices;

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
        EngineeringStyle = 4 | RightHanded,
        GlStyle = 8 | RightHanded,
        UnrealStyle = 16 | LeftHanded,
    }

    //We have to declare our own vector types, as .NET and Unity have different layouts.
    public struct Vector2
    {
        public float x, y;

        public Vector2(float x, float y) => (this.x, this.y) = (x, y);
        public Vector2(System.Numerics.Vector2 netVector) => (x, y) = (netVector.X, netVector.Y);
        public Vector2(UnityEngine.Vector2 unityVector) => (x, y) = (unityVector.x, unityVector.y);

        public static implicit operator Vector2(System.Numerics.Vector2 netVector) => new Vector2(netVector.X, netVector.Y);
        public static implicit operator Vector2(UnityEngine.Vector2 unityVector) => new Vector2(unityVector.x, unityVector.y);

        public static implicit operator System.Numerics.Vector2(Vector2 vector) => new System.Numerics.Vector2(vector.x, vector.y);
        public static implicit operator UnityEngine.Vector2(Vector2 vector) => new UnityEngine.Vector2(vector.x, vector.y);
    }

    public struct Vector3
    {
        public float x, y, z;

        public Vector3(float x, float y, float z) => (this.x, this.y, this.z) = (x, y, z);
        public Vector3(System.Numerics.Vector3 netVector) => (x, y, z) = (netVector.X, netVector.Y, netVector.Z);
        public Vector3(UnityEngine.Vector3 unityVector) => (x, y, z) = (unityVector.x, unityVector.y, unityVector.z);

        public static implicit operator Vector3(System.Numerics.Vector3 netVector) => new Vector3(netVector.X, netVector.Y, netVector.Z);
        public static implicit operator Vector3(UnityEngine.Vector3 unityVector) => new Vector3(unityVector.x, unityVector.y, unityVector.z);
        public static implicit operator Vector3(UnityEngine.Color unityColour) => new Vector3(unityColour.r, unityColour.g, unityColour.b);

        public static implicit operator System.Numerics.Vector3(Vector3 vector) => new System.Numerics.Vector3(vector.x, vector.y, vector.z);
        public static implicit operator UnityEngine.Vector3(Vector3 vector) => new UnityEngine.Vector3(vector.x, vector.y, vector.z);
        public static implicit operator UnityEngine.Color(Vector3 vector) => new UnityEngine.Color(vector.x, vector.y, vector.z);
    }

    public struct Vector4
    {
        public float x, y, z, w;

        public Vector4(float x, float y, float z, float w) => (this.x, this.y, this.z, this.w) = (x, y, z, w);
        public Vector4(System.Numerics.Vector4 netVector) => (x, y, z, w) = (netVector.X, netVector.Y, netVector.Z, netVector.W);
        public Vector4(UnityEngine.Vector4 unityVector) => (x, y, z, w) = (unityVector.x, unityVector.y, unityVector.z, unityVector.w);

        public static implicit operator Vector4(System.Numerics.Vector4 netVector) => new Vector4(netVector.X, netVector.Y, netVector.Z, netVector.W);
        public static implicit operator Vector4(UnityEngine.Vector4 unityVector) => new Vector4(unityVector.x, unityVector.y, unityVector.z, unityVector.w);
        public static implicit operator Vector4(UnityEngine.Color unityColour) => new Vector4(unityColour.r, unityColour.g, unityColour.b, unityColour.a);
        public static implicit operator Vector4(UnityEngine.Quaternion quaternion) => new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);

        public static implicit operator System.Numerics.Vector4(Vector4 vector) => new System.Numerics.Vector4(vector.x, vector.y, vector.z, vector.w);
        public static implicit operator UnityEngine.Vector4(Vector4 vector) => new UnityEngine.Vector4(vector.x, vector.y, vector.z, vector.w);
        public static implicit operator UnityEngine.Color(Vector4 vector) => new UnityEngine.Color(vector.x, vector.y, vector.z, vector.w);
        public static implicit operator UnityEngine.Quaternion(Vector4 vector) => new UnityEngine.Quaternion(vector.x, vector.y, vector.z, vector.w);
    }

    [StructLayout(LayoutKind.Sequential)]
    public class Transform
    {
        public Vector3 position = new Vector3(0, 0, 0);
        public Vector4 rotation = new Vector4(0, 0, 0, 1);
        public Vector3 scale = new Vector3(1, 1, 1);
    }

    public struct InputState
    {
        public System.UInt32 buttonsPressed;
        public System.UInt32 buttonsReleased;
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
}