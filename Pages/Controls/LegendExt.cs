using Syncfusion.Maui.Toolkit.Charts;

namespace APP_GoiXetheoGPS.Pages.Controls
{
    public class LegendExt : ChartLegend
    {
        protected override double GetMaximumSizeCoefficient()
        {
            return 0.5;
        }
    }
}
