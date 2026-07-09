using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.HLODSystem.Cache
{
    [Serializable]
    class SimplifiedMesh
    {
        public float Quality;
        public string SimplifierType;
        public Mesh Mesh;

        public SimplifiedMesh(float quality, string simplifierType, Mesh mesh)
        {
            Quality = quality;
            SimplifierType = simplifierType;
            Mesh = mesh;
        }
    }

    class SimplifiedMeshList : ScriptableObject
    {
        public long Timestamp;
        [SerializeField]
        private List<SimplifiedMesh> m_MeshList = new List<SimplifiedMesh>();

        public void AddMesh(float quality, Type simplifierType, Mesh mesh)
        {
            m_MeshList.Add(new SimplifiedMesh(quality, simplifierType.AssemblyQualifiedName!, mesh));
        }

        public SimplifiedMesh? GetMesh(float quality, Type simplifierType)
        {
            //Compare three decimal places
            int compareQuality = (int)(quality * 1000.0f + 0.5f);

            for (int i = 0; i < m_MeshList.Count; ++i)
            {
                var mesh = m_MeshList[i];
                int meshQuality = (int) (mesh.Quality * 1000.0f + 0.5f);
                if (meshQuality == compareQuality && simplifierType.AssemblyQualifiedName == mesh.SimplifierType)
                    return mesh;
            }

            return null;
        }
    }
    
}
