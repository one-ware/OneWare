using System.Text;

namespace OneWare.VcdViewer;

class UnbufferedStreamReader: TextReader
{
    public Stream BaseStream { get; }

    public UnbufferedStreamReader(string path)
    {
        BaseStream = new FileStream(path, FileMode.Open);
    }

    public UnbufferedStreamReader(Stream stream)
    {
        BaseStream = stream;
    }

    // This method assumes lines end with a line feed.
    // You may need to modify this method if your stream
    // follows the Windows convention of \r\n or some other 
    // convention that isn't just \n
    public override string ReadLine()
    {
        List<byte> bytes = new List<byte>();
        int current;
        while ((current = Read()) != -1 && current != (int)'\n')
        {
            byte b = (byte)current;
            bytes.Add(b);
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    // Read works differently than the `Read()` method of a 
    // TextReader. It reads the next BYTE rather than the next character
    public override int Read()
    {
        return BaseStream.ReadByte();
    }

    public override void Close()
    {
        BaseStream.Close();
    }
    protected override void Dispose(bool disposing)
    {
        BaseStream.Dispose();
    }

    public override int Peek()
    {
        throw new NotImplementedException();
    }

    public override int Read(char[] buffer, int index, int count)
    {
        throw new NotImplementedException();
    }

    public override int ReadBlock(char[] buffer, int index, int count)
    {
        throw new NotImplementedException();
    }       

    public override string ReadToEnd()
    {
        throw new NotImplementedException();
    }
}