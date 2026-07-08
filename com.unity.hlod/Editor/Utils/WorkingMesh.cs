using System;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.HLODSystem.Utils
{
    public static class MeshExtensions
    {
        public static WorkingMesh ToWorkingMesh(this Mesh mesh, Allocator allocator)
        {
            var bindposes = UnityEngine.Pool.ListPool<Matrix4x4>.Get();
            mesh.GetBindposes(bindposes);
            var wm = new WorkingMesh(allocator, mesh.vertexCount, mesh.triangles.Length, mesh.subMeshCount, bindposes.Count);
            mesh.ApplyToWorkingMesh(ref wm, bindposes);
            UnityEngine.Pool.ListPool<Matrix4x4>.Release(bindposes);
            return wm;
        }

        // Taking bindposes optional parameter is ugly, but saves an additional array allocation if it was already
        // accessed to get the length
        public static void ApplyToWorkingMesh(this Mesh mesh, ref WorkingMesh wm,
            System.Collections.Generic.List<Matrix4x4> bindposes)
        {
            wm.indexFormat = mesh.indexFormat;
#if OPTIMISATION
            var vertices = UnityEngine.Pool.ListPool<Vector3>.Get();
            var normals = UnityEngine.Pool.ListPool<Vector3>.Get();
            var tangents = UnityEngine.Pool.ListPool<Vector4>.Get();
            var uv = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv2 = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv3 = UnityEngine.Pool.ListPool<Vector2>.Get();
            var uv4 = UnityEngine.Pool.ListPool<Vector2>.Get();
            var colors = UnityEngine.Pool.ListPool<Color>.Get();
            var boneWeights = UnityEngine.Pool.ListPool<BoneWeight>.Get();
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            mesh.GetTangents(tangents);
            mesh.GetUVs(0, uv);
            mesh.GetUVs(1, uv2);
            mesh.GetUVs(2, uv3);
            mesh.GetUVs(3, uv4);
            mesh.GetColors(colors);
            mesh.GetBoneWeights(boneWeights);
            wm.CopyFrom(vertices, normals, tangents, uv, uv2, uv3, uv4, colors, bindposes, boneWeights);
            UnityEngine.Pool.ListPool<Vector3>.Release(vertices);
            UnityEngine.Pool.ListPool<Vector3>.Release(normals);
            UnityEngine.Pool.ListPool<Vector4>.Release(tangents);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv2);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv3);
            UnityEngine.Pool.ListPool<Vector2>.Release(uv4);
            UnityEngine.Pool.ListPool<Color>.Release(colors);
            UnityEngine.Pool.ListPool<BoneWeight>.Release(boneWeights);
#else
            wm.vertices = mesh.vertices;
            wm.normals = mesh.normals;
            wm.tangents = mesh.tangents;
            wm.uv = mesh.uv;
            wm.uv2 = mesh.uv2;
            wm.uv3 = mesh.uv3;
            wm.uv4 = mesh.uv4;
            wm.colors = mesh.colors;
            wm.boneWeights = mesh.boneWeights;
#endif // OPTIMISATION
            wm.subMeshCount = mesh.subMeshCount;
            using var _0 = UnityEngine.Pool.ListPool<int>.Get(out var triangles);
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                triangles.Clear();
                mesh.GetTriangles(triangles, i);
                wm.SetTriangles(triangles, i);
            }
            wm.name = mesh.name;
            wm.bounds = mesh.bounds;
        }
    }

    public class WorkingMesh : IDisposable
    {
        private NativeArray<int> m_detector = new NativeArray<int>(1, Allocator.Persistent);
        
        enum Channel
        {
            Vertices,
            Normals,
            Tangents,
            UV,
            UV2,
            UV3,
            UV4,
            Colors,
            BoneWeights,
            Bindposes,
            Triangles,
            SubmeshOffset
        }

        const int k_MaxNameSize = 128;
        
#if OPTIMISATION
        public NativeArray<Vector3> Vertices
        {
            get => m_Vertices.GetSubArray(0, vertexCount);
            set
            {
                vertexCount = value.Length;
                m_Vertices.GetSubArray(0, vertexCount).CopyFrom(value);
            }
        }
        public NativeArray<Vector3> Normals
        {
            get => m_Normals.GetSubArray(0, normalsCount);
            set
            {
                normalsCount = value.Length;
                m_Normals.GetSubArray(0, normalsCount).CopyFrom(value);
            }
        }
        public NativeArray<Vector4> Tangents
        {
            get => m_Tangents.GetSubArray(0, tangentsCount);
            set
            {
                tangentsCount = value.Length;
                m_Tangents.GetSubArray(0, tangentsCount).CopyFrom(value);
            }
        }
        public NativeArray<Vector2> UV
        {
            get => m_UV.GetSubArray(0, uvCount);
            set
            {
                uvCount = value.Length;
                m_UV.GetSubArray(0, uvCount).CopyFrom(value);
            }
        }
        public NativeArray<Vector2> UV2
        {
            get => m_UV2.GetSubArray(0, uv2Count);
            set
            {
                uv2Count = value.Length;
                m_UV2.GetSubArray(0, uv2Count).CopyFrom(value);
            }
        }
        public NativeArray<Vector2> UV3
        {
            get => m_UV3.GetSubArray(0, uv3Count);
            set
            {
                uv3Count = value.Length;
                m_UV3.GetSubArray(0, uv3Count).CopyFrom(value);
            }
        }
        public NativeArray<Vector2> UV4
        {
            get => m_UV4.GetSubArray(0, uv4Count);
            set
            {
                uv4Count = value.Length;
                m_UV4.GetSubArray(0, uv4Count).CopyFrom(value);
            }
        }
        public NativeArray<Color> Colors
        {
            get => m_Colors.GetSubArray(0, colorsCount);
            set
            {
                colorsCount = value.Length;
                m_Colors.GetSubArray(0, colorsCount).CopyFrom(value);
            }
        }
        public NativeArray<BoneWeight> BoneWeights
        {
            get => m_BoneWeights.GetSubArray(0, boneWeightsCount);
            set
            {
                boneWeightsCount = value.Length;
                m_BoneWeights.GetSubArray(0, boneWeightsCount).CopyFrom(value);
            }
        }
        public NativeArray<Matrix4x4> Bindposes
        {
            get => m_Bindposes.GetSubArray(0, bindposesCount);
            set
            {
                bindposesCount = value.Length;
                m_Bindposes.GetSubArray(0, bindposesCount).CopyFrom(value);
            }
        }
        public int TrianglesCount => trianglesCount;
#if UNUSED
        public NativeArray<int> Triangles
        {
            get => m_Triangles.GetSubArray(0, trianglesCount);
            set
            {
                trianglesCount = value.Length;
                m_Triangles.GetSubArray(0, trianglesCount).CopyFrom(value);
            }
        }
#endif // UNUSED

        public NativeArray<int> GetTrianglesNative(int submesh)
        {
            if (submesh < m_SubmeshOffset.Length)
            {
                var start = 0;
                var stop = 0;
                GetTriangleRange(submesh, out start, out stop);
                var length = stop - start;

                return m_Triangles.GetSubArray(start, length);
            }

            return new NativeArray<int>(0, Allocator.Temp);
        }

        public void CopyFrom(WorkingMesh other)
        {
            vertexCount = other.vertexCount;
            m_Vertices.GetSubArray(0, vertexCount).CopyFrom(other.m_Vertices.GetSubArray(0, vertexCount));
            
            normalsCount = other.normalsCount;
            m_Normals.GetSubArray(0, normalsCount).CopyFrom(other.m_Normals.GetSubArray(0, normalsCount));
            
            uvCount = other.uvCount;
            m_UV.GetSubArray(0, uvCount).CopyFrom(other.m_UV.GetSubArray(0, uvCount));
        }

        public void CopyFrom(
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Vector3> normals,
            System.Collections.Generic.List<Vector4>? tangents = null,
            System.Collections.Generic.List<Vector2>? uv = null,
            System.Collections.Generic.List<Vector2>? uv2 = null,
            System.Collections.Generic.List<Vector2>? uv3 = null,
            System.Collections.Generic.List<Vector2>? uv4 = null,
            System.Collections.Generic.List<Color>? colors = null,
            System.Collections.Generic.List<Matrix4x4>? bindposes = null,
            System.Collections.Generic.List<BoneWeight>? boneWeights = null)
        {
            vertexCount = vertices.Count;
            for (int i = 0; i < vertexCount; i++)
            {
                m_Vertices[i] = vertices[i];
            }
            
            normalsCount = normals.Count;
            for (int i = 0; i < normalsCount; i++)
            {
                m_Normals[i] = normals[i];
            }

            if (tangents != null)
            {
                tangentsCount = tangents.Count;
                for (int i = 0; i < tangentsCount; i++)
                {
                    m_Tangents[i] = tangents[i];
                }
            }

            if (uv != null)
            {
                uvCount = uv.Count;
                for (int i = 0; i < uvCount; i++)
                {
                    m_UV[i] = uv[i];
                }
            }

            if (uv2 != null)
            {
                uv2Count = uv2.Count;
                for (int i = 0; i < uv2Count; i++)
                {
                    m_UV2[i] = uv2[i];
                }
            }

            if (uv3 != null)
            {
                uv3Count = uv3.Count;
                for (int i = 0; i < uv3Count; i++)
                {
                    m_UV3[i] = uv3[i];
                }
            }

            if (uv4 != null)
            {
                uv4Count = uv4.Count;
                for (int i = 0; i < uv4Count; i++)
                {
                    m_UV4[i] = uv4[i];
                }
            }

            if (colors != null)
            {
                colorsCount = colors.Count;
                for (int i = 0; i < colorsCount; i++)
                {
                    m_Colors[i] = colors[i];
                }
            }

            if (bindposes != null)
            {
                bindposesCount = bindposes.Count;
                for (int i = 0; i < bindposesCount; i++)
                {
                    m_Bindposes[i] = bindposes[i];
                }
            }

            if (boneWeights != null)
            {
                boneWeightsCount = boneWeights.Count;
                for (int i = 0; i < boneWeightsCount; i++)
                {
                    m_BoneWeights[i] = boneWeights[i];
                }
            }
        }
#endif // OPTIMISATION
        
        public Vector3[] vertices
        {
            get
            {
                return m_Vertices.Slice(0, vertexCount).ToArray();
            }
            set
            {
                vertexCount = value.Length;
                m_Vertices.Slice(0, vertexCount).CopyFrom(value);
            }
        }
        NativeArray<Vector3> m_Vertices;

        public int vertexCount
        {
            get { return m_Counts[(int)Channel.Vertices]; }
            private set { m_Counts[(int)Channel.Vertices] = value; }
        }

        public int[] triangles
        {
            get { return m_Triangles.Slice(0, trianglesCount).ToArray(); }
            set
            {
                subMeshCount = 1;
                trianglesCount = value.Length;
                SetTriangles(value, 0);
            }
        }
        NativeArray<int> m_Triangles;

        int trianglesCount
        {
            get { return m_Counts[(int)Channel.Triangles]; }
            set { m_Counts[(int)Channel.Triangles] = value; }
        }

        public Vector3[] normals
        {
            get { return m_Normals.Slice(0, normalsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    normalsCount = 0;
                }
                else
                {
                    normalsCount = value.Length;
                    m_Normals.Slice(0, normalsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector3> m_Normals;

        int normalsCount
        {
            get { return m_Counts[(int)Channel.Normals]; }
            set { m_Counts[(int)Channel.Normals] = value; }
        }

        public Vector4[] tangents
        {
            get { return m_Tangents.Slice(0, tangentsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    tangentsCount = 0;
                }
                else
                {
                    tangentsCount = value.Length;
                    m_Tangents.Slice(0, tangentsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector4> m_Tangents;

        int tangentsCount
        {
            get { return m_Counts[(int)Channel.Tangents]; }
            set { m_Counts[(int)Channel.Tangents] = value; }
        }

        public Vector2[] uv
        {
            get { return m_UV.Slice(0, uvCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uvCount = 0;
                }
                else
                {
                    uvCount = value.Length;
                    m_UV.Slice(0, uvCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV;

        int uvCount
        {
            get { return m_Counts[(int)Channel.UV]; }
            set { m_Counts[(int)Channel.UV] = value; }
        }

        public Vector2[] uv2
        {
            get { return m_UV2.Slice(0, uv2Count).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uv2Count = 0;
                }
                else
                {
                    uv2Count = value.Length;
                    m_UV2.Slice(0, uv2Count).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV2;

        int uv2Count
        {
            get { return m_Counts[(int)Channel.UV2]; }
            set { m_Counts[(int)Channel.UV2] = value; }
        }

        public Vector2[] uv3
        {
            get { return m_UV3.Slice(0, uv3Count).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uv3Count = 0;
                }
                else
                {
                    uv3Count = value.Length;
                    m_UV3.Slice(0, uv3Count).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV3;

        int uv3Count
        {
            get { return m_Counts[(int)Channel.UV3]; }
            set { m_Counts[(int)Channel.UV3] = value; }
        }

        public Vector2[] uv4
        {
            get { return m_UV4.Slice(0, uv4Count).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uv4Count = 0;
                }
                else
                {
                    uv4Count = value.Length;
                    m_UV4.Slice(0, uv4Count).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV4;

        int uv4Count
        {
            get { return m_Counts[(int)Channel.UV4]; }
            set { m_Counts[(int)Channel.UV4] = value; }
        }

        public Color[] colors
        {
            get { return m_Colors.Slice(0, colorsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    colorsCount = 0;
                }
                else
                {
                    colorsCount = value.Length;
                    m_Colors.Slice(0, colorsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Color> m_Colors;

        int colorsCount
        {
            get { return m_Counts[(int)Channel.Colors]; }
            set { m_Counts[(int)Channel.Colors] = value; }
        }

        public Color32[] colors32
        {
            get { return colors.Select(c => (Color32)c).ToArray(); }
            set { colors = value.Select(c => (Color)c).ToArray(); }
        }

        public BoneWeight[] boneWeights
        {
            get { return m_BoneWeights.Slice(0, boneWeightsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    boneWeightsCount = 0;
                }
                else
                {
                    boneWeightsCount = value.Length;
                    m_BoneWeights.Slice(0, boneWeightsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<BoneWeight> m_BoneWeights;

        int boneWeightsCount
        {
            get { return m_Counts[(int)Channel.BoneWeights]; }
            set { m_Counts[(int)Channel.BoneWeights] = value; }
        }

        public Matrix4x4[] bindposes
        {
            get { return m_Bindposes.Slice(0, bindposesCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    bindposesCount = 0;
                }
                else
                {
                    bindposesCount = value.Length;
                    m_Bindposes.Slice(0, bindposesCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Matrix4x4> m_Bindposes;

        int bindposesCount
        {
            get { return m_Counts[(int)Channel.Bindposes]; }
            set { m_Counts[(int)Channel.Bindposes] = value; }
        }

        public int subMeshCount
        {
            get { return submeshOffsetCount; }
            set
            {
                if (submeshOffsetCount == value)
                    return;

                var previousCount = submeshOffsetCount;
                submeshOffsetCount = value;
                for (var i = previousCount; i < submeshOffsetCount; i++)
                {
                    // Initialize these offsets to be invalid, so we don't use stale values
                    m_SubmeshOffset[i] = -1;
                }
            }
        }
        NativeArray<int> m_SubmeshOffset;

        int submeshOffsetCount
        {
            get { return m_Counts[(int)Channel.SubmeshOffset]; }
            set { m_Counts[(int)Channel.SubmeshOffset] = value; }
        }

        public string name
        {
            get { return Encoding.UTF8.GetString(m_Name.ToArray()); }
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = string.Empty;

                var bytes = Encoding.UTF8.GetBytes(value);
                var length = Mathf.Min(bytes.Length, k_MaxNameSize);
                m_Name.Slice(0, length).CopyFrom(bytes);
            }
        }
        NativeArray<byte> m_Name;

        // This data does not cross the job threshold, so if it needs to be read back, then it will need to be
        // in a NativeArray or some other type of NativeContainer
        public IndexFormat indexFormat { get; set; }
        public Bounds bounds { get; set; }

        NativeArray<int> m_Counts;

        // These are stubbed out for API completeness, but obviously don't do anything
        public void RecalculateBounds() { }
        public void RecalculateNormals() { }
        public void RecalculateTangents() { }

        public void SetTriangles(ReadOnlySpan<int> triangles, int submesh)
        {
            if (submesh >= subMeshCount)
                subMeshCount = submesh + 1;

            var preSliceLength = m_SubmeshOffset[submesh];
            if (preSliceLength < 0)
            {
                if (submesh > 0)
                {
                    m_SubmeshOffset[submesh] = trianglesCount;
                    preSliceLength = trianglesCount;
                }
                else
                {
                    m_SubmeshOffset[submesh] = 0;
                    preSliceLength = 0;
                }
            }
            var totalCount = preSliceLength; // count prior to submesh
            totalCount += triangles.Length; // new submesh triangle count

            var postSliceOffset = 0;
            var postSliceLength = 0;
            if (submesh < subMeshCount - 2) // count of all triangles after submesh
            {
                postSliceOffset = m_SubmeshOffset[submesh + 1];
                if (postSliceOffset >= 0)
                {
                    postSliceLength = trianglesCount - postSliceOffset;
                    totalCount += postSliceLength;
                }
            }

            trianglesCount = totalCount;

            // Shift other following triangles up/down
            if (postSliceOffset > 0)
            {
                var offset = preSliceLength + triangles.Length;
                m_SubmeshOffset[submesh + 1] = offset;
                var sourceSlice = new NativeSlice<int>(m_Triangles, postSliceOffset, postSliceLength);
                var destSlice = new NativeSlice<int>(m_Triangles, offset, postSliceLength);
                destSlice.CopyFrom(sourceSlice);
            }

            triangles.CopyTo(m_Triangles.AsSpan().Slice(preSliceLength, triangles.Length));
        }
        
        public void SetTriangles(System.Collections.Generic.List<int> triangles, int submesh)
        {
            if (submesh >= subMeshCount)
                subMeshCount = submesh + 1;

            var preSliceLength = m_SubmeshOffset[submesh];
            if (preSliceLength < 0)
            {
                if (submesh > 0)
                {
                    m_SubmeshOffset[submesh] = trianglesCount;
                    preSliceLength = trianglesCount;
                }
                else
                {
                    m_SubmeshOffset[submesh] = 0;
                    preSliceLength = 0;
                }
            }
            var totalCount = preSliceLength; // count prior to submesh
            totalCount += triangles.Count; // new submesh triangle count

            var postSliceOffset = 0;
            var postSliceLength = 0;
            if (submesh < subMeshCount - 2) // count of all triangles after submesh
            {
                postSliceOffset = m_SubmeshOffset[submesh + 1];
                if (postSliceOffset >= 0)
                {
                    postSliceLength = trianglesCount - postSliceOffset;
                    totalCount += postSliceLength;
                }
            }

            trianglesCount = totalCount;

            // Shift other following triangles up/down
            if (postSliceOffset > 0)
            {
                var offset = preSliceLength + triangles.Count;
                m_SubmeshOffset[submesh + 1] = offset;
                var sourceSlice = new NativeSlice<int>(m_Triangles, postSliceOffset, postSliceLength);
                var destSlice = new NativeSlice<int>(m_Triangles, offset, postSliceLength);
                destSlice.CopyFrom(sourceSlice);
            }

            for (int i = 0; i < triangles.Count; i++)
            {
                m_Triangles[i + preSliceLength] = triangles[i];
            }
        }

        public int[] GetTriangles(int submesh)
        {
            if (submesh < m_SubmeshOffset.Length)
            {
                var start = 0;
                var stop = 0;
                GetTriangleRange(submesh, out start, out stop);
                var length = stop - start;

                var slice = new NativeSlice<int>(m_Triangles, start, length);
                return slice.ToArray();
            }

            return new int[0];
        }

        void GetTriangleRange(int submesh, out int start, out int stop)
        {
            if (submesh < m_SubmeshOffset.Length)
            {
                start = m_SubmeshOffset[submesh];
                stop = trianglesCount;
                if (submesh < m_SubmeshOffset.Length - 1)
                    stop = m_SubmeshOffset[submesh + 1];

                return;
            }

            start = -1;
            stop = -1;
            return;
        }

        public WorkingMesh(Allocator allocator, int maxVertices, int maxTriangles, int maxSubmeshes, int maxBindposes) 
        {
#if OPTIMISATION
            m_Counts = new NativeArray<int>(ChannelCount, allocator);
#else
            m_Counts = new NativeArray<int>(Enum.GetValues(typeof(Channel)).Length, allocator);
#endif // OPTIMISATION
            m_Vertices = new NativeArray<Vector3>(maxVertices, allocator);
            m_Normals = new NativeArray<Vector3>(maxVertices, allocator);
            m_Tangents = new NativeArray<Vector4>(maxVertices, allocator);
            m_UV = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV2 = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV3 = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV4 = new NativeArray<Vector2>(maxVertices, allocator);
            m_Colors = new NativeArray<Color>(maxVertices, allocator);
            m_BoneWeights = new NativeArray<BoneWeight>(maxVertices, allocator);
            m_Bindposes = new NativeArray<Matrix4x4>(maxBindposes, allocator);
            m_Name = new NativeArray<byte>(k_MaxNameSize, allocator);
            m_SubmeshOffset = new NativeArray<int>(maxSubmeshes, allocator);
            m_Triangles = new NativeArray<int>(maxTriangles, allocator);
        }

#if OPTIMISATION
        private static readonly int ChannelCount = Enum.GetValues(typeof(Channel)).Length;
        public WorkingMesh(Allocator allocator,
            ReadOnlySpan<Vector3> vertices, ReadOnlySpan<Vector3> normals, ReadOnlySpan<Vector4> tangents,
            ReadOnlySpan<Vector2> uvs, ReadOnlySpan<Vector2> uv2, ReadOnlySpan<Vector2> uv3, ReadOnlySpan<Vector2> uv4,
            ReadOnlySpan<Color> colors, int maxTriangles, int maxSubmeshes, int maxBindposes)
        {
            int maxVertices = vertices.Length;
            m_Counts = new NativeArray<int>(ChannelCount, allocator);
            m_Vertices = new NativeArray<Vector3>(maxVertices, allocator);
            m_Normals = new NativeArray<Vector3>(maxVertices, allocator);
            m_Tangents = new NativeArray<Vector4>(maxVertices, allocator);
            m_UV = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV2 = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV3 = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV4 = new NativeArray<Vector2>(maxVertices, allocator);
            m_Colors = new NativeArray<Color>(maxVertices, allocator);
            m_BoneWeights = new NativeArray<BoneWeight>(maxVertices, allocator);
            m_Bindposes = new NativeArray<Matrix4x4>(maxBindposes, allocator);
            m_Name = new NativeArray<byte>(k_MaxNameSize, allocator);
            m_SubmeshOffset = new NativeArray<int>(maxSubmeshes, allocator);
            m_Triangles = new NativeArray<int>(maxTriangles, allocator);

            vertices.CopyTo(m_Vertices);
            normals.CopyTo(m_Normals);
            tangents.CopyTo(m_Tangents);
            uvs.CopyTo(m_UV);
            uv2.CopyTo(m_UV2);
            uv3.CopyTo(m_UV3);
            uv4.CopyTo(m_UV4);
            colors.CopyTo(m_Colors);

            vertexCount = vertices.Length;
            normalsCount = normals.Length;
            tangentsCount = tangents.Length;
            uvCount = uvs.Length;
            uv2Count = uv2.Length;
            uv3Count = uv3.Length;
            uv4Count = uv4.Length;
            colorsCount = colors.Length;
        }
#endif // OPTIMISATION

        public void Dispose()
        {
            if (m_Counts.IsCreated)
                m_Counts.Dispose();

            if (m_Vertices.IsCreated)
                m_Vertices.Dispose();

            if (m_Normals.IsCreated)
                m_Normals.Dispose();

            if (m_Tangents.IsCreated)
                m_Tangents.Dispose();

            if (m_UV.IsCreated)
                m_UV.Dispose();

            if (m_UV2.IsCreated)
                m_UV2.Dispose();

            if (m_UV3.IsCreated)
                m_UV3.Dispose();

            if (m_UV4.IsCreated)
                m_UV4.Dispose();

            if (m_Colors.IsCreated)
                m_Colors.Dispose();

            if (m_BoneWeights.IsCreated)
                m_BoneWeights.Dispose();

            if (m_Bindposes.IsCreated)
                m_Bindposes.Dispose();

            if (m_Name.IsCreated)
                m_Name.Dispose();

            if (m_SubmeshOffset.IsCreated)
                m_SubmeshOffset.Dispose();

            if (m_Triangles.IsCreated)
                m_Triangles.Dispose();

            m_detector.Dispose();
        }

        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh();
            ApplyToMesh(mesh);
            return mesh;
        }
        
        public void ApplyToMesh(Mesh mesh)
        {
            mesh.indexFormat = vertexCount > ushort.MaxValue
                ? IndexFormat.UInt32
                : indexFormat;

#if OPTIMISATION
            mesh.SetVertices(m_Vertices.GetSubArray(0, vertexCount));
            mesh.SetNormals(m_Normals.GetSubArray(0, normalsCount));
            mesh.SetTangents(m_Tangents.GetSubArray(0, tangentsCount));
            mesh.SetUVs(0, m_UV.GetSubArray(0, uvCount));
            mesh.SetUVs(1, m_UV2.GetSubArray(0, uv2Count));
            mesh.SetUVs(2, m_UV3.GetSubArray(0, uv3Count));
            mesh.SetUVs(3, m_UV4.GetSubArray(0, uv4Count));
            mesh.SetColors(m_Colors.GetSubArray(0, colorsCount));
            mesh.boneWeights = boneWeights;
            if (bindposesCount != 0)
                mesh.SetBindposes(m_Bindposes.GetSubArray(0, bindposesCount));
#else
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.uv2 = uv2;
            mesh.uv3 = uv3;
            mesh.uv4 = uv4;
            mesh.colors = colors;
            mesh.boneWeights = boneWeights;
            mesh.bindposes = bindposes;
#endif // OPTIMISATION
            mesh.subMeshCount = subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
            {
                mesh.SetIndices(GetTrianglesNative(i), MeshTopology.Triangles, i, calculateBounds: false);
            }
            mesh.name = name;
            mesh.bounds = bounds;
            
            mesh.RecalculateBounds();
        }
    }
}
