using System;
using System.Text;
using UnityEngine;

namespace AURAID.Voice
{
    public static class WavUtility
    {
        public static AudioClip FromWavBytes(byte[] wavBytes, string clipName = "TTS_WAV")
        {
            if (wavBytes == null || wavBytes.Length < 44)
                throw new Exception("WAV too small.");

            // RIFF
            if (ReadStr(wavBytes, 0, 4) != "RIFF")
                throw new Exception("Not a RIFF file.");

            if (ReadStr(wavBytes, 8, 4) != "WAVE")
                throw new Exception("Not a WAVE file.");

            int fmt = FindChunk(wavBytes, "fmt ");
            int data = FindChunk(wavBytes, "data");

            int fmtSize = BitConverter.ToInt32(wavBytes, fmt + 4);
            if (fmtSize < 16) throw new Exception("Invalid fmt chunk size.");

            short audioFormat = BitConverter.ToInt16(wavBytes, fmt + 8);
            short channels = BitConverter.ToInt16(wavBytes, fmt + 10);
            int sampleRate = BitConverter.ToInt32(wavBytes, fmt + 12);
            short bitsPerSample = BitConverter.ToInt16(wavBytes, fmt + 22);

            if (audioFormat != 1)
                throw new Exception($"Unsupported WAV format {audioFormat}. Expected PCM (1).");

            if (bitsPerSample != 16)
                throw new Exception($"Unsupported bits {bitsPerSample}. Expected 16.");

            int dataStart = data + 8;
            int dataSize = BitConverter.ToInt32(wavBytes, data + 4);
            // 0xFFFFFFFF = "data to end of file" in some WAV writers
            if (dataSize < 0 || dataStart + dataSize > wavBytes.Length)
                dataSize = wavBytes.Length - dataStart;
            if (dataSize <= 0)
                throw new Exception("Data chunk empty or exceeds buffer length.");

            int sampleCount = dataSize / 2; // 16-bit
            float[] samples = new float[sampleCount];

            int offset = dataStart;
            for (int i = 0; i < sampleCount; i++)
            {
                short s = BitConverter.ToInt16(wavBytes, offset);
                samples[i] = s / 32768f;
                offset += 2;
            }

            int frames = sampleCount / channels;
            var clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static int FindChunk(byte[] bytes, string id)
        {
            int i = 12; // after RIFF header
            while (i + 8 <= bytes.Length)
            {
                string chunkId = ReadStr(bytes, i, 4);
                int maxSize = bytes.Length - i - 8;
                int chunkSize = ReadInt32LE(bytes, i + 4);
                // Some WAVs use big-endian chunk size; try that if LE gives negative or unreasonably large
                if (chunkSize < 0 || chunkSize > maxSize)
                    chunkSize = (int)ReadUInt32BE(bytes, i + 4);
                // 0xFFFFFFFF means "data to end of file" in some WAV writers (e.g. OpenAI)
                if (chunkSize == -1 || chunkSize == unchecked((int)0xFFFFFFFF))
                    chunkSize = maxSize;
                if (chunkSize < 0 || chunkSize > maxSize)
                    throw new Exception($"Invalid chunk size (id={chunkId}, size={chunkSize}, max={maxSize}).");
                if (chunkId == id) return i;

                i += 8 + chunkSize;
                if ((chunkSize & 1) == 1) i++; // word align
            }
            throw new Exception($"Chunk {id} not found.");
        }

        private static int ReadInt32LE(byte[] bytes, int offset)
        {
            if (offset + 4 > bytes.Length) return -1;
            return BitConverter.ToInt32(bytes, offset);
        }

        private static uint ReadUInt32BE(byte[] bytes, int offset)
        {
            if (offset + 4 > bytes.Length) return 0;
            return (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
        }

        private static string ReadStr(byte[] bytes, int offset, int len)
        {
            if (offset + len > bytes.Length) return "";
            return Encoding.ASCII.GetString(bytes, offset, len);
        }

        /// <summary>Log WAV fmt chunk (format + bits) for debugging. No throw.</summary>
        public static void LogFmtInfo(byte[] wavBytes)
        {
            try
            {
                if (wavBytes == null || wavBytes.Length < 44) return;
                int fmt = FindChunk(wavBytes, "fmt ");
                short format = BitConverter.ToInt16(wavBytes, fmt + 8);
                short bits = BitConverter.ToInt16(wavBytes, fmt + 22);
                Debug.Log($"[TTS] WAV fmt: format={format} bits={bits}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TTS] Could not read WAV fmt: " + e.Message);
            }
        }

        /// <summary>Encode float samples (0..1) to PCM 16-bit WAV bytes for STT (e.g. Deepgram).</summary>
        public static byte[] ToWavBytes(float[] samples, int sampleRate, int channels = 1)
        {
            if (samples == null || samples.Length == 0) return null;
            int numSamples = samples.Length;
            int dataSize = numSamples * 2; // 16-bit
            int fileSize = 36 + dataSize;
            var bytes = new byte[44 + dataSize];
            int pos = 0;
            // RIFF header
            WriteStr(bytes, ref pos, "RIFF");
            WriteInt32LE(bytes, ref pos, fileSize);
            WriteStr(bytes, ref pos, "WAVE");
            // fmt chunk
            WriteStr(bytes, ref pos, "fmt ");
            WriteInt32LE(bytes, ref pos, 16);
            WriteInt16LE(bytes, ref pos, 1);   // PCM
            WriteInt16LE(bytes, ref pos, (short)channels);
            WriteInt32LE(bytes, ref pos, sampleRate);
            WriteInt32LE(bytes, ref pos, sampleRate * channels * 2);
            WriteInt16LE(bytes, ref pos, (short)(channels * 2));
            WriteInt16LE(bytes, ref pos, 16);
            // data chunk
            WriteStr(bytes, ref pos, "data");
            WriteInt32LE(bytes, ref pos, dataSize);
            for (int i = 0; i < numSamples; i++)
            {
                short s = (short)Mathf.Clamp((int)(samples[i] * 32767f), -32768, 32767);
                bytes[pos++] = (byte)(s & 0xFF);
                bytes[pos++] = (byte)((s >> 8) & 0xFF);
            }
            return bytes;
        }

        private static void WriteStr(byte[] bytes, ref int pos, string s)
        {
            for (int i = 0; i < s.Length && pos < bytes.Length; i++)
                bytes[pos++] = (byte)s[i];
        }
        private static void WriteInt16LE(byte[] bytes, ref int pos, short v)
        {
            if (pos + 2 > bytes.Length) return;
            bytes[pos++] = (byte)(v & 0xFF);
            bytes[pos++] = (byte)((v >> 8) & 0xFF);
        }
        private static void WriteInt32LE(byte[] bytes, ref int pos, int v)
        {
            if (pos + 4 > bytes.Length) return;
            bytes[pos++] = (byte)(v & 0xFF);
            bytes[pos++] = (byte)((v >> 8) & 0xFF);
            bytes[pos++] = (byte)((v >> 16) & 0xFF);
            bytes[pos++] = (byte)((v >> 24) & 0xFF);
        }
    }
}
