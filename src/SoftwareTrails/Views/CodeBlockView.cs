using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;

namespace SoftwareTrails
{
    public class CodeBlockView : Grid
    {
        StackPanel breadcrumbs;
        TextBlock current;
        WrapPanel content;
        string filter;
        string includeFilter;
        string excludeFilter;
        Dictionary<string, StackPanel> towers = new Dictionary<string, StackPanel>();
        Dictionary<string, CodeBlock> blocks = new Dictionary<string, CodeBlock>();
        const string HomeLabel = "<Home>";

        public CodeBlockView()
        {
            this.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            this.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

            breadcrumbs = new StackPanel() { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,            
                Margin = new Thickness(5)
            };

            Children.Add(breadcrumbs);
            
            current = new TextBlock()
            {
                MinHeight = 24,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            breadcrumbs.Children.Add(current);
            current.Text = HomeLabel;

            content = new WrapPanel();
            content.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            Grid.SetRow(content, 1);
            Children.Add(content);

            AddHandler(CodeBlock.SelectionChangedEvent, new RoutedEventHandler(OnSelectionChanged));
        }

        public event EventHandler FilterChanged;

        private void OnFilterChanged()
        {
            if (FilterChanged != null)
            {
                FilterChanged(this, EventArgs.Empty);
            }
        }

        public string IncludeFilter {
            get { return includeFilter; }
            set { includeFilter = string.IsNullOrWhiteSpace(value) ? null : value; OnFilterChanged();  }
        }

        public string ExcludeFilter 
        {
            get { return excludeFilter; }
            set { excludeFilter = string.IsNullOrWhiteSpace(value) ? null : value; OnFilterChanged(); }
        }

        public CodeBlock Selection 
        { 
            get { return this.selection; }
            set { Select(value); }
        }

        public void AddFilter(string filter)
        {
            HyperlinkButton back = new HyperlinkButton()
            {
                MinHeight = 24,
                Tag = this.filter,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            int len = breadcrumbs.Children.Count;
            breadcrumbs.Children.Insert(len - 1, back);
            back.Click += new RoutedEventHandler(OnBackClick);

            ClearBlocks();
            if (this.filter == null)
            {
                back.Content = HomeLabel + " ";
                this.filter = filter;
            }
            else
            {
                string[] parts = this.filter.Split('.');
                back.Content = parts[parts.Length-2] + ".";
                this.filter += filter;
            }
            if (!this.filter.EndsWith("."))
            {
                this.filter += ".";
            }
            current.Text = filter == null ? HomeLabel : filter;

            OnFilterChanged(); 
        }

        public void SetFilter(string filter)
        {
            ClearAll();
            if (filter != null)
            {
                foreach (string part in filter.Split('.'))
                {
                    if (!string.IsNullOrEmpty(part))
                    {
                        AddFilter(part);
                    }
                }
            }
        }

        void OnBackClick(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            SetFilter((string)b.Tag);
        }

        CodeBlock selection;

        void Select(CodeBlock item)
        {
            if (selection != item)
            {
                if (selection != null)
                {
                    selection.IsSelected = false;
                }

                selection = item;

                if (selection != null)
                {
                    selection.IsSelected = true;
                }
            }
        }
        
        void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            CodeBlock cb = (CodeBlock)e.OriginalSource;
            Select(cb);
            if (!cb.Method.IsMethod)
            {
                // selected a namespace or type, so drill down.
                this.SetFilter(cb.Method.FullName);
            }
        }

        public void ClearBlocks()
        {
            content.Children.Clear();
            blocks.Clear();
            towers.Clear();
            if (selection != null)
            {
                selection.IsSelected = false;
                selection = null;
            }
        }

        public event EventHandler EmptyChanged;

        private void OnEmptyChanged()
        {
            if (EmptyChanged != null)
            {
                EmptyChanged(this, EventArgs.Empty);
            }
        }

        public void ClearAll()
        {
            ClearBlocks();
            breadcrumbs.Children.Clear();
            breadcrumbs.Children.Add(current);
            this.filter = null;
            OnFilterChanged();
            current.Text = HomeLabel;
            OnEmptyChanged();
        }

        public string FilterOut(MethodCall method)
        {
            string label = method.FullName;

            if (includeFilter != null)
            {
                if (!label.Contains(includeFilter))
                {
                    return null;
                }
            }

            if (excludeFilter != null)
            {
                if (label.Contains(excludeFilter))
                {
                    return null;
                }
            }

            if (filter != null)
            {
                if (!label.StartsWith(filter))
                {
                    // filter out this label.
                    return null;
                }
                else
                {
                    int filterLength = filter.Length;
                    if (label.Length < filterLength)
                    {
                        //???
                        return null;
                    }
                    else
                    {
                        label = label.Substring(filterLength);
                    }
                }
            }
            return label;
        }

        public void AddBlocks(CallHistory call, int batch)
        {
            while (call != null)
            {
                AddBlock(call.Method, call.Elapsed, batch);
                call = call.Next;
            }
        }

        private void AddBlock(MethodCall method, int elapsed, int batch)
        {
            string label = FilterOut(method);
            if (label == null)
            {
                return;
            }

            string title = "";
            bool isMethodCall = true;
            int i = label.IndexOf('.');
            if (i > 0)
            {
                title = label.Substring(0, i);
                label = label.Substring(i + 1);

                int j = label.IndexOf('.');
                if (j > 0)
                {
                    // trim off next level of detail.
                    label = label.Substring(0, j);
                    isMethodCall = false;
                }
            }

            bool wasEmpty = blocks.Count == 0;

            CodeBlock b = null;
            if (!blocks.TryGetValue(label, out b))
            {
                string typeName = title;

                if (filter != null)
                {
                    if (string.IsNullOrEmpty(title))
                    {
                        typeName = filter.Substring(0, filter.Length-1);
                    }
                    else
                    {
                        typeName = filter + typeName;
                    }
                }

                string fullName = typeName + "." + label;
                
                // use custom intermediate method call showing the filtered name only.
                b = new CodeBlock(new MethodCall(method.Id, fullName, isMethodCall)) 
                { 
                    View = this,
                    Margin = new Thickness(0, 0, 2, 0) // separate the columns.
                };
                if (isMethodCall)
                {
                    b.Namespace = null; // redundant at the leaf level.
                }
                blocks[label] = b;

                StackPanel panel = null;
                if (!towers.TryGetValue(title, out panel))
                {
                    panel = new StackPanel()
                    {
                        Orientation = System.Windows.Controls.Orientation.Vertical
                    };
                    towers[title] = panel;

                    content.Children.Add(panel);
                    var titleBlock = new CodeBlock(new MethodCall(0, typeName, false)) 
                    { 
                        View = this,
                        IsTitle = true,
                        Margin = new Thickness(0,0,2,0) // separate the columns.
                    }; 
                    panel.Children.Add(titleBlock);
                }

                int count = panel.Children.Count;
                CodeBlock previous = count > 0 ? (CodeBlock)panel.Children[count - 1] : null;
                panel.Children.Add(b);

                b.OnIndexChanged(count - 1, count - 1);
                if (previous != null)
                {
                    previous.OnIndexChanged(count - 2, count - 1);
                }
                
                b.TitleBlock = (CodeBlock)panel.Children[0];
            }

            b.AddCalls(1, elapsed, batch);

            // and summarise total on tower title.
            b.TitleBlock.AddCalls(1, elapsed, batch);

        }


        public bool HasBlocks
        {
            get { return towers.Count > 0; }
        }

        class DescendingCallComparer : IComparer<CodeBlock>
        {
            public int Compare(CodeBlock a, CodeBlock b)
            {
                int x = a.Calls;
                int y = b.Calls;
                if (x == y)
                {
                    return string.Compare(a.Method.Name, b.Method.Name);
                }
                return y - x;
            }
        }

        internal void Sort()
        {
            // sort by # of method calls, first across the towers then also inside each tower.

            List<CodeBlock> sortedTowers = new List<CodeBlock>();

            foreach (StackPanel panel in this.towers.Values)
            {
                CodeBlock title = (CodeBlock)panel.Children[0];
                sortedTowers.Add(title);
                content.Children.Remove(panel);
            }

            sortedTowers.Sort(new DescendingCallComparer());

            foreach (CodeBlock towerTitle in sortedTowers)
            {
                StackPanel panel = towerTitle.Parent as StackPanel;
                content.Children.Add(panel);

                // Now sort the blocks inside each tower
                List<CodeBlock> sortedBlocks = new List<CodeBlock>();
                CodeBlock title = (CodeBlock)panel.Children[0];
                panel.Children.RemoveAt(0);

                foreach (CodeBlock block in panel.Children)
                {
                    sortedBlocks.Add(block);
                }

                sortedBlocks.Sort(new DescendingCallComparer());

                panel.Children.Clear();

                panel.Children.Add(title);

                foreach (CodeBlock block in sortedBlocks)
                {
                    panel.Children.Add(block);
                }
            }

        }

        internal void ResetCounters()
        {
            foreach (StackPanel panel in this.towers.Values)
            {
                foreach (CodeBlock block in panel.Children)
                {
                    block.SetCalls(0, 0, 0);
                }
            }
        }
    }
}
