using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace ParaTool.Core.LSLib;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LocaHeader
{
    public static uint DefaultSignature = 0x41434f4c; // 'LOCA'
    public uint Signature;
    public uint NumEntries;
    public uint TextsOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LocaEntry
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] Key;
    public ushort Version;
    public uint Length;

    public string KeyString
    {
        get
        {
            int nameLen;
            for (nameLen = 0; nameLen < Key.Length && Key[nameLen] != 0; nameLen++) ;
            return Encoding.UTF8.GetString(Key, 0, nameLen);
        }
        set
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Key = new byte[64];
            Array.Clear(Key, 0, Key.Length);
            Array.Copy(bytes, Key, bytes.Length);
        }
    }
}

public class LocalizedText
{
    public string Key = "";
    public ushort Version;
    public string Text = "";
}

public class LocaResource
{
    public List<LocalizedText> Entries = [];
}

public class LocaBinaryReader(Stream stream) : IDisposable
{
    private readonly Stream Stream = stream;
    public void Dispose() => Stream.Dispose();

    public LocaResource Read()
    {
        using var reader = new BinaryReader(Stream);
        var loca = new LocaResource();
        var header = BinUtils.ReadStruct<LocaHeader>(reader);

        if (header.Signature != LocaHeader.DefaultSignature)
            throw new InvalidDataException("Incorrect signature in localization file");

        var entries = new LocaEntry[header.NumEntries];
        BinUtils.ReadStructs(reader, entries);

        if (Stream.Position != header.TextsOffset)
            Stream.Position = header.TextsOffset;

        foreach (var entry in entries)
        {
            var text = Encoding.UTF8.GetString(reader.ReadBytes((int)entry.Length - 1));
            loca.Entries.Add(new LocalizedText
            {
                Key = entry.KeyString,
                Version = entry.Version,
                Text = text
            });
            reader.ReadByte(); // null terminator
        }

        return loca;
    }
}

public class LocaBinaryWriter(Stream stream)
{
    private readonly Stream stream = stream;

    public void Write(LocaResource res)
    {
        using var writer = new BinaryWriter(stream);
        var header = new LocaHeader
        {
            Signature = LocaHeader.DefaultSignature,
            NumEntries = (uint)res.Entries.Count,
            TextsOffset = (uint)(Marshal.SizeOf(typeof(LocaHeader)) + Marshal.SizeOf(typeof(LocaEntry)) * res.Entries.Count)
        };
        BinUtils.WriteStruct(writer, ref header);

        var entries = new LocaEntry[header.NumEntries];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = new LocaEntry
            {
                KeyString = res.Entries[i].Key,
                Version = res.Entries[i].Version,
                Length = (uint)Encoding.UTF8.GetByteCount(res.Entries[i].Text) + 1
            };
        }
        BinUtils.WriteStructs(writer, entries);

        foreach (var entry in res.Entries)
        {
            writer.Write(Encoding.UTF8.GetBytes(entry.Text));
            writer.Write((byte)0);
        }
    }
}

public class LocaXmlReader(Stream stream) : IDisposable
{
    private readonly Stream stream = stream;
    public void Dispose() => stream.Dispose();

    public LocaResource Read()
    {
        var resource = new LocaResource();
        using var reader = XmlReader.Create(stream);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "content")
            {
                var key = reader.GetAttribute("contentuid") ?? "";
                var version = reader.GetAttribute("version") != null
                    ? ushort.Parse(reader.GetAttribute("version")!)
                    : (ushort)1;
                var text = reader.ReadString();
                resource.Entries.Add(new LocalizedText { Key = key, Version = version, Text = text });
            }
        }
        return resource;
    }
}

public class LocaXmlWriter(Stream stream)
{
    private readonly Stream stream = stream;

    public void Write(LocaResource res)
    {
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "\t" };
        using var writer = XmlWriter.Create(stream, settings);
        writer.WriteStartElement("contentList");
        foreach (var entry in res.Entries)
        {
            writer.WriteStartElement("content");
            writer.WriteAttributeString("contentuid", entry.Key);
            writer.WriteAttributeString("version", entry.Version.ToString());
            writer.WriteString(entry.Text);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.Flush();
    }
}

public enum LocaFormat { Loca, Xml }

public static class LocaUtils
{
    public static LocaFormat ExtensionToFileFormat(string path)
    {
        return Path.GetExtension(path).ToLower() switch
        {
            ".loca" => LocaFormat.Loca,
            ".xml" => LocaFormat.Xml,
            _ => throw new ArgumentException("Unrecognized file extension: " + Path.GetExtension(path))
        };
    }

    public static LocaResource Load(Stream stream, LocaFormat format)
    {
        return format switch
        {
            LocaFormat.Loca => new LocaBinaryReader(stream).Read(),
            LocaFormat.Xml => new LocaXmlReader(stream).Read(),
            _ => throw new ArgumentException("Invalid loca format")
        };
    }
}
