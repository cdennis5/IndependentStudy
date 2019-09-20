using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CSCore;
using CSCore.Codecs;
using CSCore.DSP;
using CSCore.Utils;

namespace AttributeExtractionProofOfConcept
{
    class Program
    {
        static void Main(string[] args)
        {
            // Validate cmd line args
            if (args.Length != 1)
            {
                Console.WriteLine("Provide a valid music file location (mp3, wav, or m4a)");
                return;
            }

            string filename = args[0];

            if (!File.Exists(filename))
            {
                Console.Error.WriteLine("Could not find file: '{0}'", filename);
                return;
            }

            // Read in audio file and initialize fft
            IWaveSource waveSource;
            ISampleSource sampleSource;
            try
            {
                waveSource = CodecFactory.Instance.GetCodec(filename);
            }
            catch (NotSupportedException ex)
            {
                Console.Error.WriteLine("No supporting decoder for given file: '{0}'\n", filename);
                Console.Error.WriteLine(ex.ToString());
                return;
            }

            sampleSource = waveSource.ToSampleSource();

            FftProvider fftProvider = new FftProvider(sampleSource.WaveFormat.Channels, FftSize.Fft1024);
            Dictionary<int, Complex[]> fftResults = new Dictionary<int, Complex[]>();
            int i = 0;

            // Scrub through the audio 1024 samples at a time and extract fft info
            while (sampleSource.Position < sampleSource.Length)
            {
                float[] samples = new float[1024];
                sampleSource.Read(samples, 0, 1024);
                fftProvider.Add(samples, samples.Count());

                Complex[] result = new Complex[(int)fftProvider.FftSize];
                if (fftProvider.GetFftData(result))
                {
                    fftResults.Add(i, result);
                    ++i;
                }
            }

            Console.WriteLine("FFT done");

            double[] fundFreqs = new double[fftResults.Count];      // Stores the fundamental frequency at each frame (every 1024 samples)
            i = 0;
            foreach (var kvp in fftResults)
            {
                Complex[] vals = kvp.Value;
                int nyquistLength = kvp.Value.Length / 2;
                double[] normals = new double[nyquistLength];

                for (int j = 0; j < nyquistLength; ++j)
                {
                    normals[j] = Math.Sqrt(Math.Pow(vals[j].Real, 2) + Math.Pow(vals[j].Imaginary, 2));
                }

                double fundFreq = normals.Max();
                if (fundFreq != 0)
                {
                    fundFreq = (i * (waveSource.WaveFormat.SampleRate / 2)) / ((int)fftProvider.FftSize / 2);
                }
                fundFreqs[i] = fundFreq;
                ++i;
            }

            Console.WriteLine("Fundamental frequency analysis of each frame done");
        }
    }
}
