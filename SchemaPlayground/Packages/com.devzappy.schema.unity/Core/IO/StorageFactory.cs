namespace Schema.Core.IO
{
    public static class StorageFactory
    {
        public static Storage GetEditorStorage()
        {
            return new Storage(FileSystemFactory.DefaultFileSystem);
        }
    }
}