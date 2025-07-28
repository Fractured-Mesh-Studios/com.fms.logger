using UnityEngine;

namespace LoggerEngine
{
    [ExecuteInEditMode]
    public sealed class LogService : MonoBehaviour
    {
        [Header("General")]
        [SerializeField] private bool isLogEnabled = true;
        [SerializeField] private bool showCallerInfo = true;

        [Header("Color")]
        [SerializeField] private Color classNameColor = Color.cyan;
        [SerializeField] private Color contextNameColor = Color.white;

        #region Unity
        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            DebugClient.OnLogContext += OnLogContext;
        }

        private void OnDisable()
        {
            DebugClient.OnLogContext -= OnLogContext;
        }
        #endregion

        private object OnLogContext(object msg, Object? context)
        {
            if (context)
            {
                string hex = ColorUtility.ToHtmlStringRGBA(contextNameColor);
                string name = $"<color=#{hex}>[" + context?.name + "]</color>";
                return name + msg;
            }
            else
            {
                return msg;
            }
        }

        public void Apply()
        {
            DebugClient.classNameColor = classNameColor;
            DebugClient.showCallerInfo = showCallerInfo;
            DebugClient.isLogEnabled = isLogEnabled;

            DebugClient.Log("Applied Settings", gameObject);
        }
    }
}