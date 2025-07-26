using System;
using System.Globalization;
using System.Windows.Data;

namespace IssueManager.Views
{
    public class BoolToEditSaveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && b ? "Save" : "Edit";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
