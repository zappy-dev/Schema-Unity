namespace Schema.Core
{
    public class SchemaResultSettings
    {
        public static SchemaResultSettings Instance = new SchemaResultSettings();
        
        /// <summary>
        /// Determines whether to log stack traces for new Schema Results. Useful for debugging, but not recommended during production
        /// </summary>
        public bool LogStackTrace { get; set; } = false;

        /// <summary>
        /// Determines whether to log failure information. Useful for debugging, but not recommended during production
        /// </summary>
        public bool LogFailure { get; set; } = false;

        /// <summary>
        /// Determines whether to log verbose scheme information. Useful for debugging, but not recommended during production
        /// </summary>
        public bool LogVerboseScheme { get; set; } = false;

        private SchemaResultSettings()
        {
#if SCHEMA_DEBUG
            LogFailure = true;
            LogVerboseScheme = true;
#endif
        }
    }
}