using System;
using System.Collections;
using System.Collections.Generic;
using Unity.HLODSystem.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Unity.HLODSystem.Streaming
{
    sealed
    public class AddressableHLODController : HLODControllerBase
    {
        public interface ICustomLoader
        {
            public void CustomLoad(string key, Action<GameObject> loadDoneAction);
            public void CustomUnload(string key);
        }

        [Serializable]
        public class ChildObject
        {
            public GameObject? GameObject;

            public string Address = string.Empty;

            public Transform? Parent;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        [SerializeField] private List<ChildObject> m_highObjects = new List<ChildObject>();

        [SerializeField] private List<string> m_lowObjects = new List<string>();

        sealed
        class LoadInfo
        {
            public string Key = string.Empty;
            public bool LoadFromCustom;
            public AsyncOperationHandle<GameObject> Handle;
            public GameObject? Instance;
        }

        private Dictionary<int, LoadInfo> m_highObjectLoadInfos = new Dictionary<int, LoadInfo>();
        private Dictionary<int, LoadInfo> m_lowObjectLoadInfos = new Dictionary<int, LoadInfo>();

        private GameObject? m_hlodMeshesRoot;
        private int m_hlodLayerIndex;

        private ICustomLoader? m_customLoader;

        public event Action<GameObject>? HighObjectCreated;

        public ICustomLoader? CustomLoader
        {
            set { m_customLoader = value; }
            get { return m_customLoader; }
        }


#if UNITY_EDITOR
        public override GameObject GetHighSceneObject(int id)
        {
            return m_highObjects[id].GameObject!;
        }
#endif

        public override void OnStart()
        {
            m_hlodMeshesRoot = new GameObject("HLODMeshesRoot");
            m_hlodMeshesRoot.transform.SetParent(transform, false);

            m_hlodLayerIndex = LayerMask.NameToLayer(HLOD.HLODLayerStr);
        }

        public override void OnStop()
        {
        }


        public override void Install()
        {
#if UNITY_6000_3_OR_NEWER
            var gameObjects = new Collections.NativeArray<EntityId>(m_highObjects.Count,
                Collections.Allocator.Temp,
                Collections.NativeArrayOptions.UninitializedMemory);

            int j = 0;
            for (int i = 0; i < m_highObjects.Count; ++i)
            {
                if (!string.IsNullOrEmpty(m_highObjects[i].Address))
                {
                    DestroyObject(m_highObjects[i]);
                }
                else
                {
                    gameObjects[j++] = m_highObjects[i].GameObject!.GetEntityId();
                }
            }
            
            GameObject.SetGameObjectsActive(gameObjects.GetSubArray(start: 0, j), false);
            gameObjects.Dispose();
#else
            for (int i = 0; i < m_highObjects.Count; ++i)
            {
                if (string.IsNullOrEmpty(m_highObjects[i].Address) == false)
                {
                    DestoryObject(m_highObjects[i].GameObject);
                }
                else if (m_highObjects[i].GameObject != null)
                {
                    m_highObjects[i].GameObject.SetActive(false);
                }
            }
#endif // UNITY_6000_3_OR_NEWER
        }

        public int AddHighObject(string address, GameObject origin)
        {
            int id = m_highObjects.Count;

            ChildObject obj = new ChildObject();
            obj.GameObject = origin;
            obj.Address = address;
            var t = origin.transform;
            obj.Parent = t.parent;
            t.GetLocalPositionAndRotation(out obj.Position, out obj.Rotation);
            obj.Scale = t.localScale;

            m_highObjects.Add(obj);
            return id;
        }

        public int AddHighObject(GameObject gameObject)
        {
            int id = m_highObjects.Count;

            ChildObject obj = new ChildObject();
            obj.GameObject = gameObject;

            m_highObjects.Add(obj);
            return id;
        }

        public int AddLowObject(string address)
        {
            int id = m_lowObjects.Count;
            m_lowObjects.Add(address);
            return id;
        }

        public override int HighObjectCount
        {
            get => m_highObjects.Count;
        }

        public override int LowObjectCount
        {
            get => m_lowObjects.Count;
        }

        public string GetLowObjectAddr(int index)
        {
            return m_lowObjects[index];
        }

        public override void LoadHighObject(int id, Action<GameObject>? loadDoneCallback)
        {
            var gameObject = m_highObjects[id].GameObject;
            if (gameObject != null)
            {
#if UNITY_6000_3_OR_NEWER
                ChangeLayersRecursively(gameObject, m_hlodLayerIndex);
#else
                ChangeLayersRecursively(gameObject.transform, m_hlodLayerIndex);
#endif // UNITY_6000_3_OR_NEWER
                loadDoneCallback?.Invoke(gameObject);
            }
            else
            {
                LoadInfo loadInfo = Load(m_highObjects[id].Address, m_highObjects[id].Parent,
                    m_highObjects[id].Position, m_highObjects[id].Rotation, m_highObjects[id].Scale,
                    loadDoneCallback, o => HighObjectCreated?.Invoke(o));
                
                m_highObjectLoadInfos.Add(id, loadInfo);
            }
        }

        public override void LoadLowObject(int id, Action<GameObject>? loadDoneCallback)
        {
            LoadInfo loadInfo = Load(m_lowObjects[id], m_hlodMeshesRoot!.transform, Vector3.zero,
                Quaternion.identity, Vector3.one, loadDoneCallback);
            
            m_lowObjectLoadInfos.Add(id, loadInfo);
        }

        public override void UnloadHighObject(int id)
        {
            if (string.IsNullOrEmpty(m_highObjects[id].Address) == true)
            {
                var go = m_highObjects[id].GameObject;
                if (go != null)
                    go.SetActive(false);
            }
            else
            {
                if (m_highObjectLoadInfos.Remove(id, out var loadInfo))
                {
                    DestroyObject(loadInfo);
                    Unload(loadInfo);
                }
#if ZERO
                else
                {
                    Debug.LogError($"HighObject handle not found: {id}");
                }
#endif // ZERO
            }

            
        }

        public override void UnloadLowObject(int id)
        {
            if (m_lowObjectLoadInfos.Remove(id, out var loadInfo))
            {
                DestroyObject(loadInfo);
                Unload(loadInfo);
            }
#if ZERO
            else
            {
                Debug.LogError($"LowObject handle not found: {id}");
            }
#endif // ZERO
        }

        private static void DestroyObject(ChildObject childObject)
            => DestoryObject(childObject.GameObject!);

        private static void DestroyObject(LoadInfo loadInfo)
            => DestoryObject(loadInfo.Instance!);

        static
        private void DestoryObject(Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
                Destroy(obj);
#else
            Destroy(obj);
#endif
        }

        private LoadInfo Load(string address, Transform? parent, Vector3 localPosition, Quaternion localRotation,
            Vector3 localScale, Action<GameObject>? callback0 = null, Action<GameObject>? callback1 = null)
        {
            LoadInfo loadInfo = new LoadInfo();
            loadInfo.Key = address;

#if UNITY_6000_3_OR_NEWER
            void loadDoneAction(GameObject obj)
            {
#if OPTIMISATION // Already Instantiated
                GameObject gameObject = obj;
#else
                GameObject gameObject = Instantiate(obj, parent, false);
#endif // OPTIMISATION
                var transformHandle = gameObject.transformHandle;
                transformHandle.SetLocalPositionAndRotation(localPosition, localRotation);
                transformHandle.localScale = localScale;
                gameObject.SetActive(false);
                ChangeLayersRecursively(gameObject, m_hlodLayerIndex);
#else
            Action<GameObject> loadDoneAction = (obj) => {
                GameObject gameObject = Instantiate(obj, parent, false);
                gameObject.transform.localPosition = localPosition;
                gameObject.transform.localRotation = localRotation;
                gameObject.transform.localScale = localScale;
                gameObject.SetActive(false);
                ChangeLayersRecursively(gameObject.transform, m_hlodLayerIndex);
#endif // UNITY_6000_3_OR_NEWER
                loadInfo.Instance = gameObject;
                callback0?.Invoke(gameObject);
                callback1?.Invoke(gameObject);
            };

            if (m_customLoader == null)
            {
                loadInfo.LoadFromCustom = false;
#if OPTIMISATION
                loadInfo.Handle = Addressables.InstantiateAsync(address,
                    new Vector3(0, -1024 * 1024, 0), Quaternion.identity,
                    parent!, trackHandle: false);
#else
                loadInfo.Handle = Addressables.LoadAssetAsync<GameObject>(address);
#endif // OPTIMISATION
#if UNITY_6000_3_OR_NEWER
                _ = LoadDoneActionAsync(loadInfo.Handle, loadInfo, m_hlodLayerIndex,
                    parent, localPosition, localRotation,
                    localScale, callback0, callback1, destroyCancellationToken);
#else
                loadInfo.Handle.Completed += handle =>
                {
                    if (handle.Status == AsyncOperationStatus.Failed)
                    {
                        Debug.LogError("Failed to load asset: " + address);
                        return;
                    }

                    loadDoneAction(loadInfo.Handle.Result);
                };
#endif // UNITY_6000_3_OR_NEWER
            }
            else
            {
                loadInfo.LoadFromCustom = true;
                m_customLoader.CustomLoad(address, loadDoneAction);
            }            

            return loadInfo;
        }
		
#if UNITY_6000_3_OR_NEWER
        private static async Awaitable LoadDoneActionAsync(
            AsyncOperationHandle<GameObject> handle, LoadInfo loadInfo, int m_hlodLayerIndex,
            Transform? parent, Vector3 localPosition, Quaternion localRotation,
            Vector3 localScale, Action<GameObject>? callback0, Action<GameObject>? callback1,
            System.Threading.CancellationToken ct)
        {
            bool endOfFrame = false;
            while (!ct.IsCancellationRequested && !handle.IsDone)
            {
                endOfFrame = !endOfFrame;
                if (endOfFrame)
                    await Awaitable.EndOfFrameAsync();
                else
                    await Awaitable.NextFrameAsync();
            }

            if (ct.IsCancellationRequested || handle.Status != AsyncOperationStatus.Succeeded)
                return;

            GameObject gameObject = handle.Result;
            gameObject.SetActive(false);
#if OPTIMISATION
#else
            var aio = InstantiateAsync(gameObject, new InstantiateParameters
                { parent = parent, worldSpace = false, originalImmutable = true, });
            while (!ct.IsCancellationRequested && !aio.isDone)
            {
                endOfFrame = !endOfFrame;
                if (endOfFrame)
                    await Awaitable.EndOfFrameAsync();
                else
                    await Awaitable.NextFrameAsync();
            }
            
            var result = aio.Result;
            if (ct.IsCancellationRequested || result == null || result.Length == 0 || result[0] == null)
                return;

            gameObject = result[0];
#endif // OPTIMISATION
            var transformHandle = gameObject.transformHandle;
            transformHandle.SetLocalPositionAndRotation(localPosition, localRotation);
            transformHandle.localScale = localScale;
            ChangeLayersRecursively(gameObject, m_hlodLayerIndex);

            loadInfo.Instance = gameObject;
            callback0?.Invoke(gameObject);
            callback1?.Invoke(gameObject);
        }
#endif // UNITY_6000_3_OR_NEWER

        private void Unload(LoadInfo info)
        {
            if (info.LoadFromCustom == false)
            {
                Addressables.Release(info.Handle);
            }
            else
            {
                m_customLoader?.CustomUnload(info.Key);
            }
        }

#if UNITY_6000_3_OR_NEWER
        static void ChangeLayersRecursively(GameObject go, int layer)
        {
            var list = UnityEngine.Pool.ListPool<Transform>.Get();
            go.GetComponentsInChildren(includeInactive: true, list);
            foreach (Transform child in list)
            {
                child.gameObject.layer = layer;
            }
            
            UnityEngine.Pool.ListPool<Transform>.Release(list);
        }
#else
        static void ChangeLayersRecursively(Transform trans, int layer)
        {
            trans.gameObject.layer = layer;
            foreach (Transform child in trans)
            {
                ChangeLayersRecursively(child, layer);
            }
        }
#endif // UNITY_6000_3_OR_NEWER
    }
}