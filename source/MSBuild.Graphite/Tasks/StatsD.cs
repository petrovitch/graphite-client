using System;
using Graphite;
using Graphite.Configuration;
using Graphite.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Graphite.Tasks
{
    public sealed class StatsD : Task, IStatsDConfiguration
    {
        public StatsD()
        {
            this.Port = 8125;
            this.Value = 1;
        }

        [Required]
        public string Address { get; set; }

        public int Port { get; set; }

        public string PrefixKey { get; set; }

        public string SuffixKey { get; set; }

        [Required]
        public string Key { get; set; }

        public int Value { get; set; }

        [Required]
        public MetricType Type { get; set; }

        public override bool Execute()
        {
            using (var channelFactory = new ChannelFactory(null, this))
            {
                IMonitoringChannel channel = channelFactory.CreateChannel(
                    this.Type.ToString(),
                    "statsd");

                channel.Report(this.Key, this.Value);

                var metric = string.IsNullOrEmpty(this.PrefixKey) ? this.Key : (this.PrefixKey + "." + this.Key);
                metric = string.IsNullOrEmpty(this.SuffixKey) ? metric : ( metric + this.SuffixKey);

                Console.Out.WriteLine(
                    "Reported value '{0}' of type '{1}' for key '{2}' to {3}:{4}.",
                    this.Value,
                    this.Type,
                    metric,
                    this.Address,
                    this.Port);
            }

            return true;
        }
    }
}
