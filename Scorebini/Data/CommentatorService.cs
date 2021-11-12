using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Scorebini.Data
{
    public class CommentatorService
    {
        private readonly ILogger Log;
        public CommentatorService(ILogger<CommentatorService> logger)
        {
            Log = logger;
        }
    }
}
