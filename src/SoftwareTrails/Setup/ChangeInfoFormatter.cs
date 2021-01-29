using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Xml.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace SoftwareTrails
{
    internal class ChangeInfoFormatter
    {
        string previousVersion;
        XDocument doc;
        FlowDocument flow;

        public ChangeInfoFormatter(string previousVersion, XDocument doc)
        {
            this.previousVersion = previousVersion;
            this.doc = doc;
        }

        /// <summary>
        /// return true if the given 'version' is the same or older than the 'previousVersion'
        /// </summary>
        /// <param name="previousVersion"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool IsSameOrOlder(string previousVersion, string version)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(previousVersion))
                {
                    return false;
                }
                Version v1 = Version.Parse(version);
                Version v2 = Version.Parse(previousVersion);
                if (v1 <= v2)
                {
                    return true;
                }
            }
            catch
            {
                return version == previousVersion;
            }
            return false;
        }

        public bool HasLatestVersion()
        {
            if (doc == null) return true;
            foreach (XElement change in doc.Root.Elements("change"))
            {
                string version = (string)change.Attribute("version");
                if (version == previousVersion)
                {
                    return true;
                }
                return IsSameOrOlder(previousVersion, version);
            }
            return false;
        }

        public void Generate(FlowDocument flowDocument) 
        {
            if (doc == null) return;

            this.flow = flowDocument;

            bool found = false;
            bool first = true;
           
            var style = new Style(typeof(Paragraph));
                
            foreach (XElement change in doc.Root.Elements("change"))
            {
                string version = (string)change.Attribute("version");
                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                bool match = IsSameOrOlder(previousVersion, version);
                
                if (!found && match)
                {
                    WriteHeading("The following changes were already installed");
                    found = true;
                }

                if (first && !found)
                {
                    WriteHeading("The following changes were just downloaded");
                    first = false;
                }

                string date = (string)change.Attribute("date");

                WriteSubHeading(version + "    " + date);
                
                foreach (string line in change.Value.Split('\r', '\n'))
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) 
                    {
                        Paragraph p = WriteParagraph(trimmed, FontStyles.Normal, FontWeights.Normal, found ? Brushes.Gray : Brushes.Black, 11);
                        p.TextIndent = 20;
                        p.Margin = new Thickness(0);
                        p.Padding = new Thickness(0);
                    }
                }
            }
        }

        Section section;
        Paragraph paragraph;

        private void WriteHeading(string title)
        {
            this.section = new Section();
            flow.Blocks.Add(section);
            WriteParagraph(title);
            paragraph.Style = flow.Resources["HeadingStyle"] as Style;
        }

        private void WriteSubHeading(string subHeading)
        {
            this.section = new Section();
            flow.Blocks.Add(section);
            WriteParagraph(subHeading);
            paragraph.Style = flow.Resources["SubHeadingStyle"] as Style;
        }

        private Paragraph WriteParagraph(string text)
        {
            return WriteParagraph(text, FontStyles.Normal, FontWeights.Normal, null, null);
        }

        private Paragraph WriteParagraph(string text, FontStyle style, FontWeight weight, SolidColorBrush foreground, double? fontSize)
        {
            paragraph = new Paragraph();
            Run run = new Run();
            run.Text = text;
            if (style != FontStyles.Normal)
            {
                run.FontStyle = style;
            }
            if (weight != FontWeights.Normal)
            {
                run.FontWeight = weight;
            }
            if (fontSize.HasValue)
            {
                run.FontSize = fontSize.Value;
            }
            if (foreground != null)
            {
                run.Foreground = foreground;
            }
            paragraph.Inlines.Add(run);

            if (this.section == null)
            {
                this.section = new Section();
                flow.Blocks.Add(section);
            }

            section.Blocks.Add(paragraph);

            return paragraph;
        }
    }
}
