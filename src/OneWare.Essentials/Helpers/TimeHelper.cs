namespace OneWare.Essentials.Helpers;

public static class TimeHelper
{
    
    public static string ConvertNumber(long offset, long timeScale)
    {
        var unitStr = " ps";

        bool invert = offset < 0;
        if (invert) offset *= -1;
        
        var ps = offset / 1000 * timeScale;
        decimal drawNumber = ps;

        switch (ps)
        {
            //s
            case >= 1000000000000:
                drawNumber /= 1000000000000;
                unitStr = " s";
                break;
            //ms
            case >= 1000000000:
                drawNumber /= 1000000000;
                unitStr = " ms";
                break;
            //us
            case >= 1000000:
                drawNumber /= 1000000;
                unitStr = " us";
                break;
            //ns
            case >= 1000:
                drawNumber /= 1000;
                unitStr = " ns";
                break;
        }

        if (invert) drawNumber *= -1;
        return Math.Round(drawNumber, 1) + " " + unitStr;
    }
}