using System;
using System.Collections.Generic;
using Unity.HLODSystem.Streaming;
using UnityEditor;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.HLODSystem
{
    class BuildProcess : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            var prefabs = new List<HLODPrefab>();
            var rootsList = new List<GameObject>();
            scene.GetRootGameObjects(rootsList);
            var roots = rootsList.AsReadOnlySpan();

            //first, if we use HLODPrefab, we have to create prefab instance while build.
            for (int i = 0; i < roots.Length; ++i)
            {
                prefabs.Clear();
                roots[i].GetComponentsInChildren<HLODPrefab>(prefabs);
                for (int pi = 0, prefabsCount = prefabs.Count; pi < prefabsCount; ++pi)
                {
                    prefabs[pi].IsEdit = false;
                    _ = Object.Instantiate(prefabs[pi].Prefab, prefabs[pi].transform, false);

                    Object.DestroyImmediate(prefabs[pi]);
                }
            }

            var controllers = new List<HLODControllerBase>();
            for (int i = 0; i < roots.Length; ++i)
            {
                roots[i].GetComponentsInChildren<HLODControllerBase>(controllers);
            }

            for (int i = 0; i < controllers.Count; ++i)
            {
                var controller = controllers[i];

                if (controller.enabled)
                {
                    controller.Install();
                }
            }

        }
    }
}
