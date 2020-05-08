#if (NETSTANDARD2_0 || NETSTANDARD2_1)
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace Colipu.Extensions.ACM.Aliyun
{
    public static class ServiceCollectionExtensions
    {
        public static void AddAliyunACM(this IServiceCollection services, IConfiguration configuration, Action<Exception> exptionAction = null)
        {
            var ASPNETCORE_ACM_ALIYUN_NAMESPACE = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_NAMESPACE");
            var ASPNETCORE_ACM_ALIYUN_ACCESS_KEY = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_ACCESS_KEY");
            var ASPNETCORE_ACM_ALIYUN_SECRET_KEY = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_SECRET_KEY");
            var ASPNETCORE_ACM_ALIYUN_DATA_ID = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_DATA_ID");
            var ASPNETCORE_ACM_ALIYUN_GROUP = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_GROUP");
            var ASPNETCORE_ACM_ALIYUN_DNS = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_DNS");
            var ASPNETCORE_ACM_ALIYUN_PORT = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_PORT");
            var ASPNETCORE_ACM_ALIYUN_LISTENER_INTERVAL = Environment.GetEnvironmentVariable("ASPNETCORE_ACM_ALIYUN_LISTENER_INTERVAL");

            long listenerInterval = 30;
            if (!string.IsNullOrWhiteSpace(ASPNETCORE_ACM_ALIYUN_LISTENER_INTERVAL) && long.TryParse(ASPNETCORE_ACM_ALIYUN_LISTENER_INTERVAL, out long temp) && temp >= 0)
            {
                listenerInterval = temp;
            }

            var aliyunClient = new AliyunClient(Options.Create(new AliyunConfig
            {
                Namespace = ASPNETCORE_ACM_ALIYUN_NAMESPACE ?? throw new ArgumentNullException(nameof(ASPNETCORE_ACM_ALIYUN_NAMESPACE)),
                AccessKey = ASPNETCORE_ACM_ALIYUN_ACCESS_KEY ?? throw new ArgumentNullException(nameof(ASPNETCORE_ACM_ALIYUN_ACCESS_KEY)),
                SecretKey = ASPNETCORE_ACM_ALIYUN_SECRET_KEY ?? throw new ArgumentNullException(nameof(ASPNETCORE_ACM_ALIYUN_SECRET_KEY)),
                DataId = ASPNETCORE_ACM_ALIYUN_DATA_ID ?? throw new ArgumentNullException(nameof(ASPNETCORE_ACM_ALIYUN_DATA_ID)),
                Group = ASPNETCORE_ACM_ALIYUN_GROUP ?? throw new ArgumentNullException(nameof(ASPNETCORE_ACM_ALIYUN_GROUP)),
                DNS = ASPNETCORE_ACM_ALIYUN_DNS ?? "acm.aliyun.com",
                Port = ASPNETCORE_ACM_ALIYUN_PORT ?? "8080",
                ListenerInterval = listenerInterval
            }));
            if (exptionAction != null) { aliyunClient.ExceptionEvent += exptionAction; }
            var filePath = $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json";
            //先从远程读取一次配置文件
            aliyunClient.Execute(() =>
            {
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Close();
                }
                return File.ReadAllText(filePath);
            },
            (content) =>
            {
                //写入配置
                File.WriteAllText(filePath, content);
                var configurationRoot = configuration as IConfigurationRoot;
                configurationRoot.Reload();
            });
            services.AddSingleton(serviceProvider =>
            {
                return aliyunClient;
            });
            services.AddHostedService<HostedService>();
        }

    }
}
#endif