﻿namespace SmsSync.Configuration
{
    internal class AppConfiguration
    {
        public BackgroundConfiguration Background { get; set; }
        public HttpConfiguration Http { get; set; }
        public DatabaseConfiguration Database { get; set; }
        public ResourcesConfiguration Resources { get; set; }
        public RouteConfiguration[] Routes { get; set; }
    }
}