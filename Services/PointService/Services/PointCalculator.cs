using PointService.Services.Interfaces;

namespace PointService.Services;

public class PointCalculator : IPointCalculator
{
    public int Calculate(decimal orderAmount, decimal pointRate, decimal campaignMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderAmount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pointRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(campaignMultiplier);

        return (int)Math.Floor(orderAmount * pointRate * campaignMultiplier);
    }
}
