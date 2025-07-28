using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoggerEngine;

namespace LoggerEditor
{
    [CustomEditor(typeof(LogSaverService))]
    public class LogSaverServiceEditor : Editor
    {
        private LogSaverService m_target;

        private void OnEnable()
        {
            m_target = (LogSaverService)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(10);
            if (GUILayout.Button("Save"))
            {
                m_target.Save();
            }
        }
    }
}
