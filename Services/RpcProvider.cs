using System;
using System.Collections.Generic;

namespace csRpcClient
{
    internal partial class RpcClient
    {
        // To Append Rpc Functions here
        public static string hello(string world, string name)
        {
            return $"hello {world}, {name}!";
        }
    }
}