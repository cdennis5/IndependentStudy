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
        /// <summary>
        /// Simple function to find the index of the max value in a double array
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static int MaxIndex(double[] sequence)
        {
            int maxIndex = -1;
            double maxValue = double.MinValue;

            int i = 0;
            foreach (double d in sequence)
            {
                if (d > maxValue)
                {
                    maxValue = d;
                    maxIndex = i;
                }

                ++i;
            }

            return maxIndex;
        }

        /// <summary>
        /// Produces a timestamp in seconds based on the index of an audio frame, the sample rate, and number of samples
        /// </summary>
        /// <param name="index"></param>
        /// <param name="sampleRate"></param>
        /// <param name="numSamples"></param>
        /// <returns></returns>
        public static double FrameIndexToTimestamp(int index, int sampleRate, int numSamples)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentException("Sample rate must be positive");
            }

            return index * ((double)numSamples / sampleRate);
        }

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args"></param>
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
            List<Tuple<int, Complex[]>> fftResults = new List<Tuple<int, Complex[]>>();
            int i = 0;

            // Scrub through the audio 1024 samples at a time and perform fft on each chunk
            while (sampleSource.Position < sampleSource.Length)
            {
                float[] samples = new float[1024];
                sampleSource.Read(samples, 0, 1024);
                fftProvider.Add(samples, samples.Count());

                Complex[] result = new Complex[(int)fftProvider.FftSize];
                if (fftProvider.GetFftData(result))
                {
                    fftResults.Add(new Tuple<int, Complex[]>(i, result));
                    ++i;
                }
            }

            Console.WriteLine("FFT done");

            // Stores the fundamental frequency and amplitude at each frame (1024 samples)
            List<Tuple<double, double>> fundFreqs = new List<Tuple<double, double>>();
            i = 0;

            // For each fft output
            foreach (var pair in fftResults)
            {
                // The output of the fft has a frequency domain and amplitude range.
                // In this case, the index of the value represents frequency: index * ((sampleRate / 2) / (vals.Length / 2))
                // The value at an index is the amplitude as a complex number. To normalize, calculate: sqrt(real^2 + imaginary^2), this can then be
                // used to calculate dB level with dBspl equation (20 * log10(normal))
                Complex[] vals = pair.Item2;

                // Frequency buckets produced by fft. Size of each bucket depends on sample rate.
                // 0 to N/2 of fft output is what we want, N/2 to N is garbage (negative frequencies)
                int nyquistLength = vals.Length / 2;

                // Nyquist rate is maximum possible reproducible sample frequency of a given sample rate
                int nyquistRate = sampleSource.WaveFormat.SampleRate / 2;
                

                // Normalize the amplitudes
                double[] normals = new double[nyquistLength];

                for (int j = 0; j < nyquistLength; ++j)
                {
                    normals[j] = Math.Sqrt(Math.Pow(vals[j].Real, 2) + Math.Pow(vals[j].Imaginary, 2));
                }

                // Find the fundamental frequency and amplitude of that frequency
                double fundFreq = 0;
                double amplitude = double.NegativeInfinity; // in dB spl

                int freqBucket = MaxIndex(normals);
                if (freqBucket > 0)
                {
                    fundFreq = freqBucket * (nyquistRate / nyquistLength);
                }
                if (fundFreq != 0)
                {
                    amplitude = 20 * Math.Log10(normals[freqBucket]);   // Convert to dB
                }

                fundFreqs.Add(new Tuple<double, double>(fundFreq, amplitude));
                ++i;
            }

            Console.WriteLine("Fundamental frequency analysis of each frame done");

            Console.WriteLine("Writing results to csv (timestamp,frequency,amplitude)...");

            FileStream outFileStream = null;
            StreamWriter writer = null;
            try
            {
                outFileStream = File.Create("out.csv");
                writer = new StreamWriter(outFileStream);

                for (int j = 0; j < fundFreqs.Count; ++j)
                {
                    writer.WriteLine(string.Format("{0},{1},{2}", FrameIndexToTimestamp(j, sampleSource.WaveFormat.SampleRate, 1024), fundFreqs[j].Item1, fundFreqs[j].Item2));
                }

                writer.Close();
                outFileStream.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("failed to write output:");
                Console.Error.WriteLine(ex.ToString());

                if (outFileStream != null)
                    outFileStream.Close();
                if (writer != null)
                    writer.Close();
            }

            Console.WriteLine("Done");
            Console.ReadKey(true);
        }
    }
}
