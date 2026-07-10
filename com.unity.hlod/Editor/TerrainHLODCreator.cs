using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.HLODSystem.Simplifier;
using Unity.HLODSystem.SpaceManager;
using Unity.HLODSystem.Streaming;
using Unity.HLODSystem.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Unity.HLODSystem
{
    public class TerrainHLODCreator
    {
        public static IEnumerator Create(TerrainHLOD hlod)
        {
            TerrainHLODCreator creator = new TerrainHLODCreator(hlod);
            yield return creator.CreateImpl();
        }
        public static IEnumerator Destroy(TerrainHLOD hlod)
        {
            var controller = hlod.GetComponent<HLODControllerBase>();
            if (controller == null)
                yield break;

            try
            {
                var convertedPrefabObjects = hlod.ConvertedPrefabObjects;
                for (int i = 0; i < convertedPrefabObjects.Count; ++i)
                {
                    EditorUtility.DisplayProgressBar("Destroy HLOD", "Destroying HLOD files", (float)i / convertedPrefabObjects.Count);
                    PrefabUtility.UnpackPrefabInstance(convertedPrefabObjects[i], PrefabUnpackMode.OutermostRoot,
                        InteractionMode.AutomatedAction);
                }

                
                var generatedObjects = hlod.GeneratedObjects;
                for (int i = 0; i < generatedObjects.Count; ++i)
                {
                    if (generatedObjects[i] == null)
                        continue;
                    var path = AssetDatabase.GetAssetPath(generatedObjects[i]);
                    if (string.IsNullOrEmpty(path) == false)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                    else
                    {
                        //It means scene object.
                        //Destroy it.
                        Object.DestroyImmediate(generatedObjects[i]);
                    }

                    EditorUtility.DisplayProgressBar("Destroy HLOD", "Destroying HLOD files", (float)i / (float)generatedObjects.Count);
                }
                generatedObjects.Clear();
                Object.DestroyImmediate(controller);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            EditorUtility.SetDirty(hlod.gameObject);
            EditorUtility.SetDirty(hlod);
        }



        class Layer : IDisposable
        {
            private NativeArray<int> m_detector = new NativeArray<int>(1, Allocator.Persistent);

            public float NormalScale => m_normalScale;
            
            public Layer(TerrainLayer layer, float chunkSize)
            {
                m_diffuseTextures = new DisposableList<WorkingTexture>();
                m_maskTextures = new DisposableList<WorkingTexture>();
                m_normalTextures = new DisposableList<WorkingTexture>();

                m_offset = layer.tileOffset;
                m_size = layer.tileSize;
                m_normalScale = layer.normalScale;

                m_chunkSize = chunkSize;

                MakeTexture(layer, layer.diffuseTexture, layer.diffuseRemapMin, layer.diffuseRemapMax, m_diffuseTextures);
                MakeTexture(layer, layer.maskMapTexture, layer.maskMapRemapMin, layer.maskMapRemapMax, m_maskTextures);
                MakeTexture(layer, layer.normalMapTexture, Vector4.zero, Vector4.one, m_normalTextures);
            }

#if BUGFIX
            /// <summary>
            /// If <paramref name="tex"/> is not readable, creates a readable copy.
            /// </summary>
            /// <remarks>
            /// If <see cref="Texture2D.isReadable"/> returns <see langword="false"/>,
            /// the returned <see cref="Texture2D"/> should be destroyed when no longer needed.
            /// </remarks>
            /// <param name="tex">Input <see cref="Texture2D"/>.</param>
            /// <param name="mipChain">Whether to create mipmaps on the created <see cref="Texture2D"/>.</param>
            /// <returns><see langword="false"/> if the original <paramref name="tex"/> is already readable,
            /// <see langword="true"/> if a readable copy was created.</returns>
            public static bool ReadTexture(ref Texture2D tex, bool linear, bool mipChain = false)
            {
                if (tex.isReadable && !(mipChain && tex.mipmapCount == 0))
                    return false;

                int width = tex.width;
                int height = tex.height;

                Texture2D readable;

                // No blit is required if the source texture is uncompressed and in the correct format
                var format = tex.format;
                if (format >= TextureFormat.Alpha8 && format <= TextureFormat.RGBAFloat)
                {
                    readable = new Texture2D(width, height, format,
                        mipChain, linear, createUninitialized: true);

                    Graphics.CopyTexture(tex, readable);
                    tex = readable;
                    return true;
                }

                RenderTexture rt = RenderTexture.GetTemporary(width, height, depthBuffer: 0,
                    RenderTextureFormat.Default, linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);

                Graphics.Blit(tex, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                readable = new Texture2D(width, height, TextureFormat.RGBA32,
                    mipChain, linear, createUninitialized: true);

                readable.ReadPixels(new Rect(x: 0, y: 0, width, height), destX: 0, destY: 0);
                readable.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                tex = readable;
                return true;
            }
#endif // BUGFIX

            void MakeTexture(TerrainLayer layer, Texture2D? texture, Color min, Color max, DisposableList<WorkingTexture> results)
            {
                if (texture == null)
                    return;
                
                bool linear = !GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);

                var offset = layer.tileOffset;  // TODO Unused
                var size = layer.tileSize;      // TODO Unused

                if (!linear)
                {
                    min = min.linear;
                    max = max.linear;
                }


                //make to texture readable.
                var assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                var textureImporter = assetImporter as TextureImporter;
                TextureImporterType type = TextureImporterType.Default;

                if (textureImporter)
                {
                    type = textureImporter.textureType;
                    textureImporter.isReadable = true;
                    textureImporter.textureType = TextureImporterType.Default;
                    textureImporter.SaveAndReimport();
                }

                bool dispose = false;

                try
                {
#if BUGFIX
                    dispose = ReadTexture(ref texture, linear, mipChain: true);
#endif // BUGFIX

                    int textureWidth = texture.width;
                    int textureHeight = texture.height;
                    var format = texture.format;
                    int mipmapCount = texture.mipmapCount;
                    results.EnsureCapacity(mipmapCount);
                    for (int i = 0; i < mipmapCount; ++i)
                    {
                        int width = textureWidth >> i;
                        int height = textureHeight >> i;
                        WorkingTexture workingTexture = new WorkingTexture(Allocator.Persistent, format, width, height, linear);
                        Color[] colors = texture.GetPixels(i);
                        for (int y = 0; y < height; ++y)
                        {
                            for (int x = 0; x < width; ++x)
                            {
                                workingTexture.SetPixel(x, y, colors[y * width + x] * max + min);
                            }
                        }

#if OPTIMISATION // Remap in SetPixel()
#else
                        RemapTexture(workingTexture, min, max);
#endif // OPTIMISATION
                        results.Add(workingTexture);
                    }
                }
                finally
                {
                    if (textureImporter)
                    {
                        textureImporter.isReadable = false;
                        textureImporter.textureType = type;
                        textureImporter.SaveAndReimport();
                    }

#if BUGFIX
                    if (dispose)
                    {
                        Object.DestroyImmediate(texture);
                    }
#endif // BUGFIX
                }
            }
            
            public void Dispose()
            {
                m_diffuseTextures.Dispose();
                m_maskTextures.Dispose();
                m_normalTextures.Dispose();
                m_detector.Dispose();
            }

            /// <remarks>Background thread</remarks>
            public Color GetColor(float u, float v, int mipLevel = 0)
            {
                u = u - Mathf.Floor(u);
                v = v - Mathf.Floor(v);

                mipLevel = Mathf.Min(mipLevel, m_diffuseTextures.Count - 1);
                
                return m_diffuseTextures[mipLevel].GetPixel(u, v);
            }

            /// <remarks>Background thread</remarks>
            public Vector3 GetUVByWorld(float wx, float wz, float sx, float sz)
            {
                float u = (wx + m_offset.x) / m_size.x;
                float v = (wz + m_offset.y) / m_size.y;

                float mipx = Mathf.Max(0, sx / (m_chunkSize * 2.0f) - 1);
                float mipy = Mathf.Max(0, sz / (m_chunkSize * 2.0f) - 1);

                float mip = Mathf.Max(mipx, mipy);

                return new Vector3(u, v, mip);
            }

            /// <remarks>Background thread</remarks>
            public Color GetColorByWorld(float wx, float wz, float sx, float sz)
            {
                Vector3 uv = GetUVByWorld(wx, wz, sx, sz);

                return GetColor(uv.x, uv.y, Mathf.RoundToInt(uv.z));
            }

            /// <remarks>Background thread</remarks>
            public Color GetMask(float u, float v, int mipLevel = 0)
            {
                u = u - Mathf.Floor(u);
                v = v - Mathf.Floor(v);

                mipLevel = Mathf.Min(mipLevel, m_diffuseTextures.Count - 1);

                return m_maskTextures[mipLevel].GetPixel(u, v);
            }

            /// <remarks>Background thread</remarks>
            public Color GetMaskByWorld(float wx, float wz, float sx, float sz)
            {
                Vector3 uv = GetUVByWorld(wx, wz, sx, sz);

                return GetMask(uv.x, uv.y, Mathf.RoundToInt(uv.z));
            }

            /// <remarks>Background thread</remarks>
            public Color GetNormal(float u, float v, int mipLevel = 0)
            {
                u = u - Mathf.Floor(u);
                v = v - Mathf.Floor(v);

                mipLevel = Mathf.Min(mipLevel, m_normalTextures.Count - 1);

                return m_normalTextures[mipLevel].GetPixel(u, v);
            }

            /// <remarks>Background thread</remarks>
            public Color GetNormalByWorld(float wx, float wz, float sx, float sz)
            {
                Vector3 uv = GetUVByWorld(wx, wz, sx, sz);

                return GetNormal(uv.x, uv.y, Mathf.RoundToInt(uv.z));
            }

#if UNUSED
            private WorkingTexture GenerateMipmap(WorkingTexture source)
            {
                int sx = Mathf.Max(source.Width >> 1, 1);
                int sy = Mathf.Max(source.Height >> 1, 1);
                
                WorkingTexture mipmap = new WorkingTexture(Allocator.Persistent, source.Format, sx, sy, source.Linear);
                mipmap.Name = source.Name;

                for (int y = 0; y < sy; ++y)
                {
                    for (int x = 0; x < sx; ++x)
                    {
                        Color color = new Color();

                        int x1 = Mathf.Min(x * 2 + 0, source.Width -1);
                        int x2 = Mathf.Min(x * 2 + 1, source.Width - 1);
                        int y1 = Mathf.Min(y * 2 + 0, source.Height - 1);
                        int y2 = Mathf.Min(y * 2 + 1, source.Height - 1);

                        color += source.GetPixel(x1, y1);
                        color += source.GetPixel(x1, y2);
                        color += source.GetPixel(x2, y1);
                        color += source.GetPixel(x2, y2);

                        color /= 4;

                        mipmap.SetPixel(x, y, color);
                    }
                }

                return mipmap;
            }

            private void RemapTexture(WorkingTexture source, Color min, Color max)
            {
#if OPTIMISATION
                for (int i = 0, length = source.Width * source.Height; i < length; i++)
                {
                    source[i] = source[i] * max + min;
                }
#else
                for (int y = 0; y < source.Height; ++y)
                {
                    for (int x = 0; x < source.Width; ++x)
                    {
                        var color = source.GetPixel(x, y);
                        color = color * max + min;
                        source.SetPixel(x, y, color);
                    }
                }
#endif // OPTIMISATION
            }
#endif // UNUSED

            private DisposableList<WorkingTexture> m_diffuseTextures;
            private DisposableList<WorkingTexture> m_maskTextures;
            private DisposableList<WorkingTexture> m_normalTextures;
            private Vector2 m_offset;
            private Vector2 m_size;
            private float m_chunkSize;
            private float m_normalScale;
        }

      

        private TerrainHLOD m_hlod;

#if OPTIMISATION_NULL
#else
        private JobQueue m_queue;
        private Heightmap m_heightmap;

        private Vector3 m_size;
        private DisposableList<WorkingTexture> m_alphamaps;
        private DisposableList<Layer> m_layers;

        private Material m_terrainMaterial;
#endif // OPTIMISATION_NULL
        private static int m_terrainMaterialInstanceId;
        private static string m_terrainMaterialName = string.Empty;

#if OPTIMISATION_NULL
#else
        private Material m_terrainMaterialLow;
#endif // OPTIMISATION_NULL
        private static int m_terrainMaterialLowInstanceId;
        private static string m_terrainMaterialLowName = string.Empty;

        private TerrainHLODCreator(TerrainHLOD hlod)
        {
            m_hlod = hlod;
        }

        private static Heightmap CreateSubHightmap(Bounds bounds, Vector3 m_size, Heightmap m_heightmap)
            => CreateSubHeightmap(bounds, m_size, m_heightmap);

        private static Heightmap CreateSubHeightmap(Bounds bounds,
            Vector3 m_size, Heightmap m_heightmap)
        {
            int beginX = Mathf.RoundToInt(bounds.min.x / m_size.x * (m_heightmap.Width-1));
            int beginZ = Mathf.RoundToInt(bounds.min.z / m_size.z * (m_heightmap.Height-1));
            int endX = Mathf.RoundToInt(bounds.max.x / m_size.x * (m_heightmap.Width-1));
            int endZ = Mathf.RoundToInt(bounds.max.z / m_size.z * (m_heightmap.Height-1));

            int width = endX - beginX + 1;
            int height = endZ - beginZ + 1;

            return m_heightmap.GetHeightmap(beginX, beginZ, width, height);
        }
        private static WorkingObject CreateBakedTerrain(string name, Bounds bounds, Heightmap heightmap, int distance, bool isLeaf,
            Vector3 m_size, JobQueue m_queue, DisposableList<WorkingTexture> m_alphamaps, DisposableList<Layer> m_layers,
            TerrainHLOD m_hlod)
        {
            WorkingObject wo = new WorkingObject(Allocator.Persistent);
            wo.Name = name;
            wo.LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            
            m_queue.EnqueueJob(() =>
            {
                WorkingMesh mesh = CreateBakedGeometry(name, heightmap, bounds, distance, m_hlod);
                wo.SetMesh(mesh);
            });

            m_queue.EnqueueJob(() =>
            {
                WorkingMaterial material = CreateBakedMaterial(name, bounds, isLeaf, m_size, m_queue, m_alphamaps, m_layers, m_hlod);
                lock (WorkingObject.LockObject)
                {
                    wo.Materials.Add(material);
                }
            });


            return wo;
        }

        /// <remarks>Background thread</remarks>
        private static WorkingMesh CreateBakedGeometry(string name, Heightmap heightmap, Bounds bounds, int distance,
            TerrainHLOD m_hlod)
        {
            int borderWidth = CalcBorderWidth(heightmap, distance, m_hlod);
            int borderWidth2x = borderWidth * 2;
            
            WorkingMesh mesh =
                new WorkingMesh(Allocator.Persistent, heightmap.Width * heightmap.Height,
                    (heightmap.Width - borderWidth2x - 1) * (heightmap.Height - borderWidth2x - 1) * 6, 1, 0);

            mesh.name = name + "_Mesh";


            Vector3[] vertices =  new Vector3[(heightmap.Width - borderWidth2x) * (heightmap.Height - borderWidth2x)];
            Vector3[] normals = new Vector3[(heightmap.Width - borderWidth2x) * (heightmap.Height - borderWidth2x)];
            Vector2[] uvs = new Vector2[(heightmap.Width - borderWidth2x) * (heightmap.Height - borderWidth2x)];
            var triangles = new NativeArray<int>(
                (heightmap.Width - borderWidth2x - 1) * (heightmap.Height - borderWidth2x - 1) * 6,
                Allocator.TempJob, NativeArrayOptions.UninitializedMemory);


            int vi = 0;
            //except border line
            for (int z = borderWidth; z < heightmap.Height -borderWidth; ++z)
            {
                for (int x = borderWidth; x < heightmap.Width -borderWidth; ++x)
                {
                    int index = vi++;

                    vertices[index].x = bounds.size.x * (x) / (heightmap.Width - 1) + bounds.min.x;
                    vertices[index].y = heightmap.Size.y * heightmap[z, x];
                    vertices[index].z = bounds.size.z * (z) / (heightmap.Height - 1) + bounds.min.z;

                    uvs[index].x = (float)x / (heightmap.Width - 1);
                    uvs[index].y = (float)z / (heightmap.Height - 1);
                    
                    normals[index] = heightmap.GetInterpolatedNormal(uvs[index].x, uvs[index].y);
                    
                    
                }
            }

            int ii = 0;
            for (int z = 0; z < heightmap.Height - borderWidth2x - 1; ++z)
            {
                for (int x = 0; x < heightmap.Width - borderWidth2x - 1; ++x)
                {
                    int i00 = z * (heightmap.Width -borderWidth2x)+ x;
                    int i10 = z * (heightmap.Width -borderWidth2x)+ x + 1;
                    int i01 = (z + 1) * (heightmap.Width -borderWidth2x)+ x;
                    int i11 = (z + 1) * (heightmap.Width -borderWidth2x)+ x + 1;

                    triangles[ii + 0] = i00;
                    triangles[ii + 1] = i11;
                    triangles[ii + 2] = i10;
                    triangles[ii + 3] = i11;
                    triangles[ii + 4] = i00;
                    triangles[ii + 5] = i01;
                    ii += 6;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            triangles.Dispose();

            return mesh;
        }

        /// <remarks>Background thread</remarks>
        private static WorkingMaterial CreateBakedMaterial(string name, Bounds bounds, bool useHighMaterial,
            Vector3 m_size, JobQueue m_queue, DisposableList<WorkingTexture> m_alphamaps, DisposableList<Layer> m_layers,
            TerrainHLOD m_hlod)
        {
            int matInstanceID = useHighMaterial ? m_terrainMaterialInstanceId : m_terrainMaterialLowInstanceId;
            string matName = useHighMaterial ? m_terrainMaterialName : m_terrainMaterialLowName;

            WorkingMaterial material = new WorkingMaterial(Allocator.Persistent, matInstanceID, matName);
            material.Name = name + "_Material";

            m_queue.EnqueueJob(() =>
            {
                WorkingTexture albedo = BakeAlbedo(name, bounds, m_hlod.TextureSize, m_size, m_queue, m_alphamaps, m_layers);
                material.AddTexture(m_hlod.AlbedoPropertyName, albedo);
            });

            if (m_hlod.UseNormal)
            {
                m_queue.EnqueueJob(() =>
                {
                    WorkingTexture normal = BakeNormal(name, bounds, m_hlod.TextureSize, m_size, m_queue, m_alphamaps, m_layers);
                    material.AddTexture(m_hlod.NormalPropertyName, normal);
                });
            }

            if (m_hlod.UseMask)
            {
                m_queue.EnqueueJob(() =>
                {
                    WorkingTexture mask = BakeMask(name, bounds, m_hlod.TextureSize, m_size, m_queue, m_alphamaps, m_layers);
                    material.AddTexture(m_hlod.MaskPropertyName, mask);
                });
            }

            return material;
        }

        /// <remarks>Background thread</remarks>
        static
        private Color UnPackNormal(Color c, float scale)
        {
#if BUGFIX // Handle DXT5nm normal maps
            if (c.r >= 1)
            {
                c.r = (c.a * 2 - 1) * scale;
                c.g = (c.g * 2 - 1) * scale;
                var dot = c.r * c.r + c.g * c.g;
                c.b = (float)Math.Sqrt(1.0 - Mathf.Clamp01(dot)) * 0.5f + 0.5f;
                return c;
            }
#endif // BUGFIX

            c.r = (c.r * 2 - 1) * scale;
            c.g = (c.g * 2 - 1) * scale;
            c.b = c.b * 2 - 1;
            return c;
        }

        /// <remarks>Background thread</remarks>
        static
        private Color PackNormal(Color c)
        {
            c.r = c.r * 0.5f + 0.5f;
            c.g = c.g * 0.5f + 0.5f;
            c.b = c.b * 0.5f + 0.5f;
            return c;
        }

        /// <remarks>Background thread</remarks>
        static
        private void EnqueueBlendTextureJob(WorkingTexture texture, Bounds bounds, int resolution,
            Vector3 m_size, JobQueue m_queue, DisposableList<WorkingTexture> m_alphamaps, DisposableList<Layer> m_layers,
            System.Func<int, float, float, float, float, bool, Color> getColor, System.Func<Color, Color>? packColor = null)
        {
            bool linear = texture.Linear;

            m_queue.EnqueueJob(() =>
            {
                var boundsSize = bounds.size;
                var boundsMin = bounds.min;
                var boundsMax = bounds.max;
                float ustart = (boundsMin.x) / m_size.x;
                float vstart = (boundsMin.z) / m_size.z;
                float usize = (boundsMax.x - boundsMin.x) / m_size.x;
                float vsize = (boundsMax.z - boundsMin.z) / m_size.z;

                for (int y = 0; y < resolution; ++y)
                {
                    for (int x = 0; x < resolution; ++x)
                    {
                        float u = (float)x / (float)resolution * usize + ustart;
                        float v = (float)y / (float)resolution * vsize + vstart;

                        Color color = new Color(0.0f, 0.0f, 0.0f, 0.0f);

                        for (int li = 0; li < m_layers.Count; ++li)
                        {
                            float weight = 0.0f;
                            switch (li % 4)
                            {
                                case 0:
                                    weight = m_alphamaps[li / 4].GetPixel(u, v).r;
                                    break;
                                case 1:
                                    weight = m_alphamaps[li / 4].GetPixel(u, v).g;
                                    break;
                                case 2:
                                    weight = m_alphamaps[li / 4].GetPixel(u, v).b;
                                    break;
                                case 3:
                                    weight = m_alphamaps[li / 4].GetPixel(u, v).a;
                                    break;
                            }

                            //optimize to skip unaffected pixels.
                            if (weight < 0.01f)
                                continue;

                            float wx = (float)x / (float)resolution * boundsSize.x + boundsMin.x;
                            float wy = (float)y / (float)resolution * boundsSize.z + boundsMin.z;

                            Color c = getColor(li, wx, wy, boundsSize.x, boundsSize.z, linear);

                            // blend in linear space.
                            color.r += c.r * weight;
                            color.g += c.g * weight;
                            color.b += c.b * weight;
                            color.a += c.a * weight;
                        }

                        if (packColor != null)
                        {
                            color = packColor(color);
                        }

                        if (!linear)
                        {
                            color = color.gamma;
                        }

                        texture.SetPixel(x, y, color);
                    }
                }
            });
        }

        /// <remarks>Background thread</remarks>
        private static WorkingTexture BakeAlbedo(string name, Bounds bounds, int resolution,
            Vector3 m_size, JobQueue m_queue, DisposableList<WorkingTexture> m_alphamaps, DisposableList<Layer> m_layers)
        {
            WorkingTexture albedoTexture = new WorkingTexture(Allocator.Persistent, TextureFormat.RGB24, resolution, resolution, false);
            albedoTexture.Name = name + "_Albedo";
            albedoTexture.WrapMode = TextureWrapMode.Clamp;

            EnqueueBlendTextureJob(albedoTexture, bounds, resolution, m_size, m_queue, m_alphamaps, m_layers, (layer, wx, wz, sx, sz, linear) => 
            {
                Color c = m_layers[layer].GetColorByWorld(wx, wz, sx, sz);
                if (!linear)
                    c = c.linear;
                return c;
            });

            
            return albedoTexture;
        }

        /// <remarks>Background thread</remarks>
        private static WorkingTexture BakeMask(string name, Bounds bounds, int resolution,
            Vector3 m_size, JobQueue m_queue, DisposableList<WorkingTexture> m_alphamaps, DisposableList<Layer> m_layers)
        {
            WorkingTexture maskTexture = new WorkingTexture(Allocator.Persistent, TextureFormat.ARGB32, resolution, resolution, false);
            maskTexture.Name = name + "_Mask";
            maskTexture.WrapMode = TextureWrapMode.Clamp;

            EnqueueBlendTextureJob(maskTexture, bounds, resolution, m_size, m_queue, m_alphamaps, m_layers, (layer, wx, wz, sx, sz, linear) =>
            {
                Color c = m_layers[layer].GetMaskByWorld(wx, wz, sx, sz);
                if (!linear)
                    c = c.linear;
                return c;
            });

            return maskTexture;
        }

        /// <remarks>Background thread</remarks>
        private static WorkingTexture BakeNormal(string name, Bounds bounds, int resolution,
            Vector3 m_size, JobQueue m_queue, DisposableList<WorkingTexture> m_alphamaps, DisposableList<Layer> m_layers)
        {
            WorkingTexture normalTexture = new WorkingTexture(Allocator.Persistent, TextureFormat.RGB24, resolution, resolution, true);
            normalTexture.Name = name + "_Normal";
            normalTexture.WrapMode = TextureWrapMode.Clamp;

            EnqueueBlendTextureJob(normalTexture, bounds, resolution, m_size, m_queue, m_alphamaps, m_layers, (layer, wx, wz, sx, sz, linear) =>
            {
                Color c = m_layers[layer].GetNormalByWorld(wx, wz, sx, sz);
                c = UnPackNormal(c, m_layers[layer].NormalScale);
                return c;
            },(c) =>
            {
                Vector3 n = new Vector3(c.r, c.g, c.b);
                n.Normalize();
                c = new Color(n.x, n.y, n.z);
                return PackNormal(c);
            });
            
            return normalTexture;
        }


        /// <remarks>Background thread</remarks>
        private static List<Vector2Int> GetEdgeList(List<int> tris,
            HashSet<Vector2Int> candidates, List<Vector2Int> list)
        {
            list.Clear();
            list.AddRange(GetEdgeList(tris, candidates));
            return list;
        }

        /// <remarks>Background thread</remarks>
        private static HashSet<Vector2Int> GetEdgeList(List<int> tris,
            HashSet<Vector2Int> candidates)
        {
            candidates.Clear();

            int trisCount = tris.Count / 3;

            Span<Vector2Int> edges = stackalloc Vector2Int[3];

            for (int i = 0; i < trisCount; ++i)
            {
                edges[0] = new Vector2Int(tris[i * 3 + 0], tris[i * 3 + 1]);
                edges[1] = new Vector2Int(tris[i * 3 + 1], tris[i * 3 + 2]);
                edges[2] = new Vector2Int(tris[i * 3 + 2], tris[i * 3 + 0]);

                for (int ei = 0; ei < edges.Length; ++ei)
                {
                    Vector2Int otherSideEdge = new Vector2Int(edges[ei].y, edges[ei].x);
                    if (!candidates.Remove(otherSideEdge))
                    {
                        _ =
                        candidates.Add(edges[ei]);
                    }
                }
            }
            
            return candidates;
        }

        struct BorderVertex
        {
            public Vector3 Pos;
            public int ClosestIndex;
        }

        /// <remarks>Background thread</remarks>
        private static List<BorderVertex> GenerateBorderVertices(Heightmap heightmap, int borderCount,
            List<BorderVertex> borderVertices)
        {
            //generate border vertices
            borderVertices.Clear();
            int capacity = (heightmap.Width + heightmap.Height) * 2;
            if (borderVertices.Capacity < capacity)
                borderVertices.Capacity = capacity;

            int xBorderOffset = Mathf.Max((heightmap.Width - 1) / borderCount, 1 );    //< avoid 0
            int yBorderOffset = Mathf.Max((heightmap.Height - 1) / borderCount, 1);    //< avoid 0
            
            //upside
            for (int i = 0; i < heightmap.Width-1; i += xBorderOffset)
            {
                float h = heightmap[0, i];

                BorderVertex v;
                v.Pos.x = (heightmap.Size.x * i) / (heightmap.Width-1);
                v.Pos.y = (heightmap.Size.y * h);
                v.Pos.z = 0.0f;
                v.Pos += heightmap.Offset;

                v.ClosestIndex = -1;

                borderVertices.Add(v);
            }

            //rightside
            for (int i = 0; i < heightmap.Height-1; i += yBorderOffset)
            {
                float h = heightmap[i, heightmap.Width - 1];

                BorderVertex v;
                v.Pos.x = heightmap.Size.x;
                v.Pos.y = (heightmap.Size.y * h);
                v.Pos.z = (heightmap.Size.z * i) / (heightmap.Height-1);
                v.Pos += heightmap.Offset;

                v.ClosestIndex = -1;

                borderVertices.Add(v);
            }

            //downside
            for (int i = heightmap.Width-1; i > 0; i -= xBorderOffset)
            {
                float h = heightmap[heightmap.Height - 1, i];

                BorderVertex v;
                v.Pos.x = (heightmap.Size.x * i) / (heightmap.Width-1);
                v.Pos.y = (heightmap.Size.y * h);
                v.Pos.z = heightmap.Size.z;
                v.Pos += heightmap.Offset;

                v.ClosestIndex = -1;

                borderVertices.Add(v);
            }

            //leftside
            for (int i = heightmap.Height - 1; i > 0; i -= yBorderOffset)
            {
                float h = heightmap[i, 0];

                BorderVertex v;
                v.Pos.x = 0.0f;
                v.Pos.y = (heightmap.Size.y * h);
                v.Pos.z = (heightmap.Size.z * i) / (heightmap.Height-1);
                v.Pos += heightmap.Offset;

                v.ClosestIndex = -1;

                borderVertices.Add(v);
            }

            return borderVertices;
        }
        
        /// <remarks>Background thread</remarks>
        private WorkingMesh MakeBorder(WorkingMesh? source, Heightmap? heightmap, int borderCount,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<List<int>> subMeshTris,
            HashSet<int> vertexIndces, List<BorderVertex> borderVertices, HashSet<Vector2Int> candidates)
        {
#if BUGFIX
            if (source == null)
                throw new NullReferenceException(nameof(source));
            if (heightmap == null)
                throw new NullReferenceException(nameof(heightmap));

            subMeshTris.Clear();
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            vertices.AddRange(source.Vertices);
            normals.AddRange(source.Normals);
            uvs.AddRange(source.UV);
#else
            List<Vector3> vertices = source.vertices.ToList();
            List<Vector3> normals = source.normals.ToList();
            List<Vector2> uvs = source.uv.ToList();
            List<int[]> subMeshTris = new List<int[]>();
#endif // OPTIMISATION

            int maxTris = 0;

            for (int si = 0; si < source.subMeshCount; ++si)
            {
                var tris = new List<int>(source.GetTrianglesNative(si));
                _ = GetEdgeList(tris, candidates);
                vertexIndces.Clear();

                foreach (var edge in candidates)
                {
                    vertexIndces.Add(edge.x);
                    vertexIndces.Add(edge.y);
                }
                
                _ = GenerateBorderVertices(heightmap, borderCount, borderVertices);
                
                //calculate closest vertex from border vertices.
                for (int i = 0; i < borderVertices.Count; ++i)
                {
                    float closestDistance = Single.MaxValue;
                    BorderVertex v = borderVertices[i];
                    foreach (var index in vertexIndces)
                    {
                        Vector3 pos = vertices[index];
                        float dist = Vector3.SqrMagnitude(pos - borderVertices[i].Pos);
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            v.ClosestIndex = index;
                        }
                    }

                    borderVertices[i] = v;
                }
                
                //generate tris
                int startAddIndex = vertices.Count;
                for (int bi = 0; bi < borderVertices.Count; ++bi)
                {
                    int next = (bi == borderVertices.Count - 1) ? 0 : bi + 1;
                    
                    tris.Add(bi + startAddIndex);
                    tris.Add(borderVertices[bi].ClosestIndex);
                    tris.Add(next + startAddIndex);

                    Vector2 uv;
                    uv.x = (borderVertices[bi].Pos.x - heightmap.Offset.x) / heightmap.Size.x;
                    uv.y = (borderVertices[bi].Pos.z - heightmap.Offset.z) / heightmap.Size.z;
                    vertices.Add(borderVertices[bi].Pos);
                    
                    normals.Add(heightmap.GetInterpolatedNormal(uv.x, uv.y));
                    
                    uvs.Add(uv);
                    
                    if (borderVertices[bi].ClosestIndex == borderVertices[next].ClosestIndex)
                        continue;
                    
                    tris.Add(borderVertices[bi].ClosestIndex);
                    tris.Add(borderVertices[next].ClosestIndex);
                    tris.Add(next + startAddIndex);


                }

                maxTris += tris.Count;
                subMeshTris.Add(tris);
            }

            WorkingMesh mesh = new WorkingMesh(Allocator.Persistent, vertices.Count, maxTris, subMeshTris.Count, 0);
            mesh.name = source.name;
#if OPTIMISATION
            mesh.CopyFrom(vertices, normals, uv: uvs);
#else
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
#endif // OPTIMISATION

            for (int i = 0; i < subMeshTris.Count; ++i)
            {
                mesh.SetTriangles(subMeshTris[i], i);
            }

            return mesh;
        }

        /// <remarks>Background thread</remarks>
        private static void ReampUV(WorkingMesh mesh, Heightmap heightmap)
            => RemapUV(mesh, heightmap);

        /// <remarks>Background thread</remarks>
        private static void RemapUV(WorkingMesh mesh, Heightmap heightmap)
        {
#if OPTIMISATION
            var vertices = mesh.Vertices;
            var uvs = mesh.UV;
#else
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
#endif // OPTIMISATION

#if ZERO
            for (int j = 0; j < UnityMeshSimplifier.MeshUtils.UVChannelCount; ++j)
            {
                var uvs = mesh[j];
                if (!uvs.IsCreated || uvs.Length < mesh.vertexCount)
                    continue;

                for (int i = 0, vertexCount = mesh.vertexCount; i < vertexCount; ++i)
                {
                    Vector2 uv;
                    uv.x = (vertices[i].x - heightmap.Offset.x) / heightmap.Size.x;
                    uv.y = (vertices[i].z - heightmap.Offset.z) / heightmap.Size.z;
                    uvs[i] = uv;
                    //vertices[i].
                }
            }
#else
            for (int i = 0, vertexCount = mesh.vertexCount; i < vertexCount; ++i)
            {
                Vector2 uv;
                uv.x = (vertices[i].x - heightmap.Offset.x) / heightmap.Size.x;
                uv.y = (vertices[i].z - heightmap.Offset.z) / heightmap.Size.z;
                uvs[i] = uv;
                //vertices[i].
            }
#endif // ZERO

#if OPTIMISATION
#else
            mesh.uv = uvs;
#endif // OPTIMISATION
        }
        private static int CalcBorderWidth(Heightmap heightmap, int distance,
            TerrainHLOD m_hlod)
        {
            if (m_hlod.SimplifierType == typeof(Simplifier.None))
            {
                return 1;
            }
            dynamic options = m_hlod.SimplifierOptions;
            
            int maxPolygonCount = options.SimplifyMaxPolygonCount;
            int minPolygonCount = options.SimplifyMinPolygonCount;
            float polygonRatio = options.SimplifyPolygonRatio;
            int triangleCount = (heightmap.Width - 1) * (heightmap.Height - 1) * 2;

            float maxQuality = Mathf.Min((float) maxPolygonCount / (float) triangleCount, polygonRatio);
            float minQuality = Mathf.Max((float) minPolygonCount / (float) triangleCount, 0.0f);
            
            var ratio = maxQuality * Mathf.Pow(polygonRatio, distance);
            ratio = Mathf.Max(ratio, minQuality);

            int expectPolygonCount = (int)(triangleCount * ratio);

            float areaSize = (heightmap.Size.x * heightmap.Size.z); 
            float sourceSizePerTri = areaSize/  triangleCount;
            float targetSizePerTri = areaSize / expectPolygonCount;
            float sizeRatio = targetSizePerTri / sourceSizePerTri;
            float sizeRatioSqrt = Mathf.Sqrt(sizeRatio);
            
            //sizeRatioSqrt is little bit big I think.
            //So I adjust the value by divide 2.
            return Mathf.Max((int) sizeRatioSqrt / 2, 1);
        }
        

        public class EdgeGroup
        {
            public int Begin = -1;
            public int End = -1;
            public List<Vector2Int> EdgeList = new List<Vector2Int>();
        }

        /// <remarks>Background thread</remarks>
        private WorkingMesh MakeFillHoleMesh(WorkingMesh source,
            List<List<int>> newTris, List<Vector2Int> edgeList,
            List<EdgeGroup> groups, HashSet<Vector2Int> candidates)
        {
            int totalTris = 0;
            newTris.Clear();
            
            for (int si = 0; si < source.subMeshCount; ++si)
            {
                var tris = new List<int>(source.GetTrianglesNative(si));
                _ = GetEdgeList(tris, candidates, edgeList);
                
                int groupsCount = groups.Count;
                if (groupsCount > edgeList.Count)
                {
                    groups.RemoveRange(groupsCount, edgeList.Count - groupsCount);
                    UnityEngine.Assertions.Assert.AreEqual(edgeList.Count, groups.Count);
                    groupsCount = edgeList.Count;
                }

                for (int i = 0; i < groupsCount; ++i)
                {
                    groups[i].Begin = edgeList[i].x;
                    groups[i].End = edgeList[i].y;
                    groups[i].EdgeList.Clear();
                    groups[i].EdgeList.Add(edgeList[i]);
                }

                for (int i = groupsCount; i < edgeList.Count; ++i)
                {
                    EdgeGroup group = new EdgeGroup();
                    group.Begin = edgeList[i].x;
                    group.End = edgeList[i].y;
                    group.EdgeList.Add(edgeList[i]);
                
                    groups.Add(group);
                }

                bool isFinish = false;

                while (isFinish == false)
                {
                    isFinish = true;

                    for (int gi1 = 0; gi1 < groups.Count; ++gi1)
                    {
                        for (int gi2 = gi1 + 1; gi2 < groups.Count; ++gi2)
                        {
                            EdgeGroup g1 = groups[gi1];
                            EdgeGroup g2 = groups[gi2];

                            if (g1.End == g2.Begin)
                            {
                                g1.End = g2.End;
                                g1.EdgeList.AddRange(g2.EdgeList);

                                groups[gi2] = groups[groups.Count - 1];
                                groups.RemoveAt(groups.Count - 1);

                                gi2 -= 1;
                                isFinish = false;
                            }
                            else if (g1.Begin == g2.End)
                            {
                                g2.End = g1.End;
                                g2.EdgeList.AddRange(g1.EdgeList);

                                groups[gi1] = groups[gi2];
                                groups[gi2] = groups[groups.Count - 1];
                                groups.RemoveAt(groups.Count - 1);

                                gi2 -= 1;
                                isFinish = false;
                            }
                        }
                    }
                }

                for (int gi = 0; gi < groups.Count; ++gi)
                {
                    EdgeGroup group = groups[gi];
                    for (int ei1 = 1; ei1 < group.EdgeList.Count-1; ++ei1)
                    {
                        for (int ei2 = ei1 + 1; ei2 < group.EdgeList.Count; ++ei2)
                        {
                            if (group.EdgeList[ei1].x == group.EdgeList[ei2].y)
                            {
                                EdgeGroup ng = new EdgeGroup();
                                ng.Begin = group.EdgeList[ei1].x;
                                ng.End = group.EdgeList[ei2].y;

                                for (int i = ei1; i <= ei2; ++i)
                                {
                                    ng.EdgeList.Add(group.EdgeList[i]);
                                }

                                for (int i = ei2; i >= ei1; --i)
                                {
                                    group.EdgeList.RemoveAt(i);
                                }
                                
                                groups.Add(ng);

                                ei1 = 0; // goto first
                                break;
                            }
                        }
                    }
                }

                if (groups.Count == 0)
                    continue;
                
                groups.Sort(static (g1, g2) => { return g2.EdgeList.Count - g1.EdgeList.Count; });
                
                //first group( longest group ) is outline. 
                for (int i = 1; i < groups.Count; ++i)
                {
                    EdgeGroup group = groups[i];
                    for (int ei = 1; ei < group.EdgeList.Count - 1; ++ei)
                    {
                        tris.Add(group.Begin);
                        tris.Add(group.EdgeList[ei].y);
                        tris.Add(group.EdgeList[ei].x);
                    }
                
                }

                totalTris += tris.Count;
                newTris.Add(tris);
            }
            
            WorkingMesh mesh = new WorkingMesh(Allocator.Persistent, source.vertexCount, totalTris, source.subMeshCount, 0);
            mesh.name = source.name;
#if OPTIMISATION
            mesh.CopyFrom(source);
#else
            mesh.vertices = source.vertices;
            mesh.normals = source.normals;
            mesh.uv = source.uv;
#endif // OPTIMISATION

            for (int i = 0; i < newTris.Count; ++i)
            {
                mesh.SetTriangles(newTris[i], i);
            }

            return mesh;
        }

        private static DisposableList<HLODBuildInfo> CreateBuildInfo(TerrainData data, SpaceNode root,
            Heightmap m_heightmap, Vector3 m_size, JobQueue m_queue, DisposableList<WorkingTexture> m_alphamaps, DisposableList<Layer> m_layers,
            TerrainHLOD m_hlod, DisposableList<HLODBuildInfo> results, Queue<SpaceNode> trevelQueue,
            Queue<int> parentQueue, Queue<string> nameQueue, Queue<int> depthQueue)
        {
            results.Dispose();
            trevelQueue.Clear();
            parentQueue.Clear();
            nameQueue.Clear();
            depthQueue.Clear();

            int maxDepth = 0;

            trevelQueue.Enqueue(root);
            parentQueue.Enqueue(-1);
            nameQueue.Enqueue("HLOD");
            depthQueue.Enqueue(0);
            

            while (trevelQueue.Count > 0)
            {
                int currentNodeIndex = results.Count;
                string name = nameQueue.Dequeue();
                SpaceNode node = trevelQueue.Dequeue();
                int depth = depthQueue.Dequeue();
                HLODBuildInfo info = new HLODBuildInfo
                {
                    Name = name,
                    ParentIndex = parentQueue.Dequeue(),
                    Target = node,
                };

                for (int i = 0; i < node.GetChildCount(); ++i)
                {
                    trevelQueue.Enqueue(node.GetChild(i));
                    parentQueue.Enqueue(currentNodeIndex);
                    nameQueue.Enqueue(name + "_" + (i + 1));
                    depthQueue.Enqueue(depth + 1);
                }
                
                info.Heightmap = CreateSubHightmap(node.Bounds, m_size, m_heightmap);
                info.WorkingObjects.Add(CreateBakedTerrain(name, node.Bounds, info.Heightmap, depth, node.GetChildCount() == 0, m_size, m_queue, m_alphamaps, m_layers, m_hlod));
                info.Distances.Add(depth);
                results.Add(info);
                
                if (depth > maxDepth)
                    maxDepth = depth;
            }

            //convert depth to distance
            for (int i = 0; i < results.Count; ++i)
            {
                HLODBuildInfo info = results[i];
                for (int di = 0; di < info.Distances.Count; ++di)
                {
                    info.Distances[di] = maxDepth - info.Distances[di];
                }
            }

            return results;
        }
        
        public IEnumerator CreateImpl()
        {
            try
            {
                using (var m_queue = new JobQueue(8))
                {
                    Stopwatch sw = new Stopwatch();

                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();

                    sw.Reset();
                    sw.Start();

                    EditorUtility.DisplayProgressBar("Bake HLOD", "Initialize Bake", 0.0f);


                    TerrainData? data = m_hlod.TerrainData;
#if SAFETY
                    if (data == null)
                        yield break;
#endif // SAFETY

                    var
                    m_size = data.size;

                    var heightmapResolution = data.heightmapResolution;
                    var
                    m_heightmap = new Heightmap(heightmapResolution, heightmapResolution, m_size,
                        data.GetHeights(0, 0, heightmapResolution, heightmapResolution));

                    string materialPath = AssetDatabase.GUIDToAssetPath(m_hlod.MaterialGUID);
                    var
                    m_terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (m_terrainMaterial == null)
                        m_terrainMaterial = new Material(Shader.Find("Lightweight Render Pipeline/Lit-Terrain-HLOD-High"));

                    m_terrainMaterialInstanceId = m_terrainMaterial.GetInstanceID();
                    m_terrainMaterialName = m_terrainMaterial.name;

                    materialPath = AssetDatabase.GUIDToAssetPath(m_hlod.MaterialLowGUID);
                    var
                    m_terrainMaterialLow = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (m_terrainMaterialLow == null)
                        m_terrainMaterialLow = new Material(Shader.Find("Lightweight Render Pipeline/Lit-Terrain-HLOD-Low"));

                    m_terrainMaterialLowInstanceId = m_terrainMaterialLow.GetInstanceID();
                    m_terrainMaterialLowName = m_terrainMaterialLow.name;

                    Texture2D[] alphamapTextures = data.alphamapTextures;
                    TerrainLayer[] terrainLayers = data.terrainLayers;
                    using (var m_alphamaps = new DisposableList<WorkingTexture>(alphamapTextures.Length))
                    using (var m_layers = new DisposableList<Layer>(terrainLayers.Length))
                    {
                        for (int i = 0; i < alphamapTextures.Length; ++i)
                        {
                            m_alphamaps.Add(new WorkingTexture(Allocator.Persistent, alphamapTextures[i]));
                        }

                        for (int i = 0; i < terrainLayers.Length; ++i)
                        {
                            m_layers.Add(new Layer(terrainLayers[i], m_hlod.ChunkSize));
                        }


                        QuadTreeSpaceSplitter splitter = new QuadTreeSpaceSplitter(null);

                        List<SpaceNode> rootNodeList = splitter.CreateSpaceTree(m_hlod.GetBounds(), m_hlod.ChunkSize * 2.0f,
#if OPTIMISATION
                        m_hlod.transform, null, progress => { EditorUtility.DisplayCancelableProgressBar("Bake HLOD", "Create mesh", progress); });
#else
                        m_hlod.transform, null, progress => { });

                        EditorUtility.DisplayProgressBar("Bake HLOD", "Create mesh", 0.0f);
#endif // OPTIMISATION

                        using var results = new DisposableList<HLODBuildInfo>();
                        var trevelQueue = new Queue<SpaceNode>();
                        var parentQueue = new Queue<int>();
                        var nameQueue = new Queue<string>();
                        var depthQueue = new Queue<int>();
                        var materials = new List<Material>();
                        foreach (var rootNode in rootNodeList)
                        {
                            using (DisposableList<HLODBuildInfo> buildInfos = CreateBuildInfo(data, rootNode,
                                m_heightmap, m_size, m_queue, m_alphamaps, m_layers, m_hlod,
                                results, trevelQueue, parentQueue, nameQueue, depthQueue))
                            {
                                yield return m_queue.WaitFinish();
                                //Write material & textures

#if OPTIMISATION
                                ISimplifier simplifier = (ISimplifier)Activator.CreateInstance(
                                    m_hlod.SimplifierType,
                                    m_hlod.SimplifierOptions);
                                for (int i = 0; i < buildInfos.Count; ++i)
                                {
                                    yield return new BranchCoroutine(simplifier.Simplify(buildInfos[i]));
                                }

                                yield return new WaitForBranches(progress =>
                                {
                                    EditorUtility.DisplayCancelableProgressBar("Bake HLOD", "Simplify meshes",
                                        0.25f + progress * 0.25f);
                                });
#else
                                for (int i = 0; i < buildInfos.Count; ++i)
                                {
                                    int curIndex = i;
                                    m_queue.EnqueueJob(() =>
                                    {
                                        ISimplifier simplifier = (ISimplifier)Activator.CreateInstance(
                                            m_hlod.SimplifierType,
                                            new object[] { m_hlod.SimplifierOptions });
                                        simplifier.SimplifyImmidiate(buildInfos[curIndex]);
                                    });
                                }

                                EditorUtility.DisplayProgressBar("Bake HLOD", "Simplify meshes", 0.0f);
                                yield return m_queue.WaitFinish();
#endif // OPTIMISATION

                                Debug.Log("[TerrainHLOD] Simplify: " + sw.Elapsed.ToString("g"));
                                sw.Reset();
                                sw.Start();
                                EditorUtility.DisplayProgressBar("Bake HLOD", "Make border", 0.0f);

                                for (int i = 0; i < buildInfos.Count; ++i)
                                {
                                    HLODBuildInfo info = buildInfos[i];
                                    m_queue.EnqueueJob(() =>
                                    {
                                        var vertices = new List<Vector3>();
                                        var normals = new List<Vector3>();
                                        var uvs = new List<Vector2>();
                                        var vertexIndces = new HashSet<int>();
                                        var borderVertices = new List<BorderVertex>();
                                        var candidates = new HashSet<Vector2Int>();
                                        var newTris = new List<List<int>>();
                                        var edgeList = new List<Vector2Int>();
                                        var groups = new List<EdgeGroup>();
                                        var subMeshTris = new List<List<int>>();

                                        for (int oi = 0; oi < info.WorkingObjects.Count; ++oi)
                                        {
                                            WorkingObject o = info.WorkingObjects[oi];
                                            int borderVertexCount = m_hlod.BorderVertexCount *
                                                                    Mathf.RoundToInt(Mathf.Pow(2.0f,
                                                                        (float)info.Distances[oi]));
                                            using (WorkingMesh m = MakeBorder(o.Mesh, info.Heightmap,
                                                borderVertexCount, vertices, normals, uvs, subMeshTris,
                                                vertexIndces, borderVertices, candidates))
                                            {
                                                ReampUV(m, info.Heightmap);
                                                o.SetMesh(MakeFillHoleMesh(m, newTris, edgeList, groups, candidates));
                                            }
                                        }
                                    });
                                }

                                yield return m_queue.WaitFinish();

                                Debug.Log("[TerrainHLOD] Make Border: " + sw.Elapsed.ToString("g"));
                                sw.Reset();
                                sw.Start();


                                for (int i = 0; i < buildInfos.Count; ++i)
                                {
                                    SpaceNode node = buildInfos[i].Target;
                                    HLODBuildInfo info = buildInfos[i];
                                    if (node.HasChild() == false)
                                    {
                                        SpaceNode? parent = node.ParentNode;
                                        node.ParentNode = null;

                                        GameObject go = new GameObject(buildInfos[i].Name);

                                        for (int wi = 0; wi < info.WorkingObjects.Count; ++wi)
                                        {
                                            string matName;
                                            WorkingObject wo = info.WorkingObjects[wi];
                                            GameObject targetGO;
                                            if (wi == 0)
                                            {
                                                matName = go.name + "_Mat";
                                                targetGO = go;
                                            }
                                            else
                                            {
                                                matName = wi.ToString() + "_Mat";
                                                targetGO = new GameObject(matName);
                                                targetGO.transform.SetParent(go.transform, false);
                                            }

                                            materials.Clear();
                                            for (int mi = 0; mi < wo.Materials.Count; ++mi)
                                            {

                                                WorkingMaterial wm = wo.Materials[mi];
                                                if (wm.NeedWrite() == false)
                                                {
                                                    materials.Add(wm.ToMaterial());
                                                    continue;
                                                }

                                                Material mat = new Material(wm.ToMaterial());
                                                var textureNames = wm.GetTextureNames();
                                                for (int ti = 0; ti < textureNames.Length; ++ti)
                                                {
                                                    WorkingTexture? wt = wm.GetTexture(textureNames[ti]);
                                                    if (wt == null)
                                                        continue;
                                                    Texture2D tex = wt.ToTexture();
                                                    tex.wrapMode = wt.WrapMode;
                                                    mat.name = matName;
                                                    mat.SetTexture(WorkingMaterialBuffer.ShaderProperty(textureNames[ti]), tex);
                                                }

                                                mat.EnableKeyword("_NORMALMAP");
                                                materials.Add(mat);
                                            }

                                            if (wo.Mesh != null)
                                                targetGO.AddComponent<MeshFilter>().sharedMesh = wo.Mesh.ToMesh();

                                            var mr = targetGO.AddComponent<MeshRenderer>();
                                            mr.SetSharedMaterials(materials);
                                            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                                        }

                                        go.transform.SetParent(m_hlod.transform, false);
                                        m_hlod.AddGeneratedResource(go);

                                        parent?.Objects.Add(go);
                                        buildInfos.RemoveAt(i);
                                        i -= 1;
                                    }
                                }

                                //controller
                                IStreamingBuilder builder =
                                    (IStreamingBuilder)Activator.CreateInstance(m_hlod.StreamingType,
#if BUGFIX
                                        new object[] { m_hlod, 0, m_hlod.StreamingOptions });
#else
                                        new object[] { m_hlod, m_hlod.StreamingOptions });
#endif // BUGFIX

                                builder.Build(rootNode, buildInfos, m_hlod.gameObject, m_hlod.CullDistance,
                                    m_hlod.LODDistance, true, false,
                                    progress =>
                                    {
                                        EditorUtility.DisplayProgressBar("Bake HLOD", "Storing results.",
                                            0.75f + progress * 0.25f);
                                    });

                                Debug.Log("[TerrainHLOD] Build: " + sw.Elapsed.ToString("g"));

                            }
                        }
                    }

                    EditorUtility.SetDirty(m_hlod.gameObject);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                GC.Collect();
            }
        }

        
    }

}