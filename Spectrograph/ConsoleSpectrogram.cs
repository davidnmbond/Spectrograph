using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;

namespace Spectrograph;

public class ConsoleSpectrogram : IDisposable
{
	private readonly double[] _sampleBuffer;
	private readonly int[] _lastDbValues = new int[1000];
	private bool _disposedValue;
	private readonly SlidingDFT _dft;
	private readonly double _highEndKHz;
	private readonly WaveInEvent _waveIn;
	private int _lastTerminalHeight;
	private bool _isDrawing;
	private int _lastTerminalWidth;
	private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
	private readonly Random _random = new();
	private readonly int _sampleRateHz;
	private readonly int _windowSize;
	private readonly bool _useTestSignal;

	public ConsoleSpectrogram(int deviceId, int sampleRateHz, int windowSize, bool useTestSignal)
	{
		//NBGV
		var versionString = typeof(ConsoleSpectrogram).Assembly.GetName().Version!.ToString(3);
		Console.Title = $"Spectrograph v{versionString}";

		// Enumerate available devices
		// Get DirectSound devices:
		var deviceEnumerator = new MMDeviceEnumerator();
		var devices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
		for (var i = 0; i < devices.Count; i++)
		{
			var device = devices[i];
			Console.WriteLine($"WaveIn[{i}]: {device.DeviceFriendlyName}");
		}

		var selectedDevice = 0;

		_waveIn = new()
		{
			DeviceNumber = deviceId,
			WaveFormat = new WaveFormat(sampleRateHz, 16, 1),
			BufferMilliseconds = 20,
		};

		_waveIn.DataAvailable += OnDataAvailable;
		_waveIn.StartRecording();
		_sampleRateHz = sampleRateHz;
		_windowSize = windowSize;
		_useTestSignal = useTestSignal;

		_sampleBuffer = new double[windowSize];

		_dft = new SlidingDFT(windowSize);

		_highEndKHz = sampleRateHz / 4000.0;
	}

	private void OnDataAvailable(object? sender, WaveInEventArgs e)
	{
		if (_isDrawing)
		{
			return;
		}

		_isDrawing = true;

		try
		{
			// Fill _sampleBuffer with the new samples
			if (_useTestSignal)
			{
				// Sine waves at 1000, 2000 and 4000Hz plus noise at 2% amplitude
				for (var i = 0; i < _sampleBuffer.Length; i++)
				{
					_sampleBuffer[i] =
						0.25 * Math.Sin(2 * Math.PI * 1000 * i / _sampleRateHz)
						+ 0.125 * Math.Sin(2 * Math.PI / _sampleRateHz * (2000 * i))
						+ 0.0675 * Math.Sin(2 * Math.PI / _sampleRateHz * (10000 * i))
						+ 0.02 * (2 * _random.NextDouble() - 1)
						;
				}
			}
			else
			{
				for (var i = 0; i < e.BytesRecorded; i += 2)
				{
					var sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
					_sampleBuffer[i / 2] = sample / 32768.0;
				}
			}

			_dft.Update(_sampleBuffer);

			if (_stopwatch.ElapsedMilliseconds < 100)
			{
				return;
			}

			_stopwatch.Restart();

			try
			{
				RenderSpectrogram();
			}
			catch (ArgumentOutOfRangeException)
			{
				// Handle resize exception
			}
		}
		catch (Exception exe)
		{
			Console.WriteLine(exe.ToString());
		}
		finally
		{
			_isDrawing = false;
		}
	}


	private void RenderSpectrogram()
	{
		var terminalWidth = Console.WindowWidth;
		var terminalHeight = Console.WindowHeight - 1;

		if (_lastTerminalHeight != terminalHeight || _lastTerminalWidth != terminalWidth)
		{
			Console.Clear();
			Array.Clear(_lastDbValues);
			_lastTerminalHeight = terminalHeight;
			_lastTerminalWidth = terminalWidth;
			Console.BufferHeight = terminalHeight + 1;
			Console.CursorVisible = false;
		}

		var analysis = _dft.Analyse();
		var decibels = analysis.Decibels;
		var stepSize = (0.0 + _windowSize) / terminalWidth / 4;
		for (var x = 0; x < terminalWidth - 1; x++)
		{
			var startIndex = (int)(x * stepSize) + 1;
			var endIndex = (int)(startIndex + stepSize);

			// Calculate the average decibel value in the range
			double sumDb = 0;
			var count = 0;
			for (var i = startIndex; i < endIndex; i++)
			{
				sumDb += decibels[i];
				count++;
			}

			var avgDb = sumDb / count;

			var barLevel = (int)(avgDb * terminalHeight / 20 * (1.0 + 4 * x / terminalWidth));
			// Ensure that the bar level is within the bounds of the terminal
			barLevel = Bound(barLevel, terminalHeight);

			UpdateVerticalBar(x, barLevel, _lastDbValues[x], terminalHeight, terminalWidth);
			_lastDbValues[x] = barLevel;
		}

		DrawXAxisLabels(terminalHeight, terminalWidth, decibels.Min(), decibels.Max());
	}


	private static void UpdateVerticalBar(int x, int newLevel, int oldLevel, int terminalHeight, int terminalWidth)
	{
		// Draw only when there is a change in bar level
		if (oldLevel == newLevel || terminalHeight == 0)
		{
			return;
		}

		var xPos = Bound(x, terminalWidth);

		if (oldLevel > newLevel)
		{
			foreach (var y in Enumerable.Range(newLevel + 1, oldLevel - newLevel))
			{
				Console.SetCursorPosition(xPos, terminalHeight - y);
				Console.Write(' ');
			}

			return;
		}

		foreach (var y in Enumerable.Range(oldLevel + 1, newLevel - oldLevel))
		{
			Console.SetCursorPosition(xPos, terminalHeight - y);
			Console.ForegroundColor = GetColorForPercent(y * 100 / terminalHeight);
			Console.Write('█');
		}
	}

	/// <summary>
	/// Make sure the y value is within the bounds of the console window
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	private static int Bound(int value, int maxValue)
	{
		if (value < 0)
		{
			return 0;
		}

		if (value > maxValue)
		{
			return maxValue - 1;
		}

		return value;
	}

	private void DrawXAxisLabels(
		int terminalHeight,
		int terminalWidth,
		double decibelMin,
		double decibelMax)
	{
		Console.ForegroundColor = ConsoleColor.White;
		Console.SetCursorPosition(0, terminalHeight);
		Console.Write("0kHz".PadRight(5)); // First label
		Console.SetCursorPosition(terminalWidth - 6, terminalHeight);
		Console.Write($"{_highEndKHz:N0}kHz"); // Last label

		// Draw the range in the middle at the bottom
		var text = $" min:{decibelMin:F2}dB - max:{decibelMax:F2} ";
		Console.SetCursorPosition((terminalWidth - text.Length) / 2, terminalHeight);
		Console.Write(text);
	}

	private static ConsoleColor GetColorForPercent(int percent)
	{
		if (percent > 85) return ConsoleColor.Red;     // Louder signals
		if (percent > 30) return ConsoleColor.Yellow;  // Medium
		return ConsoleColor.Green;                 // Quieter signals
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_waveIn.Dispose();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}