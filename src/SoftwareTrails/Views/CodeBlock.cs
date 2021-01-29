using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Globalization;

namespace SoftwareTrails
{
    public class CodeBlock : Grid
    {        
        Border border;
        Border innerBorder;
        SolidColorBrush background;
        int calls;
        long elapsed; // milliseconds
        TextBlock nameLabel;
        TextBlock callLabel;
        TextBlock nsLabel;
        TextBlock elapsedLabel;
        bool isSelected;
        MethodCall method;
        const int Padding = 3;
        const int BorderThickness = 1;

        public static readonly RoutedEvent SelectionChangedEvent = System.Windows.EventManager.RegisterRoutedEvent("SelectionChanged",
                    RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CodeBlock));

        // COnstruct CodeBlock that shows the Namespace+Type and Method name in MethodCall object
        public CodeBlock(MethodCall method, bool showNamespace = true)
        {
            this.method = method;
            this.Focusable = true;
            
            background = new SolidColorBrush(GetBackgroundColor());

            border = new Border() {
                Background = background,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(BorderThickness)
            };

            innerBorder = new Border()
            {
                BorderBrush = Brushes.LightBlue,
                BorderThickness = new Thickness(0),
                Padding = new System.Windows.Thickness(Padding)
            };
            this.Children.Add(border);
            border.Child = innerBorder;

            if (!method.IsMethod)
            {
                innerBorder.BorderBrush = Brushes.DarkGray;
                border.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            }

            // Layout the labels in a Grid, Namespace on top row spanning all columns since they tend to be long,
            // Method name in next row in larger font
            // Counters on bottom row, 2 columns for numbers on the left & right.

            Grid labels = new Grid();            
            labels.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            labels.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            labels.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            labels.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            labels.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });

            nameLabel = new TextBlock() { Text = method.Name, FontSize = 10 };
            Grid.SetRow(nameLabel, 1);
            Grid.SetColumn(nameLabel, 0);
            Grid.SetColumnSpan(nameLabel, 2);
            labels.Children.Add(nameLabel);

            // label showing # of calls to this method
            callLabel = new TextBlock() { FontSize = 8 };
            callLabel.ToolTip = "Total number of calls to this method";
            Grid.SetRow(callLabel, 2);
            Grid.SetColumn(callLabel, 0);
            labels.Children.Add(callLabel);

            // label showing total elapsed time inside this method (in milliseconds)
            elapsedLabel = new TextBlock() { FontSize = 8, Margin = new Thickness(10,0,0,0) };
            elapsedLabel.ToolTip = "Total elapsed time inside this method in milliseconds.  \n" +
                                   "Since many methods are faster than 1 millisecond those \n" +
                                   "methods will show zero time.  This way only the really \n" +
                                   "expensive methods show up here.";
            Grid.SetRow(elapsedLabel, 2);
            Grid.SetColumn(elapsedLabel, 1);
            labels.Children.Add(elapsedLabel);
            
            this.innerBorder.Child = labels;

            nsLabel = new TextBlock() { FontSize = 8 };
            Grid.SetRow(nsLabel, 0);
            Grid.SetColumn(nsLabel, 0);
            Grid.SetColumnSpan(nsLabel, 2);
            labels.Children.Insert(0, nsLabel);

            string nsLabelText = method.Namespace;
            if (method.Type != null && method.Name != method.Type)
            {
                if (!string.IsNullOrEmpty(nsLabelText))
                {
                    nsLabelText += ".";
                }
                nsLabelText += method.Type;
            }
            nsLabel.Text = nsLabelText;

        }

        // called when the position of this code block changes within the parent column.
        public void OnIndexChanged(int newIndex, int count)
        {
            if (newIndex == 0 || isTitle) 
            {
                // make title bar stand out with border all around.
                this.border.BorderThickness = new Thickness(1);
                this.innerBorder.BorderThickness = new Thickness(0);
            }
            else if (newIndex < count)
            {
                // black sides
                this.border.BorderThickness = new Thickness(1,0,1,0);
                // lighter inside row borders on the bottom, the top will then
                // look like it was drawn by the previous block to make it nice and thin.
                this.innerBorder.BorderThickness = new Thickness(0, 0, 0, 1);                
            }
            else
            {
                // black sides and bottom
                this.border.BorderThickness = new Thickness(1, 0, 1, 1);
                // no top row border
                this.innerBorder.BorderThickness = new Thickness(0, 0, 0, 0);   
            }
        }

        // which view this block belons to (since GetParent is not working!)
        public FrameworkElement View { get; set; }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Focus();
            IsSelected = true;
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Focus();
            base.OnMouseRightButtonDown(e);
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if (this.FocusVisualStyle == null)
            {
                this.FocusVisualStyle = (Style)FindResource("ListViewItemFocusVisual");
            }
            base.OnGotFocus(e);
        }

        public int Calls { get { return this.calls; } }

        public MethodCall Method { get { return this.method; } }

        public string Caption
        {
            get { return nameLabel.Text; }
            set { nameLabel.Text = value; }
        }


        public string Namespace 
        { 
            get { return nsLabel == null ? null : nsLabel.Text; }
            set {
                if (value == null)
                {
                    nsLabel.Text = "";
                    nsLabel.Visibility = System.Windows.Visibility.Collapsed;
                } else {
                    nsLabel.Text = value;
                    nsLabel.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }

        Color GetBackgroundColor()
        {
            if (isSelected)
            {
                return method.IsMethod ? Colors.Yellow : Colors.Green;
            }
            else
            {
                return method.IsMethod ? Colors.White : Colors.Teal;
            }
        }

        public bool IsSelected 
        {
            get { return this.isSelected; }
            set
            {
                if (this.IsSelected != value)
                {
                    this.isSelected = value;                    
                    SetBackgroundColor(GetBackgroundColor());
                    this.RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
                }
            }
        }

        bool mouseInside;

        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
        {
            mouseInside = true;
            Color c = GetBackgroundColor();
            HlsColor hls = new HlsColor(c);
            if (c == Colors.White)
            {
                hls.Darken(0.25f);
            }
            else
            {
                hls.Lighten(0.25f);
            }
            SetBackgroundColor(hls.Color);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            mouseInside = false;
            SetBackgroundColor(GetBackgroundColor());
            base.OnMouseLeave(e);
        }

        internal void SetBackgroundColor(Color c) 
        {
            background.BeginAnimation(SolidColorBrush.ColorProperty, null);
            background.Color = c;
        }

        static Duration duration = new Duration(TimeSpan.FromSeconds(5));
        int lastBatch;

        protected override Size MeasureOverride(Size constraint)
        {
            callLabel.Text = this.calls.ToString();

            // we have elapsed time now, so show it too.
            elapsedLabel.Text = (this.elapsed != 0) ? this.elapsed.ToString() : "";

            return base.MeasureOverride(constraint);
        }

        internal void AddCalls(int calls, long milliseconds, int batch)
        {
            SetCalls(this.calls + calls, this.elapsed + milliseconds, batch);
        }

        internal void SetCalls(int calls, long elapsed, int batch)
        {
            this.calls = calls;

            if (elapsed != this.elapsed)
            {
                this.elapsed = elapsed;

            }

            InvalidateMeasure();

            if (lastBatch != batch)
            {
                // then start animating.
                lastBatch = batch;
                if (!mouseInside)
                {
                    background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Red, GetBackgroundColor(), duration));
                }
            }
        }

        Button outgoingExpander;

        internal Button ShowOutgoingExpander(bool show)
        {
            if (show)
            {
                if (outgoingExpander == null)
                {
                    outgoingExpander = new ExpanderBox()
                    {
                        RenderTransform = new TranslateTransform(8,-16),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Top
                    };
                    this.Children.Add(outgoingExpander);
                }
            }
            else
            {
                this.Children.Remove(outgoingExpander);
                outgoingExpander = null;
            }
            return outgoingExpander;
        }


        Button incomingExpander;

        internal Button ShowIncomingExpander(bool show)
        {
            if (show)
            {
                if (incomingExpander == null)
                {
                    incomingExpander = new ExpanderBox()
                    {
                        RenderTransform = new TranslateTransform(8,16),                        
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Bottom
                    };
                    this.Children.Add(incomingExpander);
                }
            }
            else
            {
                this.Children.Remove(incomingExpander);
                incomingExpander = null;
            }
            return incomingExpander;
        }

        public CodeBlock TitleBlock { get; set; }

        bool isTitle;

        public bool IsTitle
        {
            get { return isTitle; }
            set
            {
                if (isTitle != value)
                {
                    isTitle = value;
                    nameLabel.FontWeight = (value ? FontWeights.Bold : FontWeights.Normal);
                    border.CornerRadius = (value ? new CornerRadius(3, 3, 0, 0) : new CornerRadius(0,0,0,0));
                }
            }
        }


    }
}
