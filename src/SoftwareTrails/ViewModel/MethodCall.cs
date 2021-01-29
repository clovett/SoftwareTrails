using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftwareTrails
{
    /// <summary>
    /// This class represents the history of a method given call, when it happened
    /// and total elapsed time inside the method.
    /// </summary>
    public class CallHistory
    {
        public MethodCall Method { get; set; }
        public long Timestamp { get; set; }
        public int Elapsed { get; set; }

        // So we can create very light weight linked list of these objects.
        public CallHistory Next { get; set; } 
    }

    /// <summary>
    /// This class represents a single method, and is immutable so it can be shared
    /// for every call to this particular method.
    /// </summary>
    public class MethodCall
    {
        private long id;
        private string fullName;
        private string name;
        private string nspace;
        private string type;
        private bool isMethod;

        public MethodCall(long id, string fullName, bool isMethodCall)
        {
            this.id = id;
            this.fullName = fullName;
            this.isMethod = isMethodCall;

            string label = fullName;
            int i = fullName.LastIndexOf('.');
            if (i >= 0)
            {
                this.type = fullName.Substring(0, i);
                this.name = fullName.Substring(i + 1);

                i = type.LastIndexOf('.');
                if (i >= 0)
                {
                    this.nspace = type.Substring(0, i);
                    this.type = type.Substring(i + 1);
                }
                else if (isMethodCall)
                {
                    this.nspace = "::"; // global class.
                    this.fullName = this.nspace + "." + this.fullName;
                }
            }
            else
            {
                this.name = label;
            }
        }

        public bool IsMethod 
        { 
            get { return isMethod; } 
        }

        public long Id
        {
            get { return this.id; }
        }

        public string FullName
        {
            get { return this.fullName; }
        }

        public string Name
        {
            get { return name; }
        }

        public string Type
        {
            get { return type; }

        }
        public string Namespace
        {
            get { return nspace; }
        }

        internal bool Matches(MethodCall methodCall)
        {
            return this.Id == methodCall.Id;
        }

    }
}
