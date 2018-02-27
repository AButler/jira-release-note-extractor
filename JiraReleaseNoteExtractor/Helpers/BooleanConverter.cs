using System;
using System.Windows.Data;

namespace JiraReleaseNoteExtractor.Helpers {
  public class BooleanConverter: IValueConverter {
    public object TrueValue { get; set; }
    public object FalseValue { get; set; }

    public object Convert( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture ) {
      var boolValue = System.Convert.ToBoolean( value );

      return boolValue ? TrueValue : FalseValue;
    }

    public object ConvertBack( object value, Type targetType, object parameter, System.Globalization.CultureInfo culture ) {
      return Equals( value, TrueValue );
    }
  }
}