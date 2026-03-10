using UnityEngine;
using UnityEditor;
using System.Linq;

namespace ScenePoser
{
    [CustomEditor(typeof(Animator))]
    public class MatchAnimatorPoseEditor : Editor
    {
        private int selectedClipIndex = 0;
        private string[] clipNames;
        private AnimationClip[] clips;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Animator animator = (Animator)target;

            if (animator.runtimeAnimatorController == null)
            {
                EditorGUILayout.HelpBox("No Animator Controller assigned.", MessageType.Warning);
                return;
            }

            clips = animator.runtimeAnimatorController.animationClips;
            if (clips.Length == 0)
            {
                EditorGUILayout.HelpBox("No Animation Clips found.", MessageType.Warning);
                return;
            }

            clipNames = clips.Select(clip => clip.name).ToArray();
            selectedClipIndex = EditorGUILayout.Popup("Select Animation", selectedClipIndex, clipNames);

            if (GUILayout.Button("Match Selected Animation Pose"))
            {
                MatchPose(animator, clips[selectedClipIndex]);
            }
        }

        private void MatchPose(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null)
            {
                Debug.LogError("Animator or Animation Clip is missing.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(animator.gameObject, "Match Animator Pose");
            clip.SampleAnimation(animator.gameObject, 0f);
            EditorUtility.SetDirty(animator.gameObject);
        }
    }
}