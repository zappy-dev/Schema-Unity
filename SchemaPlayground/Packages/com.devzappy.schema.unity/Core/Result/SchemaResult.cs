using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Schema.Core.Logging;

namespace Schema.Core
{
    public enum RequestStatus
    {
        UNSET,
        Passed,
        Failed,
    }

    public struct SchemaResult : ISchemeResult
    {
        public const string MESSAGE_NOT_SET = "Message not set!";
        public static readonly SchemaResult NoOp = new SchemaResult(status: RequestStatus.Passed, "NoOp", context: null);

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
            bool haltOnError = false,
            SchemaContext context = default)
        {
            bool success = true;
            foreach (var entry in entries)
            {
                var res = operation(entry);
                success &= res.Passed;
                if (res.Failed)
                {
                    Logger.LogError(res.Message, res.Context);
                    if (haltOnError)
                    {
                        break;
                    }
                }
            }

            return CheckIf(context, success, errorMessage);
        }
        public static async Task<SchemaResult> BulkResult<T>(IEnumerable<T> entries, 
            Func<T, Task<SchemaResult>> operation, 
            string errorMessage = "Failed Bulk Operation",
            bool haltOnError = false,
            SchemaContext context = default)
        {
            bool success = true;
            foreach (var entry in entries)
            {
                var res = await operation(entry);
                success &= res.Passed;
                if (res.Failed)
                {
                    Logger.LogError(res.Message, res.Context);
                    if (haltOnError)
                    {
                        break;
                    }
                }
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
            this.message = string.IsNullOrEmpty(message) ? MESSAGE_NOT_SET : message;
            this.context = context; // NOTE: Context may change
            
            if (SchemaResultSettings.Instance.LogFailure && status == RequestStatus.Failed)
            {
                OnSchemaResultFailed(message, this.context);
            }
        }

        public SchemaResult(RequestStatus status, string message, ICloneable cloneableContext = null) : this(status, message, cloneableContext?.Clone())
        {
        }

        internal static void OnSchemaResultFailed(string message, object context)
        {
            var logMsgSb = new StringBuilder();
            logMsgSb.Append($"{message}");
            if (context != null)
            {
                logMsgSb.AppendLine();
                logMsgSb.AppendLine("Context:");
                logMsgSb.AppendLine(context.ToString());
            }

            if (SchemaResultSettings.Instance.LogStackTrace)
            {
                StackTrace stackTrace = new StackTrace();
                logMsgSb.Append(stackTrace.ToString());
            }
                 
            Logger.LogError(logMsgSb.ToString());
        }
        
        public override string ToString()
        {
            return $"SchemaResponse[context={context}, status={status}, message={message}]";
        }

        #region Static Factory Methods
        
        public static SchemaResult Fail(SchemaContext context, string errorMessage) => 
            new SchemaResult(status: RequestStatus.Failed, message: errorMessage, context: context);

        public static SchemaResult Pass(string successMessage = "", SchemaContext context = default) =>
            new SchemaResult(status: RequestStatus.Passed, message: successMessage, context: context);

        public static SchemaResult CheckIf(SchemaContext context, bool conditional, string errorMessage,
            string successMessage = "")
        {
            return !conditional ? Fail(context: context, errorMessage: errorMessage) : Pass(successMessage: successMessage, context: context);
        }
        #endregion

        public SchemaResult<TOut> CastError<TOut>()
        {
            return SchemaResult<TOut>.Fail(Message, Context);
        }

        public SchemaResult<TOut> CastError<TOut>(SchemaResult<TOut> res)
        {
            return SchemaResult<TOut>.Fail(Message, Context);
        }
        
        public bool Try(out SchemaResult err)
        {
            err = this;
            return this.status == RequestStatus.Passed;
        }
        
        public bool TryErr(out SchemaResult err)
        {
            err = this;
            return this.status == RequestStatus.Failed;
        }
    }

