using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftwareTrails.Utilities
{
    public static class MathUtilities
    {
        /// <summary>
        /// Return the Mean of the given numbers.
        /// </summary>
        public static double Mean(IEnumerable<double> values)
        {
            double sum = 0;
            double count = 0;
            foreach (double d in values)
            {
                sum += d;
                count++;
            }
            if (count == 0) return 0;
            return sum / count;
        }

        public static double StandardDeviation(IEnumerable<double> values)
        {
            double mean = Mean(values);
            double totalSquares = 0;
            int count = 0;
            foreach (double v in values)
            {
                count++;
                double diff = mean - v;
                totalSquares += diff * diff;
            }
            if (count == 0)
            {
                return 0;
            }
            return Math.Sqrt((double)(totalSquares / (double)count));
        }

        /// <summary>
        /// Return the variance, sum of the difference between each value and the mean, squared.
        /// </summary>
        public static double Variance(IEnumerable<double> values)
        {
            double mean = Mean(values);
            double variance = 0;
            foreach (double d in values)
            {
                double diff = (d - mean);
                variance += (diff * diff);
            }
            return variance;
        }

    }
}



