using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

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

            foreach (var smr in targets)
            {
                if (smr == null) continue;

                var pieces = mode == ClothSeparatorWindow.SeparationMode.ByMaterial
                    ? SeparateByMaterial(smr, preservePhysBones)
                    : SeparateBySubMesh(smr, preservePhysBones);

                foreach (var piece in pieces)
                {
                    if (pruneBones)
                    {
                        result.PrunedBones += PruneUnusedBones(piece);
                    }

                    result.CreatedObjects.Add(piece);
                    result.CreatedPieces++;
                }

                if (!partialMode)
                {
                    Undo.DestroyObjectImmediate(smr.gameObject);
                }
                else
                {
                    Undo.RecordObject(smr, "Disable Original Renderer");
                    smr.enabled = false;
                }
            }

            Undo.CollapseUndoOperations(group);
            return result;
        }

        private static List<GameObject> SeparateByMaterial(SkinnedMeshRenderer source, bool preservePhysBones)
        {
            var pieces = new List<GameObject>();
            var materials = source.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                var piece = CreatePiece(source, i, new[] { i }, preservePhysBones);
                if (piece != null) pieces.Add(piece);
            }

            return pieces;
        }

        private static List<GameObject> SeparateBySubMesh(SkinnedMeshRenderer source, bool preservePhysBones)
        {
            var pieces = new List<GameObject>();
            var mesh = source.sharedMesh;

            if (mesh == null) return pieces;

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var piece = CreatePiece(source, i, new[] { i }, preservePhysBones);
                if (piece != null) pieces.Add(piece);
            }

            return pieces;
        }

        private static GameObject CreatePiece(
            SkinnedMeshRenderer source,
            int index,
            int[] subMeshIndices,
            bool preservePhysBones)
        {
            var sourceGo = source.gameObject;
            var pieceGo = new GameObject($"{sourceGo.name}_Piece_{index}");

            Undo.RegisterCreatedObjectUndo(pieceGo, "Create Outfit Piece");
            pieceGo.transform.SetParent(sourceGo.transform.parent, false);
            pieceGo.transform.localPosition = sourceGo.transform.localPosition;
            pieceGo.transform.localRotation = sourceGo.transform.localRotation;
            pieceGo.transform.localScale = sourceGo.transform.localScale;

            var newSmr = pieceGo.AddComponent<SkinnedMeshRenderer>();
            Undo.RecordObject(newSmr, "Configure New SMR");

            var newMesh = ExtractSubMesh(source.sharedMesh, subMeshIndices);
            if (newMesh == null)
            {
                Undo.DestroyObjectImmediate(pieceGo);
                return null;
            }

            newSmr.sharedMesh = newMesh;
            newSmr.sharedMaterials = subMeshIndices.Select(i => source.sharedMaterials[i]).ToArray();
            newSmr.bones = source.bones;
            newSmr.rootBone = source.rootBone;
            newSmr.quality = source.quality;
            newSmr.updateWhenOffscreen = source.updateWhenOffscreen;
            newSmr.skinnedMotionVectors = source.skinnedMotionVectors;
            newSmr.renderingLayerMask = source.renderingLayerMask;
            newSmr.receiveShadows = source.receiveShadows;
            newSmr.shadowCastingMode = source.shadowCastingMode;
            newSmr.motionVectorGenerationMode = source.motionVectorGenerationMode;
            newSmr.lightProbeUsage = source.lightProbeUsage;
            newSmr.reflectionProbeUsage = source.reflectionProbeUsage;
            newSmr.probeAnchor = source.probeAnchor;

            if (preservePhysBones)
            {
                CopyPhysBoneSetup(sourceGo, pieceGo, subMeshIndices);
            }

            return pieceGo;
        }

        private static Mesh ExtractSubMesh(Mesh sourceMesh, int[] subMeshIndices)
        {
            if (sourceMesh == null) return null;

            var newMesh = new Mesh();
            Undo.RegisterCreatedObjectUndo(newMesh, "Create Extracted Mesh");
            newMesh.name = $"{sourceMesh.name}_SubMesh_{string.Join("_", subMeshIndices)}";

            var vertexCount = sourceMesh.vertexCount;
            var boneWeights = sourceMesh.boneWeights;
            var bindposes = sourceMesh.bindposes;

            var usedVertices = new HashSet<int>();
            var usedBones = new HashSet<int>();
            var triangles = new List<int>();

            foreach (int subMeshIndex in subMeshIndices)
            {
                if (subMeshIndex >= sourceMesh.subMeshCount) continue;

                var indices = sourceMesh.GetTriangles(subMeshIndex);
                triangles.AddRange(indices);
                foreach (int idx in indices) usedVertices.Add(idx);
            }

            if (triangles.Count == 0)
            {
                Object.DestroyImmediate(newMesh);
                return null;
            }

            var vertexMap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newTangents = new List<Vector4>();
            var newUVs = new List<Vector2>();
            var newUV2s = new List<Vector2>();
            var newUV3s = new List<Vector2>();
            var newUV4s = new List<Vector2>();
            var newColors = new List<Color>();
            var newBoneWeights = new List<BoneWeight>();

            int newIndex = 0;
            foreach (int oldIndex in usedVertices.OrderBy(x => x))
            {
                vertexMap[oldIndex] = newIndex++;
                newVertices.Add(sourceMesh.vertices[oldIndex]);
                if (sourceMesh.normals.Length > oldIndex) newNormals.Add(sourceMesh.normals[oldIndex]);
                if (sourceMesh.tangents.Length > oldIndex) newTangents.Add(sourceMesh.tangents[oldIndex]);
                if (sourceMesh.uv.Length > oldIndex) newUVs.Add(sourceMesh.uv[oldIndex]);
                if (sourceMesh.uv2.Length > oldIndex) newUV2s.Add(sourceMesh.uv2[oldIndex]);
                if (sourceMesh.uv3.Length > oldIndex) newUV3s.Add(sourceMesh.uv3[oldIndex]);
                if (sourceMesh.uv4.Length > oldIndex) newUV4s.Add(sourceMesh.uv4[oldIndex]);
                if (sourceMesh.colors.Length > oldIndex) newColors.Add(sourceMesh.colors[oldIndex]);
                if (boneWeights.Length > oldIndex)
                {
                    var bw = boneWeights[oldIndex];
                    newBoneWeights.Add(bw);
                    if (bw.boneIndex0 >= 0) usedBones.Add(bw.boneIndex0);
                    if (bw.boneIndex1 >= 0) usedBones.Add(bw.boneIndex1);
                    if (bw.boneIndex2 >= 0) usedBones.Add(bw.boneIndex2);
                    if (bw.boneIndex3 >= 0) usedBones.Add(bw.boneIndex3);
                }
            }

            var remappedTriangles = triangles.Select(i => vertexMap[i]).ToArray();

            newMesh.SetVertices(newVertices);
            if (newNormals.Count > 0) newMesh.SetNormals(newNormals);
            if (newTangents.Count > 0) newMesh.SetTangents(newTangents);
            if (newUVs.Count > 0) newMesh.SetUVs(0, newUVs);
            if (newUV2s.Count > 0) newMesh.SetUVs(1, newUV2s);
            if (newUV3s.Count > 0) newMesh.SetUVs(2, newUV3s);
            if (newUV4s.Count > 0) newMesh.SetUVs(3, newUV4s);
            if (newColors.Count > 0) newMesh.SetColors(newColors);
            if (newBoneWeights.Count > 0) newMesh.boneWeights = newBoneWeights.ToArray();

            newMesh.subMeshCount = 1;
            newMesh.SetTriangles(remappedTriangles, 0);

            var boneMap = new Dictionary<int, int>();
            var newBindposes = new Matrix4x4[usedBones.Count];
            int b = 0;
            foreach (int oldBoneIndex in usedBones.OrderBy(x => x))
            {
                boneMap[oldBoneIndex] = b;
                newBindposes[b] = bindposes[oldBoneIndex];
                b++;
            }

            newMesh.bindposes = newBindposes;

            for (int i = 0; i < newMesh.boneWeights.Length; i++)
            {
                var bw = newMesh.boneWeights[i];
                bw.boneIndex0 = bw.boneIndex0 >= 0 ? boneMap[bw.boneIndex0] : -1;
                bw.boneIndex1 = bw.boneIndex1 >= 0 ? boneMap[bw.boneIndex1] : -1;
                bw.boneIndex2 = bw.boneIndex2 >= 0 ? boneMap[bw.boneIndex2] : -1;
                bw.boneIndex3 = bw.boneIndex3 >= 0 ? boneMap[bw.boneIndex3] : -1;
                newMesh.boneWeights[i] = bw;
            }

            newMesh.RecalculateBounds();
            return newMesh;
        }

        private static void CopyPhysBoneSetup(GameObject source, GameObject target, int[] subMeshIndices)
        {
            var sourcePhysBones = source.GetComponentsInChildren<VRCPhysBone>(true);
            var sourceColliders = source.GetComponentsInChildren<VRCPhysBoneCollider>(true);

            foreach (var srcPb in sourcePhysBones)
            {
                if (srcPb == null) continue;

                var targetPb = Undo.AddComponent<VRCPhysBone>(target);
                Undo.RecordObject(targetPb, "Copy PhysBone Settings");

                targetPb.enabled = srcPb.enabled;
                targetPb.rootTransform = srcPb.rootTransform;
                targetPb.endpointPosition = srcPb.endpointPosition;
                targetPb.pull = srcPb.pull;
                targetPb.spring = srcPb.spring;
                targetPb.stiffness = srcPb.stiffness;
                targetPb.gravity = srcPb.gravity;
                targetPb.gravityFalloff = srcPb.gravityFalloff;
                targetPb.immobile = srcPb.immobile;
                targetPb.integrateVelocity = srcPb.integrateVelocity;
                targetPb.allowTranslation = srcPb.allowTranslation;
                targetPb.allowRotation = srcPb.allowRotation;
                targetPb.maxStretch = srcPb.maxStretch;
                targetPb.collisionRadius = srcPb.collisionRadius;
                targetPb.colliders = srcPb.colliders?.Select(c => c).ToArray() ?? System.Array.Empty<VRCPhysBoneCollider>();
                targetPb.ignoreColliders = srcPb.ignoreColliders?.Select(c => c).ToArray() ?? System.Array.Empty<Collider>();
                targetPb.stationaryColliders = srcPb.stationaryColliders?.Select(c => c).ToArray() ?? System.Array.Empty<Collider>();
            }

            foreach (var srcCol in sourceColliders)
            {
                if (srcCol == null) continue;

                var targetCol = Undo.AddComponent<VRCPhysBoneCollider>(target);
                Undo.RecordObject(targetCol, "Copy PhysBoneCollider Settings");

                targetCol.enabled = srcCol.enabled;
                targetCol.radius = srcCol.radius;
                targetCol.shapeType = srcCol.shapeType;
                targetCol.height = srcCol.height;
            }
        }

        private static int PruneUnusedBones(GameObject pieceRoot)
        {
            var smr = pieceRoot.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.bones == null || smr.rootBone == null) return 0;

            var usedBones = new HashSet<Transform>();
            foreach (var bw in smr.sharedMesh.boneWeights)
            {
                if (bw.boneIndex0 >= 0 && bw.boneIndex0 < smr.bones.Length)
                    usedBones.Add(smr.bones[bw.boneIndex0]);
                if (bw.boneIndex1 >= 0 && bw.boneIndex1 < smr.bones.Length)
                    usedBones.Add(smr.bones[bw.boneIndex1]);
                if (bw.boneIndex2 >= 0 && bw.boneIndex2 < smr.bones.Length)
                    usedBones.Add(smr.bones[bw.boneIndex2]);
                if (bw.boneIndex3 >= 0 && bw.boneIndex3 < smr.bones.Length)
                    usedBones.Add(smr.bones[bw.boneIndex3]);
            }

            var allBones = smr.bones.Where(b => b != null).ToList();
            var pruned = 0;

            foreach (var bone in allBones)
            {
                if (!usedBones.Contains(bone) && bone != smr.rootBone)
                {
                    Undo.DestroyObjectImmediate(bone.gameObject);
                    pruned++;
                }
            }

            var newBones = allBones.Where(b => b != null && (usedBones.Contains(b) || b == smr.rootBone)).ToArray();
            Undo.RecordObject(smr, "Update Bones Array");
            smr.bones = newBones;

            return pruned;
        }
    }
}