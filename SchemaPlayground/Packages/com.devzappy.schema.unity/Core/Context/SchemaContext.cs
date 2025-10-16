using System;
using System.Text;
using Schema.Core.Data;

namespace Schema.Core
{
    public enum SchemaRuntimeType
    {
        UNSET = 0,
        EDITOR,
        RUNTIME
    }
    
    public class SchemaContext : IEquatable<SchemaContext>, ICloneable
    {
        #region Constants
        
        public static SchemaContext EditContext = new SchemaContext
        {
            RuntimeType = SchemaRuntimeType.EDITOR
        };
        
        public static SchemaContext RuntimeContext = new SchemaContext
        {
            RuntimeType = SchemaRuntimeType.RUNTIME
        };

        #endregion
        
        #region Fields and Properties
        public SchemaRuntimeType RuntimeType { get; set; }
        
        private SchemaProjectContainer _project;

        public SchemaProjectContainer Project
        {
            get => _project ?? Schema.LatestProject;
            set => _project = value;
        }

        public DataScheme Scheme { get; set; }
        public string AttributeName { get; set; }
        public DataType DataType { get; set; }
        public string Driver;
        public DataEntry Entry { get; internal set; }

        #endregion

        public SchemaContext()
        {
            
        }
        

        public bool IsEmpty => string.IsNullOrEmpty(AttributeName) && 
                               DataType == null && 
                               string.IsNullOrEmpty(Driver) && 
                               Scheme == null && 
                               Entry == null &&
                               Project == null;

        public override string ToString()
        {
            var sb = new StringBuilder();
            int rightPad = 15;
            if (!string.IsNullOrEmpty(Driver))
            {
                sb.Append($"- Driver:".PadRight(rightPad));
                sb.AppendLine(Driver);
            }
            if (RuntimeType != SchemaRuntimeType.UNSET)
            {
                sb.Append($"- RuntimeType:".PadRight(rightPad));
                sb.AppendLine(RuntimeType.ToString());
            }
            if (Project != null)
            {
                sb.Append($"- Project:".PadRight(rightPad));
                sb.AppendLine(Project.ToString());
            }
            if (Scheme != null)
            {
                sb.Append($"- Scheme:".PadRight(rightPad));
                sb.AppendLine(Scheme.ToString());
            }
            if (!string.IsNullOrEmpty(AttributeName))
            {
                sb.Append($"- AttributeName:".PadRight(rightPad));
                sb.AppendLine(AttributeName);
            }
            if (DataType != null)
            {
                sb.Append($"- DataType:".PadRight(rightPad));
                sb.AppendLine(DataType.ToString());
            }
            if (Entry != null)
            {
                sb.Append($"- Entry:".PadRight(rightPad));
                sb.AppendLine(Entry.ToString());
            }

            return sb.ToString();
        }

        public object Clone()
        {
            return new SchemaContext
            {
                RuntimeType = RuntimeType,
                Project = Project,
                Scheme = Scheme,
                AttributeName = AttributeName,
                DataType = DataType,
                Entry = Entry,
                Driver = Driver
            };
        }

        public static SchemaContext Merge(SchemaContext ctxA, SchemaContext ctxB)
        {
            return new SchemaContext
            {
                Driver = ctxA.Driver ?? ctxB.Driver,
                RuntimeType = ctxA.RuntimeType,
                Project = ctxA._project ?? ctxB._project,
                Scheme = ctxA.Scheme ?? ctxB.Scheme,
                Entry = ctxA.Entry ?? ctxB.Entry,
                DataType = ctxA.DataType ?? ctxB.DataType,
                AttributeName = (string.IsNullOrEmpty(ctxA.AttributeName) ? ctxB.AttributeName : ctxA.AttributeName),
            };
        }
        
        public static SchemaContext operator | (SchemaContext left, SchemaContext right)
        {
            return Merge(left, right);
        }

        public bool Equals(SchemaContext other)
        {
            if (!Equals(Scheme, other.Scheme)) return false;
            if (!Equals(Project, other.Project)) return false;
            if (!Equals(DataType, other.DataType)) return false;
            if (!Equals(Entry, other.Entry)) return false;
            if (!Equals(AttributeName, other.AttributeName)) return false;
            if (!Equals(Driver, other.Driver)) return false;
            if (!Equals(RuntimeType, other.RuntimeType)) return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is SchemaContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Scheme != null ? Scheme.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Entry != null ? Entry.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (AttributeName != null ? AttributeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DataType != null ? DataType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Driver != null ? Driver.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Project != null ? Project.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RuntimeType != null ? RuntimeType.GetHashCode() : 0);
                return hashCode;
            }
        }

        public SchemaContext WithDriver(string driver)
        {
            return this | new SchemaContext
            {
                Driver = driver,
            };
        }

        public SchemaContext WithDataType(DataType dataType)
        {
            return this | new SchemaContext
            {
                DataType = dataType,
            };
        }
    }
}