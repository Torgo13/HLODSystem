using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.HLODSystem.Utils;
using UnityEngine;

namespace Unity.HLODSystem
{
    public class MeshCombiner
    {
        public struct CombineInfo
        {
            public Matrix4x4 Transform;
            public WorkingMesh Mesh;
            public int MeshIndex;

        }
        public WorkingMesh CombineMesh(Allocator allocator, List<CombineInfo> infos)
        {
            var remappers = UnityEngine.Pool.ListPool<Dictionary<int, int>>.Get();
            var vertices = UnityEngine.Pool.ListPool<Vector3>.Get();
            var normals = UnityEngine.Pool.ListPool<Vector3>.Get();
            var tangents = UnityEngine.Pool.ListPool<Vector4>.Get();
            var uv1s = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv2s = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv3s = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv4s = UnityEngine.Pool.ListPool<Vector2>.Get();
#if UNITY_8UV_SUPPORT
            var uv5s = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv6s = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv7s = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv8s = UnityEngine.Pool.ListPool<Vector2>.Get();
#endif // UNITY_8UV_SUPPORT
            var colors = UnityEngine.Pool.ListPool<Color>.Get();
            var triangles = UnityEngine.Pool.ListPool<int>.Get();

            var mesh = CombineMesh(allocator, infos, remappers, vertices, normals, tangents,
                uv1s, uv2s, uv3s, uv4s,
#if UNITY_8UV_SUPPORT
                uv5s, uv6s, uv7s, uv8s,
#endif // UNITY_8UV_SUPPORT
                colors, triangles);
                
            UnityEngine.Pool.ListPool<Dictionary<int, int>>.Release(remappers);
            UnityEngine.Pool.ListPool<Vector3>.Release(vertices);
            UnityEngine.Pool.ListPool<Vector3>.Release(normals);
            UnityEngine.Pool.ListPool<Vector4>.Release(tangents);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv1s);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv2s);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv3s);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv4s);
#if UNITY_8UV_SUPPORT
            UnityEngine.Pool.ListPool<Vector2>.Release(uv5s);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv6s);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv7s);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv8s);
