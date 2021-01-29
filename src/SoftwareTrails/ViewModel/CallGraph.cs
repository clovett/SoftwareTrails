using System.Collections.Generic;
using System.Collections.Concurrent;
using System;
using System.Diagnostics;
using System.Xml.Linq;
using System.Linq;
using SoftwareTrails.Utilities;

namespace SoftwareTrails
{

    // This is a multi-thread read safe view model - it is built and updated on
    // a background thread, then laid out on another thread and rendered on the UI thread.
    public class CallGraph
    {
        ConcurrentDictionary<long, CallGraphNode> nodes;
        ConcurrentDictionary<CallGraphLinkKey, CallGraphLink> links;

        public CallGraph()
        {
            Clear();
        }

        public void Clear()
        {
            nodes = new ConcurrentDictionary<long, CallGraphNode>();
            links = new ConcurrentDictionary<CallGraphLinkKey, CallGraphLink>();
        }

        public CallGraphNode GetOrCreateNode(MethodCall call)
        {
            CallGraphNode node = null;
            long key = call.Id;
            if (!nodes.TryGetValue(key, out node))
            {
                node = new CallGraphNode(call);
                nodes[key] = node;
            }
            return node;
        }

        public CallGraphLink GetOrCreateLink(CallGraphNode source, CallGraphNode target)
        {
            CallGraphLink link = null;
            var key = new CallGraphLinkKey(source, target);
            if (!links.TryGetValue(key, out link))
            {
                link = new CallGraphLink(source, target);
                links[key] = link;
            }
            return link;
        }

        public IEnumerable<CallGraphNode> Nodes
        {
            get { return nodes.Values; }
        }

        public IEnumerable<CallGraphLink> Links
        {
            get { return links.Values; }
        }

        public bool IsEmpty
        {
            get { return nodes.Count == 0; }
        }

        /// <summary>
        /// Return the next most busy nodes that are not already visible in the UI.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<CallGraphNode> GetHotPath()
        {
            List<double> times = new List<double>();
            List<double> calls = new List<double>();

            // find top 10% and mark them as hot.
            foreach (CallGraphNode cgn in this.nodes.Values)
            {
                if (cgn.State == null)
                {
                    times.Add(cgn.Elapsed);
                    calls.Add(cgn.Calls);
                }
            }
            
            // anything more than 3 times standard deviation is slow.
            double slow = MathUtilities.StandardDeviation(times) * 3;

            // anything more than 3 times standard deviation is too busy
            double busy = MathUtilities.StandardDeviation(calls) * 3;

            foreach (CallGraphNode cgn in this.nodes.Values)
            {
                if (cgn.State == null)
                {
                    if (cgn.Elapsed > slow || cgn.Calls > busy)
                    {
                        yield return cgn;
                    }
                }
            }
        }

        /// <summary>
        /// Find all nodes that connect the given source node to any of the nodes in the given set.
        /// </summary>
        /// <param name="source">The source node we are trying to connect</param>
        /// <param name="toConnect">The set of nodes we are trying to connect to</param>
        internal IEnumerable<CallGraphNode> FindConnections(CallGraphNode source, HashSet<CallGraphNode> toConnect)
        {
            HashSet<CallGraphNode> connectingNodes = new HashSet<CallGraphNode>();

            Stack<CallGraphNode> path = new Stack<CallGraphNode>();
            TraverseOutgoingConnections(source, 
                (n) => {
                    bool isConnected = toConnect.Contains(n);
                    if (isConnected)
                    {
                        // add the path that connects source to target
                        foreach (CallGraphNode connector in path)
                        {
                            connectingNodes.Add(connector);
                        }
                    }
                    path.Push(n);
                },
                (n) => {
                    path.Pop();
                }, 
                new HashSet<CallGraphNode>());

            return connectingNodes;
        }

