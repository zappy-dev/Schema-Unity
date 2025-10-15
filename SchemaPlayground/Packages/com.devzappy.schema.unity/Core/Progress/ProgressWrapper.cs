using System;

namespace Schema.Core
{
    public struct ProgressWrapper<T> : IProgress<T>
    {
        private Action<T> progressReporter;
        public ProgressWrapper(Action<T> progressReporter)
        {
            this.progressReporter = progressReporter;
        }
            
        public void Report(T value)
        {
            this.progressReporter(value);
        }
    }
}