#endif // UNITY_8UV_SUPPORT
            UnityEngine.Pool.ListPool<Color>.Release(colors);
            UnityEngine.Pool.ListPool<int>.Release(triangles);

            return mesh;
        }
        
        public WorkingMesh CombineMesh(Allocator allocator, List<CombineInfo> infos,
            List<Dictionary<int, int>> remappers, List<Vector3> vertices, List<Vector3> normals, List<Vector4> tangents,
            List<Vector2> uv1s, List<Vector2> uv2s, List<Vector2> uv3s, List<Vector2> uv4s,
#if UNITY_8UV_SUPPORT
            List<Vector2> uv5s, List<Vector2> uv6s, List<Vector2> uv7s, List<Vector2> uv8s,
#endif // UNITY_8UV_SUPPORT
            List<Color> colors, List<int> triangles)
        {
            //I didn't consider animation mesh combine.
            int verticesCount = 0;
            int normalCount = 0;
            int tangentCount = 0;
            int UV1Count = 0;
            int UV2Count = 0;
            int UV3Count = 0;
            int UV4Count = 0;
#if UNITY_8UV_SUPPORT
            int UV5Count = 0;
            int UV6Count = 0;
            int UV7Count = 0;
            int UV8Count = 0;
#endif // UNITY_8UV_SUPPORT
            int colorCount = 0;

            int trianglesCount = 0;

            remappers.Clear();
            if (remappers.Capacity < infos.Count)
                remappers.Capacity = infos.Count;
            
            for (int i = 0; i < infos.Count; ++i)
            {
                using
                var meshIndices = infos[i].Mesh.GetTrianglesNative(infos[i].MeshIndex);
                Dictionary<int, int> remapper = CalculateMeshRemap(meshIndices);

                verticesCount += (infos[i].Mesh.vertexCount > 0) ? remapper.Count : 0;
                normalCount += (infos[i].Mesh.NormalsCount > 0) ? remapper.Count : 0;
                tangentCount += (infos[i].Mesh.TangentsCount > 0) ? remapper.Count : 0;
                UV1Count += (infos[i].Mesh.UVCount > 0) ? remapper.Count : 0;
                UV2Count += (infos[i].Mesh.UV2Count > 0) ? remapper.Count : 0;
                UV3Count += (infos[i].Mesh.UV3Count > 0) ? remapper.Count : 0;
                UV4Count += (infos[i].Mesh.UV4Count > 0) ? remapper.Count : 0;
#if UNITY_8UV_SUPPORT
                UV5Count += (infos[i].Mesh.UV5Count > 0) ? remapper.Count : 0;
                UV6Count += (infos[i].Mesh.UV6Count > 0) ? remapper.Count : 0;
                UV7Count += (infos[i].Mesh.UV7Count > 0) ? remapper.Count : 0;
                UV8Count += (infos[i].Mesh.UV8Count > 0) ? remapper.Count : 0;
#endif // UNITY_8UV_SUPPORT
                colorCount += (infos[i].Mesh.ColorsCount > 0) ? remapper.Count : 0;

                trianglesCount += meshIndices.Length;
                
                remappers.Add(remapper);
            }
            
            WorkingMesh combinedMesh = new WorkingMesh(allocator, verticesCount, trianglesCount, 1, 0);

            vertices.Clear();
            normals.Clear();
            tangents.Clear();
            uv1s.Clear();
            uv2s.Clear();
            uv3s.Clear();
            uv4s.Clear();
#if UNITY_8UV_SUPPORT
            uv5s.Clear();
            uv6s.Clear();
            uv7s.Clear();
            uv8s.Clear();
#endif // UNITY_8UV_SUPPORT
            colors.Clear();
            triangles.Clear();

            for (int i = 0; i < infos.Count; ++i)
            {
                WorkingMesh mesh = infos[i].Mesh;
                Dictionary<int, int> remapper = remappers[i];
                int startIndex = vertices.Count;

                if (verticesCount > 0)
                {
                    FillBuffer(ref vertices, mesh.Vertices, remapper, Vector3.zero);
                    for (int vi = startIndex; vi < vertices.Count; ++vi)
                    {
                        vertices[vi] = infos[i].Transform.MultiplyPoint(vertices[vi]);
                    }
                }

                if (normalCount > 0)
                {
                    FillBuffer(ref normals, mesh.Normals, remapper, Vector3.up);
                    for (int ni = startIndex; ni < normals.Count; ++ni)
                    {
                        normals[ni] = infos[i].Transform.MultiplyVector(normals[ni]);
                    }
                }

                if (tangentCount > 0)
                {
                    FillBuffer(ref tangents, mesh.Tangents, remapper, new Vector4(1, 0, 0, 1));
                    for (int ti = startIndex; ti < tangents.Count; ++ti)
                    {
                        Vector3 tanVec = new Vector3(tangents[ti].x, tangents[ti].y, tangents[ti].z);
                        tanVec = infos[i].Transform.MultiplyVector(tanVec);
                        Vector4 transTan = new Vector4(tanVec.x, tanVec.y, tanVec.z, tangents[ti].w);
                        tangents[ti] = transTan;
                    }
                }

                if ( UV1Count > 0 )
                    FillBuffer(ref uv1s, mesh.UV, remapper, Vector2.zero);
                if ( UV2Count > 0 )
                    FillBuffer(ref uv2s, mesh.UV2, remapper, Vector2.zero);
                if ( UV3Count > 0 )
                    FillBuffer(ref uv3s, mesh.UV3, remapper, Vector2.zero);
                if ( UV4Count > 0 )
                    FillBuffer(ref uv4s, mesh.UV4, remapper, Vector2.zero);
#if UNITY_8UV_SUPPORT
                if (UV5Count > 0)
                    FillBuffer(ref uv5s, mesh.UV5, remapper, Vector2.zero);
                if (UV6Count > 0)
                    FillBuffer(ref uv6s, mesh.UV6, remapper, Vector2.zero);
                if (UV7Count > 0)
                    FillBuffer(ref uv7s, mesh.UV7, remapper, Vector2.zero);
                if (UV8Count > 0)
                    FillBuffer(ref uv8s, mesh.UV8, remapper, Vector2.zero);
#endif // UNITY_8UV_SUPPORT
                if ( colorCount > 0 )
                    FillBuffer(ref colors, mesh.Colors, remapper, Color.white);

                FillIndices(ref triangles, mesh.GetTrianglesNative(infos[i].MeshIndex).AsReadOnlySpan(), remapper, startIndex);

            }

            combinedMesh.name = "CombinedMesh";
            combinedMesh.SetVertices(vertices);
            combinedMesh.SetNormals(normals);
            combinedMesh.SetTangents(tangents);
            combinedMesh.SetUV(uv1s);
            combinedMesh.SetUV2(uv2s);
            combinedMesh.SetUV3(uv3s);
            combinedMesh.SetUV4(uv4s);
#if UNITY_8UV_SUPPORT
            combinedMesh.SetUV5(uv5s);
            combinedMesh.SetUV6(uv6s);
            combinedMesh.SetUV7(uv7s);
            combinedMesh.SetUV8(uv8s);
#endif // UNITY_8UV_SUPPORT
            combinedMesh.SetColors(colors);
            
            combinedMesh.SetTriangles(triangles.AsReadOnlySpan(), 0);

            return combinedMesh;
        }

        private static void FillBuffer<T>(ref List<T> buffer, System.ReadOnlySpan<T> source, Dictionary<int, int> remapper, T defaultValue)
        {
            int startIndex = buffer.Count;
            if (buffer.Capacity < startIndex + remapper.Count)
                buffer.Capacity = startIndex + remapper.Count;
            for (int i = 0, remapperCount = remapper.Count; i < remapperCount; i++)
            {
                buffer.Add(defaultValue);
            }
            
            if (source == null || source.Length == 0)
            {

                return;
            }

            foreach (var pair in remapper)
            {
                buffer[pair.Value + startIndex] = source[pair.Key];
            }
        }

        private static void FillIndices(ref List<int> buffer, System.ReadOnlySpan<int> source, Dictionary<int, int> remapper, int startIndex )
        {
            for (int i = 0; i < source.Length; ++i)
            {
                int newIndex = remapper[source[i]] + startIndex;
                buffer.Add(newIndex);
            }
        }

        
        //first original index
        //second new index
        private static Dictionary<int, int> CalculateMeshRemap(System.ReadOnlySpan<int> indices)
        {
            Dictionary<int, int> remapper = new Dictionary<int, int>();

            for (int i = 0; i < indices.Length; ++i)
            {
                if (remapper.ContainsKey(indices[i]))
                    continue;
                
                remapper.Add(indices[i], remapper.Count);
            }

            return remapper;
        }
    }
}