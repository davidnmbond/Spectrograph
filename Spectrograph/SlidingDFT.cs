using System.Numerics;

namespace Spectrograph;

internal class SlidingDFT(int windowSize)
{
	private Complex[] x = new Complex[windowSize];

	public void Update(double[] sampleBuffer)
	{
		if (sampleBuffer.Length != windowSize)
		{
			throw new ArgumentException("Sample buffer length must match window size", nameof(sampleBuffer));
		}

		x = Fft(sampleBuffer);
	}

	private static Complex[] Fft(double[] sampleBuffer)
	{
		var n = sampleBuffer.Length;
		var X = new Complex[n];

		if (n == 2)
		{
			X[0] = sampleBuffer[0] + sampleBuffer[1];
			X[1] = sampleBuffer[0] - sampleBuffer[1];
			return X;
		}

		var even = new double[n / 2];
		var odd = new double[n / 2];

		for (var i = 0; i < n; i += 2)
		{
			even[i / 2] = sampleBuffer[i];
			odd[i / 2] = sampleBuffer[i + 1];
		}

		var q = Fft(even);
		var r = Fft(odd);

		for (var k = 0; k < n / 2; k++)
		{
			var angle = -2 * k * Math.PI / n;
			var wk = Complex.FromPolarCoordinates(1, angle);
			X[k] = q[k] + wk * r[k];
			X[k + n / 2] = q[k] - wk * r[k];
		}

		return X;
	}

	public Analysis Analyse()
	{
		var magnitude = new double[windowSize];
		for (var k = 0; k < windowSize; k++)
		{
			magnitude[k] = x[k].Magnitude;
		}

		var db = new double[windowSize];
		for (var k = 0; k < windowSize; k++)
		{
			db[k] = 20 * Math.Log10(magnitude[k] + 1e-12); // Avoid infinity
		}

		return new Analysis
		{
			Decibels = db,
			Min = db.Min(),
			Max = db.Max(),
		};
	}
}
