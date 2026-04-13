using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AURAID.Emergency;
using AURAID.Emergency.CPR;
using UnityEngine;

namespace AURAID.Integration
{
    public enum Esp32CsvLayout
    {
        /// <summary>fsr1, fsr2, fsr3, fMax, motion, tilt, count</summary>
        FsrMotionTiltCount,
        /// <summary>ax, ay, az, gx, gy, forceOrDepth [, flags]</summary>
        AccelGyroForceLegacy
    }

    /// <summary>
    /// TCP server (default port 5000) for CSV lines from an ESP32. Buffers by newline (TCP is stream-based).
    /// When <see cref="cprAgent"/> is assigned, pushes <see cref="SensorsSample"/> on the Unity main thread.
    /// Align CSV field order with your firmware.
    /// </summary>
    public class ESP32TestReceiver : MonoBehaviour
    {
        const int DefaultPort = 5000;

        [Header("TCP")]
        [SerializeField] int listenPort = DefaultPort;

        [Header("CPR integration (optional)")]
        [Tooltip("Emergency CPR agent; receives fsr/motion for RuleBasedCprAgent.")]
        [SerializeField] RuleBasedCprAgent cprAgent;
        [Tooltip("If set, NotifySensorSampleReceived runs so heartbeat / fault logic works.")]
        [SerializeField] EmergencyController emergencyController;

        [Header("CSV layout (must match ESP32 print order)")]
        [SerializeField] Esp32CsvLayout csvLayout = Esp32CsvLayout.FsrMotionTiltCount;

        [Header("FSR scale (training + emergency)")]
        [Tooltip("Default 6 matches the original formula (safe for demos). Increase only after the presentation if hard presses peg at 1.0 and coaching feels flat — set near your firmware’s typical peak raw fMax.")]
        [SerializeField, Min(0.01f)] float maxRawForceForNormalization = 6f;

        [Header("Logging")]
        [SerializeField] bool verboseTcpLogging;
        [SerializeField, Min(0.25f)] float logSummaryIntervalSec = 2f;

        TcpListener _server;
        Thread _thread;
        volatile bool _quit;

        readonly object _lineLock = new object();
        readonly Queue<string> _lineQueue = new Queue<string>();

        readonly object _logLock = new object();
        readonly Queue<string> _logQueue = new Queue<string>();

        readonly StringBuilder _rxBuffer = new StringBuilder(2048);

        float _lastSummaryLogTime;
        int _linesProcessed;
        SensorsSample _lastSample;
        /// <summary>Strictly increasing sample clock so <see cref="RuleBasedCprAgent"/> rate/pause logic works when many CSV lines arrive in one frame.</summary>
        float _streamSampleTimeSec = -1f;

        /// <summary>Fired on the Unity main thread for each successfully parsed sample (training practical, HUD, etc.).</summary>
        public event Action<SensorsSample> OnSensorSample;

        void Awake()
        {
            if (cprAgent == null)
                cprAgent = FindObjectOfType<RuleBasedCprAgent>(true);
            if (emergencyController == null && cprAgent != null)
                emergencyController = cprAgent.GetComponentInParent<EmergencyController>(true);
        }

        void Start()
        {
            _thread = new Thread(ServerLoop) { IsBackground = true };
            _thread.Start();
            EnqueueLog($"[ESP32TestReceiver] Server thread starting on port {listenPort}…");
        }

