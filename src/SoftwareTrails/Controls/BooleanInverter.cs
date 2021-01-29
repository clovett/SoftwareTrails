using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace SoftwareTrails
{
    /// <summary>
    /// Converter to invert a bool value.
    /// Input -> Output mapping is:
    ///  true -> false
    /// false -> true
    /// </summary>
    public class BooleanInverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool? v = value as bool?;
            if (v.HasValue)
                return !v.Value;

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
