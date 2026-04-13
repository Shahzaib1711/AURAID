using System;
using System.Collections;
using UnityEngine;

namespace AURAID.Voice
{
    /// <summary>
    /// Records from the default microphone for a set duration and produces WAV bytes for STT.
    /// </summary>
    public class MicrophoneRecorder : MonoBehaviour
    {
        [Header("Recording")]
        [Tooltip("Max recording length in seconds.")]
        public int recordLengthSec = 10;
        [Tooltip("Sample rate for recording. 16000 is good for speech; Deepgram supports many rates.")]
        public int sampleRate = 16000;

        const int Channels = 1;
        AudioClip _clip;
        string _device;
        bool _recording;

        /// <summary>True while recording is in progress.</summary>
        public bool IsRecording => _recording;

        /// <summary>Start recording. Call StopRecording() or wait for RecordForSeconds() to finish.</summary>
        public void StartRecording()
        {
            if (_recording)
            {
                Debug.LogWarning("[STT] Already recording.");
                return;
            }
            _device = null;
            _clip = Microphone.Start(_device, false, recordLengthSec, sampleRate);
            _recording = _clip != null;
            if (!_recording)
                Debug.LogError("[STT] Microphone.Start failed.");
        }

        /// <summary>Stop and return WAV bytes (or null). Call after StartRecording().</summary>
        public byte[] StopRecording()
        {
            if (!_recording || _clip == null)
                return null;
            int position = Microphone.GetPosition(_device);
            Microphone.End(_device);
            _recording = false;
            if (position <= 0)
            {
                Debug.LogWarning("[STT] No audio captured.");
                return null;
            }

            float[] allSamples = new float[_clip.samples * _clip.channels];
            _clip.GetData(allSamples, 0);
            int validSamples = Mathf.Min(position, allSamples.Length);
            float[] trimmed = new float[validSamples];
            Array.Copy(allSamples, 0, trimmed, 0, validSamples);

            byte[] wav = WavUtility.ToWavBytes(trimmed, sampleRate, Channels);
            return wav;
        }

        /// <summary>Record for the given seconds then return WAV bytes via callback. Runs as coroutine.</summary>
        public IEnumerator RecordForSeconds(float seconds, Action<byte[]> onWavReady, Action<string> onError)
        {
            if (seconds <= 0 || seconds > recordLengthSec)
            {
                onError?.Invoke("Invalid record duration.");
                yield break;
            }
            StartRecording();
            if (!_recording)
            {
                onError?.Invoke("Failed to start microphone.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(seconds);
            byte[] wav = StopRecording();
            if (wav == null || wav.Length < 44)
                onError?.Invoke("No audio captured or capture too short.");
            else
                onWavReady?.Invoke(wav);
        }
    }
}
