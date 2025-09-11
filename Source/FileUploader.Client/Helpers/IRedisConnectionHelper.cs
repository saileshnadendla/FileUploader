using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Client.Helpers
{
    internal interface IRedisConnectionHelper
    {
        Task<IConnectionMultiplexer> CreateConnectionAsync(string connectionString);
    }
}
