namespace Schema.Core
{
    public abstract class ResultGenerator
    {
        internal SchemaResult Fail(string errorMessage) => 
            new SchemaResult(status: SchemaResult.RequestStatus.Failed, message: errorMessage, context: this.ToString());

        internal SchemaResult Pass(string message = null) =>
            new SchemaResult(status: SchemaResult.RequestStatus.Passed, message: message, context: this.ToString());

        internal SchemaResult CheckIf(bool conditional, string errorMessage, string successMessage)
        {
            return conditional ? Pass(message: successMessage) : Fail(errorMessage);
        }
        
        public SchemaResult<TResult> Fail<TResult>(string errorMessage) => 
            SchemaResult<TResult>.Fail(errorMessage, this.ToString());

        public SchemaResult<TResult> Pass<TResult>(TResult result, string message = null) => 
            SchemaResult<TResult>.Pass(result: result, successMessage: message, context: this.ToString());

        public SchemaResult<TResult> CheckIf<TResult>(bool conditional, TResult result, string errorMessage, string successMessage)
            => SchemaResult<TResult>.CheckIf(conditional, result: result, errorMessage: errorMessage, successMessage: successMessage);
    }
}