        void Update()
        {
            lock (_logLock)
            {
                while (_logQueue.Count > 0)
                    Debug.Log(_logQueue.Dequeue());
            }

            List<string> batch = null;
            lock (_lineLock)
            {
                if (_lineQueue.Count == 0)
                    return;
                batch = new List<string>(_lineQueue.Count);
                while (_lineQueue.Count > 0)
                    batch.Add(_lineQueue.Dequeue());
            }

            float wall = Time.realtimeSinceStartup;
            for (int i = 0; i < batch.Count; i++)
            {
                string line = batch[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                _streamSampleTimeSec = Mathf.Max(wall, _streamSampleTimeSec + 0.00005f);
                try
                {
                    if (TryParseSample(line, _streamSampleTimeSec, maxRawForceForNormalization, out SensorsSample sample))
                    {
                        SanitizeSample(ref sample);
                        _lastSample = sample;
                        _linesProcessed++;
                        try
                        {
                            OnSensorSample?.Invoke(sample);
                            if (cprAgent != null)
                                cprAgent.PushSample(sample);
                            EmergencySessionManager.Instance?.RecordSensorSample(sample);
                            emergencyController?.NotifySensorSampleReceived();
                        }
                        catch (Exception ex)
                        {
                            EnqueueLog("[ESP32TestReceiver] Sample consumer error (ignored): " + ex.Message);
                        }
                    }
                    else if (verboseTcpLogging)
                        EnqueueLog("[ESP32TestReceiver] Unparsed line: " + line);
                }
                catch (Exception ex)
                {
                    EnqueueLog("[ESP32TestReceiver] Line handling error (ignored): " + ex.Message);
                }
            }

            if (verboseTcpLogging && Time.time - _lastSummaryLogTime >= logSummaryIntervalSec && _linesProcessed > 0)
            {
                _lastSummaryLogTime = Time.time;
                var s = _lastSample;
                EnqueueLog(
                    $"[ESP32TestReceiver] {_linesProcessed} lines — last fsr={s.fsrForce:F3} motion={s.bnoMotion:F3} tilt={s.armTiltFromVerticalDeg:F1}");
                _linesProcessed = 0;
            }
        }

        void ServerLoop()
        {
            try
            {
                _server = new TcpListener(IPAddress.Any, listenPort);
                _server.Start();
                EnqueueLog($"[ESP32TestReceiver] Listening on 0.0.0.0:{listenPort}");

                while (!_quit)
                {
                    TcpClient client;
                    try
                    {
                        client = _server.AcceptTcpClient();
                    }
                    catch (SocketException)
                    {
                        if (_quit) break;
                        throw;
                    }

                    EnqueueLog("[ESP32TestReceiver] Client connected: " + client.Client.RemoteEndPoint);
                    try
                    {
                        using (client)
                        using (NetworkStream stream = client.GetStream())
                        {
                            var buffer = new byte[1024];
                            while (!_quit && client.Connected)
                            {
                                int n = stream.Read(buffer, 0, buffer.Length);
                                if (n <= 0)
                                    break;
                                AppendAndExtractLines(buffer, n);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        EnqueueLog("[ESP32TestReceiver] Client error: " + ex.Message);
                    }

                    EnqueueLog("[ESP32TestReceiver] Client disconnected.");
                }
            }
            catch (Exception e)
            {
                EnqueueLog("[ESP32TestReceiver] Server error: " + e.Message);
            }
        }

        void AppendAndExtractLines(byte[] buffer, int length)
        {
            string chunk = Encoding.UTF8.GetString(buffer, 0, length);
            lock (_lineLock)
            {
                _rxBuffer.Append(chunk);
                for (;;)
                {
                    string s = _rxBuffer.ToString();
                    int nl = s.IndexOfAny(new[] { '\n', '\r' });
                    if (nl < 0)
                        break;

                    int lineEnd = nl;
                    int skip = 1;
                    if (s[nl] == '\r' && nl + 1 < s.Length && s[nl + 1] == '\n')
                    {
                        lineEnd = nl;
                        skip = 2;
                    }

                    string line = s.Substring(0, lineEnd).Trim();
                    if (line.Length > 0)
                        _lineQueue.Enqueue(line);

                    _rxBuffer.Clear();
                    if (nl + skip < s.Length)
                        _rxBuffer.Append(s.Substring(nl + skip));
                }
            }
        }

        bool TryParseSample(string line, float unityTime, float forceDivisor, out SensorsSample sample)
        {
            sample = default;
            string[] parts = line.Split(',');
            if (csvLayout == Esp32CsvLayout.FsrMotionTiltCount)
                return TryParseFsrMotionTilt(parts, unityTime, forceDivisor, out sample);
            return TryParseAccelGyroForce(parts, unityTime, forceDivisor, out sample);
        }

        /// <summary>
        /// Final wire format: [0] fsr1, [1] fsr2, [2] fsr3, [3] fMax (sole CPR force → fsrForce), [4] motion, [5] tilt, [6] count.
        /// fsr1–3 are validated (must be numeric) but not used for CPR force — firmware owns fMax.
        /// </summary>
        static bool TryParseFsrMotionTilt(string[] parts, float unityTime, float forceDivisor, out SensorsSample sample)
        {
            sample = default;
            if (parts.Length < 7)
                return false;

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
            if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
            if (!float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fMax)) return false;
            if (!float.TryParse(parts[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float motionIn)) return false;
            if (!float.TryParse(parts[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float tiltDeg)) return false;
            int.TryParse(parts[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

            float fsr = NormalizeForce(fMax, forceDivisor);
            float motion = Clamp01Finite(motionIn);

            sample = new SensorsSample
            {
                time = unityTime,
                fsrForce = fsr,
                bnoMotion = motion,
                armTiltFromVerticalDeg = tiltDeg,
                heartRateBpm = -1f,
                spo2 = -1f,
                motion = motion,
                responsive = true,
                respirationValue = -1f,
                breathingDetected = false,
                gaspingDetected = false,
                cyanosisDetected = false
            };
            return true;
        }

        /// <summary>ax, ay, az, gx, gy, forceOrDepth [, flags]</summary>
        static bool TryParseAccelGyroForce(string[] parts, float unityTime, float forceDivisor, out SensorsSample sample)
        {
            sample = default;
            if (parts.Length < 6)
                return false;

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float ax)) return false;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float ay)) return false;
            if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float az)) return false;
            if (!float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float gx)) return false;
            if (!float.TryParse(parts[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float gy)) return false;
            if (!float.TryParse(parts[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float forceOrDepth)) return false;

            float fsr = NormalizeForce(forceOrDepth, forceDivisor);
            float accMag = SafeVector3Magnitude(ax, ay, az);
            float gyroMag = SafeVector2Magnitude(gx, gy);
            float motion = Clamp01Finite((accMag / 25f) * 0.5f + (gyroMag / 5f) * 0.5f);

            sample = new SensorsSample
            {
                time = unityTime,
                fsrForce = fsr,
                bnoMotion = motion,
                armTiltFromVerticalDeg = -1f,
                heartRateBpm = -1f,
                spo2 = -1f,
                motion = motion,
                responsive = true,
                respirationValue = -1f,
                breathingDetected = false,
                gaspingDetected = false,
                cyanosisDetected = false
            };
            return true;
        }

        /// <summary>Maps raw FSR / depth to 0..1 for <see cref="SensorsSample.fsrForce"/>.</summary>
        static float NormalizeForce(float raw, float maxRaw)
        {
            if (raw <= 0f || float.IsNaN(raw) || float.IsInfinity(raw))
                return 0f;
            if (raw > 1.5f)
                return Mathf.Clamp01(raw / Mathf.Max(0.01f, maxRaw));
            return Mathf.Clamp01(raw);
        }

        static void SanitizeSample(ref SensorsSample s)
        {
            s.fsrForce = Clamp01Finite(s.fsrForce);
            s.bnoMotion = Clamp01Finite(s.bnoMotion);
            s.motion = Clamp01Finite(s.motion);
            if (float.IsNaN(s.armTiltFromVerticalDeg) || float.IsInfinity(s.armTiltFromVerticalDeg))
                s.armTiltFromVerticalDeg = -1f;
            else
                s.armTiltFromVerticalDeg = Mathf.Clamp(s.armTiltFromVerticalDeg, -180f, 180f);
            if (float.IsNaN(s.time) || float.IsInfinity(s.time))
                s.time = Time.realtimeSinceStartup;
        }

        static float Clamp01Finite(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
            return Mathf.Clamp01(v);
        }

        static float SafeVector3Magnitude(float x, float y, float z)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
                float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z))
                return 0f;
            return new Vector3(x, y, z).magnitude;
        }

        static float SafeVector2Magnitude(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                return 0f;
            return new Vector2(x, y).magnitude;
        }

        void EnqueueLog(string message)
        {
            lock (_logLock)
                _logQueue.Enqueue(message);
        }

        void OnApplicationQuit()
        {
            _quit = true;
            try
            {
                _server?.Stop();
            }
            catch
            {
                // ignored
            }
        }
    }
}