        internal bool TraverseOutgoingConnections(CallGraphNode node, Action<CallGraphNode> preTraversal, Action<CallGraphNode> postTraversal, HashSet<CallGraphNode> visited)
        {
            bool result = false;
            foreach (CallGraphLink link in node.OutgoingLinks)
            {
                var target = link.Target;
                if (!visited.Contains(target))
                {
                    visited.Add(target);
                    if (preTraversal != null) preTraversal(target);
                    TraverseOutgoingConnections(target, preTraversal, postTraversal, visited);
                    if (postTraversal != null) postTraversal(target);
                }
            }
            return result;
        }

        internal bool TraverseIncomingConnections(CallGraphNode node, Action<CallGraphNode> preTraversal, Action<CallGraphNode> postTraversal, HashSet<CallGraphNode> visited)
        {
            bool result = false;
            foreach (CallGraphLink link in node.IncomingLinks)
            {
                var target = link.Target;
                if (!visited.Contains(target))
                {
                    visited.Add(target);
                    if (preTraversal != null) preTraversal(target);
                    TraverseIncomingConnections(target, preTraversal, postTraversal, visited);
                    if (postTraversal != null) postTraversal(target);
                }
            }
            return result;
        }


        Dictionary<string, XElement> methodMap = new Dictionary<string, XElement>();
        Dictionary<string, string> methodIdMap = new Dictionary<string, string>();


