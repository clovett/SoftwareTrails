using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace SoftwareTrails.Utilities
{
    /// <summary>
    /// This delegate is used to find a matching item in the grid.  
    /// Return the index of the item or -1 if none found.
    /// </summary>
    public delegate int FindMatch(string text);

    public class TypeToFind : IDisposable
    {
        FindMatch finder;
        int start;
        protected DataGrid grid;
        string typedSoFar;
        int resetDelay;

        public bool IsEnabled { get; set; }

        public TypeToFind(DataGrid grid, FindMatch finder)
        {
            if (grid == null)
            {
                throw new ArgumentNullException("grid");
            }
            if (finder == null)
            {
                throw new ArgumentNullException("finder");
            }
            this.finder = finder;
            this.grid = grid;

            this.resetDelay = 500;
            RegisterEvents(true);
            IsEnabled = true;

            foreach (DataGridColumn c in grid.Columns)
            {
                if (c.CanUserSort && c.SortDirection.HasValue)
                {
                    sorted = c;
                    break;
                }
            }
        }

        void grid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            IsEnabled = true;
        }

        void grid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            IsEnabled = false;
        }

        void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            if (IsEnabled)
            {
                int tick = Environment.TickCount;
                string text = e.Text;
                foreach (char ch in text)
                {
                    if (ch < 0x20) return; // don't process control characters
                    if (tick < start || tick < this.resetDelay || start < tick - this.resetDelay)
                    {
                        typedSoFar = ch.ToString();
                    }
                    else
                    {
                        typedSoFar += ch.ToString();
                    }
                }
                int index = finder(typedSoFar);
                if (index >= 0)
                {
                    grid.SelectedIndex = index;
                    grid.ScrollIntoView(grid.SelectedItem);
                }
                start = tick;
            }
        }

        protected DataGridColumn sorted;

        void grid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            this.sorted = e.Column;
        }

        void RegisterEvents(bool register)
        {
            if (register)
            {
                this.grid.BeginningEdit += new EventHandler<DataGridBeginningEditEventArgs>(grid_BeginningEdit);
                this.grid.RowEditEnding += new EventHandler<DataGridRowEditEndingEventArgs>(grid_RowEditEnding);
                this.grid.Sorting += new DataGridSortingEventHandler(grid_Sorting);
                this.grid.PreviewTextInput += new TextCompositionEventHandler(OnTextInput);
            }
            else
            {
                this.grid.BeginningEdit -= new EventHandler<DataGridBeginningEditEventArgs>(grid_BeginningEdit);
                this.grid.RowEditEnding -= new EventHandler<DataGridRowEditEndingEventArgs>(grid_RowEditEnding);
                this.grid.Sorting -= new DataGridSortingEventHandler(grid_Sorting);
                this.grid.PreviewTextInput -= new TextCompositionEventHandler(OnTextInput);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RegisterEvents(false);
            }
        }
    }

}
