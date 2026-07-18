using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.HLODSystem.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.HLODSystem
{
    [Serializable]
    public class HLODData
    {
        [Serializable]
        public struct TextureCompressionData
        {
            [SerializeField] public TextureFormat PCTextureFormat;
            [SerializeField] public TextureFormat WebGLTextureFormat;
            [SerializeField] public TextureFormat AndroidTextureFormat;
            [SerializeField] public TextureFormat iOSTextureFormat;
            [SerializeField] public TextureFormat tvOSTextureFormat;
        }

        [Serializable]
        public struct SerializableMesh
        {
            [SerializeField] private string m_name;
            [SerializeField] private byte[] m_vertices;
            [SerializeField] private byte[] m_normals;
            [SerializeField] private byte[] m_tangents;
            [SerializeField] private byte[] m_uvs;
            [SerializeField] private byte[] m_uvs2;
            [SerializeField] private byte[] m_uvs3;
            [SerializeField] private byte[] m_uvs4;
#if UNITY_8UV_SUPPORT
            [SerializeField] private byte[] m_uvs5;
            [SerializeField] private byte[] m_uvs6;
            [SerializeField] private byte[] m_uvs7;
            [SerializeField] private byte[] m_uvs8;
#endif // UNITY_8UV_SUPPORT
            [SerializeField] private byte[] m_colors;
            [SerializeField] private List<int[]> m_indices;

            public string Name
            {
                set { m_name = value; }
                get { return m_name; }
            }
            
            private static byte[] ArrayToBytes<T>(NativeArray<T> arr)
                where T : unmanaged
                => ArrayToBytes(arr.AsReadOnlySpan());

            private static byte[] ArrayToBytes<T>(ReadOnlySpan<T> arr)
                where T : unmanaged
            {
#if OPTIMISATION
                return MemoryMarshal.AsBytes(arr).ToArray();
#else
                int dataSize = Marshal.SizeOf<T>();
                byte[] buffer = new byte[dataSize * arr.Length];

                IntPtr ptr = Marshal.AllocHGlobal(dataSize);
                for (int i = 0; i < arr.Length; ++i)
                {
                    Marshal.StructureToPtr(arr[i], ptr, false);
                    Marshal.Copy(ptr, buffer, i * dataSize, dataSize);
                }

                Marshal.FreeHGlobal(ptr);

                return buffer;
#endif // OPTIMISATION
            }

#if UNUSED
            private List<T> BytesToList<T>(Byte[] bytes,
                List<T> list)
                where T : unmanaged
            {
                int dataSize = Marshal.SizeOf<T>();
                int length = bytes.Length / dataSize;
                list.Clear();
                if (list.Capacity < length)
                    list.Capacity = length;

                IntPtr ptr = Marshal.AllocHGlobal(dataSize);
                for (int i = 0; i < length; ++i)
                {
                    Marshal.Copy(bytes, i * dataSize, ptr, dataSize);
                    list.Add(Marshal.PtrToStructure<T>(ptr));
                }

                Marshal.FreeHGlobal(ptr);

                return list;
            }
#endif // UNUSED

#if OPTIMISATION
            private List<T> BytesToList<T>(ReadOnlySpan<byte> bytes,
                List<T> list)
                where T : unmanaged
            {
                var span = MemoryMarshal.Cast<byte, T>(bytes);

                list.EnsureCount(span.Length);
                span.CopyTo(list.AsSpan());

                return list;
            }
            
            private T[] BytesToArray<T>(ReadOnlySpan<byte> bytes)
                where T : unmanaged
            {
                return MemoryMarshal.Cast<byte, T>(bytes).ToArray();
            }
#else
            private T[] BytesToArray<T>(Byte[] bytes)
                where T : unmanaged
            {
                int dataSize = Marshal.SizeOf<T>();
                T[] array = new T[bytes.Length / dataSize];

                IntPtr ptr = Marshal.AllocHGlobal(dataSize);
                for (int i = 0; i < array.Length; ++i)
                {
                    Marshal.Copy(bytes, i * dataSize, ptr, dataSize);
                    array[i] = Marshal.PtrToStructure<T>(ptr);
                }

                Marshal.FreeHGlobal(ptr);

                return array;
            }
#endif // OPTIMISATION

            public void From(WorkingMesh mesh)
            {
                m_name = mesh.name;

                m_vertices = ArrayToBytes(mesh.Vertices);
                m_normals = ArrayToBytes(mesh.Normals);
                m_tangents = ArrayToBytes(mesh.Tangents);
                m_uvs = ArrayToBytes(mesh.UV);
                m_uvs2 = ArrayToBytes(mesh.UV2);
                m_uvs3 = ArrayToBytes(mesh.UV3);
                m_uvs4 = ArrayToBytes(mesh.UV4);
#if UNITY_8UV_SUPPORT
                m_uvs5 = ArrayToBytes(mesh.UV5);
                m_uvs6 = ArrayToBytes(mesh.UV6);
                m_uvs7 = ArrayToBytes(mesh.UV7);
                m_uvs8 = ArrayToBytes(mesh.UV8);
#endif // UNITY_8UV_SUPPORT
                m_colors = ArrayToBytes(mesh.Colors);
                if (m_indices == null)
                    m_indices = new List<int[]>(mesh.subMeshCount);
                else
                    m_indices.Clear();
                for (int i = 0; i < mesh.subMeshCount; ++i)
                {
                    m_indices.Add(mesh.GetTriangles(i));
                }
            }

            public Mesh To()
            {
                Mesh mesh = new Mesh();
                mesh.name = m_name;
                if (m_vertices.Length >= ushort.MaxValue * 3 * sizeof(float))
                    mesh.indexFormat = IndexFormat.UInt32;

#if OPTIMISATION
                var v2 = new List<Vector2>();
                var v3 = new List<Vector3>();
                mesh.SetVertices(BytesToList<Vector3>(m_vertices, v3));
                mesh.SetNormals(BytesToList<Vector3>(m_normals, v3));
                mesh.tangents = BytesToArray<Vector4>(m_tangents);
                mesh.SetUVs(0, BytesToList<Vector2>(m_uvs, v2));
                mesh.SetUVs(1, BytesToList<Vector2>(m_uvs2, v2));
                mesh.SetUVs(2, BytesToList<Vector2>(m_uvs3, v2));
                mesh.SetUVs(3, BytesToList<Vector2>(m_uvs4, v2));
#if UNITY_8UV_SUPPORT
                mesh.SetUVs(4, BytesToList<Vector2>(m_uvs5, v2));
                mesh.SetUVs(5, BytesToList<Vector2>(m_uvs6, v2));
                mesh.SetUVs(6, BytesToList<Vector2>(m_uvs7, v2));
                mesh.SetUVs(7, BytesToList<Vector2>(m_uvs8, v2));
#endif // UNITY_8UV_SUPPORT
                mesh.colors = BytesToArray<Color>(m_colors);
#else
                mesh.vertices = BytesToArray<Vector3>(m_vertices);
                mesh.normals = BytesToArray<Vector3>(m_normals);
                mesh.tangents = BytesToArray<Vector4>(m_tangents);
                mesh.uv = BytesToArray<Vector2>(m_uvs);
                mesh.uv2 = BytesToArray<Vector2>(m_uvs2);
                mesh.uv3 = BytesToArray<Vector2>(m_uvs3);
                mesh.uv4 = BytesToArray<Vector2>(m_uvs4);
#if UNITY_8UV_SUPPORT
                mesh.uv5 = BytesToArray<Vector2>(m_uvs5);
                mesh.uv6 = BytesToArray<Vector2>(m_uvs6);
                mesh.uv7 = BytesToArray<Vector2>(m_uvs7);
                mesh.uv8 = BytesToArray<Vector2>(m_uvs8);
#endif // UNITY_8UV_SUPPORT
                mesh.colors = BytesToArray<Color>(m_colors);
#endif // OPTIMISATION

                mesh.subMeshCount = m_indices.Count;
                for (int i = 0; i < m_indices.Count; ++i)
                {
                    mesh.SetTriangles(m_indices[i], i);
                }

                return mesh;
            }

            public int GetSpaceUsage()
            {
                int usage = 0;
                usage += m_vertices.Length;
                usage += m_normals.Length;
                usage += m_tangents.Length;
                usage += m_uvs.Length;
                usage += m_uvs2.Length;
                usage += m_uvs3.Length;
                usage += m_uvs4.Length;
                usage += m_colors.Length;
                for ( int i = 0; i < m_indices.Count; ++i )
                {
                    usage += m_indices[i].Length * sizeof(int);
                }

                return usage;
            }
        }

        [Serializable]
        public struct SerializableTexture
        {
            [SerializeField] private string m_name;
            [SerializeField] private string m_textureName;
            [SerializeField] private GraphicsFormat m_format;
            [SerializeField] private TextureWrapMode m_wrapMode;
            [SerializeField] private int m_width;
            [SerializeField] private int m_height;
            [SerializeField] private byte[] m_bytes;

            public readonly byte[] Bytes => m_bytes;

            public string Name
            {
                set { m_name = value; }
                get { return m_name; }
            }

            public string TextureName
            {
                get { return m_textureName; }
            }

            public int Height
            {
                get { return m_height; }
            }

            public int Width
            {
                get { return m_width; }
            }

            public GraphicsFormat GraphicsFormat
            {
                get { return m_format; }
            }

            public TextureWrapMode WrapMode
            {
                get { return m_wrapMode; }
            }
            public int BytesLength
            {
                get 
                { 
                    if (m_bytes == null)
                        return 0;
                    return m_bytes.Length; 
                }
            }

            public void From(Texture2D texture)
            {
                m_textureName = texture.name;
                m_format = texture.graphicsFormat;
                m_wrapMode = texture.wrapMode;
                m_width = texture.width;
                m_height = texture.height;                
                m_bytes = texture.EncodeToPNG();
            }

            public Texture2D To()
            {
                var textureFormat = GraphicsFormatUtility.GetTextureFormat(m_format);
                var srgb = GraphicsFormatUtility.IsSRGBFormat(m_format);
                Texture2D texture = new Texture2D(m_width, m_height, textureFormat, true, !srgb);
                texture.name = m_textureName;
                texture.LoadImage(m_bytes);
                texture.wrapMode = m_wrapMode;
                texture.Apply();
                return texture;
            }
        }

        [Serializable]
        public class SerializableMaterial
        {
            [SerializeField] private string m_name = string.Empty;
            [SerializeField] private string m_id = string.Empty;
            [SerializeField] private string m_assetGuid = string.Empty;
            [SerializeField] private string m_jsonData = string.Empty;
            [SerializeField] private List<SerializableTexture> m_textures = new List<SerializableTexture>();

            public string ID
            {
                get { return m_id; }
            }

            public void AddTexture(SerializableTexture texture)
            {
#if OPTIMISATION_NULL
#else
                if (m_textures == null)
                    m_textures = new List<SerializableTexture>();
#endif // OPTIMISATION_NULL

                m_textures.Add(texture);
            }

            public int GetTextureCount()
            {
#if OPTIMISATION_NULL
#else
                if (m_textures == null)
                    return 0;
#endif // OPTIMISATION_NULL
                return m_textures.Count;
            }

            public SerializableTexture GetTexture(int index)
            {
                return m_textures[index];
            }

            public void From(WorkingMaterial material)
            {
                m_name = material.Name;
                bool needWrite = material.NeedWrite();
                if (needWrite)
                {
                    Material mat = material.ToMaterial();
                    m_jsonData = EditorJsonUtility.ToJson(mat);
                    m_assetGuid = "";
                }
                else
                {
                    m_jsonData = "";
#if UNITY_6000_3_OR_NEWER
                    string path = AssetDatabase.GetAssetPath((EntityId)material.InstanceID);
#else
                    string path  = AssetDatabase.GetAssetPath(material.InstanceID);
#endif // UNITY_6000_3_OR_NEWER
                    m_assetGuid = AssetDatabase.AssetPathToGUID(path);
                }

                m_id = material.Guid;
            }

            public Material To()
            {
                if (string.IsNullOrEmpty(m_assetGuid))
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    EditorJsonUtility.FromJsonOverwrite(m_jsonData, mat);
                    mat.name = m_name;
                    return mat;
                }
                else
                {
                    string path = AssetDatabase.GUIDToAssetPath(m_assetGuid);
                    var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int i = 0; i < objects.Length; ++i)
                    {
                        Material? mat = objects[i] as Material;
                        
                        if (mat == null)
                            continue;

                        if (mat.name == m_name)
                            return mat;
                    }
                    
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
                }
            }
        }

        [Serializable]
        public class SerializableObject
        {
            [SerializeField] private string m_name = string.Empty;
            [SerializeField] private SerializableMesh m_mesh;
            [SerializeField] private List<string> m_materialIds = new List<string>();
            [SerializeField] private List<string> m_materialNames = new List<string>();
            [SerializeField] private LightProbeUsage m_lightProbeUsage;

            public string Name
            {
                set { m_name = value; }
                get { return m_name; }
            }

            public LightProbeUsage LightProbeUsage => m_lightProbeUsage;

            public SerializableMesh GetMesh()
            {
                return m_mesh;
            }

            public List<string> GetMaterialIds()
            {
                return m_materialIds;
            }

            public List<string> GetMaterialNames()
            {
                return m_materialNames;
            }

            public void From(WorkingObject obj)
            {
                if (obj.Mesh == null)
                    return;
                Name = obj.Name;
                m_mesh.From(obj.Mesh);
                m_materialIds.Clear();
                m_materialNames.Clear();
                m_lightProbeUsage = obj.LightProbeUsage;
                foreach (WorkingMaterial wm in obj.Materials)
                {
                    m_materialIds.Add(wm.Guid);
                    m_materialNames.Add(wm.Name);
                }
            }
        }

        [Serializable]
        public struct SerializableVector3
        {
            [SerializeField]
            public float X;
            [SerializeField]
            public float Y;
            [SerializeField]
            public float Z;

            public SerializableVector3(Vector3 vector3)
            {
                X = vector3.x;
                Y = vector3.y;
                Z = vector3.z;
            }

            public Vector3 To()
            {
                return new Vector3(X, Y, Z);
            }
        }

        [Serializable]
        public struct SerializableQuaternion
        {
            [SerializeField]
            public float X;
            [SerializeField]
            public float Y;
            [SerializeField]
            public float Z;
            [SerializeField]
            public float W;

            public SerializableQuaternion(Quaternion quaternion)
            {
                X = quaternion.x;
                Y = quaternion.y;
                Z = quaternion.z;
                W = quaternion.w;
            }

            public Quaternion To()
            {
                return new Quaternion(X, Y, Z, W);
            }
        }

        [Serializable]
        public class SerializableCollider
        {
            [SerializeField]
            string m_name = string.Empty;
            [SerializeField]
            string m_type = string.Empty;
            [SerializeField]
            SerializableVector3 m_position;
            [SerializeField]
            SerializableQuaternion m_rotation;
            [SerializeField]
            SerializableVector3 m_scale;
            
            [SerializeField]
            SerializableDynamicObject m_parameters;

            public string Name
            {
                get => m_name;
                set => m_name = value;
            }

            public SerializableCollider(WorkingCollider collider)
            {
                From(collider);
                m_parameters = collider.Parameters;
            }

            public void From(WorkingCollider collider)
            {
                m_type = collider.Type;
                m_position = new SerializableVector3(collider.Position);
                m_rotation = new SerializableQuaternion(collider.Rotation);
                m_scale = new SerializableVector3(collider.Scale);
                m_parameters = collider.Parameters;
            }

            public GameObject? CreateGameObject()
            {
                if (m_type == typeof(BoxCollider).Name)
                {
                    return CreateBoxCollider();
                }

                if (m_type == typeof(MeshCollider).Name)
                {
                    return CreateMeshCollider();
                }

                if (m_type == typeof(SphereCollider).Name)
                {
                    return CreateSphereCollider();
                }

                if (m_type == typeof(CapsuleCollider).Name)
                {
                    return CreateCapsuleCollider();
                }

                return null;
            }

            private GameObject CreateBoxCollider()
            {
                dynamic param = m_parameters;
                GameObject go = new GameObject("Collider");
                var col = go.AddComponent<BoxCollider>();

                go.transform.position = m_position.To();
                go.transform.rotation = m_rotation.To();
                go.transform.localScale = m_scale.To();

                Vector3 size;
                Vector3 center;
                size.x = param.SizeX;
                size.y = param.SizeY;
                size.z = param.SizeZ;
                center.x = param.CenterX;
                center.y = param.CenterY;
                center.z = param.CenterZ;

                col.size = size;
                col.center = center;

                return go;
            }

            private GameObject? CreateMeshCollider()
            {
                dynamic param = m_parameters;
                string sharedMeshPath = param.SharedMeshPath;
                string mainAssetPath = "";
                string subAssetName = "";
                ObjectUtils.ParseObjectPath(sharedMeshPath, out mainAssetPath, out subAssetName);
                
                if (string.IsNullOrEmpty(mainAssetPath) == true)
                    return null;

                GameObject go = new GameObject("Collider");
                var col = go.AddComponent<MeshCollider>();

                go.transform.position = m_position.To();
                go.transform.rotation = m_rotation.To();
                go.transform.localScale = m_scale.To();

                if (string.IsNullOrEmpty(subAssetName) == true)
                {
                    col.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(mainAssetPath);
                }
                else
                {
                    UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(mainAssetPath);
                    for (int oi = 0; oi < objects.Length; ++oi)
                    {
                        if (objects[oi].name == subAssetName)
                        {
                            col.sharedMesh = objects[oi] as Mesh;
                            if (col.sharedMesh != null)
                            {
                                break;
                            }
                        }
                    }
                }
                
                col.convex = param.Convex;

                return go;
            }

            private GameObject CreateSphereCollider()
            {
                dynamic param = m_parameters;
                GameObject go = new GameObject("Collider");
                var col = go.AddComponent<SphereCollider>();

                go.transform.position = m_position.To();
                go.transform.rotation = m_rotation.To();
                go.transform.localScale = m_scale.To();

                Vector3 center;
                center.x = param.CenterX;
                center.y = param.CenterY;
                center.z = param.CenterZ;

                col.center = center;
                col.radius = param.Radius;

                return go;
            }

            private GameObject CreateCapsuleCollider()
            {
                dynamic param = m_parameters;
                GameObject go = new GameObject("Collider");
                var col = go.AddComponent<CapsuleCollider>();

                go.transform.position = m_position.To();
                go.transform.rotation = m_rotation.To();
                go.transform.localScale = m_scale.To();

                Vector3 center;
                center.x = param.CenterX;
                center.y = param.CenterY;
                center.z = param.CenterZ;

                col.center = center;
                col.radius = param.Radius;
                col.height = param.Height;
                col.direction = param.Direction;

                return go;
            }
            
            
        }


        public string Name
        {
            set { m_name = value; }
            get { return m_name; }
        }

        public TextureCompressionData CompressionData
        {
            set { m_compressionData = value; }
            get { return m_compressionData; }
        }

        [SerializeField] private string m_name = string.Empty;
        [SerializeField] private TextureCompressionData m_compressionData;

        [SerializeField] private List<SerializableObject> m_objects = new List<SerializableObject>();
        [SerializeField] private List<SerializableMaterial> m_materials = new List<SerializableMaterial>();
        [SerializeField] private List<SerializableCollider> m_colliders = new List<SerializableCollider>();

        public void AddFromWorkingObjects(string name, IList<WorkingObject> woList)
            => AddFromWokringObjects(name, woList);
        
        public void AddFromWokringObjects(string name, IList<WorkingObject> woList)
        {
            for (int i = 0; i < woList.Count; ++i)
            {
                WorkingObject wo = woList[i];
                if (wo.Mesh == null)
                    continue;
                SerializableObject so = new SerializableObject();
                so.From(wo);
                m_objects.Add(so);

                AddFromWorkingMaterials(wo.Materials);
            }
        }

        public void AddFromWorkingColliders(string name, IList<WorkingCollider> wcList)
        {
            for (int i = 0; i < wcList.Count; ++i)
            {
                WorkingCollider wc = wcList[i];
                SerializableCollider sc = new SerializableCollider(wc);
                sc.Name = name;
                m_colliders.Add(sc);
            }
        }
        public void AddFromGameObject(GameObject go)
        {
            if (!go.TryGetComponent(out MeshRenderer mr)
                || !go.TryGetComponent(out MeshFilter filter))
                return;

            var sharedMesh = filter.sharedMesh;
            if (sharedMesh ==null)
                return;
            
            using (WorkingObject wo = new WorkingObject(Allocator.Persistent, mr, sharedMesh))
            {
                wo.Name = go.name;

                SerializableObject so = new SerializableObject();
                so.From(wo);

                foreach (WorkingMaterial wm in wo.Materials)
                {
                    //Prevent duplication
                    if (GetMaterial(wm.Guid) != null)
                        continue;
                        
                    string[] textureNames = wm.GetTextureNames();

                    SerializableMaterial sm = new SerializableMaterial();
                    sm.From(wm);

                    for (int ti = 0; ti < textureNames.Length; ++ti)
                    {
                        WorkingTexture? tex = wm.GetTexture(textureNames[ti]);
                        if (tex == null)
                            continue;

                        SerializableTexture st = new SerializableTexture();
                        st.From(tex.ToTexture());
                        st.Name = textureNames[ti];

                        sm.AddTexture(st);
                    }

                    m_materials.Add(sm);
                }

                m_objects.Add(so);
            }
        }

        private void AddFromWorkingMaterials(IList<WorkingMaterial> wmList)
        {
            foreach (WorkingMaterial wm in wmList)
            {
                //Prevent duplication
                if (GetMaterial(wm.Guid) != null)
                    continue;

                string path = AssetDatabase.GUIDToAssetPath(wm.Guid);
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                    return;

                SerializableMaterial sm = new SerializableMaterial();
                sm.From(wm);

                string[] textureNames = wm.GetTextureNames();
                for (int ti = 0; ti < textureNames.Length; ++ti)
                {
                    WorkingTexture? wt = wm.GetTexture(textureNames[ti]);
                    if (wt == null)
                        continue;
                    SerializableTexture st = new SerializableTexture();
                    st.From(wt.ToTexture());
                    st.Name = textureNames[ti];

                    sm.AddTexture(st);
                }

                m_materials.Add(sm);
            }
        }

        public List<SerializableMaterial> GetMaterials()
        {
            return m_materials;
        }

        public List<SerializableObject> GetObjects()
        {
            return m_objects;
        }

        public List<SerializableCollider> GetColliders()
        {
            return m_colliders;
        }

        public int GetMaterialCount()
        {
#if OPTIMISATION_NULL
#else
            if (m_materials == null)
                return 0;
#endif // OPTIMISATION_NULL

            return m_materials.Count;
        }

        private SerializableMaterial? GetMaterial(string id)
        {
            for (int i = 0; i < m_materials.Count; ++i)
            {
                if (m_materials[i].ID == id)
                    return m_materials[i];
            }

            return null;
        }
    }
}