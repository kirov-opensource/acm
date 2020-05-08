#if (NETSTANDARD2_0 || NETSTANDARD2_1)
using Colipu.Extensions.ACM.Aliyun;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Colipu.Extensions.ACM.Aliyun
{
    internal class HostedService : IHostedService
    {
        private readonly AliyunClient _aliyunClient;

        public HostedService(AliyunClient aliyunClient)
        {
            _aliyunClient = aliyunClient;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            DoWork(cancellationToken);
            return Task.CompletedTask;
        }

        private void DoWork(CancellationToken cancellationToken)
        {
            //可选事件，当内部发生错误时回调此事件。可通过此事件进行日志记录等处理。
            //当发生错误时，ACM会忽略此次错误继续运行。这意味着在网络不通的情况下，此回调函数会被重复回调。
            _aliyunClient.ExceptionEvent += (ex) =>
            {
                Console.WriteLine($"出现异常:{ex.Message}");
            };
            var filePath = $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json";
            //开始监听远程配置变更
            _aliyunClient.Start(() =>
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
            }, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

    }
}
#endif