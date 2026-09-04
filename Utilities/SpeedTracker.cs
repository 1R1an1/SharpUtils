using System.Diagnostics;

namespace SharpUtils.Utilities;

public class SpeedTracker
{
    private long _bytesThisSecond;
    private double _currentSpeed;
    private readonly Stopwatch _timer = Stopwatch.StartNew();

    public double Add(long bytes)
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
