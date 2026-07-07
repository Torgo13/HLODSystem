using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.HLODSystem.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.HLODSystem
{
    /// <summary>
    /// A batcher that preserves materials when combining meshes (does not reduce draw calls)
    /// </summary>
    class MaterialPreservingBatcher : IBatcher
    {
        [InitializeOnLoadMethod]
        static void RegisterType()
        {
            BatcherTypes.RegisterBatcherType(typeof(MaterialPreservingBatcher));
        }


        public MaterialPreservingBatcher(SerializableDynamicObject batcherOptions)
        {
        }

        public void Dispose()
        {
        }

        public void Batch(Transform rootTransform, DisposableList<HLODBuildInfo> targets, Action<float> onProgress)
        {
            for (int i = 0; i < targets.Count; ++i)
            {
                Combine(rootTransform, targets[i]);

                if (onProgress != null)
                    onProgress((float) i / (float)targets.Count);
            }

        }

        private void Combine(Transform rootTransform, HLODBuildInfo info)
        {
            var materialTable = new Dictionary<string, WorkingMaterial>();
            var combineInfos = new Dictionary<string, List<MeshCombiner.CombineInfo>>();           

            var hlodWorldToLocal = rootTransform.worldToLocalMatrix;
            
            
            for (int i = 0; i < info.WorkingObjects.Count; ++i)
            {
                var materials = info.WorkingObjects[i].Materials;
                for (int m = 0; m < materials.Count; ++m)
                {
                    //var mat = materials[m];
                    MeshCombiner.CombineInfo combineInfo = new MeshCombiner.CombineInfo();

                    var colliderLocalToWorld = info.WorkingObjects[i].LocalToWorld;
                    var matrix = hlodWorldToLocal * colliderLocalToWorld;
                    
                    combineInfo.Transform = matrix;
                    combineInfo.Mesh = info.WorkingObjects[i].Mesh;
                    combineInfo.MeshIndex = m;

                    if (combineInfo.Mesh == null)
                        continue;

                    if (combineInfos.ContainsKey(materials[m].Identifier) == false)
                    {
                        combineInfos.Add(materials[m].Identifier, new List<MeshCombiner.CombineInfo>());
                        materialTable.Add(materials[m].Identifier, materials[m]);
                    }
                    
                    combineInfos[materials[m].Identifier].Add(combineInfo);
                }
            }

            using (var originWorkingObject = info.WorkingObjects)
            {
                DisposableList<WorkingObject> combinedObjects = new DisposableList<WorkingObject>();
                info.WorkingObjects = combinedObjects;
                
                var remappers = UnityEngine.Pool.ListPool<Dictionary<int, int>>.Get();
                var vertices = UnityEngine.Pool.ListPool<Vector3>.Get();
                var normals = UnityEngine.Pool.ListPool<Vector3>.Get();
                var tangents = UnityEngine.Pool.ListPool<Vector4>.Get();
                var uv1s = UnityEngine.Pool.ListPool<Vector2>.Get();
                var uv2s = UnityEngine.Pool.ListPool<Vector2>.Get();
                var uv3s = UnityEngine.Pool.ListPool<Vector2>.Get();
                var uv4s = UnityEngine.Pool.ListPool<Vector2>.Get();
                var colors = UnityEngine.Pool.ListPool<Color>.Get();
                var triangles = UnityEngine.Pool.ListPool<int>.Get();

                MeshCombiner combiner = new MeshCombiner();
                foreach (var pair in combineInfos)
                {
                    WorkingMesh combinedMesh = combiner.CombineMesh(Allocator.Persistent, pair.Value,
                        remappers, vertices, normals, tangents, uv1s, uv2s, uv3s, uv4s, colors, triangles);
                    WorkingObject combinedObject = new WorkingObject(Allocator.Persistent);
                    WorkingMaterial material = materialTable[pair.Key].Clone();

                    combinedMesh.name = info.Name + "_Mesh" + pair.Key;
                    combinedObject.Name = info.Name;
                    combinedObject.SetMesh(combinedMesh);
                    combinedObject.Materials.Add(material);

                    combinedObjects.Add(combinedObject);
                }
                
                UnityEngine.Pool.ListPool<Dictionary<int, int>>.Release(remappers);
                UnityEngine.Pool.ListPool<Vector3>.Release(vertices);
                UnityEngine.Pool.ListPool<Vector3>.Release(normals);
                UnityEngine.Pool.ListPool<Vector4>.Release(tangents);
                UnityEngine.Pool.ListPool<Vector2>.Release(uv1s);
                UnityEngine.Pool.ListPool<Vector2>.Release(uv2s);
                UnityEngine.Pool.ListPool<Vector2>.Release(uv3s);
                UnityEngine.Pool.ListPool<Vector2>.Release(uv4s);
                UnityEngine.Pool.ListPool<Color>.Release(colors);
                UnityEngine.Pool.ListPool<int>.Release(triangles);
            }
        }

        static void OnGUI(HLOD hlod, bool isFirst)
        {

        }

    }
}
