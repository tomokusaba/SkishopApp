namespace PointService.Services.Interfaces;

public interface IPointCalculator
{
    int Calculate(decimal orderAmount, decimal pointRate, decimal campaignMultiplier);
}
