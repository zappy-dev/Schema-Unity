namespace Schema.Core
{
    public struct SchemaResponse
    {
        private RequestStatus status;
        public RequestStatus Status => status;
        private object payload;
        public object Payload => payload;
        public bool IsSuccess => status == RequestStatus.Success;

        public SchemaResponse(RequestStatus status, object payload)
        {
            this.status = status;
            this.payload = payload;
            
            if (status == RequestStatus.Success)
            {
                Logger.Log(payload?.ToString());
            }
            else
            {
                Logger.LogError(payload?.ToString());
            }
        }
    
        public static SchemaResponse Error(string errorMessage) => 
            new SchemaResponse(status: RequestStatus.Error, payload: errorMessage);

        public static SchemaResponse Success(string message) =>
            new SchemaResponse(status: RequestStatus.Success, payload: message);

        public override string ToString()
        {
            return $"SchemaResponse[status={status}, payload={payload}]";
        }
    }
}