using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Unity.HLODSystem.Utils
{
    public static class ObjectUtils
    {
        //It must order by child first.
        //Because we need to make child prefab first.
        public static List<T> GetComponentsInChildren<T>(Transform root,
            Queue<Transform> queue, List<T> components) where T : Component
        {
            LinkedList<T> result = new LinkedList<T>();
            queue.Clear();
            queue.Enqueue(root);

            while (queue.TryDequeue(out Transform t))
            {
                if (t.TryGetComponent(out T component))
                    _ = result.AddFirst(component);

                foreach (Transform child in t)
                {
                    queue.Enqueue(child);
                }
            }

            components.AddRange(result);
            return components;
        }

        public static List<GameObject> HLODTargets(Transform root,
            List<GameObject> HLODTargets)
        {
            var targets = UnityEngine.Pool.ListPool<GameObject>.Get();

            List<HLODMeshSetter> meshSetters = UnityEngine.Pool.ListPool<HLODMeshSetter>.Get();
            List<LODGroup> lodGroups = UnityEngine.Pool.ListPool<LODGroup>.Get();
            List<MeshRenderer> meshRenderers = UnityEngine.Pool.ListPool<MeshRenderer>.Get();
            var queue = new Queue<Transform>();
            _ = GetComponentsInChildren<HLODMeshSetter>(root, queue, meshSetters);
            _ = GetComponentsInChildren<LODGroup>(root, queue, lodGroups);
            //This contains all of the mesh renderers, so we need to remove the duplicated mesh renderer which in the LODGroup.
            _ = GetComponentsInChildren<MeshRenderer>(root, queue, meshRenderers);

            var lg = UnityEngine.Pool.ListPool<LODGroup>.Get();
            var mr = UnityEngine.Pool.ListPool<MeshRenderer>.Get();
            for (int mi = 0; mi < meshSetters.Count; ++mi)
            {
#if UNITY_6000_3_OR_NEWER
                if (!meshSetters[mi].isActiveAndEnabled)
                    continue;
#else
                if (meshSetters[mi].enabled == false)
                    continue;
                if (meshSetters[mi].gameObject.activeInHierarchy == false)
                    continue;
#endif // UNITY_6000_3_OR_NEWER

                targets.Add(meshSetters[mi].gameObject);

                lg.Clear();
                mr.Clear();
                meshSetters[mi].GetComponentsInChildren<LODGroup>(lg);
                meshSetters[mi].GetComponentsInChildren<MeshRenderer>(mr);
                lodGroups.RemoveAll(lg);
                meshRenderers.RemoveAll(mr);
            }
            
            UnityEngine.Pool.ListPool<LODGroup>.Release(lg);
            UnityEngine.Pool.ListPool<HLODMeshSetter>.Release(meshSetters);

            for (int i = 0; i < lodGroups.Count; ++i)
            {
                if ( lodGroups[i].enabled == false )
                    continue;
                if (lodGroups[i].gameObject.activeInHierarchy == false)
                    continue;

                targets.Add(lodGroups[i].gameObject);

                mr.Clear();
                lodGroups[i].GetComponentsInChildren<MeshRenderer>(mr);
                meshRenderers.RemoveAll(mr);
            }
            
            UnityEngine.Pool.ListPool<MeshRenderer>.Release(mr);
            UnityEngine.Pool.ListPool<LODGroup>.Release(lodGroups);

            //Combine renderer which in the LODGroup and renderer which without the LODGroup.
            for (int ri = 0; ri < meshRenderers.Count; ++ri)
            {
                if (meshRenderers[ri].enabled == false)
                    continue;
                if (meshRenderers[ri].gameObject.activeInHierarchy == false)
                    continue;

                targets.Add(meshRenderers[ri].gameObject);
            }
            
            UnityEngine.Pool.ListPool<MeshRenderer>.Release(meshRenderers);
            
            //Combine several LODGroups and MeshRenderers belonging to Prefab into one.
            //Since the minimum unit of streaming is Prefab, it must be set to the minimum unit.
            var targetsByPrefab = UnityEngine.Pool.HashSetPool<GameObject>.Get();
            var hlodRoot = root.gameObject;
            for (int ti = 0; ti < targets.Count; ++ti)
            {
                var targetPrefab = GetCandidatePrefabRoot(hlodRoot, targets[ti]);
                targetsByPrefab.Add(targetPrefab);
            }

            HLODTargets.AddRange(targetsByPrefab);
            UnityEngine.Pool.HashSetPool<GameObject>.Release(targetsByPrefab);
            UnityEngine.Pool.ListPool<GameObject>.Release(targets);
            return HLODTargets;
        }

        //This is finding nearest prefab root from the HLODRoot.
        public static GameObject GetCandidatePrefabRoot(GameObject hlodRoot, GameObject target)
        {
            if (PrefabUtility.IsPartOfAnyPrefab(target) == false)
                return target;

            GameObject candidate = target;
            GameObject outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(target);

            while (Equals(target,outermost) == false && 
                   Equals(GetParent(target), hlodRoot) == false)    //< HLOD root should not be included.
            {
                target = GetParent(target);
                if (PrefabUtility.IsAnyPrefabInstanceRoot(target))
                {
                    candidate = target;
                }
            }

            return candidate;
        }

        private static GameObject GetParent(GameObject go)
        {
            return go.transform.parent.gameObject;
        }
        
        
        
        
        
        public static T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            System.Type type = original.GetType();
            T copy = destination.AddComponent<T>();
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy;
        }

        public static void CopyValues<T>(T source, T target)
        {
            System.Type type = source.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                field.SetValue(target, field.GetValue(source));
            }
        }

        public static string ObjectToPath(Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                return "";

            if (AssetDatabase.IsMainAsset(obj) == false)
            {
                path += "[" + obj.name + "]";
            }

            return path;
        }

        private static readonly char[] splitChars = { '[', ']' };

        public static void ParseObjectPath(string path, out string mainPath, out string subAssetName)
        {
            string[] splittedStr = path.Split(splitChars);
            mainPath = splittedStr[0];
            if (splittedStr.Length > 1)
            {
                subAssetName = splittedStr[1];
            }
            else
            {
                subAssetName = string.Empty;
            }
        }


    }

}