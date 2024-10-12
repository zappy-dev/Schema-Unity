using System.Collections.Generic;

namespace Schema.Core
{
    public class Schema
    {
        public static Dictionary<string, DataScheme> DataSchemes = new Dictionary<string, DataScheme>();
        
        static Schema()
        {
            // TODO: dynamically load
            DataSchemes.Add("Quests", new DataScheme("Quests")
            {
                Attributes = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "ID",
                        DataType = DataType.String,
                        DefaultValue = string.Empty,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Title",
                        DataType = DataType.String,
                        DefaultValue = string.Empty,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Description",
                        DataType = DataType.String,
                        DefaultValue = string.Empty,
                    }
                },
                Entries = new List<DataEntry>
                {
                    new DataEntry
                    {
                        EntryData = new Dictionary<string, object>
                        {
                            {"ID", "Quest_01"},
                            {"Title", "Starting Quest"},
                            {"Description", "A quest to start the game."},
                        }
                    }
                }
            });
        }

        public static SchemaResponse CreateNewSchema(string name)
        {
            // input validation
            if (DataSchemes.ContainsKey(name))
            {
                return SchemaResponse.Error("Schema already exists: " + name);
            }

            if (string.IsNullOrEmpty(name))
            {
                return SchemaResponse.Error("Schema name is invalid: " + name);
            }
        
            // business logic
            DataScheme scheme = new DataScheme(name);
            DataSchemes.Add(name, scheme);
            
            return SchemaResponse.Success(scheme);
        }
    }

    public enum RequestStatus
    {
        Error,
        Success
    }

    public struct SchemaResponse
    {
        private RequestStatus status;
        public RequestStatus Status => status;
        private object payload;
        public object Payload => payload;

        public SchemaResponse(RequestStatus status, object payload)
        {
            this.status = status;
            this.payload = payload;
        }
    
        public static SchemaResponse Error(string errorMessage) => 
            new SchemaResponse(status: RequestStatus.Error, payload: errorMessage);

        public static SchemaResponse Success(object payload) =>
            new SchemaResponse(status: RequestStatus.Success, payload: payload);
    }
}