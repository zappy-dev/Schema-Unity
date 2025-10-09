using System;
using System.Collections.Generic;
using System.Text;
using Schema.Core.Data;

namespace Schema.Core
{
    public static class DataSchemeTablePrinter
    {
        public static void PrintTableView(this DataScheme scheme, StringBuilder tableSB)
        {
            const string ID_SUFFIX = " - ID";
            var maxWidth = new Dictionary<string, int>();
            foreach (var attribute in scheme.GetAttributes())
            {
                var maxColumnHeaderLength =
                    Math.Max(attribute.AttributeName.Length, attribute.DataType.TypeName.Length);
                maxWidth[attribute.AttributeName] = maxColumnHeaderLength + (attribute.IsIdentifier ? ID_SUFFIX.Length : 0);
            }
            
            // pad entries
            foreach (var entry in  scheme.AllEntries)
            {
                foreach (var attribute in scheme.GetAttributes())
                {
                    int newMaxWidth = entry.GetDataAsString(attribute.AttributeName).Length;
                    if (maxWidth.TryGetValue(attribute.AttributeName, out var value))
                    {
                        newMaxWidth = (value > newMaxWidth) ? value :  newMaxWidth;
                    }

                    maxWidth[attribute.AttributeName] = newMaxWidth;
                }
            }

            // table header, top
            int numAttrs = scheme.AttributeCount;

            int attrIdx = 0;
            
            void PrintTableRow(char start, char end, char bridge, char gap)
            {
                attrIdx = 0;
                tableSB.Append(start);
                tableSB.Append(gap);
                foreach (var attribute in scheme.GetAttributes())
                {
                    tableSB.Append(string.Empty.PadRight(maxWidth[attribute.AttributeName], gap));
                    if (++attrIdx < numAttrs)
                    {
                        tableSB.Append(gap);
                        tableSB.Append(bridge);
                        tableSB.Append(gap);
                    }
                }
                tableSB.Append(gap);
                tableSB.AppendLine(end.ToString());
            }

            PrintTableRow('┌', '┐', '┬', '─');
            
            // table header, attributes
            tableSB.Append("│ ");
            attrIdx = 0;
            foreach (var attribute in 
                     scheme.GetAttributes())
            {
                var attrDisplayName = attribute.AttributeName + (attribute.IsIdentifier ? ID_SUFFIX : "");
                tableSB.Append(attrDisplayName.PadRight(maxWidth[attribute.AttributeName]));
                if (++attrIdx < numAttrs) tableSB.Append(" │ ");
            }
            tableSB.AppendLine(" │");
            
            // table header, attributes - data type
            tableSB.Append("│ ");
            attrIdx = 0;
            foreach (var attribute in 
                     scheme.GetAttributes())
            {
                var attrDisplayName = attribute.DataType.TypeName;
                tableSB.Append(attrDisplayName.PadRight(maxWidth[attribute.AttributeName]));
                if (++attrIdx < numAttrs) tableSB.Append(" │ ");
            }
            tableSB.AppendLine(" │");
            
            // table header, break
            PrintTableRow('├', '┤', '┼', '─');

            // table entries
            foreach (var entry in scheme.AllEntries)
            {
                tableSB.Append("│ ");
                attrIdx = 0;
                foreach (var attribute in scheme.GetAttributes())
                {
                    tableSB.Append(entry.GetDataAsString(attribute.AttributeName).PadRight(maxWidth[attribute.AttributeName]));
                    if (++attrIdx < numAttrs) tableSB.Append(" │ ");
                }

                tableSB.AppendLine(" │");
            }
            
            // footer
            PrintTableRow('└', '┘', '┴', '─');
        }

        public static StringBuilder PrintTableView(this DataScheme scheme)
        {
           var tableSB = new StringBuilder();
           scheme.PrintTableView(tableSB);
            return tableSB;
        }
    }
}
