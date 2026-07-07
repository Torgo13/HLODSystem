using System.Collections.Generic;
using Unity.Collections;
using Unity.HLODSystem.Utils;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace Unity.HLODSystem
{
    public class HLODBuilder : IProcessSceneWithReport
    {
        public int callbackOrder
        {
            get { return 0; }
        }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            using var _0 = UnityEngine.Pool.ListPool<GameObject>.Get(out var rootObjects);
            scene.GetRootGameObjects(rootObjects);
            using var _1 = UnityEngine.Pool.ListPool<Terrain>.Get(out var terrains);
            using var _2 = UnityEngine.Pool.HashSetPool<TerrainData?>.Get(out var needDestroyDatas);
            using var _3 = UnityEngine.Pool.ListPool<HLOD>.Get(out var hlods);
            using var _4 = UnityEngine.Pool.ListPool<TerrainHLOD>.Get(out var terrainHlods);

            for (int oi = 0, rootObjectsCount = rootObjects.Count; oi < rootObjectsCount; ++oi)
            {
                hlods.Clear();
                terrainHlods.Clear();

                FindComponentsInChild(rootObjects[oi], ref hlods);
                FindComponentsInChild(rootObjects[oi], ref terrainHlods);
                FindComponentsInChild(rootObjects[oi], ref terrains);

                for (int hi = 0; hi < hlods.Count; ++hi)
                {
                    Object.DestroyImmediate(hlods[hi]);
                }
                for (int hi = 0; hi < terrainHlods.Count; ++hi)
                {
                    if (terrainHlods[hi].DestroyTerrain)
                    {
                        needDestroyDatas.Add(terrainHlods[hi].TerrainData);
                    }
                    Object.DestroyImmediate(terrainHlods[hi]);
                }
            }

            for (int ti = 0; ti < terrains.Count; ++ti)
            {
                if (terrains[ti] == null)
                    continue;

                bool needDestroy = needDestroyDatas.Contains(terrains[ti].terrainData);
                if (needDestroy)
                {
                    Object.DestroyImmediate(terrains[ti]);
                }
            }
        }

        private void FindComponentsInChild<T>(GameObject target, ref List<T> components)
        {
            var component = target.GetComponent<T>();
            if (component != null)
                components.Add(component);

            foreach (Transform child in target.transform)
            {
                FindComponentsInChild(child.gameObject, ref components);
            }
        }

    }
}