/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using Concentus;
using Concentus.Enums;
using System;

namespace SharpUtils.Audio;

/// <summary>
/// Codificador y Decodificador Opus en C# puro.
/// Compresión con pérdida, ideal para streaming de UDP por su tamaño reducido.
/// </summary>
public class OpusCodec : IDisposable
{
    private readonly IOpusEncoder _encoder;
    private readonly IOpusDecoder _decoder;

    private readonly int _frameSize; // Cantidad de muestras por canal por paquete

    /// <summary>
    /// Crea el códec Opus.
    /// </summary>
    /// <param name="sampleRate">48000 para tu app.</param>
    /// <param name="channels">2 para estéreo.</param>
    /// <param name="frameSizeMs">20ms es el estándar de baja latencia (960 muestras a 48kHz).</param>
    public OpusCodec(int sampleRate = 48000, int channels = 2, int frameSizeMs = 20)
    {
        _frameSize = sampleRate * frameSizeMs / 1000; // 20ms a 48kHz = 960 muestras

        // Inicializamos el codificador (Audio genérico)
        _encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_AUDIO);
        _encoder.Bitrate = 128000;
        _encoder.Complexity = 10;
        _encoder.SignalType = OpusSignal.OPUS_SIGNAL_MUSIC;

        // Inicializamos el decodificador
        _decoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);
    }

    /// <summary>
    /// Comprime audio PCM 16-bit a Opus.
    /// </summary>
    /// <param name="pcmInput">Array de shorts (16-bit PCM). Debe tener exactamente frameSize * channels muestras.</param>
    /// <param name="output">Array destino para el Opus comprimido (con 400 bytes alcanza).</param>
    /// <returns>Cantidad de bytes escritos en el output.</returns>
    public int Encode(ReadOnlySpan<short> pcmInput, Span<byte> output)
        => _encoder.Encode(pcmInput, _frameSize, output, output.Length);

    /// <summary>
    /// Descomprime Opus a audio PCM 16-bit.
    /// </summary>
    /// <param name="opusData">Datos comprimidos que llegaron por red.</param>
    /// <param name="dataLength">Tamaño real de los datos comprimidos.</param>
    /// <param name="pcmOutput">Array destino de shorts.</param>
    /// <returns>Cantidad de muestras decodificadas.</returns>
    public int Decode(ReadOnlySpan<byte> opusData, Span<short> pcmOutput)
        => _decoder.Decode(opusData, pcmOutput, _frameSize, false);

    public void Dispose()
    {
        _decoder?.Dispose();
        _encoder?.Dispose();
        // Concentus maneja la memoria internamente, no requiere Dispose estricto,
        // pero lo dejamos por buena práctica.
    }
}