using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CSCore;
using CSCore.Codecs;
using CSCore.DSP;

namespace AttributeExtractionProofOfConcept
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Provide a valid music file location (mp3, wav, or m4a)");
                return;
            }

            string filename = args[0];

            if (!File.Exists(filename))
            {
                Console.Error.WriteLine("Could not file: '{0}'", filename);
                return;
            }

            IWaveSource source = CodecFactory.Instance.GetCodec(filename);
        }
    }
}
