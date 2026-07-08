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
            meshSimplifier.Colors = origin.colors;
#endif // OPTIMISATION

            var triangles = new int[origin.subMeshCount][];
            for (var submesh = 0; submesh < origin.subMeshCount; submesh++)
            {
                triangles[submesh] = origin.GetTriangles(submesh);
            }

            meshSimplifier.AddSubMeshTriangles(triangles);

            meshSimplifier.SimplifyMesh(quality);

            var subMeshIndices = new System.Collections.Generic.List<int>();
            int triCount = 0;
            for (int i = 0; i < meshSimplifier.SubMeshCount; ++i)
            {
                triCount += meshSimplifier.GetSubMeshTriangles(i, subMeshIndices).Count;
            }

#if OPTIMISATION
            Utils.WorkingMesh nwm = new WorkingMesh(Allocator.Persistent,
                meshSimplifier.VerticesSpan, meshSimplifier.NormalsSpan, meshSimplifier.TangentsSpan,
                meshSimplifier.UVSpan, meshSimplifier.UV2Span, meshSimplifier.UV3Span, meshSimplifier.UV4Span,
                meshSimplifier.ColorsSpan, triCount, meshSimplifier.SubMeshCount, 0);
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
            nwm.colors = meshSimplifier.Colors;
#endif // OPTIMISATION
            nwm.subMeshCount = meshSimplifier.SubMeshCount;
            subMeshIndices.Clear();
            for (var submesh = 0; submesh < nwm.subMeshCount; submesh++)
            {
                nwm.SetTriangles(meshSimplifier.GetSubMeshTriangles(submesh, subMeshIndices), submesh);
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