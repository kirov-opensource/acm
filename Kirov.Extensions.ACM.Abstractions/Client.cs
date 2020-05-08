using System;

namespace Colipu.Extensions.ACM.Abstractions
{
    public abstract class Client
    {
        /// <summary>
        /// 远程配置变更回调方法
        /// </summary>
        /// <param name="contentFunc"></param>
        /// <param name="configChangeAction"></param>
        public abstract void Start(Func<string> contentFunc, Action<string> configChangeAction);

        /// <summary>
        /// 执行拉取配置操作
        /// </summary>
        /// <param name="contentFunc"></param>
        /// <param name="configChangeAction"></param>
        public abstract void Execute(Func<string> contentFunc, Action<string> configChangeAction);
    }
}
