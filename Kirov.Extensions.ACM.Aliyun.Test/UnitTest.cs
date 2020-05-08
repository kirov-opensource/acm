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
            //��ѡ�¼������ڲ���������ʱ�ص����¼�����ͨ�����¼�������־��¼�ȴ���
            //����������ʱ��ACM����Դ˴δ���������С�����ζ�������粻ͨ������£��˻ص������ᱻ�ظ��ص���
            client.ExceptionEvent += (ex) =>
            {
                Console.WriteLine($"�����쳣:{ex.Message}");
            };
            //��ʼ����Զ�����ñ��
            client.Start(() =>
            {
                return File.ReadAllText("Config/Config.yml");
            }, (content) =>
            {
                //��Ϊ���÷�������Ļص�����
                //contentΪԶ�������ļ�����
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
