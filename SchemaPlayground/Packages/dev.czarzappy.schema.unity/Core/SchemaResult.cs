namespace Schema.Core
{
    public struct SchemaResult
    {
        public enum RequestStatus
        {
            Failed,
            Success
        }
        
        private RequestStatus status;
        public RequestStatus Status => status;
        private object payload;
        public object Payload => payload;
        public string context;
        public string Context => context;
        public bool IsSuccess => status == RequestStatus.Success;
        public bool Failed => status == RequestStatus.Failed;

        public SchemaResult(RequestStatus status, object payload = null, string context = null)
        {
            this.status = status;
            this.payload = payload;
            this.context = context;;

            string message = $"[Context={context}] {payload}";
            if (status == RequestStatus.Success)
            {
                Logger.Log(message);
            }
            else
            {
                Logger.LogError(message);
            }
        }
    
        public static SchemaResult Fail(string errorMessage, object context = null) => 
            new SchemaResult(status: RequestStatus.Failed, payload: errorMessage, context: context?.ToString());

        public static SchemaResult Success(string message, object context = null) =>
            new SchemaResult(status: RequestStatus.Success, payload: message, context: context?.ToString());

        public override string ToString()
        {
            return $"SchemaResponse[status={status}, payload={payload}]";
        }
    }
}