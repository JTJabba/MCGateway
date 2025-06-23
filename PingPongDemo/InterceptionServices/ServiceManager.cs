using PingPongDemo.InterceptionServices.Services;

namespace PingPongDemo.InterceptionServices
{
    public class ServiceManager
    {
        private List<IService> _registeredServices = new();
        private readonly ILogger<ServiceManager> _logger;

        public ServiceManager(Services.ChatService chatService, ILogger<ServiceManager> logger)
        {
            _logger = logger;
            RegisterService(chatService);
        }

        public void RegisterService(IService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _logger.LogInformation("Registering service: {ServiceName}", service.GetServiceName());
            _registeredServices.Add(service);
        }

        public T GetServiceFromRegistry<T>() where T : IService
        {
            var service = _registeredServices.FirstOrDefault(s => s is T);
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
            }
            return (T)service;
        }

        public void UnregisterService(IService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _registeredServices.Remove(service);
        }

        public Task AlertServicesOfPlayerJoin(Guid playerUuid, string playerName)
        {
            if (playerUuid == Guid.Empty) throw new ArgumentException("Player UUID cannot be empty.", nameof(playerUuid));
            if (string.IsNullOrEmpty(playerName)) throw new ArgumentException("Player name cannot be null or empty.", nameof(playerName));

            var tasks = new List<Task>();

            // Notify all registered services about the player join
            foreach (var service in _registeredServices)
            {
                if (service is IPlayerService playerJoinService)
                {
                    tasks.Add(playerJoinService.OnPlayerJoin(playerUuid, playerName));
                }
            }
            return Task.WhenAll(tasks);
        }

        public Task AlertServicesOfPlayerLeave(Guid playerUuid, string playerName)
        {
            if (playerUuid == Guid.Empty) throw new ArgumentException("Player UUID cannot be empty.", nameof(playerUuid));
            if (string.IsNullOrEmpty(playerName)) throw new ArgumentException("Player name cannot be null or empty.", nameof(playerName));

            var tasks = new List<Task>();

            // Notify all registered services about the player leave
            foreach (var service in _registeredServices)
            {
                if (service is IPlayerService playerJoinService)
                {
                    tasks.Add(playerJoinService.OnPlayerLeave(playerUuid, playerName));
                }
            }
            return Task.WhenAll(tasks);
        }
    }
}
