using Newtonsoft.Json;

namespace Schema.Core
{
    public abstract class ResultGenerator
    {
        internal SchemaResult Fail(string errorMessage, SchemaContext? context = null) => 
            new SchemaResult(status: RequestStatus.Failed, message: errorMessage, context: context);

        internal SchemaResult Pass(string message = null, SchemaContext? context = null) =>
            new SchemaResult(status: RequestStatus.Passed, message: message, context: context);

        internal SchemaResult CheckIf(bool conditional, string errorMessage, string successMessage, SchemaContext? context = null)
        {
            return conditional ? Pass(message: successMessage, context) : Fail(errorMessage, context);
        }
        
        public SchemaResult<TResult> Fail<TResult>(string errorMessage, SchemaContext? context = null) => 
            SchemaResult<TResult>.Fail(errorMessage, context);

        public SchemaResult<TResult> Pass<TResult>(TResult result, string successMessage = null, SchemaContext? context = null) => 
            SchemaResult<TResult>.Pass(result: result, successMessage: successMessage, context: context);

        public SchemaResult<TResult> CheckIf<TResult>(bool conditional, TResult result, string errorMessage, string successMessage, SchemaContext? context = null)
            => SchemaResult<TResult>.CheckIf(conditional, result: result, errorMessage: errorMessage, successMessage: successMessage, context: context);
    }
}