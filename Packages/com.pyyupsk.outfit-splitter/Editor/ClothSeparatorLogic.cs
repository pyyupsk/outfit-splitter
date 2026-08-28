using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pyyupsk.OutfitSplitter.Editor
{
    public struct SplitResult
    {
        public int CreatedPieces;
        public int PrunedBones;
        public List<GameObject> CreatedObjects;
    }

    public static class ClothSeparatorLogic
    {
        public static SplitResult SplitOutfit(
            List<SkinnedMeshRenderer> targets,
            ClothSeparatorWindow.SeparationMode mode,
            bool partialMode,
            bool pruneBones,
            bool preservePhysBones)
        {
            var result = new SplitResult
            {
                CreatedObjects = new List<GameObject>()
            };

            Undo.SetCurrentGroupName("Split Outfit");
            var group = Undo.GetCurrentGroup();

            // Group targets by their root avatar
            var grouped = targets.GroupBy(t => GetAvatarRoot(t.transform));

            foreach (var avatarGroup in grouped)
            {
                var avatarRoot = avatarGroup.Key;
                var renderers = avatarGroup.ToList();

                if (mode == ClothSeparatorWindow.SeparationMode.ByMaterial)
                {
                    // For ByMaterial, we need to split each renderer by material
                    foreach (var smr in renderers)
                    {
                        var pieces = SeparateByMaterial(smr, preservePhysBones, pruneBones);
                        foreach (var piece in pieces)
                        {
                            result.CreatedObjects.Add(piece);
                            result.CreatedPieces++;
                        }
                    }
                }
                else
                {
                    // BySubMesh - separate by sub-mesh, but for now treat as by mesh name
                    foreach (var smr in renderers)
                    {
                        var pieces = SeparateByMeshName(smr, preservePhysBones, pruneBones);
                        foreach (var piece in pieces)
                        {
                            result.CreatedObjects.Add(piece);
                            result.CreatedPieces++;
                        }
                    }
                }

                if (!partialMode)
                {
                    // Disable original renderers
                    foreach (var smr in renderers)
                    {
                        if (smr != null)
                        {
                            Undo.RecordObject(smr, "Disable Original Renderer");
                            smr.enabled = false;
                        }
                    }
                }
                else
                {
                    foreach (var smr in renderers)
                    {
                        if (smr != null)
                        {
                            Undo.RecordObject(smr, "Disable Original Renderer");
                            smr.enabled = false;
                        }
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
            return result;
        }

        private static Transform GetAvatarRoot(Transform t)
        {
            while (t.parent != null)
                t = t.parent;
            return t;
        }

        private static List<GameObject> SeparateByMaterial(SkinnedMeshRenderer source, bool preservePhysBones, bool pruneBones)
        {
            var pieces = new List<GameObject>();
            var materials = source.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                var piece = CreatePieceByMeshName(source, $"{source.gameObject.name}_Mat{i}", new[] { i }, preservePhysBones, pruneBones);
                if (piece != null) pieces.Add(piece);
            }

            return pieces;
        }

        private static List<GameObject> SeparateByMeshName(SkinnedMeshRenderer source, bool preservePhysBones, bool pruneBones)
        {
            var piece = CreatePieceByMeshName(source, source.gameObject.name, null, preservePhysBones, pruneBones);
            return piece != null ? new List<GameObject> { piece } : new List<GameObject>();
        }

        private static GameObject CreatePieceByMeshName(
            SkinnedMeshRenderer source,
            string pieceName,
            int[] materialIndices,
            bool preservePhysBones,
            bool pruneBones)
        {
            var sourceGo = source.gameObject;
            var avatarRoot = GetAvatarRoot(sourceGo.transform);

            // Instantiate the entire avatar hierarchy for this piece
            var pieceGo = UnityEngine.Object.Instantiate(avatarRoot.gameObject, avatarRoot.parent);
            pieceGo.name = $"{avatarRoot.name}-{pieceName}";

            Undo.RegisterCreatedObjectUndo(pieceGo, "Create Outfit Piece");

            // Find the corresponding renderer in the instantiated hierarchy
            var targetRenderer = FindRendererByOriginalName(pieceGo, sourceGo.name);
            if (targetRenderer == null)
            {
                Undo.DestroyObjectImmediate(pieceGo);
                return null;
            }

            // Prune meshes - keep only the target mesh
            PruneMeshes(pieceGo, targetRenderer.gameObject.name);

            // Prune bones if requested
            if (pruneBones)
            {
                PruneBones(targetRenderer, preservePhysBones);
            }

            return pieceGo;
        }

        private static SkinnedMeshRenderer FindRendererByOriginalName(GameObject root, string originalName)
        {
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return renderers.FirstOrDefault(r => r.gameObject.name == originalName);
        }

        private static void PruneMeshes(GameObject clothing, string keepMeshName)
        {
            var renderers = clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer.gameObject.name != keepMeshName)
                {
                    Undo.DestroyObjectImmediate(renderer.gameObject);
                }
            }
        }

        private static void PruneBones(SkinnedMeshRenderer targetRenderer, bool preservePhysBones)
        {
            if (targetRenderer == null || targetRenderer.rootBone == null) return;

            var necessaryBones = new HashSet<Transform>();
            var physBoneColliders = new HashSet<Transform>();
            var physBoneChildren = new HashSet<Transform>();

            // Get bones used by this mesh
            var boneWeights = targetRenderer.sharedMesh.boneWeights;
            var bones = targetRenderer.bones;

            foreach (var bw in boneWeights)
            {
                AddBoneWithParents(necessaryBones, bones, bw.boneIndex0);
                AddBoneWithParents(necessaryBones, bones, bw.boneIndex1);
                AddBoneWithParents(necessaryBones, bones, bw.boneIndex2);
                AddBoneWithParents(necessaryBones, bones, bw.boneIndex3);
            }

            if (preservePhysBones && VRChatSDKHelper.HasVRChatSDK)
            {
                AddPhysBoneCollidersAndChildren(targetRenderer.gameObject, necessaryBones, physBoneColliders, physBoneChildren);
            }

            // Prune from root bone downwards
            PruneUnnecessaryBones(targetRenderer.rootBone, necessaryBones, physBoneColliders, physBoneChildren);
        }

        private static void AddBoneWithParents(HashSet<Transform> set, Transform[] bones, int index)
        {
            if (index >= 0 && index < bones.Length)
            {
                var bone = bones[index];
                while (bone != null)
                {
                    set.Add(bone);
                    bone = bone.parent;
                }
            }
        }

        private static void AddPhysBoneCollidersAndChildren(GameObject clothing, HashSet<Transform> necessaryBones, HashSet<Transform> physBoneColliders, HashSet<Transform> physBoneChildren)
        {
            var physBones = VRChatSDKHelper.GetPhysBones(clothing);
            foreach (var pb in physBones)
            {
                var physBoneTransform = pb.transform;
                if (necessaryBones.Contains(physBoneTransform))
                {
                    // Add colliders and their parent chains
                    var colliders = VRChatSDKHelper.GetPhysBoneColliders(clothing);
                    foreach (var col in colliders)
                    {
                        if (col != null)
                        {
                            var colTransform = col.transform;
                            physBoneColliders.Add(colTransform);
                            AddBoneAndParents(physBoneColliders, colTransform, clothing.transform);
                        }
                    }

                    // Add PhysBone children
                    AddBoneAndChildren(physBoneChildren, physBoneTransform);
                }
            }
        }

        private static void AddBoneAndParents(HashSet<Transform> set, Transform bone, Transform stopAt)
        {
            while (bone != null && bone != stopAt)
            {
                set.Add(bone);
                bone = bone.parent;
            }
        }

        private static void AddBoneAndChildren(HashSet<Transform> set, Transform bone)
        {
            set.Add(bone);
            foreach (Transform child in bone)
            {
                AddBoneAndChildren(set, child);
            }
        }

        private static void PruneUnnecessaryBones(Transform currentBone, HashSet<Transform> necessaryBones, HashSet<Transform> physBoneColliders, HashSet<Transform> physBoneChildren)
        {
            for (int i = currentBone.childCount - 1; i >= 0; i--)
            {
                var child = currentBone.GetChild(i);
                bool isNecessary = necessaryBones.Contains(child) || physBoneColliders.Contains(child) || physBoneChildren.Contains(child);

                if (!isNecessary)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
                else
                {
                    PruneUnnecessaryBones(child, necessaryBones, physBoneColliders, physBoneChildren);
                }
            }
        }
    }
}