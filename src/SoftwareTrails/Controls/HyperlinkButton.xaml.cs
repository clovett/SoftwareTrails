using System.Windows.Controls;
using System.Windows;

namespace SoftwareTrails
{
    /// <summary>
    /// Interaction logic for HyperlinkButton.xaml
    /// </summary>
    public partial class HyperlinkButton : Button
    {
        public HyperlinkButton()
        {
            InitializeComponent();
        }

        private void OnGridMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Grid grid = (Grid)sender;
            TextBlock text = (TextBlock)grid.Children[0];
            text.TextDecorations.Add(TextDecorations.Underline);
        }

        private void OnGridMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Grid grid = (Grid)sender;
            TextBlock text = (TextBlock)grid.Children[0];
            text.TextDecorations.Clear();
        }
    }
}