        internal void Save(string temp)
        {
            // Load the template
            XDocument doc = XDocument.Parse(@"<DirectedGraph GraphDirection='BottomToTop' Layout='Sugiyama' xmlns='http://schemas.microsoft.com/vs/2009/dgml'>
  <Categories>
    <Category Id='Slow' Label='Slow'/>
    <Category Id='Busy' Label='Busy' />
  </Categories>
  <Styles>
    <Style TargetType='Node' GroupLabel='Slow' ValueLabel='True'>
      <Condition Expression='HasCategory(&apos;Slow&apos;)' />
      <Setter Property='Background' Value='#80D03030' />
    </Style>
    <Style TargetType='Node' GroupLabel='Busy' ValueLabel='True'>
      <Condition Expression='HasCategory(&apos;Busy&apos;)' />
      <Setter Property='Background' Value='#803030D0' />
    </Style>
  </Styles>
</DirectedGraph>");

            XElement root = doc.Root;
            XNamespace dgmlNs = root.Name.Namespace;

            XElement links = new XElement(dgmlNs + "Links");
            root.AddFirst(links);
            XElement nodes = new XElement(dgmlNs + "Nodes");
            root.AddFirst(nodes);

            this.methodMap = new Dictionary<string, XElement>();
            this.methodIdMap = new Dictionary<string, string>();

            List<double> times = new List<double>();
            List<double> calls = new List<double>();

            // find top 10% and mark them as hot.
            foreach (CallGraphNode cgn in this.nodes.Values)
            {
                if (cgn.State != null)
                {
                    times.Add(cgn.Elapsed);
                    calls.Add(cgn.Calls);
                }
            }

            // anything more than 3 times standard deviation is slow.
            double slow = MathUtilities.StandardDeviation(times) * 3;

            // anything more than 3 times standard deviation is too busy
            double busy = MathUtilities.StandardDeviation(calls) * 3;

            Dictionary<string, XElement> groups = new Dictionary<string, XElement>();

            foreach (CallGraphNode cgn in this.nodes.Values)
            {
                if (cgn.State != null)
                {
                    string ns = cgn.Method.Namespace;
                    string type = cgn.Method.Type;
                    string fullType = type;
                    if (string.IsNullOrEmpty(ns))
                    {
                        ns = "::";
                        fullType = "::" + type;
                    }
                    else
                    {
                        fullType = ns + "." + type;
                    }

                    if (!groups.ContainsKey(fullType))
                    {
                        XElement typeNode = new XElement(dgmlNs + "Node");
                        typeNode.SetAttributeValue("Id", fullType);
                        typeNode.SetAttributeValue("Label", fullType);
                        typeNode.SetAttributeValue("Namespace", ns);
                        typeNode.SetAttributeValue("Group", "Expanded");
                        nodes.Add(typeNode);
                        groups[fullType] = typeNode;
                    }

                    // this is a useless numeric number that is specific to the process instance, so we can't use that in graph diff
                    // so we want a fully qualified type name instead, but method overloading can result in duplicates, so we take care
                    // of that here.
                    string methodTypeId = GetMethodId(cgn.Method);

                    XElement node = new XElement(dgmlNs + "Node");
                    node.SetAttributeValue("Id", methodTypeId);
                    node.SetAttributeValue("Label", cgn.Method.Name);
                    node.SetAttributeValue("Type", cgn.Method.Type);
                    node.SetAttributeValue("Namespace", cgn.Method.Namespace);
                    node.SetAttributeValue("Calls", cgn.Calls.ToString());
                    node.SetAttributeValue("Elapsed", cgn.Elapsed.ToString());
                    methodMap[methodTypeId] = node;
                    bool hascategory = false;
                    if (cgn.Elapsed > slow)
                    {
                        node.SetAttributeValue("Category", "Slow");
                        hascategory = true;
                    }
                    if (cgn.Calls > busy)
                    {
                        if (hascategory)
                        {
                            XElement cref = new XElement(dgmlNs + "Category");
                            cref.SetAttributeValue("Ref", "Busy");
                            node.Add(cref);
                        }
                        else
                        {
                            node.SetAttributeValue("Category", "Busy");
                        }
                    }
                    nodes.Add(node);

                    if (fullType != null)
                    {
                        // put inside type group
                        XElement link = new XElement(dgmlNs + "Link");
                        link.SetAttributeValue("Source", fullType);
                        link.SetAttributeValue("Target", methodTypeId);
                        link.SetAttributeValue("Category", "Contains");
                        links.Add(link);
                    }
                }
            }

            foreach (CallGraphLink cgl in this.links.Values)
            {
                if (cgl.Source.State != null && cgl.Target.State != null)
                {
                    XElement link = new XElement(dgmlNs + "Link");
                    link.SetAttributeValue("Source", GetMethodId(cgl.Source.Method));
                    link.SetAttributeValue("Target", GetMethodId(cgl.Target.Method));
                    links.Add(link);
                }
            }

            doc.Save(temp);
        }

        private string GetMethodId(MethodCall method)
        {
            // see if this is a new method id we have not seen before
            string methodTypeId = null;
            string methodId = method.Id.ToString();
            string baseName = method.Namespace + "." + method.Type + "." + method.Name;
            if (!methodIdMap.TryGetValue(methodId, out methodTypeId))
            {
                int index = 2;
                string newName = baseName;
                while (methodMap.ContainsKey(newName))
                {
                    newName = baseName + index++;
                }
                methodTypeId = newName;
                methodIdMap[methodId] = methodTypeId;
            }

            return methodTypeId;
        }
    }

    struct CallGraphLinkKey : IEquatable<CallGraphLinkKey>
    {
        CallGraphNode source;
        CallGraphNode target;

        public CallGraphLinkKey(CallGraphNode source, CallGraphNode target)
        {
            this.source = source;
            this.target = target;
        }

        public bool Equals(CallGraphLinkKey other)
        {
            return other.source == this.source && other.target == this.target;
        }
    }

    public class CallGraphLink
    {
        CallGraphNode source;
        CallGraphNode target;
        object state;

        public CallGraphLink(CallGraphNode source, CallGraphNode target)
        {
            this.source = source;
            this.target = target;
            source.AddOutgoingLink(this);
            target.AddIncomingLink(this);
        }

        public CallGraphNode Source
        {
            get { return source; }
            set { source = value; }
        }

