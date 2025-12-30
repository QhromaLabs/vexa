using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Vexa.Models;

namespace Vexa.Services;

public sealed class WaveformGenerator
{
    private const int PointsPerMinute = 3000;

    public async Task<WaveformData> GenerateAsync(string audioPath)
    {
        if (!File.Exists(audioPath))
        {
            return new WaveformData();
        }

        return await Task.Run(() =>
        {
            using var reader = new AudioFileReader(audioPath);
            var duration = reader.TotalTime;
            
            // Calculate how many samples we want to extract
            int totalPoints = (int)(duration.TotalMinutes * PointsPerMinute);
            if (totalPoints < 100) totalPoints = 100;

            float[] peaks = new float[totalPoints];
            int samplesPerPoint = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8) / totalPoints);
            
            if (samplesPerPoint <= 0) samplesPerPoint = 1;

            float[] buffer = new float[samplesPerPoint];
            for (int i = 0; i < totalPoints; i++)
            {
                int read = reader.Read(buffer, 0, samplesPerPoint);
                if (read == 0) break;

                float max = 0;
                for (int j = 0; j < read; j++)
                {
                    float abs = Math.Abs(buffer[j]);
                    if (abs > max) max = abs;
                }
                peaks[i] = max;
            }

            return new WaveformData
            {
                Peaks = peaks,
                Duration = duration
            };
        });
    }
}
