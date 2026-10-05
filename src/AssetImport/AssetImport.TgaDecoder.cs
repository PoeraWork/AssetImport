using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AssetImport
{
    internal static class TgaDecoder
    {
        internal sealed class Image
        {
            internal int Width, Height;
            // RGBA, left-to-right / bottom-to-top, as required by Texture2D.
            internal byte[] Pixels;
        }
        private static void BigEndian(Stream stream, uint value)
        { for (int i=3;i>=0;i--) stream.WriteByte((byte)(value >> (i*8))); }
        private static void Chunk(Stream stream,string type,byte[] bytes)
        {
            BigEndian(stream,(uint)bytes.Length);
            byte[] tag=Encoding.ASCII.GetBytes(type); stream.Write(tag,0,4); stream.Write(bytes,0,bytes.Length);
            uint crc=0xffffffff;
            for (int i=0;i<bytes.Length+4;i++)
            {
                crc^=i<4 ? tag[i] : bytes[i-4];
                for (int bit=0;bit<8;bit++) crc=(crc>>1)^((crc&1)!=0 ? 0xedb88320u : 0u);
            }
            BigEndian(stream,crc^0xffffffff);
        }
        internal static byte[] EncodePng(Image image)
        {
            // PNG scanlines are top-to-bottom. DeflateStream provides raw DEFLATE;
            // add the zlib wrapper explicitly for .NET 3.5 and Mono compatibility.
            int stride=image.Width*4;
            byte[] raw=new byte[(stride+1)*image.Height];
            for (int y=0;y<image.Height;y++) Buffer.BlockCopy(image.Pixels,(image.Height-1-y)*stride,raw,y*(stride+1)+1,stride);
            uint a=1,b=0; foreach (byte value in raw) { a=(a+value)%65521; b=(b+a)%65521; }
            byte[] compressed;
            using (var deflated=new MemoryStream())
            {
                using (var compressor=new DeflateStream(deflated,CompressionMode.Compress)) compressor.Write(raw,0,raw.Length);
                compressed=deflated.ToArray();
            }
            using (var png=new MemoryStream())
            using (var ihdr=new MemoryStream())
            using (var idat=new MemoryStream())
            {
                byte[] signature={137,80,78,71,13,10,26,10}; png.Write(signature,0,8);
                BigEndian(ihdr,(uint)image.Width); BigEndian(ihdr,(uint)image.Height);
                byte[] format={8,6,0,0,0}; ihdr.Write(format,0,5); Chunk(png,"IHDR",ihdr.ToArray());
                idat.WriteByte(0x78); idat.WriteByte(0x9c); idat.Write(compressed,0,compressed.Length); BigEndian(idat,(b<<16)|a);
                Chunk(png,"IDAT",idat.ToArray()); Chunk(png,"IEND",new byte[0]); return png.ToArray();
            }
        }
        internal static Image Decode(byte[] bytes)
        {
            using (var r=new BinaryReader(new MemoryStream(bytes,false)))
            {
                try
                {
                    int id=r.ReadByte(),map=r.ReadByte(),type=r.ReadByte(); r.ReadBytes(9);
                    int width=r.ReadUInt16(),height=r.ReadUInt16(),depth=r.ReadByte(),descriptor=r.ReadByte();
                    bool grey=type==3 || type==11, rle=type==10 || type==11;
                    if (map!=0 || (type!=2 && type!=3 && type!=10 && type!=11) ||
                        (grey ? depth!=8 : depth!=24 && depth!=32) || (descriptor&0xc0)!=0)
                        throw new FormatException("Unsupported TGA: use 24/32-bit true color or 8-bit grayscale, raw/RLE, without a palette.");
                    if (width<=0 || height<=0 || (long)width*height>16777216) throw new FormatException("Invalid/oversized TGA dimensions.");
                    if (r.ReadBytes(id).Length!=id) throw new EndOfStreamException();
                    int count=width*height,n=0,bpp=depth/8;
                    var image=new Image { Width=width, Height=height, Pixels=new byte[count*4] };
                    while (n<count)
                    {
                        int packet=rle ? r.ReadByte() : 0;
                        int run=rle ? (packet&127)+1 : 1;
                        if (run>count-n) throw new FormatException("TGA packet exceeds image size.");
                        byte[] sample=null;
                        for (int i=0;i<run;i++,n++)
                        {
                            if (i==0 || !rle || (packet&128)==0) { sample=r.ReadBytes(bpp); if (sample.Length!=bpp) throw new EndOfStreamException(); }
                            int x=n%width,y=n/width;
                            if ((descriptor&16)!=0) x=width-1-x;
                            if ((descriptor&32)!=0) y=height-1-y;
                            int offset=(y*width+x)*4;
                            image.Pixels[offset]=sample[grey ? 0 : 2]; image.Pixels[offset+1]=sample[grey ? 0 : 1]; image.Pixels[offset+2]=sample[0];
                            image.Pixels[offset+3]=(byte)(bpp==4 && (descriptor&15)!=0 ? sample[3] : 255);
                        }
                    }
                    return image;
                }
                catch (EndOfStreamException) { throw new FormatException("Truncated TGA image."); }
            }
        }
    }
}
