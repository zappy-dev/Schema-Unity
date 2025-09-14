using System;
using System.Collections.Generic;
using Schema.Core.Logging;

namespace Schema.Core
{
    public enum RequestStatus
    {
        UNSET,
        Passed,
        Failed,
    }
    
    public struct SchemaResult
    {
        public static readonly SchemaResult NoOp = new SchemaResult(status: RequestStatus.Passed, "NoOp");

        /// <summary>
        /// Performs the operation across the given entries under a bulk operation context
        /// </summary>
        /// <param name="entries">Entries to operate on.</param>
        /// <param name="operation">Operation to perform on entries.</param>
        /// <param name="context">Operation context</param>
        /// <typeparam name="T">Type of entry</typeparam>
        /// <returns>Aggregate result of the bulk operation</returns>
        public static SchemaResult BulkResult<T>(IEnumerable<T> entries, 
            Func<T, SchemaResult> operation, 
            string errorMessage = "Failed Bulk Operation",
            SchemaContext context = default)
        {
            bool success = true;
            foreach (var entry in entries)
            {
                var res = operation(entry);
                success &= res.Passed;
            }

            return CheckIf(context, success, errorMessage);
        }
        
        private RequestStatus status;
        public RequestStatus Status => status;

        private string message;
        public string Message => message;
        private object context;
        public object Context => context;
        public bool Passed => status == RequestStatus.Passed;
        public bool Failed => status == RequestStatus.Failed;

        public SchemaResult(RequestStatus status, string message, object context = null)
        {
            this.status = status;
            this.message = message;
            this.context = context;;

            // TODO: Maybe create a preference for whether schema results automatically create a log?
            // TODO: Handle logging when creating an empty result
// #if SCHEMA_DEBUG
             if (status == RequestStatus.Failed)
             {
                 string logMsg = $"[Context={context}] {message}";
                 Logger.LogError(logMsg);
             }
// #endif
        }
        
        public override string ToString()
        {
            return $"SchemaResponse[status={status}, message={message}]";
        }
    
        public static SchemaResult Fail(SchemaContext context, string errorMessage) => 
            new SchemaResult(status: RequestStatus.Failed, message: errorMessage, context: context);

        public static SchemaResult Pass(string successMessage = "", SchemaContext context = default) =>
            new SchemaResult(status: RequestStatus.Passed, message: successMessage, context: context);

        public static SchemaResult CheckIf(SchemaContext context, bool conditional, string errorMessage,
            string successMessage = "")
        {
            return !conditional ? Fail(context: context, errorMessage: errorMessage) : Pass(successMessage: successMessage, context: context);
        }
    }

    public struct SchemaResult<TResult>
    {
        private RequestStatus status;
        public RequestStatus Status => status;
        private TResult result;
        public TResult Result
        {
            get
            {
                // if (status == RequestStatus.Failed)
                // {
                //     throw new InvalidOperationException($"The request status {status} is not supported.");
                // }
                
                return result;
            }
        }

        private object context;
        public object Context => context;
        private string message;
        public string Message => message;
        public bool Passed => status == RequestStatus.Passed;
        public bool Failed => status == RequestStatus.Failed;

        public SchemaResult(RequestStatus status, TResult result, string message, object context = null)
        {
            this.status = status;
            this.result = result;
            this.message = message;
            this.context = context;;

            // TODO: Maybe create a preference for whether schema results automatically create a log?
            // TODO: Handle logging when creating an empty result
// #if SCHEMA_DEBUG
             if (status == RequestStatus.Failed)
             {
                 string logMsg = $"[Context={context}] {message}";
                 Logger.LogError(logMsg);
             }
// #endif
        }
        
        public override string ToString()
        {
            return $"SchemaResponse[status={status}, Message={message}]";
        }
    
        public static SchemaResult<TResult> Fail(string errorMessage, object context) => 
            new SchemaResult<TResult>(status: RequestStatus.Failed, message: errorMessage, result: default, context: context?.ToString());

        public static SchemaResult<TResult> Pass(TResult result, string successMessage = "", object context = null) =>
            new SchemaResult<TResult>(status: RequestStatus.Passed, message: successMessage, result: result, context: context?.ToString());
        
        public static SchemaResult<TResult> CheckIf(bool conditional, TResult result, string errorMessage, string successMessage = "", object context = null)
        {
            return conditional ? Pass(result: result, successMessage: successMessage, context: context) : Fail(errorMessage: errorMessage, context: context);
        }

        public bool Try(out TResult result)
        {
            result = this.result;
            return Passed;
        }

        public bool Try(out TResult result, out SchemaResult<TResult> res)
        {
            result = this.result;
            res = this;
            return Passed;
        }

        public SchemaResult<TOut> CastError<TOut>()
        {
            return SchemaResult<TOut>.Fail(Message, Context);
        }

        public SchemaResult<TResult> Fail(string errorMessage)
        {
            return Fail(errorMessage, context);
        }

        public SchemaResult<TResult> Pass(TResult result, string successMessage = "")
        {
            return Pass(result, successMessage, context: context);
        }
        
        public SchemaResult<TResult> CheckIf(bool conditional, TResult result, string errorMessage, string successMessage = "")
        {
            return conditional ? Pass(result: result, successMessage: successMessage, context: context) : Fail(errorMessage: errorMessage, context: context);
        }

        public static SchemaResult<TResult> New(object context)
        {
            var newRes = new SchemaResult<TResult>(status: RequestStatus.UNSET, default, null, context?.ToString());
            return newRes;
        }

        public SchemaResult Cast()
        {
            return new SchemaResult(status, message, context);
        }
    }
}