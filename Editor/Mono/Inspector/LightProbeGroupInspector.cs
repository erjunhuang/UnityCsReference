// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEditorInternal;
using UnityEngine;
using System.Linq;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;

namespace UnityEditor
{
    internal class LightProbeGroupEditor : IEditablePoint
    {
        private bool m_Editing;

        private List<Vector3> m_SourcePositions;
        private List<int> m_Selection = new List<int>();

        private LightProbeGroupSelection m_SerializedSelectedProbes;

        private readonly LightProbeGroup m_Group;
        private bool m_ShouldRecalculateTetrahedra;
        private bool m_SourcePositionsDirty;
        private Vector3 m_LastPosition = Vector3.zero;
        private Quaternion m_LastRotation = Quaternion.identity;
        private Vector3 m_LastScale = Vector3.one;

        public SavedBool drawTetrahedra { get; set; }
        public bool deringProbes { get { return m_Group.dering; } set { m_Group.dering = value; } }

        public LightProbeGroupEditor(LightProbeGroup group)
        {
            m_Group = group;
            m_ShouldRecalculateTetrahedra = false;
            m_SourcePositionsDirty = false;
            m_SerializedSelectedProbes = ScriptableObject.CreateInstance<LightProbeGroupSelection>();
            m_SerializedSelectedProbes.hideFlags = HideFlags.HideAndDontSave;
        }

        public void SetEditing(bool editing)
        {
            m_Editing = editing;
        }

        public void AddProbe(Vector3 position)
        {
            Undo.RegisterCompleteObjectUndo(new Object[] { m_Group, m_SerializedSelectedProbes }, "Add Probe");
            m_SourcePositions.Add(position);
            SelectProbe(m_SourcePositions.Count - 1);

            MarkSourcePositionsDirty();
        }

        private void SelectProbe(int i)
        {
            if (!m_Selection.Contains(i))
                m_Selection.Add(i);
        }

        public void SelectAllProbes()
        {
            DeselectProbes();

            var count = m_SourcePositions.Count;
            for (var i = 0; i < count; i++)
                m_Selection.Add(i);
        }

        public void DeselectProbes()
        {
            m_Selection.Clear();
            m_SerializedSelectedProbes.m_Selection = m_Selection;
        }

        private IEnumerable<Vector3> SelectedProbePositions()
        {
            return m_Selection.Select(t => m_SourcePositions[t]).ToList();
        }

        public void DuplicateSelectedProbes()
        {
            var selectionCount = m_Selection.Count;
            if (selectionCount == 0) return;

            Undo.RegisterCompleteObjectUndo(new Object[] { m_Group, m_SerializedSelectedProbes }, "Duplicate Probes");

            foreach (var position in SelectedProbePositions())
            {
                m_SourcePositions.Add(position);
            }

            MarkSourcePositionsDirty();
        }

        private void CopySelectedProbes()
        {
            //Convert probes to world position for serialization
            var localPositions = SelectedProbePositions();

            var serializer = new XmlSerializer(typeof(Vector3[]));
            var writer = new StringWriter();

            serializer.Serialize(writer, localPositions.Select(pos => m_Group.transform.TransformPoint(pos)).ToArray());
            writer.Close();
            GUIUtility.systemCopyBuffer = writer.ToString();
        }

