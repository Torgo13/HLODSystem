using UnityEngine;

namespace Unity.HLODSystem.Utils
{
#if UNITY_EDITOR
#if USING_BURST
    [Burst.BurstCompile]
#endif // USING_BURST
    public class BoundsUtils
    {
#if USING_BURST
        public static Bounds CalcLocalBounds(System.ReadOnlySpan<Renderer> renderer, Transform transform)
        {
            var bounds = new Collections.NativeArray<Bounds>(renderer.Length,
                Collections.Allocator.TempJob,
                Collections.NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < bounds.Length; i++)
            {
                bounds[i] = renderer[i].bounds;
            }
            
            CalcLocalBounds(ref bounds, transform.worldToLocalMatrix, out Bounds newBounds);
            bounds.Dispose();
            return newBounds;
        }
        
        public static Bounds CalcLocalBounds(System.ReadOnlySpan<MeshRenderer> renderer, Transform transform)
        {
            var bounds = new Collections.NativeArray<Bounds>(renderer.Length,
                Collections.Allocator.TempJob,
                Collections.NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < bounds.Length; i++)
            {
                bounds[i] = renderer[i].bounds;
            }
            
            CalcLocalBounds(ref bounds, transform.worldToLocalMatrix, out Bounds newBounds);
            bounds.Dispose();
            return newBounds;
        }

        [Burst.BurstCompile(FloatMode = Burst.FloatMode.Fast)]
        private static void CalcLocalBounds(ref Collections.NativeArray<Bounds> bounds, in Matrix4x4 matrix,
            out Bounds newBounds)
        {
            System.Span<Vector3> points = stackalloc Vector3[8];
            Bounds temp = CalcLocalBounds(bounds[0], matrix, points);
                
            for (int i = 1; i < bounds.Length; ++i)
            {
                temp.Encapsulate(CalcLocalBounds(bounds[i], matrix, points));
            }

            newBounds = temp;
        }
#else
        public static Bounds CalcLocalBounds(System.ReadOnlySpan<Renderer> renderers, Transform transform)
        {
            Bounds bounds = Utils.BoundsUtils.CalcLocalBounds(renderers[0], transform);
            for (int i = 1; i < renderers.Length; ++i)
            {
                bounds.Encapsulate(Utils.BoundsUtils.CalcLocalBounds(renderers[i], transform));
            }

            return bounds;
        }

        public static Bounds CalcLocalBounds(System.ReadOnlySpan<MeshRenderer> renderers, Transform transform)
        {
            Bounds bounds = Utils.BoundsUtils.CalcLocalBounds(renderers[0], transform);
            for (int i = 1; i < renderers.Length; ++i)
            {
                bounds.Encapsulate(Utils.BoundsUtils.CalcLocalBounds(renderers[i], transform));
            }

            return bounds;
        }
#endif // USING_BURST
        
        public static Bounds CalcLocalBounds(Renderer renderer, Transform transform)
        {
            Bounds bounds = renderer.bounds;
            Matrix4x4 matrix = transform.worldToLocalMatrix;
            System.Span<Vector3> points = stackalloc Vector3[8];
            return CalcLocalBounds(bounds, matrix, points);
        }
        
        private static Bounds CalcLocalBounds(Bounds bounds, in Matrix4x4 matrix,
            System.Span<Vector3> points)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            points[0] = new Vector3(min.x, min.y, min.z);
            points[1] = new Vector3(max.x, min.y, min.z);
            points[2] = new Vector3(min.x, min.y, max.z);
            points[3] = new Vector3(max.x, min.y, max.z);
            points[4] = new Vector3(min.x, max.y, min.z);
            points[5] = new Vector3(max.x, max.y, min.z);
            points[6] = new Vector3(min.x, max.y, max.z);
            points[7] = new Vector3(max.x, max.y, max.z);

            for (int i = 0; i < points.Length; ++i)
            {
                points[i] = matrix.MultiplyPoint(points[i]);
            }

            Vector3 newMin = points[0];
            Vector3 newMax = points[0];

            for (int i = 1; i < points.Length; ++i)
            {
                if (newMin.x > points[i].x) newMin.x = points[i].x;
                if (newMax.x < points[i].x) newMax.x = points[i].x;
                
                if (newMin.y > points[i].y) newMin.y = points[i].y;
                if (newMax.y < points[i].y) newMax.y = points[i].y;
                
                if (newMin.z > points[i].z) newMin.z = points[i].z;
                if (newMax.z < points[i].z) newMax.z = points[i].z;
            }


            Bounds newBounds = new Bounds();
            newBounds.SetMinMax(newMin, newMax);
            return newBounds;
        }
    }
#endif // UNITY_EDITOR
}