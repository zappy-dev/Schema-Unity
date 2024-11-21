using System;

namespace Schema.Core
{
    public struct SchemaResult
    {
        public enum RequestStatus
        {
            Passed,
            Failed,
        }
        
        private RequestStatus status;
        public RequestStatus Status => status;
        private string message;
        public string Message => message;
        private string context;
        public string Context => context;
        public bool Passed => status == RequestStatus.Passed;
        public bool Failed => status == RequestStatus.Failed;

        public SchemaResult(RequestStatus status, string message, string context = null)
        {
            this.status = status;
            this.message = message;
            this.context = context;;

            string logMsg = $"[Context={context}] {message}";
            if (status == RequestStatus.Passed)
            {
                Logger.LogVerbose(logMsg);
            }
            else
            {
                if (Logger.Level <= Logger.LogLevel.VERBOSE)
                {
                    Logger.LogError(logMsg);
                }
            }
        }
        
        public override string ToString()
        {
            return $"SchemaResponse[status={status}, message={message}]";
        }
    
        public static SchemaResult Fail(string errorMessage, object context = null) => 
            new SchemaResult(status: RequestStatus.Failed, message: errorMessage, context: context?.ToString());

        public static SchemaResult Pass(string successMessage, object context = null) =>
            new SchemaResult(status: RequestStatus.Passed, message: successMessage, context: context?.ToString());

        public static SchemaResult CheckIf(bool conditional, string errorMessage, object context = null, string successMessage = null)
        {
            return !conditional ? Fail(errorMessage, context: context) : Pass(successMessage: successMessage, context: context);
        }
    }

    public struct SchemaResult<TResult>
    {
        private SchemaResult.RequestStatus status;
        public SchemaResult.RequestStatus Status => status;
        private TResult result;
        public TResult Result
        {
            get
            {
                if (status == SchemaResult.RequestStatus.Failed)
                {
                    throw new InvalidOperationException($"The request status {status} is not supported.");
                }
                
                return result;
            }
        }

        private string context;
        public string Context => context;
        private string message;
        public string Message => message;
        public bool Passed => status == SchemaResult.RequestStatus.Passed;
        public bool Failed => status == SchemaResult.RequestStatus.Failed;

        public SchemaResult(SchemaResult.RequestStatus status, TResult result, string message, string context = null)
        {
            this.status = status;
            this.result = result;
            this.message = message;
            this.context = context;;

            string logMsg = $"[Context={context}] {message}";
            if (status == SchemaResult.RequestStatus.Passed)
            {
                Logger.LogVerbose(logMsg);
            }
            else
            {
                Logger.LogError(logMsg);
            }
        }
        
        public override string ToString()
        {
            return $"SchemaResponse[status={status}, Message={message}]";
        }
    
        public static SchemaResult<TResult> Fail(string errorMessage, object context) => 
            new SchemaResult<TResult>(status: SchemaResult.RequestStatus.Failed, message: errorMessage, result: default, context: context?.ToString());

        public static SchemaResult<TResult> Pass(TResult result, string successMessage, object context) =>
            new SchemaResult<TResult>(status: SchemaResult.RequestStatus.Passed, message: successMessage, result: result, context: context?.ToString());
        
        public static SchemaResult<TResult> CheckIf(bool conditional, TResult result, string errorMessage, string successMessage, object context = null)
        {
            return conditional ? Pass(result: result, successMessage: successMessage, context: context) : Fail(errorMessage: errorMessage, context: context);
        }

        public bool Try(out TResult result)
        {
            result = this.result;
            return Passed;
        }
    }
}