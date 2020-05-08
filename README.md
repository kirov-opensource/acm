# Kirov.Extensions.ACM
为了能够实时从远程变更项目的配置以及实现统一管理，所以将项目的配置统一获取，ACM实现了调用方只需要填写远程服务器地址和授权信息，就能够实时获取到最新的配置信息，而无需关心如何获取.

| Vendor | Support |  
| ------ | ------- |
| 阿里云 | :white_check_mark: |

## 如何使用
#### `ASP.NET Framework`
```csharp
var config = new AliyunConfig
{
    Namespace = "",
    AccessKey = "",
    SecretKey = "",
    DNS = "acm.aliyun.com",
    ListenerInterval = 30,
    Port = "8080",
    DataId = "",
    Group = ""
};
var client = new AliyunClient(config);
//可选事件，当内部发生错误时回调此事件。可通过此事件进行日志记录等处理。
//当发生错误时，ACM会忽略此次错误继续运行。这意味着在网络不通的情况下，此回调函数会被重复回调。
client.ExceptionEvent += (ex) =>
{
    Console.WriteLine($"出现异常:{ex.Message}");
};
//开始监听远程配置变更
client.Start(() => { return File.ReadAllText("Config/Config.yml"); }, (content) =>
 {
     //此为配置发生变更的回调方法
     //content为远程配置文件内容
     Console.WriteLine("============Config Changed============");
     File.WriteAllText("Config/Config.yml", content);
     Console.WriteLine(content);
 });
```

对于运行在`IIS`上的应用程序, 如果`ACM`程序在`Web Site`停止的时候还在工作, 则会影响`IIS`程序池回收, 所以需要使用`取消令牌`用来控制`ACM`后台任务的停止.
```csharp
//创建令牌, 最好使用静态变量保存它, 以便在程序停止时取消令牌
static CancellationTokenSource tokenSource = new CancellationTokenSource();
//开始监听远程配置变更
client.Start(() => { return File.ReadAllText("Config/Config.yml"); }, (content) =>
 {
     //此为配置发生变更的回调方法
     //content为远程配置文件内容
     Console.WriteLine("============Config Changed============");
     File.WriteAllText("Config/Config.yml", content);
     Console.WriteLine(content);
 }, 
 //传入令牌
 tokenSource.Token);
```
`Global.asax.cs`
```csharp
//在程序终止时取消ACM后台任务
public void Application_End(){


}

```


#### `ASP.NET Core 2.2`
#### 先决条件
##### `ACM`的相关配置是从环境变量读取的，因此需要配置以下环境变量，带`*`为必须。
* `* ASPNETCORE_ACM_ALIYUN_NAMESPACE`
* `* ASPNETCORE_ACM_ALIYUN_ACCESS_KEY`
* `* ASPNETCORE_ACM_ALIYUN_SECRET_KEY`
* `* ASPNETCORE_ACM_ALIYUN_DATA_ID`
* `* ASPNETCORE_ACM_ALIYUN_GROUP`
* `ASPNETCORE_ACM_ALIYUN_DNS` 可不配置，默认值为`acm.aliyun.com`
* `ASPNETCORE_ACM_ALIYUN_PORT` 可不配置，默认值为`8080`
* `ASPNETCORE_ACM_ALIYUN_LISTENER_INTERVAL` 可不配置，默认值为`30`

在`Startup.cs`配置`ACM Client`

```csharp
public Startup(IConfiguration configuration)
{
    Configuration = configuration;
}

public IConfiguration Configuration { get; }

public void ConfigureServices(IServiceCollection services)
{
    //应当确保ACM的配置始在所有配置的最开始
    services.AddAliyunACM(Configuration, (ex) =>
    {   
        //错误回调
        System.Console.WriteLine(ex.Message);
    });
}
```

## 构建和发布
* 构建`nupkg`
  * 使用`scripts/nuget_pack.bat`即可将项目打包成`nupkg`.
