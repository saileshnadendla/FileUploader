using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Worker.Helpers
{
    public interface IRedisConnectionHelper
    {
        IConnectionMultiplexer CreateConnection(string connectionString);
    }
}
