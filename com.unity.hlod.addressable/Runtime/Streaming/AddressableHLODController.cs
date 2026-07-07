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
#if !UNITY_6000_3_OR_NEWER
            private GameObject _instance;
            public GameObject Instance
            {
                get
                {
                    if (_instance == null && Handle.IsValid() && Handle.IsDone)
                    {
                        _instance = Handle.Result;
                    }

                    return _instance;
                }
                set => _instance = value;
            }
#else
            public GameObject? Instance;
#endif // UNITY_6000_3_OR_NEWER
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
            for (int i = 0; i < m_highObjects.Count; ++i)
            {
                if (string.IsNullOrEmpty(m_highObjects[i].Address) == false)
                {
                    DestoryObject(m_highObjects[i].GameObject);
                }
                else if (m_highObjects[i].GameObject != null)
                {
                    m_highObjects[i].GameObject!.SetActive(false);
                }
            }
        }

        public int AddHighObject(string address, GameObject origin)
        {
            int id = m_highObjects.Count;

            ChildObject obj = new ChildObject();
            obj.GameObject = origin;
            obj.Address = address;
            obj.Parent = origin.transform.parent;
            obj.Position = origin.transform.localPosition;
            obj.Rotation = origin.transform.localRotation;
            obj.Scale = origin.transform.localScale;

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
            if (m_highObjects[id].GameObject != null)
            {
                var gameObject = m_highObjects[id].GameObject!;
                ChangeLayersRecursively(gameObject.transform, m_hlodLayerIndex);
                loadDoneCallback?.Invoke(gameObject);
            }
            else
            {
                var callbacks = new List<Action<GameObject>?>();
                callbacks.Add(loadDoneCallback);
                callbacks.Add(o => { HighObjectCreated?.Invoke(o); });

                LoadInfo loadInfo = Load(m_highObjects[id].Address, m_highObjects[id].Parent,
                    m_highObjects[id].Position, m_highObjects[id].Rotation, m_highObjects[id].Scale, callbacks);
                
                m_highObjectLoadInfos.Add(id, loadInfo);
            }
        }

        public override void LoadLowObject(int id, Action<GameObject>? loadDoneCallback)
        {
            var callbacks = new List<Action<GameObject>?>();
            callbacks.Add(loadDoneCallback);

            LoadInfo loadInfo = Load(m_lowObjects[id], m_hlodMeshesRoot!.transform, Vector3.zero,
                Quaternion.identity, Vector3.one, callbacks);
            
            m_lowObjectLoadInfos.Add(id, loadInfo);
        }

        public override void UnloadHighObject(int id)
        {
            if (string.IsNullOrEmpty(m_highObjects[id].Address) == true)
            {
                if (m_highObjects[id].GameObject != null)
                    m_highObjects[id].GameObject!.SetActive(false);
            }
            else
            {
                if (m_highObjectLoadInfos.TryGetValue(id, out var loadInfo))
                {
                    DestoryObject(loadInfo.Instance);
                    Unload(loadInfo);

                    m_highObjectLoadInfos.Remove(id);
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
            if (m_lowObjectLoadInfos.TryGetValue(id, out var loadInfo))
            {
                DestoryObject(loadInfo.Instance);
                Unload(loadInfo);
                
                m_lowObjectLoadInfos.Remove(id);
                
            }
#if ZERO
            else
            {
                Debug.LogError($"LowObject handle not found: {id}");
            }
#endif // ZERO
        }

        private void DestoryObject(Object? obj)
        {
#if SAFETY
            if (obj == null)
                return;
#endif // SAFETY

#if UNITY_EDITOR
            DestroyImmediate(obj);
#else
            Destroy(obj);
#endif
        }

        private LoadInfo Load(string address, Transform? parent, Vector3 localPosition, Quaternion localRotation,
            Vector3 localScale, List<Action<GameObject>?> callbacks)
        {
            LoadInfo loadInfo = new LoadInfo();
            loadInfo.Key = address;

#if UNITY_6000_3_OR_NEWER
#else
            Action<GameObject> loadDoneAction = (obj) => {
                GameObject gameObject = Instantiate(obj, parent, false);
                gameObject.transform.localPosition = localPosition;
                gameObject.transform.localRotation = localRotation;
                gameObject.transform.localScale = localScale;
                gameObject.SetActive(false);
                ChangeLayersRecursively(gameObject.transform, m_hlodLayerIndex);

                loadInfo.Instance = gameObject;
                foreach (var callback in callbacks)
                {
                    callback?.Invoke(gameObject);
                }
            };
#endif // UNITY_6000_3_OR_NEWER

            if (m_customLoader == null)
            {
                loadInfo.LoadFromCustom = false;
                loadInfo.Handle = Addressables.LoadAssetAsync<GameObject>(address);
#if UNITY_6000_3_OR_NEWER
                _ = LoadDoneActionAsync(loadInfo.Handle, loadInfo, m_hlodLayerIndex,
                    parent, localPosition, localRotation,
                    localScale, callbacks, destroyCancellationToken);
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
#if UNITY_6000_3_OR_NEWER
                void LoadDoneAction(GameObject obj)
                {
                    GameObject gameObject = Instantiate(obj, parent, false);
                    var t = gameObject.transform;
                    t.SetLocalPositionAndRotation(localPosition, localRotation);
                    t.localScale = localScale;
                    gameObject.SetActive(false);
                    ChangeLayersRecursively(t, m_hlodLayerIndex);

                    loadInfo.Instance = gameObject;
                    foreach (var callback in callbacks)
                    {
                        callback?.Invoke(gameObject);
                    }
                }
                m_customLoader.CustomLoad(address, LoadDoneAction);
#else
                m_customLoader.CustomLoad(address, loadDoneAction);
#endif // UNITY_6000_3_OR_NEWER
            }            

            return loadInfo;
        }
		
#if UNITY_6000_3_OR_NEWER
        private static async Awaitable LoadDoneActionAsync(
            AsyncOperationHandle<GameObject> handle, LoadInfo loadInfo, int m_hlodLayerIndex,
            Transform? parent, Vector3 localPosition, Quaternion localRotation,
            Vector3 localScale, List<Action<GameObject>?> callbacks,
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

            GameObject handleResult = handle.Result;
            handleResult.SetActive(false);
            var aio = InstantiateAsync(handleResult, new InstantiateParameters
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

            GameObject gameObject = result[0];
            var t = gameObject.transform;
            t.SetLocalPositionAndRotation(localPosition, localRotation);
            t.localScale = localScale;
            ChangeLayersRecursively(t, m_hlodLayerIndex);

            loadInfo.Instance = gameObject;
            foreach (var callback in callbacks)
            {
                callback?.Invoke(gameObject);
            }
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

        static void ChangeLayersRecursively(Transform trans, int layer)
        {
            trans.gameObject.layer = layer;
            foreach (Transform child in trans)
            {
                ChangeLayersRecursively(child, layer);
            }
        }
    }
}