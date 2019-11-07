using System;
namespace csRpcClient
{
    public interface IRpcClient
    {
        string Call(string method, params Object[] args);
    }
}
