namespace MCGateway.Protocol
{
    public class MCConnectionClosedException : Exception
    {
        public MCConnectionClosedException() { }
        public MCConnectionClosedException(string message) : base(message) { }
        public MCConnectionClosedException(string message, Exception inner) : base(message, inner) { }
    }
    public class MCClientConnectionClosedException : MCConnectionClosedException
    {
        public MCClientConnectionClosedException() { }
        public MCClientConnectionClosedException(string message) : base(message) { }
        public MCClientConnectionClosedException(string message, Exception inner) : base(message, inner) { }
    }
    public class MCServerConnectionClosedException : MCConnectionClosedException
    {
        public MCServerConnectionClosedException() { }
        public MCServerConnectionClosedException(string message) : base(message) { }
        public MCServerConnectionClosedException(string message, Exception inner) : base(message, inner) { }
    }
}
