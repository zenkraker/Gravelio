namespace Gravelio.Api.Activities;

/// Pure activity metric maths, ported from the original computation module.
public static class Computation
{
    public const int ElevationMedianWindow = 9;
    public const int ElevationAvgWindow = 21;
    public const double ElevationThreshold = 0.1;

    /// WGS-84 ellipsoidal distance in meters (Vincenty inverse; matches geopy's geodesic to sub-mm).
    public static double GeodesicMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double a = 6378137.0, f = 1 / 298.257223563, b = a * (1 - f);
        if (lat1 == lat2 && lon1 == lon2) return 0;
        double L = ToRad(lon2 - lon1);
        double U1 = Math.Atan((1 - f) * Math.Tan(ToRad(lat1)));
        double U2 = Math.Atan((1 - f) * Math.Tan(ToRad(lat2)));
        double sinU1 = Math.Sin(U1), cosU1 = Math.Cos(U1), sinU2 = Math.Sin(U2), cosU2 = Math.Cos(U2);
        double lambda = L, sinSigma, cosSigma, sigma, cosSqAlpha, cos2SigmaM;
        for (int i = 0; ; i++)
        {
            double sinLambda = Math.Sin(lambda), cosLambda = Math.Cos(lambda);
            sinSigma = Math.Sqrt(Math.Pow(cosU2 * sinLambda, 2) + Math.Pow(cosU1 * sinU2 - sinU1 * cosU2 * cosLambda, 2));
            if (sinSigma == 0) return 0;
            cosSigma = sinU1 * sinU2 + cosU1 * cosU2 * cosLambda;
            sigma = Math.Atan2(sinSigma, cosSigma);
            double sinAlpha = cosU1 * cosU2 * sinLambda / sinSigma;
            cosSqAlpha = 1 - sinAlpha * sinAlpha;
            cos2SigmaM = cosSqAlpha == 0 ? 0 : cosSigma - 2 * sinU1 * sinU2 / cosSqAlpha;
            double C = f / 16 * cosSqAlpha * (4 + f * (4 - 3 * cosSqAlpha));
            double prev = lambda;
            lambda = L + (1 - C) * f * sinAlpha * (sigma + C * sinSigma * (cos2SigmaM + C * cosSigma * (-1 + 2 * cos2SigmaM * cos2SigmaM)));
            if (Math.Abs(lambda - prev) < 1e-12) break;
            // Near-antipodal points don't converge; haversine is plenty for consecutive GPS fixes anyway.
            if (i > 200) return Haversine(lat1, lon1, lat2, lon2);
        }
        double uSq = cosSqAlpha * (a * a - b * b) / (b * b);
        double A = 1 + uSq / 16384 * (4096 + uSq * (-768 + uSq * (320 - 175 * uSq)));
        double B = uSq / 1024 * (256 + uSq * (-128 + uSq * (74 - 47 * uSq)));
        double deltaSigma = B * sinSigma * (cos2SigmaM + B / 4 * (cosSigma * (-1 + 2 * cos2SigmaM * cos2SigmaM)
            - B / 6 * cos2SigmaM * (-3 + 4 * sinSigma * sinSigma) * (-3 + 4 * cos2SigmaM * cos2SigmaM)));
        return b * A * (sigma - deltaSigma);
    }

    static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRad(lat2 - lat1), dLon = ToRad(lon2 - lon1);
        double h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * 6371008.8 * Math.Asin(Math.Sqrt(h));
    }

    static double ToRad(double d) => d * Math.PI / 180;

    public static double Median(IReadOnlyList<double> values)
    {
        var s = values.Order().ToArray();
        int n = s.Length;
        return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
    }

    static double[] SlidingWindow(double[] values, int window, Func<double[], double> reduce)
    {
        if (window < 2) return (double[])values.Clone();
        int half = window / 2, n = values.Length;
        var result = new double[n];
        for (int i = 0; i < n; i++)
        {
            int from = Math.Max(0, i - half), to = Math.Min(n, i + half + 1);
            result[i] = reduce(values[from..to]);
        }
        return result;
    }

    /// Median filter followed by a moving average, as used for gain/loss and the stored elevation stream.
    public static double[] SmoothElevations(double[] values, int medianWindow = ElevationMedianWindow, int avgWindow = ElevationAvgWindow) =>
        SlidingWindow(SlidingWindow(values, medianWindow, w => Median(w)), avgWindow, w => w.Average());

    public static (double Gain, double Loss) SumElevationDeltas(IReadOnlyList<double> smoothed, double threshold = ElevationThreshold)
    {
        double gain = 0, loss = 0;
        for (int i = 1; i < smoothed.Count; i++)
        {
            double diff = smoothed[i] - smoothed[i - 1];
            if (diff > threshold) gain += diff;
            else if (diff < -threshold) loss -= diff;
        }
        return (gain, loss);
    }

    /// Max of the sliding-window medians so a single GPS glitch never becomes the maximum.
    public static double RobustMax(IReadOnlyList<double> values, int window = 5)
    {
        if (values.Count == 0) return 0;
        if (values.Count < window) return values.Max();
        double max = double.MinValue;
        for (int i = 0; i + window <= values.Count; i++)
            max = Math.Max(max, Median(values.Skip(i).Take(window).ToArray()));
        return max;
    }

    public static double NormalizedPower(IReadOnlyList<double> power) =>
        power.Count == 0 ? 0 : Math.Pow(power.Average(p => Math.Pow(p, 4)), 0.25);
}
