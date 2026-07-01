using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Models.Utils
{
    public class MathHelper
    {
        public static bool Equals(double a, double b)
        {
            return Compare(a, b) == 0;
        }

        public static int Compare(double a, double b)
        {
            return Compare(a, b, 5E-07);
        }

        public static int Compare(double a, double b, double eps)
        {
            if (a > b + eps)
            {
                return 1;
            }

            return a < b - eps ? -1 : (double.IsNaN(a) ? 1 : 0) - (double.IsNaN(b) ? 1 : 0);
        }

        public static List<int> GetLCDAdjustedList(List<int> inputList, out int divisor)
        {
            int div = GCD(inputList.Select(x => Math.Abs(x)).ToList());
            divisor = div;
            if (div == 0)
            {
                return inputList;
            }

            inputList = inputList.Select(x => x / div).ToList();
            return inputList;
        }

        public static int GCD(List<int> inputList)
        {
            if (inputList.Count == 0)
            {
                return 0;
            }

            int a = inputList.Max();
            foreach (int input in inputList)
            {
                a = GCD2(a, input);
            }

            return a;
        }

        private static int GCD2(int a, int b)
        {
            while (a * b != 0)
            {
                if (a > b)
                {
                    a %= b;
                }
                else
                {
                    b %= a;
                }
            }
            return Math.Max(a, b);
        }

        public static double Round(double value)
        {
            return (int)((value * 1000000.0) + 0.5) / 1000000.0;
        }

        public static double RoundEvenCents(double value)
        {
            double d = value * 100.0;
            double num1 = Math.Floor(d);
            double num2 = d - num1;
            return (num2 >= 0.49995 ? (num2 <= 0.50005 ? (((long)num1 & 1L) != 0L ? num1 + 1.0 : num1) : num1 + 1.0) : num1) / 100.0;
        }

        public static double PadForNicel(double input, bool floor)
        {
            if (double.IsNaN(input) || double.IsInfinity(input))
            {
                return input;
            }
            int multiplier = 100;
            decimal value = Convert.ToDecimal(input);
            int whole = (int)value;

            int fraction = (int)((Math.Abs(value) - Math.Abs(whole)) * multiplier);

            if (input == 0)
            {
                return input;
            }

            int lastDigit = fraction % 10;
            if (lastDigit > 0 && lastDigit < 5)
            {
                int diffW5 = 5 - lastDigit;
                if (!floor)
                {
                    fraction = lastDigit < diffW5 ? fraction - lastDigit : fraction + diffW5;
                }
                else
                {
                    fraction = input > 0 ? fraction - lastDigit : fraction + diffW5;
                }
            }
            else if (lastDigit > 5 && lastDigit <= 9)
            {
                int diffW5 = lastDigit - 5;
                int diffW10 = 10 - lastDigit;
                if (!floor)
                {
                    fraction = diffW5 < diffW10 ? fraction - diffW5 : fraction + diffW10;
                }
                else
                {
                    fraction = input > 0 ? fraction - diffW5 : fraction + diffW10;
                }
            }

            decimal newFraction = fraction / (decimal)multiplier;
            return input > 0 ? Convert.ToDouble(whole + newFraction) : Convert.ToDouble(whole - newFraction);
        }

        public static double StdDev(IEnumerable<double> values)
        {
            double num = 0.0;
            if (values.Count() > 0)
            {
                double avg = values.Average();
                num = Math.Sqrt(values.Sum<double>(d => Math.Pow(d - avg, 2.0)) / (values.Count<double>() - 1));
            }
            return num;
        }
    }
}
