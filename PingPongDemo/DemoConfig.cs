﻿// Ignore cringe for now. Will eventually replace this file with autogenerated one when EasyConfig is updated

namespace PingPongDemo
{
    internal sealed class DemoConfig
    {
        public static string BackendServerAddress { get; set; }
        public string backendserveraddress
        {
            get { return BackendServerAddress; }
            set { BackendServerAddress = value; }
        }
        public static int BackendServerPort { get; set; }
        public int backendserverport
        {
            get { return BackendServerPort; }
            set { BackendServerPort = value; }
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        static DemoConfig()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("/app/appsettings.json")
                .Build();
            config.GetRequiredSection("DemoConfig").Get<DemoConfig>();
        }
    }
}