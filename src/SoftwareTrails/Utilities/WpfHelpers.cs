﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SoftwareTrails
{
    static class WpfHelpers
    {
        public static Point Center(this Rect r)
        {
            return new Point(r.Left + (r.Width / 2), r.Top + (r.Height / 2));
        }

        /// <summary>
        /// Finds an ancestor of a given type starting with a given element in the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of the item we are looking for.</typeparam>
        /// <param name="child">A direct or indirect child of the
        /// queried item.</param>
        /// <returns>The first parent item that matches the submitted
        /// type parameter. If not matching item can be found, a null
        /// reference is being returned.</returns>
        public static T FindAncestor<T>(this DependencyObject current) where T : DependencyObject
        {
            do
            {
                //check if the current matches the type we're looking for
                T parent = current as T;
                if (parent != null)
                {
                    return parent;
                }

                //get parent item
                current = GetParentObject(current);
            }
            while (current != null);

            return null;
        }


        /// <summary>
        /// This method is an alternative to WPF's
        /// <see cref="VisualTreeHelper.GetParent"/> method, which also
        /// supports content elements. Keep in mind that for content element,
        /// this method falls back to the logical tree of the element!
        /// </summary>
        /// <param name="child">The item to be processed.</param>
        /// <returns>The submitted item's parent, if available. Otherwise
        /// null.</returns>
        public static DependencyObject GetParentObject(this DependencyObject child)
        {
            if (child == null) return null;

            //handle content elements separately
            ContentElement contentElement = child as ContentElement;
            if (contentElement != null)
            {
                DependencyObject parent = ContentOperations.GetParent(contentElement);
                if (parent != null) return parent;

                FrameworkContentElement fce = contentElement as FrameworkContentElement;
                return fce != null ? fce.Parent : null;
            }

            //also try searching for parent in framework elements (such as DockPanel, etc)
            FrameworkElement frameworkElement = child as FrameworkElement;
            if (frameworkElement != null)
            {
                DependencyObject parent = frameworkElement.Parent;
                if (parent != null) return parent;
            }

            //if it's not a ContentElement/FrameworkElement, rely on VisualTreeHelper
            try
            {
                return VisualTreeHelper.GetParent(child);
            }
            catch
            {
                // exception is thrown if child is a FlowDocument "Inline" object (it is not a Visual).
                return null;
            }
        }

    }

    class BooleanToOpacityMaskConverter : IValueConverter
    {
        static Brush DisabledOpacityMaskBrush = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Brush disabledBrush = DisabledOpacityMaskBrush;
            double opacity = 0;
            if (parameter is string)
            {
                double.TryParse((string)parameter, out opacity);
                if (opacity < 0) opacity = 0;
                if (opacity > 1) opacity = 1;
                disabledBrush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0));
            }
            return (bool)value ? DependencyProperty.UnsetValue : disabledBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
