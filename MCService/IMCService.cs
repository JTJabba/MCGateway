using System.Security.Cryptography.X509Certificates;

namespace MCService
{
    public interface IMCService
    {
        void Start(string[] args) ;

        void AddClientConnection<TClientClass>(ClientConnection clientConnection) where TClientClass : class;

        void AddHandler<TServiceHandler>() where TServiceHandler : class;

        void Build<TService>() where TService : class;

        void Stop();

    }

    public record ClientConnection(string hostAddress, X509Certificate certificate)
    {
        public ClientConnection(string hostAddress, string certificateFileLocation)
        : this(hostAddress, new X509Certificate2(certificateFileLocation))
        {
        } 
    }


}
