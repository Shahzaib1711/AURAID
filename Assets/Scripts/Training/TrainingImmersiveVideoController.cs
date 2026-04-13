using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

namespace AURAID.Training
{
    /// <summary>
    /// Replaces the training info slides with an immersive video.
    /// When the training info root is shown, this plays the video on the assigned Renderer (e.g. 360° sphere or quad).
    /// When the video ends or the user presses Continue, the flow advances to QnA (before practical).
    /// 360° setup: use a Sphere, scale e.g. (50,50,50), invert normals (Scale = -1 on X or use an "Inside" shader) so the camera inside sees the video.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class TrainingImmersiveVideoController : MonoBehaviour
    {
        [Header("Video")]
        [Tooltip("Video to play. If null, Video URL is used.")]
        [SerializeField] VideoClip videoClip;
        [Tooltip("Optional: play from URL instead of clip (e.g. streaming or external file).")]
        [SerializeField] string videoUrl;
        [Tooltip("Renderer to display the video (e.g. 360° sphere or quad). VideoPlayer will render to its material.")]
        [SerializeField] Renderer videoRenderer;
        [Tooltip("Create and assign a RenderTexture to the VideoPlayer and Renderer if not already set.")]
        [SerializeField] bool createRenderTexture = true;
        [SerializeField] int renderTextureWidth = 1920;
        [SerializeField] int renderTextureHeight = 1080;

        [Header("Flow")]
        [Tooltip("When video ends or Continue is pressed, go to QnA (before practical).")]
        [SerializeField] TrainingFlowManager flowManager;
        [Tooltip("Advance to next step when video finishes (no loop).")]
        [SerializeField] bool advanceOnVideoEnd = true;
        [Tooltip("Optional: button to skip / continue before video ends. Assign to same object or child.")]
        [SerializeField] Button continueButton;

        [Header("Optional: hide old slides")]
        [Tooltip("GameObjects to hide when showing video (e.g. the old training slide panels).")]
        [SerializeField] GameObject[] screensToHide;

        VideoPlayer _videoPlayer;
        RenderTexture _renderTexture;

        void Awake()
        {
            _videoPlayer = GetComponent<VideoPlayer>();
            if (_videoPlayer == null) _videoPlayer = gameObject.AddComponent<VideoPlayer>();

            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = !advanceOnVideoEnd;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            if (videoClip != null)
                _videoPlayer.clip = videoClip;
            else if (!string.IsNullOrEmpty(videoUrl))
                _videoPlayer.url = videoUrl;

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
        }

        void OnEnable()
        {
            if (screensToHide != null)
            {
                for (int i = 0; i < screensToHide.Length; i++)
                {
                    if (screensToHide[i] != null)
                        screensToHide[i].SetActive(false);
                }
            }

            if (createRenderTexture && videoRenderer != null && _videoPlayer.targetTexture == null)
                SetupRenderTexture();

            _videoPlayer.loopPointReached -= OnVideoFinished;
            _videoPlayer.loopPointReached += OnVideoFinished;

            _videoPlayer.Play();
        }

        void OnDisable()
        {
            _videoPlayer.loopPointReached -= OnVideoFinished;
            _videoPlayer.Stop();

            if (screensToHide != null)
            {
                for (int i = 0; i < screensToHide.Length; i++)
                {
                    if (screensToHide[i] != null)
                        screensToHide[i].SetActive(true);
                }
            }
        }

        void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                _renderTexture = null;
            }
        }

        void SetupRenderTexture()
        {
            if (_renderTexture != null) return;

            _renderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 0);
            _renderTexture.Create();
            _videoPlayer.targetTexture = _renderTexture;

            if (videoRenderer != null && videoRenderer.material != null)
                videoRenderer.material.mainTexture = _renderTexture;
        }

        void OnVideoFinished(VideoPlayer source)
        {
            if (advanceOnVideoEnd)
                GoToNext();
        }

        /// <summary>Call from Continue / Skip button.</summary>
        public void OnContinueClicked()
        {
            GoToNext();
        }

        void GoToNext()
        {
            if (flowManager == null)
                flowManager = FindObjectOfType<TrainingFlowManager>();
            if (flowManager != null)
                flowManager.ShowQnABefore();
        }
    }
}
