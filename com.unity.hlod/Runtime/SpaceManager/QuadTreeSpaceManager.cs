using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.HLODSystem.SpaceManager
{
    public struct QuadTreeSpaceManager : ISpaceManager
    {

        private float preRelative;
        private Vector3 camPosition;
        public void UpdateCamera(Transform hlodTransform, Camera cam)
        {
            if (cam.orthographic)
            {
                preRelative = 0.5f / cam.orthographicSize;
            }
            else
            {
                double halfAngle = System.Math.Tan(Mathf.Deg2Rad * 0.5 * cam.fieldOfView);
                preRelative = (float)(0.5 / halfAngle);
            }
            preRelative = preRelative * QualitySettings.lodBias;
            camPosition = hlodTransform.worldToLocalMatrix.MultiplyPoint(cam.transform.position);
        }

        readonly
        public bool IsHigh(float lodDistance, Bounds bounds)
        {
            //float distance = 1.0f;
            //if (cam.orthographic == false)
            
                float distance = GetDistance(bounds.center, camPosition);
            float relativeHeight = bounds.size.x * preRelative / distance;
            return relativeHeight > lodDistance;
        }

        readonly
        public float GetDistanceSqure(Bounds bounds)
        {
            float x = bounds.center.x - camPosition.x;
            float z = bounds.center.z - camPosition.z;

            float square = x * x + z * z;
            return square;
        }
        
        readonly
        public bool IsCull(float cullDistance, Bounds bounds)
        {
            float distance = GetDistance(bounds.center, camPosition);

            float relativeHeight = bounds.size.x * preRelative / distance;
            return relativeHeight < cullDistance;
        }

        static
        private float GetDistance(Vector3 boundsPos, Vector3 camPos)
        {
            float x = boundsPos.x - camPos.x;
            float z = boundsPos.z - camPos.z;
            float square = x * x + z * z;
            return Mathf.Sqrt(square);
        }

       
    }

}