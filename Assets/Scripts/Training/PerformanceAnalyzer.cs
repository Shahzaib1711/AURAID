using System.Collections.Generic;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Tracks CPR performance metrics during the practical session.
    /// Attach to Practical_Panel.
    /// </summary>
    public class PerformanceAnalyzer : MonoBehaviour
    {
        int _totalCompressions;
        int _correctDepthCount;
        int _correctRateCount;
        int _goodRecoilCount;
        float _sumDepthCm;
        float _sumRateBpm;
        int _sampleCount;

        /// <summary>Wall-clock time when the first compression sample was stored; used for normalized chart time.</summary>
        float _compressionSessionStartTime = -1f;

        /// <summary>Seconds from first compression sample — one entry per detected compression (real samples, not averages).</summary>
        public List<float> CompressionTimes = new List<float>();

        /// <summary>Force/depth at each sample (same count as <see cref="CompressionTimes"/>).</summary>
        public List<float> CompressionForces = new List<float>();

        public int TotalCompressions => _totalCompressions;
        public int CorrectDepthCount => _correctDepthCount;
        public int CorrectRateCount => _correctRateCount;
        public int GoodRecoilCount => _goodRecoilCount;

        public float CorrectDepthPercent => _totalCompressions > 0
            ? 100f * _correctDepthCount / _totalCompressions
            : 0f;

        public float CorrectRatePercent => _totalCompressions > 0
            ? 100f * _correctRateCount / _totalCompressions
            : 0f;

        public float GoodRecoilPercent => _totalCompressions > 0
            ? 100f * _goodRecoilCount / _totalCompressions
            : 0f;

        /// <summary>
        /// Hand placement is not tracked separately yet; uses the same metric as depth accuracy for reporting.
        /// </summary>
        public float HandPlacementAccuracyPercent => CorrectDepthPercent;

        /// <summary>Average compression depth in cm (from recorded samples).</summary>
        public float AverageDepthCm => _sampleCount > 0 ? _sumDepthCm / _sampleCount : 0f;

        /// <summary>Average compression rate in BPM (from recorded samples).</summary>
        public float AverageRateBpm => _sampleCount > 0 ? _sumRateBpm / _sampleCount : 0f;

        public void RecordCompression(bool correctDepth, bool correctRate, bool goodRecoil)
        {
            _totalCompressions++;
            if (correctDepth) _correctDepthCount++;
            if (correctRate) _correctRateCount++;
            if (goodRecoil) _goodRecoilCount++;
        }

        /// <summary>
        /// Call once per detected compression (e.g. from <see cref="CPRSessionManager"/>).
        /// Updates running averages and appends one real sample to <see cref="CompressionTimes"/> / <see cref="CompressionForces"/>.
        /// </summary>
        /// <param name="depthCm">Depth used for averages and as force proxy for the chart.</param>
        /// <param name="rateBpm">Rate used for rolling average only.</param>
        public void RecordSample(float depthCm, float rateBpm)
        {
            _sumDepthCm += depthCm;
            _sumRateBpm += rateBpm;
            _sampleCount++;

            AppendCompressionWaveSample(depthCm);
        }

        /// <summary>
        /// Stores normalized time (seconds since first compression in this session) and current force/depth for PDF chart.
        /// </summary>
        void AppendCompressionWaveSample(float currentForce)
        {
            if (CompressionTimes.Count == 0)
                _compressionSessionStartTime = Time.time;

            float normalizedTime = Time.time - _compressionSessionStartTime;
            CompressionTimes.Add(normalizedTime);
            CompressionForces.Add(currentForce);
        }

        /// <summary>Copies time series for Firestore / PDF chart (safe to mutate).</summary>
        public List<float> GetCompressionTimesSnapshot() => new List<float>(CompressionTimes);

        /// <summary>Copies force series for Firestore / PDF chart (safe to mutate).</summary>
        public List<float> GetCompressionForcesSnapshot() => new List<float>(CompressionForces);

        public void Reset()
        {
            _totalCompressions = 0;
            _correctDepthCount = 0;
            _correctRateCount = 0;
            _goodRecoilCount = 0;
            _sumDepthCm = 0f;
            _sumRateBpm = 0f;
            _sampleCount = 0;
            _compressionSessionStartTime = -1f;
            CompressionTimes.Clear();
            CompressionForces.Clear();
        }
    }
}
