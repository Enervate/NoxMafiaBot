using System;
using System.Linq;
using System.Threading.Tasks;

namespace NoxMafiaBot
{
    class Program
    {
        public static Task Main(string[] args)
            => Startup.RunAsync(args);

        //public static void Main(string[] args)
        //{
        //    PowerFlags test = PowerFlags.Doctor | PowerFlags.Frame;

        //    PowerFlags test2 = test & PowerFlags.Miller;
        //}
    }
}
