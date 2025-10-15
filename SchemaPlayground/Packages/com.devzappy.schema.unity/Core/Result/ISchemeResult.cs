using System;

namespace Schema.Core
{
    public interface ISchemeResult
    {
        public RequestStatus Status { get; }
        public object Context { get; }
        public string Message { get; }
    }
}