        private static bool CanPasteProbes()
        {
            try
            {
                var deserializer = new XmlSerializer(typeof(Vector3[]));
                var reader = new StringReader(GUIUtility.systemCopyBuffer);
                deserializer.Deserialize(reader);
                reader.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool PasteProbes()
        {
            //If we can't paste / paste buffer is bad do nothing
            try
            {
                var deserializer = new XmlSerializer(typeof(Vector3[]));
                var reader = new StringReader(GUIUtility.systemCopyBuffer);
                var pastedProbes = (Vector3[])deserializer.Deserialize(reader);
                reader.Close();

                if (pastedProbes.Length == 0) return false;

                Undo.RegisterCompleteObjectUndo(new Object[] { m_Group, m_SerializedSelectedProbes }, "Paste Probes");

                var oldLength = m_SourcePositions.Count;

                //Need to convert into local space...
                foreach (var position in pastedProbes)
                {
                    m_SourcePositions.Add(m_Group.transform.InverseTransformPoint(position));
                }

                //Change selection to be the newly pasted probes
                DeselectProbes();
                for (int i = oldLength; i < oldLength + pastedProbes.Length; i++)
                {
                    SelectProbe(i);
                }
                MarkSourcePositionsDirty();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RemoveSelectedProbes()
        {
            int selectionCount = m_Selection.Count;
            if (selectionCount == 0)
                return;

            Undo.RegisterCompleteObjectUndo(new Object[] { m_Group, m_SerializedSelectedProbes }, "Delete Probes");

            var reverseSortedIndicies = m_Selection.OrderByDescending(x => x);
            foreach (var index in reverseSortedIndicies)
            {
                m_SourcePositions.RemoveAt(index);
            }
            DeselectProbes();
            MarkSourcePositionsDirty();
        }

        public void PullProbePositions()
        {
            if (m_Group != null && m_SerializedSelectedProbes != null)
            {
                m_SourcePositions = new List<Vector3>(m_Group.probePositions);
                m_Selection = new List<int>(m_SerializedSelectedProbes.m_Selection);
            }
        }

        public void PushProbePositions()
        {
            if (m_SourcePositionsDirty)
            {
                m_Group.probePositions = m_SourcePositions.ToArray();
                m_SourcePositionsDirty = false;
            }

            m_SerializedSelectedProbes.m_Selection = m_Selection;
        }

        private void DrawTetrahedra()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (SceneView.lastActiveSceneView)
            {
                LightProbeVisualization.DrawTetrahedra(m_ShouldRecalculateTetrahedra,
                    SceneView.lastActiveSceneView.camera.transform.position);
                m_ShouldRecalculateTetrahedra = false;
            }
        }

        public void HandleEditMenuHotKeyCommands()
        {
            //Handle other events!
            if (Event.current.type == EventType.ValidateCommand
                || Event.current.type == EventType.ExecuteCommand)
            {
                bool execute = Event.current.type == EventType.ExecuteCommand;
                switch (Event.current.commandName)
                {
                    case EventCommandNames.SoftDelete:
                    case EventCommandNames.Delete:
                        if (execute) RemoveSelectedProbes();
                        Event.current.Use();
                        break;
                    case EventCommandNames.Duplicate:
                        if (execute) DuplicateSelectedProbes();
                        Event.current.Use();
                        break;
                    case EventCommandNames.SelectAll:
                        if (execute)
                            SelectAllProbes();
                        Event.current.Use();
                        break;
                    case EventCommandNames.Cut:
                        if (execute)
                        {
                            CopySelectedProbes();
                            RemoveSelectedProbes();
                        }
                        Event.current.Use();
                        break;
                    case EventCommandNames.Copy:
                        if (execute) CopySelectedProbes();
                        Event.current.Use();
                        break;
                }
            }
        }

        public static void TetrahedralizeSceneProbes(out Vector3[] positions, out int[] indices)
        {
            var probeGroups = Object.FindObjectsOfType(typeof(LightProbeGroup)) as LightProbeGroup[];

            if (probeGroups == null)
            {
                positions = new Vector3[0];
                indices = new int[0];
                return;
            }
            var probePositions = new List<Vector3>();

            foreach (var group in probeGroups)
            {
                var localPositions = group.probePositions;
                foreach (var position in localPositions)
                {
                    var wPosition = group.transform.TransformPoint(position);
                    probePositions.Add(wPosition);
                }
            }

            if (probePositions.Count == 0)
            {
                positions = new Vector3[0];
                indices = new int[0];
                return;
            }

            Lightmapping.Tetrahedralize(probePositions.ToArray(), out indices, out positions);
        }

        public bool OnSceneGUI(Transform transform)
        {
            if (!m_Group.enabled || SupportedRenderingFeatures.active.overridesLightProbeSystem)
                return m_Editing;

            if (Event.current.type == EventType.Layout)
            {
                //If the group has moved / scaled since last frame need to retetra);)
                if (m_LastPosition != m_Group.transform.position
                    || m_LastRotation != m_Group.transform.rotation
                    || m_LastScale != m_Group.transform.localScale)
                {
                    MarkSourcePositionsDirty();
                }

                m_LastPosition = m_Group.transform.position;
                m_LastRotation = m_Group.transform.rotation;
                m_LastScale = m_Group.transform.localScale;
            }

            //Need to cache this as select points will use it!
            var mouseUpEvent = Event.current.type == EventType.MouseUp;

            if (m_Editing)
            {
                if (PointEditor.SelectPoints(this, transform, ref m_Selection))
                {
                    Undo.RegisterCompleteObjectUndo(new Object[] { m_Group, m_SerializedSelectedProbes }, "Select Probes");
                }
            }

            //Special handling for paste (want to be able to paste when not in edit mode!)

            if ((Event.current.type == EventType.ValidateCommand || Event.current.type == EventType.ExecuteCommand)
                && Event.current.commandName == EventCommandNames.Paste)
            {
                if (Event.current.type == EventType.ValidateCommand)
                {
                    if (CanPasteProbes())
                        Event.current.Use();
                }
                if (Event.current.type == EventType.ExecuteCommand)
                {
                    if (PasteProbes())
                    {
                        Event.current.Use();
                        m_Editing = true;
                    }
                }
            }

            if (drawTetrahedra)
                DrawTetrahedra();

            PointEditor.Draw(this, transform, m_Selection, true);

            if (!m_Editing)
                return m_Editing;

            HandleEditMenuHotKeyCommands();

            if (m_Editing && PointEditor.MovePoints(this, transform, m_Selection))
            {
                Undo.RegisterCompleteObjectUndo(new Object[] { m_Group, m_SerializedSelectedProbes }, "Move Probes");
                if (LightProbeVisualization.dynamicUpdateLightProbes)
                    MarkSourcePositionsDirty();
            }

            if (m_Editing && mouseUpEvent && !LightProbeVisualization.dynamicUpdateLightProbes)
            {
                MarkSourcePositionsDirty();
            }

            return m_Editing;
        }

        public void MarkSourcePositionsDirty()
        {
            m_ShouldRecalculateTetrahedra = true;
            m_SourcePositionsDirty = true;
        }

        public Bounds selectedProbeBounds
        {
            get
            {
                List<Vector3> selectedPoints = new List<Vector3>();
                foreach (var idx in m_Selection)
                    selectedPoints.Add(m_SourcePositions[(int)idx]);
                return GetBounds(selectedPoints);
            }
        }

        public Bounds bounds
        {
            get { return GetBounds(m_SourcePositions); }
        }

        private Bounds GetBounds(List<Vector3> positions)
        {
            if (positions.Count == 0)
                return new Bounds();

            if (positions.Count == 1)
                return new Bounds(m_Group.transform.TransformPoint(positions[0]), new Vector3(1f, 1f, 1f));

            return GeometryUtility.CalculateBounds(positions.ToArray(), m_Group.transform.localToWorldMatrix);
        }

        /// Get the world-space position of a specific point
        public Vector3 GetPosition(int idx)
        {
            return m_SourcePositions[idx];
        }

        public Vector3 GetWorldPosition(int idx)
        {
            return m_Group.transform.TransformPoint(m_SourcePositions[idx]);
        }

        public void SetPosition(int idx, Vector3 position)
        {
            if (m_SourcePositions[idx] == position)
                return;

            m_SourcePositions[idx] = position;
            MarkSourcePositionsDirty();
        }

        private static readonly Color kCloudColor = new Color(200f / 255f, 200f / 255f, 20f / 255f, 0.85f);
        private static readonly Color kSelectedCloudColor = new Color(.3f, .6f, 1, 1);

        public Color GetDefaultColor()
        {
            return kCloudColor;
        }

        public Color GetSelectedColor()
        {
            return kSelectedCloudColor;
        }

        public float GetPointScale()
        {
            // Should match LightProbeVisualizationSettings::GetLightProbeSize()
            return 10.0f * AnnotationUtility.iconSize;
        }

        public Vector3[] GetSelectedPositions()
        {
            var selectedCount = SelectedCount;
            var result = new Vector3[selectedCount];
            for (int i = 0; i < selectedCount; i++)
            {
                result[i] = m_SourcePositions[m_Selection[i]];
            }
            return result;
        }

        public void UpdateSelectedPosition(int idx, Vector3 position)
        {
            if (idx > (SelectedCount - 1))
                return;

            m_SourcePositions[m_Selection[idx]] = position;

            MarkSourcePositionsDirty();
        }

        public IEnumerable<Vector3> GetPositions()
        {
            return m_SourcePositions;
        }

        public Vector3[] GetUnselectedPositions()
        {
            var totalProbeCount = Count;
            var selectedProbeCount = SelectedCount;

            if (selectedProbeCount == totalProbeCount)
            {
                return new Vector3[0];
            }
            else if (selectedProbeCount == 0)
            {
                return m_SourcePositions.ToArray();
            }
            else
            {
                var selectionList = new bool[totalProbeCount];

                // Mark everything unselected
                for (int i = 0; i < totalProbeCount; i++)
                {
                    selectionList[i] = false;
                }

                // Mark selected
                for (int i = 0; i < selectedProbeCount; i++)
                {
                    selectionList[m_Selection[i]] = true;
                }

                // Get remaining unselected
                var result = new Vector3[totalProbeCount - selectedProbeCount];
                var unselectedCount = 0;
                for (int i = 0; i < totalProbeCount; i++)
                {
                    if (selectionList[i] == false)
                    {
                        result[unselectedCount++] = m_SourcePositions[i];
                    }
                }

                return result;
            }
        }

        /// How many points are there in the array.
        public int Count { get { return m_SourcePositions.Count; } }


        /// How many points are selected in the array.
        public int SelectedCount { get { return m_Selection.Count; } }
    }

    [CustomEditor(typeof(LightProbeGroup))]
    class LightProbeGroupInspector : Editor
    {
        private static class Styles
        {
            public static readonly GUIContent showWireframe = EditorGUIUtility.TrTextContent("Show Wireframe", "Show the tetrahedron wireframe visualizing the blending between probes.");
            public static readonly GUIContent selectedProbePosition = EditorGUIUtility.TrTextContent("Selected Probe Position", "The local position of this probe relative to the parent group.");
            public static readonly GUIContent addProbe = EditorGUIUtility.TrTextContent("Add Probe", "Add a Light Probe to the Light Probe Group.");
            public static readonly GUIContent deleteSelected = EditorGUIUtility.TrTextContent("Delete Selected", "Delete the selected Light Probes from the Light Probe Group.");
            public static readonly GUIContent selectAll = EditorGUIUtility.TrTextContent("Select All", "Select all Light Probes in the Light Probe Group.");
            public static readonly GUIContent duplicateSelected = EditorGUIUtility.TrTextContent("Duplicate Selected", "Duplicate the selected Light Probes.");
            public static readonly GUIContent performDeringing = EditorGUIUtility.TrTextContent("Remove Ringing", "When enabled, removes visible overshooting often observed as ringing on objects affected by intense lighting at the expense of reduced contrast.");

            // Toolbar
            public static readonly GUIContent editModeButton = EditorGUIUtility.TrIconContent("EditCollider", "Edit Light Probes");
            public static readonly EditMode.SceneViewEditMode[] toolbarModes = {EditMode.SceneViewEditMode.LightProbeGroup};
            public static readonly GUIContent[] toolbarButtons = { editModeButton };
        }
        private LightProbeGroupEditor m_Editor;

        private bool m_EditingProbes;
        private bool m_ShouldFocus;

        public void OnEnable()
        {
            m_Editor = new LightProbeGroupEditor(target as LightProbeGroup);
            m_Editor.PullProbePositions();
            m_Editor.DeselectProbes();
            m_Editor.PushProbePositions();

            m_Editor.drawTetrahedra = new SavedBool($"{target.GetType()}.drawTetrahedra", true);

            Undo.undoRedoPerformed += UndoRedoPerformed;
            EditMode.editModeStarted += OnEditModeStarted;
            EditMode.editModeEnded += OnEditModeEnded;
        }

        private void OnEditModeEnded(IToolModeOwner owner)
        {
            if (owner == (IToolModeOwner)this)
            {
                EndEditProbes();
            }
        }

        private void OnEditModeStarted(IToolModeOwner owner, EditMode.SceneViewEditMode mode)
        {
            if (owner == (IToolModeOwner)this && mode == EditMode.SceneViewEditMode.LightProbeGroup)
            {
                StartEditProbes();
            }
        }

        public void StartEditMode()
        {
            EditMode.ChangeEditMode(EditMode.SceneViewEditMode.LightProbeGroup, m_Editor.bounds, this);
        }

        private void StartEditProbes()
        {
            if (m_EditingProbes)
                return;

            m_EditingProbes = true;
            m_Editor.SetEditing(true);
            Tools.s_Hidden = true;
            SceneView.RepaintAll();
        }

        private void EndEditProbes()
        {
            if (!m_EditingProbes)
                return;

            m_Editor.DeselectProbes();
            m_Editor.SetEditing(false);
            m_EditingProbes = false;
            Tools.s_Hidden = false;
            SceneView.RepaintAll();
        }

        public void OnDisable()
        {
            EndEditProbes();
            Undo.undoRedoPerformed -= UndoRedoPerformed;
            EditMode.editModeStarted -= OnEditModeStarted;
            EditMode.editModeEnded -= OnEditModeEnded;

            if (target != null)
            {
                m_Editor.PushProbePositions();
                m_Editor = null;
            }
        }

        private void UndoRedoPerformed()
        {
            // Update the cached probe positions from the ones just restored in the LightProbeGroup
            m_Editor.PullProbePositions();

            m_Editor.MarkSourcePositionsDirty();
        }

        public override void OnInspectorGUI()
        {
            bool srpHasAlternativeToLegacyProbes = UnityEngine.Rendering.SupportedRenderingFeatures.active.overridesLightProbeSystem;
            if (srpHasAlternativeToLegacyProbes)
            {
                EditorGUILayout.HelpBox(UnityEngine.Rendering.SupportedRenderingFeatures.active.overridesLightProbeSystemWarningMessage, MessageType.Warning, wide: true);
            }

            using (new EditorGUI.DisabledScope(srpHasAlternativeToLegacyProbes))
            {
                EditorGUI.BeginChangeCheck();

                m_Editor.PullProbePositions();

                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    EditMode.DoInspectorToolbar(Styles.toolbarModes, Styles.toolbarButtons, this);
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(3);
                m_Editor.drawTetrahedra.value = EditorGUILayout.Toggle(Styles.showWireframe, m_Editor.drawTetrahedra.value);
                m_Editor.deringProbes = EditorGUILayout.Toggle(Styles.performDeringing, m_Editor.deringProbes);

                EditorGUI.BeginDisabledGroup(EditMode.editMode != EditMode.SceneViewEditMode.LightProbeGroup);
                EditorGUI.BeginDisabledGroup(m_Editor.SelectedCount == 0);

                EditorGUI.BeginChangeCheck();
                Vector3 pos = m_Editor.SelectedCount > 0 ? m_Editor.GetSelectedPositions()[0] : Vector3.zero;
                Vector3 newPosition = EditorGUILayout.Vector3Field(Styles.selectedProbePosition, pos);

                if (EditorGUI.EndChangeCheck())
                {
                    Vector3[] selectedPositions = m_Editor.GetSelectedPositions();
                    Vector3 delta = CalculateDeltaAndClamp(newPosition, pos);
                    for (int i = 0; i < selectedPositions.Length; i++)
                        m_Editor.UpdateSelectedPosition(i, selectedPositions[i] + delta);
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(3);

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();

                if (GUILayout.Button(Styles.addProbe))
                {
                    var position = Vector3.zero;
                    if (SceneView.lastActiveSceneView)
                    {
                        var probeGroup = target as LightProbeGroup;
                        if (probeGroup) position = probeGroup.transform.InverseTransformPoint(position);
                    }
                    StartEditProbes();
                    m_Editor.DeselectProbes();
                    m_Editor.AddProbe(position);
                }

                if (GUILayout.Button(Styles.deleteSelected))
                {
                    StartEditProbes();
                    m_Editor.RemoveSelectedProbes();
                }
                GUILayout.EndVertical();
                GUILayout.BeginVertical();

                if (GUILayout.Button(Styles.selectAll))
                {
                    StartEditProbes();
                    m_Editor.SelectAllProbes();
                }

                if (GUILayout.Button(Styles.duplicateSelected))
                {
                    StartEditProbes();
                    m_Editor.DuplicateSelectedProbes();
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();

                m_Editor.HandleEditMenuHotKeyCommands();
                m_Editor.PushProbePositions();

                if (EditorGUI.EndChangeCheck())
                {
                    m_Editor.MarkSourcePositionsDirty();
                    SceneView.RepaintAll();
                }
            }
        }

        internal override Bounds GetWorldBoundsOfTarget(Object targetObject)
        {
            return m_Editor.bounds;
        }

        private Vector3 CalculateDeltaAndClamp(Vector3 vec1, Vector3 vec2)
        {
            if (float.IsInfinity(vec1.x) || float.IsNaN(vec1.x))
                vec1.x = 0;

            if (float.IsInfinity(vec1.y) || float.IsNaN(vec1.y))
                vec1.y = 0;

            if (float.IsInfinity(vec1.z) || float.IsNaN(vec1.z))
                vec1.z = 0;

            vec1.x = Mathf.Clamp(vec1.x, float.MinValue, float.MaxValue);
            vec1.y = Mathf.Clamp(vec1.y, float.MinValue, float.MaxValue);
            vec1.z = Mathf.Clamp(vec1.z, float.MinValue, float.MaxValue);

            return vec1 - vec2;
        }

        public static bool IsSceneGUIEnabled()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                if (!SceneView.lastActiveSceneView.drawGizmos)
                    return false;
            }

            return true;
        }

        public void OnSceneGUI()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                if (m_ShouldFocus)
                {
                    m_ShouldFocus = false;
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            }

            m_Editor.PullProbePositions();
            var lpg = target as LightProbeGroup;
            if (lpg != null)
            {
                if (m_Editor.OnSceneGUI(lpg.transform))
                    StartEditProbes();
                else
                    EndEditProbes();
            }
            m_Editor.PushProbePositions();
        }

        public bool HasFrameBounds()
        {
            return m_Editor.SelectedCount > 0;
        }

        public Bounds OnGetFrameBounds()
        {
            return m_Editor.selectedProbeBounds;
        }
    }
}
