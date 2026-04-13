using UnityEngine;
using UnityEngine.Rendering;

namespace Landscape.Atmosphere
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AtmosphereController : MonoBehaviour
    {
        private const string DefaultProfilePath = "Atmosphere/AtmosphereProfile_Earth";
        private const float OverlayPaddingLeft = 8.0f;
        private const float OverlayPaddingRight = 8.0f;
        private const float OverlayPaddingTop = 6.0f;
        private static AtmosphereController instance;

        [SerializeField] private AtmosphereProfile profile;
        [SerializeField] private Light mainLight;
        [SerializeField] private bool showDebugOverlay = true;
        [SerializeField] private Vector2 debugOverlaySize = new Vector2(384.0f, 96.0f);

        private AtmosphereLutManager lutManager;
        private bool forceRebuild = true;
        private GUIStyle overlayLabelStyle;
        private GUIStyle overlayTitleStyle;

        public static AtmosphereController Instance => instance;
        public RTHandle TransmittanceHandle => lutManager != null ? lutManager.TransmittanceHandle : null;
        public RenderTexture TransmittanceTexture => lutManager != null ? lutManager.TransmittanceTexture : null;
        public ComputeShader TransmittanceComputeShader => lutManager != null ? lutManager.ComputeShader : null;
        public int TransmittanceKernelIndex => lutManager != null ? lutManager.KernelIndex : -1;

        private void Reset()
        {
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
        }

        private void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Atmosphere: multiple AtmosphereController instances found. Disabling duplicate.", this);
                enabled = false;
                return;
            }

            instance = this;
            lutManager ??= new AtmosphereLutManager();
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
            forceRebuild = true;
        }

        private void OnDisable()
        {
            if (instance == this)
                instance = null;

            if (lutManager != null)
                lutManager.Release();
        }

        private void OnValidate()
        {
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
            forceRebuild = true;
        }

        public bool TryPrepareForRender(out AtmosphereParameters parameters)
        {
            parameters = default;

            if (!enabled)
                return false;

            LoadDefaultProfileIfNeeded();
            if (profile == null)
                return false;

            lutManager ??= new AtmosphereLutManager();
            parameters = AtmosphereParameters.FromProfile(profile);
            return lutManager.EnsureResources(parameters);
        }

        public bool NeedsTransmittanceRebuild(in AtmosphereParameters parameters)
        {
            return forceRebuild || lutManager == null || lutManager.NeedsRebuild(parameters);
        }

        public void RenderTransmittance(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            lutManager ??= new AtmosphereLutManager();
            lutManager.RenderTransmittance(cmd, parameters);
            lutManager.BindGlobals(cmd, parameters);
            forceRebuild = false;
        }

        public void BindGlobals(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (lutManager == null)
                return;

            lutManager.BindGlobals(cmd, parameters);
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
                return;

            RenderTexture texture = TransmittanceTexture;
            if (texture == null)
                return;

            EnsureOverlayStyles();

            float width = Mathf.Max(128.0f, debugOverlaySize.x);
            float height = Mathf.Max(32.0f, debugOverlaySize.y);
            Rect panelRect = new Rect(16.0f, 16.0f, width + OverlayPaddingLeft + OverlayPaddingRight, height + 44.0f);
            Rect textureRect = new Rect(
                panelRect.x + OverlayPaddingLeft,
                panelRect.y + 28.0f,
                width,
                height);

            GUI.Box(panelRect, GUIContent.none);
            GUI.Label(new Rect(panelRect.x + 8.0f, panelRect.y + 6.0f, panelRect.width - 16.0f, 20.0f), "Atmosphere Transmittance LUT", overlayTitleStyle);
            GUI.DrawTexture(textureRect, texture, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(textureRect.x, textureRect.yMax + 4.0f, textureRect.width, 18.0f), $"{texture.width}x{texture.height} ARGBHalf", overlayLabelStyle);
        }

        private void EnsureOverlayStyles()
        {
            if (overlayTitleStyle != null && overlayLabelStyle != null)
                return;

            overlayTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            overlayLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f, 1.0f) }
            };
        }

        private void LoadDefaultProfileIfNeeded()
        {
            if (profile != null)
                return;

            profile = Resources.Load<AtmosphereProfile>(DefaultProfilePath);
        }

        private void TryAssignMainLight()
        {
            if (mainLight != null && mainLight.type == LightType.Directional)
                return;

            if (RenderSettings.sun != null)
            {
                mainLight = RenderSettings.sun;
                return;
            }

            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    mainLight = lights[i];
                    return;
                }
            }
        }
    }
}
