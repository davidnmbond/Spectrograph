using NAudio.CoreAudioApi;

namespace Spectrograph;

class Program
{
	private const int SampleRateHz = 44100;
	private const int WindowSize = 1024;
	private const bool UseTestSignal = false;

	static void Main(string[] args)
	{
		var function = args.Length > 0 ? args[0] : "0";

		switch (function)
		{
			case "Devices":
				// Enumerate available devices
				// Get DirectSound devices:
				var deviceEnumerator = new MMDeviceEnumerator();
				var devices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
				for (var i = 0; i < devices.Count; i++)
				{
					var device = devices[i];
					Console.WriteLine($"WaveIn[{i}]: {device.DeviceFriendlyName}");
				}

				break;
			default:
				{
					if (!int.TryParse(function, out var deviceId))
					{
						deviceId = 0;
					}

					using var spectrogram = new ConsoleSpectrogram(deviceId, SampleRateHz, WindowSize, UseTestSignal);

					while (!Console.KeyAvailable)
					{
						Thread.Sleep(100);
					}

					Console.CursorVisible = true;
					Console.ForegroundColor = ConsoleColor.White;
				}
				break;
		}
	}
}
