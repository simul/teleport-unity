using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace teleport
{
    class Quad
    {
        public Vector3 BottomLeft
        {
            get; set;
        }
        public Vector3 TopLeft
        {
            get; set;
        }
        public Vector3 TopRight
        {
            get; set;
        }
        public Vector3 BottomRight
        {
            get; set;
        }

        public Quad(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight)
        {
            BottomLeft = bottomLeft;
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
        }

        public Quad()
        {
            BottomLeft = Vector3.zero;
            TopLeft = Vector3.zero;
            TopRight = Vector3.zero;
            BottomRight = Vector3.zero;
        }
    }

    public class CubemapCuller
    {
        static Quaternion frontQuat = Quaternion.identity;
        static Quaternion backQuat = new Quaternion(0, 1, 0, 180);
        static Quaternion rightQuat = new Quaternion(0, 1, 0, 90);
        static Quaternion leftQuat = new Quaternion(0, 1, 0, -90);
        static Quaternion upQuat = new Quaternion(1, 0, 0, 90);
        static Quaternion downQuat = new Quaternion(1, 0, 0, -90);

        static Quaternion[] faceQuats = new Quaternion[] { frontQuat, backQuat, rightQuat, leftQuat, upQuat, downQuat };

        List<Quad> quads = new List<Quad>();
        Dictionary<Vector3, bool> intersectResults = new Dictionary<Vector3, bool>();

        float cubeWidth = 512;
        int numQuadsAcross = 2;
        int numQuadsPerFace = 4;

        public CubemapCuller()
        {
            Reset();
        }

        public void Reset()
        {
            SCServer.CasterSettings settings = TeleportSettings.GetOrCreateSettings().casterSettings;
            // TODO: can't use the default, it should be the one in ClientSettings:
            cubeWidth = settings.defaultCaptureCubeTextureSize;
            numQuadsAcross = settings.blocksPerCubeFaceAcross;
            numQuadsPerFace = numQuadsAcross * numQuadsAcross;
            ClearIntersectionResults();
            CreateCubeQuads();
        }

        public void ClearIntersectionResults()
        {
            intersectResults.Clear();
        }

        public bool FaceIntersectsFrustum(Camera camera, int face, ref Matrix4x4 projectionMatrix)
        {
            bool intersects = false;
            for (int i = face * numQuadsPerFace; i < (face + 1) * numQuadsPerFace; ++i)
            {
                Quad quad = quads[i];
                if (QuadIntersectsFrustum(camera, quad))
                {
                    intersects = true;
                }
            }
            return intersects;
        }

        bool QuadIntersectsFrustum(Camera camera, Quad quad)
        {
            if (VectorIntersectsFrustum(camera, quad.BottomLeft) || VectorIntersectsFrustum(camera, quad.TopLeft)
                || VectorIntersectsFrustum(camera, quad.TopRight) || VectorIntersectsFrustum(camera, quad.BottomRight))
            {
                return true;
            }
            return false;
        }

        bool VectorIntersectsFrustum(Camera camera, Vector3 v)
        {
            if (intersectResults.ContainsKey(v))
            {
                return intersectResults[v];
            }

            var screenPos = camera.WorldToScreenPoint(v);

            if(screenPos == Vector3.zero || screenPos.x < 0 || screenPos.y < 0 || screenPos.x > camera.pixelWidth 
                || screenPos.y > camera.pixelHeight)
            {
                return false;
            }

            return true;
        }

        void CreateCubeQuads()
        {
            quads.Clear();

            float halfWidth = cubeWidth / 2;
            float quadWidth = cubeWidth / numQuadsAcross;

            Vector3 startPos = new Vector3(halfWidth, -halfWidth, -halfWidth);

            // Iterate through all six faces
            for (int i = 0; i < 6; ++i)
            {             
                Vector3 rightVec = (faceQuats[i] * Vector3.right) * quadWidth;
                Vector3 upVec = (faceQuats[i] * Vector3.up) * quadWidth;
                Vector3 pos = faceQuats[i] * startPos;

                // Go right
                for (int j = 0; j < numQuadsAcross; ++j)
                {
                    Vector3 quadPos = pos;
                    // Go up
                    for (int k = 0; k < numQuadsAcross; ++k)
                    {
                        Quad quad = new Quad (quadPos, quadPos + upVec, quadPos + rightVec, quadPos + upVec + rightVec);
                        quads.Add(quad);
                    }
                    pos += rightVec;
                }
            }
        }
    }
}
