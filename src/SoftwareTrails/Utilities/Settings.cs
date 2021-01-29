using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace SoftwareTrails
{
    //==========================================================================================
    /// <summary>
    /// This class encapsulates the bag of settings used by various components in this application
    /// and knows how to serialize and deserialize them between application sessions.
    /// </summary>
    public class Settings : IXmlSerializable
    {
        //
        // This is the global singleton Settings instance used by the everywhere in MyMoney
        // it is initialized by App.Xaml.cs
        //
        public static Settings TheSettings { get; set; }

        #region PROPERTIES

        private object GetValue(string name)
        {
            object result = null;
            map.TryGetValue(name, out result);
            return result;
        }

        public Point WindowLocation
        {
            get
            {
                object value = GetValue("WindowLocation");
                return value is Point ? (Point)value : new Point(0, 0);
            }
            set { map["WindowLocation"] = value; }
        }

        public Size WindowSize
        {
            get
            {
                object value = GetValue("WindowSize");
                return value is Size ? (Size)value : new Size(0, 0);
            }
            set { map["WindowSize"] = value; }
        }

        public string[] TemporaryFiles
        {
            get
            {
                object value = GetValue("TemporaryFiles");
                return (string[])value;
            }
            set { map["TemporaryFiles"] = value; }
        }

        public string ProfilerOutputDirectory
        {
            get { return (string)GetValue("ProfilerOutputDirectory"); }
            set { map["ProfilerOutputDirectory"] = value; }
        }

        public string ProfilerDllPath
        {
            get { return (string)GetValue("ProfilerDllPath"); }
            set { map["ProfilerDllPath"] = value; }
        }

        public string ExeVersion
        {
            get { return (string)GetValue("ExeVersion"); }
            set { map["ExeVersion"] = value; }
        }

        public DateTime? LastExeTimestamp
        {
            get { return (DateTime?)GetValue("LastExeTimestamp"); }
            set { map["LastExeTimestamp"] = value; }
        }

        #endregion

        IDictionary<string, object> map = new SortedDictionary<string, object>();

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public object this[string key]
        {
            get { return GetValue(key);  }
            set { map[key] = value; }
        }

        public void Load(string path)
        {
            try
            {
                using (XmlReader r = XmlReader.Create(path))
                {
                    ReadXml(r);
                }
            }
            catch
            {
                // never mind, we will auto-recover.
            }
        }

        public void Save(string path)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = Encoding.UTF8;
            using (XmlWriter w = XmlWriter.Create(path, settings))
            {
                WriteXml(w);
            }
        }

        public void ReadXml(XmlReader r)
        {
            r.MoveToContent();
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    PropertyInfo pi = this.GetType().GetProperty(r.Name);
                    if (pi != null)
                    {
                        Type t = pi.PropertyType;
                        if (t == typeof(Point))
                        {
                            pi.SetValue(this, DeserializePoint(r), null);
                        }
                        else if (t == typeof(Size))
                        {
                            pi.SetValue(this, DeserializeSize(r), null);
                        }
                        else if (t == typeof(int))
                        {
                            pi.SetValue(this, Int32.Parse(r.ReadString()), null);
                        }
                        else if (t == typeof(string))
                        {
                            pi.SetValue(this, r.ReadString(), null);
                        }
                        else if (t == typeof(DateTime))
                        {
                            pi.SetValue(this, DeserializeDateTime(r), null);
                        }
                        else if (t == typeof(DateTime?))
                        {
                            DateTime? ndt = DeserializeDateTime(r);
                            pi.SetValue(this, ndt, null);
                        }
                        else if (t == typeof(bool))
                        {
                            pi.SetValue(this, bool.Parse(r.ReadString()), null);
                        }
                        else if (t == typeof(string[]))
                        {
                            pi.SetValue(this, ReadStringArray(r), null);
                        }
                        else
                        {
                            throw new Exception("Settings.ReadXml encountered unsupported property type '" + t.FullName + "'");
                        }
                    }
                    else
                    {
                        map[r.Name] = r.ReadString();
                    }
                }
            }
        }

        public static string[] ReadStringArray(XmlReader r)
        {
            List<string> list = new List<string>();
            if (r.IsEmptyElement) return null;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "Item")
                    {
                        list.Add(r.ReadString());
                    }
                }
            }
            return list.ToArray();
        }

        public void WriteXml(XmlWriter w)
        {
            w.WriteStartDocument();
            w.WriteStartElement("Settings");
            foreach (KeyValuePair<string, object> pair in map)
            {
                object value = pair.Value;
                string key =pair.Key;
                if (value == null) continue;

                PropertyInfo pi = this.GetType().GetProperty(key);
                if (pi != null)
                {
                    Type t = pi.PropertyType;
                    if (t == typeof(Point))
                    {
                        w.WriteStartElement(key);
                        SerializePoint(w, (Point)value);
                        w.WriteEndElement();
                    }
                    else if (t == typeof(Size))
                    {
                        SerializeSize(w, key, (Size)value);
                    }
                    else if (t == typeof(int))
                    {
                        w.WriteElementString(key, ((int)value).ToString());
                    }
                    else if (t == typeof(string))
                    {
                        w.WriteElementString(key, ((string)value));
                    }
                    else if (t == typeof(XmlElement))
                    {
                    }
                    else if (t == typeof(DateTime))
                    {
                        SerializeDateTime(w, key, (DateTime)value);
                    }
                    else if (t == typeof(DateTime?))
                    {
                        DateTime? dt = (DateTime?)value;
                        if (dt.HasValue)
                        {
                            SerializeDateTime(w, key, dt.Value);
                        }
                    }
                    else if (t == typeof(bool))
                    {
                        w.WriteElementString(key, ((bool)value).ToString());
                    }
                    else if (t == typeof(string[]))
                    {
                        w.WriteStartElement(key);
                        WriteStringArray(w, (string[])value);
                        w.WriteEndElement();
                    }
                    else
                    {
                        throw new Exception("Settings.ReadXml encountered unsupported property type '" + t.FullName + "'");
                    }
                }
                else if (value is IXmlSerializable)
                {
                    IXmlSerializable s = (IXmlSerializable)value;
                    if (s != null)
                    {
                        w.WriteStartElement(key);
                        s.WriteXml(w);
                        w.WriteEndElement();
                    }
                }
                else
                {
                    w.WriteElementString(key, value.ToString());
                }
            }

            w.WriteEndElement();
            w.WriteEndDocument();
        }



        public static void WriteStringArray(XmlWriter w, string[] query)
        {
            if (query != null)
            {
                foreach (string value in query)
                {
                    w.WriteElementString("Item", value);
                }
            }
        }


        static private Point DeserializePoint(XmlReader r)
        {
            Point p = new Point();
            if (r.IsEmptyElement) return p;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "X")
                    {
                        p.X = Int32.Parse(r.ReadString());
                    }
                    else if (r.Name == "Y")
                    {
                        p.Y = Int32.Parse(r.ReadString());
                    }
                }
            }
            return p;
        }

        static private Size DeserializeSize(XmlReader r)
        {
            Size s = new Size();
            if (r.IsEmptyElement) return s;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "Width")
                    {
                        s.Width = Int32.Parse(r.ReadString());
                    }
                    else if (r.Name == "Height")
                    {
                        s.Height = Int32.Parse(r.ReadString());
                    }
                }
            }
            return s;
        }

        static private DateTime DeserializeDateTime(XmlReader r)
        {
            if (r.IsEmptyElement) return new DateTime();
            if (r.Read())
            {
                string s = r.ReadString();
                return DateTime.Parse(s);
            }
            return new DateTime();
        }

        static private void SerializePoint(XmlWriter w, Point p)
        {
            w.WriteElementString("X", p.X.ToString());
            w.WriteElementString("Y", p.Y.ToString());
        }

        static private void SerializeSize(XmlWriter w, string name, Size s)
        {
            w.WriteStartElement(name);
            w.WriteElementString("Width", s.Width.ToString());
            w.WriteElementString("Height", s.Height.ToString());
            w.WriteEndElement();
        }

        static private void SerializeDateTime(XmlWriter w, string name, DateTime dt)
        {
            w.WriteStartElement(name);
            w.WriteString(dt.ToString());
            w.WriteEndElement();
        }
    }

}
