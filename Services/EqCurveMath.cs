using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kater1EQ.Services
{
    public enum EqFilterType
    {
        Bell,       // PK  - Peaking/Bell
        LowShelf,   // LSC
        HighShelf,  // HSC
        LowPass,    // LPQ
        HighPass,   // HPQ
        Notch       // NO  - Band-reject cố định (không có Gain, chỉ có độ sâu qua Q)
    }

    /// <summary>
    /// RBJ Audio EQ Cookbook formulas - the exact same biquad math Equalizer APO uses internally
    /// for its "Filter:" directive. Used here (a) to compute the real frequency-response curve for
    /// the UI, and (b) to find the worst-case positive peak across the audible range so gain
    /// compensation can be computed - without needing to touch the actual audio stream, since the
    /// real-time DSP itself is performed by Equalizer APO, not by this process.
    /// </summary>
    public static class EqCurveMath
    {
        /// <summary>Magnitude in dB of a single band at a given frequency, evaluated at 48kHz reference
        /// sample rate (Equalizer APO computes its own coefficients per-device sample rate at load time;
        /// 48kHz is used here purely for UI/estimation purposes and matches almost all playback devices
        /// closely enough that the drawn curve and the compensation estimate are accurate in practice).
        /// <paramref name="slopeDbPerOct"/> chỉ áp dụng cho LowPass/HighPass: 1 biquad (RBJ) vốn có độ
        /// dốc 12 dB/oct; Kater1EQ đạt 24/36/48 dB/oct bằng cách cascade nhiều biquad giống hệt nhau nối
        /// tiếp - vì các stage giống hệt nhau, biên độ (dB) của cascade = biên độ 1 stage nhân với số stage,
        /// đây là công thức chính xác (không xấp xỉ) khi các stage đồng nhất nối tiếp nhau.</summary>
        public static double MagnitudeDb(EqFilterType type, double freqHz, double gainDb, double q, double evalFreqHz, double sampleRate = 48000, int slopeDbPerOct = 12)
        {
            if (Math.Abs(gainDb) < 1e-9 && type == EqFilterType.Bell) return 0.0;

            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * freqHz / sampleRate;
            double cosW0 = Math.Cos(w0);
            double sinW0 = Math.Sin(w0);
            double alpha = sinW0 / (2.0 * Math.Max(q, 0.05));

            double b0, b1, b2, a0, a1, a2;
            switch (type)
            {
                case EqFilterType.Bell:
                    b0 = 1 + alpha * A; b1 = -2 * cosW0; b2 = 1 - alpha * A;
                    a0 = 1 + alpha / A; a1 = -2 * cosW0; a2 = 1 - alpha / A;
                    break;
                case EqFilterType.LowShelf:
                {
                    double sqrtA = Math.Sqrt(A), beta = 2 * sqrtA * alpha;
                    b0 = A * ((A + 1) - (A - 1) * cosW0 + beta);
                    b1 = 2 * A * ((A - 1) - (A + 1) * cosW0);
                    b2 = A * ((A + 1) - (A - 1) * cosW0 - beta);
                    a0 = (A + 1) + (A - 1) * cosW0 + beta;
                    a1 = -2 * ((A - 1) + (A + 1) * cosW0);
                    a2 = (A + 1) + (A - 1) * cosW0 - beta;
                    break;
                }
                case EqFilterType.HighShelf:
                {
                    double sqrtA = Math.Sqrt(A), beta = 2 * sqrtA * alpha;
                    b0 = A * ((A + 1) + (A - 1) * cosW0 + beta);
                    b1 = -2 * A * ((A - 1) + (A + 1) * cosW0);
                    b2 = A * ((A + 1) + (A - 1) * cosW0 - beta);
                    a0 = (A + 1) - (A - 1) * cosW0 + beta;
                    a1 = 2 * ((A - 1) - (A + 1) * cosW0);
                    a2 = (A + 1) - (A - 1) * cosW0 - beta;
                    break;
                }
                case EqFilterType.LowPass:
                    b0 = (1 - cosW0) / 2; b1 = 1 - cosW0; b2 = (1 - cosW0) / 2;
                    a0 = 1 + alpha; a1 = -2 * cosW0; a2 = 1 - alpha;
                    break;
                case EqFilterType.HighPass:
                    b0 = (1 + cosW0) / 2; b1 = -(1 + cosW0); b2 = (1 + cosW0) / 2;
                    a0 = 1 + alpha; a1 = -2 * cosW0; a2 = 1 - alpha;
                    break;
                case EqFilterType.Notch:
                    // RBJ notch: không có tham số Gain (luôn là band-reject cố định), Q quyết định độ hẹp.
                    b0 = 1; b1 = -2 * cosW0; b2 = 1;
                    a0 = 1 + alpha; a1 = -2 * cosW0; a2 = 1 - alpha;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            double w = 2.0 * Math.PI * evalFreqHz / sampleRate;
            var z1 = Complex.Exp(new Complex(0, -w));
            var z2 = Complex.Exp(new Complex(0, -2 * w));
            var num = b0 + b1 * z1 + b2 * z2;
            var den = 1 + a1 * z1 + a2 * z2;
            double mag = (num / den).Magnitude;
            double db = 20.0 * Math.Log10(Math.Max(mag, 1e-9));

            if (type == EqFilterType.LowPass || type == EqFilterType.HighPass)
            {
                int stages = Math.Max(1, slopeDbPerOct / 12);
                db *= stages;
            }

            return db;
        }

        /// <summary>Số stage biquad cần cascade để đạt đúng slope mong muốn (chỉ có ý nghĩa với LP/HP).</summary>
        public static int StageCountForSlope(int slopeDbPerOct) => Math.Max(1, slopeDbPerOct / 12);

        /// <summary>Summed magnitude in dB of all bands (cascade) at a given frequency.</summary>
        public static double CombinedMagnitudeDb(IEnumerable<(EqFilterType type, double freqHz, double gainDb, double q, int slope)> bands, double evalFreqHz)
        {
            double sum = 0;
            foreach (var b in bands)
                sum += MagnitudeDb(b.type, b.freqHz, b.gainDb, b.q, evalFreqHz, slopeDbPerOct: b.slope);
            return sum;
        }

        /// <summary>
        /// Worst-case positive peak of the combined curve across 20Hz-20kHz (log-spaced scan).
        /// Used to decide how much auto gain compensation is needed to avoid clipping - never more
        /// than that, so the relative shape of the EQ curve is preserved exactly.
        /// </summary>
        public static double FindPeakDb(IEnumerable<(EqFilterType type, double freqHz, double gainDb, double q, int slope)> bands, int steps = 200)
        {
            var list = new List<(EqFilterType, double, double, double, int)>(bands);
            double worst = 0.0;
            for (int i = 0; i < steps; i++)
            {
                double t = i / (double)(steps - 1);
                double freq = 20.0 * Math.Pow(1000.0, t); // 20Hz..20kHz
                double sum = CombinedMagnitudeDb(list, freq);
                if (sum > worst) worst = sum;
            }
            return worst;
        }
    }
}
