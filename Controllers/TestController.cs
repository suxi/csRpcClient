using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace csRpcClient.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        private readonly IRpcClient _client;

        public TestController(ILogger<TestController> logger, IRpcClient rpcClient)
        {
            _logger = logger;
            _client = rpcClient;
        }

        [HttpGet]
        public async Task<string> Get()
        {
            return await _client.CallAsync(queue: "inner.test.1", method: "hello",timeout:3000, "world", "csharp");
        }
    }
}
