using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SoftwareTrails
{
    /// <summary>
    /// Interaction logic for CalLStackControl.xaml
    /// </summary>
    public partial class CallStackControl : UserControl
    {
        public CallStackControl()
        {
            InitializeComponent();

            this.Visibility = System.Windows.Visibility.Hidden;
            CloseBox.Click += OnCloseBoxClick;
            ExpandAll.Click += OnExpandAll;
            CollapseAll.Click += OnCollapseAll;
            ShowHotPath.Click += OnShowHotPath;
            View.GraphLayoutChanged += View_GraphLayoutChanged;
        }

        public event EventHandler Closed;

        void OnClosed()
        {
            if (Closed != null)
            {
                Closed(this, EventArgs.Empty);
            }
        }
        
        void View_GraphLayoutChanged(object sender, EventArgs e)
        {
            // We are a Grid within another Grid and we want to size ourselves to
            // fit the View if possible, but scroll the view if it's too big.

            if (View.Width == 0 || View.Watching == null)
            {
                Close();
                return;
            }

            Point scrollerPosition = CallStackScroller.TransformToAncestor(this).Transform(new Point(0, 0));

            double allowedWidth = this.ActualWidth;
            double allowedHeight = this.ActualHeight;

            double contentWidth = Math.Min(allowedWidth, Math.Max(200, View.Width));
            double contentHeight = Math.Min(allowedHeight, View.Height + scrollerPosition.Y);

            Thickness newMargin = new Thickness(allowedWidth - contentWidth, 0, 0, allowedHeight - contentHeight);

            if (Visibility != System.Windows.Visibility.Visible)
            {
                // for the reveal animation we have to establish the "from" position off the right of the window so it slides in.
                this.ContentGrid.Margin = new Thickness(ActualWidth, 0, 0, allowedHeight - contentHeight);
                this.Visibility = System.Windows.Visibility.Visible;
            }

            // now slide it to the desired position.
            SlideTo(newMargin, false);
        }

        void OnCloseBoxClick(object sender, RoutedEventArgs e)
        {
            View.Watching = null;
            OnClosed();
            Close();
        }

        public void Close()
        {
            if (this.Visibility == System.Windows.Visibility.Visible)
            {
                SlideTo(new Thickness(this.ActualWidth, 0, 0, ContentGrid.Margin.Bottom), true);
            }
        }

        public CallStackView View
        {
            get { return this.CallStack; }
        }

        void OnExpandAll(object sender, RoutedEventArgs e)
        {
            View.ExpandAll();
        }
        void OnCollapseAll(object sender, RoutedEventArgs e)
        {
            View.CollapseAll();
        }

        void OnShowHotPath(object sender, RoutedEventArgs e)
        {
            View.ExpandHotPath();
        }

        internal void SlideTo(Thickness newMargin, bool closing)
        {
            var animation = new ThicknessAnimation(ContentGrid.Margin, newMargin, new Duration(TimeSpan.FromMilliseconds(200)), FillBehavior.Stop);
            animation.Completed += new EventHandler((s, e) =>
            {
                if (closing)
                {
                    this.Visibility = System.Windows.Visibility.Hidden;
                }
                else
                {
                    // remove animation
                    ContentGrid.BeginAnimation(Grid.MarginProperty, null);
                    ContentGrid.Margin = newMargin;
                }
            });
            ContentGrid.BeginAnimation(Grid.MarginProperty, animation);
        }

    }
}
