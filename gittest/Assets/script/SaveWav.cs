using System;
using System.IO;
using UnityEngine;

public static class SaveWav {
    const int HEADER_SIZE = 44;

    public static void Save(string filepath, AudioClip clip) {
        Directory.CreateDirectory(Path.GetDirectoryName(filepath));
        using (var fileStream = CreateEmpty(filepath)) {
            ConvertAndWrite(fileStream, clip);
            WriteHeader(fileStream, clip);
        }
    }

    static FileStream CreateEmpty(string filepath) {
        var fs = new FileStream(filepath, FileMode.Create);
        byte emptyByte = new byte();
        for (int i = 0; i < HEADER_SIZE; i++) fs.WriteByte(emptyByte);
        return fs;
    }

    static void ConvertAndWrite(FileStream fs, AudioClip clip) {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];
        const float rescaleFactor = 32767; // to convert float to Int16

        for (int i = 0; i < samples.Length; i++) {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }
        fs.Write(bytesData, 0, bytesData.Length);
    }

    static void WriteHeader(FileStream fs, AudioClip clip) {
        int hz = clip.frequency;
        int channels = clip.channels;
        long samples = clip.samples;

        fs.Seek(0, SeekOrigin.Begin);
        using (var bw = new BinaryWriter(fs)) {
            bw.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            bw.Write((int)(fs.Length - 8));
            bw.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(hz);
            bw.Write(hz * channels * 2);
            bw.Write((short)(channels * 2));
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            bw.Write((int)(samples * channels * 2));
        }
    }
}
