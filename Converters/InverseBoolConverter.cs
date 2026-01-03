using System;
using System.Globalization;
using System.Windows.Data;

namespace PasteList.Converters
{
    /// <summary>
    /// 布尔值取反转换器
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值取反
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        /// <summary>
        /// 将布尔值取反（反向转换）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
