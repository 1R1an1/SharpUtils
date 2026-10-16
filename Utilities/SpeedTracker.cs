/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System.Diagnostics;

namespace SharpUtils.Utilities;

/// <summary>
/// Tracker de velocidad de transferencia de bytes por segundo.
/// </summary>
public class SpeedTracker
{
    private long _bytesThisSecond;
    private long _currentSpeed;
    private readonly Stopwatch _timer = Stopwatch.StartNew();

    /// <summary>
    /// Acumula los bytes transferidos y devuelve la velocidad del último segundo en bytes/segundo.
    /// </summary>
    public long Add(long bytes)
    {
        _bytesThisSecond += bytes;

        if (_timer.ElapsedMilliseconds >= 1000)
        {
            _currentSpeed = _bytesThisSecond;
            _bytesThisSecond = 0;
            _timer.Restart();
        }

        return _currentSpeed;
    }
}
