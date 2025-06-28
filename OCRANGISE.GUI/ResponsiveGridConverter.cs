using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OCRANGISE.GUI
{
	public class ResponsiveGridConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is double width && parameter is string columnType)
			{
				return columnType switch
				{
					"MainColumn" => width < 800 ? new GridLength(1, GridUnitType.Star) : new GridLength(2, GridUnitType.Star),
					"SideColumn" => width < 800 ? new GridLength(0) : new GridLength(1, GridUnitType.Star),
					_ => new GridLength(1, GridUnitType.Star)
				};
			}
			return new GridLength(1, GridUnitType.Star);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
