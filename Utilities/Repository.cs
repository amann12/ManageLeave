using System;
using System.Linq;

namespace CoreBotCLU.Utilities
{
    public class Repository
    {
        private static Random random=new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars,length).Select(c => c[random.Next(c.Length)]).ToArray());
        }
    }
}
