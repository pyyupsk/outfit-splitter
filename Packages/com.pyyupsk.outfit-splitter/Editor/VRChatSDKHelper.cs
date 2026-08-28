using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Pyyupsk.OutfitSplitter.Editor
{
    internal static class VRChatSDKHelper
    {
        private static Type _vrcPhysBoneType;
        private static Type _vrcPhysBoneColliderType;
        private static bool _typesResolved = false;

        private static bool ResolveTypes()
        {
            if (_typesResolved) return _vrcPhysBoneType != null;
            _typesResolved = true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                if (asm.FullName.StartsWith("VRC.SDK3.Dynamics.PhysBone"))
                {
                    _vrcPhysBoneType = asm.GetType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
                    _vrcPhysBoneColliderType = asm.GetType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider");
                    break;
                }
            }

            if (_vrcPhysBoneType == null)
            {
                foreach (var asm in assemblies)
                {
                    if (asm.FullName.StartsWith("VRC.SDK3.Avatars"))
                    {
                        _vrcPhysBoneType = asm.GetType("VRC.SDK3.Avatars.Components.VRCPhysBone");
                        _vrcPhysBoneColliderType = asm.GetType("VRC.SDK3.Avatars.Components.VRCPhysBoneCollider");
                        if (_vrcPhysBoneType != null) break;
                    }
                }
            }

            return _vrcPhysBoneType != null;
        }

        public static bool HasVRChatSDK => ResolveTypes();

        public static Component[] GetPhysBones(GameObject go)
        {
            ResolveTypes();
            if (_vrcPhysBoneType == null) return Array.Empty<Component>();
            return go.GetComponentsInChildren(_vrcPhysBoneType, true);
        }

        public static Component[] GetPhysBoneColliders(GameObject go)
        {
            ResolveTypes();
            if (_vrcPhysBoneColliderType == null) return Array.Empty<Component>();
            return go.GetComponentsInChildren(_vrcPhysBoneColliderType, true);
        }

        public static Component AddPhysBone(GameObject go)
        {
            ResolveTypes();
            if (_vrcPhysBoneType == null) return null;
            return Undo.AddComponent(go, _vrcPhysBoneType);
        }

        public static Component AddPhysBoneCollider(GameObject go)
        {
            ResolveTypes();
            if (_vrcPhysBoneColliderType == null) return null;
            return Undo.AddComponent(go, _vrcPhysBoneColliderType);
        }

        public static void CopyPhysBoneProperties(Component src, Component dst)
        {
            if (src == null || dst == null) return;

            var props = new[]
            {
                "enabled", "rootTransform", "endpointPosition", "pull", "spring", "stiffness",
                "gravity", "gravityFalloff", "immobile", "integrateVelocity", "allowTranslation",
                "allowRotation", "maxStretch", "collisionRadius", "colliders", "ignoreColliders", "stationaryColliders"
            };

            foreach (var propName in props)
            {
                var prop = src.GetType().GetProperty(propName);
                if (prop != null && prop.CanRead && prop.CanWrite)
                {
                    try
                    {
                        var value = prop.GetValue(src);
                        prop.SetValue(dst, value);
                    }
                    catch { }
                }
            }
        }

        public static void CopyPhysBoneColliderProperties(Component src, Component dst)
        {
            if (src == null || dst == null) return;

            var props = new[] { "enabled", "radius", "shapeType", "height" };

            foreach (var propName in props)
            {
                var prop = src.GetType().GetProperty(propName);
                if (prop != null && prop.CanRead && prop.CanWrite)
                {
                    try
                    {
                        var value = prop.GetValue(src);
                        prop.SetValue(dst, value);
                    }
                    catch { }
                }
            }
        }
    }
}