using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GLMExtractor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var path = Environment.CurrentDirectory;

            Regex glmRegex = null;
            Regex fileRegex = null;

            if (args.Length > 0 && args[0].Length > 0)
                path = args[0];

            if (args.Length > 1 && args[1].Length > 0)
                glmRegex = new Regex(args[1]);

            if (args.Length > 2 && args[2].Length > 0)
                fileRegex = new Regex(args[2]);

            var o = 1;
            var output = Path.Combine(path, "GLM");
            while (Directory.Exists(output))
                output = Path.Combine(path, String.Format("GLM-{0}", o++));

            Directory.CreateDirectory(output);

            Console.WriteLine("Unpacking GLMs from {0} to {1} with glm filter: \"{2}\" and file filter: \"{3}\"", path, output, glmRegex, fileRegex);

            var fc = 1;
            var files = EnumerateFiles(path, glmRegex);
            var tfc = files.Count;
            var ugc = 0;
            var ufc = 0;

            var start = DateTime.Now;

            foreach (var f in files)
            {
                try
                {
                    Console.WriteLine("Processing {0}...", f);

                    using (var br = new BinaryReader(new FileStream(f, FileMode.Open, FileAccess.Read)))
                    {
                        br.BaseStream.Seek(br.BaseStream.Length - 4, SeekOrigin.Begin);

                        var headerOff = br.ReadInt32();
                        br.BaseStream.Seek(headerOff, SeekOrigin.Begin);

                        var strHeader = Encoding.UTF8.GetString(br.ReadBytes(4));
                        if (strHeader != "CHNK")
                        {
                            Console.WriteLine();
                            ++fc;
                            continue;
                        }

                        var opts = br.ReadBytes(4);
                        if (opts[0] != 66 || opts[1] != 76)
                        {
                            Console.WriteLine();
                            ++fc;
                            continue;
                        }

                        var strTableOff = br.ReadInt32();
                        var strTableSize = br.ReadInt32();
                        /*var entryCount = */br.ReadInt32();

                        var currPos = br.BaseStream.Position;

                        br.BaseStream.Seek(strTableOff, SeekOrigin.Begin);

                        var stringTable = br.ReadBytes(strTableSize);
                        var fileEntries = CreateEntriesByStringTable(stringTable);

                        br.BaseStream.Position = currPos;

                        var c = 1;
                        var tfec = fileEntries.Count;

                        ++ugc;

                        foreach (var entry in fileEntries)
                        {
                            if (fileRegex != null && !fileRegex.IsMatch(entry.Name))
                            {
                                ++c;
                                continue;
                            }

                            Console.Title = String.Format("Current File: {0} {1}/{2} | Processed: {3}/{4} (\"{5}\" From \"{6}\")", Path.GetFileName(f), fc, tfc, c++, tfec, fileRegex, glmRegex);

                            entry.Read(br, f);

                            currPos = br.BaseStream.Position;

                            Console.WriteLine("Unpacking {0}", entry.Name);

                            var outName = Path.Combine(output, entry.Name);
                            var i = 1;
                            while (File.Exists(outName))
                                outName = Path.Combine(output, String.Format("{0}-{1}.{2}", entry.Name, i++, Path.GetExtension(entry.Name)));

                            using (var bw = new BinaryWriter(new FileStream(outName, FileMode.Create, FileAccess.ReadWrite)))
                            {
                                br.BaseStream.Position = entry.Offset;
                                bw.Write(br.ReadBytes((Int32) entry.Size));
                            }

                            br.BaseStream.Position = currPos;
                            ++ufc;
                        }
                        
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Got exception at file: {0}. Exception: {1}", f, e);
                }

                Console.WriteLine();
                ++fc;
            }
            Console.Title = String.Format("Unpacked {0} GLMs! Total files unpacked: {1}", ugc, ufc);
            Console.WriteLine("Unpacking finished! Total time: {0}", DateTime.Now - start);
            Console.WriteLine("Output folder: {0}", output);
            Console.ReadLine();
        }

        private static List<FileEntry> CreateEntriesByStringTable(IEnumerable<Byte> data)
        {
            var sList = new List<FileEntry>();

            var sb = new StringBuilder();

            foreach (var t in data)
            {
                if (t != 0)
                    sb.Append((Char) t);
                else
                {
                    sList.Add(new FileEntry { Name = sb.ToString() });
                    sb.Clear();
                }
            }
            return sList;
        }

        private static List<String> EnumerateFiles(String path, Regex regex)
        {
            return Directory.EnumerateFiles(path, "*.glm", SearchOption.AllDirectories).Where(f => regex == null || regex.IsMatch(f)).ToList();
        }
    }

    public class FileEntry
    {
        public UInt32 Offset;
        public UInt32 Size;
        public UInt32 RealSize;
        public UInt32 ModifiedTime;
        public UInt16 Scheme;
        public UInt32 PackFile;
        public String Name;

        public Boolean IsRead { get; set; }
        public String FileName { get; set; }

        public void Read(BinaryReader br, String fName)
        {
            FileName = fName;

            Offset = br.ReadUInt32();
            Size = br.ReadUInt32();
            RealSize = br.ReadUInt32();
            ModifiedTime = br.ReadUInt32();
            Scheme = br.ReadUInt16();
            PackFile = br.ReadUInt32();
        }

        public override String ToString()
        {
            return String.Format("FileName: {0} | Name: {1}", FileName, Name);
        }
    }
}
