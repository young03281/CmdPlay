using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdPlay
{
    class Main_C
    {
        static void Main(string[] args)
        {
            Console.Write("which gpu are you using? 1 for nvidia, 2 for others or none:");
            int choose = int.Parse(Console.ReadLine());
            if (choose == 1)
            {
                CmdPlay.Main_cuda(args);
            }
            else
            {
                CmdPlay_ge.Main_general(args);
            }
        }
    }
}
