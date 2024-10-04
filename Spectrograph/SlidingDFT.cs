namespace Spectrograph;

internal class SlidingDFT
{
	private readonly int _windowSize;
	private readonly double[] _real;
	private readonly double[] _imag;
	private readonly double[] _cosTable;
	private readonly double[] _sinTable;
	private double _newSampleSum;
	private int _newSampleCount;

	public SlidingDFT(int windowSize)
	{
		_windowSize = windowSize;
		_real = new double[_windowSize];
		_imag = new double[_windowSize];
		_cosTable = new double[_windowSize];
		_sinTable = new double[_windowSize];

		for (var k = 0; k < _windowSize; k++)
		{
			_cosTable[k] = Math.Cos(2 * Math.PI * k / _windowSize);
			_sinTable[k] = Math.Sin(2 * Math.PI * k / _windowSize);
		}
	}

	public void Update(double newSample, double oldSample)
	{
		for (var k = 0; k < _windowSize; k++)
		{
			_real[k] += (newSample - oldSample) * _cosTable[k];
			_imag[k] += (newSample - oldSample) * _sinTable[k];
		}

		_newSampleSum += newSample;
		_newSampleCount++;
	}

	internal double DcOffset => _newSampleSum / _newSampleCount;

	public Analysis Analyse()
	{
		var magnitude = new double[_windowSize];
		for (var k = 0; k < _windowSize; k++)
		{
			magnitude[k] = Math.Sqrt(_real[k] * _real[k] + _imag[k] * _imag[k]);
		}

		var db = new double[_windowSize];
		for (var k = 0; k < _windowSize; k++)
		{
			db[k] = 20 * Math.Log10(magnitude[k] + 1e-12); // Avoid log(0)
		}

		return new Analysis
		{
			Decibels = db,
			Min = magnitude.Min(),
			Max = magnitude.Max(),
			RollingDcOffset = DcOffset
		};
	}
}
