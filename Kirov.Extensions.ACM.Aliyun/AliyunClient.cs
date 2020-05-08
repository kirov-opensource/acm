#if (NETSTANDARD2_0 || NETSTANDARD2_1)
using Microsoft.Extensions.Options;
#endif
using Colipu.Extensions.ACM.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Colipu.Extensions.ACM.Aliyun
{
    public class AliyunClient : Client
    {
        private AliyunConfig aliyunConfig;
        //内部错误事件回调
        public event Action<Exception> ExceptionEvent;
#if (NETSTANDARD2_0 || NETSTANDARD2_1)
        public AliyunClient(IOptions<AliyunConfig> options)
        {
            this.aliyunConfig = options.Value;
            //标准库未实现GBK编码,引入拓展
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
#endif
#if (NET45 || NET451 || NET452 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)
        public AliyunClient(AliyunConfig aliyunConfig)
        {
            this.aliyunConfig = aliyunConfig;
        }
#endif


        /// <summary>
        /// 开始监听远程配置变更.发生异常会回调异常事件
        /// </summary>
        /// <param name="contentFunc">获取内容的Func，程序会多次调用此Func，应当保证每次调用得到的都是最新的Content</param>
        /// <param name="configChangeAction"></param>
        public override void Start(Func<string> contentFunc, Action<string> configChangeAction)
        {
            if (contentFunc == null)
            {
                throw new ArgumentNullException(nameof(contentFunc));
            }
            //程序初始化时先读取一次配置
            Execute(contentFunc, configChangeAction);
            Task.Factory.StartNew(() =>
            {
                Listen(contentFunc, configChangeAction);
            });
        }

        /// <summary>
        /// 开始监听远程配置变更.发生异常会回调异常事件
        /// </summary>
        /// <param name="contentFunc"></param>
        /// <param name="configChangeAction"></param>
        /// <param name="cancellationToken">取消令牌</param>
        public void Start(Func<string> contentFunc, Action<string> configChangeAction, CancellationToken cancellationToken)
        {
            if (contentFunc == null)
            {
                throw new ArgumentNullException(nameof(contentFunc));
            }
            //程序初始化时先读取一次配置
            Execute(contentFunc, configChangeAction);
            Task.Factory.StartNew(() =>
            {
                Listen(contentFunc, configChangeAction);
            }, cancellationToken);
        }

        /// <summary>
        /// 执行拉取配置操作
        /// </summary>
        /// <param name="contentFunc"></param>
        /// <param name="configChangeAction"></param>
        public override void Execute(Func<string> contentFunc, Action<string> configChangeAction)
        {
            if (contentFunc == null)
            {
                throw new ArgumentNullException(nameof(contentFunc));
            }
            try
            {
                var configContent = GetRemoteConfigure(aliyunConfig.Group, aliyunConfig.DataId).Result;
                configChangeAction.Invoke(configContent);
            }
            catch (Exception ex)
            {
                if (ExceptionEvent == null)
                {
                    return;
                }
                ExceptionEvent.Invoke(ex);
            }
        }

        /// <summary>
        /// 监听配置变更
        /// </summary>
        /// <param name="configChangeAction"></param>
        private void Listen(Func<string> contentFunc, Action<string> configChangeAction)
        {
            while (true)
            {
                try
                {
                    if (!CheckRemoteConfigureChangeAsync(aliyunConfig.Group, aliyunConfig.DataId, contentFunc.Invoke()).Result)
                    {
                        continue;
                    }
                    var configContent = GetRemoteConfigure(aliyunConfig.Group, aliyunConfig.DataId).Result;
                    configChangeAction.Invoke(configContent);
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (ExceptionEvent == null)
                    {
                        continue;
                    }
                    try
                    {
                        ExceptionEvent.Invoke(ex);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }
        }
        private async Task<bool> CheckRemoteConfigureChangeAsync(string group, string dataId, string content)
        {
            var probeModifyRequest = dataId + char.ToString((char)2) + group + char.ToString((char)2) + GetMD5(content) + char.ToString((char)2) + aliyunConfig.Namespace + char.ToString((char)1);
            long timeStamp = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            var signature = await SignatureAsync($"{aliyunConfig.Namespace}+{group}+{timeStamp}", aliyunConfig.SecretKey);
            var param = new Dictionary<string, string>() {
                    { "Probe-Modify-Request", probeModifyRequest }
                };
            var headerInfo = new Dictionary<string, string>() {
                    { "longPullingTimeout", aliyunConfig.ListenerInterval.ToString() },
                    { "Spas-AccessKey", aliyunConfig.SecretKey },
                    { "timeStamp", timeStamp.ToString() },
                    { "Spas-Signature", signature }
                };
            var ipAddress = await GetAcmIpAddressAsync($"http://{aliyunConfig.DNS}:{aliyunConfig.Port}/diamond-server/diamond");
            var listenRet = await GetConfigureAsync(param, headerInfo, $"http://{ipAddress}:{aliyunConfig.Port}/diamond-server/config.co");
            return !string.IsNullOrEmpty(listenRet); //如果为空，则配置文件没有变化
        }

        private async Task<string> GetRemoteConfigure(string group, string dataId)
        {
            #region 配置信息
            long timeStamp = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            var spasSignature = await SignatureAsync($"{aliyunConfig.Namespace}+{group}+{timeStamp}", aliyunConfig.SecretKey);
            var headerInfo = new Dictionary<string, string> {
                    { "Spas-AccessKey", aliyunConfig.AccessKey },
                    { "timeStamp", timeStamp.ToString() },
                    { "Spas-Signature", spasSignature }
                };
            #endregion
            var ipAddress = await GetAcmIpAddressAsync($"http://{aliyunConfig.DNS}:{aliyunConfig.Port}/diamond-server/diamond");
            var url = $"http://{ipAddress}:{aliyunConfig.Port}/diamond-server/config.co?dataId={dataId}&group={group}&tenant={aliyunConfig.Namespace}";
            return await GetConfigureAsync(headerInfo, url);

        }

        private string GetMD5(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            using (var md5 = MD5.Create())
            {
                byte[] data = md5.ComputeHash(Encoding.GetEncoding("GBK").GetBytes(value));
                var sBuilder = new StringBuilder();
                foreach (byte t in data)
                {
                    sBuilder.Append(t.ToString("x2"));
                }

                return sBuilder.ToString();
            }
        }

        private async Task<string> SignatureAsync(string encryptText, string encryptKey)
        {
            var byteData = Encoding.GetEncoding("GBK").GetBytes(encryptText);
            var byteKey = Encoding.GetEncoding("GBK").GetBytes(encryptKey);
            var hmac = new HMACSHA1(byteKey);
            var cryptoStream = new CryptoStream(Stream.Null, hmac, CryptoStreamMode.Write);
            await cryptoStream.WriteAsync(byteData, 0, byteData.Length);
            cryptoStream.Close();
            return Convert.ToBase64String(hmac.Hash);
        }


        #region HttpUtitl
        private async Task<string> GetConfigureAsync(Dictionary<string, string> headerInfo, string url)
        {
            var resp = await CreateGetHttpResponseAsync(url, headerInfo);
            var content = await GetResponseStringAsync(resp);
            return content;
        }

        private async Task<string> GetAcmIpAddressAsync(string url)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            return (await GetResponseStringAsync(request.GetResponse() as HttpWebResponse)).Trim();
        }

        private async Task<string> GetResponseStringAsync(HttpWebResponse webresponse)
        {
            using (Stream s = webresponse.GetResponseStream())
            {
                StreamReader reader = new StreamReader(s, Encoding.GetEncoding("GBK"));
                return await reader.ReadToEndAsync();
            }
        }

        private async Task<string> GetConfigureAsync(IDictionary<string, string> parameters, Dictionary<string, string> headerInfo, string url)
        {
            var resp = await CreatePostHttpResponseAsync(url, parameters, headerInfo);
            return await GetResponseStringAsync(resp);
        }

        private async Task<HttpWebResponse> CreateGetHttpResponseAsync(string url, Dictionary<string, string> headerInfo)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            request.ContentType = "application/x-www-form-urlencoded"; //链接类型
            if (headerInfo != null && headerInfo.Count > 0)
            {
                foreach (var item in headerInfo)
                {
                    request.Headers.Add(item.Key, item.Value);
                }
            }
            return await request.GetResponseAsync() as HttpWebResponse;
        }

        private async Task<HttpWebResponse> CreatePostHttpResponseAsync(string url, IDictionary<string, string> parameters, IDictionary<string, string> headerInfo)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;//创建请求对象
            request.Method = "POST";//请求方式
            request.ContentType = "application/x-www-form-urlencoded";//链接类型
            //构造header
            if (!(headerInfo == null || headerInfo.Count == 0))
            {
                foreach (var item in headerInfo)
                {
                    request.Headers.Add(item.Key, item.Value);
                }
            }

            //构造查询字符串param
            if (parameters == null || parameters.Count == 0)
            {
                return await request.GetResponseAsync() as HttpWebResponse;
            }
            var stringBuilder = new StringBuilder();
            foreach (string key in parameters.Keys)
            {
                stringBuilder.AppendFormat("&{0}={1}", key, parameters[key]);
            }
            var data = Encoding.GetEncoding("GBK").GetBytes(stringBuilder.ToString().TrimStart('&'));
            //写入请求流
            using (Stream stream = await request.GetRequestStreamAsync())
            {
                stream.Write(data, 0, data.Length);
            }
            return await request.GetResponseAsync() as HttpWebResponse;
        }
        #endregion
    }
}
