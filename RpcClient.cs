using System;
using Microsoft.Extensions.Configuration;

class RpcClient : IRpcClient
{
    string Host;
    public RpcClient(IConfiguration config)
    {
        Host = config["Host"];
    }

    public T Call<T>(object[] args)
    {
        throw new NotImplementedException();
    }
}