        public CallGraphNode Target
        {
            get { return target; }
            set { target = value; }
        }

        public object State
        {
            get { return this.state; }
            set { this.state = value; }
        }

    }

    /// <summary>
    /// This is a concurrent safe list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ConcurrentList<T> : IList<T>
    {
        List<T> buffer = new List<T>();

        #region IList 

        public int IndexOf(T item)
        {
            lock (buffer)
            {
                return buffer.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (buffer)
            {
                buffer[index] = item;
            }
        }

        public void RemoveAt(int index)
        {
            lock (buffer)
            {
                buffer.RemoveAt(index);
            }
        }

        public T this[int index]
        {
            get
            {
                lock (buffer)
                {
                    return buffer[index];
                }
            }
            set
            {
                lock(buffer)
                {
                    buffer[index] = value;
                }
            }
        }

        public void Add(T item)
        {
            lock (buffer)
            {
                buffer.Add(item);
            }
        }

        public void Clear()
        {
            lock (buffer)
            {
                buffer.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (buffer)
            {
                return buffer.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (buffer)
            {
                buffer.CopyTo(array, arrayIndex);
            }
        }

        public int Count
        {
            get
            {
                lock (buffer)
                {
                    return buffer.Count;
                }
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            bool rc = false;
            lock (buffer)
            {
                rc = buffer.Remove(item);
            }
            return rc;
        }

        public IEnumerator<T> GetEnumerator()
        {
            // this is horribly inefficient, but it's safe.
            List<T> snapshot = null;
            lock (buffer)
            {
                snapshot = new List<T>(buffer);
                return snapshot.GetEnumerator();
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            // this is horribly inefficient, but it's safe.
            T[] snapshot;
            lock (buffer)
            {
                snapshot = new T[buffer.Count];
                buffer.CopyTo(snapshot, 0);
                return snapshot.GetEnumerator();
            }
        }
        #endregion 

    }

    public class CallGraphNode
    {
        CallGraphNode parent;
        MethodCall method;
        object state;
        ConcurrentList<CallGraphNode> children;
        ConcurrentList<CallGraphLink> incoming;
        ConcurrentList<CallGraphLink> outgoing;

        internal CallGraphNode(MethodCall method)
        {
            this.method = method;
        }

        public MethodCall Method { get { return this.method; } }

        public int Calls { get; set; }
        public long Elapsed { get; set; }

        internal void AddIncomingLink(CallGraphLink link)
        {
            if (incoming == null)
            {
                incoming = new ConcurrentList<CallGraphLink>();
            }
            incoming.Add(link);
        }

        internal void AddOutgoingLink(CallGraphLink link)
        {
            if (outgoing == null)
            {
                outgoing = new ConcurrentList<CallGraphLink>();
            }
            outgoing.Add(link);
        }

        public object State
        {
            get { return this.state; }
            set { this.state = value; }
        }

        public object LayoutObject { get; set; }

        public CallGraphNode Parent
        {
            get { return this.parent; }
            private set { this.parent = value; }
        }

        public void AddChild(CallGraphNode node)
        {
            if (children == null)
            {
                children = new ConcurrentList<CallGraphNode>();
            }
            children.Add(node);
            node.Parent = this;
        }

        static CallGraphNode[] NoChildren = new CallGraphNode[0];

        public IEnumerable<CallGraphNode> Children
        {
            get
            {
                if (children == null)
                {
                    return NoChildren;
                }
                return children;
            }
        }

        static CallGraphLink[] NoLinks = new CallGraphLink[0];

        public IEnumerable<CallGraphLink> IncomingLinks
        {
            get
            {
                if (incoming == null)
                {
                    return NoLinks;
                }
                return incoming;
            }
        }

        public IEnumerable<CallGraphLink> OutgoingLinks
        {
            get
            {
                if (outgoing == null)
                {
                    return NoLinks;
                }
                return outgoing;
            }
        }
    }

}