using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using Xunit;

namespace Colipu.Extensions.ACM.Aliyun.Standard.Test
{
    public class UnitTest
    {
        [Fact]
        public void Test()
        {
            var config = new AliyunConfig
            {
                Namespace = "",
                AccessKey = "",
                SecretKey = "",
                DNS = "",
                ListenerInterval = 30,
                Port = "8080",
                DataId = "",
                Group = ""
            };
            var tokenSource = new CancellationTokenSource();
            var client = new AliyunClient(Options.Create(config));
            //可选事件，当内部发生错误时回调此事件。可通过此事件进行日志记录等处理。
            //当发生错误时，ACM会忽略此次错误继续运行。这意味着在网络不通的情况下，此回调函数会被重复回调。
            client.ExceptionEvent += (ex) =>
            {
                Console.WriteLine($"出现异常:{ex.Message}");
            };
            //开始监听远程配置变更
            client.Start(() =>
            {
                return File.ReadAllText("Config/Config.yml");
            }, (content) =>
            {
                //此为配置发生变更的回调方法
                //content为远程配置文件内容
                Console.WriteLine("============Config Changed============");
                File.WriteAllText("Config/Config.yml", content);
                Console.WriteLine(content);
            }, tokenSource.Token);
            Thread.Sleep(1000);
            tokenSource.Cancel();
            Thread.Sleep(int.MaxValue);
        }
    }
}
