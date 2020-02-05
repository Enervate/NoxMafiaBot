using System;
using System.Threading.Tasks;

namespace NoxMafiaBot
{
    class Program
    {
        public static Task Main(string[] args)
            => Startup.RunAsync(args);
    }
}
