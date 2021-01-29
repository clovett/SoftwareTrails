using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Globalization;
using System.Windows.Shapes;
using Microsoft.Msagl;
using Microsoft.Msagl.Layout;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Core.Layout;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace SoftwareTrails
{
    public class CallStackView : Canvas
    {
        MethodCall watching;
        CallGraph graph = new CallGraph();
        Brush foreground = Brushes.White;
        TreeLayout layout;
        int batch;
        CallGraphNode focus;
        bool layoutDirty;
        DispatcherTimer dirtyTimer;
        Dispatcher dispatcher;

        public CallStackView()
        {
            this.Width = 0;
            this.Height = 0;
            this.dispatcher = this.Dispatcher; // so we can access it from background thread.
            this.dirtyTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, OnUpdateLayout, this.dispatcher);
            this.dirtyTimer.IsEnabled = false;
            this.dirtyTimer.Stop();
        }

        public event EventHandler EmptyChanged;

        private void OnEmptyChanged()
        {
            if (EmptyChanged != null)
            {
                EmptyChanged(this, EventArgs.Empty);
            }
        }

        internal MethodCall Watching
        {
            get { return watching; }
            set
            {
                watching = value;
                this.graph.Clear();
                this.Children.Clear();
                this.Width = 0;
                this.Height = 0;
                this.focus = null;
                OnGraphLayoutChanged();
                OnEmptyChanged();
            }
        }

        public CallGraph Graph { get { return this.graph; } }

        /// <summary>
        /// This method has to be thread safe.  It will be called from background thread.
        /// We need to process this as quickly as possible.
        /// </summary>
        public void ShowStack(IEnumerable<CallHistory> stack)
        {
            CallGraphNode callee = null;
            batch++;
            foreach (CallHistory call in stack)
            {
                MethodCall method = call.Method;
                CallGraphNode node = graph.GetOrCreateNode(method);
                if (watching != null && node.Method.Matches(watching))
                {
                    focus = node;
                }

                // the rest of the stack must have at least one call.
                node.Calls++;

                layoutDirty = true;

                if (callee != null)
                {
                    // since we are traversing the stack from top to bottom the previous item
                    // is the method being called (hence callee).
                    // no need for self links.
                    if (callee != node)
                    {
                        graph.GetOrCreateLink(node, callee);
                    }
                }

                callee = node;
            }

            LazyUpdate();
        }

        /// <summary>
        /// Notify when a new method is being entered
        /// </summary>
        /// <param name="calledFrom">The method that called the new method</param>
        /// <param name="method">The method we are entering</param>
        internal void EnterMethod(CallHistory calledFrom, CallHistory method)
        {
            CallGraphNode node = graph.GetOrCreateNode(method.Method);
            node.Calls++;
            layoutDirty = true;

            if (calledFrom != null)
            {
                CallGraphNode caller = graph.GetOrCreateNode(calledFrom.Method);
                // no need for self links.
                if (caller != node)
                {
                    graph.GetOrCreateLink(caller, node);
                }
            }
            LazyUpdate();
        }

        internal void ExitMethod(CallHistory exiting, CallHistory backTo)
        {
            CallGraphNode node = graph.GetOrCreateNode(exiting.Method);
            // update the elapsed time.
            if (exiting.Elapsed != 0)
            {
                node.Elapsed += exiting.Elapsed;
                layoutDirty = true;
            }
            if (layoutDirty) 
            {
                LazyUpdate();
            }
        }

        internal void LazyUpdate()
        {
            if (layoutDirty && layout == null && !dirtyTimer.IsEnabled)
            {
                dirtyTimer.IsEnabled = true;
                dirtyTimer.Start();
            }

        }

        internal void OnUpdateLayout(object sender, EventArgs e)
        {
            dirtyTimer.Stop();
            dirtyTimer.IsEnabled = false;

            if (layout == null && watching != null)
            {
                BindView();
                StartLayout();
            }
        }

        public event EventHandler GraphLayoutChanged;

        private void OnGraphLayoutChanged()
        {
            if (this.GraphLayoutChanged != null)
            {
                this.GraphLayoutChanged(this, EventArgs.Empty);
            }
        }

        private void StartLayout()
        {
            if (layoutDirty)
            {
                try
                {
                    if (this.layout != null)
                    {
                        this.layout.StopLayout();
                    }
                    var tree = new TreeLayout();
                    this.layout = tree;
                    layout.BeginLayoutGraph(graph);
                    layout.LayoutFinished += new EventHandler((s, e) =>
                    {
                        this.Width = tree.Width;
                        this.Height = tree.Height;
                        this.layout = null;
                        this.UpdateLayout();

                        OnGraphLayoutChanged();

                        if (layoutDirty)
                        {
                            // need to catch up to more changes!
                            LazyUpdate();
                        }
                    });
                }
                catch
                {
                    // layout blew up, so clear the object.
                    this.layout = null;
                }
                finally
                {
                    layoutDirty = false;
                }
            }
        }

        public void Terminate()
        {
            if (layout != null)
            {
                layout.StopLayout();
            }
        }

        internal CodeBlock BindNode(CallGraphNode node)
        {
            CodeBlock block = node.State as CodeBlock;

            if (block == null)
            {
                layoutDirty = true;
                MethodCall call = node.Method;

                block = new CodeBlock(call) { View = this };

                if (watching != null && call.Matches(watching))
                {
                    block.IsSelected = true;
                }

                block.Tag = node;
                this.Children.Add(block);
                node.State = block;

                // transfer call state to the newly visible node.
                block.Visibility = System.Windows.Visibility.Hidden; // until layout is done
            }
            
            // update call state to the newly visible node.
            block.SetCalls(node.Calls, node.Elapsed, batch);

            return block;
        }

        internal void BindView()
        {
            bool wasEmpty = this.Children.Count == 0;

            if (focus != null)
            {
                CodeBlock block = BindNode(focus);

                // make sure all nodes directly connected to the focus are visible.
                foreach (CallGraphLink link in focus.OutgoingLinks)
                {
                    BindNode(link.Target);
                }
                foreach (CallGraphLink link in focus.IncomingLinks)
                {
                    BindNode(link.Source);
                }
            }
        
            // Make sure all visible nodes that have incoming or outgoing data provide [+] buttons to expand that part of the graph.

            foreach (CallGraphNode node in graph.Nodes)
            {
                CodeBlock block = node.State as CodeBlock;
                if (block != null)
                {
                    bool hasMoreOutgoing = false;
                    foreach (CallGraphLink link in node.OutgoingLinks)
                    {
                        if (link.Target.State == null)
                        {
                            hasMoreOutgoing = true;
                            break;
                        }
                    }
                    Button showOutGoing = block.ShowOutgoingExpander(hasMoreOutgoing);
                    if (showOutGoing != null)
                    {
                        showOutGoing.Tag = node;
                        showOutGoing.Click -= new RoutedEventHandler(ShowOutgoing);
                        showOutGoing.Click += new RoutedEventHandler(ShowOutgoing);
                    }

                    bool hasMoreIncoming = false;
                    foreach (CallGraphLink link in node.IncomingLinks)
                    {
                        if (link.Source.State == null)
                        {
                            hasMoreIncoming = true;
                            break;
                        }
                    }

                    Button showIncoming = block.ShowIncomingExpander(hasMoreIncoming);
                    if (showIncoming != null)
                    {
                        showIncoming.Tag = node;
                        showIncoming.Click -= new RoutedEventHandler(ShowIncoming);
                        showIncoming.Click += new RoutedEventHandler(ShowIncoming);

                    }
                }
            }

            // Make sure all links connecting visible nodes are created
            foreach (CallGraphLink link in graph.Links)
            {
                if (link.State == null && link.Source.State != null && link.Target.State != null)
                {
                    if (link.State == null)
                    {
                        BindLink(link);
                    }
                }
            }

            bool isEmpty = this.Children.Count == 0;
            if (wasEmpty != isEmpty)
            {
                OnEmptyChanged();
            }
        }

        public void CollapseAll()
        {
            this.Children.Clear();

            // start over.
            foreach (CallGraphNode node in graph.Nodes)
            {
                node.State = null;
            }
            foreach (CallGraphLink link in graph.Links)
            {
                link.State = null;
            }

            BindView();
            StartLayout();
        }

        public void ExpandAll()
        {
            int count = graph.Nodes.Count();
            int linkCount = graph.Links.Count();
            if (count > 100 || linkCount > 200)
            {
                if (MessageBox.Show(string.Format(SoftwareTrails.Properties.Resources.LargeGraphWarning, count, linkCount), 
                    SoftwareTrails.Properties.Resources.LargeGraphWarningCaption, MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            foreach (CallGraphNode node in graph.Nodes)
            {
                BindNode(node);
            }

            // now create the matching links and update + button states
            BindView();
            StartLayout();
        }

        public void ExpandHotPath()
        {
            // the quick brown fox.

            HashSet<CallGraphNode> hotNodes = new HashSet<CallGraphNode>(graph.GetHotPath());

            HashSet<CallGraphNode> connectors = new HashSet<CallGraphNode>();

            foreach (CallGraphNode n in graph.Nodes)
            {
                if (n.State != null)
                {
                    connectors.UnionWith(graph.FindConnections(n, hotNodes));
                }
            }

            // now make the hot nodes and any connector nodes visible.
            foreach (CallGraphNode node in hotNodes.Concat(connectors))
            {
                BindNode(node);
            }

            // now create the matching links and update + button states
            BindView();
            StartLayout();
        }

        void ShowIncoming(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            CallGraphNode node = button.Tag as CallGraphNode;
            if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl))
            {
                ShowAllIncoming(node);
            }
            else
            {
                foreach (CallGraphLink link in node.IncomingLinks)
                {
                    BindNode(link.Source);
                }
                BindView();
                StartLayout();
            }
        }

        Tuple<int, int> CountNewNodesAndLInks(IEnumerable<CallGraphNode> list)
        {
            int nodes = 0;
            int links = 0;
            foreach (CallGraphNode node in list)
            {
                if (node.State == null)
                {
                    nodes++;
                    links += node.IncomingLinks.Count() + node.OutgoingLinks.Count();
                }
            }
            return new Tuple<int, int>(nodes, links);
        }

        private bool BindNewNodes(HashSet<CallGraphNode> found)
        {

            Tuple<int, int> pair = CountNewNodesAndLInks(found);
            if (pair.Item1 > 100 || pair.Item2 > 100)
            {
                if (MessageBox.Show(string.Format(SoftwareTrails.Properties.Resources.LargeGraphWarning, pair.Item1, pair.Item2),
                       SoftwareTrails.Properties.Resources.LargeGraphWarningCaption, MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
                {
                    return false;
                }
            }
            foreach (CallGraphNode n in found)
            {
                BindNode(n);
            }
           
            BindView();
            StartLayout();
           
            return true;
        }

        bool ShowAllIncoming(CallGraphNode node) 
        {
            var found = new HashSet<CallGraphNode>();
            GetAllIncoming(node, found);
            return BindNewNodes(found);
        }

        void GetAllIncoming(CallGraphNode node, HashSet<CallGraphNode> visited)
        {
            visited.Add(node);
            foreach (CallGraphLink link in node.IncomingLinks)
            {                
                CallGraphNode source = link.Source;
                if (!visited.Contains(source))
                {
                    GetAllIncoming(source, visited);
                }
            }
        }

        void ShowAllIncoming(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            CallGraphNode node = button.Tag as CallGraphNode;
            ShowAllIncoming(node);
        }

        void ShowOutgoing(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            CallGraphNode node = button.Tag as CallGraphNode;

            if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl))
            {
                ShowAllOutgoing(node);
            }
            else
            {
                foreach (CallGraphLink link in node.OutgoingLinks)
                {
                    BindNode(link.Target);
                }
                BindView();
                StartLayout();
            }
        }

        void GetAllOutgoing(CallGraphNode node, HashSet<CallGraphNode> visited)
        {
            visited.Add(node);
            foreach (CallGraphLink link in node.OutgoingLinks)
            {
                CallGraphNode target = link.Target;
                if (!visited.Contains(target))
                {
                    GetAllOutgoing(target, visited);
                }
            }
        }

        bool ShowAllOutgoing(CallGraphNode node)
        {
            var found = new HashSet<CallGraphNode>();
            GetAllOutgoing(node, found);
            return BindNewNodes(found);
        }

        void ShowAllOutgoing(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            CallGraphNode node = button.Tag as CallGraphNode;
            ShowAllOutgoing(node);
        }

        private void BindLink(CallGraphLink link)
        {
            layoutDirty = true;
            Line line = new Line() { Stroke = Brushes.Navy, StrokeThickness = 1 };
            line.Tag = link;
            this.Children.Insert(0, line); // put links behind the nodes
            link.State = line;
            line.Visibility = System.Windows.Visibility.Hidden; // until layout is done.
        }

    }

    class TreeLayout
    {
        GeometryGraph g = new GeometryGraph();
        Size size;
        Microsoft.Msagl.Core.CancelToken cancel;
        CallGraph graph;

        public double Margin = 10;

        public double Width { get { return size.Width; } }

        public double Height { get { return size.Height; } }

        public event EventHandler LayoutFinished;

        private Dispatcher dispatcher;

        public void BeginLayoutGraph(CallGraph graph)
        {
            dispatcher = Application.Current.MainWindow.Dispatcher;

            this.graph = graph;

            foreach (CallGraphNode node in graph.Nodes)
            {
                CodeBlock block = (CodeBlock)node.State;
                if (block != null)
                {
                    block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Size size = block.DesiredSize;
                    var layoutNode = new Node(Microsoft.Msagl.Core.Geometry.Curves.CurveFactory.CreateRectangle(
                        new Microsoft.Msagl.Core.Geometry.Rectangle(new Microsoft.Msagl.Core.Geometry.Point(0, 0),
                             new Microsoft.Msagl.Core.Geometry.Point(size.Width, size.Height))));
                    node.LayoutObject = layoutNode;
                    g.Nodes.Add(layoutNode);
                }
            }

            foreach (CallGraphLink link in graph.Links)
            {
                if (link.State != null)
                {
                    g.Edges.Add(new Edge((Node)link.Source.LayoutObject, (Node)link.Target.LayoutObject));
                }
            }
            cancel = new Microsoft.Msagl.Core.CancelToken();

            // Run it on a background thread.
            Task.Factory.StartNew(RunLayout, cancel.CancellationToken);
        }

        public void StopLayout()
        {
            cancel.Canceled = true;
        }

        void RunLayout()
        {
            try
            {
                var settings = new SugiyamaLayoutSettings();
                settings.LayerSeparation = 20;
                settings.NodeSeparation = 20;
                // We want a bottom-to-top layout so that the top of the stack floats to the top
                // and this transformation does that.
                settings.Transformation = new Microsoft.Msagl.Core.Geometry.Curves.PlaneTransformation(
                    1, 0, 0,
                    0, -1, 0);
                settings.EdgeRoutingSettings.EdgeRoutingMode = Microsoft.Msagl.Core.Routing.EdgeRoutingMode.StraightLine;
                
                Microsoft.Msagl.Layout.Layered.LayeredLayout layered = new LayeredLayout(g, settings);
                layered.Run(cancel);

                // But copying layout back to UI has to be done on UI thread.
                dispatcher.BeginInvoke(new Action(FinishLayout));
            }
            catch
            {
                // probably cancelled, but still have to raise the event
                dispatcher.BeginInvoke(new Action(OnLayoutFinished));
            }

        }


        void FinishLayout()
        {
            var rect = g.BoundingBox;

            // msagl coordinates are upside down, so we have to flip the Y coordinates to bring graph back to the origin.
            var xTranslate = -rect.Left;
            var yTranslate = -rect.Bottom;
            var height = rect.Height;

            double maxx = 0;
            double maxy = 0;
            double plusButtonHeight = 16;

            foreach (CallGraphNode node in graph.Nodes)
            {
                CodeBlock block = (CodeBlock)node.State;
                if (block != null)
                {
                    Node layoutNode = (Node)node.LayoutObject;
                    var box = layoutNode.BoundingBox;
                    Size blockSize = block.DesiredSize;
                    double y = height - (box.Bottom + yTranslate) - blockSize.Height + Margin + plusButtonHeight;
                    double x = box.Left + xTranslate + Margin;
                    Canvas.SetTop(block, y);
                    Canvas.SetLeft(block, x);

                    maxx = Math.Max(maxx, x + blockSize.Width);
                    maxy = Math.Max(maxy, y + blockSize.Height);
                    block.Visibility = Visibility.Visible;

                    // again, just in case the number changed during layout.
                    block.SetCalls(node.Calls, node.Elapsed, 0);
                }
            }

            this.size = new Size(maxx + 2 * Margin, maxy + 2 * Margin + plusButtonHeight);

            foreach (CallGraphLink link in graph.Links)
            {
                Line line = (Line)link.State;
                if (line != null)
                {
                    CodeBlock sourceBlock = (CodeBlock)link.Source.State;
                    Rect sourceBounds = new Rect(new Point(Canvas.GetLeft(sourceBlock), Canvas.GetTop(sourceBlock)), sourceBlock.DesiredSize);
                    CodeBlock targetBlock = (CodeBlock)link.Target.State;
                    Rect targetBounds = new Rect(new Point(Canvas.GetLeft(targetBlock), Canvas.GetTop(targetBlock)), targetBlock.DesiredSize);

                    Point sourceCenter = sourceBounds.Center();
                    Point targetCenter = targetBounds.Center();

                    line.X1 = sourceCenter.X;
                    line.Y1 = sourceCenter.Y;
                    line.X2 = targetCenter.X;
                    line.Y2 = targetCenter.Y;
                    line.Visibility = Visibility.Visible;
                }
            }

            OnLayoutFinished();
        }

        private void OnLayoutFinished()
        {
            if (LayoutFinished != null)
            {
                LayoutFinished(this, EventArgs.Empty);
            }
        }

    }

}

