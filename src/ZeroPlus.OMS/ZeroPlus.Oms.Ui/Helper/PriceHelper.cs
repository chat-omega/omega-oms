using System;

namespace ZeroPlus.Oms.Ui.Helper
{
    /// <summary>
    /// PriceHelper class.
    /// The PadForNickel method takes a double input and pads it to the nearest nickel value.
    /// If the input is NaN, it returns the input as is.
    /// The method converts the input to a decimal value and separates it into whole and fractional parts.
    /// It then checks the last digit of the fraction and rounds it to the nearest nickel value.
    /// The rounded fraction is then added or subtracted from the whole part based on the sign of the input.
    /// The final result is returned as a double value.
    /// </summary>
    public class PriceHelper
    {
        /// <summary>
        /// round a double value to the nearest nickel value
        /// </summary>
        /// <param name="input">Price in dollars</param>
        /// <returns></returns>
        public static double PadForNickel(double input)
        {
            int multiplier = 100;
            if (double.IsNaN(input))
            {
                return input;
            }
            decimal value = Convert.ToDecimal(input);
            int whole = (int)value;

            int fraction = (int)((Math.Abs(value) - Math.Abs(whole)) * multiplier);

            if (input == 0 || fraction == 0)
            {
                return input;
            }

            int lastDigit = fraction % 10;
            if (lastDigit is > 0 and < 5)
            {
                int diffW5 = 5 - lastDigit;
                fraction = lastDigit < diffW5 ? fraction - lastDigit : fraction + diffW5;
            }
            else if (lastDigit is > 5 and <= 9)
            {
                int diffW5 = lastDigit - 5;
                int diffW10 = 10 - lastDigit;
                fraction = diffW5 < diffW10 ? fraction - diffW5 : fraction + diffW10;
            }

            decimal newFraction = fraction / (decimal)multiplier;
            return input > 0
                ? Convert.ToDouble(whole + newFraction)
                : Convert.ToDouble(whole - newFraction);
        }
    }
}
