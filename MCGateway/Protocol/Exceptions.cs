namespace MCGateway.Protocol
{
    public class MCConnectionClosedException : Exception
    {
        /// <summary>
        /// Thrown by Gateway when a connection shuts down smoothly
        /// or after any other errors causing a connection to close have been handled
        /// </summary>
        public MCConnectionClosedException() { }
    }
}
