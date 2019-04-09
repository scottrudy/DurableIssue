using System;
using System.Threading;
using Microsoft.AspNetCore.Mvc;

namespace DurableIssue.ExternalApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private static readonly Random _random = new Random();

        // GET api/values/5
        [HttpGet("{sleepTime}")]
        public ActionResult<object> Get(int sleepTime)
        {
            var startTime = DateTime.UtcNow.ToString("H:mm:ss");
            var next = sleepTime < 1 ? _random.Next(1, 60) : _random.Next(1, sleepTime);
            Thread.Sleep(next * 1000);

            var result = new {
                Start = startTime,
                End = DateTime.UtcNow.ToString("H:mm:ss")
            };
            return Ok(result);
        }
    }
}
