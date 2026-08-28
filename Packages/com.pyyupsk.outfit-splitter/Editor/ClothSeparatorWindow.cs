using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Pyyupsk.OutfitSplitter.Editor
{
    public class ClothSeparatorWindow : EditorWindow
    {
        public enum SeparationMode
        {
            ByMaterial,
            BySubMesh
        }

        private SeparationMode _mode = SeparationMode.ByMaterial;
        private bool _partialMode = false;
        private bool _pruneBones = true;
        private bool _preservePhysBones = true;
        private Vector2 _scrollPosition;
        private List<SkinnedMeshRenderer> _targetRenderers = new List<SkinnedMeshRenderer>();
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.Info;

        [MenuItem(Version.MenuPath, false, 2000)]
        public static void ShowWindow()
        {
            GetWindow<ClothSeparatorWindow>("Outfit Splitter");
        }

        private void OnEnable()
        {
            minSize = new Vector2(360, 400);
            RefreshTargets();
        }

        private void OnSelectionChange()
        {
            RefreshTargets();
            Repaint();
        }

        private void RefreshTargets()
        {
            _targetRenderers.Clear();
            var selection = Selection.gameObjects;
            foreach (var go in selection)
            {
                var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                _targetRenderers.AddRange(renderers);
            }
            _targetRenderers = _targetRenderers.Distinct().ToList();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            DrawSDKWarning();
            DrawTargetList();
            DrawOptions();
            DrawActionButtons();
            DrawStatus();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Outfit Splitter", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"v{Version.VersionString}", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);
        }

        private void DrawTargetList()
        {
            EditorGUILayout.LabelField("Target Renderers", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select avatar root or specific SkinnedMeshRenderers in Hierarchy/Scene view.",
                MessageType.Info);

            if (_targetRenderers.Count == 0)
            {
                EditorGUILayout.LabelField("No SkinnedMeshRenderers found in selection.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var smr in _targetRenderers)
                {
                    EditorGUILayout.ObjectField(smr, typeof(SkinnedMeshRenderer), true);
                }
                EditorGUILayout.LabelField($"Total: {_targetRenderers.Count} renderer(s)", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(10);
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Separation Options", EditorStyles.boldLabel);

            _mode = (SeparationMode)EditorGUILayout.EnumPopup("Mode", _mode);
            EditorGUILayout.HelpBox(
                _mode == SeparationMode.ByMaterial
                    ? "Creates one piece per material slot on each renderer."
                    : "Creates one piece per sub-mesh index on each renderer.",
                MessageType.Info);

            _partialMode = EditorGUILayout.ToggleLeft("Partial Mode (selected meshes only)", _partialMode);
            if (_partialMode)
            {
                EditorGUILayout.HelpBox(
                    "Only separates the currently selected renderers. Unselected renderers on the same avatar remain untouched.",
                    MessageType.Info);
            }

            _pruneBones = EditorGUILayout.ToggleLeft("Prune Unused Bones", _pruneBones);
            _preservePhysBones = EditorGUILayout.ToggleLeft("Preserve PhysBone Setup", _preservePhysBones);

            EditorGUILayout.Space(10);
        }

        private void DrawActionButtons()
        {
            var canSplit = _targetRenderers.Count > 1;
            using (new EditorGUI.DisabledScope(!canSplit))
            {
                if (GUILayout.Button("Split Outfit", GUILayout.Height(36)))
                {
                    SplitOutfit();
                }
            }

            if (_targetRenderers.Count == 0)
            {
                EditorGUILayout.HelpBox("Select at least one SkinnedMeshRenderer to enable splitting.", MessageType.Warning);
            }
            else if (_targetRenderers.Count == 1)
            {
                EditorGUILayout.HelpBox("Select multiple SkinnedMeshRenderers to split. Single renderer cannot be split.", MessageType.Info);
            }

            if (GUILayout.Button("Refresh Selection", GUILayout.Height(24)))
            {
                RefreshTargets();
            }
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }
        }

        private void DrawSDKWarning()
        {
            if (!VRChatSDKHelper.HasVRChatSDK)
            {
                EditorGUILayout.HelpBox(
                    "VRChat SDK not detected. PhysBone preservation will be disabled.\n" +
                    "Install VRChat SDK3 Avatars via VCC for full functionality.",
                    MessageType.Warning);
                _preservePhysBones = false;
            }
            EditorGUILayout.Space(5);
        }

        private void SplitOutfit()
        {
            if (_targetRenderers.Count == 0)
            {
                SetStatus("No SkinnedMeshRenderers selected.", MessageType.Error);
                return;
            }

            try
            {
                var result = ClothSeparatorLogic.SplitOutfit(
                    _targetRenderers,
                    _mode,
                    _partialMode,
                    _pruneBones,
                    _preservePhysBones);

                SetStatus($"Success! Created {result.CreatedPieces} piece(s), pruned {result.PrunedBones} bone(s).", MessageType.Info);
                Selection.objects = result.CreatedObjects.ToArray();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OutfitSplitter] {e}");
                SetStatus($"Error: {e.Message}", MessageType.Error);
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }
    }
}