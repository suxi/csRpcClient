using System;
using System.Threading.Tasks;

namespace csRpcClient
{
    public interface IRpcClient
    {
        string Call(string queue, string method,int timeout, params Object[] args);
        Task<string> CallAsync(string queue, string method,int timeout, params Object[] args);
    }
}
