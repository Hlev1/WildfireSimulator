﻿#if UNITY_EDITOR
using Mapbox.Unity.MeshGeneration.Modifiers;
using UnityEditor;
using UnityEngine;

namespace MapboxSDK.Mapbox.Unity.Editor
{
	[CustomEditor(typeof(MaterialModifier))]
	public class MaterialModifierEditor : UnityEditor.Editor
	{

		private MonoScript script;
		private SerializedProperty _materials;

		private void OnEnable()
		{
			script = MonoScript.FromScriptableObject((MaterialModifier)target);
			_materials = serializedObject.FindProperty("_options");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			GUI.enabled = false;
			script = EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false) as MonoScript;
			GUI.enabled = true;

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_materials);
			EditorGUILayout.Space();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
#endif