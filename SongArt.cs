using System;
using System.IO;
using System.Collections.Generic;

using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Tools;

using System.Drawing;
using System.Drawing.Imaging;

using System.Windows.Forms;
using System.Linq;

namespace SongArt.cs
{

    public class Note
    {

        public int inst { get; }

        public int time { get; }

        public int num { get; }

        public int duration { get; }
        
        public Note(int inst, int time, int num, int duration)
        {
            this.inst = inst;
            this.time = time;
            this.num = num;
            this.duration = duration;
        }
    }

    class SongArt
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Find the project directory. We'll be needing it a lot
            string cd = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            Console.WriteLine("Starting up at current directory: " + cd);


            // Find the song
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = cd + "/Songs/";
            ofd.RestoreDirectory = true;
            ofd.Title = "Choose a midiFile";
            ofd.DefaultExt = "mid";
            ofd.ShowDialog();
            string filename = ofd.FileName;
            var midiFile = MidiFile.Read(filename);
            Console.WriteLine("Using midiFile " + filename);


            // Convert the file to CSV
            Console.WriteLine("Converting midiFile to CSV...");
            var csv_conv = new CsvConverter();
            MidiFileCsvConversionSettings settings = new MidiFileCsvConversionSettings();
            settings.NoteFormat = NoteFormat.Note;
            csv_conv.ConvertMidiFileToCsv(midiFile, cd + "/Notes.csv", true, settings);
            Console.WriteLine("Wrote notes to CSV at " + cd + "/Notes.csv");


            // Read CSV file for notes
            List<Note> notes = new List<Note>();
            int minNum = 255;
            int maxNum = 0;
            int maxTicks = 0;
            using (var reader = new StreamReader(cd + "/Notes.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line.Contains("Note"))
                    {
                        var values = line.Split(',');
                        int inst = Int32.Parse(values[0]) - 1;
                        int time = Int32.Parse(values[1]);
                        int num = Int32.Parse(values[4]);
                        int duration = Int32.Parse(values[5]);
                        notes.Add(new Note(inst, time, num, duration));
                        if (minNum > num) { minNum = num; }
                        if (maxNum < num) { maxNum = num; }
                        if (maxTicks <= time + duration) { maxTicks = time + duration; }
                    }
                }
            }

            // Convert notes list to n-D array for each instrument to pixels array alternating each row with each instrument
            int numInst = notes[notes.Count - 1].inst + 1;
            Console.WriteLine("Number of instruments: " + numInst);
            Console.WriteLine("Number of ticks per instrument: " + maxTicks);
            Console.WriteLine("Number of pixels: " + (numInst * maxTicks));
            int[,] pixelsND = new int[numInst, maxTicks];
            foreach (Note n in notes)
            {
                for(int i = 0; i < n.duration; i++)
                {
                    pixelsND[n.inst, n.time + i] = 255 * (n.num - minNum) / (maxNum - minNum);
                }
            }


            // Generate PNGs
            Console.WriteLine("Generating image...");

            int size_multiplier = 1;
            float r_mult = 1;
            float g_mult = 1;
            float b_mult = 0;
            int dim = (int)(Math.Sqrt((numInst * maxTicks))) * size_multiplier;
            Console.WriteLine("Dimensions of image = " + dim/size_multiplier);

            using (Bitmap b = new Bitmap(dim, dim))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    int inst_index = 0;
                    int index = 0;
                    for (int y = 0; y < dim; y += size_multiplier)
                    {
                        for (int x = 0; x < dim; x += size_multiplier)
                        {
                            //Console.WriteLine(inst_index + "," + ((x/size_multiplier) + index));
                            var color = Color.Black;
                            try
                            {
                                // Try to set a color
                                color = Color.FromArgb(
                                (int)(r_mult * pixelsND[inst_index, (x / size_multiplier) + index]),
                                (int)(g_mult * pixelsND[inst_index, (x / size_multiplier) + index]),
                                (int)(b_mult * pixelsND[inst_index, (x / size_multiplier) + index])
                                );
                            } catch
                            {
                                // If out of range, just make it black
                                color = Color.Black;
                            }

                            Pen pen = new Pen(color);
                            Brush brush = new SolidBrush(color);
                            Rectangle point = new Rectangle(x, y, size_multiplier, size_multiplier);
                            g.DrawRectangle(pen, point);
                            g.FillRectangle(brush, x, y, size_multiplier, size_multiplier);
                        }
                        inst_index++;
                        if (inst_index >= numInst)
                        {
                            inst_index = 0;
                            index += dim / size_multiplier;
                        }

                    }
                }
                b.Save(cd + "/Image.png", ImageFormat.Png);
            }
            Console.WriteLine("Image saved in " + cd + "/Image.png");


            // Play the song
            Console.WriteLine("Playing back MidiFile...");            
            using (var outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth"))
            using (var playback = midiFile.GetPlayback(outputDevice))
            {
                //playback.Speed = 2.0;
                playback.Play();
            }
            Console.WriteLine("Done!");


            Console.ReadLine();
            return 0;
        }
    }
}