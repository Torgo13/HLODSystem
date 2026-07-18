using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.HLODSystem.SpaceManager
{
    public static class BoundsExtension
    {
        public static bool IsPartOf(this Bounds bounds, Bounds target)
        {
            var boundsMax = bounds.max;
            var boundsMin = bounds.min;
            var targetMax = target.max;
            var targetMin = target.min;
            return boundsMin.x >= targetMin.x &&
                   boundsMax.x <= targetMax.x &&
                   boundsMin.y >= targetMin.y &&
                   boundsMax.y <= targetMax.y &&
                   boundsMin.z >= targetMin.z &&
                   boundsMax.z <= targetMax.z;
        }
    }
    public class QuadTreeSpaceSplitter : ISpaceSplitter
    {
        
        [InitializeOnLoadMethod]
        static void RegisterType()
        {
            SpaceSplitterTypes.RegisterSpaceSplitterType(typeof(QuadTreeSpaceSplitter));
        }

        private float m_looseSizeFromOptions;

        private bool m_useSubHLODTree;
        private float m_subHLODTreeSize;
        

        public QuadTreeSpaceSplitter(SerializableDynamicObject? spaceSplitterOptions)
        {
            m_looseSizeFromOptions = 0.0f;

            m_useSubHLODTree = false;
            m_subHLODTreeSize = 0.0f;
            
            if (spaceSplitterOptions == null)
            {
                return;
            }
            
            dynamic options = spaceSplitterOptions;
            if(options.LooseSize1 != null)
                m_looseSizeFromOptions = options.LooseSize;
            if(options.UseSubHLODTree != null)
                m_useSubHLODTree = options.UseSubHLODTree;
            if(options.SubHLODTreeSize != null)
                m_subHLODTreeSize = options.SubHLODTreeSize;
            
        }

        public int CalculateSubTreeCount(Bounds bounds)
        {
            if (m_useSubHLODTree == false)
                return 1;
            
            List<Bounds> splittedBounds = SplitBounds(bounds, m_subHLODTreeSize);
            return splittedBounds.Count;
        }

        public int CalculateTreeDepth(Bounds bounds, float chunkSize)
        {
            float maxLength = 0.0f;
            if (m_useSubHLODTree)
            {
                List<Bounds> splittedBounds = SplitBounds(bounds, m_subHLODTreeSize);
                if (splittedBounds.Count > 0)
                {
                    maxLength = Mathf.Max(splittedBounds[0].extents.x, splittedBounds[0].extents.z);
                }
                else
                {
                    maxLength = Mathf.Max(bounds.extents.x, bounds.extents.z);    
                }
            }
            else
            {
                maxLength = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }

            int depth = 1;

            while (maxLength > chunkSize)
            {
                depth += 1;
                maxLength *= 0.5f;
            }

            return depth;
        }
        
        public List<SpaceNode> CreateSpaceTree(Bounds initBounds, float chunkSize, Transform transform,
            List<GameObject> targetObjects, Action<float>? onProgress)
            => CreateSpaceTree(initBounds, chunkSize, transform, targetObjects.AsReadOnlySpan(), onProgress);

        public List<SpaceNode> CreateSpaceTree(Bounds initBounds, float chunkSize, Transform transform,
            ReadOnlySpan<GameObject> targetObjects, Action<float>? onProgress)
        {
            var targetInfos = new List<TargetInfo>();
            var targetInfosSpan = CreateTargetInfoList(targetObjects, transform, targetInfos).AsReadOnlySpan();
            List<SpaceNode> nodes;

            if (m_useSubHLODTree == true)
            {
                var splittedBounds = SplitBounds(initBounds, m_subHLODTreeSize).AsReadOnlySpan();
                var splittedTargetInfos = new List<List<TargetInfo>>();
                _ = SplitTargetObjects(targetInfosSpan, splittedBounds, splittedTargetInfos);

                nodes = new List<SpaceNode>(splittedTargetInfos.Count);
                float progressSize = 1.0f / splittedTargetInfos.Count; 
                for (int i = 0; i < splittedTargetInfos.Count; ++i)
                {
                    int i1 = i;
                    nodes.Add(CreateSpaceTreeImpl(splittedBounds[i], chunkSize, splittedTargetInfos[i].AsReadOnlySpan(), (p =>
                    {
                        float startProgress = i1 * progressSize;
                        onProgress?.Invoke(startProgress + p * progressSize);
                    })));
                }
            }
            else
            {
                nodes = new List<SpaceNode>();
                nodes.Add(CreateSpaceTreeImpl(initBounds, chunkSize, targetInfosSpan, onProgress));
            }

            return nodes;
        }
        private SpaceNode CreateSpaceTreeImpl(Bounds initBounds, float chunkSize, ReadOnlySpan<TargetInfo> targetObjects, Action<float>? onProgress)
        {
            float looseSize = CalcLooseSize(chunkSize);
            SpaceNode rootNode = new SpaceNode();
            rootNode.Bounds = initBounds;

            if ( onProgress != null)
                onProgress(0.0f);

			//space split first
			Stack<SpaceNode> nodeStack = new Stack<SpaceNode>();
			nodeStack.Push(rootNode);

            var childNodes = new List<SpaceNode>();
            while(nodeStack.Count > 0 )
            {
                SpaceNode node = nodeStack.Pop();
                if ( node.Bounds.size.x > chunkSize )
                {
                    childNodes.Clear();
                    _ = CreateChildSpaceNodes(node, looseSize, childNodes);
					
                    for ( int i = 0; i < childNodes.Count; ++i )
                    {
                        childNodes[i].ParentNode = node;
                        nodeStack.Push(childNodes[i]);
                    }
						
                }
            }

            if (targetObjects == null)
                return rootNode;

            for (int oi = 0; oi < targetObjects.Length; ++oi)
            {
                Bounds objectBounds = targetObjects[oi].Bounds;
                SpaceNode target = rootNode;

                while (true)
                {
                    if (target.HasChild())
                    {
                        //the object can be in the over 2 nodes.
                        //we should figure out which node is more close with the object.
                        int nearestChild = -1;
                        float nearestDistance = float.MaxValue;

                        for (int ci = 0; ci < target.GetChildCount(); ++ci)
                        {
                            if (objectBounds.IsPartOf(target.GetChild(ci).Bounds))
                            {
                                float dist = Vector3.Distance(target.GetChild(ci).Bounds.center, objectBounds.center);

                                if (dist < nearestDistance)
                                {
                                    nearestChild = ci;
                                    nearestDistance = dist;
                                }
                            }
                        }

                        //We should find out it until we get the fit size from the bottom.
                        //this means the object is small to add in the current node.
                        if (nearestChild >= 0)
                        {
                            target = target.GetChild(nearestChild);
                            continue;
                        }
                    }

                    target.Objects.Add(targetObjects[oi].GameObject);
                    break;
                }        
                
                if ( onProgress != null)
                    onProgress((float)oi/ (float)targetObjects.Length);
            }
            
            return rootNode;
        }

        struct TargetInfo
        {
            public GameObject GameObject;
            public Bounds Bounds;
        }

        private static List<TargetInfo> CreateTargetInfoList(ReadOnlySpan<GameObject> gameObjects, Transform transform,
            List<TargetInfo> targetInfos)
        {
#if BUGFIX
            if (gameObjects == null)
                return targetInfos;
#endif // BUGFIX

            var renderers = new List<MeshRenderer>();
            for (int i = 0; i < gameObjects.Length; ++i)
            {
                if (!CalculateBounds(gameObjects[i], transform, renderers, out Bounds bounds))
                    continue;
                targetInfos.Add(new TargetInfo()
                {
                    GameObject = gameObjects[i],
                    Bounds = bounds,
                });
            }

            return targetInfos;
        }
        
        private static bool CalculateBounds(GameObject obj, Transform transform,
            List<MeshRenderer> renderers, out Bounds result)
        {
            result = default;
            renderers.Clear();
            obj.GetComponentsInChildren<MeshRenderer>(renderers);
            if (renderers.Count == 0)
                return false;

            result = Utils.BoundsUtils.CalcLocalBounds(renderers[0], transform);
            for (int i = 1; i < renderers.Count; ++i)
            {
                result.Encapsulate(Utils.BoundsUtils.CalcLocalBounds(renderers[i], transform));
            }

            return true;
        }

        private static List<Bounds> SplitBounds(Bounds bounds, float splitSize,
            List<Bounds>? boundsList = null)
        {
            var boundsSize = bounds.size;
            int xcount = Mathf.CeilToInt(boundsSize.x / splitSize);
            int zcount = Mathf.CeilToInt(boundsSize.z / splitSize);

            // Avoid allocating boundsList with too many elements
            if (xcount * zcount > ushort.MaxValue)
                return new List<Bounds>();

            float xsize = boundsSize.x / xcount;
            float zsize = boundsSize.z / zcount;

            if (boundsList == null)
                boundsList = new List<Bounds>(xcount * zcount);
            else
                boundsList.Clear();
            Vector3 splitBoundSize = new Vector3(xsize, boundsSize.y, zsize);

            var boundsExtentsY = bounds.extents.y;
            var boundsMin = bounds.min;

            for (int z = 0; z < zcount; ++z)
            {
                for (int x = 0; x < xcount; ++x)
                {
                    Vector3 center = new Vector3(
                        x * xsize + xsize * 0.5f,
                        boundsExtentsY,
                        z * zsize + zsize * 0.5f) + boundsMin;
                    
                    boundsList.Add(new Bounds(center, splitBoundSize));
                }
            }

            return boundsList;
        }

        private static List<List<TargetInfo>> SplitTargetObjects(ReadOnlySpan<TargetInfo> targetInfoList, ReadOnlySpan<Bounds> targetBoundList,
            List<List<TargetInfo>> targetObjectsList)
        {
            for (int i = 0; i < targetBoundList.Length; ++i)
            {
                targetObjectsList.Add(new List<TargetInfo>());
            }

            foreach (var targetInfo in targetInfoList)
            {
                for (int bi = 0; bi < targetBoundList.Length; ++bi)
                {
                    if (targetBoundList[bi].Contains(targetInfo.Bounds.center))
                    {
                        targetObjectsList[bi].Add(targetInfo);
                        break;
                    }
                }
            }

            return targetObjectsList;
        }

        private float CalcLooseSize(float chunkSize)
        {
            //If the chunk size is small, there is a problem that it may get caught in an infinite loop.
            //So, the size can be determined according to the chunk size.
            return Mathf.Min(chunkSize * 0.3f, m_looseSizeFromOptions);
            
        }

        

        private static List<SpaceNode> CreateChildSpaceNodes(SpaceNode parentNode, float looseSize,
            List<SpaceNode> childSpaceNodes)
        {
            float size = parentNode.Bounds.size.x;
            float extend = size * 0.5f;
            float offset = extend * 0.5f;

            Vector3 center = parentNode.Bounds.center;
            Vector3 looseBoundsSize = new Vector3(extend + looseSize, size, extend + looseSize);

            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(-offset, 0.0f, -offset), looseBoundsSize)
                ));
            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(-offset, 0.0f, offset), looseBoundsSize)
                ));
            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(offset, 0.0f, -offset), looseBoundsSize)
                ));
            childSpaceNodes.Add(
                SpaceNode.CreateSpaceNodeWithBounds(
                    new Bounds(center + new Vector3(offset, 0.0f, offset), looseBoundsSize)
                ));
            
            return childSpaceNodes;
        }

        public static SerializableDynamicObject CreateOptions(
            bool autoCalcLooseSize = true,
            float looseSize = 5.0f,
            bool useSubHLODTree = false,
            float subHLODTreeSize = 100.0f)
        {
            var dynamicObject = new SerializableDynamicObject();
            dynamic options = dynamicObject;

            options.AutoCalcLooseSize = autoCalcLooseSize;
            options.LooseSize = looseSize;
            options.UseSubHLODTree = useSubHLODTree;
            options.SubHLODTreeSize = subHLODTreeSize;

            return dynamicObject;
        }

        public static void OnGUI(SerializableDynamicObject spaceSplitterOptions)
        {
            dynamic options = spaceSplitterOptions;

            //initialize values
            if (options.LooseSize == null)
                options.LooseSize = 5.0f;
            if (options.UseSubHLODTree == null)
                options.UseSubHLODTree = false;
            if (options.SubHLODTreeSize == null)
                options.SubHLODTreeSize = 100.0f;

            //Draw UI
            options.LooseSize = EditorGUILayout.FloatField("Loose size", options.LooseSize);

            options.UseSubHLODTree = EditorGUILayout.ToggleLeft("Use sub HLOD tree", options.UseSubHLODTree);
            if (options.UseSubHLODTree == true)
            {
                EditorGUI.indentLevel += 1;
                options.SubHLODTreeSize = EditorGUILayout.FloatField("Sub tree size", options.SubHLODTreeSize);
                EditorGUI.indentLevel -= 1;
            }

        }

        
    }

}