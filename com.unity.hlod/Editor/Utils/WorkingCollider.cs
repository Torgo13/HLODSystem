using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.HLODSystem.Utils
{
    public static class ColliderExtension
    {
        // ReSharper disable Unity.PerformanceAnalysis
        public static WorkingCollider ToWorkingCollider(this Collider collider, HLOD hlod)
        {
            var hlodWorldToLocal = hlod.transform.worldToLocalMatrix;
            var colliderLocalToWorld = collider.transform.localToWorldMatrix;
            var matrix = hlodWorldToLocal * colliderLocalToWorld;

            WorkingCollider wc = new WorkingCollider();
            dynamic parameters = wc.Parameters;

            wc.Type = collider.GetType().Name;
            wc.Position = matrix.GetPosition();
            wc.Rotation = matrix.rotation;
            wc.Scale = matrix.lossyScale;

            parameters.Enabled = collider.enabled;
            parameters.IsTrigger = collider.isTrigger;
            parameters.ContactOffset = collider.contactOffset;

            if (collider is BoxCollider boxCollider)
            {
                var size = boxCollider.size;
                var center = boxCollider.center;
                parameters.SizeX = size.x;
                parameters.SizeY = size.y;
                parameters.SizeZ = size.z;
                parameters.CenterX = center.x;
                parameters.CenterY = center.y;
                parameters.CenterZ = center.z;
            }
            else if (collider is MeshCollider meshCollider)
            {
                parameters.SharedMeshPath = ObjectUtils.ObjectToPath(meshCollider.sharedMesh);
                parameters.Convex = meshCollider.convex;
            }
            else if (collider is SphereCollider sphereCollider)
            {
                var center = sphereCollider.center;
                parameters.CenterX = center.x;
                parameters.CenterY = center.y;
                parameters.CenterZ = center.z;
                parameters.Radius = sphereCollider.radius;
            }
            else if (collider is CapsuleCollider capsuleCollider)
            {
                var center = capsuleCollider.center;
                parameters.CenterX = center.x;
                parameters.CenterY = center.y;
                parameters.CenterZ = center.z;
                parameters.Radius = capsuleCollider.radius;
                parameters.Height = capsuleCollider.height;
                parameters.Direction = capsuleCollider.direction;
            }
            else if (collider is TerrainCollider terrainCollider)
            {
                parameters.TerrainData = GUIDUtils.ObjectToGUID(terrainCollider.terrainData);
            }
            else
            {
                Debug.LogError($"{collider.name} collider is not support.");
            }

            return wc;
        }
    }

    public class WorkingCollider
    {
        string m_type = string.Empty;

        Vector3 m_position;
        Quaternion m_rotation;
        Vector3 m_scale;

        readonly
        SerializableDynamicObject m_parameters = new SerializableDynamicObject();

        public string Type
        {
            get => m_type;
            set => m_type = value;
        }

        public Vector3 Position
        {
            get => m_position;
            set => m_position = value;
        }

        public Quaternion Rotation
        {
            get => m_rotation;
            set => m_rotation = value;
        }

        public Vector3 Scale
        {
            get => m_scale;
            set => m_scale = value;
        }

        public SerializableDynamicObject Parameters => m_parameters;
    }
}