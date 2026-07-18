using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.HLODSystem.Utils
{
    public static class MeshRendererExtension
    {
        public static WorkingObject ToWorkingObject(this MeshRenderer renderer, Allocator allocator)
        {
            WorkingObject obj = new WorkingObject(allocator,
                renderer, renderer.GetComponent<MeshFilter>().sharedMesh);
            return obj;
        }
    }

    
    
    public class WorkingObject : IDisposable
    {
        private NativeArray<int> m_detector = new NativeArray<int>(1, Allocator.Persistent);

        private WorkingMesh? m_mesh;
        private DisposableBag<WorkingMaterial> m_materials = new DisposableBag<WorkingMaterial>();
        private Matrix4x4 m_localToWorld;

        private Allocator m_allocator;

        private UnityEngine.Rendering.LightProbeUsage m_lightProbeUsage;

        private string name = string.Empty;
        public string Name { get => name; set => name = value; }
        public WorkingMesh? Mesh
        {
            get { return m_mesh; }
        }

        public DisposableBag<WorkingMaterial> Materials
        {
            get { return m_materials; }
        }

        public Matrix4x4 LocalToWorld
        {
            get { return m_localToWorld; }
        }

        public UnityEngine.Rendering.LightProbeUsage LightProbeUsage
        {
            get => m_lightProbeUsage;
            set => m_lightProbeUsage = value;
        }

        public WorkingObject(Allocator allocator,
            WorkingMesh? mesh = null)
        {
            m_allocator = allocator;
            m_mesh = mesh;
            m_localToWorld = Matrix4x4.identity;
            m_lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        }

        public WorkingObject(Allocator allocator,
            MeshRenderer renderer, Mesh filterSharedMesh)
        {
            m_allocator = allocator;
            m_mesh = filterSharedMesh.ToWorkingMesh(m_allocator);
            m_localToWorld = Matrix4x4.identity;
            m_lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            FromRenderer(renderer);
        }
        
        public void FromRenderer(MeshRenderer renderer)
        {
            //clean old data
            //m_mesh?.Dispose();
            m_materials.Dispose();

            var sharedMaterials = new System.Collections.Generic.List<Material>();
            renderer.GetSharedMaterials(sharedMaterials);
            foreach (var mat in sharedMaterials)
            {
                m_materials.Add(mat.ToWorkingMaterial(m_allocator));
            }

            m_localToWorld = renderer.localToWorldMatrix;

            m_lightProbeUsage = renderer.lightProbeUsage;
        }

        /// <remarks>Background thread</remarks>
        public void SetMesh(WorkingMesh mesh)
        {
            if (m_mesh == mesh)
                return;
            
            if (m_mesh != null)
            {
                m_mesh.Dispose();
                m_mesh = null;
            }

            m_mesh = mesh;
        }

        public void Dispose()
        {
            m_mesh?.Dispose();
            m_materials.Dispose();
            m_detector.Dispose();
        }
    }

}