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
            var rootObjectsList = new List<GameObject>();
            scene.GetRootGameObjects(rootObjectsList);
            var rootObjects = rootObjectsList.AsReadOnlySpan();
            var terrains = new List<Terrain>();
            var needDestroyDatas = new HashSet<TerrainData?>();
            var hlods = new List<HLOD>();
            var terrainHlods = new List<TerrainHLOD>();

            for (int oi = 0; oi < rootObjects.Length; ++oi)
            {
                hlods.Clear();
                terrainHlods.Clear();

                rootObjects[oi].GetComponentsInChildren(includeInactive: true, hlods);
                rootObjects[oi].GetComponentsInChildren(includeInactive: true, terrainHlods);
                rootObjects[oi].GetComponentsInChildren(includeInactive: true, terrains);

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
#if OPTIMISATION_NULL
#else
                if (terrains[ti] == null)
                    continue;
#endif // OPTIMISATION_NULL

                bool needDestroy = needDestroyDatas.Contains(terrains[ti].terrainData);
                if (needDestroy)
                {
                    Object.DestroyImmediate(terrains[ti]);
                }
            }
        }

#if UNUSED
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
#endif // UNUSED
    }
}