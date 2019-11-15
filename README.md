# csRpcClient
asp.net rabbitmq framework

RPC的api函数，直接声明在Services/RpcProvider.cs文件中
例：

    internal partial class RpcClient
    {
        // To Append Rpc Functions here
        public static string hello(string world, string name)
        {
            return $"hello {world}, {name}!";
        }
    }

# 参考库
* [Redis](https://stackexchange.github.io/StackExchange.Redis/) 
* [Mongodb](https://docs.mongodb.com/ecosystem/drivers/csharp/)
* [Mysql](https://dev.mysql.com/doc/connector-net/en/connector-net-tutorials-intro.html)
* [Json](https://www.newtonsoft.com/json)
