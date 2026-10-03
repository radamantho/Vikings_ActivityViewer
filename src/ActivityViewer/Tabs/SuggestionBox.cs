using System.Collections.Generic;
using System.Windows.Controls;

namespace ActivityViewer.Tabs
{
    internal static class SuggestionBox
    {
        internal static void Fill(ComboBox box, IEnumerable<string>? items)
        {
            string text = box.Text;
            box.ItemsSource = items;
            box.Text = text;
        }
    }
}
