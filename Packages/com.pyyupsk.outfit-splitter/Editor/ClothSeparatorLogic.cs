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

        public struct ExtractedMeshData
        {
            public Mesh Mesh;
            public Transform[] Bones;
            public Transform RootBone;
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

            var extracted = ExtractSubMesh(source.sharedMesh, subMeshIndices, source.bones, source.rootBone);
            if (extracted.Mesh == null)
            {
                Undo.DestroyObjectImmediate(pieceGo);
                return null;
            }

            newSmr.sharedMaterials = subMeshIndices.Select(i => source.sharedMaterials[i]).ToArray();
            newSmr.bones = extracted.Bones;
            newSmr.rootBone = extracted.RootBone;
            newSmr.sharedMesh = extracted.Mesh;
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

            if (preservePhysBones && VRChatSDKHelper.HasVRChatSDK)
            {
                CopyPhysBoneSetup(sourceGo, pieceGo, subMeshIndices);
            }

            return pieceGo;
        }

        private static ExtractedMeshData ExtractSubMesh(Mesh sourceMesh, int[] subMeshIndices, Transform[] sourceBones, Transform sourceRootBone)
        {
            var result = new ExtractedMeshData();
            if (sourceMesh == null) return result;

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
                return default;
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

            // Build the bones array that matches the new bindposes
            var usedBoneIndices = usedBones.OrderBy(x => x).ToList();
            var newBones = new Transform[usedBoneIndices.Count];
            for (int i = 0; i < usedBoneIndices.Count; i++)
            {
                int oldBoneIndex = usedBoneIndices[i];
                if (oldBoneIndex < sourceBones.Length && sourceBones[oldBoneIndex] != null)
                {
                    newBones[i] = sourceBones[oldBoneIndex];
                }
            }

            // Find root bone in the new bones array
            Transform newRootBone = null;
            if (sourceRootBone != null)
            {
                // Try to find the root bone in the new bones array
                for (int i = 0; i < newBones.Length; i++)
                {
                    if (newBones[i] == sourceRootBone)
                    {
                        newRootBone = newBones[i];
                        break;
                    }
                }
                // If not found, use the first bone or null
                if (newRootBone == null && newBones.Length > 0)
                {
                    newRootBone = newBones[0];
                }
            }

            result.Mesh = newMesh;
            result.Bones = newBones;
            result.RootBone = newRootBone;
            return result;
        }

        private static void CopyPhysBoneSetup(GameObject source, GameObject target, int[] subMeshIndices)
        {
            var sourcePhysBones = VRChatSDKHelper.GetPhysBones(source);
            var sourceColliders = VRChatSDKHelper.GetPhysBoneColliders(source);

            foreach (var srcPb in sourcePhysBones)
            {
                if (srcPb == null) continue;

                var targetPb = VRChatSDKHelper.AddPhysBone(target);
                if (targetPb == null) continue;

                Undo.RecordObject(targetPb, "Copy PhysBone Settings");
                VRChatSDKHelper.CopyPhysBoneProperties(srcPb, targetPb);
            }

            foreach (var srcCol in sourceColliders)
            {
                if (srcCol == null) continue;

                var targetCol = VRChatSDKHelper.AddPhysBoneCollider(target);
                if (targetCol == null) continue;

                Undo.RecordObject(targetCol, "Copy PhysBoneCollider Settings");
                VRChatSDKHelper.CopyPhysBoneColliderProperties(srcCol, targetCol);
            }
        }

        private static int PruneUnusedBones(GameObject pieceRoot)
        {
            var smr = pieceRoot.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.bones == null || smr.rootBone == null) return 0;

            var mesh = smr.sharedMesh;
            if (mesh == null) return 0;

            var usedBones = new HashSet<Transform>();
            foreach (var bw in mesh.boneWeights)
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

            // Sort by hierarchy depth (deepest first) to avoid destroying parents before children
            allBones.Sort((a, b) => GetHierarchyDepth(b).CompareTo(GetHierarchyDepth(a)));

            foreach (var bone in allBones)
            {
                if (bone == null) continue;
                if (!usedBones.Contains(bone) && bone != smr.rootBone)
                {
                    Undo.DestroyObjectImmediate(bone.gameObject);
                    pruned++;
                }
            }

            // Rebuild bone array and remap bone weights
            var oldBones = smr.bones;
            var newBonesList = new List<Transform>();
            var oldToNewIndex = new Dictionary<int, int>();

            for (int i = 0; i < oldBones.Length; i++)
            {
                var bone = oldBones[i];
                if (bone != null && (usedBones.Contains(bone) || bone == smr.rootBone))
                {
                    oldToNewIndex[i] = newBonesList.Count;
                    newBonesList.Add(bone);
                }
            }

            var newBones = newBonesList.ToArray();

            // Remap bone weights to new indices
            var newBoneWeights = new BoneWeight[mesh.boneWeights.Length];
            for (int i = 0; i < mesh.boneWeights.Length; i++)
            {
                var bw = mesh.boneWeights[i];
                newBoneWeights[i] = new BoneWeight
                {
                    boneIndex0 = bw.boneIndex0 >= 0 && oldToNewIndex.TryGetValue(bw.boneIndex0, out var ni0) ? ni0 : -1,
                    weight0 = bw.weight0,
                    boneIndex1 = bw.boneIndex1 >= 0 && oldToNewIndex.TryGetValue(bw.boneIndex1, out var ni1) ? ni1 : -1,
                    weight1 = bw.weight1,
                    boneIndex2 = bw.boneIndex2 >= 0 && oldToNewIndex.TryGetValue(bw.boneIndex2, out var ni2) ? ni2 : -1,
                    weight2 = bw.weight2,
                    boneIndex3 = bw.boneIndex3 >= 0 && oldToNewIndex.TryGetValue(bw.boneIndex3, out var ni3) ? ni3 : -1,
                    weight3 = bw.weight3,
                };
            }

            // Create new bindposes matching the new bones array (copy from original mesh for kept bones)
            var oldBindposes = mesh.bindposes;
            var newBindposes = new Matrix4x4[newBones.Length];
            var oldToNewBindposeIndex = new Dictionary<int, int>();
            int newBindposeIdx = 0;
            for (int i = 0; i < oldBones.Length; i++)
            {
                var bone = oldBones[i];
                if (bone != null && (usedBones.Contains(bone) || bone == smr.rootBone))
                {
                    oldToNewBindposeIndex[i] = newBindposeIdx;
                    if (i < oldBindposes.Length)
                    {
                        newBindposes[newBindposeIdx] = oldBindposes[i];
                    }
                    newBindposeIdx++;
                }
            }

            // Create completely new mesh with correct bindposes (don't instantiate - bindposes would mismatch)
            var newMesh = new Mesh();
            Undo.RegisterCreatedObjectUndo(newMesh, "Create Pruned Mesh");
            newMesh.name = mesh.name + "_Pruned";

            newMesh.SetVertices(mesh.vertices);
            if (mesh.normals.Length > 0) newMesh.SetNormals(mesh.normals);
            if (mesh.tangents.Length > 0) newMesh.SetTangents(mesh.tangents);
            if (mesh.uv.Length > 0) newMesh.SetUVs(0, mesh.uv);
            if (mesh.uv2.Length > 0) newMesh.SetUVs(1, mesh.uv2);
            if (mesh.uv3.Length > 0) newMesh.SetUVs(2, mesh.uv3);
            if (mesh.uv4.Length > 0) newMesh.SetUVs(3, mesh.uv4);
            if (mesh.colors.Length > 0) newMesh.SetColors(mesh.colors);
            newMesh.boneWeights = newBoneWeights;
            newMesh.bindposes = newBindposes;
            newMesh.subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                newMesh.SetTriangles(mesh.GetTriangles(s), s);
            }
            newMesh.RecalculateBounds();

            Undo.RecordObject(smr, "Update Bones and Mesh");
            smr.bones = newBones;
            smr.sharedMesh = newMesh;

            return pruned;
        }

        private static int GetHierarchyDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                t = t.parent;
                depth++;
            }
            return depth;
        }
    }
}