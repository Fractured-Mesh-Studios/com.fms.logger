using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using LoggerEngine;

namespace LoggerEditor
{
    [CustomEditor(typeof(LogService))]
    public class LogServiceEditor : Editor
    {
        LogService logService;

        private void OnEnable()
        {
            logService = (LogService)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(10);
            if (GUILayout.Button("Apply", GUILayout.MinHeight(20)))
            {
                logService.Apply();
            }
        }
    }
}