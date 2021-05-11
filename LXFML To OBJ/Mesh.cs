using System;
using System.IO;
using System.Drawing;
using System.Text;

namespace LXFML_To_OBJ
{
    class Mesh
    {
        //-----------------------
        //----- Header data -----
        //-----------------------

        private int VertexAmt;
        private int IndexAmt;
        private Flag Flags;

        public Image Texture { get; private set; }
        public string TextureID { get; private set; }
        public bool HasUV { get; }

        //------------------------------------
        //----- Vector and position data -----
        //------------------------------------

        public float[,] Vertices { get; private set; }
        public float[,] Normals { get; }
        public float[,] UV { get; }
        public int[] Tris { get; }

        [Flags]
        public enum Flag
        {
            None      = 0,
            UV        = 1,  // 0x01
            Normals   = 2,  // 0x02
            Flexible  = 4,  // 0x08
            Unknown8  = 8,  // 0x04
            Unknown16 = 16, // 0x10
            Unknown32 = 32, // 0x20
        }

        public Mesh(byte[] file)
        {
            using (EndianReader reader = new EndianReader(new MemoryStream(file), Endian.Little))
            {
                if (reader.ReadInt32() != 0x42473031)
                    throw new InvalidDataException();

                VertexAmt = reader.ReadInt32();
                IndexAmt = reader.ReadInt32();
                Flags = (Flag)reader.ReadInt32();



                //-------------------------
                //----- Read vertices -----
                //-------------------------

                Vertices = new float[VertexAmt, 3];
                for (int i = 0; i < VertexAmt; i++)
                {
                    Vertices[i, 0] = reader.ReadSingle();
                    Vertices[i, 1] = reader.ReadSingle();
                    Vertices[i, 2] = reader.ReadSingle();
                }



                //--------------------------------------
                //----- Read normals if they exist -----
                //--------------------------------------

                if ((Flags & Flag.Normals) == Flag.Normals)
                {
                    Normals = new float[VertexAmt, 3];
                    for (int i = 0; i < VertexAmt; i++)
                    {
                        Normals[i, 0] = reader.ReadSingle();
                        Normals[i, 1] = reader.ReadSingle();
                        Normals[i, 2] = reader.ReadSingle();
                    }
                }
                else //Create dummy normals if they don't exist
                {
                    Normals = new float[VertexAmt, 3];
                    for (int i = 0; i < VertexAmt; i++)
                    {
                        Normals[i, 0] = 0;
                        Normals[i, 1] = 1;
                        Normals[i, 2] = 0;
                    }
                }



                //----------------------------------
                //----- Read UVs if they exist -----
                //----------------------------------

                if ((Flags & Flag.UV) == Flag.UV)
                {
                    HasUV = true;
                    UV = new float[VertexAmt, 2];
                    for (int i = 0; i < VertexAmt; i++)
                    {
                        UV[i, 0] = reader.ReadSingle();
                        UV[i, 1] = -reader.ReadSingle() + 1.0f;
                    }
                }
                else //Leave values at 0 if they don't exist
                {
                    HasUV = false;
                    UV = new float[VertexAmt, 2];
                }



                //----------------------------
                //----- Read face points -----
                //----------------------------

                Tris = new int[IndexAmt];
                for (int i = 0; i < IndexAmt; i++)
                {
                    Tris[i] = reader.ReadInt32();
                }
            }
        }

        public void SetTexture(string fileName, string textureID)
        {
            //-------------------------------------------
            //----- texture IDs start at 0x1DB6D0FB -----
            //-------------------------------------------

            using (EndianReader reader = new EndianReader(new FileStream(fileName, FileMode.Open), Endian.Big))
            {
                reader.ReadBytes(0x1DB6D0FB);

                for (int i = 0; i < 434; i++)
                {
                    StringBuilder id = new StringBuilder();
                    char c = (char)reader.ReadInt16();
                    while (c != '\u0000')
                    {
                        id.Append(c);
                        c = (char)reader.ReadInt16();
                    }

                    if (Path.GetFileNameWithoutExtension(id.ToString()) == textureID)
                    {
                        TextureID = textureID;

                        //----------------------------------
                        //----- Start of texture files -----
                        //----------------------------------

                        reader.BaseStream.Position = 0x12EAC;
                        for (int j = 0; j < i; j++)
                        {
                            reader.ReadBytes(reader.ReadInt32() - 4);
                        }

                        int targetSize = reader.ReadInt32() - 20;

                        //Skip over padding
                        reader.ReadBytes(8);

                        //Stores file data
                        using (MemoryStream ms = new MemoryStream(reader.ReadBytes(targetSize)))
                        {
                            Texture = Image.FromStream(ms);
                        }

                        break;
                    }

                    reader.ReadBytes(38); //Jump to next id
                }
            }
        }

        public void Offset(float x, float y, float z)
        {
            for (int i = 0; i < Vertices.GetLength(0); i++)
            {
                Vertices[i, 0] += x;
                Vertices[i, 1] += y;
                Vertices[i, 2] += z;
            }
        }

        public void Rotate(float angle, float ax, float ay, float az)
        {
            float theta = (float)(angle * Math.PI / 180f);
            for(int i = 0; i < Vertices.GetLength(0); i++)
            {
                float[] r = new float[] { ax, ay, az };
                float[] q = new float[] { 0.0f, 0.0f, 0.0f };

                float costheta = (float)Math.Cos(theta);
                float sintheta = (float)Math.Sin(theta);

                q[0] += (costheta + (1 - costheta) * r[0] * r[0]) * Vertices[i, 0];
                q[0] += ((1 - costheta) * r[0] * r[1] - r[2] * sintheta) * Vertices[i, 1];
                q[0] += ((1 - costheta) * r[0] * r[2] + r[1] * sintheta) * Vertices[i, 2];

                q[1] += ((1 - costheta) * r[0] * r[1] + r[2] * sintheta) * Vertices[i, 0];
                q[1] += (costheta + (1 - costheta) * r[1] * r[1]) * Vertices[i, 1];
                q[1] += ((1 - costheta) * r[1] * r[2] - r[0] * sintheta) * Vertices[i, 2];

                q[2] += ((1 - costheta) * r[0] * r[2] - r[1] * sintheta) * Vertices[i, 0];
                q[2] += ((1 - costheta) * r[1] * r[2] + r[0] * sintheta) * Vertices[i, 1];
                q[2] += (costheta + (1 - costheta) * r[2] * r[2]) * Vertices[i, 2];

                Vertices[i, 0] = q[0];
                Vertices[i, 1] = q[1];
                Vertices[i, 2] = q[2];
            }
        }
    }
}