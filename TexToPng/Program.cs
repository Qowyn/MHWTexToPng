using SharpGL;
using SharpGL.Version;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TexToPng
{
    class Program
    {
        const int MagicNumberTex = 0x00584554; // 54 45 58 00 | TEX 

        static int Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.Error.WriteLine("Usage: TexToPng source.tex [destination.png]");
                return -1;
            }

            FileInfo fi = new FileInfo(args[0]);
            if (!fi.Exists)
            {
                Console.Error.WriteLine("ERROR: {0} not found!", args[0]);
                return -2;
            }

            if (fi.Length < 0xC0)
            {
                Console.Error.WriteLine("ERROR: {0} too small to be valid!", args[0]);
                return -2;
            }

            string destPath = Path.GetFullPath(args.Length == 2 ? args[1] : Path.ChangeExtension(args[0], ".png"));
            
            if (destPath == Path.GetFullPath(args[0]))
            {
                Console.Error.WriteLine("ERROR: Source and Destination are the same path.");
                return -2;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(args[1]));

            OpenGL gl = new OpenGL();
            if (!gl.Create(OpenGLVersion.OpenGL4_2, RenderContextType.HiddenWindow, 1, 1, 32, null))
            {
                Console.Error.WriteLine("ERROR: Unable to initialize OpenGL 4.2");
            }

            using (BinaryReader reader = new BinaryReader(File.OpenRead(args[0])))
            {
                int magicNumber = reader.ReadInt32();
                if (magicNumber != MagicNumberTex)
                {
                    Console.Error.WriteLine("ERROR: {0} is not a valid tex file.", args[0]);
                    return -2;
                }

                reader.BaseStream.Position = 0x14;

                int mipMapCount = reader.ReadInt32();
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                reader.BaseStream.Position = 0x24;

                int type = reader.ReadInt32();

                reader.BaseStream.Position = 0xB8;

                long offset = reader.ReadInt64();
                int size;

                if (mipMapCount > 1)
                    size = (int)(reader.ReadInt64() - offset);
                else
                    size = (int)(fi.Length - offset);

                reader.BaseStream.Position = offset;

                uint internalFormat;

                switch (type)
                {
                    case 0x16:
                        internalFormat = 0x83F1; // COMPRESSED_RGBA_S3TC_DXT1_EXT
                        break;
                    case 0x17:
                        internalFormat = 0x83F1; // COMPRESSED_RGBA_S3TC_DXT1_EXT
                        break;
                    case 0x1A:
                        internalFormat = 0x8DBD; // COMPRESSED_RG_RGTC2
                        break;
                    case 0x1f:
                        internalFormat = 0x8E8C; // COMPRESSED_RGBA_BPTC_UNORM_ARB
                        break;
                    default:
                        internalFormat = 0;
                        break;
                }

                if (internalFormat == 0)
                {
                    Console.Error.WriteLine("ERROR: Unknown Texture format {0}.", type);
                    return -2;
                }

                {
                    byte[] data = reader.ReadBytes(size);
                    GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                    gl.CompressedTexImage2D(OpenGL.GL_TEXTURE_2D, 0, internalFormat, width, height, 0, size, dataHandle.AddrOfPinnedObject());
                    dataHandle.Free();
                }

                int[] pixels = new int[width * height];
                GCHandle pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                gl.GetTexImage(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, pixels);
                Bitmap texture = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, pixelsHandle.AddrOfPinnedObject());
                texture.Save(destPath, ImageFormat.Png);
                pixelsHandle.Free();
            }

            return 0;
        }
    }
}