    public struct SchemaResult<TResult> : ISchemeResult
    {
        private RequestStatus status;
        public RequestStatus Status => status;
        private TResult result;
        public TResult Result
        {
            get
            {
                // Number of comments, uncomments: 2
                // Uncomment reason: I want to make sure this fails if calling the result was wrong during list data type creation
                if (status == RequestStatus.Failed)
                {
                    throw new InvalidOperationException($"This result failed, reason: {Message}");
                }
                
                return result;
            }
        }

        private object context;
        public object Context => context;
        private string message;
        public string Message => message;
        public bool Passed => status == RequestStatus.Passed;
        public bool Failed => status == RequestStatus.Failed;

        public SchemaResult(RequestStatus status, TResult result, string message, ICloneable context = null)
        {
            this.status = status;
            this.message = (string.IsNullOrEmpty(message) ? SchemaResult.MESSAGE_NOT_SET : message);
            this.result = result;
            this.context = context?.Clone();

            // TODO: Maybe create a preference for whether schema results automatically create a log?
            // TODO: Handle logging when creating an empty result
            // TODO: Need to not log an error during tests...
// #if SCHEMA_DEBUG
            // Number of times I comment / uncomment this: 5
            // Reason to uncomment: Helpful for debugging a publishing issue
            
             if (SchemaResultSettings.Instance.LogFailure && status == RequestStatus.Failed)
             {
                 SchemaResult.OnSchemaResultFailed(this.message, this.context);
                 // string logMsg = $"[Context={context}] {message}";
                 // // Logger.LogDbgError(logMsg);
                 // // Reason to convert to non-dbg: Helpful for debugging list data type conversion error
                 // Logger.LogError(logMsg);
             }
// #endif
        }
        
        public override string ToString()
        {
            return $"SchemaResponse[status={status}, Message={message}]";
        }

        public bool Try(out TResult result)
        {
            result = this.result;
            return Passed;
        }

        public bool TryErr(out TResult result, out SchemaResult<TResult> error)
        {
            result = this.result;
            error = this;
            return Failed;
        }

        public bool Try(out TResult result, out SchemaResult<TResult> res)
        {
            result = this.result;
            res = this;
            return Passed;
        }

        public SchemaResult<TResult> Pass(TResult result, string successMessage = "")
        {
            return Pass(result, successMessage, context: null); // fast pass, skip context closing
            // return Pass(result, successMessage, context: context);
        }
        
        public SchemaResult<TResult> CheckIf(bool conditional, TResult result, string errorMessage, string successMessage = "")
        {
            return conditional ? Pass(result: result, successMessage: successMessage, context: context) : Fail(errorMessage: errorMessage, context: context);
        }

        public SchemaResult<TResult> Fail(string errorMessage)
        {
            return Fail(errorMessage, Context);
        }

        #region Static Factory Methods
        
        public static SchemaResult<TResult> New(object context)
        {
            var newRes = new SchemaResult<TResult>(status: RequestStatus.UNSET, default, null, context?.ToString());
            return newRes;
        }
    
        public static SchemaResult<TResult> Fail(string errorMessage, object context) => 
            new SchemaResult<TResult>(status: RequestStatus.Failed, message: errorMessage, result: default, context: context?.ToString());

        public static SchemaResult<TResult> Pass(TResult result, string successMessage = "", object context = null) =>
            new SchemaResult<TResult>(status: RequestStatus.Passed, message: successMessage, result: result, context: context?.ToString());
        
        public static SchemaResult<TResult> CheckIf(bool conditional, TResult result, string errorMessage, string successMessage = "", object context = null)
        {
            return conditional ? Pass(result: result, successMessage: successMessage, context: context) : Fail(errorMessage: errorMessage, context: context);
        }

        #endregion

        #region Cast Methods
        
        public SchemaResult Cast()
        {
            return new SchemaResult(status, message, context: context);
        }

        public SchemaResult<TOut> CastError<TOut>()
        {
            return SchemaResult<TOut>.Fail(Message, Context);
        }

        public SchemaResult<TOut> CastError<TOut>(SchemaResult<TOut> res)
        {
            return SchemaResult<TOut>.Fail(Message, context);
        }

        #endregion
    }
}