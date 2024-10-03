namespace Spectrograph;

class Program
{
	private const int SampleRateHz = 44100;

	static void Main()
	{
		using var spectrogram = new ConsoleSpectrogram(SampleRateHz);

		while (!Console.KeyAvailable)
		{
			Thread.Sleep(100);
		}
	}
}
