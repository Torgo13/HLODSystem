using System;
using System.Collections;
using Unity.Collections;
using Unity.HLODSystem.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.HLODSystem.Simplifier
{
    public class UnityMeshSimplifier : SimplifierBase
    {

        [InitializeOnLoadMethod]
        static void RegisterType()
        {
            SimplifierTypes.RegisterType(typeof(UnityMeshSimplifier));
        }

        public UnityMeshSimplifier(SerializableDynamicObject simplifierOptions): base(simplifierOptions)
        {
        }

        protected override IEnumerator GetSimplifiedMesh(Utils.WorkingMesh origin, float quality, Action<Utils.WorkingMesh>? resultCallback)
        {
#if USING_COLLECTIONS
            using
#endif // USING_COLLECTIONS
#if OPTIMISATION
            var meshSimplifier = new global::UnityMeshSimplifier.MeshSimplifier(
                origin.Vertices, origin.Normals, origin.Tangents,
                origin.UV, origin.UV2, origin.UV3, origin.UV4,
#if UNITY_8UV_SUPPORT
                origin.UV5, origin.UV6, origin.UV7, origin.UV8,
#endif // UNITY_8UV_SUPPORT
                origin.Colors);
#else
            var meshSimplifier = new global::UnityMeshSimplifier.MeshSimplifier();
            meshSimplifier.Vertices = origin.vertices;
            meshSimplifier.Normals = origin.normals;
            meshSimplifier.Tangents = origin.tangents;
            meshSimplifier.UV1 = origin.uv;
            meshSimplifier.UV2 = origin.uv2;
            meshSimplifier.UV3 = origin.uv3;
            meshSimplifier.UV4 = origin.uv4;
#if UNITY_8UV_SUPPORT
            meshSimplifier.UV5 = origin.uv5;
            meshSimplifier.UV6 = origin.uv6;
            meshSimplifier.UV7 = origin.uv7;
            meshSimplifier.UV8 = origin.uv8;
#endif // UNITY_8UV_SUPPORT
            meshSimplifier.Colors = origin.colors;
#endif // OPTIMISATION

#if OPTIMISATION
            for (var submesh = 0; submesh < origin.subMeshCount; submesh++)
            {
                meshSimplifier.AddSubMeshTriangles(origin.GetTrianglesNative(submesh));
            }
#else
            var triangles = new int[origin.subMeshCount][];
            for (var submesh = 0; submesh < origin.subMeshCount; submesh++)
            {
                triangles[submesh] = origin.GetTriangles(submesh);
            }

            meshSimplifier.AddSubMeshTriangles(triangles);
#endif // OPTIMISATION

            meshSimplifier.SimplifyMesh(quality);

            int triCount = 0;
            for (int i = 0; i < meshSimplifier.SubMeshCount; ++i)
            {
                triCount += meshSimplifier.GetSubMeshTriangles(i).Length;
            }

#if OPTIMISATION
            Utils.WorkingMesh nwm = new WorkingMesh(Allocator.Persistent,
                meshSimplifier.VerticesSpan, meshSimplifier.NormalsSpan, meshSimplifier.TangentsSpan,
                meshSimplifier.UVSpan, meshSimplifier.UV2Span, meshSimplifier.UV3Span, meshSimplifier.UV4Span,
#if UNITY_8UV_SUPPORT
                meshSimplifier.UV5Span, meshSimplifier.UV6Span, meshSimplifier.UV7Span, meshSimplifier.UV8Span,
#endif // UNITY_8UV_SUPPORT
                meshSimplifier.ColorsSpan, triCount, meshSimplifier.SubMeshCount, maxBindposes: 0);
            nwm.name = origin.name;
#else
            var vertices = meshSimplifier.Vertices;
            Utils.WorkingMesh nwm = new WorkingMesh(Allocator.Persistent, vertices.Length, triCount, meshSimplifier.SubMeshCount, 0);
            nwm.name = origin.name;
            nwm.vertices = vertices;
            nwm.normals = meshSimplifier.Normals;
            nwm.tangents = meshSimplifier.Tangents;
            nwm.uv = meshSimplifier.UV1;
            nwm.uv2 = meshSimplifier.UV2;
            nwm.uv3 = meshSimplifier.UV3;
            nwm.uv4 = meshSimplifier.UV4;
#if UNITY_8UV_SUPPORT
            nwm.uv5 = meshSimplifier.UV5;
            nwm.uv6 = meshSimplifier.UV6;
            nwm.uv7 = meshSimplifier.UV7;
            nwm.uv8 = meshSimplifier.UV8;
#endif // UNITY_8UV_SUPPORT
            nwm.colors = meshSimplifier.Colors;
#endif // OPTIMISATION
            nwm.subMeshCount = meshSimplifier.SubMeshCount;
            for (var submesh = 0; submesh < nwm.subMeshCount; submesh++)
            {
                nwm.SetTriangles(meshSimplifier.GetSubMeshTriangles(submesh), submesh);
            }

            if (resultCallback != null)
            {
                resultCallback(nwm);
            }
            yield break;
        }

        

        public static void OnGUI(SerializableDynamicObject simplifierOptions)
        {
            OnGUIBase(simplifierOptions);
        }
    }
}