using System;
namespace csRpcClient
{
    public interface IRpcClient
    {
        string Call(string queue, string method,int timeout, params Object[] args);
    }
}
