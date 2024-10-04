using NAudio.Wave;
using System.Diagnostics;

namespace Spectrograph;

public class ConsoleSpectrogram : IDisposable
{
	private const int WindowSize = 1024;
	private static readonly double[] _sampleBuffer = new double[1024];
	private static readonly int[] _lastDbValues = new int[1000];
	private bool _disposedValue;
	private readonly SlidingDFT _dft = new(WindowSize);
	private readonly WaveInEvent _waveIn;
	private int _sampleIndex;
	private int _lastTerminalHeight;
	private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

	public ConsoleSpectrogram(int sampleRateHz)
	{
		//NBGV
		var versionString = typeof(ConsoleSpectrogram).Assembly.GetName().Version!.ToString(3);
		Console.Title = $"Spectrograph v{versionString}";

		_waveIn = new()
		{
			DeviceNumber = 0,
			WaveFormat = new WaveFormat(sampleRateHz, 1),
			BufferMilliseconds = 10
		};

		_waveIn.DataAvailable += OnDataAvailable;
		_waveIn.StartRecording();
	}

	private void OnDataAvailable(object? sender, WaveInEventArgs e)
	{
		var dcOffset = 0.0;

		// First pass to compute the DC offset
		for (var i = 0; i < e.BytesRecorded; i += 2)
		{
			var sample = BitConverter.ToInt16(e.Buffer, i);
			var normalizedSample = sample / 32768.0; // Normalize to [-1, 1]
			dcOffset += normalizedSample;            // Sum the samples to compute the DC offset
		}

		dcOffset /= (e.BytesRecorded / 2);           // Average the sum of the samples

		// Second pass to subtract the DC offset and update the DFT
		for (var i = 0; i < e.BytesRecorded; i += 2)
		{
			var sample = BitConverter.ToInt16(e.Buffer, i);
			var normalizedSample = sample / 32768.0;

			// Subtract the computed DC offset
			var dcCorrectedSample = normalizedSample - dcOffset;

			var oldSample = _sampleBuffer[_sampleIndex];
			_dft.Update(dcCorrectedSample, oldSample);

			_sampleBuffer[_sampleIndex] = dcCorrectedSample;
			_sampleIndex = (_sampleIndex + 1) % WindowSize;
		}

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


	private void RenderSpectrogram()
	{
		var terminalWidth = Console.WindowWidth;
		var terminalHeight = Console.WindowHeight - 1;

		if (_lastTerminalHeight != terminalHeight)
		{
			Console.Clear();
			Array.Clear(_lastDbValues);
			_lastTerminalHeight = terminalHeight;
		}

		var analysis = _dft.Analyse();
		var decibels = analysis.Decibels;
		var decibelRange = decibels.Max() - decibels.Min();
		var stepSize = (int)Math.Max((0.0 + WindowSize) / terminalWidth, 1);
		var minBarLevel = 100;
		var maxBarLevel = 0;
		for (var x = 0; x < terminalWidth - 1; x++)
		{
			var startIndex = x * stepSize;
			var endIndex = Math.Min(startIndex + stepSize, decibels.Length - 1);

			if (startIndex >= decibels.Length) break;

			// Calculate the average decibel value in the range
			double sumDb = 0;
			var count = 0;
			for (var i = startIndex; i < endIndex; i++)
			{
				sumDb += decibels[i];
				count++;
			}

			var avgDb = sumDb / count;

			var barLevel = (int)(terminalHeight * (avgDb + 100) / 256);

			if (minBarLevel > barLevel)
			{
				minBarLevel = barLevel;
			}

			if (maxBarLevel < barLevel)
			{
				maxBarLevel = barLevel;
			}

			UpdateVerticalBar(x, barLevel, _lastDbValues[x], terminalHeight, terminalWidth);
			_lastDbValues[x] = barLevel;
		}

		DrawXAxisLabels(terminalHeight, terminalWidth, analysis.RollingDcOffset);
	}


	private static void UpdateVerticalBar(int x, int newLevel, int oldLevel, int terminalHeight, int terminalWidth)
	{
		// Draw only when there is a change in bar level
		if (oldLevel == newLevel)
		{
			return;
		}

		var xPos = Bound(x, terminalWidth);

		if (oldLevel > newLevel)
		{
			foreach (var y in Enumerable.Range(newLevel, oldLevel + 1))
			{
				Console.SetCursorPosition(xPos, Bound(terminalHeight - y, terminalHeight));
				Console.Write(' ');
			}

			return;
		}

		foreach (var y in Enumerable.Range(oldLevel, newLevel))
		{
			Console.SetCursorPosition(xPos, Bound(terminalHeight - y, terminalHeight));
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
			return maxValue;
		}

		return value;
	}

	private static void DrawXAxisLabels(int terminalHeight, int terminalWidth, double rollingDcOffset)
	{
		Console.ForegroundColor = ConsoleColor.White;
		Console.SetCursorPosition(0, terminalHeight);
		Console.Write("0kHz".PadRight(5)); // First label
		Console.SetCursorPosition(terminalWidth - 6, terminalHeight);
		Console.Write("22kHz"); // Last label

		// Draw the range in the middle at the bottom
		var text = $"DC Offset: {rollingDcOffset:F5}";
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
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}