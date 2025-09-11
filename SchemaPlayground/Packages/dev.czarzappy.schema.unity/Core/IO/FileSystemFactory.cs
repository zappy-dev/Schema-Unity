namespace Schema.Core.IO
{
    public static class FileSystemFactory
    {
        public static readonly IFileSystem DefaultFileSystem = new LocalFileSystem();
    }
}