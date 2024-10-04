namespace Spectrograph;

internal record Analysis
{
	internal required double RollingDcOffset { get; init; }

	internal required double[] Decibels { get; init; }

	internal required double Min { get; init; }

	internal required double Max { get; init; }
}